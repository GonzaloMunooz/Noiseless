using UnityEngine;

/// <summary>
/// Maquina de estados del jugador. Decide en que estado esta mirando su velocidad real
/// y si esta agachado. Estados posibles:
/// "Sprinting", "Walking", "CrouchWalking", "Standing" y "Crouching".
/// Se puede poner en el Player o en cualquier hijo suyo (busca las referencias en el padre).
/// </summary>
[DefaultExecutionOrder(-100)] // Se ejecuta antes que PlayerController para que su estado este al dia
public class PlayerStateMachine : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("PlayerController del jugador. Si se deja vacio se busca en el padre.")]
    [SerializeField] private PlayerController player;
    [Tooltip("CharacterController del jugador (para leer su velocidad). Si se deja vacio se busca en el padre.")]
    [SerializeField] private CharacterController controller;

    private string currentState = "Standing";

    // Lo pueden leer otros scripts para saber en que estado estamos
    public string CurrentState
    {
        get { return currentState; }
    }

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
        }

        if (controller == null)
        {
            controller = GetComponentInParent<CharacterController>();
        }
    }

    private void Update()
    {
        currentState = ReadState();
    }

    // Mas rapido que walkSpeed -> Sprinting
    // Moviendose de pie -> Walking
    // Moviendose agachado -> CrouchWalking
    // Parado de pie -> Standing
    // Parado agachado -> Crouching
    private string ReadState()
    {
        bool crouching = false;
        if (player != null)
        {
            crouching = player.IsCrouching;
        }

        float speed = ReadHorizontalSpeed();

        // Parado (velocidad practicamente 0)
        if (speed <= 0.01f)
        {
            if (crouching)
            {
                return "Crouching";
            }

            return "Standing";
        }

        // Moviendose por encima de la velocidad de andar
        if (player != null && speed > player.WalkSpeed + 0.25f)
        {
            return "Sprinting";
        }

        if (crouching)
        {
            return "CrouchWalking";
        }

        return "Walking";
    }

    private float ReadHorizontalSpeed()
    {
        if (controller == null)
        {
            return 0f;
        }

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }
}
