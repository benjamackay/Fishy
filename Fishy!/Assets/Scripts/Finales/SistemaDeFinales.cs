using System;
using System.Collections;
using System.Collections.Generic;
using Fishy.Chat;
using Fishy.Net;
using Fishy.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fishy.Finales
{
    /// <summary>
    /// Sistema de Finales Narrativos (HDU-09): al terminar la Misión 6, por la rama
    /// que sea, muestra el Final A, B o C según el porcentaje de decisiones seguras
    /// de las tres zonas, y entrega su recompensa.
    ///
    /// <b>Todo lo que es contenido sale del banco</b> (<c>finales_narrativos</c>): los
    /// textos, los umbrales, la recompensa y qué escenarios lo disparan. Aquí solo
    /// está el cuándo y el cómo.
    ///
    /// <b>Cuándo.</b> Escucha <see cref="ChatModuleController.OnCierreNarrativo"/>:
    /// solo salta al llegar a un nodo FIN de un escenario de
    /// <c>disparan_al_terminar</c>, así que un chat abandonado a medias no dispara
    /// nada. Espera a que el chat y el zoom del celular terminen para no aparecer
    /// encima.
    ///
    /// <b>El porcentaje</b> lo calcula el backend (<c>/decisiones-seguras/</c>) a
    /// partir de lo guardado, porque la aventura se juega en varias sesiones. Antes
    /// de pedirlo se vacía la cola de guardado: la decisión de la Misión 6 se acaba
    /// de tomar y si no subió, no contaría. Sin servidor se usa lo contado en esta
    /// ejecución (<see cref="ChatModuleController.SegurasEnLaSesion"/>).
    ///
    /// <b>Cómo se ve.</b> Usa el mismo panel de diálogo de los NPCs neutros, que ya
    /// está en la escena: se toma prestado del primer NPC que lo tenga. Una línea por
    /// vez, se avanza con E o con clic, como cualquier diálogo neutro.
    /// </summary>
    [DisallowMultipleComponent]
    public class SistemaDeFinales : MonoBehaviour
    {
        public static SistemaDeFinales Instance { get; private set; }

        [Tooltip("Segundos máximos para subir la cola de guardado antes de pedir el porcentaje.")]
        [Min(0f)] public float topeGuardado = 5f;

        [Tooltip("Segundos máximos de espera a la respuesta del servidor.")]
        [Min(0f)] public float topeServidor = 5f;

        [Tooltip("Segundos entre letras al escribir cada línea.")]
        [Min(0f)] public float velocidadTecleo = 0.03f;

        [Tooltip("Escribir en consola qué final se eligió y por qué.")]
        public bool verboseLogs = true;

        /// <summary>Se dispara después de mostrar el final, con su id (FINAL_A/B/C).</summary>
        public event Action<string> OnFinalMostrado;

        private bool _enCurso;

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<SistemaDeFinales>() != null) return;
            new GameObject(nameof(SistemaDeFinales)).AddComponent<SistemaDeFinales>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()  => ChatModuleController.OnCierreNarrativo += AlCerrarNarrativa;
        private void OnDisable() => ChatModuleController.OnCierreNarrativo -= AlCerrarNarrativa;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Reglas (públicas para poder probarlas sin escena) ─────────────────

        /// <summary>
        /// True si cerrar el nodo <paramref name="preguntaId"/> termina la Misión 6:
        /// es un FIN del banco y su escenario está en <c>disparan_al_terminar</c>.
        /// </summary>
        public static bool TerminaLaAventura(BancoRaiz banco, string preguntaId)
        {
            if (banco?.preguntas == null || banco.finales_narrativos?.disparan_al_terminar == null)
                return false;

            foreach (var p in banco.preguntas)
            {
                if (p == null || p.id != preguntaId) continue;
                return (p.es_fin_de_npc || p.es_fin_de_zona)
                       && banco.finales_narrativos.disparan_al_terminar.Contains(p.escenario_id);
            }
            return false;
        }

        /// <summary>
        /// El final que corresponde a <paramref name="porcentaje"/>: el de umbral más
        /// alto que se alcance. Sin porcentaje (nadie contó ninguna decisión) va el
        /// del medio: no hay con qué premiar ni con qué marcar, y el B es el que no
        /// afirma ninguna de las dos cosas.
        /// </summary>
        public static FinalNarrativo ElegirFinal(IList<FinalNarrativo> finales, float? porcentaje)
        {
            if (finales == null || finales.Count == 0) return null;

            var ordenados = new List<FinalNarrativo>(finales);
            ordenados.RemoveAll(f => f == null);
            if (ordenados.Count == 0) return null;
            ordenados.Sort((a, b) => b.porcentaje_minimo.CompareTo(a.porcentaje_minimo));

            if (porcentaje == null) return ordenados[ordenados.Count / 2];

            foreach (var f in ordenados)
                if (porcentaje.Value >= f.porcentaje_minimo) return f;

            return ordenados[ordenados.Count - 1];
        }

        /// <summary>Nombre que se muestra sobre la línea: "Huemul (por radio)".</summary>
        public static string Hablante(LineaDeFinal linea)
        {
            if (linea == null) return "";
            return linea.por_radio ? $"{linea.npc_nombre} (por radio)" : linea.npc_nombre;
        }

        // ── Flujo ─────────────────────────────────────────────────────────────

        private void AlCerrarNarrativa(string preguntaId)
        {
            if (_enCurso) return;
            if (!TerminaLaAventura(BancoPreguntasLoader.Load(), preguntaId)) return;
            StartCoroutine(Mostrar());
        }

        private IEnumerator Mostrar()
        {
            _enCurso = true;

            // 1. Que se cierre el chat y vuelva la cámara. Con tope: si algo deja el
            //    contador colgado, el final igual tiene que salir.
            float tope = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < tope &&
                   ((ChatModuleController.Instance != null && ChatModuleController.Instance.IsActive)
                    || Phone.PhoneChatLauncher.SecuenciasEnCurso > 0))
                yield return null;

            // 2. El porcentaje.
            float? porcentaje = null;
            string origen = "sin datos";
            yield return PedirPorcentaje((p, o) => { porcentaje = p; origen = o; });

            var banco = BancoPreguntasLoader.Load();
            FinalNarrativo final = ElegirFinal(banco?.finales_narrativos?.finales, porcentaje);
            if (final == null)
            {
                Debug.LogWarning("[Finales] El banco no trae finales_narrativos: no hay final que mostrar.");
                _enCurso = false;
                yield break;
            }

            if (verboseLogs)
                Debug.Log($"[Finales] {final.id} ({final.nombre}) con " +
                          $"{(porcentaje.HasValue ? porcentaje.Value.ToString("0.#") + " %" : "porcentaje desconocido")} " +
                          $"de decisiones seguras ({origen}).", this);

            // 3. Las líneas, y al final la recompensa.
            yield return MostrarLineas(final);
            EntregarRecompensa(final);

            OnFinalMostrado?.Invoke(final.id);
            _enCurso = false;
        }

        private IEnumerator PedirPorcentaje(Action<float?, string> listo)
        {
            var api = ApiManager.Instance;
            if (api != null && !api.IsLocalMode && api.IsLoggedIn && api.PartidaId.HasValue)
            {
                var cola = ColaDeCambios.Instance;
                if (cola != null)
                    yield return cola.Vaciar("finales", topeGuardado, reintentarSiFalla: true);

                bool respondio = false;
                DecisionesSegurasDto dto = null;
                api.ObtenerDecisionesSeguras(
                    onSuccess: d => { dto = d; respondio = true; },
                    onError: e =>
                    {
                        Debug.LogWarning($"[Finales] No se pudo pedir el porcentaje ({e}); " +
                                         "se usa lo contado en esta sesión.");
                        respondio = true;
                    });

                float tope = Time.realtimeSinceStartup + topeServidor;
                while (!respondio && Time.realtimeSinceStartup < tope) yield return null;

                if (dto != null && dto.porcentaje.HasValue)
                {
                    listo(dto.porcentaje, "servidor");
                    yield break;
                }
            }

            int seguras = ChatModuleController.SegurasEnLaSesion;
            int contadas = seguras + ChatModuleController.InsegurasEnLaSesion;
            if (contadas > 0) listo(100f * seguras / contadas, "esta sesión");
            else              listo(null, "sin decisiones contadas");
        }

        // ── Pantalla ──────────────────────────────────────────────────────────

        private IEnumerator MostrarLineas(FinalNarrativo final)
        {
            NPC prestado = BuscarPanel();
            if (prestado == null)
            {
                // Sin panel no hay dónde escribir, pero el final no puede perderse:
                // la recompensa se entrega igual y queda el registro en consola.
                Debug.LogWarning("[Finales] No hay ningún NPC con panel de diálogo en la escena; " +
                                 "el final se entrega sin mostrarse.");
                yield break;
            }

            GameObject panel  = prestado.dialoguePanel;
            TMP_Text nombre   = prestado.nameText;
            TMP_Text texto    = prestado.dialogueText;
            Image retrato     = prestado.portraitImage;

            var otto = FindAnyObjectByType<OttoController>();
            bool bloqueado = otto != null && otto.movementEnabled;
            if (bloqueado) otto.DisableMovement();

            Fishy.UI.DialogoNeutroSkin.Aplicar(panel, nombre, texto, retrato);
            if (retrato != null) retrato.enabled = false;   // son varios hablantes, sin cara
            panel.SetActive(true);

            var lineas = new List<(string quien, string que)>();
            foreach (var l in final.lineas) lineas.Add((Hablante(l), l.texto));
            if (!string.IsNullOrWhiteSpace(final.recompensa))
                lineas.Add(("", $"Otto recibe: {final.recompensa}"));

            foreach (var (quien, que) in lineas)
            {
                if (nombre != null)
                {
                    nombre.SetText(quien);
                    nombre.gameObject.SetActive(!string.IsNullOrEmpty(quien));
                }
                yield return Escribir(texto, que);
                yield return EsperarAvance();
            }

            if (texto != null) texto.SetText("");
            if (nombre != null) nombre.gameObject.SetActive(true);
            panel.SetActive(false);
            if (bloqueado && otto != null) otto.EnableMovement();
        }

        private IEnumerator Escribir(TMP_Text destino, string linea)
        {
            if (destino == null) yield break;
            destino.SetText("");
            yield return null;   // el E del paso anterior no salta la línea entera

            for (int i = 0; i < linea.Length; i++)
            {
                if (PidioAvanzar()) { destino.SetText(linea); yield return null; yield break; }
                destino.SetText(linea.Substring(0, i + 1));
                yield return new WaitForSeconds(velocidadTecleo);
            }
        }

        private static IEnumerator EsperarAvance()
        {
            yield return null;
            while (!PidioAvanzar()) yield return null;
        }

        private static bool PidioAvanzar()
        {
            return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        }

        private static NPC BuscarPanel()
        {
            foreach (var npc in FindObjectsByType<NPC>(FindObjectsInactive.Include))
                if (npc != null && npc.dialoguePanel != null && npc.dialogueText != null)
                    return npc;
            return null;
        }

        // ── Recompensa ────────────────────────────────────────────────────────

        private void EntregarRecompensa(FinalNarrativo final)
        {
            if (string.IsNullOrWhiteSpace(final.recompensa_item_id)) return;

            ItemData item = CatalogoItems.Buscar(final.recompensa_item_id);
            if (item == null)
            {
                Debug.LogWarning($"[Finales] '{final.id}' entrega '{final.recompensa_item_id}', " +
                                 "pero no hay ningún ItemData con ese id en Resources/Items.");
                return;
            }

            var inventario = InventoryManager.Instance;
            if (inventario == null) return;

            // Rejugar la Misión 6 no duplica la misma medalla. Un final distinto sí
            // entrega la suya: es otra recompensa.
            if (inventario.GetQuantity(item) > 0) return;
            inventario.AddItem(item, 1);
        }
    }
}
