using System.Linq;
using UnityEngine;
using Fishy.World;
using Fishy.Mision;
using Fishy.Net;

namespace Fishy.Detective
{
    /// <summary>
    /// Coloca este componente en el GameObject de la zona del modo detective.
    /// Requiere un Collider2D con isTrigger = true.
    /// </summary>
    public class DetectiveLauncher : MonoBehaviour
    {
        [Header("Caso")]
        [Tooltip("caso_id en el backend (ej: DC_CASO_01). Se intenta cargar de ahí primero. " +
                 "Tiene que existir en la base: si no, el caso se juega desde el respaldo local " +
                 "y el resultado NO se guarda.")]
        [SerializeField] private string casoId = "DC_CASO_01";
        [Tooltip("Respaldo local si no hay sesión/backend: Resources/<esto>.json.")]
        [SerializeField] private string resourcePath = "detective_caso_01";

        [Header("Misión")]
        [Tooltip("Desafío del panel de misión activa que este caso desbloquea/completa. Opcional.")]
        [SerializeField] private DesafioData desafioAsociado;

        [Header("Repetición")]
        [Tooltip("Una vez completado el caso, el NPC deja de activarse. Desmarcar solo " +
                 "para poder probar el caso una y otra vez en el editor.")]
        [SerializeField] private bool bloquearSiYaCompletado = true;

        [Header("Referencias (se crean solas si están vacías)")]
        [SerializeField] private DetectiveCaseManager caseManager;
        [SerializeField] private DetectiveUI          detectiveUI;

        private bool _enCurso   = false;
        private bool _completado = false;

        /// <summary>Respaldo local del "ya completado", por si no hay backend. Se
        /// separa por partida para que el avance de un perfil no oculte el NPC en
        /// otro (mismo riesgo que corre MissionManager con sus PlayerPrefs).</summary>
        private const string PrefsKeyPrefix = "Fishy.Detective.Completado.";

        private string PrefsKey
        {
            get
            {
                var api = ApiManager.Instance;
                string ambito = api != null && api.PartidaId.HasValue
                    ? api.PartidaId.Value.ToString()
                    : "local";
                return $"{PrefsKeyPrefix}{ambito}.{casoId}";
            }
        }

        // ── Estado inicial ─────────────────────────────────────────────

        private void Start()
        {
            _completado = PlayerPrefs.GetInt(PrefsKey, 0) == 1;

            // La verdad la tiene la base: si esta partida ya tiene progreso de este
            // caso, el NPC no se activa aunque los PlayerPrefs digan otra cosa (por
            // ejemplo, si el niño jugó antes en otro equipo).
            var api = ApiManager.Instance;
            if (api == null || api.IsLocalMode || !api.IsLoggedIn || api.PartidaId == null) return;

            api.ObtenerProgresoDetective(
                onSuccess: progresos =>
                {
                    if (progresos != null && progresos.Any(pr => pr.caso_id == casoId))
                        MarcarCompletado();
                },
                onError: e => Debug.LogWarning(
                    $"[Detective] No se pudo consultar el progreso del caso {casoId}: {e}"));
        }

        // ── Trigger ──────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_enCurso) return;
            if (!other.CompareTag("Player")) return;

            if (bloquearSiYaCompletado && YaCompletado())
            {
                Debug.Log($"[Detective] El caso {casoId} ya fue completado; el NPC no se activa.");
                return;
            }

            _enCurso = true;
            DetectiveCaseLoader.LoadAsync(casoId, resourcePath, OnCasoCargado);
        }

        // ── Flujo principal ──────────────────────────────────────────────────

        private void OnCasoCargado(DetectiveCase caso)
        {
            if (caso == null)
            {
                Debug.LogError("[Detective] No se pudo cargar el caso (ni backend ni local). Abortando.");
                _enCurso = false;
                return;
            }

            // 1. Obtener o crear el manager
            if (caseManager == null)
                caseManager = gameObject.AddComponent<DetectiveCaseManager>();

            caseManager.CargarCaso(caso);

            if (desafioAsociado != null)
                MissionManager.GetOrCreate().RegistrarDesafioDisponible(desafioAsociado);

            // 2. Bloquear movimiento de Otto (mismo patrón que PhoneChatLauncher)
            var otto = GameObject.FindWithTag("Player");
            var controller = otto?.GetComponent<OttoController>();
            controller?.DisableMovement();

            // 3. Iniciar la UI: primero el ritual de permiso (Otto pide, el NPC
            //    autoriza explícitamente), y solo al continuar se abre la
            //    conversación observada.
            if (detectiveUI == null)
                detectiveUI = DetectiveUI.GetOrCreate();

            detectiveUI.Inicializar(
                manager:   caseManager,
                onCerrar:  () => TerminarModoDetective(controller),
                onRepetir: () => ReiniciarCaso()
            );

            detectiveUI.MostrarPermiso(caso, onContinuar: () => detectiveUI.MostrarConversacion());
        }

        /// <summary>Vuelve a jugar el mismo caso sin repetir el ritual de permiso:
        /// el NPC ya autorizó, y el caso no se da por cerrado hasta que el jugador
        /// pulse "Continuar".</summary>
        private void ReiniciarCaso()
        {
            DetectiveCaseLoader.LoadAsync(casoId, resourcePath, caso =>
            {
                if (caso == null) return;
                caseManager.CargarCaso(caso);
                detectiveUI.MostrarConversacion();
            });
        }

        private void TerminarModoDetective(OttoController controller)
        {
            controller?.EnableMovement();
            _enCurso = false;
            MarcarCompletado();

            if (desafioAsociado != null)
                MissionManager.Instance?.CompletarDesafio(desafioAsociado);

            Debug.Log("[Detective] Modo detective terminado.");
        }

        /// <summary>Se relee PlayerPrefs en vez de confiar solo en lo que se leyó en
        /// Start: si el login terminó después de cargar la escena, la clave de Start
        /// era la genérica ("local") y no la de esta partida.</summary>
        private bool YaCompletado() =>
            _completado || PlayerPrefs.GetInt(PrefsKey, 0) == 1;

        /// <summary>Deja el caso cerrado para este NPC. Idempotente: repetir el caso
        /// y volver a terminarlo no cambia nada.</summary>
        private void MarcarCompletado()
        {
            if (_completado) return;
            _completado = true;
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
        }
    }
}
