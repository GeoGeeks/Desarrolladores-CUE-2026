using UnityEngine;

public class DoorOpenTrigger : MonoBehaviour
{
    public CableCarDoors puertas;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CableCar"))
        {
            puertas.AbrirPuertas();
        }
    }
}