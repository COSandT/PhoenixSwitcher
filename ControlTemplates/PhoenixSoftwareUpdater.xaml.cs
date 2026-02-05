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
        private PhoenixSwitcherLogic _phoenixSwitcher;
        private Logger _logger;

        private string _driveName;
        private string _boxName;
        private string _espID;

        public PhoenixSoftwareUpdater(MainWindow parent, string espID, string driveName, string boxName, Logger logger)
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            _driveName = driveName;
            _boxName = boxName;
            _logger = logger;
            _espID = espID;

            _phoenixSwitcher = new PhoenixSwitcherLogic(_logger);
            InitPhoenixSwitcher();

            StatusBarControl.Init(_phoenixSwitcher, _logger);
            MachineInfoWindowControl.Init(_phoenixSwitcher, _logger);
            MachineListControl.Init(_phoenixSwitcher, parent.PCMMachineList, _logger);
        }
        public void UpdateBundleFiles()
        {
            _phoenixSwitcher.UpdateBundleFilesOnDrive();
        }

        private void InitPhoenixSwitcher()
        {
            _phoenixSwitcher.Init(_espID, _driveName, _boxName);

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                , 24, new Action(_phoenixSwitcher.UpdateBundleFilesOnDrive));

            if (Helpers.GetHoursSinceLastUpdate() > 24) _phoenixSwitcher.UpdateBundleFilesOnDrive();
        }
    }
}
