
// Copyright 2018 ESRI
//
// All rights reserved under the copyright laws of the United States
// and applicable international laws, treaties, and conventions.
//
// You may freely redistribute and use this sample code, with or
// without modification, provided you include the original copyright
// notice and use restrictions.
//
// See the use restrictions at
// <your Enterprise SDK install location>/userestrictions.txt.
//
// Adaptacion de GeoGeeksSOE para consultar
// Estaciones_ambientales mediante ArcGIS REST API.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.Server.SOESupport;

namespace GeoGeeksSOE
{
    [ComVisible(true)]
    [Guid("eb619a25-9c4c-4e88-babb-3ad2e74ac8e3")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectExtension(
        "MapServer",
        AllCapabilities = "",
        DefaultCapabilities = "",
        Description = "Analisis de estaciones ambientales mediante REST",
        DisplayName = "GeoGeeksSOE",
        Properties = "",
        SupportsREST = true,
        SupportsSOAP = false,
        SupportsSharedInstances = false)]
    public class GeoGeeksSOE :
        IServerObjectExtension,
        IObjectConstruct,
        IRESTRequestHandler
    {
        // Capa confirmada: Estaciones_ambientales (ID 0).
        private const string URL_CAPA =
            "https://geoapps.esri.co/server/rest/services/" +
            "Hosted/Estaciones_ambientales/FeatureServer/0";

        private const int TAMANO_PAGINA = 500;

        private readonly string soe_name;
        private readonly IRESTRequestHandler reqHandler;
        private readonly ServerLogger logger;

        private IPropertySet configProps;
        private IServerObjectHelper serverObjectHelper;

        private static readonly HttpClient httpClient =
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

        // ==========================================
        // MODELO DE DATOS
        // ==========================================

        private class Estacion
        {
            public int ObjectId { get; set; }

            public string Nombre { get; set; }

            public double? CO { get; set; }

            public double? Temperatura { get; set; }

            public string Estado { get; set; }

            public double? Latitud { get; set; }

            public double? Longitud { get; set; }
        }

        // ==========================================
        // CONSTRUCTOR
        // ==========================================

        public GeoGeeksSOE()
        {
            soe_name = GetType().Name;

            logger = new ServerLogger();

            reqHandler = new SoeRestImpl(
                soe_name,
                CreateRestSchema()
            ) as IRESTRequestHandler;
        }

        // ==========================================
        // IServerObjectExtension
        // ==========================================

        public void Init(IServerObjectHelper pSOH)
        {
            serverObjectHelper = pSOH;
        }

        public void Shutdown()
        {
        }

        // ==========================================
        // IObjectConstruct
        // ==========================================

        public void Construct(IPropertySet props)
        {
            configProps = props;
        }

        // ==========================================
        // IRESTRequestHandler
        // ==========================================

        public string GetSchema()
        {
            return reqHandler.GetSchema();
        }

        public byte[] HandleRESTRequest(
            string Capabilities,
            string resourceName,
            string operationName,
            string operationInput,
            string outputFormat,
            string requestProperties,
            out string responseProperties)
        {
            return reqHandler.HandleRESTRequest(
                Capabilities,
                resourceName,
                operationName,
                operationInput,
                outputFormat,
                requestProperties,
                out responseProperties
            );
        }

        // ==========================================
        // ESQUEMA REST
        // ==========================================

        private RestResource CreateRestSchema()
        {
            RestResource rootRes = new RestResource(
                soe_name,
                false,
                RootResHandler
            );

            RestOperation analizarOper = new RestOperation(
                "analizarEstaciones",
                new string[] { "umbralCO" },
                new string[] { "json" },
                AnalizarEstacionesHandler
            );

            rootRes.operations.Add(analizarOper);

            return rootRes;
        }

        // ==========================================
        // RECURSO PRINCIPAL
        // ==========================================

        private byte[] RootResHandler(
            NameValueCollection boundVariables,
            string outputFormat,
            string requestProperties,
            out string responseProperties)
        {
            responseProperties = null;

            var resultado = new
            {
                extension = "GeoGeeksSOE",
                descripcion =
                    "Analisis de estaciones ambientales",
                fuente = URL_CAPA,
                operaciones = new[]
                {
                    "analizarEstaciones"
                }
            };

            return Serializar(resultado);
        }

        // ==========================================
        // OPERACION: analizarEstaciones
        // ==========================================

        private byte[] AnalizarEstacionesHandler(
            NameValueCollection boundVariables,
            JsonObject operationInput,
            string outputFormat,
            string requestProperties,
            out string responseProperties)
        {
            responseProperties = null;

            // ==========================================
            // 1. OBTENER Y VALIDAR EL UMBRAL DE CO
            // ==========================================

            double? umbralRecibido;

            bool encontrado = operationInput.TryGetAsDouble(
                "umbralCO",
                out umbralRecibido
            );

            if (!encontrado || !umbralRecibido.HasValue)
            {
                throw new ArgumentException(
                    "Debes proporcionar el parametro umbralCO."
                );
            }

            double umbralCO = umbralRecibido.Value;

            if (double.IsNaN(umbralCO) ||
                double.IsInfinity(umbralCO))
            {
                throw new ArgumentException(
                    "El parametro umbralCO no es valido."
                );
            }

            // 3. Consultar las estaciones.

            List<Estacion> estaciones =
                ConsultarTodasLasEstaciones();

            // 4. Filtrar estaciones con valores de CO.

            List<Estacion> estacionesConCO =
                estaciones
                    .Where(e => e.CO.HasValue)
                    .ToList();

            // 5. Estaciones que superan el umbral.

            List<Estacion> estacionesSobreUmbral =
                estacionesConCO
                    .Where(e => e.CO.Value > umbralCO)
                    .OrderByDescending(e => e.CO.Value)
                    .ToList();

            // 6. Promedio de CO.

            double? promedioCO = null;

            if (estacionesConCO.Count > 0)
            {
                promedioCO = estacionesConCO
                    .Average(e => e.CO.Value);
            }

            // 7. Estacion con mayor CO.

            Estacion estacionMaxima =
                estacionesConCO
                    .OrderByDescending(e => e.CO.Value)
                    .FirstOrDefault();

            // 8. Porcentaje sobre el umbral.

            double porcentajeSobreUmbral = 0;

            if (estacionesConCO.Count > 0)
            {
                porcentajeSobreUmbral =
                    (double)estacionesSobreUmbral.Count /
                    estacionesConCO.Count * 100;
            }

            // 9. Preparar los resultados.

            var resultado = new
            {
                operacion = "analizarEstaciones",

                umbralCO = umbralCO,

                resumen = new
                {
                    totalEstaciones = estaciones.Count,

                    estacionesConCO =
                        estacionesConCO.Count,

                    estacionesSinCO =
                        estaciones.Count -
                        estacionesConCO.Count,

                    promedioCO = promedioCO,

                    maximoCO = estacionMaxima != null
                        ? estacionMaxima.CO
                        : null,

                    estacionMaximoCO = estacionMaxima != null
                        ? estacionMaxima.Nombre
                        : null,

                    estacionesSobreUmbral =
                        estacionesSobreUmbral.Count,

                    porcentajeSobreUmbral =
                        porcentajeSobreUmbral
                },

                detalle = estacionesSobreUmbral
                    .Select(e => new
                    {
                        objectId = e.ObjectId,
                        nombre = e.Nombre,
                        co = e.CO,
                        temperatura = e.Temperatura,
                        estado = e.Estado,
                        latitud = e.Latitud,
                        longitud = e.Longitud
                    })
                    .ToList()
            };

            // 10. Devolver el resultado en JSON.

            return Serializar(resultado);
        }

        // ==========================================
        // CONSULTAR TODAS LAS ESTACIONES
        // ==========================================

        private List<Estacion> ConsultarTodasLasEstaciones()
        {
            List<Estacion> estaciones =
                new List<Estacion>();

            // Obtener primero los ObjectID.

            List<int> objectIds = ConsultarObjectIds();

            if (objectIds.Count == 0)
            {
                return estaciones;
            }

            objectIds.Sort();

            // Consultar los registros por lotes.

            for (int inicio = 0;
                 inicio < objectIds.Count;
                 inicio += TAMANO_PAGINA)
            {
                List<int> lote = objectIds
                    .Skip(inicio)
                    .Take(TAMANO_PAGINA)
                    .ToList();

                List<Estacion> estacionesLote =
                    ConsultarEstacionesPorIds(lote);

                estaciones.AddRange(estacionesLote);
            }

            // Verificar que no falten registros.

            HashSet<int> idsRecuperados =
                new HashSet<int>(
                    estaciones.Select(e => e.ObjectId)
                );

            List<int> idsFaltantes = objectIds
                .Where(id => !idsRecuperados.Contains(id))
                .ToList();

            if (idsFaltantes.Count > 0)
            {
                throw new InvalidOperationException(
                    "No se recuperaron todas las estaciones. " +
                    "Registros faltantes: " +
                    idsFaltantes.Count
                );
            }

            return estaciones;
        }

        // ==========================================
        // OBTENER OBJECTIDS
        // ==========================================

        private List<int> ConsultarObjectIds()
        {
            var parametros = new Dictionary<string, string>
            {
                { "where", "1=1" },
                { "returnIdsOnly", "true" },
                { "f", "json" }
            };

            string respuesta = EjecutarConsulta(parametros);

            using (JsonDocument documento =
                JsonDocument.Parse(respuesta))
            {
                JsonElement raiz = documento.RootElement;

                JsonElement idsJson;

                if (!raiz.TryGetProperty(
                    "objectIds",
                    out idsJson))
                {
                    throw new InvalidOperationException(
                        "ArcGIS no devolvio los ObjectID."
                    );
                }

                if (idsJson.ValueKind ==
                    JsonValueKind.Null)
                {
                    return new List<int>();
                }

                List<int> ids = new List<int>();

                foreach (JsonElement id in
                    idsJson.EnumerateArray())
                {
                    ids.Add(id.GetInt32());
                }

                return ids.Distinct().ToList();
            }
        }

        // ==========================================
        // CONSULTAR ESTACIONES POR OBJECTID
        // ==========================================

        private List<Estacion> ConsultarEstacionesPorIds(
            List<int> ids)
        {
            List<Estacion> estaciones =
                new List<Estacion>();

            if (ids.Count == 0)
            {
                return estaciones;
            }

            var parametros = new Dictionary<string, string>
            {
                {
                    "objectIds",
                    string.Join(",", ids)
                },
                {
                    "outFields",
                    "objectid,nombre,co,temperatura," +
                    "estado,latitud,longitud"
                },
                {
                    "returnGeometry",
                    "false"
                },
                {
                    "f",
                    "json"
                }
            };

            string respuesta = EjecutarConsulta(parametros);

            using (JsonDocument documento =
                JsonDocument.Parse(respuesta))
            {
                JsonElement raiz = documento.RootElement;

                JsonElement features;

                if (!raiz.TryGetProperty(
                    "features",
                    out features))
                {
                    throw new InvalidOperationException(
                        "ArcGIS no devolvio la lista de estaciones."
                    );
                }

                foreach (JsonElement feature in
                    features.EnumerateArray())
                {
                    JsonElement atributos =
                        feature.GetProperty("attributes");

                    Estacion estacion = new Estacion
                    {
                        ObjectId = LeerEntero(
                            atributos,
                            "objectid"
                        ),

                        Nombre = LeerTexto(
                            atributos,
                            "nombre"
                        ),

                        CO = LeerNumero(
                            atributos,
                            "co"
                        ),

                        Temperatura = LeerNumero(
                            atributos,
                            "temperatura"
                        ),

                        Estado = LeerTexto(
                            atributos,
                            "estado"
                        ),

                        Latitud = LeerNumero(
                            atributos,
                            "latitud"
                        ),

                        Longitud = LeerNumero(
                            atributos,
                            "longitud"
                        )
                    };

                    estaciones.Add(estacion);
                }
            }

            return estaciones;
        }

        // ==========================================
        // EJECUTAR CONSULTA REST
        // ==========================================

        private string EjecutarConsulta(
            Dictionary<string, string> parametros)
        {
            string url = URL_CAPA + "/query";

            using (FormUrlEncodedContent contenido =
                new FormUrlEncodedContent(parametros))
            {
                using (HttpResponseMessage respuesta =
                    httpClient
                        .PostAsync(url, contenido)
                        .GetAwaiter()
                        .GetResult())
                {
                    respuesta.EnsureSuccessStatusCode();

                    string json = respuesta.Content
                        .ReadAsStringAsync()
                        .GetAwaiter()
                        .GetResult();

                    // ArcGIS puede devolver errores
                    // incluso con una respuesta HTTP 200.

                    using (JsonDocument documento =
                        JsonDocument.Parse(json))
                    {
                        JsonElement error;

                        if (documento.RootElement.TryGetProperty(
                            "error",
                            out error))
                        {
                            throw new InvalidOperationException(
                                "Error de ArcGIS REST: " +
                                error.ToString()
                            );
                        }
                    }

                    return json;
                }
            }
        }

        // ==========================================
        // METODOS AUXILIARES
        // ==========================================

        private static string LeerTexto(
            JsonElement atributos,
            string campo)
        {
            JsonElement valor;

            if (!atributos.TryGetProperty(
                campo,
                out valor))
            {
                return null;
            }

            if (valor.ValueKind ==
                JsonValueKind.Null)
            {
                return null;
            }

            return valor.ToString();
        }

        private static double? LeerNumero(
            JsonElement atributos,
            string campo)
        {
            JsonElement valor;

            if (!atributos.TryGetProperty(
                campo,
                out valor))
            {
                return null;
            }

            if (valor.ValueKind !=
                JsonValueKind.Number)
            {
                return null;
            }

            double numero;

            if (!valor.TryGetDouble(out numero))
            {
                return null;
            }

            if (double.IsNaN(numero) ||
                double.IsInfinity(numero))
            {
                return null;
            }

            return numero;
        }

        private static int LeerEntero(
            JsonElement atributos,
            string campo)
        {
            JsonElement valor;

            if (!atributos.TryGetProperty(
                campo,
                out valor))
            {
                throw new InvalidOperationException(
                    "No se encontro el campo: " + campo
                );
            }

            return valor.GetInt32();
        }

        private static byte[] Serializar(
            object resultado)
        {
            string json =
                JsonSerializer.Serialize(resultado);

            return Encoding.UTF8.GetBytes(json);
        }
    }
}
