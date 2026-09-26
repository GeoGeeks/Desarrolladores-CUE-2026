/*
 * ESP32 + TSL2591 — IRRADIANCIA + COORDENADAS DD + ENVÍO A ARCGIS VELOCITY (HTTP Receiver)
 *

 *** //////////////// CONEXIONES ///////////////////

  * TSL2591
    SCL -> GPIO22
    SDA -> GPIO21
    3V3 y GND comunes
  
  * LEDS
    + LED ROJO -> GPIO5
    + LED VERDE -> GPIO18
    + LED AMARILLO -> GPIO 19
    3V3 y GND comunes

 */

// ── Importar librerías requeridas ────────────────────────────────────────
#include <Wire.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_TSL2591.h>

// ── Credenciales ──────────────────────────────────────────────────────────
const char* ssid         = "SU_SSID";
const char* password     = "SU_CONTRASEÑA";
const char* ApiKey = "SUS_CREDENCIALES_DE_API";

// ==== AGREGADO PARA API ====
// Debe utilizar una API para poder georeferenciar la ubicación actual del dispositivo. De ser necesario, modificiar la lógica actual para ello.

// URL del HTTP Endpoint Path que ArcGIS Velocity genera al crear el feed (aparece en la página de detalles del feed, después de crearlo).

const char* velocityEndpoint = "https://LA_URL_DE_SU_FEED";

// Credenciales de autenticación tipo "Basic" configuradas en el feed.
// Deben coincidir EXACTAMENTE 
// con las que se registren en el feed dentro de ArcGIS Velocity.
  // const char* velocityUser = "SU_USUARIO";
  // const char* velocityPass = "SU_CONTRASEÑA";

// ==== AGREGADO PARA TIMESTAMP (NTP) ====
// Servidores NTP para sincronizar el reloj interno del ESP32.
const char* ntpServer1 = "pool.ntp.org";
const char* ntpServer2 = "time.google.com";
// Se usa UTC (offset 0) para que el timestamp sea directamente
// comparable en ArcGIS Velocity, sin depender de usos horarios.
const long  gmtOffsetSec      = 0;
const int   daylightOffsetSec = 0;

bool relojSincronizado = false;
// ==== FIN AGREGADO ====

// ── Objetos ───────────────────────────────────────────────────────────────
Adafruit_TSL2591 tsl = Adafruit_TSL2591(2591);  //Se crea el objeto del sensor

// ── Estados ────────────────────────────────────────────────────────────────
float         latitud        = 0.0;
float         longitud       = 0.0;
float         altitud        = 0.0;
bool          ubicacionValida = false;
bool          altitudValida   = false;
unsigned long ultimaLectura   = 0;
const unsigned long INTERVALO = 4000;   // 4 segundos; 1 Segundo = 1000.

// ── Regresión cúbica ──────────────────────────────────────────────────────
float irradiancia(uint32_t x) {
  double v = 5.553e-12 * pow(x, 3) - 3.618e-7 * pow(x, 2) + 2.263e-2 * x - 29.53;
  return v < 0 ? 0.0 : (float)v;
}

// ==== AGREGADO PARA REGRESIÓN CÚBICA ====
// Esta regresión es producto de un estudio realizado por parte del expositor (Johan Rodríguez). En resumen, corresponde a una regresión Piranómetro vs TSL2591 que posteriormente fue filtrada mediante binning para emular el comportamiento de un piranómetro.
// ==== FIN AGREGADO ====


// ── Construye el payload JSON con las redes WiFi visibles (geolocalización) ──
String construirPayload() {
  int n = WiFi.scanNetworks();
  StaticJsonDocument<2048> doc;
  JsonArray wifiAccessPoints = doc.createNestedArray("wifiAccessPoints");

  for (int i = 0; i < n; i++) {
    JsonObject ap = wifiAccessPoints.createNestedObject();
    ap["macAddress"]     = WiFi.BSSIDstr(i);
    ap["signalStrength"] = WiFi.RSSI(i);
  }

  String payload;
  serializeJson(doc, payload);
  return payload;
}

