using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRange = 2f;

    public void Interact()
    {
        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                interactionRange
            );

        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact(gameObject);

                break;
            }
        }
    }
}