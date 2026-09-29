using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

namespace PhoenixSwitcher.Phoenix
{
	public class PhoenixEspController
	{
		private const int ConnectRetryCount = 5;
		private const int ConnectRetryDelayMs = 500;

		private readonly Esp32Controller _controller;
		private readonly EspControllerInfo _espInfo;
		private readonly LogManager? _logManager;
		private readonly string _boxText;

		public bool IsConnected => _controller.IsConnected;

		public PhoenixEspController(EspControllerInfo espInfo, LogManager? logManager = null)
		{
			_espInfo = espInfo;
			_logManager = logManager;
			_boxText = $"Box: {_espInfo.BoxName}\t";

			_controller = new Esp32Controller();
		}

		public async Task<bool> ConnectAsync()
		{
			Log(LogLevel.Info, "Connect -> Starting ESP32 controller connection.");

			if (_espInfo.COMPortID > 0)
			{
				Log(LogLevel.Info, $"Connect -> Attempting connection using COM port ID: {_espInfo.COMPortID}");
				for (int attempt = 1; attempt <= ConnectRetryCount; attempt++)
				{
					if (_controller.Connect(_espInfo.COMPortID))
					{
						Log(LogLevel.Info, $"Connect -> Successfully connected on attempt {attempt}.");
						break;
					}

					Log(LogLevel.Warn, $"Connect -> Failed connection attempt {attempt}/{ConnectRetryCount}.");
					if (attempt < ConnectRetryCount) await Task.Delay(ConnectRetryDelayMs);
				}
			}
			else
			{
				Log(LogLevel.Warn, "Connect -> No COM port ID configured. Connecting using EspID.");
				if (!int.TryParse(_espInfo.EspID, out int espId))
				{
					Log(LogLevel.Error, $"Connect -> Invalid EspID: '{_espInfo.EspID}'.");
					return false;
				}
				_controller.Connect(espId);
			}

			if (!IsConnected)
			{
				Log(LogLevel.Error, "Connect -> Unable to connect to ESP32 controller.");
				return false;
			}

			Log(LogLevel.Info, "Connect -> Connection successful. Resetting all relays.");

			if (_controller.SetAllRelays(false) == -1)
			{
				Log(LogLevel.Error, "Connect -> Failed to reset all relays.");
				return false;
			}

			Log(LogLevel.Info, "Connect -> ESP32 controller setup completed.");
			return true;
		}

		public bool SetRelay1(bool state)
		{
			if (!EnsureConnected(nameof(SetRelay1))) return false;

			Log(LogLevel.Info, $"SetRelay1 -> Setting relay 1 to {state}.");
			return _controller.SetRelay1(state) != -1;
		}

		public bool SetRelay2(bool state)
		{
			if (!EnsureConnected(nameof(SetRelay2))) return false;

			Log(LogLevel.Info, $"SetRelay2 -> Setting relay 2 to {state}.");
			return _controller.SetRelay2(state) != -1;
		}

		public bool SetAllRelays(bool state)
		{
			if (!EnsureConnected(nameof(SetAllRelays))) return false;

			Log(LogLevel.Info, $"SetAllRelays -> Setting all relays to {state}.");
			return _controller.SetAllRelays(state) != -1;
		}

		public void Disconnect()
		{
			if (!IsConnected)
			{
				Log(LogLevel.Info, "Disconnect -> ESP32 controller is already disconnected.");
				return;
			}

			Log(LogLevel.Info, "Disconnect -> Disconnecting ESP32 controller.");
			_controller.Disconnect();
		}

		private bool EnsureConnected(string operation)
		{
			if (IsConnected) return true;

			Log(LogLevel.Warn, $"{operation} -> Cannot perform operation because ESP32 controller is disconnected.");
			return false;
		}

		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"{_boxText}PhoenixEspController::{message}");
		}
	}
}
