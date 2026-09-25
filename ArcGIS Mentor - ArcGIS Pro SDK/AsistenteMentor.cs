using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Internal.Core.Assistant;

namespace ArcGISMentorAddIn
{
    /// <summary>
    /// Extension for the ArcGIS Pro AI assistant that answers questions about an
    /// organization's own geoprocessing toolboxes.
    ///
    /// Pro's built-in assistant already knows about the system toolboxes that ship with the
    /// product, but it has no visibility into the .pyt and .atbx toolboxes an organization
    /// wrote for itself. This extension closes that gap by reading a catalog built ahead of
    /// time from the organization's own code, and answering with what that catalog knows —
    /// including what it does not know, instead of guessing.
    ///
    /// The catalog itself is not read here. Each function shells out to a Python module
    /// (toolscout.pro) that already implements the search, verification and correction
    /// logic and is covered by its own test suite. Reimplementing that logic in C# would
    /// mean keeping two independent judgments in sync — cheap to get wrong, expensive to
    /// notice.
    ///
    /// The Description attributes below are not documentation for other developers; they
    /// are the text the language model reads to decide whether to call this extension at
    /// all, so they are written as an assistant would phrase the question, not as an API
    /// reference.
    /// </summary>
    [Description(
        "Conoce el catálogo de herramientas de geoprocesamiento propias de esta organización: sus " +
        "toolboxes .pyt y .atbx, leídas de su propio código. No sabe nada de las herramientas de " +
        "sistema de ArcGIS, que ya tienen quien las busque.")]
    internal class AsistenteMentor : AIAssistantExtension
    {
        /// <summary>How long to wait on the Python subprocess. A large catalog can take a
        /// moment to query, and leaving the assistant conversation hanging indefinitely is
        /// not an acceptable failure mode.</summary>
        private const int EsperaMs = 30000;

        /// <summary>
        /// System prompt for this extension.
        ///
        /// Pro's own geoprocessing assistant extension overrides this same property to
        /// register its instructions with the router that decides which extension answers a
        /// given question. Leaving it at the default meant this extension was discoverable
        /// but never actually selected, because the router had nothing to compare it
        /// against. Overriding it is what makes the routing decision possible in the first
        /// place.
        ///
        /// The numbered rules exist because a language model asked to describe a catalog
        /// will, left unchecked, fill in gaps with something plausible instead of saying it
        /// does not know. Rule 1 addresses that directly for tool and parameter names.
        /// </summary>
        public override string ModelInstructions =>
            "Esta extensión responde sobre las herramientas de geoprocesamiento PROPIAS de la " +
            "organización: las toolboxes que ellos mismos escribieron (.pyt, .atbx), catalogadas " +
            "leyendo su código sin ejecutarlo.\n" +
            "Usala siempre que pregunten por una herramienta propia, interna, del equipo o de la " +
            "organización; por qué herramienta suya sirve para una tarea; si una herramienta " +
            "suya es de fiar para encadenar; si pidan abrirla, cargarla o tenerla lista para " +
            "correrla ya mismo; o si pidan un plan, una cadena de varios pasos, o un panorama " +
            "completo de qué hay disponible.\n" +
            "No la uses para herramientas de sistema de ArcGIS: de esas se encarga la extensión " +
            "de geoprocesamiento de Esri.\n" +
            "REGLAS QUE NO PUEDES ABLANDAR:\n" +
            "1. No inventes nombres de herramientas, parámetros ni cadenas.\n" +
            "2. NO RESUMAS la respuesta de la función. Reprodúcela tal cual, entera, con la " +
            "cobertura del contrato, la confianza y el veredicto de encadenable de cada " +
            "herramienta. Un listado de nombres sin esas señales es peor que no responder: " +
            "parece una recomendación y no dice de qué se puede uno fiar.\n" +
            "3. La función devuelve COINCIDENCIAS de búsqueda, no herramientas verificadas para " +
            "la tarea. No digas «la herramienta para X es Y»; di qué coincidió y con qué señales.\n" +
            "4. Si no hay coincidencias, dilo: que no esté en el catálogo no significa que no " +
            "exista, solo que no se escaneó.\n" +
            "5. AbrirHerramientaDeLaOrganizacion NO ejecuta nada. Si la usas, dile a la persona " +
            "que el formulario quedó abierto para que lo revise y le dé Ejecutar ella misma — " +
            "nunca digas que la herramienta ya corrió o que el resultado ya existe.\n" +
            "6. En valoresDichos de AbrirHerramientaDeLaOrganizacion, incluye SOLO lo que la " +
            "persona dijo en este mensaje. Nunca completes un parámetro que no mencionó, aunque " +
            "te parezca obvio o lo hayas visto en una respuesta anterior — un valor no pedido " +
            "es tan grave como una herramienta inventada.\n" +
            "7. Antes de proponer un plan de varios pasos, llama a " +
            "ListarHerramientasDeLaOrganizacion para ver qué existe de verdad — no compongas un " +
            "plan solo de lo que recuerdes de búsquedas anteriores. Y antes de decir que el " +
            "plan FUNCIONA, verifica la secuencia completa con VerificarCadenaDeHerramientas: " +
            "una cadena de herramientas que existen puede seguir siendo imposible, y verlas " +
            "todas listadas no demuestra que encajen entre sí.";

