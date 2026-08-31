using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionDetector : MonoBehaviour
{
    private IInteractable interactableInrange = null;
    public GameObject interactionIcon;
    void Start()
    {
        interactionIcon.SetActive(false);
    }

    public void onInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            interactableInrange?.Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable) && interactable.CanInteract())
        {
            interactableInrange = interactable;
            interactionIcon.SetActive(true);
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IInteractable interactable) && interactable == interactableInrange)
        {
            interactableInrange = null;
            interactionIcon.SetActive(false);
        }
    }
}
