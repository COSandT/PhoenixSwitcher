using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for MachineInfoWindow.xaml
    /// </summary>
    public partial class MachineInfoWindow : UserControl
    {
        private MachineInfoWindowViewModel _viewModel = new MachineInfoWindowViewModel();
        private PhoenixSwitcherDone _selectedMachineInfo = new PhoenixSwitcherDone();
        private PhoenixSwitcherLogic? _switcherLogic = null;
        private XmlMachinePCM? _selectedMachine = new XmlMachinePCM();
        private Logger? _logger;


        public delegate void StartBundleProcessHandler(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? selectedMachine);
        public static event StartBundleProcessHandler? OnStartBundleProcess;

        public delegate void TestProcessHandler(PhoenixSwitcherLogic? switcherLogic, bool power = true);
        public static event TestProcessHandler? OnTest;

        public delegate void ShutOffPowerProcessHandler(PhoenixSwitcherLogic? switcherLogic, bool power = false);
        public static event ShutOffPowerProcessHandler? OnShutOffPower;

        public delegate void FinishedProcessHandler(PhoenixSwitcherLogic? switcherLogic);
        public static event FinishedProcessHandler? OnProcessFinished;

        public MachineInfoWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
        }
        public void Init(PhoenixSwitcherLogic switcherLogic, Logger logger)
        {
            _logger = logger;
            _switcherLogic = switcherLogic;
            _logger?.LogInfo($"MachineInfoWindow::Init -> Start initializing MachineInfoWindow.");

            PhoenixSwitcherLogic.OnProcessStarted += ProcessStarted;
            PhoenixSwitcherLogic.OnFinishedEspSetup += OnFinishedEspSetup;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            // Call once to setup initial language.
            OnLanguageChanged();

            MachineList.OnMachineSelected += UpdateSelectedMachine;

            _logger?.LogInfo($"MachineInfoWindow::Init -> Finished initializing MachineInfoWindow.");
        }

        // Bound delegate events
        private void ProcessStarted(PhoenixSwitcherLogic switcherLogic)
        {
            if (_switcherLogic != switcherLogic) return;

            _logger?.LogInfo($"MachineInfoWindow::ProcessStarted -> Update button visibility for started process");
            _viewModel.ShutDownPhoenixButtonVisibility = Visibility.Visible;
            _viewModel.StartButtonVisibility = Visibility.Hidden;
        }
        private void OnFinishedEspSetup(PhoenixSwitcherLogic switcherLogic, bool bSuccess)
        {
            if (switcherLogic != _switcherLogic) return;
            if (bSuccess)
            {
                StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
            }
            else
            {
                _viewModel.RetryButtonVisibility = Visibility.Visible;
            }
        }

        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineInfoWindow::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.StartButtonText = Helpers.TryGetLocalizedText("ID_04_0001", "Start");
            _viewModel.FinishButtonText = Helpers.TryGetLocalizedText("ID_04_0002", "Finish");
            _viewModel.RetryButtonText = Helpers.TryGetLocalizedText("ID_04_0025", "Retry");
            _viewModel.TestButtonText = Helpers.TryGetLocalizedText("ID_04_0016", "Power On");
            _viewModel.ShutDownPhoenixText = Helpers.TryGetLocalizedText("ID_04_0017", "Power Off");

            _viewModel.SelectedMachineHeaderText = Helpers.TryGetLocalizedText("ID_04_0003", "Machine Info");
            _viewModel.MachineTypeDescriptionText = Helpers.TryGetLocalizedText("ID_04_0004", "MachineType: ");
            _viewModel.MachineN17DescriptionText = Helpers.TryGetLocalizedText("ID_04_0005", "Machine Num Long: ");
            _viewModel.MachineN9DescriptionText = Helpers.TryGetLocalizedText("ID_04_0006", "Machine Num Short: ");
            _viewModel.DisplayTypeDescriptionText = Helpers.TryGetLocalizedText("ID_04_0007", "DisplayType: ");
            _viewModel.BundleDescriptionText = Helpers.TryGetLocalizedText("ID_04_0008", "Bundle: ");
            _viewModel.VANDescriptionText = Helpers.TryGetLocalizedText("ID_04_0009", "VAN: ");
            _viewModel.SeriesDescriptionText = Helpers.TryGetLocalizedText("ID_04_00010", "Series: ");
        }
        public async void UpdateSelectedMachine(PhoenixSwitcherLogic? switcherLogic, XmlMachinePCM? machine)
        {
            if (_switcherLogic != switcherLogic && switcherLogic != null) return;
            _logger?.LogInfo($"MachineInfoWindow::SetMachineInfoFromBundle -> Set selected bundle.");
            if (machine == null)
            {
                _viewModel.StartButtonVisibility = Visibility.Hidden;
                _selectedMachineInfo = new PhoenixSwitcherDone();
                _selectedMachine = null;
                _viewModel.MachineN17ValueText = "";
                _viewModel.MachineN9ValueText = "";
                _viewModel.MachineTypeValueText = "";
                _viewModel.SeriesValueText = "";
                _viewModel.VANValueText = "";
                _viewModel.DisplayTypeValueText = "";
                _viewModel.BundleValueText = "";
                StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
                return;
            }

            _selectedMachine = machine;
            _selectedMachineInfo.Vin = _viewModel.MachineN17ValueText = machine.N17;
            _selectedMachineInfo.Vin_9char = _viewModel.MachineN9ValueText = machine.No;
            _selectedMachineInfo.Machine_type = _viewModel.MachineTypeValueText = machine.Ty;
            _selectedMachineInfo.Display_type = _viewModel.DisplayTypeValueText = machine.DT;
            _selectedMachineInfo.Van = _viewModel.VANValueText = machine.VAN;
            _viewModel.SeriesValueText = machine.SE;
            _viewModel.BundleValueText = Helpers.TryGetLocalizedText("ID_04_0020", "'No available Bundle'");

            switch (machine.DT)
            {
                case "1":
                    _viewModel.DisplayTypeValueText = $"{machine.DT} - Fred";
                    break;
                case "2":
                    _viewModel.DisplayTypeValueText = $"{machine.DT}  - Phoenix";
                    break;
                default:
                    _viewModel.DisplayTypeValueText = $"{machine.DT} - Unknown";
                    break;
            }

            if (machine.DT == 1.ToString())
            {
               _viewModel.StartButtonVisibility = Visibility.Hidden;
                return;
            }

            if (machine.Ops != null && machine.Ops.Modules != null)
            {
                XmlModulePCM pcmModule = machine.Ops.Modules.First();
                _selectedMachineInfo.Pcm_type = pcmModule.PCMT;
                _selectedMachineInfo.Pcm_gen = pcmModule.PCMG;

                BundleSelection? bundle = await PhoenixRest.GetInstance().GetPcmAppSettings(machine.N17.Substring(0, 4), pcmModule.PCMT, pcmModule.PCMG, machine.DT);
                if (bundle != null && bundle.Bundle != null) _selectedMachineInfo.Bundle_version = _viewModel.BundleValueText = bundle.Bundle;
            }

            if (_viewModel.BundleValueText == "'not found'")
            {
                StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0014", "Unable to find bundle for machine. Try other machine.");
                Helpers.ShowLocalizedOkMessageBox("ID_04_0014", "Unable to find bundle for machine. Try other machine.");
                return;
            }

            _viewModel.StartButtonVisibility = Visibility.Visible;
            StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0012", "Press start to start the setup process on the 'Phoenix Screen'");
        }

        // Button press events
        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke start bundle process event.");
            OnStartBundleProcess?.Invoke(_switcherLogic, _selectedMachineInfo);
            _viewModel.StartButtonVisibility = Visibility.Hidden;
        }
        private void TestProcess_Click(object sender, RoutedEventArgs e)
        {
            OnTest?.Invoke(_switcherLogic, true);
            _viewModel.ShutDownPhoenixButtonVisibility = Visibility.Visible;
            _viewModel.FinishButtonVisibility = Visibility.Hidden;
            _viewModel.TestButtonVisibility = Visibility.Hidden;
            StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0018", "Press power off once done.");
        }
        private void ShutDownPhoenixProcess_Click(object sender, RoutedEventArgs e)
        {
            OnShutOffPower?.Invoke(_switcherLogic, false);
            _viewModel.TestButtonVisibility = Visibility.Visible;
            _viewModel.FinishButtonVisibility = Visibility.Visible;
            _viewModel.ShutDownPhoenixButtonVisibility = Visibility.Hidden;
            StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0019", "Press finish once done with this screen or Power On if you want to see if screen got updates properly.");
        }
        private void FinishProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke finish process event. And update button visibility");
            _selectedMachineInfo.TimeStamp = DateTime.Now;
            PhoenixRest.GetInstance().PostMachineResults(_selectedMachineInfo);

            _viewModel.FinishButtonVisibility = Visibility.Hidden;
            _viewModel.TestButtonVisibility = Visibility.Hidden;
            XmlMachinePCM? machine = _selectedMachine;
            OnProcessFinished?.Invoke(_switcherLogic);

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (!settings.bShouldSelectPCMForAll)
            {
                MessageBoxResult result = Helpers.ShowLocalizedYesNoMessageBox("ID_04_0013", "Do you want to setup another screen for this machine?");
                // Still call finish to reset everything properly but immediatly call UpdateSelectedMachine to fill in the info again.
                if (result == MessageBoxResult.Yes) UpdateSelectedMachine(_switcherLogic, machine);
            }

        }
        private void RetryEspSetup_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _switcherLogic?.RetryInit();
            _viewModel.RetryButtonVisibility = Visibility.Hidden;
            Mouse.OverrideCursor = null;
        }


    }
}
