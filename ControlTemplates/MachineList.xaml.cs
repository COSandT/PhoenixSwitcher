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
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private readonly MachineListViewModel _viewModel = new MachineListViewModel();
        private PhoenixSwitcherLogic? _switcherLogic = null;
        private LogManager? _logManager;

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
            _logManager = LogManager.GetInstance();
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::Init -> Start initializing MachineList.");

            MainWindow.OnMachineListUpdated += Internal_UpdateMachineList;
            MachineInfoWindow.OnStartBundleProcess += OnProcessStarted;
            PhoenixSwitcherLogic.OnProcessFinished += OnProcessFinished;
            PhoenixSwitcherLogic.OnProcessCancelled += OnProcessCancelled;
            PhoenixSwitcherLogic.OnBundleUpdateStarted += OnBundleUpdateStarted;
            PhoenixSwitcherLogic.OnBundleUpdateFinished += OnBundleUpdateFinished;
            PhoenixSwitcherLogic.OnFinishedEspSetup += OnFinishedEspSetup;

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            //ListScrollViewer.ScrollChanged += OnScrollViewerChanged;
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::Init -> Finished initializing MachineList.");
        }
        
        // Delegate bound events
        private async void OnBundleUpdateStarted(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
        }
        private async void OnBundleUpdateFinished(PhoenixSwitcherLogic switcherLogic)
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
        }
        private async void OnFinishedEspSetup(PhoenixSwitcherLogic switcherLogic, bool bSuccess)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
            //if (_viewModel.bIsMachineListEnabled) await Internal_UpdateSelectedMachineFromSettings();
        }
        private async void OnProcessStarted(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? selectedMachine)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic!);
        }
        private async void OnProcessCancelled(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
            if (_viewModel.bIsMachineListEnabled) await Internal_SelectMachine(_selectedMachine);
        }
        private async void OnProcessFinished(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
            if (_switcherLogic != switcherLogic) return;
            _selectedMachine = null;
            OnMachineSelected?.Invoke(_switcherLogic, _selectedMachine);
            ScannedMachineText.Focus();
        }
        private void OnLanguageChanged()
        {
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::OnLanguageChanged -> Updating text to match newly selected language.");
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
            string barcode = ScannedMachineText.Text.Trim();
            await Internal_SelectMachineFromText(barcode);

            e.Handled = true;
        }
        private async void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Invoke OnMachineSelected delegate to let others know which machine was selected.
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::MachineSelected_Click -> A machine was selected. Let any listeners know which one.");
            if (e.AddedItems.Count != 1) return;

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if ((_switcherLogic != null && (_switcherLogic.bIsPhoenixSetupOngoing || _switcherLogic.bIsInitializingEsp))
                || (settings.bShouldSelectPCMForAll && (PhoenixSwitcherLogic.NumActiveSetups > 0 || PhoenixSwitcherLogic.NumConnectedEspControllers < Helpers.GetNumActiveEspController())))
            {
                LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::MachineSelected_Click -> Cannot select a new machine when setup is ongoing. will not even bother switching selection");
                return;
            }

            MachineListItem? item = (MachineListItem?)e.AddedItems[0];
            _selectedMachine = (XmlMachinePCM?)item?.Tag;
            await Internal_SelectMachine(_selectedMachine);
        }


        // Internal Helpers
        private void Internal_UpdateMachineList(XmlProductionDataPCM? pcmMachineList)
        {
            _pcmMachineList = pcmMachineList;
            if (_pcmMachineList != null)
            {
                _viewModel.ListViewItems.Clear();
                LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::UpdatePcmMachineList -> Filling in listview.");
                foreach (XmlMachinePCM pcmMachine in _pcmMachineList.Machines)
                {
                    MachineListItem item = new MachineListItem();
                    item.Name = pcmMachine.N17;
                    item.Tag = pcmMachine;

                    _viewModel.ListViewItems.Add(item);
                }
            }
        }

        private async Task<bool> Internal_SelectMachineFromText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return false;
                if (_pcmMachineList == null)
                {
                    await PhoenixRest.GetInstance().GetPCMMachineFile();
                    if (_pcmMachineList == null) return false;
                }

                XmlMachinePCM? foundMachine = null;
                if (text.Length == 17) foundMachine = _pcmMachineList?.Machines.Find(mach => mach.N17 == text);
                else if (text.Length == 10) foundMachine = _pcmMachineList?.Machines.Find(mach => mach.VAN == text);
                else if (text.Length < 10)
                {
                    while (text.Length < 10)
                    {
                        text = $"0{text}";
                    }
                    foundMachine = _pcmMachineList?.Machines.Find(mach => mach.VAN == text);
                }
                else return false;

                await Internal_SelectMachine(foundMachine);
                Internal_UpdateVisualSelection(foundMachine);

                ScannedMachineText.Clear();
                ScannedMachineText.Focus();
                return true;
            }
            catch
            {
                return false;
            }
            
        }
        private async Task<bool> Internal_ShouldMachineListBeActive(PhoenixSwitcherLogic switcherLogic)
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
                else if (switcherLogic == _switcherLogic)
                {
                    return switcherLogic.HasEspConnection() && !switcherLogic.bIsUpdatingBundles 
                        && !switcherLogic.bIsPhoenixSetupOngoing && !switcherLogic.bIsInitializingEsp;
                }
                return _viewModel.bIsMachineListEnabled;
            }
            catch
            {
                await Task.Delay(250);
                return await Internal_ShouldMachineListBeActive(switcherLogic);
            }
        }
        private async Task Internal_SelectMachine(XmlMachinePCM? machine)
        {
            if (_bIsUpdatingSelectedItem) return;
            _bIsUpdatingSelectedItem = true;

            try
            {
                if (!await Internal_CanSelectMachine())
                {
                    LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tMachineList::Internal_SelectMachine -> Unable to select machine.");
                    _bIsUpdatingSelectedItem = false;
                    return;
                }

                XmlProjectSettings settings = Helpers.GetProjectSettings();
                OnMachineSelected?.Invoke(settings.bShouldSelectPCMForAll ? null : _switcherLogic, machine);
                Internal_UpdateVisualSelection(machine);
                if (_switcherLogic != null)
                {
                    foreach (EspControllerInfo espInfo in settings.EspControllers)
                    {
                        if (espInfo != _switcherLogic.EspInfo) continue;
                        espInfo.LastSelectedMachineN17 = machine != null ? machine.N17 : "";
                        settings.TrySave($"C:\\COSnT\\PhoenixUpdater\\Settings\\ProjectSettings.xml");
                    }
                }

                if (machine != null && machine.DT == 1.ToString())
                {
                    StatusDelegates.UpdateStatus(settings.bShouldSelectPCMForAll ? null : _switcherLogic, StatusLevel.Instruction, "ID_04_0015", "Cannot update phoenix software for display type 1. Select new Machine.");
                }
            }
            catch { }
            _bIsUpdatingSelectedItem = false;
        }
        private async Task<bool> Internal_CanSelectMachine()
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (_switcherLogic != null)
            {
                if (_switcherLogic.bIsPhoenixSetupOngoing)
                {
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0023", "Cannot select a new machine when ControllerBox is still initializing.");
                    return false;
                }
                else if (!_switcherLogic.HasEspConnection())
                {
                    await _switcherLogic.RetryInit();
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0024", "Cannot select a new machine when ControllerBox is not connected.");
                    return false;
                }
                if (_switcherLogic.bIsUpdatingBundles)
                {
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0028", "Cannot select a machine when bundle update is ongoing.");
                    return false;
                }
            }
            if (settings.bShouldSelectPCMForAll)
            {
                if (PhoenixSwitcherLogic.NumConnectedEspControllers < Helpers.GetNumActiveEspController())
                {
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0026", "Cannot select a new machine in multiselect mode when not all ControllerBoxes are ready.");
                    return false;
                }
                else if (PhoenixSwitcherLogic.NumActiveSetups > 0)
                {
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_04_0027", "Cannot select a machine in multiselect mode when a setup is still ongoing.");
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
                var targetItem = _viewModel.ListViewItems.FirstOrDefault(i => (i.Tag as XmlMachinePCM)?.N17 == machine.N17);
                if (targetItem != null)
                {
                    MachineListBox.SelectedItem = targetItem;
                    MachineListBox.ScrollIntoView(targetItem);
                }
            }
        }


    }
}