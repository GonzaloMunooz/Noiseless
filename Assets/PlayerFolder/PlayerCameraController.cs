using UnityEngine;

/// <summary>
/// Todo lo que hace la camara del jugador en primera persona:
/// - Bobbing realista: dos modos que se mezclan solos (parado y en movimiento, segun la velocidad real).
/// - Altura al agacharse: baja con lerpeo mientras PlayerController mantiene el crouch pulsado.
/// Se pone en la camara (hijo del Player). Solo modifica la posicion local, nunca la rotacion,
/// asi que no choca con el pitch de PlayerController.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCameraController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("PlayerController del jugador (para saber si esta agachado). Si se deja vacio se busca en el padre.")]
    [SerializeField] private PlayerController player;
    [Tooltip("CharacterController del jugador (para saber su velocidad). Si se deja vacio se busca en el padre.")]
    [SerializeField] private CharacterController controller;

    [Header("Agacharse")]
    [Tooltip("Cuanto baja la camara al agacharse, en metros.")]
    [SerializeField] private float crouchCameraDrop = 0.6f;
    [Tooltip("Rapidez del lerpeo de la camara al agacharse o levantarse.")]
    [SerializeField] private float crouchLerpSpeed = 10f;

    [Header("FOV")]
    [Tooltip("FOV en reposo (parado, andando o agachado).")]
    [SerializeField] private float normalFov = 60f;
    [Tooltip("FOV al correr a tope.")]
    [SerializeField] private float sprintFov = 72f;

    [Tooltip("Velocidad (m/s) a la que el bob alcanza su maxima potencia. Por encima se clampa.")]
    [SerializeField] private float referenceSpeed = 10f;
    [Tooltip("Longitud de zancada (m) para derivar la cadencia de pasos a partir de la velocidad.")]
    [SerializeField] private float strideLength = 1.5f;

    [Header("Modo estacionado")]
    [Tooltip("Amplitud vertical en metros del bob parado.")]
    [SerializeField] private float idleAmplitude = 0.02f;
    [Tooltip("Frecuencia (Hz) del bob parado: respiracion lenta.")]
    [SerializeField] private float idleFrequency = 0.5f;
    [Tooltip("Amplitud lateral relativa a la vertical.")]
    [SerializeField] private float idleSwayRatio = 0.6f;

    [Header("Modo movimiento")]
    [Tooltip("Amplitud vertical en metros a potencia maxima (referenceSpeed).")]
    [SerializeField] private float moveAmplitude = 0.07f;
    [Tooltip("Amplitud lateral relativa a la vertical.")]
    [SerializeField] private float moveSwayRatio = 0.75f;
    [Tooltip("Amplitud adelante/atras relativa a la vertical.")]
    [SerializeField] private float moveForwardRatio = 0.45f;

    [Header("Suavizado")]
    [Tooltip("Velocidad de la mezcla parado <-> andando.")]
    [SerializeField] private float blendSpeed = 6f;
    [Tooltip("Velocidad del suavizado de la velocidad leida.")]
    [SerializeField] private float speedSmoothing = 10f;

    private Vector3 baseLocalPosition;
    private float phase;
    private float smoothedSpeed;
    private float moveWeight;
    private float crouchOffsetY; // 0 = de pie, -crouchCameraDrop = agachado
    private Camera cameraComponent;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<PlayerController>();

        if (controller == null)
            controller = GetComponentInParent<CharacterController>();

        cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null && Camera.main != null)
        {
            cameraComponent = Camera.main;
        }

        // Posicion de reposo de la camara: todo el bob y el crouch son offsets sobre esta
        baseLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        UpdateCrouchOffset(dt);
        UpdateFov();

        // 1) Velocidad horizontal real del jugador (ignoramos la gravedad del CharacterController)
        float targetSpeed = ReadHorizontalSpeed();
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, targetSpeed, 1f - Mathf.Exp(-speedSmoothing * dt));

        // 2) Peso del modo movimiento: 0 = parado, 1 = a tope
        float targetWeight = referenceSpeed > 0.001f ? Mathf.Clamp01(smoothedSpeed / referenceSpeed) : 0f;
        moveWeight = Mathf.Lerp(moveWeight, targetWeight, 1f - Mathf.Exp(-blendSpeed * dt));

        // 3) Fase: respiracion parado, cadencia de pasos en movimiento
        float idleFreq = Mathf.Max(0.01f, idleFrequency);
        float stepFreq = Mathf.Max(idleFreq, smoothedSpeed / Mathf.Max(0.01f, strideLength));
        float frequency = Mathf.Lerp(idleFreq, stepFreq, moveWeight);
        phase += frequency * Mathf.PI * 2f * dt;

        // 4) Amplitudes mezcladas
        float amplitudeY = Mathf.Lerp(idleAmplitude, moveAmplitude, moveWeight);
        float swayRatio = Mathf.Lerp(idleSwayRatio, moveSwayRatio, moveWeight);
        float forwardRatio = Mathf.Lerp(idleSwayRatio * 0.5f, moveForwardRatio, moveWeight);

        // Vertical = una oscilacion por paso; lateral y frontal = media frecuencia (balanceo del cuerpo)
        float offsetY = Mathf.Sin(phase) * amplitudeY;
        float offsetX = Mathf.Sin(phase * 0.5f) * amplitudeY * swayRatio;
        float offsetZ = Mathf.Sin(phase * 0.25f) * amplitudeY * forwardRatio;

        transform.localPosition = baseLocalPosition + new Vector3(offsetX, offsetY + crouchOffsetY, offsetZ);
    }

    // Baja o sube la camara segun este agachado el jugador, con lerpeo
    private void UpdateCrouchOffset(float dt)
    {
        float targetOffsetY = 0f;
        if (player != null && player.IsCrouching)
        {
            targetOffsetY = -crouchCameraDrop;
        }

        crouchOffsetY = Mathf.Lerp(crouchOffsetY, targetOffsetY, 1f - Mathf.Exp(-crouchLerpSpeed * dt));
    }

    // El FOV va con la mezcla de sprint del PlayerController: sube al correr y vuelve al normal
    private void UpdateFov()
    {
        if (cameraComponent == null || player == null)
        {
            return;
        }

        cameraComponent.fieldOfView = Mathf.Lerp(normalFov, sprintFov, player.SprintBlend);
    }

    private float ReadHorizontalSpeed()
    {
        if (controller == null)
            return 0f;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }
}
