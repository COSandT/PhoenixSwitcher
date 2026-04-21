using System.Windows.Media;
using System.Windows.Controls;
using CosntCommonLibrary.Settings;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;
using CosntCommonLibrary.Tools.Logging;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for InstructionBar.xaml
    /// </summary>
    public partial class StatusBar : UserControl
    {
        private StatusBarViewModel _viewModel = new StatusBarViewModel();
        private PhoenixSwitcherLogic? _switcherLogic = null;
        private Status _status = new Status();

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
        public void Init(PhoenixSwitcherLogic? switcherLogic)
        {
            _switcherLogic = switcherLogic;
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tStatusBar::Init -> Initializing StatusInstructionBar.");

            StatusDelegates.OnLocaStatusTextUpdated += UpdateStatus;
            StatusDelegates.OnStatusPercentageUpdated += UpdateStatusPercentage;
            StatusDelegates.OnStatusCleared += ClearStatus;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += UpdateStatusText;
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tStatusBar::Init -> Finished initializing StatusInstructionBar.");
        }

        public void UpdateStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, string locaStatusId, string fallbackStatusText)
        {
            if (_switcherLogic != switcherLogic && switcherLogic != null) return;
            _status = new Status(locaStatusId, fallbackStatusText);
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tStatusBar::UpdateNewStatus -> Recieved new status: {fallbackStatusText}");
            switch (level)
            {
                case StatusLevel.Instruction:
                    _viewModel.StatusColor = Brushes.DeepSkyBlue;
                    break;
                case StatusLevel.Error:
                    _viewModel.StatusColor = Brushes.Orange;
                    break;
                case StatusLevel.Status:
                    default:
                    _viewModel.StatusColor = Brushes.Gray;
                    break;
            }
            UpdateStatusText();
        }
        public void UpdateStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, string text)
        {
            if (_switcherLogic != switcherLogic && switcherLogic != null) return;
            _status = new Status("", text);
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tStatusBar::UpdateNewStatus -> Recieved new status: {text}");
            switch (level)
            {
                case StatusLevel.Instruction:
                    _viewModel.StatusColor = Brushes.DeepSkyBlue;
                    break;
                case StatusLevel.Error:
                    _viewModel.StatusColor = Brushes.Orange;
                    break;
                case StatusLevel.Status:
                default:
                    _viewModel.StatusColor = Brushes.Gray;
                    break;
            }
            _viewModel.MainStatusText = text;
        }
        public void UpdateStatusPercentage(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, int newPercentage)
        {
            if (_switcherLogic != switcherLogic && switcherLogic != null) return;
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tStatusBar::UpdateStatusPercentage -> Updating status percentage for specified sstatus level.");
            int clampedPercentage = Math.Clamp(newPercentage, 0, 100);
            _viewModel.MainStatusPercentage = clampedPercentage;
        }
        public void ClearStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level)
        {
            if (_switcherLogic != switcherLogic && switcherLogic != null) return;
            // When we clear a status we also want to clear all the status messages of lower level.
            LogManager.GetInstance()?.Log(LogLevel.Info, $"Box: {_switcherLogic?.EspInfo.BoxName}\tStatusBar::ClearStatus -> Clear status message of specified level and lower levels.");
            _viewModel.MainStatusPercentage = 0;
            _viewModel.MainStatusText = string.Empty;
        }

        private void UpdateStatusText()
        {
            //_logger?.LogInfo($"StatusInstructionBar::UpdateStatusText -> Updating Status text for all status levels");
            _viewModel.MainStatusText = Helpers.TryGetLocalizedText(_status.LocalizedTextID, _status.FallbackText);
        }
    }
}
