
El proyecto implementa un modelo ConvLSTM para pronosticar espacialmente la distribución de cuerpos de agua entre septiembre de 2026 y agosto de 2027, utilizando información histórica obtenida mediante teledetección.
El procesamiento se divide en dos notebooks, de acuerdo con el entorno utilizado:
# 1. Preparación y modelado — TensorFlow/Keras
El notebook `01_preparación_datos_ConvLSTM.ipynb` contiene la preparación de los datos históricos, la organización temporal de las observaciones, la construcción y entrenamiento del modelo ConvLSTM, la validación de su desempeño y la generación de las predicciones para los 12 meses del periodo de estudio.

La evaluación del modelo se realiza mediante MAE, RMSE y correlación, permitiendo analizar tanto el error de las predicciones como su capacidad para reproducir los patrones observados.

# 2. Procesamiento geoespacial — ArcGIS Pro

El notebook `02_procesamiento_ArcGIS_Pro_ConvLSTM.ipynb` procesa las predicciones generadas por el modelo y las convierte en información geoespacial. Incluye la generación de rasters, la creación y organización del Mosaic Dataset y CRF, el recorte al área de estudio, el cálculo de la superficie de agua y la generación de estadísticas y gráficos.

# Reproducción
Para replicar el ejercicio, se deben ejecutar los notebooks en orden:
**01_preparación_datos_ConvLSTM.ipynb` → `02_procesamiento_ArcGIS_Pro_ConvLSTM.ipynb`**

El primer notebook debe ejecutarse en el entorno de TensorFlow/Keras, mientras que el segundo debe ejecutarse en el entorno de ArcGIS Pro/ArcPy.

El flujo completo es:
**Datos históricos → preparación → entrenamiento ConvLSTM → validación → predicción → generación de rasters → procesamiento en ArcGIS Pro → 
cálculo de áreas → estadísticas y visualización.**
Los notebooks incluyen las rutas y parámetros principales necesarios para reproducir el procesamiento, que pueden ajustarse a la estructura de archivos del usuario.
