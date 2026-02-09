using System.Windows;

using PhoenixSwitcher.ViewModels;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonViewLibrary.XmlEditorV2;

namespace PhoenixSwitcher.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private SettingsWindowViewModel _viewModel = new SettingsWindowViewModel();
        private XmlProjectSettings _settings;
        private Logger? _logger;

        private bool _bHaveSettingsChanged = false;

        public SettingsWindow(Logger? logger)
        {
            InitializeComponent();
            this.DataContext = _viewModel;
            _logger = logger;

            _settings = Helpers.GetProjectSettings();
            _logger?.LogInfo("SettingsWindow::Constructor -> Setting up settings window");
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();
            Closing += OnWindowClosing;
            Editor.OnValueChanged += Editor_OnValueChanged;
        }

        private void Editor_OnValueChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _bHaveSettingsChanged = true;
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _logger?.LogInfo("SettingsWindow::OnWindowClosing -> Settings window is closing");
            if (!_bHaveSettingsChanged) return;

            MessageBoxResult result =  Helpers.ShowLocalizedYesNoMessageBox("ID_06_0004", "Do you want to save changes?");
            if (result == MessageBoxResult.Yes) Internal_SaveSettings();
        }

        private void OnLanguageChanged()
        {
            _viewModel.WindowName = Helpers.TryGetLocalizedText("ID_06_0001", "Phoenix Switcher - Settings Window");
            _viewModel.FileText = Helpers.TryGetLocalizedText("ID_06_0002", "File");
            _viewModel.SaveButtonText = Helpers.TryGetLocalizedText("ID_06_0003", "Save");
        }
        public void SetXmlToEdit(string path)
        {
            _logger?.LogInfo("SettingsWindow::SetXmlToEdit -> Setting the xml we want to edit");
            _viewModel.SettingsItemList.Add(new CosntCommonViewLibrary.SettingsControl.Models.TabbItemModelReference{ModelReference= _settings, Title="PhoenixUpdater" });
            foreach (EspControllerInfo item in _settings.EspControllers)
            {
                _viewModel.SettingsItemList.Add(new CosntCommonViewLibrary.SettingsControl.Models.TabbItemModelReference { ModelReference = item, Title = item.BoxName });
            }
        }
        private void Save_Click(object? sender, RoutedEventArgs? e)
        {
            Internal_SaveSettings();
        }
        private void Internal_SaveSettings()
        {
            _logger?.LogInfo("SettingsWindow::Save_Click -> Attempting to save settings");
            _settings.TrySave($"{AppContext.BaseDirectory}Settings\\ProjectSettings.xml");
            Helpers.ShowLocalizedOkMessageBox("ID_06_0005", "File has saved. Restart is required to apply changes.");
        }
    }
}
