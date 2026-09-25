using System;
using System.IO;

namespace ArcGISMentorAddIn
{
    /// <summary>
    /// Leaves a record on disk confirming that the add-in loaded.
    ///
    /// Whether ArcGIS Pro actually loads a given add-in build is not something that can be
    /// checked from a terminal — it requires opening the desktop application. A file with a
    /// timestamp and version turns "it looked like it worked" into something that can be
    /// verified afterward without watching the screen at the moment it happened.
    ///
    /// Writes to LocalApplicationData because that is where an add-in can write without
    /// elevated permissions on any managed machine.
    ///
    /// Never throws: a trace that could bring down the add-in it is supposed to be
    /// reporting on would defeat its own purpose.
    /// </summary>
    internal static class Rastro
    {
        internal static string Carpeta =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcGISMentor");

        internal static string Archivo => Path.Combine(Carpeta, "addin-cargado.txt");

        internal static void Anotar(string evento)
        {
            try
            {
                Directory.CreateDirectory(Carpeta);
                string version = typeof(Rastro).Assembly.GetName().Version?.ToString() ?? "?";
                string pro = Environment.GetEnvironmentVariable("ProgramW6432") ?? "";
                File.AppendAllText(
                    Archivo,
                    string.Format(
                        "{0:yyyy-MM-dd HH:mm:ss}\t{1}\tArcGISMentorAddIn {2}\t{3}\tproceso={4}{5}",
                        DateTime.Now, evento, version, pro, Environment.ProcessId, Environment.NewLine));
            }
            catch (Exception)
            {
                // Intentionally swallowed. See the class summary above.
            }
        }
    }
}
