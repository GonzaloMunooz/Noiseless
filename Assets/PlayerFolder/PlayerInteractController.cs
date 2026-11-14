using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sistema de interaccion del jugador.
/// Los InteractableObject se registran aqui cuando el jugador entra en su trigger y se
/// quitan cuando sale, asi que solo se puede interactuar con objetos dentro del BoxCollider.
/// Con la tecla E se interactua con el objeto registrado mas cercano.
/// Se pone en el Player.
/// </summary>
public class PlayerInteractController : MonoBehaviour
{
    [Header("Input (PlayerInputActions)")]
    [Tooltip("Asset PlayerInputActions. Mapa 'Gameplay' con la accion Interact.")]
    [SerializeField] private InputActionAsset inputActions;

    private InputAction interactAction;

    // Objetos cuyo trigger estamos tocando ahora mismo
    private List<InteractableObject> interactablesInRange = new List<InteractableObject>();

    private InteractableObject currentInteractable;

    // El mas cercano: es el que muestra la etiqueta "[E]" y el que responde a la tecla.
    // Lo leen los InteractableObject para saber si les toca mostrarse.
    public InteractableObject CurrentInteractable
    {
        get { return currentInteractable; }
    }

    private void Awake()
    {
        SetupInput();
    }

    private void SetupInput()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("PlayerInteractController: falta asignar el asset PlayerInputActions.", this);
            return;
        }

        InputActionMap gameplay = inputActions.FindActionMap("Gameplay");
        if (gameplay == null)
        {
            Debug.LogWarning("PlayerInteractController: el asset no tiene el mapa 'Gameplay'.", this);
            return;
        }

        interactAction = gameplay.FindAction("Interact");
    }

    // Activo solo esta accion, igual que hace PlayerController con las suyas
    private void OnEnable()
    {
        interactAction?.Enable();
    }

    private void OnDisable()
    {
        interactAction?.Disable();
    }

    private void Update()
    {
        currentInteractable = FindClosest();

        if (interactAction == null || currentInteractable == null)
        {
            return;
        }

        if (interactAction.WasPressedThisFrame())
        {
            currentInteractable.Interact();
        }
    }

    // Lo llaman los InteractableObject desde OnTriggerEnter
    public void AddInteractable(InteractableObject interactable)
    {
        if (interactable == null || interactablesInRange.Contains(interactable))
        {
            return;
        }

        interactablesInRange.Add(interactable);
    }

    // Lo llaman los InteractableObject desde OnTriggerExit
    public void RemoveInteractable(InteractableObject interactable)
    {
        interactablesInRange.Remove(interactable);
    }

    // El mas cercano de la lista, midiendo en horizontal
    private InteractableObject FindClosest()
    {
        InteractableObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (InteractableObject interactable in interactablesInRange)
        {
            if (interactable == null)
            {
                continue;
            }

            Vector3 difference = interactable.transform.position - transform.position;
            difference.y = 0f;

            float distance = difference.magnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = interactable;
            }
        }

        return closest;
    }
}
