using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AdonisUI;
using CosntCommonLibrary.Helpers;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using Org.BouncyCastle.Crypto.IO;
using PhoenixSwitcher.ControlTemplates;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.Models;
using PhoenixSwitcher.ViewModels;
using PhoenixSwitcher.Windows;
using TaskScheduler = CosntCommonLibrary.Helpers.TaskScheduler;

namespace PhoenixSwitcher
{
    public partial class MainWindow : Window
    {
        private List<PhoenixSoftwareUpdater> _softwareUpdaters = new List<PhoenixSoftwareUpdater>();
        private MainWindowViewModel _viewModel = new MainWindowViewModel();
        private Logger _logger;

        private int _gridColumns;
        private int _gridRows;

        private long _millisecondsToWaitGridUpdate = 1000;
        private bool _bCanUpdateGrid = true;

        public XmlProductionDataPCM? PCMMachineList { get; private set; }

        public delegate void MachineListUpdated(XmlProductionDataPCM? pcmMachineList);
        public static event MachineListUpdated? OnMachineListUpdated;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            _logger = new Logger(settings.LogFileName, settings.LogDirectory);
            _logger.LogInfo("MainWindow::Constructor -> Start initializing.");

            Internal_UpdateTheme(settings.Theme);
            InitializeEspControllers();
            InitLanguageSettings();

            _logger.LogInfo("MainWindow::Constructor -> Finished initializing.");
        }
        private async void InitializeEspControllers()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            XmlProjectSettings settings = Helpers.GetProjectSettings();

            //Math to determing how many rows/columns should be made.
            List<EspControllerInfo> activeControllers = new List<EspControllerInfo>();
            foreach (EspControllerInfo espController in settings.EspControllers)
            {
                if (!espController.bIsActive) continue;
                activeControllers.Add(espController);
            }
            SoftwareUpdaterGrid.Visibility = Visibility.Hidden;
            _gridRows = (int)Math.Round(Math.Sqrt(activeControllers.Count));
            _gridColumns = (int)Math.Ceiling((double)activeControllers.Count / (double)_gridRows);
            for (int i = 0; i < _gridRows; ++i)
            {
                StackPanel panel = new StackPanel();
                SoftwareUpdaterGrid.Children.Add(panel);
                panel.Orientation = Orientation.Horizontal;
                panel.VerticalAlignment = VerticalAlignment.Stretch;
                panel.HorizontalAlignment = HorizontalAlignment.Stretch;

                for (int j = 0; j < _gridColumns; ++j)
                {
                    int index = i * _gridColumns + j;
                    if (activeControllers.Count <= index) break;

                    EspControllerInfo espController = activeControllers[index];
                    PhoenixSoftwareUpdater updater = new PhoenixSoftwareUpdater(this, espController.EspID, espController.DriveName, espController.BoxName, _logger);
                    _softwareUpdaters.Add(updater);
                    panel.Children.Add(updater);
                    await Task.Delay(1000);
                    updater.HorizontalAlignment = HorizontalAlignment.Stretch;
                    updater.VerticalAlignment = VerticalAlignment.Stretch;
                    updater.MachineListControl.MachineListBox.SelectionChanged += OnMachineListSelectionChanged;
                }
            }

            // Slight delay giving the stackpanels time to load so their height is set.
            await Task.Delay(500);
            UpdateGridSize();
            SoftwareUpdaterGrid.Visibility = Visibility.Visible;

            TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours
                , settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds
                , 24, new Action(UpdatePcmMachineList));
            UpdatePcmMachineList(); 
            foreach (PhoenixSoftwareUpdater updater in _softwareUpdaters)
            {
                updater.InitPhoenixSwitcher();
            }
            Mouse.OverrideCursor = null;
        }
        private void SoftwareUpdaterGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CheckUpdateGridSize(500);
        }
        private async void CheckUpdateGridSize(long timer)
        {
            _millisecondsToWaitGridUpdate = timer;
            if (_bCanUpdateGrid == true)
            {
                _bCanUpdateGrid = false;
                while (_millisecondsToWaitGridUpdate > 0.0)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    await Task.Delay(250);
                    _millisecondsToWaitGridUpdate -= sw.ElapsedMilliseconds;
                }
                UpdateGridSize();
                _bCanUpdateGrid = true;
            }
        }
        private void UpdateGridSize()
        {
            
            foreach (StackPanel panel in SoftwareUpdaterGrid.Children)
            {
                panel.MaxHeight = GridBorder.ActualHeight / _gridRows;
                panel.Height = GridBorder.ActualHeight / _gridRows;
                panel.MaxWidth = GridBorder.ActualWidth;
                panel.Width = GridBorder.ActualWidth;
                foreach (PhoenixSoftwareUpdater updater in panel.Children)
                {
                    updater.MaxHeight = panel.Height;
                    updater.MaxWidth = panel.Width / _gridColumns;
                    updater.Height = panel.Height;
                    updater.Width = panel.Width / _gridColumns;
                }
            }
        }


        // Init
        public async void UpdatePcmMachineList()
        {
            StatusDelegates.UpdateStatus(null, StatusLevel.Status, "ID_03_0004", "Updating pcm machine list, please wait.");
            _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Started updating pcm machine list.");
            await Application.Current.Dispatcher.Invoke(async delegate
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Getting machine file from RestAPI");
                    PCMMachineList = await Task.Run(() => PhoenixRest.GetInstance().GetPCMMachineFile());
                    if (PCMMachineList == null) throw new Exception("pcm machine list is null.");
                    OnMachineListUpdated?.Invoke(PCMMachineList);
                }
                catch (Exception ex)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_03_0013", "Failed to update pcm machine list. Look at logs for reason.");
                    _logger?.LogError($"MachineList::UpdatePcmMachineList -> exception occured: {ex.Message}");
                }
                Mouse.OverrideCursor = null;
                _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Finished updating pcm machine list");
            });
        }
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


        // Click Events
        private void ChangeSettings_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo("MainWindow::ChangeSettings_Click -> Change settings clicked, opening xml settings editor.");
            SettingsWindow settingsWindow = new SettingsWindow(_logger);

            XmlSettingsHelper<XmlProjectSettings> projectSettings = new XmlSettingsHelper<XmlProjectSettings>("ProjectSettings.xml", $"{AppContext.BaseDirectory}//Settings//");
            settingsWindow.SetXmlToEdit(projectSettings.SettingsFileFolderPath + projectSettings.SettingsFileName);
            settingsWindow.Topmost = true;
            settingsWindow.ShowDialog();

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
            Internal_UpdateBundleFiles();
        }
        private void UpdateMachineList_Click(object sender, RoutedEventArgs e)
        {
            UpdatePcmMachineList();
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo("MainWindow::About_Click -> About button clicked, opening the about window.");
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Topmost = true;
            aboutWindow.ShowDialog();
        }
        private void SwitchThemeDark_Click(object sender, RoutedEventArgs e)
        {
            Internal_UpdateTheme("Dark");
        }

        private void SwitchThemeLight_Click(object sender, RoutedEventArgs e) 
        {
            Internal_UpdateTheme("Light");
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
            _viewModel.ThemeText = Helpers.TryGetLocalizedText("ID_01_0010", "Theme");
            _viewModel.DarkModeText = Helpers.TryGetLocalizedText("ID_01_0011", "Dark Mode");
            _viewModel.LightModeText = Helpers.TryGetLocalizedText("ID_01_0012", "Light Mode");

            foreach (MenuItem item in LanguageSettings.Items)
            {
                string? language = (string?)item.Header;
                item.IsChecked = language == LocalizationManager.GetInstance().GetActiveLanguage();
            }
        }
        private void OnMachineListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (settings == null || !settings.bShouldSelectPCMForAll) return;
            if (e.AddedItems.Count <= 0) return;

            // Select the same object for all machinelists
            foreach (PhoenixSoftwareUpdater updater in _softwareUpdaters)
            {
                MachineList machineList = updater.MachineListControl;
                ObservableCollection<MachineListItem> listItems = machineList.GetListItems();

                MachineListItem? targetItem = listItems.FirstOrDefault(i => (i.Tag as XmlMachinePCM)?.N17 == ((e.AddedItems[0] as MachineListItem)?.Tag as XmlMachinePCM)?.N17);
                if (targetItem != null)
                {
                    machineList.MachineListBox.SelectedItem = targetItem;
                    machineList.MachineListBox.ScrollIntoView(targetItem);

                    // Set it as selected in settings as well
                    int idx = Helpers.GetEspSettingsIdxInfoFromID(updater.PhoenixSwitcher.EspID);
                    if (idx != -1 && targetItem.Tag is XmlMachinePCM)
                    {
                        settings.EspControllers[idx].LastSelectedMachineN17 = ((XmlMachinePCM)targetItem.Tag).N17;
                        settings.TrySave($"{AppContext.BaseDirectory}Settings\\ProjectSettings.xml");
                    }
                }
            }
        }

        private void Internal_UpdateBundleFiles()
        {
            foreach (PhoenixSoftwareUpdater updater in _softwareUpdaters)
            {
                updater.UpdateBundleFiles();
            }
        }
        private void Internal_UpdateTheme(string theme)
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            Uri colorScheme;
            switch (theme)
            {
                case "Light":
                    colorScheme = ResourceLocator.LightColorScheme;
                    settings.Theme = theme;
                    DarkButton.IsChecked = false;
                    LightButton.IsChecked = true;
                    break;
                case "Dark":
                default:
                    colorScheme = ResourceLocator.DarkColorScheme;
                    settings.Theme = "Dark";
                    DarkButton.IsChecked = true;
                    LightButton.IsChecked = false;
                    break;
            }
            ResourceLocator.SetColorScheme(Application.Current.Resources, colorScheme);
            settings.TrySave($"{AppContext.BaseDirectory}Settings\\ProjectSettings.xml");
        }

    }
}