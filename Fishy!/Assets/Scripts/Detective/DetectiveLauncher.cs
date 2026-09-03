using UnityEngine;
using Fishy.World;
using Fishy.Mision;

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

        [Header("Referencias (se crean solas si están vacías)")]
        [SerializeField] private DetectiveCaseManager caseManager;
        [SerializeField] private DetectiveUI          detectiveUI;

        private bool _enCurso = false;

        // ── Trigger ──────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_enCurso) return;
            if (!other.CompareTag("Player")) return;

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
                onRepetir: () => ReiniciarCaso(controller)
            );

            detectiveUI.MostrarPermiso(caso, onContinuar: () => detectiveUI.MostrarConversacion());
        }

        private void ReiniciarCaso(OttoController controller)
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

            if (desafioAsociado != null)
                MissionManager.Instance?.CompletarDesafio(desafioAsociado);

            Debug.Log("[Detective] Modo detective terminado.");
        }
    }
}
