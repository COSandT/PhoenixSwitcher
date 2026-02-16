using System.Windows;
using System.Windows.Controls;

using CosntCommonLibrary.Tools;
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
        private Logger _logger;

        public PhoenixSwitcherLogic PhoenixSwitcher { get; private set; }

        public PhoenixSoftwareUpdater(MainWindow parent, string espID, string driveName, string boxName, Logger logger)
        {
            InitializeComponent();
            this.DataContext = _viewModel;
            _logger = logger;

            PhoenixSwitcher = new PhoenixSwitcherLogic(_logger, espID, driveName, boxName);

            StatusBarControl.Init(PhoenixSwitcher, _logger);
            MachineInfoWindowControl.Init(PhoenixSwitcher, _logger);
            MachineListControl.Init(PhoenixSwitcher, parent.PCMMachineList, _logger);
        }
        public void UpdateBundleFiles()
        {
            PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
        public async void InitPhoenixSwitcher()
        {
            await PhoenixSwitcher.Init();

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                , 24, new Action(PhoenixSwitcher.UpdateBundleFilesOnDrive));

            if (Helpers.GetHoursSinceLastUpdate() > 24) PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
    }
}