// ── Consulta la API de geolocalización (se llama una sola vez) ────────────
bool obtenerCoordenadas() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("ERROR: sin conexión WiFi, no se puede geolocalizar.");
    return false;
  }

  String url = "https://LA_URL_DE_SU_API" + String(ApiKey);

  WiFiClientSecure client;
  client.setInsecure();

  HTTPClient http;
  http.setConnectTimeout(15000);
  http.setTimeout(15000);
  http.begin(client, url);
  http.addHeader("Content-Type", "application/json");
  int codigo = http.POST(construirPayload());

  if (codigo != 200) {
    Serial.printf("ERROR: API geolocalización respondió, código %d\n", codigo);
    http.end();
    return false;
  }

  StaticJsonDocument<512> doc;
  DeserializationError err = deserializeJson(doc, http.getString());
  http.end();

  if (err) {
    Serial.println("ERROR: no se pudo parsear respuesta JSON.");
    return false;
  }

  // Equivalencia a un GET.
  latitud  = doc["location"]["lat"];
  longitud = doc["location"]["lng"];
  return true;
}

// ── Consulta la Elevation API con la lat/lon ya obtenida (una sola vez) ───
bool obtenerAltitud() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("ERROR: sin conexión WiFi, no se puede obtener altitud.");
    return false;
  }

  String url = "https://LA_URL_DE_SU_API"
               + String(latitud, 6) + "," + String(longitud, 6)
               + "&key=" + String(ApiKey);

  WiFiClientSecure client;
  client.setInsecure();

  HTTPClient http;
  http.setConnectTimeout(15000);
  http.setTimeout(15000);
  http.begin(client, url);
  int codigo = http.GET();

  if (codigo != 200) {
    Serial.printf("ERROR: La API respondió, código %d\n", codigo);
    http.end();
    return false;
  }

  StaticJsonDocument<1024> doc;
  DeserializationError err = deserializeJson(doc, http.getString());
  http.end();

  if (err || doc["status"] != "OK") {
    Serial.println("ERROR: no se pudo parsear respuesta de Elevation API.");
    return false;
  }

  altitud = doc["results"][0]["elevation"];
  return true;
}

// ==== AGREGADO PARA TIMESTAMP (NTP) ====
// Sincroniza el reloj interno del ESP32 vía NTP. Se llama una sola vez
// en setup(), igual que la geolocalización y la altitud. Una vez
// sincronizado, el reloj interno del ESP32 sigue avanzando solo,
// así que no hace falta repetir esta llamada en cada lectura.
bool sincronizarReloj() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("ERROR: sin conexión WiFi, no se puede sincronizar el reloj.");
    return false;
  }

  configTime(gmtOffsetSec, daylightOffsetSec, ntpServer1, ntpServer2);

  struct tm horaActual;
  unsigned long inicio = millis();
  // getLocalTime reintenta internamente; se limita el intento total a 15 s
  while (!getLocalTime(&horaActual, 1000) && millis() - inicio < 15000) {
    Serial.print(".");
  }

  if (horaActual.tm_year < (2020 - 1900)) {
    // Si el año obtenido es absurdo, la sincronización falló
    return false;
  }
  return true;
}
// ==== FIN AGREGADO ====

// ── Imprime irradiancia + coordenadas en el formato solicitado ────────────
void imprimirLectura(float irr) {
  String device_id = "ESP32_01";     //TrackingID de ArcGIS Velocity
  String x = ubicacionValida ? String(latitud, 6)  : "";
  String y = ubicacionValida ? String(longitud, 6) : "";
  String z = altitudValida   ? String(altitud, 2)  : "";
  Serial.printf("%s, %.4f, (%s, %s, %s)\n", device_id.c_str(), irr, x.c_str(), y.c_str(), z.c_str());
}

// Función para encender los indicadores LED del dispositivo
void encenderLed(float irr)
{
  //Estado bajo -> Encender LED Rojo (GPIO5)
  if (irr < 300.0)
  {
    digitalWrite(5, HIGH);
    digitalWrite(18, LOW);
    digitalWrite(19, LOW);
  }

  //Estado normal -> Encender LED Verde (GPIO18)
  else if (irr >= 300.0 && irr <= 700.0)
  {
    digitalWrite(5, LOW);
    digitalWrite(18, HIGH);
    digitalWrite(19, LOW);
  }
  //Estado alto -> Encender LED Amarillo (GPIO19)
  else if (irr > 700.0)
  {
    digitalWrite(5, LOW);
    digitalWrite(18, LOW);
    digitalWrite(19, HIGH);
  }
}

