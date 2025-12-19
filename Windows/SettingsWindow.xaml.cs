using System.Windows;
using AdonisUI.Controls;
using CosntCommonLibrary.Settings;
using PhoenixSwitcher.ViewModels;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PhoenixSwitcher.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private SettingsWindowViewModel _viewModel = new SettingsWindowViewModel();
        public SettingsWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();
            Closing += OnWindowClosing;
        }
        
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult result =  Helpers.ShowLocalizedYesNoMessageBox(Helpers.TryGetLocalizedText("TODO: LOCA", "Do you want to save changes?"));
            if (result == MessageBoxResult.Yes) _viewModel.OnSaveCommand.Invoke();
        }

        private void OnLanguageChanged()
        {
            _viewModel.WindowName = Helpers.TryGetLocalizedText("TODO: LOCA", "Phoenix Switcher - Settings Window");
            _viewModel.SaveButtonText = Helpers.TryGetLocalizedText("TODO: LOCA", "Save");
        }

        public void SetXmlToEdit(string path)
        {
            _viewModel.XmlToEditPath = path;
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OnSaveCommand.Invoke();
        }
    }
}
