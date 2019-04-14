namespace MultiCaret
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Windows;
    using Microsoft.VisualStudio.ComponentModelHost;
    using Microsoft.VisualStudio.Shell;

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    [Guid("d1bdec27-7925-45f4-b8e6-3ce2d52b6391")]
    public class OptionsDialogPage : UIElementDialogPage
    {
        private OptionsDialgView optionsDialogControl;

        protected override UIElement Child => this.optionsDialogControl
            ?? (this.optionsDialogControl = new OptionsDialgView());

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            var componentModel = (IComponentModel)(this.Site.GetService(typeof(SComponentModel)));
            var settings = componentModel.DefaultExportProvider.GetExportedValue<ICoreSettings>();
            ((OptionsDialgView)this.Child).DataContext = new OptionsDialgViewModel(settings);
        }

        protected override void OnApply(PageApplyEventArgs args)
        {
            if (args.ApplyBehavior == ApplyKind.Apply)
                ((OptionsDialgViewModel)((OptionsDialgView)this.Child).DataContext).Save();

            base.OnApply(args);
        }
    }
}
