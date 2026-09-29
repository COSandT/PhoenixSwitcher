using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.Phoenix;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{
    public partial class MachineInfoWindow : UserControl
    {
		private readonly MachineInfoWindowViewModel _viewModel = new();

		private PhoenixSwitcherDone _selectedMachineInfo = new();
		private PhoenixSwitcherLogic? _switcherLogic;
		private XmlMachinePCM? _selectedMachine;

		private LogManager? _logManager;

		private bool _isInitialized;
		private bool _isDisposed;

		public static event Action<PhoenixSwitcherLogic?, PhoenixSwitcherDone?>? OnStartBundleProcess;
		public static event Action<PhoenixSwitcherLogic?>? OnProcessFinished;
		public static event Action<PhoenixSwitcherLogic?>? OnShutOffPower;
		public static event Action<PhoenixSwitcherLogic?>? OnTest;

		public MachineInfoWindow()
		{
			InitializeComponent();
			DataContext = _viewModel;
		}
		private void SubscribeToEvents()
		{
			LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
			PhoenixSwitcherLogic.OnProcessStarted += ProcessStarted;
			PhoenixSwitcherLogic.OnFinishedEspSetup += OnFinishedEspSetup;
			PhoenixSwitcherLogic.OnProcessCancelled += OnProcessCancelled;

			MachineList.OnMachineSelected += UpdateSelectedMachine;
		}
		private void UnsubscribeFromEvents()
		{
			LocalizationManager.GetInstance().OnActiveLanguageChanged -= OnLanguageChanged;
			PhoenixSwitcherLogic.OnProcessStarted -= ProcessStarted;
			PhoenixSwitcherLogic.OnFinishedEspSetup -= OnFinishedEspSetup;
			PhoenixSwitcherLogic.OnProcessCancelled -= OnProcessCancelled;

			MachineList.OnMachineSelected -= UpdateSelectedMachine;

			_isInitialized = false;
		}


		// **********
		// Initialize
		public void Init(PhoenixSwitcherLogic switcherLogic)
		{
			if (_isDisposed) return;

			Log(LogLevel.Info, "Init -> Start initializing MachineInfoWindow.");
			_logManager = LogManager.GetInstance();
			_switcherLogic = switcherLogic;

			if (_isInitialized) UnsubscribeFromEvents();
			SubscribeToEvents();
			UpdateLocalizedText();

			_viewModel.ControllerBoxName = switcherLogic.EspInfo.BoxName;
			_isInitialized = true;
			Log(LogLevel.Info, "Init -> Finished initializing MachineInfoWindow.");
		}


		// **********************
		// PhoenixSwitcher Events
		private void ProcessStarted(PhoenixSwitcherLogic switcherLogic)
		{
			if (!IsCurrentSwitcher(switcherLogic)) return; 

			Log(LogLevel.Info, "ProcessStarted -> Updating button visibility for started process.");
			_viewModel.ShutDownPhoenixButtonVisibility = Visibility.Visible;
			_viewModel.StartButtonVisibility = Visibility.Hidden;
		}
		private void OnProcessCancelled(PhoenixSwitcherLogic switcherLogic)
		{
			if (!IsCurrentSwitcher(switcherLogic)) return;

			Log(LogLevel.Info, "OnProcessCancelled -> Resetting process button visibility.");
			HideProcessButtons();
		}
		private void OnFinishedEspSetup(PhoenixSwitcherLogic switcherLogic, bool bSuccess)
		{
			if (!IsCurrentSwitcher(switcherLogic)) return;

			if (bSuccess)
			{
				Log(LogLevel.Info, "OnFinishedEspSetup -> ESP setup completed successfully.");
				StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
				_viewModel.RetryButtonVisibility = Visibility.Hidden;
				return;
			}
			Log(LogLevel.Warn, "OnFinishedEspSetup -> ESP setup failed. Showing retry button.");
			_viewModel.RetryButtonVisibility = Visibility.Visible;
		}
		private bool IsCurrentSwitcher(PhoenixSwitcherLogic? switcherLogic)
		{
			return switcherLogic != null && _switcherLogic == switcherLogic;
		}


		// ************
		// Localization
		private void OnLanguageChanged()
		{
			if (_isDisposed) return;
			Log(LogLevel.Info, "OnLanguageChanged -> Updating text to match selected language.");
			UpdateLocalizedText();
		}
		private void UpdateLocalizedText()
		{
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
			_viewModel.SeriesDescriptionText = Helpers.TryGetLocalizedText("ID_04_0010", "Series: ");
		}


		// *****************
		// Machine Selection
		public async void UpdateSelectedMachine(PhoenixSwitcherLogic? switcherLogic, XmlMachinePCM? machine)
		{
			if (_isDisposed) return; 
			if (_switcherLogic != switcherLogic && switcherLogic != null) return;
			try
			{
				if (machine == null)
				{
					ClearSelectedMachine();
					return;
				}
				await UpdateSelectedMachineAsync(machine);
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, "UpdateSelectedMachine -> Exception occurred while updating selected machine.");
				_logManager?.LogEntireException(ex);
			}
		}
		private async Task UpdateSelectedMachineAsync(XmlMachinePCM machine)
		{
			Log(LogLevel.Info, "UpdateSelectedMachine -> Setting selected machine information.");
			_selectedMachine = machine;
			PopulateMachineInformation(machine);

            // Display type 1 is not supported for Phoenix software updates.
			// Keep the machine information visible, but don't offer the start operation.
			if (machine.DT == "1")
			{
				Log(LogLevel.Warn, "UpdateSelectedMachine -> Unsupported display type. Hiding bundle/start operation.");
				_viewModel.StartButtonVisibility = Visibility.Hidden;
				return;
			}

			await LoadMachineBundleAsync(machine);
			if (!HasValidBundle())
			{
				Log(LogLevel.Warn, "UpdateSelectedMachine -> No bundle found for selected machine.");
				StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0014", "Unable to find bundle for machine. Try other machine.");
				_viewModel.StartButtonVisibility = Visibility.Hidden;
				return;
			}

			Log(LogLevel.Info, "UpdateSelectedMachine -> Showing Start button.");
			_viewModel.StartButtonVisibility = Visibility.Visible;
			StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0012", "Press start to start the setup process on the 'Phoenix Screen'.");
		}
		private void ClearSelectedMachine()
		{
			Log(LogLevel.Info, "UpdateSelectedMachine -> Passed machine was null. Clearing selected machine information.");
			_selectedMachineInfo = new PhoenixSwitcherDone();
			_selectedMachine = null;
			ClearMachineInformation();
			_viewModel.StartButtonVisibility = Visibility.Hidden;
			StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
		}
		private void PopulateMachineInformation(XmlMachinePCM machine)
		{
			_selectedMachineInfo.Vin = _viewModel.MachineN17ValueText = machine.N17;
			_selectedMachineInfo.Vin_9char = _viewModel.MachineN9ValueText = machine.No;
			_selectedMachineInfo.Machine_type = _viewModel.MachineTypeValueText = machine.Ty;
			_selectedMachineInfo.Display_type = _viewModel.DisplayTypeValueText = machine.DT;
			_selectedMachineInfo.Van = _viewModel.VANValueText = machine.VAN;
			_viewModel.SeriesValueText = machine.SE;
			_viewModel.BundleValueText = Helpers.TryGetLocalizedText("ID_04_0020", "'No available Bundle'");
			_viewModel.DisplayTypeValueText = GetDisplayTypeText(machine.DT);
		}
		private static string GetDisplayTypeText(string? displayType)
		{
			return displayType switch
			{
				"1" => $"{displayType} - Fred",
				"2" => $"{displayType} - Phoenix",
				_ => $"{displayType} - Unknown"
			};
		}
		private void ClearMachineInformation()
		{
			_viewModel.MachineN17ValueText = string.Empty;
			_viewModel.MachineN9ValueText = string.Empty;
			_viewModel.MachineTypeValueText = string.Empty;
			_viewModel.SeriesValueText = string.Empty;
			_viewModel.VANValueText = string.Empty;
			_viewModel.DisplayTypeValueText = string.Empty;
			_viewModel.BundleValueText = string.Empty;
		}


		// *************
		// Bundle Loading
		private async Task LoadMachineBundleAsync(XmlMachinePCM machine)
		{
			if (machine.Ops?.Modules == null || machine.Ops.Modules.Count == 0)
			{
				Log(LogLevel.Warn, "LoadMachineBundle -> Selected machine has no PCM modules.");
				return;
			}

			XmlModulePCM pcmModule = machine.Ops.Modules.First();
			_selectedMachineInfo.Pcm_type = pcmModule.PCMT;
			_selectedMachineInfo.Pcm_gen = pcmModule.PCMG;
			if (string.IsNullOrWhiteSpace(machine.N17) || machine.N17.Length < 4)
			{
				Log(LogLevel.Warn, "LoadMachineBundle -> Machine N17 is too short to determine bundle.");
				return;
			}

			string machinePrefix = machine.N17[..4];
			Log(LogLevel.Info, $"LoadMachineBundle -> Looking up bundle for machine prefix '{machinePrefix}'.");
			BundleSelection? bundle = await PhoenixRest.GetInstance().GetPcmAppSettings(machinePrefix, pcmModule.PCMT, pcmModule.PCMG, machine.DT);
			if (string.IsNullOrWhiteSpace(bundle?.Bundle)) return;

			_selectedMachineInfo.Bundle_version = _viewModel.BundleValueText = bundle.Bundle;
		}
		private bool HasValidBundle()
		{
			string? bundle = _viewModel.BundleValueText;
			if (string.IsNullOrWhiteSpace(bundle)) return false;
			string noBundleText = Helpers.TryGetLocalizedText("ID_04_0020", "'No available Bundle'");
			return !bundle.Contains("'No available Bundle'", StringComparison.OrdinalIgnoreCase) && !bundle.Equals(noBundleText, StringComparison.OrdinalIgnoreCase);
		}


		// *************
		// Button Events
		private void StartProcess_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "StartProcess_Click -> Start Process button was pressed.");
			OnStartBundleProcess?.Invoke(_switcherLogic, _selectedMachineInfo);
			_viewModel.StartButtonVisibility = Visibility.Hidden;
		}
		private void TestProcess_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "TestProcess_Click -> Power On button was pressed.");
			OnTest?.Invoke(_switcherLogic);
			_viewModel.TestButtonVisibility = Visibility.Hidden;
			_viewModel.FinishButtonVisibility = Visibility.Hidden;
			_viewModel.ShutDownPhoenixButtonVisibility = Visibility.Visible;
			StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0018", "Press power off once done.");
		}
		private void ShutDownPhoenixProcess_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "ShutDownPhoenixProcess_Click -> Power Off button was pressed.");
			OnShutOffPower?.Invoke(_switcherLogic);
			_viewModel.TestButtonVisibility = Visibility.Visible;
			_viewModel.FinishButtonVisibility = Visibility.Visible;
			_viewModel.ShutDownPhoenixButtonVisibility = Visibility.Hidden;
			StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0019", "Press finish once done with this screen or Power On if you want to see if screen got updates properly.");
		}
		private void FinishProcess_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "FinishProcess_Click -> Finish Process button was pressed.");
			PostMachineResults();
			_viewModel.FinishButtonVisibility = Visibility.Hidden;
			_viewModel.TestButtonVisibility = Visibility.Hidden;
			OnProcessFinished?.Invoke(_switcherLogic);
			HandleFinishMachineSelection();
		}
		private void RetryEspSetup_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "RetryEspSetup_Click -> Retry ESP setup button was pressed.");
			try
			{
				Mouse.OverrideCursor = Cursors.Wait;
				_switcherLogic?.RetryInitAsync();
				_viewModel.RetryButtonVisibility = Visibility.Hidden;
			}
			finally
			{
				Mouse.OverrideCursor = null;
			}
		}


		// *****************
		// Finish Processing
		private void PostMachineResults()
		{
			try
			{
				_selectedMachineInfo.TimeStamp = DateTime.Now;
				PhoenixSwitcherDone result = _selectedMachineInfo;
				Log(LogLevel.Info, "PostMachineResults -> Posting machine results to server.");
				Task.Run(() => PhoenixRest.GetInstance().PostMachineResults(result));
			}
			catch (Exception ex)
			{
				Log(LogLevel.Warn, $"PostMachineResults -> Unable to post machine results: {ex.Message}");
			}
		}
		private void HandleFinishMachineSelection()
		{
			XmlProjectSettings settings = Helpers.GetProjectSettings();
			if (settings.bShouldSelectPCMForAll) return;

			MessageBoxResult result = Helpers.ShowLocalizedYesNoMessageBox(Application.Current.MainWindow, "ID_04_0013", "Do you want to setup another screen for this machine?");
			if (result != MessageBoxResult.Yes) return; 

            // Finish has already been raised above so the rest of the application can reset its state. 
            // Re-populate this control if the operator wants to configure the same machine again.
			XmlMachinePCM? machine = _selectedMachine;
			if (machine != null) UpdateSelectedMachine(_switcherLogic, machine);
		}
		private void HideProcessButtons()
		{
			_viewModel.ShutDownPhoenixButtonVisibility = Visibility.Hidden;
			_viewModel.FinishButtonVisibility = Visibility.Hidden;
			_viewModel.TestButtonVisibility = Visibility.Hidden;
			_viewModel.StartButtonVisibility = Visibility.Hidden;
		}


		// *****
		// Other
		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;

			UnsubscribeFromEvents();
			_switcherLogic = null;
			_selectedMachine = null;
			_selectedMachineInfo = new PhoenixSwitcherDone();
		}
		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"{_switcherLogic?.BoxText ?? ""}MachineInfoWindow::{message}");
		}
	}
}
