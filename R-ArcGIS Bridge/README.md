# BiLISA Tool: R-ArcGIS Bridge para Asociación Espacial Bivariada

Este repositorio contiene el código fuente y los archivos de configuración para integrar el **Índice de Moran Local Bivariado (BiLISA)** dentro de ArcGIS Pro. La herramienta utiliza el puente de interoperabilidad R-ArcGIS para ejecutar los algoritmos de la librería `rgeoda` (GeoDa) directamente desde la interfaz gráfica de geoprocesamiento de ArcGIS.

## Archivos del repositorio

*   **`LISA_Bivariado_Tool.R`**: el script principal de R que actúa como motor de la herramienta. Se encarga de recibir los datos desde ArcGIS, calcular la topología espacial, ejecutar el estadígrafo BiLISA y devolver los resultados.
*   **`Simbologia_LISA.lyrx`**: archivo de capa de ArcGIS Pro que contiene la simbología estándar (colores de clústeres Alto-Alto, Bajo-Bajo, etc.). Se aplica automáticamente al resultado para una visualización inmediata.

## Requisitos previos

Para ejecutar esta herramienta, se necesita tener instalado y configurado lo siguiente en el equipo:
1.  **ArcGIS Pro** (Versión 2.5 o superior).
2.  **R** (Versión 4.0 o superior).
3.  **R-ArcGIS Bridge** configurado (Se puede instalar desde ArcGIS Pro en *Opciones > Geoprocesamiento > R-ArcGIS*).
4.  Paquetes de R instalados: `arcgisbinding`, `sf`, `rgeoda`.

## ¿Qué hace el código fuente? (Resumen)

El script de R está estructurado en una función `tool_exec` (requerida por el puente) y se divide en 4 bloques lógicos:
1.  **Lectura y traducción:** utiliza `arcgisbinding` para capturar la capa de entrada desde ArcGIS y la convierte a un objeto espacial estándar de R (`sf`).
2.  **Topología espacial:** lee el parámetro de tipo de vecindad elegido por el usuario (Queen, Rook, KNN) y usa `rgeoda` para construir la matriz de pesos espaciales.
3.  **Limpieza de datos:** extrae las variables a analizar y realiza un manejo preventivo de valores nulos (NAs) transformándolos en ceros para evitar errores de ejecución.
4.  **Cálculo y retorno:** ejecuta la función `local_bimoran`, clasifica los polígonos según su clúster de asociación cruzada y nivel de significancia, y envía la capa enriquecida de vuelta a la geodatabase de ArcGIS mediante `arc.write`.

## Cómo replicar la herramienta en ArcGIS Pro

Sigue estos pasos para construir la interfaz visual (*Script Tool*) en tu proyecto de ArcGIS Pro:

1. En el panel **Catalog**, haz clic derecho en tu *Toolbox* (`.atbx`) > **New** > **Script**.
2. En la pestaña **General**, asigna un nombre y una etiqueta a la herramienta. En la sección **Execution**, selecciona el archivo `LISA_Bivariado_Tool.R` en la casilla *Script File*.
3. Ve a la pestaña **Parameters** y configúralos **exactamente en este orden**:

| N° | Label | Data Type | Type / Direction | Dependency / Filter |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Input Features | Feature Layer | Required / Input | Ninguna |
| 2 | Input Field X | Field | Required / Input | Dependency: *Input Features* |
| 3 | Input Field Y | Field | Required / Input | Dependency: *Input Features* |
| 4 | Neighborhood Type | String | Required / Input | Filter: *Value List* (Ver nota abajo) |
| 5 | Number of Permutations | Long | Required / Input | Filter: *Value List* (99, 199, 499, 999...) |
| 6 | Output Feature Class | Feature Class | Required / Output | Ninguna |

> **Nota para el parámetro 4:** En el filtro *Value List*, añade exactamente estos textos: `Contiguity edges corners`, `Contiguity edges only`, `K nearest neighbors`.

### Aplicar la simbología automática (.lyrx)

Para que el mapa resultante se dibuje automáticamente con los colores correctos de los clústeres espaciales (Rojo para Alto-Alto, Azul para Bajo-Bajo, etc.), debes vincular el archivo de simbología al parámetro de salida:

1. En la misma ventana de propiedades de la herramienta, ve a la pestaña **Parameters**.
2. Haz clic sobre el parámetro número 6 (**Output Feature Class**).
3. En la parte inferior, busca la propiedad llamada **Symbology** (Simbología).
4. Haz clic en el ícono de la carpeta y selecciona el archivo **`Simbologia_LISA.lyrx`** incluido en este repositorio.
5. Haz clic en **OK** para guardar la herramienta.
