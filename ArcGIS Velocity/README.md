# Sistema IoT de Telemetría de Irradiancia Solar con ESP32 y ArcGIS Velocity

![Hardware](https://img.shields.io/badge/Hardware-ESP32-blue?style=flat-square&logo=espressif)
![Sensor](https://img.shields.io/badge/Sensor-TSL2591-orange?style=flat-square)
![Platform](https://img.shields.io/badge/GIS-ArcGIS%20Velocity-green?style=flat-square&logo=esri)
![Language](https://img.shields.io/badge/Language-C%2B%2B%20%2F%20Arduino-00599C?style=flat-square&logo=c%2B%2B)

Sistema embebido de telemetría IoT orientado a la medición, procesamiento y transmisión en tiempo real de datos de irradiancia solar hacia **ArcGIS Velocity**. La solución integra un microcontrolador ESP32 y un sensor óptico de alta precisión TSL2591, complementado con funciones de geolocalización por redes Wi-Fi, consulta de elevación topográfica, sincronización de tiempo en formato Epoch UTC (NTP) e indicación visual de umbrales mediante LEDs.

---

## Tabla de Contenidos
- [Esquema de Hardware y Pinout](#esquema-de-hardware-y-pinout)
- [Modelo de Conversión (Regresión Cúbica)](#modelo-de-conversión-regresión-cúbica)
- [Requisitos de Software y Librerías](#requisitos-de-software-y-librerías)
- [Estructura del Payload JSON](#estructura-del-payload-json)
- [Guía de Replicación e Instalación](#guía-de-replicación-e-instalación)
- [Configuración del Feed en ArcGIS Velocity](#configuración-del-feed-en-arcgis-velocity)

---

## Esquema de Hardware y Pinout

### Componentes Requeridos
- Microcontrolador **ESP32 Dev Module**.
- Sensor de luz digital de alta precisión **Adafruit TSL2591**.
- 3 LEDs indicadores (Rojo, Verde, Amarillo) con sus respectivas resistencias de protección (220Ω).
- Protoboard y cableado de conexión.

### Asignación de Pines (Pinout)

| Componente | Pin del Componente | Pin ESP32 (GPIO) | Descripción |
| :--- | :--- | :--- | :--- |
| **TSL2591** | SCL | `GPIO 22` | Línea de reloj bus I2C |
| **TSL2591** | SDA | `GPIO 21` | Línea de datos bus I2C |
| **LED Rojo** | Anodo (+) | `GPIO 5` | Estado Bajo (< 300 W/m²) |
| **LED Verde** | Anodo (+) | `GPIO 18` | Estado Normal (300 - 700 W/m²) |
| **LED Amarillo** | Anodo (+) | `GPIO 19` | Estado Alto (> 700 W/m²) |

---

## Modelo de Conversión (Regresión Cúbica)

La conversión de la lectura cruda del sensor TSL2591 a unidades de irradiancia ($W/m^2$) se realiza mediante un modelo matemático de regresión cúbica derivado de un estudio comparativo directo entre un piranómetro de grado científico y el TSL2591, aplicando un filtrado posterior mediante *binning* para emular la respuesta espectral del piranómetro:

$$\text{Irradiancia } (v) = 5.553 \times 10^{-12} \cdot x^3 - 3.618 \times 10^{-7} \cdot x^2 + 2.263 \times 10^{-2} \cdot x - 29.53$$

*Donde x corresponde al conteo de radiación total (Espectro Visible + Infrarrojo).*

---

## Requisitos de Software y Librerías

Para la compilación del firmware en **Arduino IDE**, se requiere el paquete de tarjetas `esp32` (Espressif Systems) y las siguientes librerías:

- `Wire.h` (Incluida en el core de ESP32)
- `WiFi.h`, `WiFiClientSecure.h`, `HTTPClient.h` (Incluidas en el core de ESP32)
- [**ArduinoJson**](https://arduinojson.org/) (Versión 6.x)
- [**Adafruit Sensor**](https://github.com/adafruit/Adafruit_Sensor)
- [**Adafruit TSL2591 Library**](https://github.com/adafruit/Adafruit_TSL2591_Library)

---

## Estructura del Payload JSON

El mensaje emitido periódicamente por el ESP32 hacia el endpoint de ArcGIS Velocity cumple con el esquema estandarizado:

```json
{
  "device_id": "ESP32_01",
  "irradiancia": 542.1850,
  "latitud": 4.609710,
  "longitud": -74.081750,
  "altitud": 2600.50,
  "timestamp": 1727345297
}
```

## Guía de Replicación e Instalación

1. Configuración del entorno de desarrollo: 
- Instale la extensión de tarjetas ESP32 en Arduino IDE.
- Instale las librerías ArduinoJSON y Adafruit TSL2591.

```markdown
2. Parámetros del Firmware:
Abra el archivo *Velocity_ESP32.ino* y configure las siguientes variables:
```cpp
- const char* ssid             = "SU_SSID_WIFI";
- const char* password         = "SU_PASSWORD_WIFI";
- const char* ApiKey           = "SU_API_KEY";
- const char* velocityEndpoint = "SU_VELOCITY_ENDPOINT_URL";
```

3. Carga al microcontrolador:
- Conecte el ESP32 mediante el cable USB
- Seleccione la placa ESP32 Dev Module y el puerto COM correspondiente
- Compile y suba el firmware (Si es necesario, presione el botón *BOOT* de su placa cuando se esté subiendo el código).
- Abra el monitor serial a 9600 baudios para verificar el proceso de inicio

## Configuración del Feed en ArcGIS Velocity
1. Ingrese a ArcGIS Velocity y cree un nuevo Feed de tipo HTTP Receiver (Tal cual como se expuso en la presentación).
2. Copie la URL del Endpoint Path e ingréselo en la variable *velocityEndpoint* del firmware.
3. Vuelva a cargar el firmware con la anterior modificación. En este punto ya podrá abrir el panel de *log* del feed para verificar la recepción de datos.
