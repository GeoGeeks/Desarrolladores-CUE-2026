# Esmeraldina

## Descripción

Este proyecto contiene el proceso de entrenamiento de un modelo de Machine Learning basado en Random Forest, desarrollado para apoyar el análisis de datos asociados a la exploración esmeraldífera.

El notebook `Esmeraldina_Entrenamiento.ipynb` contiene el flujo completo utilizado para preparar los datos, entrenar el modelo y evaluar su desempeño.

## Contenido

- `Esmeraldina_Entrenamiento.ipynb`: notebook principal utilizado para el procesamiento de datos, entrenamiento y evaluación del modelo Random Forest.
- `README.md`: documentación general del proyecto.

## Flujo del código

El notebook realiza, de manera general, los siguientes procesos:

1. Importación de las librerías necesarias.
2. Carga de los datos.
3. Preparación y limpieza de los datos.
4. Definición de las variables predictoras (X) y la variable objetivo (y).
5. División de los datos en conjuntos de entrenamiento y prueba.
6. Entrenamiento del modelo Random Forest.
7. Generación de predicciones.
8. Evaluación del desempeño del modelo.
9. Almacenamiento del modelo entrenado para su posterior utilización.

## Requisitos

Para ejecutar el proyecto se requiere Python 3 y las siguientes librerías:

```bash
pip install pandas numpy scikit-learn matplotlib joblib jupyter
