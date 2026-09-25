
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

// URL de nuestra capa de estaciones ambientales.
const string URL_CAPA =
    "https://geoapps.esri.co/server/rest/services/" +
    "Hosted/Estaciones_ambientales/FeatureServer/0";

const int TAMANO_LOTE = 500;

using HttpClient cliente = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(60)
};

Console.WriteLine("==================================");
Console.WriteLine("    GEOGEEKS - PRUEBA DE SOE");
Console.WriteLine("==================================");
Console.WriteLine();

Console.Write("Ingresa el umbral de CO (ejemplo: 600): ");

string? entrada = Console.ReadLine();

if (!double.TryParse(
        entrada,
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out double umbralCO)
    || !double.IsFinite(umbralCO))
{
    Console.WriteLine(
        "Error: ingresa un numero valido, por ejemplo 600.");

    return;
}

try
{
    Console.WriteLine();
    Console.WriteLine("Consultando ArcGIS Enterprise...");

    // 1. Obtener todos los ObjectID.

    var parametrosIds = new Dictionary<string, string>
    {
        ["where"] = "1=1",
        ["returnIdsOnly"] = "true",
        ["f"] = "json"
    };

    using JsonDocument respuestaIds =
        await ConsultarAsync(parametrosIds);

    JsonElement idsJson =
        respuestaIds.RootElement.GetProperty("objectIds");

    List<int> objectIds = idsJson.ValueKind ==
                          JsonValueKind.Null
        ? new List<int>()
        : idsJson.EnumerateArray()
            .Select(id => id.GetInt32())
            .Distinct()
            .OrderBy(id => id)
            .ToList();

    // 2. Recuperar estaciones por lotes.

    var estaciones = new List<Estacion>();

    for (int inicio = 0;
         inicio < objectIds.Count;
         inicio += TAMANO_LOTE)
    {
        List<int> lote = objectIds
            .Skip(inicio)
            .Take(TAMANO_LOTE)
            .ToList();

        var parametros = new Dictionary<string, string>
        {
            ["objectIds"] = string.Join(",", lote),
            ["outFields"] =
                "objectid,nombre,co,temperatura,estado",
            ["returnGeometry"] = "false",
            ["f"] = "json"
        };

        using JsonDocument respuesta =
            await ConsultarAsync(parametros);

        JsonElement features =
            respuesta.RootElement.GetProperty("features");

        foreach (JsonElement feature in
                 features.EnumerateArray())
        {
            JsonElement atributos =
                feature.GetProperty("attributes");

            JsonElement coJson =
                atributos.GetProperty("co");

            double? co = coJson.ValueKind ==
                         JsonValueKind.Number
                ? coJson.GetDouble()
                : null;

            estaciones.Add(new Estacion(
                atributos.GetProperty("objectid").GetInt32(),
                atributos.GetProperty("nombre").GetString()
                    ?? "Sin nombre",
                co
            ));
        }
    }

    // 3. Comprobar que recuperamos todos los registros.

    var recuperados = estaciones
        .Select(e => e.ObjectId)
        .ToHashSet();

    if (objectIds.Any(id => !recuperados.Contains(id)))
    {
        throw new Exception(
            "La consulta no devolvio todas las estaciones.");
    }

    // 4. Calcular indicadores.

    List<Estacion> conCO = estaciones
        .Where(e => e.CO.HasValue)
        .ToList();

    List<Estacion> sobreUmbral = conCO
        .Where(e => e.CO!.Value > umbralCO)
        .OrderByDescending(e => e.CO)
        .ToList();

    double? promedio = conCO.Count > 0
        ? conCO.Average(e => e.CO!.Value)
        : null;

    Estacion? estacionMaxima = conCO
        .OrderByDescending(e => e.CO)
        .FirstOrDefault();

    double porcentaje = conCO.Count > 0
        ? sobreUmbral.Count * 100.0 / conCO.Count
        : 0;

    // 5. Mostrar los resultados.

    Console.WriteLine();
    Console.WriteLine("==================================");
    Console.WriteLine("      RESULTADOS DEL ANALISIS");
    Console.WriteLine("==================================");

    Console.WriteLine(
        $"Total de estaciones: {estaciones.Count}");

    Console.WriteLine(
        $"Estaciones con CO: {conCO.Count}");

    Console.WriteLine(
        $"Estaciones sin CO: " +
        $"{estaciones.Count - conCO.Count}");

    Console.WriteLine(
        $"Promedio CO: {promedio?.ToString("F2") ?? "N/D"}");

    Console.WriteLine(
        $"CO maximo: " +
        $"{estacionMaxima?.CO?.ToString("F2") ?? "N/D"}");

    Console.WriteLine(
        $"Estacion con mayor CO: " +
        $"{estacionMaxima?.Nombre ?? "N/D"}");

    Console.WriteLine(
        $"Estaciones sobre el umbral: {sobreUmbral.Count}");

    Console.WriteLine(
        $"Porcentaje sobre el umbral: {porcentaje:F2}%");

    Console.WriteLine();
    Console.WriteLine("ESTACIONES QUE SUPERAN EL UMBRAL");
    Console.WriteLine("----------------------------------");

    foreach (Estacion estacion in sobreUmbral)
    {
        Console.WriteLine(
            $"{estacion.Nombre}: {estacion.CO:F2}");
    }

    Console.WriteLine();
    Console.WriteLine("Prueba finalizada.");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("Error durante la prueba:");
    Console.WriteLine(ex.Message);
}

// Metodo para consultar ArcGIS REST.
async Task<JsonDocument> ConsultarAsync(
    Dictionary<string, string> parametros)
{
    using var contenido =
        new FormUrlEncodedContent(parametros);

    using HttpResponseMessage respuesta =
        await cliente.PostAsync(
            URL_CAPA + "/query",
            contenido);

    respuesta.EnsureSuccessStatusCode();

    string json =
        await respuesta.Content.ReadAsStringAsync();

    JsonDocument documento =
        JsonDocument.Parse(json);

    if (documento.RootElement.TryGetProperty(
            "error", out JsonElement error))
    {
        documento.Dispose();

        throw new Exception(
            "Error de ArcGIS REST: " + error.ToString());
    }

    return documento;
}

// Modelo de datos para las estaciones.
record Estacion(
    int ObjectId,
    string Nombre,
    double? CO
);