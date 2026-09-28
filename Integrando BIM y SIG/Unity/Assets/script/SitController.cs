using UnityEngine;
using UnityEngine.InputSystem;

public class SitController : MonoBehaviour
{
    [Header("REFERENCIAS")]
    public Transform sitPoint;
    public Transform vrSeatPoint;
    public Transform xrOrigin;
    public Animator animator;

    [Header("CONFIGURACION")]
    public float distanciaParaSentarse = 2f;

    private bool estaSentado = false;
    private CharacterController characterController;
    private CharacterController xrCharacterController;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (xrOrigin != null)
        {
            xrCharacterController = xrOrigin.GetComponent<CharacterController>();
        }
    }

    void Update()
    {
        if (estaSentado)
            return;

        if (sitPoint == null)
            return;

        float distancia = Vector3.Distance(
            transform.position,
            sitPoint.position
        );

        if (distancia <= distanciaParaSentarse)
        {
            Debug.Log("Puedes sentarte. Presiona E o A.");

            // PC
            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                Sentarse();
                return;
            }

            // VR - botón A del mando derecho
            if (Gamepad.current != null &&
                Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                Sentarse();
                return;
            }
        }
    }

    public void Sentarse()
    {
        if (estaSentado)
            return;

        if (sitPoint == null)
        {
            Debug.LogError("SitController: Falta asignar SitPoint.");
            return;
        }

        estaSentado = true;

        Debug.Log("PERSONAJE SENTANDOSE");

        // -----------------------------------------
        // 1. DESACTIVAR CHARACTER CONTROLLER
        // -----------------------------------------

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // -----------------------------------------
        // 2. COLOCAR PERSONAJE EN EL ASIENTO
        // -----------------------------------------

        transform.position = sitPoint.position;
        transform.rotation = sitPoint.rotation;

        // -----------------------------------------
        // 3. VINCULAR PERSONAJE A LA CABINA
        // -----------------------------------------

        transform.SetParent(sitPoint, true);

        Debug.Log("PLAYER AHORA VIAJA CON LA CABINA");

        // -----------------------------------------
        // 4. ANIMACION DE SENTARSE
        // -----------------------------------------

        if (animator != null)
        {
            animator.SetTrigger("Sit");
        }
        else
        {
            Debug.LogError("SitController: No se encontró Animator.");
        }

        // -----------------------------------------
        // 5. VINCULAR XR ORIGIN A LA CABINA
        // -----------------------------------------

        if (xrOrigin != null && vrSeatPoint != null)
        {
            if (xrCharacterController != null)
            {
                xrCharacterController.enabled = false;
            }

            xrOrigin.position = vrSeatPoint.position;
            xrOrigin.rotation = vrSeatPoint.rotation;

            xrOrigin.SetParent(vrSeatPoint, true);

            Debug.Log("XR ORIGIN AHORA VIAJA CON LA CABINA");
        }
        else
        {
            Debug.LogWarning(
                "SitController: XR Origin o VRSeatPoint no están asignados."
            );
        }

        Debug.Log("PERSONAJE SENTADO Y CABINA EN MOVIMIENTO");
    }
}