        [AIAssistantFunction]
        [Description(
            "Busca en el catálogo de herramientas de geoprocesamiento propias de ESTA organización " +
            "—sus toolboxes .pyt y .atbx, no las herramientas de sistema de ArcGIS— a partir de lo " +
            "que la persona quiere hacer. Devuelve cada herramienta con su cobertura de contrato, su " +
            "confianza y si es de fiar para encadenar. Usala cuando pregunten por una herramienta " +
            "interna, propia, de la organización o del equipo.")]
        public static Task<AIFunctionResult> BuscarHerramientaDeLaOrganizacion(
            [Description("Lo que la persona quiere hacer, en sus palabras: recortar, interpolar, reproyectar, cuenca…")]
            string intencion)
        {
            // PreserveResponse determines whether the person reads the catalog's own
            // judgment or a summary of it. Without it, a response carrying real signal
            // (coverage, confidence, a chainability verdict) can come back from the model
            // as just a bare list of names, because the model treats free text as safe to
            // condense. Pro's own geoprocessing extension already sets this flag on its
            // responses, which is how the behavior was found in the first place.
            return Task.FromResult(new AIFunctionResult(Responder(intencion)) { PreserveResponse = true });
        }

        /// <summary>
        /// Verifies a proposed sequence of tools instead of proposing one.
        ///
        /// The split is deliberate: composing a sequence from a description of a task is
        /// something a language model is reasonably good at, and asserting that the
        /// sequence actually holds together is something it has no reliable way to check on
        /// its own. This function gives it something to check against.
        ///
        /// The verdict carries three values rather than two, and the middle one is the one
        /// that matters: "sin_verificar" is neither an approval nor a rejection. The result
        /// is preserved rather than summarized for the same reason as the search function —
        /// flattening a three-way verdict into a yes/no answer discards exactly the
        /// information that made verifying the chain worthwhile.
        /// </summary>
        [AIAssistantFunction]
        [Description(
            "Verifica si una secuencia de herramientas de geoprocesamiento PROPIAS de esta " +
            "organización se sostiene: comprueba los tipos entre pasos, la confianza del " +
            "contrato de cada una y las licencias que exigen. Devuelve un veredicto de tres " +
            "valores —verificada, rechazada o sin_verificar— y qué quedó sin comprobar. " +
            "Usala SIEMPRE antes de proponerle a alguien una secuencia de herramientas suyas.")]
        public static Task<AIFunctionResult> VerificarCadenaDeHerramientas(
            [Description("Las clases de las herramientas, en el orden en que se ejecutarían, separadas por comas o por flechas.")]
            string pasos)
        {
            return Task.FromResult(
                new AIFunctionResult(Encadenar(pasos)) { PreserveResponse = true });
        }

