using CosntCommonLibrary.Tools.Usb;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

namespace PhoenixSwitcher.Phoenix
{
	public sealed class PhoenixDriveSwitcher
	{
		private const int RelaySwitchTimeMs = 750;
		private const int DriveDetectionWaitMs = 5000;
		private const int MaxSwitchAttempts = 3;

		private readonly PhoenixEspController _espController;
		private readonly EspControllerInfo _espInfo;
		private readonly LogManager? _logManager;
		private readonly UsbTool _usbTool;
		private readonly string _boxText;

		private string _drive = string.Empty;
		public string Drive => _drive;

		public bool IsDriveConnectedToPc => TryFindDrive();

		public PhoenixDriveSwitcher(PhoenixEspController espController, UsbTool usbTool, EspControllerInfo espInfo, LogManager? logManager = null)
		{
			_espController = espController;
			_logManager = logManager;
			_usbTool = usbTool;
			_espInfo = espInfo;
			_boxText = $"Box: {_espInfo.BoxName}\t";
		}

		// Makes sure the Phoenix USB drive is connected to the PC.
		// If it is not currently connected, the hardware relay is switched and the drive is given time to appear.
		public async Task<bool> ConnectToPcAsync()
		{
			Log(LogLevel.Info, "ConnectToPc -> Attempting to connect USB drive to PC.");
			if (!HasEspConnection())
			{
				Log(LogLevel.Warn, "ConnectToPc -> ESP controller is not connected.");
				return false;
			}

			if (TryFindDrive())
			{
				Log(LogLevel.Info, $"ConnectToPc -> Drive already connected at '{_drive}'.");
				return true;
			}

			for (int attempt = 1; attempt <= MaxSwitchAttempts; attempt++)
			{
				Log(LogLevel.Info, $"ConnectToPc -> Switching drive connection. " + $"Attempt {attempt}/{MaxSwitchAttempts}.");
				if (!await SwitchConnectionAsync())
				{
					Log(LogLevel.Warn, $"ConnectToPc -> Drive switch failed on attempt {attempt}.");
					continue;
				}

				Log(LogLevel.Info, $"ConnectToPc -> Waiting {DriveDetectionWaitMs}ms for drive to appear.");
				await Task.Delay(DriveDetectionWaitMs);
				if (TryFindDrive())
				{
					Log(LogLevel.Info, $"ConnectToPc -> Drive successfully connected at '{_drive}'.");

					return true;
				}

				Log(LogLevel.Warn, $"ConnectToPc -> Drive was not detected after attempt {attempt}.");
			}

			Log(LogLevel.Error, "ConnectToPc -> Failed to connect drive to PC.");
			return false;
		}

		// Physically switches the USB connection. This method only performs the relay operation.
		// It does not decide whether the drive should ultimately be connected to the PC.
		public async Task<bool> SwitchConnectionAsync()
		{
			if (!HasEspConnection())
			{
				Log(LogLevel.Error, "SwitchConnection -> Cannot switch drive because ESP controller is disconnected.");
				return false;
			}

			Log(LogLevel.Info, "SwitchConnection -> Activating drive-switch relay.");
			if (!_espController.SetRelay1(true))
			{
				Log(LogLevel.Error, "SwitchConnection -> Failed to activate drive-switch relay.");
				return false;
			}

			try
			{
				Log(LogLevel.Info, $"SwitchConnection -> Waiting {RelaySwitchTimeMs}ms for relay.");
				await Task.Delay(RelaySwitchTimeMs);
			}
			finally
			{
				Log(LogLevel.Info, "SwitchConnection -> Deactivating drive-switch relay.");
				if (!_espController.SetRelay1(false))
				{
					Log(LogLevel.Error, "SwitchConnection -> Failed to deactivate drive-switch relay.");
				}
			}

			Log(LogLevel.Info, "SwitchConnection -> Drive switch completed.");
			return true;
		}

		// Safely ejects the currently connected drive before physically switching it.
		public bool TryEjectDrive()
		{
			if (string.IsNullOrEmpty(_drive))
			{
				Log(LogLevel.Info, "TryEjectDrive -> No drive currently detected.");
				return true;
			}

			Log(LogLevel.Info, $"TryEjectDrive -> Attempting to safely eject '{_drive}'.");
			bool success = UsbEjectTool.SafeRemove(_drive);
			if (!success)
			{
				Log(LogLevel.Warn, "TryEjectDrive -> Failed to safely eject drive. " + "The drive will still be switched.");
			}

			return success;
		}

		// Ejects the drive, switches the hardware connection and waits until the PC sees the drive again.
		public async Task<bool> SwitchToPcAsync()
		{
			if (!HasEspConnection()) return false;
			return await ConnectToPcAsync();
		}

		private bool TryFindDrive()
		{
			Log(LogLevel.Info, $"TryFindDrive -> Looking for drive '{_espInfo.DriveName}'.");

			DriveInfoWrapped driveInfo = _usbTool.GetDrive(_espInfo.DriveName);
			if (driveInfo == null || string.IsNullOrWhiteSpace(driveInfo.DriveLetter))
			{
				_drive = string.Empty;
				Log(LogLevel.Warn, $"TryFindDrive -> Drive '{_espInfo.DriveName}' was not found.");
				return false;
			}

			_drive = driveInfo.DriveLetter;
			Log(LogLevel.Info, $"TryFindDrive -> Found drive at '{_drive}'.");
			return true;
		}

		private bool HasEspConnection()
		{
			return _espController.IsConnected;
		}

		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"{_boxText}PhoenixDriveSwitcher::{message}");
		}
	}
}
