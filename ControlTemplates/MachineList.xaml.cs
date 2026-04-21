using System.Collections.ObjectModel;
using System.Reflection.PortableExecutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.Models;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private readonly MachineListViewModel _viewModel = new MachineListViewModel();
        private PhoenixSwitcherLogic? _switcherLogic = null;
        private LogManager? _logManager;
        private string _boxText = string.Empty;

        private XmlMachinePCM? _selectedMachine = null;
        private XmlProductionDataPCM? _pcmMachineList;
        private bool _bIsUpdatingSelectedItem = false;

        public delegate void MachineSelectedHandler(PhoenixSwitcherLogic? switcherLogic, XmlMachinePCM? selectedMachinePCMProductionData);
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
        public void Init(PhoenixSwitcherLogic switcherLogic, XmlProductionDataPCM? pcmMachineList)
        {
            _switcherLogic = switcherLogic;
            _boxText = $"Box: {_switcherLogic?.EspInfo.BoxName}\t";
            _logManager = LogManager.GetInstance();
            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Init -> Start initializing MachineList.");

            MainWindow.OnMachineListUpdated += Internal_UpdateMachineList;
            MachineInfoWindow.OnStartBundleProcess += OnProcessStarted;
            PhoenixSwitcherLogic.OnProcessFinished += OnProcessFinished;
            PhoenixSwitcherLogic.OnProcessCancelled += OnProcessCancelled;
            PhoenixSwitcherLogic.OnBundleUpdateStarted += OnBundleUpdateStarted;
            PhoenixSwitcherLogic.OnBundleUpdateFinished += OnBundleUpdateFinished;
            PhoenixSwitcherLogic.OnFinishedEspSetup += OnFinishedEspSetup;

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Init -> Finished initializing MachineList.");
        }
        
        // Delegate bound events
        private void OnBundleUpdateStarted(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = Internal_ShouldMachineListBeActive(switcherLogic);
        }
        private void OnBundleUpdateFinished(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = Internal_ShouldMachineListBeActive(switcherLogic);
        }
        private void OnFinishedEspSetup(PhoenixSwitcherLogic switcherLogic, bool bSuccess)
        {
            _viewModel.bIsMachineListEnabled = Internal_ShouldMachineListBeActive(switcherLogic);
        }
        private void OnProcessStarted(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? selectedMachine)
        {
            _viewModel.bIsMachineListEnabled = Internal_ShouldMachineListBeActive(switcherLogic!);
        }
        private void OnProcessCancelled(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = Internal_ShouldMachineListBeActive(switcherLogic);
            if (_viewModel.bIsMachineListEnabled)
            {
                _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::OnProcessCancelled -> Selecting reselect same machine after cancel");
                Internal_SelectMachine(_selectedMachine);
            }
        }
        private void OnProcessFinished(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = Internal_ShouldMachineListBeActive(switcherLogic);
            if (_switcherLogic != switcherLogic) return;

            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::OnProcessFinished -> Selecting 'Null' machine to clear out info after finish.");
            _selectedMachine = null;
            OnMachineSelected?.Invoke(_switcherLogic, null);
            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::OnProcessFinished -> Put focus on ScanBox.");
            ScannedMachineText.Focus();
        }
        private void OnLanguageChanged()
        {
            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.MachineListHeaderText = Helpers.TryGetLocalizedText("ID_03_0001", "MachineList");
            _viewModel.SelectToScanText = Helpers.TryGetLocalizedText("ID_03_0002", "-- Scan --");
        }


        public ObservableCollection<MachineListItem> GetListItems()
        {
            return _viewModel.ListViewItems;
        }

        // Xaml bound events
        private async void OnScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::OnScannedMachineText_KeyUp -> Calling selectMachine using barcode.");
            string barcode = ScannedMachineText.Text.Trim();
            await Internal_SelectMachineFromText(barcode);

            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Clearning ScanBox and refocus on it.");
            ScannedMachineText.Clear();
            ScannedMachineText.Focus();

            e.Handled = true;
        }
        private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Was item selected.
            if (e.AddedItems.Count != 1) return;

            // Is item already selected.
            MachineListItem? item = (MachineListItem?)e.AddedItems[0];
            XmlMachinePCM? selected = (XmlMachinePCM?)item?.Tag;
            if (selected?.N17 == _selectedMachine?.N17)
            {
                _logManager?.Log(LogLevel.Warn, $"MachineList::OnListView_SelectionChanged -> Machine is already selected.");
                return;
            }

            //// Can select item.
            //if (!Internal_CanSelectMachine(false))
            //{
            //    _logManager?.Log(LogLevel.Warn, $"{_boxText}MachineList::OnListView_SelectionChanged -> Cannot select a new machine when setup is ongoing. will not even bother switching selection");
            //    return;
            //}

            _selectedMachine = selected;
            Internal_SelectMachine(_selectedMachine);
        }


        // Internal Helpers
        private void Internal_UpdateMachineList(XmlProductionDataPCM? pcmMachineList)
        {
            _pcmMachineList = pcmMachineList;
            if (_pcmMachineList != null)
            {
                _viewModel.ListViewItems.Clear();
                _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::UpdatePcmMachineList -> Filling in listview.");
                foreach (XmlMachinePCM pcmMachine in _pcmMachineList.Machines)
                {
                    MachineListItem item = new MachineListItem();
                    item.Name = pcmMachine.N17;
                    item.Tag = pcmMachine;

                    _viewModel.ListViewItems.Add(item);
                }
            }
        }

        private async Task Internal_SelectMachineFromText(string text)
        {
            try
            {
                _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Attempting to select machine from text");
                if (string.IsNullOrEmpty(text))
                {
                    _logManager?.Log(LogLevel.Error, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Text was null");
                    return;
                }
                if (_pcmMachineList == null)
                {
                    await PhoenixRest.GetInstance().GetPCMMachineFile();
                    if (_pcmMachineList == null)
                    {
                        _logManager?.Log(LogLevel.Error, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Unable to get machinelist to select item from.");
                        return;
                    }
                }

                XmlMachinePCM? foundMachine = null;
                if (text.Length == 17)
                {
                    _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Selecting machine using VIN17");
                    foundMachine = _pcmMachineList?.Machines.Find(mach => mach.N17 == text);
                }
                else if (text.Length <= 10)
                {
                    _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Selecting machine using VAN");
                    while (text.Length < 10) { text = $"0{text}"; }
                    foundMachine = _pcmMachineList?.Machines.Find(mach => mach.VAN == text);
                }
                else
                {
                    _logManager?.Log(LogLevel.Error, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Invalid text.");
                    return;
                }

                Internal_SelectMachine(foundMachine);
            }
            catch (Exception ex)
            {
                _logManager?.Log(LogLevel.Error, $"{_boxText}MachineList::Internal_SelectMachineFromText -> Exception occured while selecting machine.");
                _logManager?.LogEntireException(ex);
            }

        }
        private void Internal_SelectMachine(XmlMachinePCM? machine)
        {
            try
            {
                if (!Internal_CanSelectMachine(true))
                {
                    _logManager?.Log(LogLevel.Warn, $"{_boxText}MachineList::Internal_SelectMachine -> Unable to select machine.");
                    return;
                }

                _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_SelectMachine -> Machine was selected: {machine?.N17}");
                XmlProjectSettings settings = Helpers.GetProjectSettings();
                OnMachineSelected?.Invoke(settings.bShouldSelectPCMForAll ? null : _switcherLogic, machine);

                Internal_UpdateVisualSelection(machine);
                Internal_SaveSelectionSetting(machine);

                if (machine != null && machine.DT == 1.ToString())
                {
                    string fallbackText = "Cannot update phoenix software for display type 1. Select new Machine.";
                    StatusDelegates.UpdateStatus(settings.bShouldSelectPCMForAll ? null : _switcherLogic, StatusLevel.Instruction, "ID_04_0015", fallbackText);
                }
            }
            catch (Exception ex)
            {
                _logManager?.Log(LogLevel.Error, $"{_boxText}MachineList::Internal_SelectMachine -> Exception occured while selecting machine.");
                _logManager?.LogEntireException(ex);
            }
        }

        private bool Internal_ShouldMachineListBeActive(PhoenixSwitcherLogic switcherLogic)
        {
            try
            {
                XmlProjectSettings settings = Helpers.GetProjectSettings();
                if (settings.bShouldSelectPCMForAll)
                {
                    return PhoenixSwitcherLogic.NumOngoingBundleUpdates <= 0
                        && PhoenixSwitcherLogic.NumConnectedEspControllers >= Helpers.GetNumActiveEspController()
                        && PhoenixSwitcherLogic.NumActiveSetups <= 0;
                }
                else if (switcherLogic == _switcherLogic && switcherLogic != null)
                {
                    return switcherLogic.HasEspConnection() && !switcherLogic.bIsUpdatingBundles 
                        && !switcherLogic.bIsPhoenixSetupOngoing && !switcherLogic.bIsInitializingEsp;
                }
                return _viewModel.bIsMachineListEnabled;
            }
            catch (Exception ex)
            {
                _logManager?.Log(LogLevel.Error, $"{_boxText}MachineList::Internal_ShouldMachineListBeActive -> Exception occured while checking if machinelist hould be active.");
                _logManager?.LogEntireException(ex);
                return true;
            }
        }
        private bool Internal_CanSelectMachine(bool showMessage)
        {
            string fallbackText = string.Empty;
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_CanSelectMachine -> Checking if we can select machine.");
            if (_switcherLogic != null)
            {
                if (_switcherLogic.bIsPhoenixSetupOngoing)
                {
                    fallbackText = "Cannot select a new machine when ControllerBox is still initializing.";
                    if (showMessage) Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0023", fallbackText);
                    return false;
                }
                else if (!_switcherLogic.HasEspConnection())
                {
                    fallbackText = "Cannot select a new machine when ControllerBox is not connected.";
                    if (showMessage) Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0024", fallbackText);
                    return false;
                }
                if (_switcherLogic.bIsUpdatingBundles)
                {
                    fallbackText = "Cannot select a machine when bundle update is ongoing.";
                    if (showMessage) Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0028", fallbackText);
                    return false;
                }
            }
            if (settings.bShouldSelectPCMForAll)
            {
                if (PhoenixSwitcherLogic.NumConnectedEspControllers < Helpers.GetNumActiveEspController())
                {
                    fallbackText = "Cannot select a new machine in multiselect mode when not all ControllerBoxes are ready.";
                    if (showMessage) Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0026", fallbackText);
                    return false;
                }
                else if (PhoenixSwitcherLogic.NumActiveSetups > 0)
                {
                    fallbackText = "Cannot select a machine in multiselect mode when a setup is still ongoing.";
                    if (showMessage) Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0027", fallbackText);
                    return false;
                }
            }
            return true;
        }
        private void Internal_UpdateVisualSelection(XmlMachinePCM? machine)
        {
            // Update visual selection in the ListBox and scroll it into view.
            if (machine != null)
            {
                _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_UpdateVisualSelection -> Update visual selection inside machine list");
                MachineListItem? targetItem = _viewModel.ListViewItems.FirstOrDefault(i => (i.Tag as XmlMachinePCM)?.N17 == machine.N17);
                if (targetItem != null)
                {
                    MachineListBox.SelectedItem = targetItem;
                    MachineListBox.ScrollIntoView(targetItem);
                }
            }
        }
        private void Internal_SaveSelectionSetting(XmlMachinePCM? machine)
        {
            if (_switcherLogic != null)
            {
                _logManager?.Log(LogLevel.Info, $"{_boxText}MachineList::Internal_SaveSelectionSetting -> Saving selected machine in settings.");

                XmlProjectSettings settings = Helpers.GetProjectSettings();
                foreach (EspControllerInfo espInfo in settings.EspControllers)
                {
                    if (espInfo != _switcherLogic.EspInfo) continue;
                    espInfo.LastSelectedMachineN17 = machine != null ? machine.N17 : "";
                    settings.TrySave($"C:\\COSnT\\PhoenixUpdater\\Settings\\ProjectSettings.xml");
                }
            }
        }
    }
}