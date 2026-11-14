using UnityEngine;

/// <summary>
/// Sonidos del jugador. Por ahora solo los pasos.
/// Suena cuando el PlayerStateMachine esta en Walking o Sprinting y el ritmo se calcula
/// por distancia recorrida: al correr se recorren mas metros por segundo, asi que suena mas veces.
/// Se puede poner en el Player o en cualquier hijo suyo (busca las referencias en el padre).
/// </summary>
[DisallowMultipleComponent]
public class PlayerSoundController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("PlayerStateMachine del jugador (para saber si esta andando o corriendo). Si se deja vacio se busca en el padre.")]
    [SerializeField] private PlayerStateMachine stateMachine;
    [Tooltip("CharacterController del jugador (para medir la distancia recorrida). Si se deja vacio se busca en el padre.")]
    [SerializeField] private CharacterController controller;
    [Tooltip("AudioSource de los pasos. Si se deja vacio se busca en este GameObject y, si no hay, se crea uno.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Pasos")]
    [Tooltip("Clip del paso (Assets/PlayerFolder/Sounds/PlayerStep.mp3.mp3).")]
    [SerializeField] private AudioClip stepClip;
    [Tooltip("Volumen del paso (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float stepVolume = 0.6f;
    [Tooltip("Distancia en metros entre paso y paso. Cuanto mas bajo, mas pasos suenan.")]
    [SerializeField] private float stepDistance = 1.5f;

    private float distanceSinceStep;

    private void Awake()
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponentInParent<PlayerStateMachine>();
        }

        if (controller == null)
        {
            controller = GetComponentInParent<CharacterController>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D: los pasos del propio jugador siempre se oyen igual
        }
    }

    private void Update()
    {
        // Los pasos solo suenan andando o corriendo
        if (IsWalkingState() == false)
        {
            distanceSinceStep = 0f;
            return;
        }

        float speed = ReadHorizontalSpeed();
        distanceSinceStep = distanceSinceStep + speed * Time.deltaTime;

        float distanceForStep = Mathf.Max(0.1f, stepDistance);
        if (distanceSinceStep >= distanceForStep)
        {
            distanceSinceStep = distanceSinceStep - distanceForStep;
            PlayStep();
        }
    }

    // Andando o corriendo segun el estado actual del jugador
    private bool IsWalkingState()
    {
        if (stateMachine == null)
        {
            return false;
        }

        string state = stateMachine.CurrentState;

        if (state == "Walking")
        {
            return true;
        }

        if (state == "Sprinting")
        {
            return true;
        }

        return false;
    }

    // Velocidad horizontal real del jugador (ignoramos la gravedad del CharacterController)
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

    private void PlayStep()
    {
        if (audioSource == null || stepClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(stepClip, stepVolume);
    }
}
