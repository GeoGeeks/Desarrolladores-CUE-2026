using UnityEngine;
using UnityEngine.InputSystem;

public class TeleportToPoint : MonoBehaviour
{
    [Header("Jugador")]
    public Transform xrOrigin;

    [Header("Destino")]
    public Transform destino;

    [Header("Botón X")]
    public InputActionReference botonX;

    private void OnEnable()
    {
        if (botonX != null)
        {
            botonX.action.Enable();
            botonX.action.performed += Teletransportar;
        }
    }

    private void OnDisable()
    {
        if (botonX != null)
        {
            botonX.action.performed -= Teletransportar;
            botonX.action.Disable();
        }
    }

    private void Teletransportar(InputAction.CallbackContext context)
    {
        if (xrOrigin == null || destino == null)
        {
            Debug.LogWarning("Falta asignar XR Origin o Destino.");
            return;
        }

        xrOrigin.position = destino.position;

        Debug.Log("TELETRANSPORTE ACTIVADO");
    }
}