        /// <summary>
        /// Diagnoses a toolbox's contract without writing anything to disk.
        ///
        /// Nothing invoked from a conversation modifies a file. The assistant reports what
        /// is wrong, what can be derived from the code with confidence, and what would have
        /// to be guessed and is therefore left alone; producing the corrected copy is a
        /// separate, explicit action the person takes in a form, with the output location
        /// visible before anything is written. A model that gets a search wrong costs a bad
        /// answer; one that gets a write wrong costs a file.
        /// </summary>
        [AIAssistantFunction]
        [Description(
            "Revisa el contrato de una toolbox .pyt de esta organización y dice qué defectos " +
            "tiene, cuáles se pueden arreglar derivándolos del propio código y con qué certeza, " +
            "y cuáles habría que adivinar y por eso NO se tocan. Solo diagnostica: no escribe " +
            "ni modifica ningún archivo. Usala cuando pregunten qué le pasa a una herramienta " +
            "suya, o cuando otra función haya señalado que su contrato tiene defectos.")]
        public static Task<AIFunctionResult> DiagnosticarHerramientaDeLaOrganizacion(
            [Description("Ruta completa al archivo .pyt que se quiere revisar.")]
            string ruta)
        {
            return Task.FromResult(
                new AIFunctionResult(Diagnosticar(ruta)) { PreserveResponse = true });
        }

        /// <summary>
        /// Returns the full inventory, not just what matches one stated intent.
        ///
        /// With only the search function above, the model sees a single task at a time.
        /// Asked for a multi-step plan spanning raw data to a final deliverable, it either
        /// needs the full picture or has to fill in steps it never actually looked up. This
        /// function provides that picture; the underlying catalog listing already existed
        /// as part of the core library, and this is simply the missing connection to Pro.
        ///
        /// It does not replace VerificarCadenaDeHerramientas: seeing every tool listed does
        /// not establish that any two of them fit together, and the system prompt above
        /// states that as a rule rather than leaving it as an assumption.
        /// </summary>
        [AIAssistantFunction]
        [Description(
            "Lista TODO el inventario de herramientas propias de esta organización, con su " +
            "cobertura, confianza y si son de fiar para encadenar. Usala cuando pidan un plan de " +
            "trabajo, una cadena completa de varios pasos, o un panorama de qué hay disponible " +
            "— no para una tarea puntual (para eso está BuscarHerramientaDeLaOrganizacion).")]
        public static Task<AIFunctionResult> ListarHerramientasDeLaOrganizacion()
        {
            return Task.FromResult(
                new AIFunctionResult(Ejecutar(true, "listar", RutaDelCatalogo())) { PreserveResponse = true });
        }

        /// <summary>
        /// Opens the tool's real form in the Geoprocessing pane, pre-filled where possible,
        /// and leaves the Run button to the person.
        ///
        /// This does not conflict with the rule that a conversation never modifies disk:
        /// OpenToolDialog does not execute anything, it only opens the pane with a form
        /// already populated. The person still has to press Run inside Pro — the same
        /// boundary used by the diagnostic function above, applied to geoprocessing instead
        /// of to a corrected file.
        /// </summary>
        [AIAssistantFunction]
        [Description(
            "Abre en el panel de Geoprocesamiento de Pro la herramienta propia de ESTA " +
            "organización que mejor coincide con lo que la persona quiere hacer, con su " +
            "formulario precargado con lo que la persona haya dicho. NO la ejecuta: la persona " +
            "sigue teniendo que darle a Ejecutar dentro de Pro. Usala cuando pidan abrir, cargar " +
            "o tener lista una herramienta propia para correrla — no cuando solo pregunten cuál " +
            "usar (para eso está BuscarHerramientaDeLaOrganizacion).")]
        public static Task<AIFunctionResult> AbrirHerramientaDeLaOrganizacion(
            [Description("Lo que la persona quiere hacer, en sus palabras: recortar, interpolar, reproyectar, cuenca…")]
            string intencion,
            [Description(
                "Los valores que la persona mencionó EXPLÍCITAMENTE para los parámetros de la " +
                "herramienta, como pares etiqueta=valor separados por comas — usa tus propias " +
                "palabras para la etiqueta (la que la persona usó o algo parecido), no inventes " +
                "el nombre interno del parámetro porque no lo conoces todavía. Ejemplo: " +
                "'capa=reportes_sentido_2026ago, campo=DIST_HIPO, magnitud=7.4'. Deja FUERA " +
                "cualquier parámetro que la persona no haya mencionado — nunca completes ni " +
                "inventes un valor que no dijo. Si no mencionó ningún valor, deja esto vacío.")]
            string valoresDichos = null)
        {
            string salida = Ejecutar(true, "abrir", RutaDelCatalogo(), intencion, "--dichos", valoresDichos ?? "");
            return Task.FromResult(AbrirDesdeCatalogo(salida));
        }

