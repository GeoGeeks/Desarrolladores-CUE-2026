using UnityEngine;

public class DoorCloseTrigger : MonoBehaviour
{
    public CableCarDoors puertas;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CableCar"))
        {
            puertas.CerrarPuertas();
        }
    }
}