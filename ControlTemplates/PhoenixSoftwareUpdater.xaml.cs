using System.Windows.Controls;

using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

using PhoenixSwitcher.ViewModels;

using TaskScheduler = CosntCommonLibrary.Helpers.TaskScheduler;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for PhoenixSoftwareUpdater.xaml
    /// </summary>
    public partial class PhoenixSoftwareUpdater : UserControl
    {
        private PhoenixSoftwareUpdaterViewModel _viewModel = new PhoenixSoftwareUpdaterViewModel();

        public PhoenixSwitcherLogic PhoenixSwitcher { get; private set; }

        public PhoenixSoftwareUpdater(MainWindow parent, EspControllerInfo controllerInfo)
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            PhoenixSwitcher = new PhoenixSwitcherLogic(controllerInfo);

            StatusBarControl.Init(PhoenixSwitcher);
            MachineInfoWindowControl.Init(PhoenixSwitcher);
            MachineListControl.Init(PhoenixSwitcher, parent.PCMMachineList);
        }
        public void UpdateBundleFiles()
        {
            PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
        public async Task InitPhoenixSwitcher()
        {
            await PhoenixSwitcher.Init();
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                , 24, new Action(PhoenixSwitcher.UpdateBundleFilesOnDrive));

            if (!PhoenixSwitcher.HasEspConnection()) return;
            if (Helpers.GetHoursSinceLastUpdate() > 24) PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
    }
}