        private static string Responder(string intencion) =>
            string.IsNullOrWhiteSpace(intencion)
                ? "No me dijeron qué buscar."
                : Ejecutar(true, "buscar", RutaDelCatalogo(), intencion, "--limite", "5");

        /// <summary>
        /// Steps arrive as a single string because the assistant passes strings, not lists.
        ///
        /// Comma, the unicode arrow and the ASCII arrow are all accepted, because a model
        /// composing a sequence on its own will use whichever of the three feels natural,
        /// and rejecting two of them would turn a correct answer into a failure on this
        /// side. An empty step is dropped rather than passed through, which would otherwise
        /// surface as a confusing "tool '' does not exist".
        /// </summary>
        private static string Encadenar(string pasos)
        {
            if (string.IsNullOrWhiteSpace(pasos))
                return "No me dijeron qué cadena verificar.";

            string[] partes = pasos.Split(
                new[] { ",", "→", "->", ";" }, StringSplitOptions.RemoveEmptyEntries);
            var argumentos = new System.Collections.Generic.List<string> { "encadenar", RutaDelCatalogo() };
            foreach (string parte in partes)
            {
                string limpio = parte.Trim();
                if (limpio.Length > 0)
                    argumentos.Add(limpio);
            }

            // Two steps is the minimum for a chain, and checking it here avoids a wasted
            // subprocess call. toolscout.pro enforces the same rule independently; this is
            // a short-circuit, not the authority on the invariant.
            if (argumentos.Count < 4)
                return "Una cadena necesita al menos dos herramientas. Recibí: " + pasos;

            return Ejecutar(true, argumentos.ToArray());
        }

