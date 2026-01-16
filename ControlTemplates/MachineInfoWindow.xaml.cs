using System.Windows;
using System.Windows.Controls;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for MachineInfoWindow.xaml
    /// </summary>
    public partial class MachineInfoWindow : UserControl
    {
        private MachineInfoWindowViewModel _viewModel = new MachineInfoWindowViewModel();
        private XmlMachinePCM? _selectedMachine;
        private Logger? _logger;


        public delegate void StartBundleProcessHandler(XmlMachinePCM? selectedMachine);
        public static event StartBundleProcessHandler? OnStartBundleProcess;

        public delegate void FinishedProcessHandler();
        public static event FinishedProcessHandler? OnProcessFinished;

        public MachineInfoWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
        }
        public void Init(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"MachineInfoWindow::Init -> Start initializing MachineInfoWindow.");

            PhoenixSwitcherLogic.OnProcessStarted += ProcessStarted;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            // Call once to setup initial language.
            OnLanguageChanged();

            MachineList.OnMachineSelected += UpdateSelectedMachine;

            _logger?.LogInfo($"MachineInfoWindow::Init -> Finished initializing MachineInfoWindow.");
        }
        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineInfoWindow::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.StartButtonText = Helpers.TryGetLocalizedText("ID_04_0001", "Start");
            _viewModel.FinishButtonText = Helpers.TryGetLocalizedText("ID_04_0002", "Finish");
            _viewModel.SelectedMachineHeaderText = Helpers.TryGetLocalizedText("ID_04_0003", "SelectedMachineInfo");
            _viewModel.MachineTypeDescriptionText = Helpers.TryGetLocalizedText("ID_04_0004", "MachineType: ");
            _viewModel.MachineN17DescriptionText = Helpers.TryGetLocalizedText("ID_04_0005", "Machine Num Long: ");
            _viewModel.MachineN9DescriptionText = Helpers.TryGetLocalizedText("ID_04_0006", "Machine Num Short: ");
            _viewModel.DisplayTypeDescriptionText = Helpers.TryGetLocalizedText("ID_04_0007", "DisplayType: ");
            _viewModel.BundleDescriptionText = Helpers.TryGetLocalizedText("ID_04_0008", "Bundle: ");
            _viewModel.VANDescriptionText = Helpers.TryGetLocalizedText("ID_04_0009", "VAN: ");
            _viewModel.SeriesDescriptionText = Helpers.TryGetLocalizedText("ID_04_00010", "Series: ");
        }

        public async void UpdateSelectedMachine(XmlMachinePCM? machine)
        {
            _logger?.LogInfo($"MachineInfoWindow::SetMachineInfoFromBundle -> Set selected bundle.");
            _selectedMachine = machine;
            if (_selectedMachine == null)
            {
                _viewModel.StartButtonVisibility = Visibility.Hidden;
                _viewModel.MachineN17ValueText = "";
                _viewModel.MachineN9ValueText = "";
                _viewModel.MachineTypeValueText = "";
                _viewModel.SeriesValueText = "";
                _viewModel.VANValueText = "";
                _viewModel.DisplayTypeValueText = "";
                _viewModel.BundleValueText = "";
                StatusDelegates.UpdateStatus(StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
            }
            else
            {
                _viewModel.StartButtonVisibility = Visibility.Visible;
                _viewModel.MachineN17ValueText = _selectedMachine.N17;
                _viewModel.MachineN9ValueText = _selectedMachine.NS;
                _viewModel.MachineTypeValueText = _selectedMachine.Ty;
                _viewModel.DisplayTypeValueText = _selectedMachine.DT;
                _viewModel.SeriesValueText = _selectedMachine.SE;
                _viewModel.VANValueText = _selectedMachine.VAN;

                if (_selectedMachine.Ops != null && _selectedMachine.Ops.Modules != null)
                {
                    XmlModulePCM pcmModule = _selectedMachine.Ops.Modules.First();
                    BundleSelection? bundle = await PhoenixRest.GetInstance().GetPcmAppSettings(_selectedMachine.N17.Substring(0, 4), pcmModule.PCMT, pcmModule.PCMG, _selectedMachine.DT);
                    _viewModel.BundleValueText = (bundle != null && bundle.Bundle != null) ? bundle.Bundle : "'not found'";
                }
                else
                {
                    _viewModel.BundleValueText = "'not found'";
                }
                StatusDelegates.UpdateStatus(StatusLevel.Instruction, "ID_04_0012", "Press start to start the setup process on the 'Phoenix Screen'");
            }

        }

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke start bundle process event.");
            OnStartBundleProcess?.Invoke(_selectedMachine);
            _viewModel.StartButtonVisibility = Visibility.Hidden;
        }
        private void FinishProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke finish process event. And update button visibility");
            OnProcessFinished?.Invoke();
            _viewModel.FinishButtonVisibility = Visibility.Hidden;
        }
        private void ProcessStarted()
        {
            _logger?.LogInfo($"MachineInfoWindow::ProcessStarted -> Update button visibility for started process");
            _viewModel.FinishButtonVisibility = Visibility.Visible;
            _viewModel.StartButtonVisibility = Visibility.Hidden;
        }

    }
}
