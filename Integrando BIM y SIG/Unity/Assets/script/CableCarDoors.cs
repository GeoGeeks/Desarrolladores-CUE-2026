using UnityEngine;

public class CableCarDoors : MonoBehaviour
{
    [Header("Puertas")]
    public Transform puertaIzquierda;
    public Transform puertaDerecha;

    [Header("Apertura")]
    public Vector3 desplazamientoIzquierda = new Vector3(-0.8f, 0f, 0f);
    public Vector3 desplazamientoDerecha = new Vector3(0.8f, 0f, 0f);

    [Header("Velocidad de apertura")]
    public float velocidad = 1.0f;

    private Vector3 posicionCerradaIzquierda;
    private Vector3 posicionCerradaDerecha;

    private Vector3 posicionAbiertaIzquierda;
    private Vector3 posicionAbiertaDerecha;

    private bool estaAbriendo = false;
    private bool estaCerrando = false;

    private void Awake()
    {
        // Guardamos las posiciones originales de las puertas
        if (puertaIzquierda != null)
        {
            posicionCerradaIzquierda = puertaIzquierda.localPosition;
            posicionAbiertaIzquierda =
                posicionCerradaIzquierda + desplazamientoIzquierda;
        }

        if (puertaDerecha != null)
        {
            posicionCerradaDerecha = puertaDerecha.localPosition;
            posicionAbiertaDerecha =
                posicionCerradaDerecha + desplazamientoDerecha;
        }
    }

    private void Update()
    {
        // =========================
        // ABRIR
        // =========================

        if (estaAbriendo)
        {
            if (puertaIzquierda != null)
            {
                puertaIzquierda.localPosition = Vector3.MoveTowards(
                    puertaIzquierda.localPosition,
                    posicionAbiertaIzquierda,
                    velocidad * Time.deltaTime
                );
            }

            if (puertaDerecha != null)
            {
                puertaDerecha.localPosition = Vector3.MoveTowards(
                    puertaDerecha.localPosition,
                    posicionAbiertaDerecha,
                    velocidad * Time.deltaTime
                );
            }

            // Comprobar si ya llegaron completamente abiertas
            bool izquierdaLista =
                puertaIzquierda == null ||
                Vector3.Distance(
                    puertaIzquierda.localPosition,
                    posicionAbiertaIzquierda
                ) < 0.001f;

            bool derechaLista =
                puertaDerecha == null ||
                Vector3.Distance(
                    puertaDerecha.localPosition,
                    posicionAbiertaDerecha
                ) < 0.001f;

            if (izquierdaLista && derechaLista)
            {
                estaAbriendo = false;

                Debug.Log("🚪 PUERTAS COMPLETAMENTE ABIERTAS");
            }
        }

        // =========================
        // CERRAR
        // =========================

        if (estaCerrando)
        {
            if (puertaIzquierda != null)
            {
                puertaIzquierda.localPosition = Vector3.MoveTowards(
                    puertaIzquierda.localPosition,
                    posicionCerradaIzquierda,
                    velocidad * Time.deltaTime
                );
            }

            if (puertaDerecha != null)
            {
                puertaDerecha.localPosition = Vector3.MoveTowards(
                    puertaDerecha.localPosition,
                    posicionCerradaDerecha,
                    velocidad * Time.deltaTime
                );
            }

            // Comprobar si ya llegaron completamente cerradas
            bool izquierdaLista =
                puertaIzquierda == null ||
                Vector3.Distance(
                    puertaIzquierda.localPosition,
                    posicionCerradaIzquierda
                ) < 0.001f;

            bool derechaLista =
                puertaDerecha == null ||
                Vector3.Distance(
                    puertaDerecha.localPosition,
                    posicionCerradaDerecha
                ) < 0.001f;

            if (izquierdaLista && derechaLista)
            {
                estaCerrando = false;

                Debug.Log("🚪 PUERTAS COMPLETAMENTE CERRADAS");
            }
        }
    }

    public void AbrirPuertas()
    {
        if (estaAbriendo)
            return;

        estaCerrando = false;
        estaAbriendo = true;

        Debug.Log("🚪 INICIANDO APERTURA");
    }

    public void CerrarPuertas()
    {
        if (estaCerrando)
            return;

        estaAbriendo = false;
        estaCerrando = true;

        Debug.Log("🚪 INICIANDO CIERRE");
    }
}