        /// <summary>
        /// Turns the JSON line returned by "toolscout.pro abrir" into an actual call into
        /// Pro, or into a plain text message when there is nothing safe to open.
        ///
        /// The underlying function already refuses to return a tool whose contract is
        /// broken or whose extraction confidence is low, which is the same guard that keeps
        /// this path from reopening a tool definition that is known to crash Pro on load.
        /// </summary>
        private static AIFunctionResult AbrirDesdeCatalogo(string salidaJson)
        {
            JsonDocument documento = null;
            try
            {
                documento = JsonDocument.Parse(salidaJson);
            }
            catch (JsonException)
            {
                // Ejecutar returns plain text, not JSON, when it fails before reaching
                // Python (no catalog, no Python interpreter found, a timeout). That text is
                // reproduced as-is.
                return new AIFunctionResult(salidaJson) { PreserveResponse = true };
            }

            using (documento)
            {
                JsonElement raiz = documento.RootElement;
                if (raiz.TryGetProperty("error", out JsonElement error))
                    return new AIFunctionResult(error.GetString()) { PreserveResponse = true };

                // Everything below assumes the exact contract of abrir_herramienta in
                // toolscout.pro: only two possible shapes, an error object or one carrying
                // class, file and parameters. A contract between two languages talking
                // through a subprocess is exactly the kind of thing that drifts silently
                // over time, so this is guarded rather than trusted blindly.
                string clase = null;
                try
                {
                    string archivo = raiz.GetProperty("archivo").GetString();
                    clase = raiz.GetProperty("clase").GetString();

                    // Passing an empty string instead of null for every parameter, including
                    // the ones with no value to offer, breaks the dropdown of a Field
                    // parameter that depends on another one (a field list depending on a
                    // layer parameter). Once populated with an empty string, Pro does not
                    // repopulate that dropdown even after the person picks a real layer by
                    // hand — the field has to be cleared completely first. So every unknown
                    // slot in the values array below travels as a genuine null, translated
                    // element by element rather than filled with a single placeholder value.
                    //
                    // abrir_herramienta builds this array from two sources: values the person
                    // stated explicitly in the current message, and a small named table of
                    // known defaults used as a fallback. The result can be partial — some
                    // parameters filled, others left null — and that is exactly what gets
                    // passed to Pro: pre-fill what is known, leave the rest for the person to
                    // complete by hand.
                    string[] valores = null;
                    bool precargado = false;
                    if (raiz.TryGetProperty("valores", out JsonElement valoresJson)
                        && valoresJson.ValueKind == JsonValueKind.Array)
                    {
                        valores = new string[valoresJson.GetArrayLength()];
                        int i = 0;
                        foreach (JsonElement v in valoresJson.EnumerateArray())
                            valores[i++] = v.ValueKind == JsonValueKind.Null ? null : v.GetString();
                        precargado = true;
                    }

                    string toolPath = archivo + "\\" + clase;

                    // OpenToolDialog is documented as always being called from the UI
                    // thread. Assistant functions are not guaranteed to run there — which is
                    // also why Ejecutar below talks to Python through a subprocess instead of
                    // calling into Pro directly — so the call is dispatched through the
                    // application's own dispatcher. QueuedTask is not the right tool here;
                    // that queues onto Pro's background CIM thread, a different one.
                    Application.Current.Dispatcher.Invoke(() =>
                        Geoprocessing.OpenToolDialog(toolPath, valores, null, false, null));

                    // An "aviso" field is informational, not blocking, unlike "error" handled
                    // above. Today it only appears for an .atbx FunctionTool: its code lives
                    // outside the declared contract, so nothing here can confirm it exists.
                    // It is reproduced as-is rather than summarized, for the same reason
                    // stated in the system prompt.
                    string aviso = raiz.TryGetProperty("aviso", out JsonElement avisoJson)
                        ? avisoJson.GetString()
                        : null;
                    string mensaje = precargado
                        ? $"Abrí {clase} en el panel de Geoprocesamiento, con su formulario " +
                          "precargado. Revísalo — no la ejecuté."
                        : $"Abrí {clase} en el panel de Geoprocesamiento, con su formulario " +
                          "listo para revisar. No la ejecuté — dale a Ejecutar ahí cuando la " +
                          "hayas revisado.";
                    if (!string.IsNullOrEmpty(aviso))
                        mensaje += "\n\nAviso: " + aviso;

                    return new AIFunctionResult(mensaje) { PreserveResponse = true };
                }
                catch (Exception e)
                {
                    return new AIFunctionResult(
                        clase == null
                            ? $"El catálogo respondió algo que no supe interpretar: {e.Message}"
                            : $"Encontré {clase}, pero no pude abrir su formulario en Pro: {e.Message}")
                    { PreserveResponse = true };
                }
            }
        }

        /// <summary>Diagnoses a .pyt file directly; no catalog is required. See Ejecutar.</summary>
        private static string Diagnosticar(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
                return "No me dijeron qué archivo revisar.";
            if (!File.Exists(ruta))
                return $"No existe el archivo {ruta}.";
            // Without --a, toolscout.pro corregir writes nothing unless given an output
            // folder, and having the short path be the one that never touches disk matters
            // when the caller is a model, which tends to omit optional arguments.
            return Ejecutar(false, "corregir", ruta);
        }

