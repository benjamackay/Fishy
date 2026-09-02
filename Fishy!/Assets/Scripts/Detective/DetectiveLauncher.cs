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

            IniciarModoDetective();
        }

        // ── Flujo principal ──────────────────────────────────────────────────

        private void IniciarModoDetective()
        {
            _enCurso = true;

            // 1. Cargar el caso desde Resources
            DetectiveCase caso = DetectiveCaseLoader.Load(resourcePath);
            if (caso == null)
            {
                Debug.LogError("[Detective] No se pudo cargar el caso. Abortando.");
                _enCurso = false;
                return;
            }

            // 2. Obtener o crear el manager
            if (caseManager == null)
                caseManager = gameObject.AddComponent<DetectiveCaseManager>();

            caseManager.CargarCaso(caso);

            if (desafioAsociado != null)
                MissionManager.GetOrCreate().RegistrarDesafioDisponible(desafioAsociado);

            // 3. Bloquear movimiento de Otto (mismo patrón que PhoneChatLauncher)
            var otto = GameObject.FindWithTag("Player");
            var controller = otto?.GetComponent<OttoController>();
            controller?.DisableMovement();

            // 4. Mostrar mensaje de permiso en consola (la UI real viene después)
            Debug.Log($"[Detective] NPC dice: \"{caso.mensajePermiso}\"");

            // 5. Iniciar la UI
            if (detectiveUI == null)
                detectiveUI = DetectiveUI.GetOrCreate();

            detectiveUI.Inicializar(
                manager:   caseManager,
                onCerrar:  () => TerminarModoDetective(controller),
                onRepetir: () => ReiniciarCaso(controller)
            );

            detectiveUI.MostrarConversacion();
        }

        private void ReiniciarCaso(OttoController controller)
        {
            DetectiveCase caso = DetectiveCaseLoader.Load(resourcePath);
            caseManager.CargarCaso(caso);
            detectiveUI.MostrarConversacion();
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