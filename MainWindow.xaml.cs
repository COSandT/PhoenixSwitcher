using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Helpers;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

using PhoenixSwitcher.Windows;
using PhoenixSwitcher.ViewModels;
using TaskScheduler = CosntCommonLibrary.Helpers.TaskScheduler;

namespace PhoenixSwitcher
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel = new MainWindowViewModel();
        private PhoenixSwitcherLogic _phoenixSwitcher;
        private Logger _logger;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            _logger = new Logger(settings.LogFileName, settings.LogDirectory);
            _logger.LogInfo("MainWindow::Constructor -> Start initializing.");

            _phoenixSwitcher = new PhoenixSwitcherLogic(_logger);
            InitPhoenixSwitcherLogic();
            InitLanguageSettings();

            StatusBarControl.Init(_logger);
            MachineInfoWindowControl.Init(_logger);
            MachineListControl.Init(_logger);

            _logger.LogInfo("MainWindow::Constructor -> Finished initializing.");
        }

        // Init
        private void InitLanguageSettings()
        {
            foreach (string language in LocalizationManager.GetInstance().AvailableLanguages)
            {
                MenuItem item = new MenuItem();
                item.Header = language;
                item.Click += ChangeLanguage_Click;
                item.IsCheckable = true;
                LanguageSettings.Items.Add(item);
            }
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();
        }
        private async void InitPhoenixSwitcherLogic()
        {
            try
            {
                await Task.Run(() => _phoenixSwitcher.Init());

                XmlProjectSettings settings = Helpers.GetProjectSettings();
                TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                    , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                    , 24, new Action(_phoenixSwitcher.UpdateBundleFilesOnDrive));
                TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                    , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                    , 24, new Action(MachineListControl.UpdatePcmMachineList));
                UpdateBundleAndMachineList();
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                _logger.LogError($"MainWindow::InitPhoenixSwitcherLoging -> exception occured: {ex.Message}");
            }
        }
        private async void UpdateBundleAndMachineList()
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            // Too long has passed since an updat for the bundle files. time to update them.
            int daysBetweenUpdate = DateTime.Now.DayOfYear - settings.LastBundleUpdateDate.DayOfYear;
            int hoursBetweenUpdate = DateTime.Now.TimeOfDay.Hours - settings.LastBundleUpdateDate.TimeOfDay.Hours + (daysBetweenUpdate * 24);
            if (hoursBetweenUpdate > 24)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await Task.Run(() => _phoenixSwitcher.UpdateBundleFilesOnDrive());
                await Task.Run(() => MachineListControl.UpdatePcmMachineList());
                Mouse.OverrideCursor = null;
            }
        }

        // Click Events
        private void ChangeSettings_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo("MainWindow::ChangeSettings_Click -> Change settings clicked, opening xml settings editor.");
            SettingsWindow settingsWindow = new SettingsWindow();

            XmlSettingsHelper<XmlProjectSettings> projectSettings = new XmlSettingsHelper<XmlProjectSettings>("ProjectSettings.xml", $"{AppContext.BaseDirectory}//Settings//");
            settingsWindow.SetXmlToEdit(projectSettings.SettingsFileFolderPath + projectSettings.SettingsFileName);
            settingsWindow.Show();
        }
        private void ChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            if (item == null) return;

            string? newLanguage = (string?)item.Header;
            if (newLanguage == null) return;

            LocalizationManager.GetInstance().SetActiveLanguage(newLanguage);
        }
        private void UpdateBundleFiles_Click(object sender, RoutedEventArgs e)
        {
            _phoenixSwitcher.UpdateBundleFilesOnDrive();
        }
        private void UpdateMachineList_Click(object sender, RoutedEventArgs e)
        {
            MachineListControl.UpdatePcmMachineList();
        }
        private void ConnectToEspController_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Task.Run(() => _phoenixSwitcher.Init());
            UpdateBundleAndMachineList();
            MachineInfoWindowControl.UpdateSelectedMachine(null);
            Mouse.OverrideCursor = null;
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo("MainWindow::About_Click -> About button clicked, opening the about window.");
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Show();
        }

        // Other
        private void OnLanguageChanged()
        {
            _logger?.LogInfo("MainWindow::OnLanguageChanged -> Updating localized text to newly selected language.");
            _viewModel.WindowName = Helpers.TryGetLocalizedText("ID_01_0001", "Phoenix Switcher");
            _viewModel.SettingsText = Helpers.TryGetLocalizedText("ID_01_0002", "Settings");
            _viewModel.ProgramSettingsText = Helpers.TryGetLocalizedText("ID_01_0003", "Program Settings");
            _viewModel.LanguageSettingsText = Helpers.TryGetLocalizedText("ID_01_0004", "Languages");
            _viewModel.HelpText = Helpers.TryGetLocalizedText("ID_01_0005", "UpdateBundleFiles");
            _viewModel.AboutText = Helpers.TryGetLocalizedText("ID_01_0006", "UpdateMachineList");
            _viewModel.UpdateText = Helpers.TryGetLocalizedText("ID_01_0007", "UpdateBundleFiles");
            _viewModel.UpdateBundleFilesText = Helpers.TryGetLocalizedText("ID_01_0008", "UpdateBundleFiles");
            _viewModel.UpdateMachineListText = Helpers.TryGetLocalizedText("ID_01_0009", "UpdateMachineList");
            _viewModel.ConntectText = Helpers.TryGetLocalizedText("ID_01_0010", "Connect");
            _viewModel.EspControllerText = Helpers.TryGetLocalizedText("ID_01_0011", "EspController");

            foreach (MenuItem item in LanguageSettings.Items)
            {
                string? language = (string?)item.Header;
                item.IsChecked = language == LocalizationManager.GetInstance().GetActiveLanguage();
            }
        }
    }
}