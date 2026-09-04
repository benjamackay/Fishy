using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;
using Fishy.Mision;

namespace Fishy.Chat
{
    /// <summary>
    /// HDU-8 — Orquesta una sesión de chat de prevención.
    ///
    /// Recorre las conversaciones de la zona, muestra el historial (mensaje neutro
    /// + mensaje de riesgo, sin etiquetar), presenta 2-3 opciones por mensaje de
    /// riesgo y ramifica según la elección. Al terminar, calcula el porcentaje de
    /// respuestas seguras y muestra el estado emocional de Otto.
    /// </summary>
    public class ChatModuleController : MonoBehaviour
    {
        public static ChatModuleController Instance { get; private set; }

        [System.Serializable]
        public class MoodTier
        {
            [Tooltip("Porcentaje mínimo de respuestas seguras para este estado.")]
            public float minSafePercent;
            public OttoMood mood;
            public string emoji = "🙂";
            [TextArea] public string message;
            [Tooltip("Trigger del Animator de Otto (opcional).")]
            public string animatorTrigger;
            public Color messageColor = Color.white;
        }

        [Header("Estados emocionales de Otto (según % de respuestas seguras)")]
        public List<MoodTier> moodTiers = new List<MoodTier>
        {
            new MoodTier
            {
                minSafePercent = 70f, mood = OttoMood.Seguro, emoji = "😌",
                message = "Otto se siente seguro", animatorTrigger = "Seguro",
                messageColor = new Color(0.55f, 0.9f, 0.6f)
            },
            new MoodTier
            {
                minSafePercent = 0f, mood = OttoMood.Preocupado, emoji = "😟",
                message = "Otto está preocupado. Repasemos cómo cuidarte en internet.",
                animatorTrigger = "Preocupado", messageColor = new Color(0.95f, 0.7f, 0.4f)
            },
        };

        [Header("Ritmo")]
        [Tooltip("Segundos entre mensajes encadenados del NPC (0 = inmediato).")]
        public float autoAdvanceDelay = 0.6f;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Se dispara al cerrar la sesión (normal o abortada), con el % de
        /// respuestas seguras acumulado. Lo usan componentes zonales (p.ej.
        /// <c>BosqueDesconocidosNPC</c>) para decidir si la interacción cuenta
        /// como éxito, sin que ChatModuleController necesite saber nada de zonas.
        /// </summary>
        public event Action<float> OnSesionCerrada;

        private ChatModuleUI ui;
        private OttoMoodController ottoMood;
        private readonly Queue<ChatConversation> queue = new Queue<ChatConversation>();
        private ChatConversation conversation;
        private ChatBackendLogger logger;
        private bool reportToBackend;
        private bool firstLineLogged;
        private DesafioData desafioActual;

