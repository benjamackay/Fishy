using UnityEngine;

namespace Fishy
{
    // Tarea 4 — Integrar input del jugador con la respuesta del NPC (CA1, CA3, CA6)
    // Detecta cuando Otto esta cerca de un NPC y envia el evento al presionar E.
    public class PlayerInteractionController : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private NpcInteractable _nearbyNpc;

        private void Update()
        {
            if (Input.GetKeyDown(interactKey))
                TryInteract();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_nearbyNpc == null)
                _nearbyNpc = other.GetComponent<NpcInteractable>();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<NpcInteractable>() == _nearbyNpc)
                _nearbyNpc = null;
        }

        private void TryInteract()
        {
            if (_nearbyNpc == null) return;

            if (_nearbyNpc.IsInteracting)
                _nearbyNpc.AdvanceDialogue();
            else
                _nearbyNpc.StartInteraction();
        }
    }
}
