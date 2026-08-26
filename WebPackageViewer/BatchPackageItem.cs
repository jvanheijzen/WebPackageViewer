using System.ComponentModel;

namespace WebPackageViewer
{
    public sealed class BatchPackageItem : INotifyPropertyChanged
    {
        private string _moduleId;
        private string _moduleName;
        private string _outputFileName;
        private string _status;

        public string SourceFolder { get; set; }
        public string WindowTitle { get; set; }
        public string WindowSize { get; set; }

        public string ModuleId
        {
            get => _moduleId;
            set
            {
                if (_moduleId == value)
                    return;

                _moduleId = value;
                OnPropertyChanged(nameof(ModuleId));
            }
        }

        public string ModuleName
        {
            get => _moduleName;
            set
            {
                if (_moduleName == value)
                    return;

                _moduleName = value;
                OnPropertyChanged(nameof(ModuleName));
            }
        }

        public string OutputFileName
        {
            get => _outputFileName;
            set
            {
                if (_outputFileName == value)
                    return;

                _outputFileName = value;
                OnPropertyChanged(nameof(OutputFileName));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(name));
        }
    }
}
