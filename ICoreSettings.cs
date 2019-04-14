namespace MultiCaret
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;

    public interface ICoreSettings
    {
        event EventHandler SettingsChanged;

        Brush SelectionBrush { get; }

        Brush CaretBrush { get; }

        MouseSelectionAddKey MouseSelectionAddKey { get; set; }

        TempFileLanguage TempFileLanguage { get; set; }

        void RefreshVsSettings();

        bool IsMouseAddSelectionKeyDown();

        void AddView(IWpfTextView view, IEditorFormatMapService service);
    }
}
