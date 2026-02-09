using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MainWindowViewModel
    {
        public string WindowName { get; set; } = "";
        public string SettingsText { get; set; } = "";
        public string ProgramSettingsText { get; set; } = "";
        public string LanguageSettingsText { get; set; } = "";
        public string UpdateText { get; set; } = "";
        public string UpdateBundleFilesText { get; set; } = "";
        public string UpdateMachineListText { get; set; } = "";
        public string ThemeText { get; set; } = "";
        public string DarkModeText { get; set; } = "";
        public string LightModeText { get; set; } = "";
        public string HelpText { get; set; } = "";
        public string AboutText { get; set; } = "";
        public string FileEditorName { get; set; } = @"D:\Miel\Git Repos\PhoenixSwitcher\bin\Debug\net8.0-windows\Settings\ProjectSettings.xml";
        public MachineListViewModel MachineListViewModel { get; set; } = new MachineListViewModel();
    }
}
