using CosntCommonViewLibrary;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class SettingsWindowViewModel
    {
        public string WindowName { get; set; } = "";
        public string SaveButtonText { get; set; } = "";
        public string XmlToEditPath { get; set; } = "";
        public Command OnSaveCommand { get; set; } = new Command();
    }
}
