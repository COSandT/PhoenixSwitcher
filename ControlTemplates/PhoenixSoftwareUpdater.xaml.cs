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

        private string _driveName;
        private string _boxName;
        private string _espID;
        public PhoenixSwitcherLogic PhoenixSwitcher { get; private set; }

        public PhoenixSoftwareUpdater(MainWindow parent, string espID, string driveName, string boxName, Logger logger)
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            _driveName = driveName;
            _boxName = boxName;
            _logger = logger;
            _espID = espID;

            PhoenixSwitcher = new PhoenixSwitcherLogic(_logger);

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
            await PhoenixSwitcher.Init(_espID, _driveName, _boxName);

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                , 24, new Action(PhoenixSwitcher.UpdateBundleFilesOnDrive));

            if (Helpers.GetHoursSinceLastUpdate() > 24) PhoenixSwitcher.UpdateBundleFilesOnDrive();
        }
    }
}
