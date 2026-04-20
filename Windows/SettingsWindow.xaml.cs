using System.Windows;

using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonViewLibrary.XmlEditorV2;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonViewLibrary.XmlEditorV2.Models;
using CosntCommonViewLibrary.SettingsControl.Models;

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
        private XmlProjectSettings _settings;

        private bool _bHaveSettingsChanged = false;

        public SettingsWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
            LogManager.GetInstance()?.Log(LogLevel.Info, "SettingsWindow::Constructor -> Setting up settings window");

            _settings = Helpers.GetProjectSettings();
            Internal_AddSettings();

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            Closing += OnWindowClosing;
            Editor.OnValueChanged += Editor_OnValueChanged;
        }

        private void Editor_OnValueChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender == null) return;

            PropertyInfoReference infoRef = (PropertyInfoReference)sender;
            if (infoRef == null) return;

            // Update the names of the controllberbox tabs.
            if (infoRef.ReferenceObject != null && infoRef.ReferenceObject is EspControllerInfo)
            {
                for (int i = 0; i < _settings.EspControllers.Count; ++i)
                {
                    _viewModel.SettingsItemList[i + 1].Title = _settings.EspControllers[i].BoxName;
                }
            }

            _bHaveSettingsChanged = true;
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            LogManager.GetInstance()?.Log(LogLevel.Info, "SettingsWindow::OnWindowClosing -> Settings window is closing");
            if (!_bHaveSettingsChanged) return;

            MessageBoxResult result =  Helpers.ShowLocalizedYesNoMessageBox(this, "ID_06_0004", "Do you want to save changes?");
            if (result == MessageBoxResult.Yes) Internal_SaveSettings();
        }

        private void OnLanguageChanged()
        {
            _viewModel.WindowName = Helpers.TryGetLocalizedText("ID_06_0001", "Phoenix Switcher - Settings Window");
            _viewModel.FileText = Helpers.TryGetLocalizedText("ID_06_0002", "File");
            _viewModel.SaveButtonText = Helpers.TryGetLocalizedText("ID_06_0003", "Save");
            _viewModel.AddButtonText = Helpers.TryGetLocalizedText("ID_06_0006", "Add");
        }
        private void Save_Click(object? sender, RoutedEventArgs? e)
        {
            Internal_SaveSettings();
        }
        private void AddControllerBox_Click(object? sender, RoutedEventArgs? e)
        {
            EspControllerInfo espInfo = new EspControllerInfo();
            _settings.EspControllers.Add(espInfo);

            TabbItemModelReference modelRef = new TabbItemModelReference { ModelReference = espInfo, Title = "NewBox" };
            _viewModel.SettingsItemList.Add(modelRef);
            _bHaveSettingsChanged = true;
        }


        private void Internal_AddSettings()
        {
            _viewModel.SettingsItemList.Add(new TabbItemModelReference { ModelReference = _settings, Title = "PhoenixUpdater" });
            foreach (EspControllerInfo item in _settings.EspControllers)
            {
                _viewModel.SettingsItemList.Add(new TabbItemModelReference { ModelReference = item, Title = item.BoxName });
            }
        }
        private void Internal_SaveSettings()
        {
            LogManager.GetInstance()?.Log(LogLevel.Info, "SettingsWindow::Save_Click -> Attempting to save settings");
            _settings.TrySave($"C:\\COSnT\\PhoenixUpdater\\Settings\\ProjectSettings.xml");
            _bHaveSettingsChanged = false;
            Helpers.ShowLocalizedOkMessageBox(this, "ID_06_0005", "File has saved. Restart is required to apply changes.");
        }
    }
}
