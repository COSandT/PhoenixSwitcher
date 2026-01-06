using System.Windows;
using System.Windows.Controls;

using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Settings;

using PhoenixSwitcher.ViewModels;
using PhoenixSwitcher.Delegates;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for InstructionBar.xaml
    /// </summary>
    public partial class StatusBar : UserControl
    {
        private Dictionary<StatusLevel, Status> _statusList = new Dictionary<StatusLevel, Status>();
        private StatusBarViewModel _viewModel = new StatusBarViewModel();
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

            StatusDelegates.UpdateStatus(StatusLevel.Main, "", "TODO: add instructions");
        }

        public void UpdateStatus(StatusLevel level, string locaStatusId, string fallbackStatusText)
        {
            _logger?.LogInfo($"StatusInstructionBar::UpdateNewStatus -> Recieved new status:");
            Status status = new Status(locaStatusId, fallbackStatusText);
            if (!_statusList.TryAdd(level, status))
            {
                if (!_statusList.ContainsKey(level)) return; // failed to add.
                // Update the text
                _statusList[level] = status;
            }
            // Remove the lower status levels as they are from a previous status.
            switch (level)
            {
                case StatusLevel.Main:
                    _statusList.Remove(StatusLevel.L1);
                    goto case StatusLevel.L1;
                case StatusLevel.L1:
                    _statusList.Remove(StatusLevel.L2);
                    break;
            }
            UpdateStatusText();
        }
        public void UpdateStatusPercentage(StatusLevel level, int newPercentage)
        {
            _logger?.LogWarning($"StatusInstructionBar::UpdateStatusPercentage -> Updating status percentage for specified sstatus level.");
            int clampedPercentage = Math.Clamp(newPercentage, 0, 100);
            switch (level)
            {
                case StatusLevel.Main:
                    _viewModel.MainStatusPercentage = clampedPercentage;
                    break;
                case StatusLevel.L1:
                    _viewModel.L1StatusPercentage = clampedPercentage;
                    break;
                case StatusLevel.L2:
                    _viewModel.L2StatusPercentage = clampedPercentage;
                    break;
            }
        }
        public void ClearStatus(StatusLevel level)
        {
            // When we clear a status we also want to clear all the status messages of lower level.
            _logger?.LogWarning($"StatusInstructionBar::ClearStatus -> Clear status message of specified level and lower levels.");
            switch (level)
            {
                case StatusLevel.Main:
                    _viewModel.MainStatusPercentage = 0;
                    _statusList.Remove(StatusLevel.Main);
                    goto case StatusLevel.L1;
                case StatusLevel.L1:
                    _viewModel.L1StatusPercentage = 0;
                    _statusList.Remove(StatusLevel.L1);
                    _viewModel.L1StatusVisibility = Visibility.Collapsed;
                    goto case StatusLevel.L2;
                case StatusLevel.L2:
                    _viewModel.L2StatusPercentage = 0;
                    _statusList.Remove(StatusLevel.L2);
                    _viewModel.L2StatusVisibility = Visibility.Collapsed;
                    break;
            }
        }

        private void UpdateStatusText()
        {
            _logger?.LogInfo($"StatusInstructionBar::UpdateStatusText -> Updating Status text for all status levels");
            Status status;
            if (!_statusList.TryGetValue(StatusLevel.Main, out status))
            {
                _viewModel.MainStatusText = "";
                _viewModel.L1StatusText = "";
                _viewModel.L2StatusText = "";
                return;
            }
            _viewModel.MainStatusText = Helpers.TryGetLocalizedText(status.LocalizedTextID, status.FallbackText);


            if (!_statusList.TryGetValue(StatusLevel.L1, out status))
            {
                _viewModel.L1StatusText = "";
                _viewModel.L2StatusText = "";
                return;
            }
            _viewModel.L1StatusText = Helpers.TryGetLocalizedText(status.LocalizedTextID, status.FallbackText);


            if (!_statusList.TryGetValue(StatusLevel.L2, out status))
            {
                _viewModel.L2StatusText = "";
                return;
            }
            _viewModel.L2StatusText = Helpers.TryGetLocalizedText(status.LocalizedTextID, status.FallbackText);
        }
    }
}
