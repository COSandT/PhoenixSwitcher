using System.Reflection.PortableExecutable;
using System.Windows;
using System.Windows.Controls;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Xml;
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
            _viewModel.PCMTypeDescriptionText = Helpers.TryGetLocalizedText("ID_04_0005", "PCMType: ");
            _viewModel.PCMGenDescriptionText = Helpers.TryGetLocalizedText("ID_04_0006", "PCMGen: ");
            _viewModel.DisplayTypeDescriptionText = Helpers.TryGetLocalizedText("ID_04_0007", "DisplayType: ");
            _viewModel.BundleDescriptionText = Helpers.TryGetLocalizedText("ID_04_0008", "Bundle: ");
            _viewModel.VANDescriptionText = Helpers.TryGetLocalizedText("ID_04_0009", "VAN: ");
            _viewModel.VANDescriptionText = Helpers.TryGetLocalizedText("ID_04_00010", "Machine Num: ");
        }

        public async void UpdateSelectedMachine(XmlMachinePCM? machine)
        {
            _logger?.LogInfo($"MachineInfoWindow::SetMachineInfoFromBundle -> Set selected bundle.");
            _selectedMachine = machine;
            _viewModel.StartButtonVisibility = Visibility.Visible;
            if (_selectedMachine == null)
            {
                _viewModel.MachineNumberValueText = "";
                _viewModel.MachineTypeValueText = "";
                _viewModel.VANValueText = "";
                _viewModel.PCMTypeValueText = "";
                _viewModel.PCMGenValueText = "";
                _viewModel.DisplayTypeValueText = "";
                _viewModel.BundleValueText = "";
                StatusDelegates.UpdateStatus(StatusLevel.Main, "ID_04_0011", "Select machine from list or use scanner.");
            }
            else
            {
                _viewModel.MachineNumberValueText = _selectedMachine.N17;
                _viewModel.MachineTypeValueText = _selectedMachine.N17.Substring(0, 4);
                _viewModel.DisplayTypeValueText = _selectedMachine.DT;
                _viewModel.VANValueText = _selectedMachine.VAN;

                _viewModel.PCMTypeValueText = "'not found'";
                _viewModel.PCMGenValueText = "'not found'";
                _viewModel.BundleValueText = "'not found'";
                if (_selectedMachine.Ops != null && _selectedMachine.Ops.Modules != null)
                {
                    XmlModulePCM pcmModule = _selectedMachine.Ops.Modules.First();
                    _viewModel.PCMTypeValueText = pcmModule.PCMT;
                    _viewModel.PCMGenValueText = pcmModule.PCMG;
                    BundleSelection? bundle = await PhoenixRest.GetInstance().GetPcmAppSettings(_selectedMachine.N17.Substring(0, 4), pcmModule.PCMT, pcmModule.PCMG, _selectedMachine.DT);
                    _viewModel.BundleValueText = (bundle != null && bundle.Bundle != null) ? bundle.Bundle : "'not found'";
                }
                StatusDelegates.UpdateStatus(StatusLevel.Main, "ID_04_0012", "Press start to start the setup process on the 'Phoenix Screen'");
            }

        }

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke start bundle process event.");
            OnStartBundleProcess?.Invoke(_selectedMachine);
        }
        private void FinishProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke finish process event. And update button visibility");
            OnProcessFinished?.Invoke();
            _viewModel.FinishButtonVisibility = Visibility.Hidden;
            _viewModel.StartButtonVisibility = Visibility.Visible;
        }
        private void ProcessStarted()
        {
            _logger?.LogInfo($"MachineInfoWindow::ProcessStarted -> Update button visibility for started process");
            _viewModel.FinishButtonVisibility = Visibility.Visible;
            _viewModel.StartButtonVisibility = Visibility.Hidden;
        }

    }
}
