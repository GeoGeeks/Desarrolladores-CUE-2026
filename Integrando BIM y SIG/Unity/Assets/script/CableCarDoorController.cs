using UnityEngine;

public class CableCarDoorController : MonoBehaviour
{
    [Header("Puntos de la estación")]
    public Transform doorOpenPoint;
    public Transform doorClosePoint;

    [Header("Control de distancia")]
    public float distanciaAbrir = 4f;
    public float distanciaCerrar = 4f;

    [Header("Puertas")]
    public CableCarDoors puertas;

    private bool yaAbrio = false;
    private bool yaCerro = false;

    private void Update()
    {
        if (puertas == null)
            return;

        // =========================
        // ABRIR
        // =========================

        if (!yaAbrio && doorOpenPoint != null)
        {
            float distancia = Vector3.Distance(
                transform.position,
                doorOpenPoint.position
            );

            if (distancia <= distanciaAbrir)
            {
                yaAbrio = true;

                Debug.Log("🚪 PUERTAS ABRIENDO - Llegó al punto de apertura");

                puertas.AbrirPuertas();
            }
        }

        // =========================
        // CERRAR
        // =========================

        if (yaAbrio && !yaCerro && doorClosePoint != null)
        {
            float distancia = Vector3.Distance(
                transform.position,
                doorClosePoint.position
            );

            if (distancia <= distanciaCerrar)
            {
                yaCerro = true;

                Debug.Log("🚪 PUERTAS CERRANDO - Salió de la estación");

                puertas.CerrarPuertas();
            }
        }

        // =========================
        // PREPARAR SIGUIENTE VIAJE
        // =========================

        if (yaCerro)
        {
            float distanciaAlAbrir = Vector3.Distance(
                transform.position,
                doorOpenPoint.position
            );

            // Cuando la cabina ya se alejó bastante del punto
            // de apertura, queda preparada para otra vuelta.

            if (distanciaAlAbrir > 20f)
            {
                yaAbrio = false;
                yaCerro = false;
            }
        }
    }
}