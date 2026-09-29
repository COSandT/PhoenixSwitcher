using System.Windows.Controls;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using TaskScheduler = CosntCommonLibrary.Helpers.TaskScheduler;

using PhoenixSwitcher.Phoenix;
using PhoenixSwitcher.ViewModels;


namespace PhoenixSwitcher.ControlTemplates
{
    public partial class PhoenixSoftwareUpdater : UserControl
    {
        private readonly PhoenixSoftwareUpdaterViewModel _viewModel = new PhoenixSoftwareUpdaterViewModel();

        public PhoenixSwitcherLogic PhoenixSwitcher { get; private set; }

        public PhoenixSoftwareUpdater(EspControllerInfo controllerInfo, XmlProductionDataPCM? machineList)
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            PhoenixSwitcher = new PhoenixSwitcherLogic(controllerInfo);

            StatusBarControl.Init(PhoenixSwitcher);
            MachineInfoWindowControl.Init(PhoenixSwitcher);
            MachineListControl.Init(PhoenixSwitcher, machineList);
        }
        public void UpdateBundleFiles()
        {
            PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
        public async Task InitPhoenixSwitcherAsync()
        {
            await PhoenixSwitcher.InitAsync();
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours, settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds, 24, new Action(PhoenixSwitcher.UpdateBundleFilesOnDrive));

            if (!PhoenixSwitcher.HasEspConnection()) return;
            if (Helpers.GetHoursSinceLastUpdate() > 24) PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
    }
}
