using System.Windows.Media;
using System.Windows.Controls;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;

using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for InstructionBar.xaml
    /// </summary>
    public partial class StatusBar : UserControl
    {
        private StatusBarViewModel _viewModel = new StatusBarViewModel();
        private Status _status = new Status();
        private Logger? _logger;

        // Struct holding all the status info.
        public struct Status
        {
            public Status(string locaTextID, string fallbackText, bool bPersistent = false) 
            {
                LocalizedTextID = locaTextID;
                FallbackText = fallbackText;
                bIsDefault = false;
            }
            public readonly bool bIsDefault = true;
            public string LocalizedTextID = "";
            public string FallbackText = "";
        }


        public StatusBar()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
        }
        public void Init(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"StatusInstructionBar::Init -> Initializing StatusInstructionBar.");

            StatusDelegates.OnStatusTextUpdated += UpdateStatus;
            StatusDelegates.OnStatusPercentageUpdated += UpdateStatusPercentage;
            StatusDelegates.OnStatusCleared += ClearStatus;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += UpdateStatusText;
            _logger?.LogInfo($"StatusInstructionBar::Init -> Finished initializing StatusInstructionBar.");
        }

        public void UpdateStatus(StatusLevel level, string locaStatusId, string fallbackStatusText)
        {
            _logger?.LogInfo($"StatusInstructionBar::UpdateNewStatus -> Recieved new status:");
            _status = new Status(locaStatusId, fallbackStatusText);
            switch (level)
            {
                case StatusLevel.Instruction:
                    _viewModel.StatusColor = Brushes.Green;
                    break;
                case StatusLevel.Error:
                    _viewModel.StatusColor = Brushes.Red;
                    break;
                case StatusLevel.Status:
                    default:
                    _viewModel.StatusColor = Brushes.White;
                    break;
            }
            UpdateStatusText();
        }
        public void UpdateStatusPercentage(StatusLevel level, int newPercentage)
        {
            _logger?.LogWarning($"StatusInstructionBar::UpdateStatusPercentage -> Updating status percentage for specified sstatus level.");
            int clampedPercentage = Math.Clamp(newPercentage, 0, 100);
            _viewModel.MainStatusPercentage = clampedPercentage;
        }
        public void ClearStatus(StatusLevel level)
        {
            // When we clear a status we also want to clear all the status messages of lower level.
            _logger?.LogWarning($"StatusInstructionBar::ClearStatus -> Clear status message of specified level and lower levels.");
            _viewModel.MainStatusPercentage = 0;
            _viewModel.MainStatusText = string.Empty;
        }

        private void UpdateStatusText()
        {
            _logger?.LogInfo($"StatusInstructionBar::UpdateStatusText -> Updating Status text for all status levels");
            _viewModel.MainStatusText = Helpers.TryGetLocalizedText(_status.LocalizedTextID, _status.FallbackText);
        }
    }
}
