using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.ObjectModel;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.Models;
using PhoenixSwitcher.Phoenix;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl, IDisposable
	{
        private readonly MachineListViewModel _viewModel = new MachineListViewModel();
        private PhoenixSwitcherLogic? _switcherLogic = null;
        private LogManager? _logManager;

        private XmlProductionDataPCM? _pcmMachineList = null;
        private XmlMachinePCM? _selectedMachine = null;

		private bool _isInitialized;
		private bool _isDisposed;

		public static event Action<PhoenixSwitcherLogic?, XmlMachinePCM?>? OnMachineSelected;

        public MachineList()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
		}
		private void RegisterEvents()
		{
			LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
			MainWindow.OnMachineListUpdated += Internal_UpdateMachineList;
			MachineInfoWindow.OnStartBundleProcess += OnProcessStarted;

			PhoenixSwitcherLogic.OnProcessFinished += OnProcessFinished;
			PhoenixSwitcherLogic.OnProcessCancelled += OnProcessCancelled;
			PhoenixSwitcherLogic.OnBundleUpdateStarted += OnBundleUpdateStarted;
			PhoenixSwitcherLogic.OnBundleUpdateFinished += OnBundleUpdateFinished;
			PhoenixSwitcherLogic.OnFinishedEspSetup += OnFinishedEspSetup;
		}
		private void UnregisterEvents()
		{
			LocalizationManager.GetInstance().OnActiveLanguageChanged -= OnLanguageChanged;
			MainWindow.OnMachineListUpdated -= Internal_UpdateMachineList;
			MachineInfoWindow.OnStartBundleProcess -= OnProcessStarted;

			PhoenixSwitcherLogic.OnProcessFinished -= OnProcessFinished;
			PhoenixSwitcherLogic.OnProcessCancelled -= OnProcessCancelled;
			PhoenixSwitcherLogic.OnBundleUpdateStarted -= OnBundleUpdateStarted;
			PhoenixSwitcherLogic.OnBundleUpdateFinished -= OnBundleUpdateFinished;
			PhoenixSwitcherLogic.OnFinishedEspSetup -= OnFinishedEspSetup;

			_isInitialized = false;
		}
		public ObservableCollection<MachineListItem> GetListItems()
		{
			return _viewModel.ListViewItems;
		}

		// **********
		// Initialize
		public void Init(PhoenixSwitcherLogic switcherLogic, XmlProductionDataPCM? pcmMachineList)
		{
			if (_isDisposed) return; 
			Log(LogLevel.Info, "Init -> Start initializing MachineList.");

			_switcherLogic = switcherLogic;
			_pcmMachineList = pcmMachineList;
			_logManager = LogManager.GetInstance();
			if (_isInitialized) UnregisterEvents();

			RegisterEvents();
			UpdateLocalizedText();

			if (_pcmMachineList != null) UpdateMachineList(_pcmMachineList);
			_isInitialized = true;
			Log(LogLevel.Info, "Init -> Finished initializing MachineList.");
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
			_viewModel.MachineListHeaderText = Helpers.TryGetLocalizedText("ID_03_0001", "MachineList");
			_viewModel.SelectToScanText = Helpers.TryGetLocalizedText("ID_03_0002", "-- Scan --");
		}


		// *******
		// Events
		private void OnBundleUpdateStarted(PhoenixSwitcherLogic switcherLogic)
		{
			UpdateMachineListEnabledState(switcherLogic);
		}
		private void OnBundleUpdateFinished(PhoenixSwitcherLogic switcherLogic)
		{
			UpdateMachineListEnabledState(switcherLogic);
		}
		private void OnFinishedEspSetup(PhoenixSwitcherLogic switcherLogic, bool bSuccess)
		{
			UpdateMachineListEnabledState(switcherLogic);
		}
		private void OnProcessStarted(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? selectedMachine)
		{
			if (switcherLogic == null) return;
			UpdateMachineListEnabledState(switcherLogic);
		}
		private void OnProcessCancelled(PhoenixSwitcherLogic switcherLogic)
		{
			UpdateMachineListEnabledState(switcherLogic);
			if (!_viewModel.bIsMachineListEnabled) return;
			Log(LogLevel.Info, "OnProcessCancelled -> Reselecting the same machine after cancellation.");
			SelectMachine(_selectedMachine);
		}
		private void OnProcessFinished(PhoenixSwitcherLogic switcherLogic)
		{
			UpdateMachineListEnabledState(switcherLogic);
			if (_switcherLogic != switcherLogic) return;

			Log(LogLevel.Info, "OnProcessFinished -> Clearing selected machine.");
			_selectedMachine = null;
			OnMachineSelected?.Invoke(_switcherLogic, null);
			Log(LogLevel.Info, "OnProcessFinished -> Putting focus on ScanBox.");
			ScannedMachineText.Focus();
		}


		// ***********
		// XAML Events
		private async void OnScannedMachineText_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter)return;

			e.Handled = true;
			string barcode = ScannedMachineText.Text.Trim();
			Log(LogLevel.Info, $"OnScannedMachineText_KeyUp -> Selecting machine using barcode '{barcode}'.");
			try
			{
				await SelectMachineFromTextAsync(barcode);
			}
			finally
			{
				ClearScanBox();
			}
		}
		private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count != 1) return;
			if (e.AddedItems[0] is not MachineListItem item) return;
			if (item.Tag is not XmlMachinePCM machine) return;
			if (IsSameMachine(machine, _selectedMachine))
			{
				Log(LogLevel.Warn, "OnListView_SelectionChanged -> Machine is already selected.");
				return;
			}

			_selectedMachine = machine;
			SelectMachine(_selectedMachine);
		}


		// ************
		// Machine List
		private void Internal_UpdateMachineList(object? sender, XmlProductionDataPCM? pcmMachineList)
		{
			_pcmMachineList = pcmMachineList;
			UpdateMachineList(_pcmMachineList);
		}
		private void UpdateMachineList(XmlProductionDataPCM? pcmMachineList)
		{
			_viewModel.ListViewItems.Clear();
			if (pcmMachineList == null) return;

			Log(LogLevel.Info, "UpdateMachineList -> Filling machine list.");
			foreach (XmlMachinePCM machine in pcmMachineList.Machines)
			{
				_viewModel.ListViewItems.Add(CreateMachineListItem(machine));
			}
		}
		private static MachineListItem CreateMachineListItem(XmlMachinePCM machine)
		{
			return new MachineListItem
			{
				Name = machine.N17,
				Tag = machine
			};
		}


		// *****************
		// Machine Selection
		private async Task SelectMachineFromTextAsync(string text)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(text))
				{
					Log(LogLevel.Warn, "SelectMachineFromText -> Scan text was empty.");
					return;
				}

				XmlProductionDataPCM? machineList = await EnsureMachineListLoadedAsync();

				if (machineList == null)
				{
					Log(LogLevel.Error, "SelectMachineFromText -> Unable to load machine list.");
					return;
				}

				XmlMachinePCM? machine = FindMachineFromScanText(machineList, text.Trim());
				if (machine == null)
				{
					Log(LogLevel.Warn, $"SelectMachineFromText -> No machine found for '{text}'.");
					return;
				}

				SelectMachine(machine);
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, "SelectMachineFromText -> Exception occurred while selecting machine.");
				_logManager?.LogEntireException(ex);
			}
		}
		private async Task<XmlProductionDataPCM?> EnsureMachineListLoadedAsync()
		{
			if (_pcmMachineList != null) return _pcmMachineList;
			Log(LogLevel.Info, "EnsureMachineListLoaded -> Machine list not loaded. Requesting machine file.");
			await PhoenixRest.GetInstance().GetPCMMachineFile();
			return _pcmMachineList;
		}
		private XmlMachinePCM? FindMachineFromScanText(XmlProductionDataPCM machineList, string text)
		{
			if (text.Length == 17)
			{
				Log(LogLevel.Info, "FindMachineFromScanText -> Selecting machine using VIN17.");
				return machineList.Machines.Find(machine => machine.N17 == text);
			}

			if (text.Length <= 10)
			{
				Log(LogLevel.Info, "FindMachineFromScanText -> Selecting machine using VAN.");
				string van = text.PadLeft(10, '0');
				return machineList.Machines.Find(machine => machine.VAN == van);
			}

			Log(LogLevel.Warn, "FindMachineFromScanText -> Invalid scan text.");
			return null;
		}
		private void SelectMachine(XmlMachinePCM? machine)
		{
			try
			{
				if (!CanSelectMachine(showMessage: true))
				{
					Log(LogLevel.Warn, "SelectMachine -> Unable to select machine.");
					return;
				}

				Log(LogLevel.Info, $"SelectMachine -> Machine selected: {machine?.N17 ?? "null"}.");
				XmlProjectSettings settings = Helpers.GetProjectSettings();
				PhoenixSwitcherLogic? targetSwitcher = settings.bShouldSelectPCMForAll ? null : _switcherLogic;

				OnMachineSelected?.Invoke(targetSwitcher, machine);
				UpdateVisualSelection(machine);
				ShowDisplayTypeWarningIfRequired(machine, targetSwitcher);
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, "SelectMachine -> Exception occurred while selecting machine.");
				_logManager?.LogEntireException(ex);
			}
		}
		private static void ShowDisplayTypeWarningIfRequired(XmlMachinePCM? machine, PhoenixSwitcherLogic? targetSwitcher)
		{
			if (machine == null || machine.DT != "1") return; 
			const string fallbackText = "Cannot update phoenix software for display type 1. Select new Machine.";
			StatusDelegates.UpdateStatus(targetSwitcher, StatusLevel.Instruction, "ID_04_0015", fallbackText);
		}
		private bool CanSelectMachine(bool showMessage)
		{
			XmlProjectSettings settings = Helpers.GetProjectSettings();
			Log(LogLevel.Info, "CanSelectMachine -> Checking whether machine selection is allowed.");
			if (_switcherLogic != null && !CanSelectMachineForSwitcher(_switcherLogic, showMessage)) return false;
			if (!settings.bShouldSelectPCMForAll) return true;
			return CanSelectMachineForMultiSelect(showMessage);
		}
		private static bool CanSelectMachineForSwitcher(PhoenixSwitcherLogic switcherLogic, bool showMessage)
		{
			if (switcherLogic.IsPhoenixSetupOngoing)
			{
				ShowSelectionError(showMessage, "ID_04_0023", "Cannot select a new machine when ControllerBox is still initializing.");
				return false;
			}
			if (!switcherLogic.HasEspConnection())
			{
				ShowSelectionError(showMessage, "ID_04_0024", "Cannot select a new machine when ControllerBox is not connected.");
				return false;
			}
			if (switcherLogic.IsUpdatingBundles)
			{
				ShowSelectionError(showMessage, "ID_04_0028", "Cannot select a machine when bundle update is ongoing.");
				return false;
			}
			return true;
		}
		private static bool CanSelectMachineForMultiSelect(bool showMessage)
		{
			int connectedControllers = PhoenixSwitcherLogic.ConnectedEspControllers;
			int requiredControllers = Helpers.GetNumActiveEspController();
			if (connectedControllers < requiredControllers)
			{
				ShowSelectionError(showMessage, "ID_04_0026","Cannot select a new machine in multiselect mode when not all ControllerBoxes are ready.");
				return false;
			}

			if (PhoenixSwitcherLogic.ActiveSetups > 0)
			{
				ShowSelectionError(showMessage,"ID_04_0027","Cannot select a machine in multiselect mode when a setup is still ongoing.");
				return false;
			}
			return true;
		}
		private static void ShowSelectionError(bool showMessage, string localizationId, string fallbackText)
		{
			if (!showMessage)return;
			Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, localizationId,fallbackText);
		}
		private void UpdateVisualSelection(XmlMachinePCM? machine)
		{
			if (machine == null) return;

			Log(LogLevel.Info, "UpdateVisualSelection -> Updating visual selection inside machine list.");
			MachineListItem? targetItem = _viewModel.ListViewItems.FirstOrDefault(item => (item.Tag as XmlMachinePCM)?.N17 == machine.N17);
			if (targetItem == null) return;

			MachineListBox.SelectedItem = targetItem;
			MachineListBox.ScrollIntoView(targetItem);
		}
		private static bool IsSameMachine(XmlMachinePCM? first, XmlMachinePCM? second)
		{
			if (first == null || second == null) return first == second;
			return string.Equals(first.N17, second.N17, StringComparison.Ordinal);
		}
		private void ClearScanBox()
		{
			Log(LogLevel.Info, "ClearScanBox -> Clearing ScanBox and restoring focus.");

			ScannedMachineText.Clear();
			ScannedMachineText.Focus();
		}


		// ******************
		// Machine List State
		private void UpdateMachineListEnabledState(PhoenixSwitcherLogic switcherLogic)
		{
			_viewModel.bIsMachineListEnabled = ShouldMachineListBeActive(switcherLogic);
		}
		private bool ShouldMachineListBeActive(PhoenixSwitcherLogic switcherLogic)
		{
			try
			{
				XmlProjectSettings settings = Helpers.GetProjectSettings();
				if (settings.bShouldSelectPCMForAll) return IsMultiSelectMachineListActive();
				if (switcherLogic == _switcherLogic && switcherLogic != null) return IsSingleSelectMachineListActive(switcherLogic);
				return _viewModel.bIsMachineListEnabled;
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, "ShouldMachineListBeActive -> Exception occurred while checking machine list state.");
				_logManager?.LogEntireException(ex);
				return true;
			}
		}
		private static bool IsMultiSelectMachineListActive()
		{
			return PhoenixSwitcherLogic.OngoingBundleUpdates <= 0
				   && PhoenixSwitcherLogic.ConnectedEspControllers >= Helpers.GetNumActiveEspController()
				   && PhoenixSwitcherLogic.ActiveSetups <= 0;
		}
		private static bool IsSingleSelectMachineListActive(PhoenixSwitcherLogic switcherLogic)
		{
			return switcherLogic.HasEspConnection()
				   && !switcherLogic.IsUpdatingBundles
				   && !switcherLogic.IsPhoenixSetupOngoing
				   && !switcherLogic.IsInitializingEsp;
		}


		// *****
		// Other
		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;

			UnregisterEvents();

			_switcherLogic = null;
			_pcmMachineList = null;
			_selectedMachine = null;
			GC.SuppressFinalize(this);
		}
		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"{_switcherLogic?.BoxText ?? ""}MachineList::{message}");
		}
	}
}