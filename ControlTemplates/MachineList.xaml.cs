using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Xml;
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

            OnMachineSelected?.Invoke(_switcherLogic, null);
        }
        private async void ScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string barcode = ScannedMachineText.Text.Trim();

            if (string.IsNullOrEmpty(barcode)) return;
            if (_pcmMachineList == null) await PhoenixRest.GetInstance().GetPCMMachineFile();
            XmlMachinePCM? foundMachine = _pcmMachineList?.Machines.Find(mach => mach.N17 == barcode || mach.VAN == barcode);

            if (foundMachine == null) return;
            OnMachineSelected?.Invoke(_switcherLogic, foundMachine);

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
            OnMachineSelected?.Invoke(_switcherLogic, _selectedMachine);
        }
    }
}
