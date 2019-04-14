namespace MultiCaret
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public class OptionsDialgViewModel : INotifyPropertyChanged
    {
        private readonly ICoreSettings settings;
        private MouseSelectionAddKey selectedMouseAddKey;
        private TempFileLanguage tempFileLanguage;

        public OptionsDialgViewModel(ICoreSettings settings)
        {
            this.AllTempFileLanguages = Enum.GetValues(typeof(TempFileLanguage))
                .Cast<TempFileLanguage>()
                .ToObservableCollection();

            this.AllMouseAddKeys = MouseSelectionAddKey.AllKeys.ToObservableCollection();

            this.settings = settings;
            this.SelectedMouseAddKey = settings.MouseSelectionAddKey;
            this.TempFileLanguage = settings.TempFileLanguage;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MouseSelectionAddKey SelectedMouseAddKey
        {
            get { return this.selectedMouseAddKey; }
            set
            {
                if (this.selectedMouseAddKey == value)
                    return;

                this.selectedMouseAddKey = value;
                this.OnPropertyChanged();
            }
        }

        public TempFileLanguage TempFileLanguage
        {
            get { return this.tempFileLanguage; }
            set
            {
                if (this.tempFileLanguage == value)
                    return;

                this.tempFileLanguage = value;
                this.OnPropertyChanged();
            }
        }

        public ObservableCollection<MouseSelectionAddKey> AllMouseAddKeys { get; }

        public ObservableCollection<TempFileLanguage> AllTempFileLanguages { get; }

        public void Save()
        {
            this.settings.MouseSelectionAddKey = this.SelectedMouseAddKey;
            this.settings.TempFileLanguage = this.TempFileLanguage;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