        private int safeCount;
        private int unsafeCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static ChatModuleController GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("ChatModuleController");
                Instance = go.AddComponent<ChatModuleController>();
            }
            return Instance;
        }

        // ── Apertura de la sesión ──────────────────────────────────────────────
        public void OpenSession(ChatConversation single, OttoMoodController otto = null, bool report = false, DesafioData desafio = null)
            => OpenSession(new List<ChatConversation> { single }, otto, report, desafio);

        public void OpenSession(IList<ChatConversation> conversations, OttoMoodController otto = null, bool report = false, DesafioData desafio = null)
        {
            if (IsActive || conversations == null || conversations.Count == 0) return;

            IsActive = true;
            // Habilitar reporte automáticamente si hay sesión activa en el backend.
            reportToBackend = report || AutoReportEnabled();
            ottoMood = otto != null ? otto : FindAnyObjectByType<OttoMoodController>();
            safeCount = 0;
            unsafeCount = 0;

            desafioActual = desafio;
            if (desafioActual != null)
                MissionManager.GetOrCreate().RegistrarDesafioDisponible(desafioActual);

            queue.Clear();
            foreach (var c in conversations)
                if (c != null) queue.Enqueue(c);

            ui = ChatModuleUI.GetOrCreate();
            ui.Open(queue.Peek().contactName, onCloseRequested: AbortSession);

            StartNextConversation();
        }

        /// <summary>
        /// Devuelve true si hay un ApiManager con sesión y partida activa,
        /// lo que significa que podemos enviar respuestas al backend automáticamente.
        /// </summary>
        private static bool AutoReportEnabled()
        {
            var api = ApiManager.Instance;
            return api != null && api.IsLoggedIn && api.PartidaId.HasValue;
        }

        private void StartNextConversation()
        {
            if (queue.Count == 0)
            {
                EndSession();
                return;
            }

            conversation = queue.Dequeue();
            firstLineLogged = false;
            logger = new ChatBackendLogger();
            if (reportToBackend)
                logger.Begin(conversation.contactName, conversation.zoneId, conversation.categoriaRiesgo);

            EnterNode(conversation.StartNode);
        }

        // ── Recorrido del grafo ────────────────────────────────────────────────
        /// <summary>
        /// Avanza al nodo indicado, distinguiendo los dos motivos por los que
        /// puede no haber a dónde seguir:
        ///  • id vacío → la rama termina ahí; es un final legítimo.
        ///  • id que no existe en el grafo → falta contenido por cargar (la
        ///    pregunta vive en otro <c>escenario_id</c> que ningún launcher pidió).
        ///    Se avisa por consola y se cierra igual, para no dejar el chat abierto
        ///    en el backend. Si más adelante se engancha ese contenido, el nodo
        ///    resuelve y la advertencia desaparece sola: la guarda no lo tapa.
        /// </summary>
        private void Avanzar(string nextNodeId)
        {
            if (string.IsNullOrEmpty(nextNodeId))
            {
                EndCurrentConversation("fin_de_rama");
                return;
            }

            var siguiente = conversation.GetNode(nextNodeId);
            if (siguiente == null)
            {
                Debug.LogWarning(
                    $"[ChatModule] '{conversation.contactName}' (zona {conversation.zoneId}) " +
                    $"apunta a '{nextNodeId}', que no está cargado en esta conversación. " +
                    "Se cierra el chat ahí. Revisa que el escenario_id que contiene esa " +
                    "pregunta esté asignado a algún launcher de la escena.");
                EndCurrentConversation($"contenido_faltante:{nextNodeId}");
                return;
            }

            EnterNode(siguiente);
        }

        private void EnterNode(ChatNode node)
        {
            if (node == null)
            {
                EndCurrentConversation("nodo_nulo");
                return;
            }

            ui.PostNpc(node.text, node.isSystem);

            // ── Registro best-effort en el backend ─────────────────────────────
            // node.id == pregunta_banco_id del banco (ej. "HDU2_NPC01_F2_Q01").
            if (!firstLineLogged)
            {
                logger.LogStart(node.text, preguntaBancoId: node.id);
                firstLineLogged = true;
            }
            else if (node.HasOptions)
            {
                logger.LogRequest(node.text, node.ToOpciones(), preguntaBancoId: node.id);
            }

            if (node.closesChat)
            {
                logger.LogEnd(node.text);
                EndCurrentConversation("cierre_narrativo");
                return;
            }

            if (node.HasOptions)
            {
                var texts = new List<string>(node.options.Count);
                foreach (var o in node.options) texts.Add(o.text);
                ui.ShowOptions(texts, idx => OnOption(node, idx));
                return;
            }

            if (!string.IsNullOrEmpty(node.nextNodeId))
            {
                StartCoroutine(AdvanceAfterDelay(node.nextNodeId));
                return;
            }

            EndCurrentConversation("fin_de_rama");
        }

        private IEnumerator AdvanceAfterDelay(string nextNodeId)
        {
            if (autoAdvanceDelay > 0f)
                yield return new WaitForSecondsRealtime(autoAdvanceDelay);
            if (!IsActive) yield break;
            Avanzar(nextNodeId);
        }

        private void OnOption(ChatNode node, int index)
        {
            if (index < 0 || index >= node.options.Count) return;
            var option = node.options[index];

            ui.PostChild(option.text);
            ui.ClearOptions();

            if (option.CountsForScore)
            {
                if (option.safety == OptionSafety.Safe) safeCount++;
                else unsafeCount++;
            }

            // Registra la respuesta del jugador vinculada al nodo-pregunta que la
            // originó y a la opción exacta del banco (la que lleva el puntaje).
            logger.LogChoice(option.text, option.QualityKey,
                preguntaBancoId: node.id, opcionBancoId: option.bancoOptionId);

            Avanzar(option.nextNodeId);
        }

        /// <summary>
        /// Cierra la conversación actual y pasa a la siguiente de la cola.
        ///
        /// Registra el cierre en el backend antes de soltar el logger. Sin esto el
        /// chat quedaba con <c>fecha_termino</c> en NULL cada vez que la
        /// conversación no terminaba en un nodo con <c>closesChat</c> — el caso de
        /// las ramas que apuntan a contenido de otro escenario. <c>LogEnd</c> es
        /// idempotente, así que si el nodo de cierre ya lo registró con su texto
        /// narrativo, esta llamada no pisa nada.
        /// </summary>
        private void EndCurrentConversation(string motivo)
        {
            logger?.LogEnd(motivo);
            if (queue.Count > 0) StartNextConversation();
            else EndSession();
        }

        // ── Cierre + estado emocional de Otto ──────────────────────────────────
        private void EndSession()
        {
            float percent = SafePercent();
            var tier = PickTier(percent);

            Debug.Log($"[ChatModule] Sesión cerrada. Seguras={safeCount}, Inseguras={unsafeCount}, " +
                      $"%seguras={percent:0}, estado={tier.mood}.");

            if (ottoMood != null) ottoMood.SetMood(tier.mood, tier.animatorTrigger);

            ui.ShowMood(tier.emoji, tier.message, tier.messageColor, onClose: CloseModule);
        }

        /// <summary>Porcentaje de respuestas seguras (sobre las que cuentan).</summary>
        public float SafePercent()
        {
            int total = safeCount + unsafeCount;
            return total > 0 ? safeCount * 100f / total : 100f;
        }

        private MoodTier PickTier(float percent)
        {
            MoodTier best = null;
            foreach (var tier in moodTiers)
            {
                if (percent >= tier.minSafePercent &&
                    (best == null || tier.minSafePercent > best.minSafePercent))
                    best = tier;
            }
            return best ?? moodTiers[moodTiers.Count - 1];
        }

        private void AbortSession()
        {
            // El niño/a cierra el chat antes de terminar: cerrar el backend también.
            logger?.LogEnd("abortado_por_jugador");
            CloseModule();
        }

        private void CloseModule()
        {
            float percentAlCierre = SafePercent();

            StopAllCoroutines();
            if (ui != null) ui.Hide();
            IsActive = false;
            conversation = null;
            logger = null;
            queue.Clear();

            if (desafioActual != null)
            {
                MissionManager.Instance?.CompletarDesafio(desafioActual);
                desafioActual = null;
            }

            OnSesionCerrada?.Invoke(percentAlCierre);
        }
    }
}
