using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto con el que el jugador puede interactuar con la tecla E.
/// Necesita un BoxCollider con "Is Trigger" en este mismo GameObject (puede ser mas grande
/// que el objeto) que marca la zona de interaccion.
/// Mientras el jugador esta dentro del trigger el objeto se registra en el
/// PlayerInteractController; si ademas es el mas cercano, muestra la etiqueta "[E]"
/// encima suyo y responde a la tecla.
/// Lo que pasa al interactuar se configura en el evento 'onInteract' del Inspector.
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("Etiqueta")]
    [Tooltip("Texto que se muestra en la etiqueta.")]
    [SerializeField] private string text = "[E]";
    [Tooltip("Altura del texto en metros.")]
    [SerializeField] private float textHeight = 0.25f;
    [Tooltip("Separacion entre la parte de arriba del objeto y la etiqueta.")]
    [SerializeField] private float heightOffset = 0.3f;
    [Tooltip("Color del texto.")]
    [SerializeField] private Color color = Color.white;

    [Header("Interaccion")]
    [Tooltip("Que pasa al pulsar la tecla de interaccion.")]
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    private PlayerInteractController playerInteract;
    private Transform cameraTransform;
    private TextMeshPro label;
    private float topOffset;

    private void Awake()
    {
        playerInteract = FindAnyObjectByType<PlayerInteractController>();
        if (playerInteract == null)
        {
            Debug.LogWarning("InteractableObject: no hay ningun PlayerInteractController en la escena.", this);
        }

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        CheckTrigger();

        // Distancia desde el centro del objeto hasta su parte de arriba
        topOffset = ReadTopOffset() + heightOffset;

        CreateLabel();
    }

    private void OnDisable()
    {
        // Al desactivarse o destruirse no puede seguir contando como interactable
        if (playerInteract != null)
        {
            playerInteract.RemoveInteractable(this);
        }

        if (label != null)
        {
            label.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (label != null)
        {
            Destroy(label.gameObject);
        }
    }

    // Entrar en el trigger = poder interactuar con el objeto
    private void OnTriggerEnter(Collider other)
    {
        if (playerInteract == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerInteractController>() == null)
        {
            return;
        }

        playerInteract.AddInteractable(this);
    }

    // Salir del trigger = dejar de poder interactuar
    private void OnTriggerExit(Collider other)
    {
        if (playerInteract == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerInteractController>() == null)
        {
            return;
        }

        playerInteract.RemoveInteractable(this);
    }

    // El jugador se encarga de llamar a esto al pulsar la tecla
    public void Interact()
    {
        onInteract.Invoke();
    }

    private void LateUpdate()
    {
        if (label == null)
        {
            return;
        }

        // Solo se ve si el jugador esta dentro del trigger y este es el objeto mas cercano
        bool focused = playerInteract != null && playerInteract.CurrentInteractable == this;

        if (focused == false || cameraTransform == null)
        {
            label.gameObject.SetActive(false);
            return;
        }

        // Justo encima del objeto y mirando siempre a la camara
        label.gameObject.SetActive(true);
        label.transform.position = transform.position + Vector3.up * topOffset;
        label.transform.rotation = cameraTransform.rotation;
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("InteractionLabel", typeof(RectTransform));

        label = labelObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;

        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
        if (fontAsset == null)
        {
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        if (fontAsset != null)
        {
            label.font = fontAsset;
            label.GetComponent<MeshRenderer>().sharedMaterial = fontAsset.material;
        }
        else
        {
            Debug.LogWarning("InteractableObject: falta el Font Asset de TextMeshPro (importa TMP Essentials).", this);
        }

        label.ForceMeshUpdate();

        // El texto se escala para que mida siempre lo mismo, sea el objeto grande o pequeno
        float generatedHeight = label.textBounds.size.y;
        if (generatedHeight > 0.001f)
        {
            labelObject.transform.localScale = Vector3.one * (textHeight / generatedHeight);
        }

        labelObject.SetActive(false);
    }

    // Los eventos de trigger solo llegan al GameObject que tiene el collider,
    // asi que el BoxCollider con "Is Trigger" tiene que estar en este mismo objeto
    private void CheckTrigger()
    {
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider itemCollider in colliders)
        {
            if (itemCollider.isTrigger)
            {
                return;
            }
        }

        Debug.LogWarning("InteractableObject: '" + name + "' necesita un BoxCollider con 'Is Trigger' en este mismo GameObject.", this);
    }

    private float ReadTopOffset()
    {
        Renderer[] itemRenderers = GetComponentsInChildren<Renderer>();
        if (itemRenderers.Length == 0)
        {
            return 1f;
        }

        // Parte mas alta de todo el objeto
        float top = transform.position.y;
        foreach (Renderer itemRenderer in itemRenderers)
        {
            if (itemRenderer.bounds.max.y > top)
            {
                top = itemRenderer.bounds.max.y;
            }
        }

        return top - transform.position.y;
    }
}
