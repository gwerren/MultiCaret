namespace MultiCaret
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Editor;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Operations;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
    /// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class MultiCaretTextAdornmentCreation : IWpfTextViewCreationListener
    {
        // Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169

        /// <summary>
        /// Defines the adornment layer for the adornment. This layer is ordered
        /// after the selection layer in the Z-order
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(Constants.AdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
        private AdornmentLayerDefinition editorAdornmentLayer;

#pragma warning restore 649, 169

        [Import(typeof(IVsEditorAdaptersFactoryService))]
        internal IVsEditorAdaptersFactoryService EditorFactory;

        [Import(typeof(SVsServiceProvider))] internal SVsServiceProvider ServiceProvider;

        [Import] internal IEditorFormatMapService FormatMapService;

        [Import] internal ICoreSettings CoreSettings;

        [Import] internal ITextSearchService TextSearch;

        /// <summary>
        /// Called when a text view having matching roles is created over a text
        /// data model having a matching content type.
        /// Instantiates a TextAdornment1 manager when the textView is created.
        /// </summary>
        /// <param name="textView">The <see cref="IWpfTextView"/> upon which the adornment should be placed</param>
        public void TextViewCreated(IWpfTextView textView)
        {
            var commandFilter = new MultiCaretCommandFilter(
                this.ServiceProvider,
                textView,
                this.TextSearch,
                new MultiCaretTextAdornment(textView, this.CoreSettings),
                this.CoreSettings);

            var view = this.EditorFactory.GetViewAdapter(textView);
            if (view.AddCommandFilter(commandFilter, out var next) != VSConstants.S_OK)
            {
                // If the command failed to add then simply give up
                return;
            }

            if (next == null)
            {
                // We dont support the case where there is no next command target
                view.RemoveCommandFilter(commandFilter);
            }
            else
            {
                commandFilter.SetNextTarget(next);
                this.CoreSettings.AddView(textView, this.FormatMapService);
                textView.Properties.AddProperty(typeof(MultiCaretCommandFilter), commandFilter);
                textView.Properties.AddProperty(typeof(ICoreSettings), this.CoreSettings);
            }
        }
    }
}
