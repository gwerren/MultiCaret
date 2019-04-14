namespace MultiCaret
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Input;
    using System.Windows.Media;
    using EnvDTE;
    using EnvDTE80;
    using Microsoft.VisualStudio.Settings;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Settings;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;

    [Export(typeof(ICoreSettings))]
    public class CoreSettings : ICoreSettings
    {
        private readonly FormatMapChangedHelper changedHelper;
        private readonly CustomSettingsStore customSettings;
        private MouseSelectionAddKey mouseSelectionAddKey;
        private TempFileLanguage? tempFileLanguage;

        [ImportingConstructor]
        public CoreSettings(SVsServiceProvider vsServiceProvider)
        {
            this.changedHelper = new FormatMapChangedHelper(this);
            this.customSettings = new CustomSettingsStore(vsServiceProvider);
        }

        public event EventHandler SettingsChanged;

        public Brush SelectionBrush { get; private set; }

        public Brush CaretBrush { get; private set; }

        public MouseSelectionAddKey MouseSelectionAddKey
        {
            get => this.mouseSelectionAddKey ??
                (this.mouseSelectionAddKey = this.customSettings.GetMouseAddSelectionKey());
            set
            {
                if (this.mouseSelectionAddKey == value)
                    return;

                this.customSettings.SetMouseAddSelectionKey(value);
                this.mouseSelectionAddKey = value;
            }
        }

        public TempFileLanguage TempFileLanguage
        {
            get => this.tempFileLanguage ??
                (this.tempFileLanguage = this.customSettings.GetTempFileLanguage()).Value;
            set
            {
                if (this.tempFileLanguage == value)
                    return;

                this.customSettings.SetTempFileLanguage(value);
                this.tempFileLanguage = value;
            }
        }

        public void RefreshVsSettings()
        {
            var updatedSelection = this.UpdateSelectionBrush();
            var updatedCaret = this.UpdateCaretBrush();

            if (updatedSelection || updatedCaret)
                this.SettingsChanged?.Invoke(null, EventArgs.Empty);
        }

        public bool IsMouseAddSelectionKeyDown()
        {
            var keys = this.MouseSelectionAddKey.Keys;
            for (int i = 0; i < keys.Count; ++i)
            {
                if (Keyboard.IsKeyDown(keys[i]))
                    return true;
            }

            return false;
        }

        public void AddView(IWpfTextView view, IEditorFormatMapService service)
        {
            this.changedHelper.AddView(view, service);
        }

        private bool UpdateSelectionBrush()
        {
            var existingBrush = this.SelectionBrush as SolidColorBrush;

            // Find the current selection color
            var dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
            var fontsAndColors = (FontsAndColorsItems)dte
                .Properties["FontsAndColors", "TextEditor"]
                .Item("FontsAndColorsItems")
                .Object;

            var color = System.Drawing.ColorTranslator
                .FromOle((int)fontsAndColors.Item("Selected Text").Background);

            // Create the brush
            var selectionBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            selectionBrush.Freeze();

            // Update the exposed brush
            if (existingBrush == null || !existingBrush.Color.Equals(selectionBrush.Color))
            {
                this.SelectionBrush = selectionBrush;
                return true;
            }

            return false;
        }

        private bool UpdateCaretBrush()
        {
            var existingBrush = this.CaretBrush as SolidColorBrush;

            // Create the brush
            var caretBrush = new SolidColorBrush(Colors.Black);
            caretBrush.Freeze();

            // Update the exposed brush
            if (existingBrush == null || !existingBrush.Color.Equals(caretBrush.Color))
            {
                this.CaretBrush = caretBrush;
                return true;
            }

            return false;
        }

        private class CustomSettingsStore
        {
            private const string CollectionPath = "GW_MultiCaret";
            private const string MouseAddSelectionKeysProperty = "MouseAddSelectionKeys";
            private const string TempFileLanguageProperty = "TempFileLanguage";

            private readonly WritableSettingsStore store;

            public CustomSettingsStore(IServiceProvider vsServiceProvider)
            {
                var manager = new ShellSettingsManager(vsServiceProvider);
                this.store = manager.GetWritableSettingsStore(SettingsScope.UserSettings);
            }

            public MouseSelectionAddKey GetMouseAddSelectionKey()
            {
                var value = this.LoadInt(MouseAddSelectionKeysProperty);
                return value.HasValue
                    ? MouseSelectionAddKey.GetById(value.Value) ?? MouseSelectionAddKey.Default
                    : MouseSelectionAddKey.Default;
            }

            public TempFileLanguage GetTempFileLanguage()
            {
                var value = this.LoadInt(TempFileLanguageProperty);
                return value.HasValue ? (TempFileLanguage)value.Value : TempFileLanguage.cs;
            }

            public void SetMouseAddSelectionKey(MouseSelectionAddKey key)
            {
                this.Save(MouseAddSelectionKeysProperty, key.ID);
            }

            public void SetTempFileLanguage(TempFileLanguage language)
            {
                this.Save(TempFileLanguageProperty, (int)language);
            }

            private int? LoadInt(string propertyName)
            {
                try
                {
                    if (this.store.PropertyExists(CollectionPath, propertyName))
                        return this.store.GetInt32(CollectionPath, propertyName);
                }
                catch (Exception ex)
                {
                    Debug.Fail(ex.Message);
                }

                return null;
            }

            private void Save(string propertyName, int value)
            {
                try
                {
                    if (!this.store.CollectionExists(CollectionPath))
                        this.store.CreateCollection(CollectionPath);

                    this.store.SetInt32(CollectionPath, propertyName, value);
                }
                catch (Exception ex)
                {
                    Debug.Fail(ex.Message);
                }
            }
        }

        private class FormatMapChangedHelper
        {
            private readonly ICoreSettings settings;
            private readonly object Lock = new object();

            private readonly HashSet<IWpfTextView> allViews = new HashSet<IWpfTextView>();
            private IEditorFormatMapService formatMapService;

            private IWpfTextView textView;
            private IEditorFormatMap editorFormatMap;

            public FormatMapChangedHelper(ICoreSettings settings) { this.settings = settings; }

            public void AddView(IWpfTextView view, IEditorFormatMapService service)
            {
                lock (this.Lock)
                {
                    // Set this every time although we only really
                    // need to set it once - as long as it is set.
                    this.formatMapService = service;

                    view.Closed += this.HandleViewClosed;
                    this.allViews.Add(view);
                    if (this.textView == null)
                    {
                        this.SetAsListener(view);

                        // As this is the first view the settings may
                        // have changed since we last had a view open
                        this.settings.RefreshVsSettings();
                    }
                }
            }

            private void SetAsListener(IWpfTextView view)
            {
                if (this.textView != null)
                    this.editorFormatMap.FormatMappingChanged -= this.FormatMapChanged;

                this.textView = view;
                this.editorFormatMap = this.formatMapService.GetEditorFormatMap(this.textView);
                this.editorFormatMap.FormatMappingChanged += this.FormatMapChanged;
            }

            private void CLearListener()
            {
                if (this.textView == null)
                    return;

                this.editorFormatMap.FormatMappingChanged -= this.FormatMapChanged;
                this.textView = null;
                this.editorFormatMap = null;
            }

            private void FormatMapChanged(object sender, FormatItemsEventArgs e)
            {
                this.settings.RefreshVsSettings();
            }

            private void HandleViewClosed(object sender, EventArgs e)
            {
                lock (this.Lock)
                {
                    var view = sender as IWpfTextView;
                    if (view == null)
                        return;

                    this.allViews.Remove(view);
                    if (view == this.textView)
                    {
                        if (this.allViews.Count == 0)
                            this.CLearListener();
                        else
                            this.SetAsListener(this.allViews.First());
                    }
                }
            }
        }
    }
}
