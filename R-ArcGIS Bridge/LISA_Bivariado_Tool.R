# Función principal requerida por el R-ArcGIS Bridge
tool_exec <- function(in_params, out_params) {
  
  # Cargar librerías
  library(arcgisbinding)
  library(sf)
  library(rgeoda)
  
  # Inicializar conexión con ArcGIS
  arc.check_product()
  
  # 1. Capturar los parámetros enviados desde ArcGIS
  input_fc   <- in_params[[1]]      
  campo_x    <- in_params[[2]]       
  campo_y    <- in_params[[3]]       
  neigh_type <- in_params[[4]]      # Tipo de vecindad
  perms      <- as.numeric(in_params[[5]]) # Número de permutaciones
  output_fc  <- out_params[[1]]    
  
  # Leer datos
  arc_data <- arc.open(input_fc)
  sf_data <- arc.data2sf(arc.select(arc_data))
  
  # 2. Lógica para el Tipo de Vecindad (Neighborhood Type)
  # Mapeo de los textos de ArcGIS a las funciones de rgeoda
  if (neigh_type == "Contiguity edges corners") {
    w <- queen_weights(sf_data)
  } else if (neigh_type == "Contiguity edges only") {
    w <- rook_weights(sf_data)
  } else if (neigh_type == "K nearest neighbors") {
    # Por defecto se usan 4 vecinos más cercanos si eligen esta opción
    w <- knn_weights(sf_data, k = 4) 
  } else {
    w <- queen_weights(sf_data) # Fallback por seguridad
  }
  
  # 3. Preparar variables
  var_x <- as.numeric(sf_data[[campo_x]])
  var_x[is.na(var_x)] <- 0 
  var_y <- as.numeric(sf_data[[campo_y]])
  var_y[is.na(var_y)] <- 0
  
  datos_bivariados <- data.frame(X = var_x, Y = var_y)
  
  # 4. Ejecutar el LISA Bivariado con las permutaciones seleccionadas
  lisa_biv <- local_bimoran(w, datos_bivariados, permutations = perms)
  
  # Extraer resultados
  cluster_ids <- lisa_clusters(lisa_biv) 
  etiquetas <- lisa_labels(lisa_biv)      
  
  # Asignar resultados. Se nombra la columna "LISA_Class" 
  # para facilitar la simbología automática más adelante.
  sf_data$LISA_Class <- etiquetas[cluster_ids + 1]
  sf_data$P_Value <- lisa_pvalues(lisa_biv)
  
  # Escribir capa de salida
  arc.write(output_fc, sf_data)
  
  return(out_params)
}