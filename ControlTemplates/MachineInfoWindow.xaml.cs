using System.Windows;
using System.Windows.Controls;

using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for MachineInfoWindow.xaml
    /// </summary>
    public partial class MachineInfoWindow : UserControl
    {
        private MachineInfoWindowViewModel _viewModel = new MachineInfoWindowViewModel();
        private BundleSelection? _selectedBundle;
        private Logger? _logger;


        public delegate void StartBundleProcessHandler(BundleSelection? bundleSelection);
        public static event StartBundleProcessHandler? OnStartBundleProcess;

        public delegate void FinishedProcessHandler();
        public static event FinishedProcessHandler? OnProcessFinished;

        public MachineInfoWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
        }
        public void Init(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"MachineInfoWindow::Init -> Start initializing MachineInfoWindow.");

            PhoenixSwitcherLogic.OnProcessStarted += ProcessStarted;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            // Call once to setup initial language.
            OnLanguageChanged();

            MachineList.OnMachineSelected += SetMachineInfoFromBundle;

            _logger?.LogInfo($"MachineInfoWindow::Init -> Finished initializing MachineInfoWindow.");
        }
        private void OnLanguageChanged()
        {
            _logger?.LogInfo($"MachineInfoWindow::OnLanguageChanged -> Updating text to match newly selected language.");
            _viewModel.StartButtonText = Helpers.TryGetLocalizedText("TODO: LOCA", "Start");
            _viewModel.FinishButtonText = Helpers.TryGetLocalizedText("TODO: LOCA", "Finish");
        }

        public void SetMachineInfoFromBundle(BundleSelection bundle)
        {
            _logger?.LogInfo($"MachineInfoWindow::SetMachineInfoFromBundle -> Set selected bundle.");
            _selectedBundle = bundle;
            _viewModel.StartButtonVisibility = Visibility.Visible;
        }

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke start bundle process event.");
            OnStartBundleProcess?.Invoke(_selectedBundle);
        }
        private void FinishProcess_Click(object sender, RoutedEventArgs e)
        {
            _logger?.LogInfo($"MachineInfoWindow::StartProcess_Click -> Invoke finish process event. And update button visibility");
            OnProcessFinished?.Invoke();
            _viewModel.FinishButtonVisibility = Visibility.Hidden;
            _viewModel.StartButtonVisibility = Visibility.Visible;
        }
        private void ProcessStarted()
        {
            _logger?.LogInfo($"MachineInfoWindow::ProcessStarted -> Update button visibility for started process");
            _viewModel.FinishButtonVisibility = Visibility.Visible;
            _viewModel.StartButtonVisibility = Visibility.Hidden;
        }

    }
}
