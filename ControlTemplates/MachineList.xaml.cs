using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;

using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private MachineListViewModel _viewModel = new MachineListViewModel();
        private XmlProductionDataPCM? _pcmMachineList;
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
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();

            UpdatePcmMachineList();

            _logger?.LogInfo($"MachineList::Init -> Finished initializing MachineList.");
        }
        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineList::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.MachineListHeaderText = Helpers.TryGetLocalizedText("ID_03_0001", "MachineList");
            _viewModel.SelectToScanText = Helpers.TryGetLocalizedText("ID_03_0002", "-- Scan --");
        }
        private void OnProcessFinished()
        {
            OnMachineSelected?.Invoke(null);
        }
        public async void UpdatePcmMachineList()
        {
            StatusDelegates.UpdateStatus(StatusLevel.Main, "ID_03_0004", "Updating pcm machine list.");
            _pcmMachineList = await PhoenixRest.GetInstance().GetPCMMachineFile();
            if (_pcmMachineList == null) return;

            List.Children.Clear();
            foreach (XmlMachinePCM pcmMachine in _pcmMachineList.Machines)
            {
                Button machineButton = new Button();
                machineButton.Height = 50;
                machineButton.Click += MachineSelected_Click;
                Viewbox viewBox = new Viewbox();
                TextBlock textBlock = new TextBlock();
                textBlock.Text = pcmMachine.N17;
                viewBox.Child = textBlock;
                machineButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                machineButton.Content = viewBox;
                machineButton.Tag = pcmMachine;
                List.Children.Add(machineButton);
            }
            StatusDelegates.UpdateStatus(StatusLevel.Main, "ID_03_0005", "Finished updating pcm machine list. Please select or scan a machine.");
        }

        private void MachineSelected_Click(object sender, RoutedEventArgs e)
        {
            // Invoke OnMachineSelected delegate to let others know which machine was selected.
            _logger?.LogInfo($"MachineList::MachineSelected_Click -> A machine was selected. Let any listeners know which one.");
            Button button = (Button)sender;
            XmlMachinePCM selectedMachinePCMProductionData = (XmlMachinePCM)button.Tag;
            OnMachineSelected?.Invoke(selectedMachinePCMProductionData);
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
    }
}
