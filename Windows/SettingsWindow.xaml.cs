using System.Windows;

using PhoenixSwitcher.ViewModels;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PhoenixSwitcher.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private SettingsWindowViewModel _viewModel = new SettingsWindowViewModel();
        private Logger? _logger;
        public SettingsWindow(Logger? logger)
        {
            InitializeComponent();
            this.DataContext = _viewModel;
            _logger = logger;

            _logger?.LogInfo("SettingsWindow::Constructor -> Setting up settings window");
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();
            Closing += OnWindowClosing;
        }
        
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _logger?.LogInfo("SettingsWindow::OnWindowClosing -> Settings window is closing");
            MessageBoxResult result =  Helpers.ShowLocalizedYesNoMessageBox("ID_06_0004", "Do you want to save changes?");
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.OnSaveCommand.Invoke();
                Helpers.ShowLocalizedOkMessageBox("ID_06_0005", "File has saved. Restart is required to apply changes.");
            }
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
            _viewModel.XmlToEditPath = path;
        }
        private void Save_Click(object? sender, RoutedEventArgs? e)
        {
            _logger?.LogInfo("SettingsWindow::Save_Click -> Attempting to save settings");
            _viewModel.OnSaveCommand.Invoke();
            Helpers.ShowLocalizedOkMessageBox("ID_06_0005", "File has saved. Restart is required to apply changes.");
        }
    }
}
