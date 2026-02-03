using System.Windows.Controls;
using System.Windows.Input;
using CosntCommonLibrary.Settings;
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
            OnMachineSelected?.Invoke(_switcherLogic, _selectedMachine);
        }
        private void OnProcessFinished(PhoenixSwitcherLogic switcherLogic)
        {
            if (_switcherLogic != switcherLogic) return;
            _selectedMachine = null;
            OnMachineSelected?.Invoke(_switcherLogic, _selectedMachine);
        }

        private void UpdateMachineList(XmlProductionDataPCM? pcmMachineList)
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

            SelectMachine_Internal(null);
        }
        private async void ScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string barcode = ScannedMachineText.Text.Trim();

            if (string.IsNullOrEmpty(barcode)) return;
            if (_pcmMachineList == null) await PhoenixRest.GetInstance().GetPCMMachineFile();

            XmlMachinePCM? foundMachine = null;
            if (barcode.Length == 17) foundMachine = _pcmMachineList?.Machines.Find(mach => mach.N17 == barcode);
            else if (barcode.Length == 10) foundMachine = _pcmMachineList?.Machines.Find(mach => mach.VAN == barcode);
            else if (barcode.Length < 10)
            {
                while (barcode.Length < 10)
                {
                    barcode = $"0{barcode}";
                }
                foundMachine = _pcmMachineList?.Machines.Find(mach => mach.VAN == barcode);
            }
            else return;

            SelectMachine_Internal(foundMachine);

            ScannedMachineText.Clear();
            ScannedMachineText.Focus();

            e.Handled = true;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Invoke OnMachineSelected delegate to let others know which machine was selected.
            _logger?.LogInfo($"MachineList::MachineSelected_Click -> A machine was selected. Let any listeners know which one.");
            if (e.AddedItems.Count != 1) return;

            MachineListItem? item = (MachineListItem?)e.AddedItems[0];
            _selectedMachine = (XmlMachinePCM?)item?.Tag;
            SelectMachine_Internal(_selectedMachine);
        }
        private void SelectMachine_Internal(XmlMachinePCM? machine)
        {
            XmlProjectSettings settings = Helpers.GetProjectSettings();
            if (settings.bShouldSelectPCMForAll)
            {
                OnMachineSelected?.Invoke(null, machine);
            }
            else
            {
                OnMachineSelected?.Invoke(_switcherLogic, machine);
            }

            if (machine != null && machine.DT == 1.ToString())
            {
                StatusDelegates.UpdateStatus(_switcherLogic, StatusLevel.Instruction, "ID_04_0015", "Cannot update phoenix software for display type 1. Select new Machine.");
                Helpers.ShowLocalizedOkMessageBox("ID_04_0015", "Cannot update phoenix software for display type 1. Select new Machine.");
            }
        }
    }
}
