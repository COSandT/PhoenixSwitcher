using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.ViewModels;

using MessageBox = AdonisUI.Controls.MessageBox;



namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private MachineListViewModel _viewModel = new MachineListViewModel();
        private Logger? _logger;

        private const string _defaultFailedPCMAppSettingsText = "Failed to find pcm.";
        private const string _defaultMachineListHeaderText = "Mach Li";
        private const string _defaultSelectToScanText = "-- Scan --";

        public delegate void MachineSelectedHandler(BundleSelection bundle);
        public static event MachineSelectedHandler? OnMachineSelected;

        public MachineList()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            // Call once to setup initial language.
            OnLanguageChanged();
        }
        public void Init(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"MachineList::Init -> Start initializing MachineList.");

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            LoadMachineList();

            _logger?.LogInfo($"MachineList::Init -> Finished initializing MachineList.");
        }

        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineList::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.MachineListHeaderText = Helpers.TryGetLocalizedText("ID_01_0002", _defaultMachineListHeaderText);
            _viewModel.SelectToScanText = Helpers.TryGetLocalizedText("ID_01_0004", _defaultSelectToScanText);
        }
        private async void LoadMachineList()
        {
            _logger?.LogInfo($"MachineList::LoadMachineList -> Getting all pcm app settings to generate machine list from.");
            PhoenixRest phoenixRest = PhoenixRest.GetInstance();
            List<AppSettingPcm>? pcmAppSettings = await phoenixRest.GetPcmAppSettings();
            // TODO: report error not found any pcm settings.
            if (pcmAppSettings == null)
            {
                _logger?.LogWarning($"MachineList::LoadMachineList -> Failed to ge tpcm app settings returning without loading machine list.");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", _defaultFailedPCMAppSettingsText);
                return;
            }

            _logger?.LogInfo($"MachineList::LoadMachineList -> Looping over found pcm app settings and adding each bundle as a machinebutton");
            foreach (AppSettingPcm pcmSetting in pcmAppSettings)
            {
                if (pcmSetting.BundleSelections == null) continue;

                foreach (BundleSelection bundleSelection in pcmSetting.BundleSelections)
                {
                    Button machineButton = new Button();
                    machineButton.Click += MachineSelected_Click;
                    machineButton.Content = bundleSelection;
                    machineButton.Tag = bundleSelection;
                    List.Children.Add(machineButton);
                }
            }
        }
        private void MachineSelected_Click(object sender, RoutedEventArgs e)
        {
            // Invoke OnMachineSelected delegate to let others know which machine was selected.
            _logger?.LogInfo($"MachineList::MachineSelected_Click -> A machine was selected. Let any listeners know which one.");
            Button button = (Button)sender;
            BundleSelection bundleSelection = (BundleSelection)button.Tag;
            OnMachineSelected?.Invoke(bundleSelection);
        }

        private void ScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {

        }
    }
}
