using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
            [Tooltip("Hoja de sprites de Otto dentro de Resources que reemplaza al " +
                     "emoji. Vacío, o si no se encuentra, se muestra el emoji.")]
            public string animacion;
        }

        [Header("Estados emocionales de Otto (según % de respuestas seguras)")]
        public List<MoodTier> moodTiers = new List<MoodTier>
        {
            new MoodTier
            {
                minSafePercent = 70f, mood = OttoMood.Seguro, emoji = "😌",
                message = "Otto se siente seguro", animatorTrigger = "Seguro",
                messageColor = new Color(0.55f, 0.9f, 0.6f),
                animacion = ChatUITheme.Animo.Seguro
            },
            new MoodTier
            {
                minSafePercent = 0f, mood = OttoMood.Preocupado, emoji = "😟",
                message = "Otto está preocupado. Repasemos cómo cuidarte en internet.",
                animatorTrigger = "Preocupado", messageColor = new Color(0.95f, 0.7f, 0.4f),
                animacion = ChatUITheme.Animo.Preocupado
            },
        };

        // Ritmo: entre mensajes encadenados y antes de la reacción de Otto, el juego
        // espera a que el niño/a toque "Continuar" o pulse E — ver EsperarContinuar().
        // Antes eran WaitForSeconds fijos e "iba demasiado rápido" para quien lee más
        // despacio; ahora el ritmo lo pone quien juega, no un número fijo.

        [Header("Respuestas")]
        [Tooltip("Barajar las respuestas posibles cada vez que se muestran, en vez de " +
                 "sacarlas siempre en el orden en que las escribió el equipo.\n\n" +
                 "Apagado, la respuesta segura cae siempre en el mismo sitio dentro de " +
                 "cada conversación, y al repetirla se puede acertar por posición sin " +
                 "leer. Barajar obliga a leer, que es justamente lo que la actividad " +
                 "quiere enseñar.\n\n" +
                 "Este componente se crea solo (GetOrCreate), así que para tocar esta " +
                 "casilla desde el Inspector hay que poner un ChatModuleController en " +
                 "la escena; si no, manda el valor de aquí.")]
        public bool aleatorizarOpciones = true;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Se dispara al cerrar la sesión (normal o abortada), con el % de
        /// respuestas seguras acumulado. Lo usan componentes zonales (p.ej.
        /// <c>BosqueDesconocidosNPC</c>) para decidir si la interacción cuenta
        /// como éxito, sin que ChatModuleController necesite saber nada de zonas.
        /// </summary>
        public event Action<float> OnSesionCerrada;

        /// <summary>
        /// Seguridad de la última respuesta que eligió el jugador en esta sesión, o null
        /// si cerró sin elegir ninguna. Se conserva hasta que se abre la siguiente sesión,
        /// así que quien se entera de que el chat terminó todavía puede leerla.
        ///
        /// La última y no un promedio: un reto se gana o se pierde por cómo TERMINA. Es la
        /// misma regla que usa el backend para el contador de rechazos, donde una duda
        /// seguida de un "no" cuenta como rechazo.
        /// </summary>
        public OptionSafety? UltimaEleccion { get; private set; }

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
            UltimaEleccion = null;

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

            // En qué orden se dibujan las respuestas. Se calcula una sola vez al entrar
            // al nodo porque lo usan dos cosas que no pueden discrepar: los botones y lo
            // que se le cuenta al backend. Volver a barajar para el registro mandaría un
            // orden que nadie vio.
            int[] orden = node.HasOptions
                ? OrdenDeOpciones(node.options.Count, aleatorizarOpciones)
                : null;

            // ── Registro best-effort en el backend ─────────────────────────────
            // node.id == pregunta_banco_id del banco (ej. "HDU2_NPC01_F2_Q01").
            if (!firstLineLogged)
            {
                logger.LogStart(node.text, preguntaBancoId: node.id);
                firstLineLogged = true;
            }
            else if (node.HasOptions)
            {
                logger.LogRequest(node.text, node.ToOpciones(orden), preguntaBancoId: node.id);
            }

            if (node.closesChat)
            {
                logger.LogEnd(node.text);
                EndCurrentConversation("cierre_narrativo");
                return;
            }

            if (node.HasOptions)
            {
                var texts = new List<string>(orden.Length);
                foreach (int opcion in orden) texts.Add(node.options[opcion].text);

                // Lo que devuelve la UI es el número de BOTÓN, no el de la opción:
                // `orden` es lo que los vuelve a juntar. Sin esta traducción, barajar
                // haría que elegir "bloquear" contara como otra cosa.
                ui.ShowOptions(texts, boton => OnOption(node, orden[boton]));
                return;
            }

            if (!string.IsNullOrEmpty(node.nextNodeId))
            {
                StartCoroutine(AdvanceAfterContinue(node.nextNodeId));
                return;
            }

            EndCurrentConversation("fin_de_rama");
        }

        private IEnumerator AdvanceAfterContinue(string nextNodeId)
        {
            yield return EsperarContinuar();
            if (!IsActive) yield break;
            Avanzar(nextNodeId);
        }

        /// <summary>
        /// Muestra "Continuar" y no sigue hasta que el niño/a lo toque o pulse E.
        /// Antes esto era un tiempo fijo —"iba demasiado rápido" para quien lee más
        /// despacio—; ahora el ritmo lo pone quien juega, igual que ya pasa con el
        /// diálogo de un NPC neutro (ver <see cref="NPC.Interact"/>).
        /// </summary>
        private IEnumerator EsperarContinuar()
        {
            bool avanzado = false;
            ui.ShowOptions(new[] { ChatUITheme.Textos.BotonContinuar }, _ => avanzado = true);

            // Un frame de gracia: si el E que disparó el paso anterior (elegir una
            // opción, o el Continuar de antes) todavía se lee como "presionado este
            // frame" en la primera vuelta, este Continuar se saltaría solo.
            yield return null;

            while (!avanzado)
            {
                if (!IsActive) yield break;
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    avanzado = true;
                else
                    yield return null;
            }

            ui.ClearOptions();
        }

        /// <summary>
        /// En qué orden salen las <paramref name="cantidad"/> respuestas de un nodo:
        /// devuelve, para cada botón, cuál opción de <c>node.options</c> le toca.
        ///
        /// Es una permutación aparte y <b>no se reordena <c>node.options</c></b>: una
        /// <see cref="ChatConversation"/> puede ser un asset del proyecto, y barajarle
        /// la lista en el editor dejaría el orden cambiado en disco. Además el orden
        /// original es el que escribió el equipo (<c>BancoPreguntasLoader</c> ordena por
        /// el campo <c>orden</c> del banco) y hay que poder volver a él apagando la
        /// casilla.
        ///
        /// Se vuelve a barajar en cada entrada al nodo, a propósito: si la conversación
        /// se repite, tampoco debería servir recordar dónde estaba la buena.
        /// </summary>
        public static int[] OrdenDeOpciones(int cantidad, bool aleatorizar)
        {
            var orden = new int[Mathf.Max(0, cantidad)];
            for (int i = 0; i < orden.Length; i++) orden[i] = i;

            if (!aleatorizar) return orden;

            // Fisher-Yates: cada permutación sale con la misma probabilidad. Barajar
            // "a ojo" (recorrer intercambiando con una posición cualquiera) no lo hace
            // y deja sesgos que, con 2 ó 3 opciones, se notan.
            for (int i = orden.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);   // Range(int) excluye el máximo
                int tmp = orden[i];
                orden[i] = orden[j];
                orden[j] = tmp;
            }
            return orden;
        }

        private void OnOption(ChatNode node, int index)
        {
            if (index < 0 || index >= node.options.Count) return;
            var option = node.options[index];

            ui.PostChild(option.text);
            ui.ClearOptions();

            UltimaEleccion = option.safety;

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
            // Un chat sin ninguna respuesta que evaluar —sólo narración, como el cierre
            // de una zona— no tiene estado de Otto que mostrar: con 0 respuestas el
            // porcentaje daba 100 y salía "Otto se siente seguro" tapando el último
            // mensaje. Se deja leer y se cierra con Continuar.
            if (safeCount + unsafeCount == 0)
            {
                Debug.Log("[ChatModule] Sesión cerrada sin respuestas que evaluar: " +
                          "no se muestra el estado de Otto.");
                ui.ShowOptions(new[] { ChatUITheme.Textos.BotonContinuar }, _ => CloseModule());
                return;
            }

            StartCoroutine(MostrarReaccionTrasEspera());
        }

        /// <summary>
        /// Espera a que el niño/a toque Continuar (o pulse E) antes de mostrar la
        /// reacción de Otto, para no taparle el último mensaje justo cuando termina
        /// de aparecer. Mismo mecanismo que <see cref="EsperarContinuar"/> usa entre
        /// mensajes encadenados.
        /// </summary>
        private IEnumerator MostrarReaccionTrasEspera()
        {
            yield return EsperarContinuar();
            if (!IsActive) yield break;

            float percent = SafePercent();
            var tier = PickTier(percent);

            Debug.Log($"[ChatModule] Sesión cerrada. Seguras={safeCount}, Inseguras={unsafeCount}, " +
                      $"%seguras={percent:0}, estado={tier.mood}.");

            if (ottoMood != null) ottoMood.SetMood(tier.mood, tier.animatorTrigger);

            ui.ShowMood(tier.emoji, tier.message, tier.messageColor, onClose: CloseModule,
                animacion: tier.animacion);
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
