using System.Windows.Media;
using System.Windows.Controls;

using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools.Logging;

using PhoenixSwitcher.Phoenix;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{
	public enum StatusLevel
	{
		Instruction,
		Status,
		Error
	}

	public partial class StatusBar : UserControl
	{
		private readonly StatusBarViewModel _viewModel = new();
		private readonly LogManager? _logManager;

		private PhoenixSwitcherLogic? _switcherLogic;

		private Status _status = Status.Default;

		private string _boxText = string.Empty;

		private bool _isSubscribed;
		private bool _isDisposed;

		public readonly record struct Status(string LocalizedTextId, string FallbackText)
		{
			public static Status Default => new(string.Empty, string.Empty);
			public bool IsDefault => string.IsNullOrEmpty(LocalizedTextId) && string.IsNullOrEmpty(FallbackText);
		}
		public StatusBar()
		{
			InitializeComponent();
			DataContext = _viewModel;
			_logManager = LogManager.GetInstance();
		}

		// Init
		public void Init(PhoenixSwitcherLogic? switcherLogic)
		{
			if (_isDisposed) return;
			if (ReferenceEquals(_switcherLogic, switcherLogic))
			{
				SubscribeToEvents();
				return;
			}

			UnsubscribeFromEvents();
			_switcherLogic = switcherLogic;

			string? boxName = _switcherLogic?.EspInfo?.BoxName;
			_boxText = string.IsNullOrWhiteSpace(boxName) ? string.Empty : $"Box: {boxName}\t";

			SubscribeToEvents();
			Log(LogLevel.Info, "Init -> StatusBar initialized.");
		}
		
		// Event Subscribtion
		private void SubscribeToEvents()
		{
			if (_isSubscribed || _switcherLogic == null) return;
			StatusDelegates.OnLocaStatusTextUpdated += OnStatusChanged;
			StatusDelegates.OnStatusTextUpdated += OnStatusChanged;
			StatusDelegates.OnStatusCleared += OnStatusCleared;

			LocalizationManager.GetInstance().OnActiveLanguageChanged += UpdateStatusText;
			_isSubscribed = true;
		}
		private void UnsubscribeFromEvents()
		{
			if (!_isSubscribed) return;
			if (_switcherLogic != null)
			{
				StatusDelegates.OnLocaStatusTextUpdated += OnStatusChanged;
				StatusDelegates.OnStatusTextUpdated += OnStatusChanged;
				StatusDelegates.OnStatusCleared += OnStatusCleared;
			}
			LocalizationManager.GetInstance().OnActiveLanguageChanged -= UpdateStatusText;
			_isSubscribed = false;
		}

		// Status Events
		private void OnStatusChanged(PhoenixSwitcherLogic? switcher, StatusLevel level, string localizedTextId, string fallbackText)
		{
			if (_isDisposed || switcher != _switcherLogic) return;

			_status = new Status(localizedTextId ?? string.Empty, fallbackText ?? string.Empty);
			ApplyStatusLevel(level);
			Log(LogLevel.Info, $"OnStatusChanged -> Received status: {_status.FallbackText}");
			UpdateStatusText();
		}
		private void OnStatusChanged(PhoenixSwitcherLogic? switcher, StatusLevel level, string text)
		{
			if (_isDisposed || switcher != _switcherLogic) return;

			_status = new Status(string.Empty, text ?? string.Empty);
			ApplyStatusLevel(level);
			Log(LogLevel.Info, $"OnStatusChanged -> Received status: {_status.FallbackText}");
			_viewModel.MainStatusText = _status.FallbackText;
		}
		private void OnStatusCleared(PhoenixSwitcherLogic? switcher, StatusLevel level)
		{
			if (_isDisposed || switcher != _switcherLogic) return;
			
			Log(LogLevel.Info, $"OnStatusCleared -> Clearing status level: {level}.");
			_status = Status.Default;
			_viewModel.MainStatusPercentage = 0;
			_viewModel.MainStatusText = string.Empty;
		}
		private void ApplyStatusLevel(StatusLevel level)
		{
			switch (level)
			{
				case StatusLevel.Instruction:
					_viewModel.StatusColor = Brushes.DeepSkyBlue;
					break;
				case StatusLevel.Error:
					_viewModel.StatusColor = Brushes.Orange;
					break;
				case StatusLevel.Status:
					_viewModel.StatusColor = Brushes.Gray;
					break;
				default:
					_viewModel.StatusColor = Brushes.Gray;
					break;
			}
		}

		private void UpdateStatusText()
		{
			if (_isDisposed) return;

			_viewModel.MainStatusText = Helpers.TryGetLocalizedText(_status.LocalizedTextId, _status.FallbackText);
		}

		// Other
		public void Dispose()
		{
			if (_isDisposed) return;

			_isDisposed = true;
			UnsubscribeFromEvents();
			_switcherLogic = null;
		}
		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"{_boxText}StatusBar::{message}");
		}
	}
}