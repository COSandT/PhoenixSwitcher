using System.Windows;
using System.Windows.Controls;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Helpers;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

using PhoenixSwitcher.Windows;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher
{
    public struct Version
    {
        public static string VersionNum = "0.1";
    }
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
            await _phoenixSwitcher.Init();
            await _phoenixSwitcher.UpdateBundleFiles();
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
            _viewModel.LanguageSettingsText = Helpers.TryGetLocalizedText("ID_01_0003", "Languages");
            _viewModel.AboutText = Helpers.TryGetLocalizedText("ID_01_0004", "About");

            foreach (MenuItem item in LanguageSettings.Items)
            {
                string? language = (string?)item.Header;
                item.IsChecked = language == LocalizationManager.GetInstance().GetActiveLanguage();
            }
        }
    }
}