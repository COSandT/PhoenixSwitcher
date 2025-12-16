using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MainWindowViewModel
    {
        public string WindowName { get; set; } = "";
        public string SettingsText { get; set; } = "";
        public string LanguageSettingsText { get; set; } = "";
        public string AboutText { get; set; } = "";
        public string FileEditorName { get; set; } = @"D:\Miel\Git Repos\PhoenixSwitcher\bin\Debug\net8.0-windows\Settings\ProjectSettings.xml";
        public MachineListViewModel MachineListViewModel { get; set; } = new MachineListViewModel();
    }
}
