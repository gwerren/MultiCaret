namespace MultiCaret
{
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration(
        "Multi-Caret",
        "Extension to allow multi-point editing in Visual Studio with smart-select functionality.",
        "1.0",
        IconResourceID = 400)]
    [Guid("859df000-b7d2-4f45-a055-d99dbe79dd52")]
    [ProvideOptionPage(typeof(OptionsDialogPage), "Multi-Caret", "General", 0, 0, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class VSPackage : Package { }
}