// ==== AGREGADO PARA ARCGIS VELOCITY ====
// Arma el JSON plano que espera el feed HTTP Receiver, con los 6 campos
// acordados: devide_id, irradiancia, latitud, longitud, altitud y timestamp.
String construirPayloadVelocity(float irr) {
  StaticJsonDocument<256> doc;
  doc["device_id"] = "ESP32_01";
  doc["irradiancia"] = irr;
  doc["latitud"]     = ubicacionValida ? latitud  : 0.0;
  doc["longitud"]    = ubicacionValida ? longitud : 0.0;
  doc["altitud"]     = altitudValida   ? altitud  : 0.0;

  // ==== AGREGADO PARA TIMESTAMP (NTP) ====
  // Se envía como epoch en segundos (entero), porque ArcGIS Velocity reconoce
  // ese formato automáticamente en el paso de Date and time, sin
  // necesidad de indicar un Date format ni hacer conversión adicional.
  time_t ahora = 0;
  if (relojSincronizado) {
    time(&ahora); // el reloj interno ya sincronizado sigue avanzando solo
  }
  doc["timestamp"] = (uint32_t)ahora; // epoch en segundos UTC
  // ==== FIN AGREGADO ====

  String payload;
  serializeJson(doc, payload);
  return payload;
}

// Envía el payload por HTTP POST al endpoint del feed HTTP Receiver,
void enviarAVelocity(float irr) {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("ERROR: sin conexión WiFi, no se puede enviar a Velocity.");
    return;
  }

  WiFiClientSecure client;
  client.setInsecure(); // mismo criterio usado en las demás llamadas HTTPS de este sketch

  HTTPClient http;
  http.setConnectTimeout(15000);
  http.setTimeout(15000);
  http.begin(client, velocityEndpoint);
  http.addHeader("Content-Type", "application/json");
  // http.setAuthorization(velocityUser, velocityPass); // autenticación Basic

  //Encender los LEDs dependiendo de la radiación
  encenderLed(irr);

  String payload = construirPayloadVelocity(irr);
  int codigo = http.POST(payload);

  if (codigo > 0) {
    Serial.printf("ArcGIS Velocity: POST enviado, código %d\n", codigo);
  } else {
    Serial.printf("ERROR: no se pudo enviar a ArcGIS Velocity (%s)\n", http.errorToString(codigo).c_str());
  }
  http.end();

}
// ==== FIN AGREGADO ====

// ── Setup ─────────────────────────────────────────────────────────────────
void setup() {
  Serial.begin(9600);
  delay(1000);

  // Establecer pines 5, 18 y 19 como salida digital
  pinMode (5, OUTPUT);
  pinMode (18, OUTPUT);
  pinMode (19, OUTPUT);

  // Checklist 1: WiFi
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);
  Serial.print("Conectando a WiFi");
  unsigned long inicio = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - inicio < 15000) {
    delay(500);
    Serial.print(".");
  }
  Serial.println();
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("WiFi OK");
  } else {
    Serial.println("ERROR: no se pudo conectar a WiFi (continúa sin coordenadas).");
  }

  // Checklist 2: TSL2591
  Wire.begin(21, 22);
  if (!tsl.begin()) {
    Serial.println("ERROR: TSL2591 no encontrado. Verifica SDA→GPIO21, SCL→GPIO22.");
    while (1);
  }

  // Parámetros estándar para configurar el sensor TSL2591
  tsl.setGain(TSL2591_GAIN_LOW);
  tsl.setTiming(TSL2591_INTEGRATIONTIME_100MS);
  Serial.println("TSL2591 OK");

  // Checklist 3: Sincronización de reloj (una sola vez)
  relojSincronizado = sincronizarReloj();
  Serial.println(relojSincronizado ? "Reloj sincronizado (NTP)." : "Reloj NO sincronizado.");

  // Checklist 4: Coordenadas (una sola consulta)
  ubicacionValida = obtenerCoordenadas();
  Serial.println(ubicacionValida ? "Coordenadas obtenidas." : "Coordenadas no disponibles.");

  // Checklist 4: Altitud (una sola consulta, requiere lat/lon previos)
  if (ubicacionValida) {
    altitudValida = obtenerAltitud();
    Serial.println(altitudValida ? "Altitud obtenida." : "Altitud no disponible.");
  } else {
    Serial.println("Se omite consulta de altitud: sin coordenadas.");
  }

  ultimaLectura = millis() - INTERVALO; // primera lectura inmediata
}

// ── Loop ──────────────────────────────────────────────────────────────────
void loop() {

  // Se obtiene una lectura del sensor cada INTERVALO segundos.
  if (millis() - ultimaLectura < INTERVALO) return;
  ultimaLectura = millis();

  // Obtención de la irradiancia (TSL2591): Total = Visible + IR
  uint32_t full  = tsl.getFullLuminosity();
  uint32_t total = (full & 0xFFFF) + (full >> 16);
  float    irr   = irradiancia(total);

  // Se llama la función que imprime los datos en el monitor serial
  imprimirLectura(irr);

  // Se llama la función que envía los datos a ArcGIS Velocity.
  enviarAVelocity(irr);

}
