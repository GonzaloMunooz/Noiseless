using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de jugador en primera persona.
/// Necesita un CharacterController en el mismo GameObject y una Camera (hijo) que haga de cabeza.
/// Se mueve con las acciones W/A/S/D del mapa "Gameplay", corre con Sprint,
/// se agacha con Crouch y mira con el mouse.
/// La stamina se gasta al correr (sin stamina no se puede correr) y se recupera al dejar de correr.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [Tooltip("Velocidad al moverte agachado. Se aplica al instante, sin lerpeo.")]
    [SerializeField] private float crouchWalkSpeed = 2.5f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Rapidez del lerpeo al empezar o dejar de correr (mas alto = mas rapido).")]
    [SerializeField] private float sprintLerpSpeed = 8f;

    [Header("Agacharse")]
    [Tooltip("Altura del CharacterController agachado. Se aplica al instante, sin lerpeo.")]
    [SerializeField] private float crouchHeight = 1f;

    [Header("Stamina")]
    [Tooltip("Stamina maxima.")]
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("Stamina que se gasta por segundo mientras corremos.")]
    [SerializeField] private float staminaDrainPerSecond = 20f;
    [Tooltip("Stamina que se recupera por segundo al dejar de correr.")]
    [SerializeField] private float staminaRegenPerSecond = 20f;
    [Tooltip("Cada cuantos segundos se imprime la stamina que queda mientras corremos.")]
    [SerializeField] private float staminaLogInterval = 1f;

    [Header("Camara")]
    [Tooltip("Transform de la camara (hijo). Si se deja vacio usa Camera.main.")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Referencias")]
    [Tooltip("PlayerStateMachine del jugador (para saber si esta agachado o corriendo). Si se deja vacio se busca en el padre.")]
    [SerializeField] private PlayerStateMachine stateMachine;

    [Header("Input (PlayerInputActions)")]
    [Tooltip("Asset PlayerInputActions. Mapa 'Gameplay' con las acciones W, A, S, D, Sprint y Crouch.")]
    [SerializeField] private InputActionAsset inputActions;

    private InputAction moveW;
    private InputAction moveA;
    private InputAction moveS;
    private InputAction moveD;
    private InputAction sprintAction;
    private InputAction crouchAction;

    private CharacterController controller;

    private float verticalVelocity;
    private float pitch;
    private float sprintBlend; // 0 = andando, 1 = corriendo

    private float stamina;
    private float staminaLogTimer;
    private bool exhausted; // true al quedarse sin stamina: no puede correr hasta recuperarla entera

    private float standHeight;
    private float standCenterY;
    private bool isCrouching;

    // Lo lee PlayerCameraController para saber si tiene que bajar la camara
    public bool IsCrouching
    {
        get { return isCrouching; }
    }

    // Lo lee PlayerStateMachine para saber si vamos rapido o andando
    public float WalkSpeed
    {
        get { return walkSpeed; }
    }

    // Lo lee PlayerNoiseEmitter para poner el techo de la tabla de ruido
    public float SprintSpeed
    {
        get { return sprintSpeed; }
    }

    // Lo lee PlayerNoiseEmitter para poner el primer tramo de la tabla de ruido
    public float CrouchWalkSpeed
    {
        get { return crouchWalkSpeed; }
    }

    // Lo lee PlayerCameraController para subir o bajar el FOV
    public float SprintBlend
    {
        get { return sprintBlend; }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (stateMachine == null)
        {
            stateMachine = GetComponentInParent<PlayerStateMachine>();
        }

        if (cameraPivot == null && Camera.main != null)
        {
            cameraPivot = Camera.main.transform;
        }

        // Guardamos las medidas de pie para poder volver a ellas al levantarnos
        standHeight = controller.height;
        standCenterY = controller.center.y;

        // Empezamos con la stamina al maximo
        stamina = maxStamina;

        SetupInput();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetupInput()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("PlayerController: falta asignar el asset PlayerInputActions.", this);
            return;
        }

        InputActionMap gameplay = inputActions.FindActionMap("Gameplay");
        if (gameplay == null)
        {
            Debug.LogWarning("PlayerController: el asset no tiene el mapa 'Gameplay'.", this);
            return;
        }

        moveW = gameplay.FindAction("W");
        moveA = gameplay.FindAction("A");
        moveS = gameplay.FindAction("S");
        moveD = gameplay.FindAction("D");
        sprintAction = gameplay.FindAction("Sprint");
        crouchAction = gameplay.FindAction("Crouch");
    }

    // Activo solo estas acciones (no el mapa entero) para no chocar con otros sistemas
    private void OnEnable()
    {
        moveW?.Enable();
        moveA?.Enable();
        moveS?.Enable();
        moveD?.Enable();
        sprintAction?.Enable();
        crouchAction?.Enable();
    }

    private void OnDisable()
    {
        moveW?.Disable();
        moveA?.Disable();
        moveS?.Disable();
        moveD?.Disable();
        sprintAction?.Disable();
        crouchAction?.Disable();
    }

    private void Update()
    {
        // La stamina va antes de Move() porque decide si podemos correr este frame
        UpdateStamina();

        Look();
        Move();
        UpdateCrouch();
    }

    private void Look()
    {
        if (cameraPivot == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // El cuerpo gira en Y, la camara solo en X (con limite para no doblar el cuello)
        transform.Rotate(Vector3.up * mouseDelta.x);
        pitch = Mathf.Clamp(pitch - mouseDelta.y, -maxLookAngle, maxLookAngle);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = ReadMoveInput();

        UpdateSprintBlend();

        float speed = GetMoveSpeed();

        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;

        // Gravedad para que el CharacterController no flote
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity = verticalVelocity + gravity * Time.deltaTime;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    // Mezcla 0..1 que sube al empezar a correr y baja al dejarlo.
    // Esa misma mezcla mueve la velocidad y el FOV, asi van siempre cuadrados.
    private void UpdateSprintBlend()
    {
        float targetBlend = 0f;
        if (IsSprinting())
        {
            targetBlend = 1f;
        }

        float lerpFactor = 1f - Mathf.Exp(-sprintLerpSpeed * Time.deltaTime);
        sprintBlend = Mathf.Lerp(sprintBlend, targetBlend, lerpFactor);
    }

    // Para correr hay que pulsar Sprint, no estar agachado y tener stamina
    private bool IsSprinting()
    {
        if (sprintAction == null)
        {
            return false;
        }

        if (sprintAction.IsPressed() == false)
        {
            return false;
        }

        // Sin stamina no se puede correr
        if (exhausted || stamina <= 0f)
        {
            return false;
        }

        // Agachado no se puede correr: Crouching y CrouchWalking anulan el sprint
        if (IsState("Crouching") || IsState("CrouchWalking"))
        {
            return false;
        }

        return true;
    }

    // Gastamos stamina mientras corremos y la recuperamos al dejar de correr.
    // Al llegar a 0 el jugador queda agotado y no puede volver a correr hasta recuperarla entera.
    private void UpdateStamina()
    {
        if (IsSprinting())
        {
            stamina -= staminaDrainPerSecond * Time.deltaTime;
            print(stamina);
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
            }

            LogStamina();
            return;
        }

        staminaLogTimer = 0f;

        stamina += staminaRegenPerSecond * Time.deltaTime;

        if (stamina >= maxStamina)
        {
            stamina = maxStamina;
            exhausted = false;
        }
    }

    // Print de control: cuanto queda de stamina mientras corremos
    private void LogStamina()
    {
        staminaLogTimer += Time.deltaTime;

        if (staminaLogTimer < staminaLogInterval)
        {
            return;
        }

        staminaLogTimer = 0f;

        Debug.Log("Stamina: " + Mathf.RoundToInt(stamina) + " / " + Mathf.RoundToInt(maxStamina), this);
    }

    // Agachado vamos siempre a crouchWalkSpeed (sin lerpeo); si no, mezcla andar <-> correr
    private float GetMoveSpeed()
    {
        if (isCrouching)
        {
            return crouchWalkSpeed;
        }

        return Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);
    }

    // Mientras Crouch este pulsado, el jugador va agachado.
    // Aqui solo cambia la altura del CharacterController; la camara la baja PlayerCameraController.
    private void UpdateCrouch()
    {
        bool crouchPressed = crouchAction != null && crouchAction.IsPressed();

        // Solo se puede agachar desde Standing o Walking: corriendo no se puede pasar a agachado
        if (IsState("Sprinting"))
        {
            crouchPressed = false;
        }

        ApplyCrouchHeight(crouchPressed);
    }

    // Estado actual del PlayerStateMachine. Si no hay, cuenta como "no es ese estado"
    private bool IsState(string state)
    {
        if (stateMachine == null)
        {
            return false;
        }

        return stateMachine.CurrentState == state;
    }

    // La altura del CharacterController cambia de golpe, sin lerpeo
    private void ApplyCrouchHeight(bool crouchPressed)
    {
        if (crouchPressed == isCrouching)
        {
            return;
        }

        isCrouching = crouchPressed;

        if (isCrouching)
        {
            controller.height = crouchHeight;
            controller.center = new Vector3(controller.center.x, crouchHeight / 2f, controller.center.z);
        }
        else
        {
            controller.height = standHeight;
            controller.center = new Vector3(controller.center.x, standCenterY, controller.center.z);
        }
    }

    // Lee las 4 acciones del mapa "Gameplay" y las junta en un Vector2 (-1..1)
    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (moveW != null && moveW.IsPressed())
        {
            input.y += 1f;
        }

        if (moveS != null && moveS.IsPressed())
        {
            input.y -= 1f;
        }

        if (moveD != null && moveD.IsPressed())
        {
            input.x += 1f;
        }

        if (moveA != null && moveA.IsPressed())
        {
            input.x -= 1f;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }
}
