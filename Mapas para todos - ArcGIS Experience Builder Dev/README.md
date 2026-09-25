# Mapas para todos - ArcGIS Experience Builder Dev

## Descripción

Este proyecto contiene **Filtro Adaptativo de Daltonismo** (`widget-filter-view`), un widget personalizado para ArcGIS Experience Builder (edición Developer) que hace más accesibles los mapas para usuarios con deficiencias en la visión del color (daltonismo).

El widget se conecta a un widget de Mapa existente y:

1. **Detecta** las capas del mapa y los colores de su simbología.
2. **Evalúa** si esos colores son distinguibles entre sí para personas con protanopia, deuteranopia o tritanopia, e informa el riesgo en pantalla.
3. **Corrige** la visualización con un interruptor por tipo de daltonismo, reemplazando la paleta de las capas por una paleta accesible, o aplicando un filtro de color global sobre el mapa.

## Contenido

```
widget-filter-view/
├── manifest.json                     # Metadatos del widget (nombre, versión, dependencias, exbVersion)
├── config.json                       # Configuración por defecto (vacía)
├── icon.svg                          # Ícono mostrado en el builder
└── src/
    ├── runtime/
    │   ├── Widget.tsx                # Interfaz y lógica principal (detección, interruptores, cambio de paleta)
    │   └── colorAccessibility.ts     # Simulación de daltonismo y cálculo de riesgo de color
    └── setting/
        └── setting.tsx               # Panel de configuración: selección del widget de Mapa
```

## Funcionamiento

### 1. Detección de capas

Al conectarse al mapa, el widget recorre las capas del mapa (`view.map.layers`) y, para cada una que tenga `renderer`, extrae los colores de su simbología. Soporta renderers de tipo:

- `simple` (un solo color)
- `unique-value` (valores únicos)
- `class-breaks` (rangos de clases)

La detección se actualiza automáticamente si se agregan o quitan capas.

### 2. Análisis de riesgo (`colorAccessibility.ts`)

Para cada tipo de daltonismo:

1. Se simula cómo percibe cada color una persona con esa condición, usando las matrices de **Machado, Oliveira & Fernandes (2009)** aplicadas en espacio RGB lineal.
2. Los colores simulados se convierten a **CIE Lab**.
3. Se calcula la distancia mínima (ΔE CIE76) entre todos los pares de colores.
4. Si la distancia mínima es menor a **12**, se marca como *"⚠ Riesgo: Colores poco distinguibles"*; en caso contrario, *"✅ Capas actualmente distinguibles"*.

### 3. Corrección

El widget ofrece dos modos:

- **Cambio de paleta (por defecto):** clona el renderer original de cada capa y reemplaza los colores por una paleta accesible, conservando la transparencia original de cada símbolo. Al apagar el interruptor, se restaura el renderer original.
  - Protanopia / Deuteranopia: paleta **Okabe-Ito** (`#0072B2`, `#E69F00`, `#56B4E9`, `#F0E442`, `#CC79A7`, `#D55E00`, …).
  - Tritanopia: paleta alternativa sin el eje azul-amarillo (`#D55E00`, `#009E73`, `#CC79A7`, `#882255`, `#44AA99`, `#117733`, …).
- **Corrección global por lente de matriz (SVG):** activando la casilla correspondiente, se aplica un filtro SVG `feColorMatrix` sobre todo el contenedor del mapa, incluyendo el mapa base, en lugar de modificar las capas.

Solo un tipo de daltonismo puede estar activo a la vez.

## Requisitos

- **ArcGIS Experience Builder Developer Edition 1.17** (el widget se desarrolló y probó con esta versión).
- **Node.js** en una versión compatible con Experience Builder 1.17 (ver la documentación oficial).
- Una cuenta de **ArcGIS Online** o **ArcGIS Enterprise** con un *Client ID* (App ID) registrado para iniciar sesión en Experience Builder Developer.
- Un web map con capas de entidades simbolizadas por color.

## Cómo replicarlo

### 1. Instalar Experience Builder Developer

1. Descargar ArcGIS Experience Builder Developer Edition 1.17 desde [developers.arcgis.com](https://developers.arcgis.com/experience-builder/guide/downloads/) y descomprimirlo.
2. Instalar y arrancar el servidor:

   ```bash
   cd ArcGISExperienceBuilder/server
   npm ci
   npm start
   ```

3. En otra terminal, instalar y arrancar el cliente:

   ```bash
   cd ArcGISExperienceBuilder/client
   npm ci
   npm start
   ```

4. Abrir `https://localhost:3001/` e iniciar sesión con la URL del portal y el *Client ID*.

### 2. Agregar el widget

1. Copiar la carpeta `widget-filter-view` de este repositorio en:

   ```
   ArcGISExperienceBuilder/client/your-extensions/widgets/
   ```

2. Si el cliente estaba corriendo, detenerlo y volver a ejecutar `npm start` para que compile el nuevo widget.

### 3. Usarlo en una aplicación

1. En Experience Builder, crear una nueva experiencia o abrir una existente.
2. Agregar un widget de **Mapa** y asignarle un web map.
3. En el panel de widgets, buscar **widget-filter-view** y arrastrarlo al lienzo.
4. En la configuración del widget, seleccionar el widget de Mapa en **Seleccionar mapa**.
5. Guardar y obtener la vista previa. El widget mostrará el riesgo para cada tipo de daltonismo y los interruptores para aplicar la corrección.

## Limitaciones conocidas

- Solo se procesan las capas de primer nivel del mapa; las subcapas dentro de *group layers* no se modifican.
- Solo se soportan renderers `simple`, `unique-value` y `class-breaks`. Otros tipos (heatmap, dot-density, visual variables de color, etc.) no se modifican.
- Las paletas tienen 8 colores; si una capa tiene más clases, los colores se repiten de forma cíclica.
- Los cambios se aplican solo en la sesión del navegador y no modifican el web map original.
- La interfaz está disponible únicamente en español.

## Autor

Sergio
