using System.Collections.ObjectModel;
using System.Reflection.PortableExecutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Tools;
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

        private XmlMachinePCM? _selectedMachine = null;
        private XmlProductionDataPCM? _pcmMachineList;
        private Logger? _logger;
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
        public void Init(PhoenixSwitcherLogic switcherLogic, XmlProductionDataPCM? pcmMachineList, Logger logger)
        {
            _logger = logger;
            _switcherLogic = switcherLogic;
            _logger?.LogInfo($"MachineList::Init -> Start initializing MachineList.");

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
            _logger?.LogInfo($"MachineList::Init -> Finished initializing MachineList.");
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
            if (_viewModel.bIsMachineListEnabled) await Internal_UpdateSelectedMachineFromSettings();
        }
        private async void OnProcessStarted(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? selectedMachine)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic!);
        }
        private async void OnProcessCancelled(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
            if (_viewModel.bIsMachineListEnabled) Internal_SelectMachine(_selectedMachine);
        }
        private async void OnProcessFinished(PhoenixSwitcherLogic switcherLogic)
        {
            _viewModel.bIsMachineListEnabled = await Internal_ShouldMachineListBeActive(switcherLogic);
            if (_switcherLogic != switcherLogic) return;
            _selectedMachine = null;
            OnMachineSelected?.Invoke(_switcherLogic, _selectedMachine);
        }
        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineList::OnLanguageChanged -> Updating text to match newly selected language.");
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
        private void OnListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Invoke OnMachineSelected delegate to let others know which machine was selected.
            _logger?.LogInfo($"MachineList::MachineSelected_Click -> A machine was selected. Let any listeners know which one.");
            if (e.AddedItems.Count != 1) return;

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if ((_switcherLogic != null && (_switcherLogic.bIsPhoenixSetupOngoing || _switcherLogic.bIsInitializingEsp))
                || (settings.bShouldSelectPCMForAll && (PhoenixSwitcherLogic.NumActiveSetups > 0 || PhoenixSwitcherLogic.NumConnectedEspControllers < Helpers.GetNumActiveEspController())))
            {
                _logger?.LogInfo($"MachineList::MachineSelected_Click -> Cannot select a new machine when setup is ongoing. will not even bother switching selection");
                //Helpers.ShowLocalizedOkMessageBox("ID_04_0021", "Cannot select a new machine when setup is ongoing.");
                return;
            }

            MachineListItem? item = (MachineListItem?)e.AddedItems[0];
            _selectedMachine = (XmlMachinePCM?)item?.Tag;
            Internal_SelectMachine(_selectedMachine);
        }


        // Internal Helpers
        private async void Internal_UpdateMachineList(XmlProductionDataPCM? pcmMachineList)
        {
            _pcmMachineList = pcmMachineList;
            if (_pcmMachineList != null)
            {
                _viewModel.ListViewItems.Clear();
                _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Filling in listview.");
                foreach (XmlMachinePCM pcmMachine in _pcmMachineList.Machines)
                {
                    MachineListItem item = new MachineListItem();
                    item.Name = pcmMachine.N17;
                    item.Tag = pcmMachine;

                    _viewModel.ListViewItems.Add(item);
                }
            }
            await Internal_UpdateSelectedMachineFromSettings();
        }
        private async Task Internal_UpdateSelectedMachineFromSettings()
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (_switcherLogic != null)
            {
                if (_switcherLogic.bIsInitializingEsp)
                {
                    StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Status, "ID_04_0022", "Initializing ControllerBox.");
                }
                else if (settings.bShouldSelectPCMForAll && PhoenixSwitcherLogic.NumConnectedEspControllers < Helpers.GetNumActiveEspController())
                {
                    StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0029", "Wait until all ControllerBoxes are initialized.");
                }
                else if (_switcherLogic.HasEspConnection())
                {
                    int idx = Helpers.GetEspSettingsIdxInfoFromID(_switcherLogic.EspID);
                    StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
                    if (idx != -1)
                    {
                        await Internal_SelectMachineFromText(settings.EspControllers[idx].LastSelectedMachineN17);
                    }
                }
            }
        }
        private async Task<bool> Internal_SelectMachineFromText(string text)
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

            Internal_SelectMachine(foundMachine);
            Internal_UpdateVisualSelection(foundMachine);

            ScannedMachineText.Clear();
            ScannedMachineText.Focus();
            return true;
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
        private void Internal_SelectMachine(XmlMachinePCM? machine)
        {
            if (_bIsUpdatingSelectedItem) return;
            _bIsUpdatingSelectedItem = true;

            try
            {
                if (!Internal_CanSelectMachine())
                {
                    _logger?.LogInfo($"MachineList::Internal_SelectMachine -> Unable to select machine.");
                    _bIsUpdatingSelectedItem = false;
                    return;
                }

                XmlProjectSettings settings = Helpers.GetProjectSettings();
                OnMachineSelected?.Invoke(settings.bShouldSelectPCMForAll ? null : _switcherLogic, machine);
                Internal_UpdateVisualSelection(machine);
                if (_switcherLogic != null)
                {
                    int idx = Helpers.GetEspSettingsIdxInfoFromID(_switcherLogic.EspID);
                    if (idx != -1)
                    {
                        settings.EspControllers[idx].LastSelectedMachineN17 = machine != null ? machine.N17 : "";
                        settings.TrySave($"{AppContext.BaseDirectory}Settings\\ProjectSettings.xml");
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
        private bool Internal_CanSelectMachine()
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
                    _switcherLogic.RetryInit();
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