        /// <summary>
        /// Shared adapter for all of the functions above: locates Pro's own Python
        /// interpreter, runs toolscout.pro with the given arguments, and turns each failure
        /// mode into something the person can act on.
        ///
        /// exigeCatalogo is not incidental: corregir does not need a catalog. It works
        /// directly on a .pyt file on disk and derives its contract from the code itself, so
        /// refusing to run it for lack of an inventory would be an invented dependency —
        /// and would teach people that the "no catalog yet" message can appear for reasons
        /// unrelated to the catalog.
        /// </summary>
        private static string Ejecutar(bool exigeCatalogo, params string[] argumentos)
        {
            string python = RutaDePython();
            if (!File.Exists(python))
                return $"No encuentro el Python de ArcGIS Pro. Busqué en: {python}";

            string catalogo = RutaDelCatalogo();
            if (exigeCatalogo && !File.Exists(catalogo))
                return "Todavía no hay catálogo de las herramientas de la organización. Se crea con la " +
                       $"herramienta «1 · Catalogar mis herramientas» de ArcGISMentor.pyt. Busqué en: {catalogo}";

            try
            {
                var inicio = new ProcessStartInfo(python)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                // Declaring UTF-8 above only covers how this process reads the child's
                // output; it does not change what encoding Python writes with. When its
                // output goes to a pipe rather than a console, Python falls back to the
                // system code page, which produced garbled accented characters until this
                // environment variable was set explicitly. Both halves of the contract are
                // needed.
                inicio.Environment["PYTHONIOENCODING"] = "utf-8";
                inicio.ArgumentList.Add("-m");
                inicio.ArgumentList.Add("toolscout.pro");
                foreach (string argumento in argumentos)
                    inicio.ArgumentList.Add(argumento);

                // Only set when needed: if toolscout is already installed in Pro's own
                // Python environment, forcing a PYTHONPATH here would shadow that
                // installation with an older copy sitting next to the catalog.
                string src = RutaDelPaquete();
                if (src != null)
                    inicio.Environment["PYTHONPATH"] = src;

                using var proceso = Process.Start(inicio);
                string salida = proceso.StandardOutput.ReadToEnd();
                string error = proceso.StandardError.ReadToEnd();
                if (!proceso.WaitForExit(EsperaMs))
                {
                    try { proceso.Kill(true); } catch (Exception) { }
                    return $"La consulta al catálogo pasó de {EsperaMs / 1000} s y se cortó. El catálogo quedó intacto.";
                }

                if (proceso.ExitCode != 0)
                    return string.IsNullOrWhiteSpace(error)
                        ? $"La consulta al catálogo falló (código {proceso.ExitCode})."
                        : error.Trim();

                return string.IsNullOrWhiteSpace(salida)
                    ? "El catálogo no devolvió nada."
                    : salida.Trim();
            }
            catch (Exception e)
            {
                return $"No se pudo consultar el catálogo: {e.Message}";
            }
        }

        /// <summary>
        /// Locates Pro's own Python interpreter relative to where this add-in is running.
        ///
        /// AppContext.BaseDirectory is Pro's bin folder when this code runs inside Pro, so
        /// there is no need to guess a Program Files path or read the registry — this keeps
        /// working even when Pro is installed on a different drive.
        /// </summary>
        private static string RutaDePython()
        {
            string bin = AppContext.BaseDirectory;
            return Path.Combine(bin, "Python", "envs", "arcgispro-py3", "python.exe");
        }

        /// <summary>
        /// Locates the toolscout package on disk, or null when nothing needs to be said.
        ///
        /// Order matters: an explicitly declared path is honored first, then the copy that
        /// lives next to the catalog. If neither exists, null is returned deliberately
        /// instead of an invented path, so that the Python process falls back to whatever
        /// toolscout installation it already has.
        /// </summary>
        private static string RutaDelPaquete()
        {
            string declarado = Environment.GetEnvironmentVariable("ARCGISMENTOR_SRC");
            if (!string.IsNullOrWhiteSpace(declarado) && Directory.Exists(declarado))
                return declarado;

            string juntoAlCatalogo = Path.Combine(CarpetaDeDatos(), "src");
            return Directory.Exists(juntoAlCatalogo) ? juntoAlCatalogo : null;
        }

        /// <summary>The add-in's own data folder, shared with the load trace.</summary>
        private static string CarpetaDeDatos() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcGISMentor");

        private static string RutaDelCatalogo()
        {
            // ARCGISMENTOR_CATALOGO is the current variable name; GISPILOT_CATALOGO is kept
            // as a fallback for installations set up under the product's earlier name.
            // Removing it silently would break an existing setup without any warning, which
            // is worse than keeping an old name around.
            string declarado = Environment.GetEnvironmentVariable("ARCGISMENTOR_CATALOGO");
            if (string.IsNullOrWhiteSpace(declarado))
                declarado = Environment.GetEnvironmentVariable("GISPILOT_CATALOGO");
            if (!string.IsNullOrWhiteSpace(declarado))
                return declarado;
            return Path.Combine(CarpetaDeDatos(), "inventario.db");
        }
    }
}
