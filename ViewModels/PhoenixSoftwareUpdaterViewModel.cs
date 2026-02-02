using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixSwitcher.ViewModels
{
    public class PhoenixSoftwareUpdaterViewModel
    {
        public string WindowName { get; set; } = "";
        public string SettingsText { get; set; } = "";
        public string ProgramSettingsText { get; set; } = "";
        public string LanguageSettingsText { get; set; } = "";
        public string UpdateText { get; set; } = "";
        public string UpdateBundleFilesText { get; set; } = "";
        public string UpdateMachineListText { get; set; } = "";
        public string ConntectText { get; set; } = "";
        public string EspControllerText { get; set; } = "";
        public string HelpText { get; set; } = "";
        public string AboutText { get; set; } = "";
        public string FileEditorName { get; set; } =  AppContext.BaseDirectory + "\\Settings\\ProjectSettings.xml";
        public MachineListViewModel MachineListViewModel { get; set; } = new MachineListViewModel();
    }
}
