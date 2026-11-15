using UnityEngine;

/// <summary>
/// Ruido que hace el jugador, para que el Ciego lo pueda oir.
/// Locomocion: el radio sale de la velocidad horizontal real, no del estado, asi que
/// empujar una pared (no moverse) no hace ruido. Al parar, el radio baja a 0 en noiseHoldTime.
/// Respiracion: radio fijo por ahora; la fatiga y la stamina llegan en el hito 4.
/// Este script solo ofrece datos de lectura: el que decide si se oye es BlindHearing.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("PlayerController del jugador, de donde se leen las velocidades. Si se deja vacio se busca en el padre.")]
    [SerializeField] private PlayerController playerController;

    [Header("Emision")]
    [Tooltip("Transform del pecho desde el que nace el ruido. Si se deja vacio se calcula con chestHeight.")]
    [SerializeField] private Transform chest;
    [Tooltip("Altura del pecho sobre la base del jugador cuando no hay Transform asignado.")]
    [SerializeField] private float chestHeight = 1.2f;
    [Tooltip("Tiempo que tarda el radio de pisadas en bajar a 0 despues de parar.")]
    [SerializeField] private float noiseHoldTime = 0.4f;

    [Header("Radios de locomocion")]
    [Tooltip("Radio de pisadas a la velocidad de agachado (CrouchWalkSpeed del PlayerController).")]
    [SerializeField] private float crouchRadius = 2.5f;
    [Tooltip("Radio de pisadas a la velocidad de andar (WalkSpeed del PlayerController).")]
    [SerializeField] private float walkRadius = 8f;
    [Tooltip("Radio de pisadas a la velocidad de correr (SprintSpeed del PlayerController).")]
    [SerializeField] private float runRadius = 14f;

    [Header("Respiracion")]
    [Tooltip("Radio de respiracion con la stamina llena.")]
    [SerializeField] private float baseBreathRadius = 1.5f;
    [Tooltip("Campo de prueba: simula que el jugador aguanta la respiracion. La tecla real llega mas adelante.")]
    [SerializeField] private bool holdBreathForTesting = false;

    private CharacterController controller;

    private float locomotionRadius;
    private float lastLocomotionRadius;
    private float stoppedTime;

    // Radio de pisadas actual (0 si lleva parado un rato)
    public float LocomotionRadius
    {
        get { return locomotionRadius; }
    }

    // Radio de respiracion actual (0 si esta aguantando el aire)
    public float BreathRadius
    {
        get
        {
            if (IsHoldingBreath)
            {
                return 0f;
            }

            return baseBreathRadius;
        }
    }

    // True si ahora mismo hay ruido de pisadas
    public bool MakesNoise
    {
        get { return locomotionRadius > 0f; }
    }

    // Punto del pecho desde el que nace el ruido
    public Vector3 EmitPosition
    {
        get
        {
            if (chest != null)
            {
                return chest.position;
            }

            return transform.position + Vector3.up * chestHeight;
        }
    }

    // Preparado para la futura mecanica de aguantar la respiracion
    public bool IsHoldingBreath
    {
        get { return holdBreathForTesting; }
        set { holdBreathForTesting = value; }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            Debug.LogWarning("PlayerNoiseEmitter no encuentra el PlayerController: no habra ruido de pisadas.", this);
        }
    }

    private void Update()
    {
        UpdateLocomotionRadius();
    }

    // Si hay velocidad, el radio sale de la tabla. Si no, baja de forma lineal hasta 0.
    // Ese pequeno margen representa el eco del ultimo paso y evita que un solo frame
    // sin velocidad corte de golpe la escucha.
    private void UpdateLocomotionRadius()
    {
        if (playerController == null)
        {
            locomotionRadius = 0f;
            return;
        }

        float speed = ReadHorizontalSpeed();

        if (speed > 0.01f)
        {
            locomotionRadius = GetRadiusForSpeed(speed);
            lastLocomotionRadius = locomotionRadius;
            stoppedTime = 0f;
            return;
        }

        if (noiseHoldTime <= 0f)
        {
            locomotionRadius = 0f;
            return;
        }

        stoppedTime = stoppedTime + Time.deltaTime;

        float fade = stoppedTime / noiseHoldTime;
        locomotionRadius = Mathf.Lerp(lastLocomotionRadius, 0f, fade);

        if (fade >= 1f)
        {
            locomotionRadius = 0f;
        }
    }

    // Tabla de radios con tres tramos: agachado, andando y corriendo.
    // Las velocidades de las marcas se leen del PlayerController, asi los radios
    // caen justo en las velocidades reales del jugador y no hay que cuadrar nada a mano.
    private float GetRadiusForSpeed(float speed)
    {
        float crouchSpeed = playerController.CrouchWalkSpeed;
        float walkSpeed = playerController.WalkSpeed;
        float runSpeed = playerController.SprintSpeed;

        if (speed < crouchSpeed)
        {
            float t = Mathf.InverseLerp(0f, crouchSpeed, speed);
            return Mathf.Lerp(0f, crouchRadius, t);
        }

        if (speed < walkSpeed)
        {
            float t = Mathf.InverseLerp(crouchSpeed, walkSpeed, speed);
            return Mathf.Lerp(crouchRadius, walkRadius, t);
        }

        if (speed < runSpeed)
        {
            float t = Mathf.InverseLerp(walkSpeed, runSpeed, speed);
            return Mathf.Lerp(walkRadius, runRadius, t);
        }

        return runRadius;
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

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = EmitPosition;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireSphere(origin, locomotionRadius);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(origin, BreathRadius);
    }
}
