using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;

using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;
using PhoenixSwitcher.Models;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private readonly MachineListViewModel _viewModel = new MachineListViewModel();

        private XmlProductionDataPCM? _pcmMachineList;
        private XmlMachinePCM? _selectedMachine = null;
        private Logger? _logger;

        public delegate void MachineSelectedHandler(XmlMachinePCM? selectedMachinePCMProductionData);
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
        public void Init(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"MachineList::Init -> Start initializing MachineList.");

            PhoenixSwitcherLogic.OnProcessFinished += OnProcessFinished;
            PhoenixSwitcherLogic.OnProcessCancelled += OnProcessCancelled;
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            UpdatePcmMachineList();
            //ListScrollViewer.ScrollChanged += OnScrollViewerChanged;
            _logger?.LogInfo($"MachineList::Init -> Finished initializing MachineList.");
        }
        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineList::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.MachineListHeaderText = Helpers.TryGetLocalizedText("ID_03_0001", "MachineList");
            _viewModel.SelectToScanText = Helpers.TryGetLocalizedText("ID_03_0002", "-- Scan --");
        }
        private void OnProcessCancelled()
        {
            OnMachineSelected?.Invoke(_selectedMachine);
        }
        private void OnProcessFinished()
        {
            _selectedMachine = null;
            OnMachineSelected?.Invoke(_selectedMachine);
        }
        private void OnScrollViewerChanged(object? sender, EventArgs e)
        {
            //UpdateMachineListButtonScale();
        }
        public async void UpdatePcmMachineList()
        {
            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_03_0004", "Updating pcm machine list, please wait.");
            _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Started updating pcm machine list.");
            await Application.Current.Dispatcher.Invoke(async delegate
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Getting machine file from RestAPI");
                    _pcmMachineList = await Task.Run(() => PhoenixRest.GetInstance().GetPCMMachineFile());
                    if (_pcmMachineList == null) throw new Exception("pcm machine list is null.");

                    _viewModel.ListViewItems.Clear();
                    _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Filling in listview.");
                    foreach (XmlMachinePCM pcmMachine in _pcmMachineList.Machines)
                    {
                        MachineListItem item = new MachineListItem();
                        item.Name = pcmMachine.N17;
                        item.Tag = pcmMachine;

                        _viewModel.ListViewItems.Add(item);
                    }
                    OnMachineSelected?.Invoke(null);
                }
                catch (Exception ex)
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_03_0013", "Failed to update pcm machine list. Look at logs for reason.");
                    _logger?.LogError($"MachineList::UpdatePcmMachineList -> exception occured: {ex.Message}");
                    OnMachineSelected?.Invoke(null);
                }
                Mouse.OverrideCursor = null;
                _logger?.LogInfo($"MachineList::UpdatePcmMachineList -> Finished updating pcm machine list");
            });
        }

        private async void ScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string barcode = ScannedMachineText.Text.Trim();

            if (string.IsNullOrEmpty(barcode)) return;
            if (_pcmMachineList == null) await PhoenixRest.GetInstance().GetPCMMachineFile();
            XmlMachinePCM? foundMachine = _pcmMachineList?.Machines.Find(mach => mach.N17 == barcode || mach.VAN == barcode);

            if (foundMachine == null) return;
            OnMachineSelected?.Invoke(foundMachine);

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
            OnMachineSelected?.Invoke(_selectedMachine);
        }
    }
}
