using ArcGIS.Desktop.Framework.Contracts;

namespace ArcGISMentorAddIn
{
    /// <summary>
    /// The add-in's module. Left empty on purpose.
    ///
    /// Everything else in this add-in — the assistant extension and its functions — depends
    /// on ArcGIS Pro being able to load a build produced without Visual Studio's Pro SDK
    /// project templates. Keeping this module free of any real logic makes that question
    /// answerable on its own, independent of whether the rest of the add-in works.
    ///
    /// autoLoad="true" in Config.daml is what makes this a meaningful check: without it the
    /// module only initializes once someone interacts with one of its UI elements, and
    /// "nothing happened" would not distinguish between "did not load" and "was never used".
    /// </summary>
    internal class Module1 : Module
    {
        protected override bool Initialize()
        {
            Rastro.Anotar("Initialize");
            return base.Initialize();
        }

        protected override bool CanUnload()
        {
            Rastro.Anotar("CanUnload");
            return true;
        }
    }
}
