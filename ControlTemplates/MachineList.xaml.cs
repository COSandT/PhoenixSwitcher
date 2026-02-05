using System.Reflection.PortableExecutable;
using System.Windows.Controls;
using System.Windows.Input;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.Models;
using PhoenixSwitcher.ViewModels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private readonly MachineListViewModel _viewModel = new MachineListViewModel();
        private PhoenixSwitcherLogic? _switcherLogic = null;

        private XmlMachinePCM? _selectedMachine = null;
        private XmlProductionDataPCM? _pcmMachineList;
        private Logger? _logger;

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

            MainWindow.OnMachineListUpdated += UpdateMachineList;
            PhoenixSwitcherLogic.OnProcessFinished += OnProcessFinished;
            PhoenixSwitcherLogic.OnProcessCancelled += OnProcessCancelled;
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            //ListScrollViewer.ScrollChanged += OnScrollViewerChanged;
            _logger?.LogInfo($"MachineList::Init -> Finished initializing MachineList.");
        }
        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineList::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.MachineListHeaderText = Helpers.TryGetLocalizedText("ID_03_0001", "MachineList");
            _viewModel.SelectToScanText = Helpers.TryGetLocalizedText("ID_03_0002", "-- Scan --");
        }
        private void OnProcessCancelled(PhoenixSwitcherLogic switcherLogic)
        {
            if (_switcherLogic != switcherLogic) return;
            Internal_SelectMachine(_selectedMachine);
        }
        private void OnProcessFinished(PhoenixSwitcherLogic switcherLogic)
        {
            if (_switcherLogic != switcherLogic) return;
            _selectedMachine = null;
            OnMachineSelected?.Invoke(_switcherLogic, _selectedMachine);
        }

        private async void UpdateMachineList(XmlProductionDataPCM? pcmMachineList)
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

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (_switcherLogic != null)
            {
                if (_switcherLogic.HasEspConnection() || (settings.bShouldSelectPCMForAll && PhoenixSwitcherLogic.NumConnectedEspControllers >= settings.EspControllers.Count))
                {
                    if (_switcherLogic != null)
                    {
                        EspControllerInfo? esp = GetEspInfoFromID(_switcherLogic.EspID);
                        if (esp == null || !await Internal_SelectMachineFromText(esp.LastSelectedMachineN17))
                        {
                            StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0011", "Select machine from list or use scanner.");
                        }
                    }
                            
                }
                else if (_switcherLogic.bIsInitializingEsp)
                {
                    StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Status, "ID_04_0022", "Initializing ControllerBox.");
                }
            }
        }
        private async void ScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string barcode = ScannedMachineText.Text.Trim();
            await Internal_SelectMachineFromText(barcode);

            e.Handled = true;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Invoke OnMachineSelected delegate to let others know which machine was selected.
            _logger?.LogInfo($"MachineList::MachineSelected_Click -> A machine was selected. Let any listeners know which one.");
            if (e.AddedItems.Count != 1) return;

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if ((_switcherLogic != null && _switcherLogic.bIsPhoenixSetupOngoing)
                || settings.bShouldSelectPCMForAll && PhoenixSwitcherLogic.NumActiveSetups > 0)
            {
                _logger?.LogInfo($"MachineList::MachineSelected_Click -> Cannot select a new machine when setup is ongoing. will not even bother switching selection");
                Helpers.ShowLocalizedOkMessageBox("ID_04_0021", "Cannot select a new machine when setup is ongoing.");
                return;
            }

            MachineListItem? item = (MachineListItem?)e.AddedItems[0];
            _selectedMachine = (XmlMachinePCM?)item?.Tag;
            Internal_SelectMachine(_selectedMachine);
        }
        private async Task<bool> Internal_SelectMachineFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (_pcmMachineList == null) await PhoenixRest.GetInstance().GetPCMMachineFile();

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

            ScannedMachineText.Clear();
            ScannedMachineText.Focus();
            return true;
        }
        private void Internal_SelectMachine(XmlMachinePCM? machine)
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (_switcherLogic != null)
            {
                if (_switcherLogic.bIsPhoenixSetupOngoing)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_04_0023", "Cannot select a new machine when ControllerBox is still initializing.");
                    return;
                }
                else if (!_switcherLogic.HasEspConnection())
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_04_0024", "Cannot select a new machine when ControllerBox is not connected.");
                    return;
                }
                else if (_switcherLogic.bIsPhoenixSetupOngoing)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_04_0021", "Cannot select a new machine when setup is ongoing.");
                    return;
                }
                if (_switcherLogic.bIsUpdatingBundles)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_02_0028", "Cannot select a machine when bundle update is ongoing.");
                    return;
                }
            }
            if (settings.bShouldSelectPCMForAll)
            {
                if (PhoenixSwitcherLogic.NumConnectedEspControllers < settings.EspControllers.Count)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_04_0026", "Cannot select a new machine in multiselect mode when not all ControllerBoxes are ready.");
                    return;
                }
                else if (PhoenixSwitcherLogic.NumActiveSetups > 0)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_04_0027", "Cannot select a machine in multiselect mode when a setup is still ongoing.");
                    return;
                }
            }

            OnMachineSelected?.Invoke(settings.bShouldSelectPCMForAll ? null : _switcherLogic, machine);
            if (_switcherLogic != null)
            {
                EspControllerInfo? esp = GetEspInfoFromID(_switcherLogic.EspID);
                if (esp != null)
                {
                    esp.LastSelectedMachineN17 = machine != null ? machine.N17 : "";
                    settings.TrySave($"{AppContext.BaseDirectory}//Settings//ProjectSettings.xml");
                }
            }
            if (machine != null && machine.DT == 1.ToString())
            {
                StatusDelegates.UpdateStatus(settings.bShouldSelectPCMForAll ? null : _switcherLogic, StatusLevel.Instruction, "ID_04_0015", "Cannot update phoenix software for display type 1. Select new Machine.");
                Helpers.ShowLocalizedOkMessageBox("ID_04_0015", "Cannot update phoenix software for display type 1. Select new Machine.");
            }

        }
        
        private EspControllerInfo? GetEspInfoFromID(string id)
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            foreach (EspControllerInfo esp in settings.EspControllers)
            {
                if (esp.EspID == id) return esp;
            }
            return null;
        }

    }
}
