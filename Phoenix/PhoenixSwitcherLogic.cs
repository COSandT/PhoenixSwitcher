using System.IO;
using System.Windows;
using System.Windows.Input;

using CosntCommonLibrary.Tools.Usb;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.Windows;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ControlTemplates;

namespace PhoenixSwitcher.Phoenix
{

    public class PhoenixSwitcherLogic : IDisposable
	{
		private readonly PhoenixDriveSwitcher _driveSwitcher;
		private readonly PhoenixEspController _espController;
        private readonly LogManager? _logManager;

		private const string PhoenixFileName = "GHMIFiles";
		private const string BundleDirectoryPrefix = "PCMBUNDLE_";
		private const string ProjectSettingsPath = @"C:\COSnT\PhoenixUpdater\Settings\ProjectSettings.xml";

		private bool _wasInitialized;
		private bool _disposed;

		private string Drive => _driveSwitcher.Drive;
		private string PhoenixFilePath => string.IsNullOrEmpty(Drive) ? string.Empty : Path.Combine(Drive, PhoenixFileName);


        public EspControllerInfo EspInfo { get; }
		public string BoxText => $"Box: {EspInfo.BoxName}\t";
		public static int ConnectedEspControllers = 0;
		public static int OngoingBundleUpdates = 0;
		public static int ActiveSetups = 0;

		public bool IsPhoenixSetupOngoing { get; private set; }
		public bool IsUpdatingBundles { get; private set; }
		public bool IsInitializingEsp { get; private set; }

		public static event Action<PhoenixSwitcherLogic, bool>? OnFinishedEspSetup;
		public static event Action<PhoenixSwitcherLogic>? OnProcessStarted;
		public static event Action<PhoenixSwitcherLogic>? OnProcessFinished;
		public static event Action<PhoenixSwitcherLogic>? OnProcessCancelled;
		public static event Action<PhoenixSwitcherLogic>? OnBundleUpdateStarted;
		public static event Action<PhoenixSwitcherLogic>? OnBundleUpdateFinished;

        public PhoenixSwitcherLogic(EspControllerInfo controllerInfo)
        {
            EspInfo = controllerInfo;
            _logManager = LogManager.GetInstance();
			Log(LogLevel.Info, "Constructor -> Start.");

            _espController = new PhoenixEspController(EspInfo, _logManager);
			_driveSwitcher = new PhoenixDriveSwitcher(_espController, new UsbTool(), EspInfo, _logManager);

            RegisterEvents();
            Log(LogLevel.Info, "Constructor -> End.");
		}
        private void RegisterEvents()
		{
			MachineInfoWindow.OnShutOffPower += TurnOffProcess;
			MachineInfoWindow.OnStartBundleProcess += StartProcess;
			MachineInfoWindow.OnProcessFinished += FinishProcess;
			MachineInfoWindow.OnTest += TestProcess;
		}
		private void UnregisterEvents()
		{
			MachineInfoWindow.OnShutOffPower -= TurnOffProcess;
			MachineInfoWindow.OnStartBundleProcess -= StartProcess;
			MachineInfoWindow.OnProcessFinished -= FinishProcess;
			MachineInfoWindow.OnTest -= TestProcess;
		}


		// **************
		// Initialization
		public async Task InitAsync()
		{
			Log(LogLevel.Info, "Init -> Initializing switcher logic.");
			await InitializeInternalAsync();
		}
		public async Task RetryInitAsync()
		{
			Log(LogLevel.Info, "RetryInit -> Reinitializing switcher logic.");

			Disconnect();
			await InitializeInternalAsync();
		}
		private async Task InitializeInternalAsync()
		{
			if (IsInitializingEsp)
			{
				Log(LogLevel.Info, "InitializeInternal -> Initialization already in progress.");
				return;
			}

			IsInitializingEsp = true;
			try
			{
				StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0024", "Attempting to connect to ControllerBox");
				Log(LogLevel.Info, "InitializeInternal -> Attempting to connect to ControllerBox");
				if (!await SetupEspControllerAsync()) throw new InvalidOperationException("Failed to setup ESP Controller. See logs for details.");

				await Task.Delay(500);
				CleanupDrive();

				if (!_wasInitialized)
				{
					_wasInitialized = true;
					ConnectedEspControllers++;
				}

				OnFinishedEspSetup?.Invoke(this, true);
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"InitializeInternal -> {ex.Message}");
				_logManager?.LogEntireException(ex);

				Helpers.ShowOkMessageBox(Application.Current.MainWindow, ex.Message);
				OnFinishedEspSetup?.Invoke(this, false);
			}
			finally
			{
				IsInitializingEsp = false;
			}
		}
		private async Task<bool> SetupEspControllerAsync()
		{
			Log(LogLevel.Info, "SetupEspController -> Starting ESP32 controller setup.");
			if (!await _espController.ConnectAsync())
			{
				Log(LogLevel.Error, "SetupEspController -> Unable to connect to ESP controller.");
				StatusDelegates.UpdateStatus(this, StatusLevel.Error, "ID_02_0022", $"Missing USB connection to the box with name: {EspInfo.BoxName}");
				return false;
			}

			Log(LogLevel.Info, "SetupEspController -> ESP32 controller connected.");
			if (!await _driveSwitcher.ConnectToPcAsync())
			{
				Log(LogLevel.Error, "SetupEspController -> Unable to connect drive to PC.");
				return false;
			}

			return true;
		}
		public bool HasEspConnection()
        {
            bool connected = _espController.IsConnected;
            Log(LogLevel.Info, $"HasEspConnection -> {connected}");
            if (!connected && _wasInitialized)
            {
                ConnectedEspControllers = int.Max(0, ConnectedEspControllers - 1);
                _wasInitialized = false;
            }

            return connected;
        }
		public void Disconnect()
        {
			if (!HasEspConnection()) return;

			Log(LogLevel.Info, $"Disconnect -> Disconnecting ESP controller.");
			_espController.Disconnect();
            if (_wasInitialized)
            {
                ConnectedEspControllers--;
                _wasInitialized = false;
            }
		}


		// *************
		// Bundle Update
		public void UpdateBundleFilesOnDrive()
		{
			Log(LogLevel.Info, $"UpdateBundleFiles -> Started updating bundle files.");
			if (IsPhoenixSetupOngoing)
			{
				Log(LogLevel.Info, $"UpdateBundleFiles -> Phoenix setup is ongoing. Ignoring request.");
				return;
			}
			Application.Current.Dispatcher.Invoke((Action)async delegate{ await UpdateBundleFilesAsync(); });
		}
		private async Task UpdateBundleFilesAsync()
		{
			UpdateWindow? updatingWindow = null;
			bool updateStarted = false;

			try
			{
				OnBundleUpdateStarted?.Invoke(this);

				OngoingBundleUpdates++;
				IsUpdatingBundles = true;
				updateStarted = true;

				Mouse.OverrideCursor = Cursors.Wait;
				updatingWindow = new UpdateWindow{ Topmost = true };
				updatingWindow.Show();
				await Task.Run(UpdateBundleFilesInternal);
				Log(LogLevel.Info, "UpdateBundleFilesOnDrive -> Finished updating bundle files.");
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"UpdateBundleFilesOnDrive -> Exception occurred: {ex.Message}");
				_logManager?.LogEntireException(ex);
				Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0015", "Failed to update the bundles. Look at logs for what went wrong.");
			}
			finally
			{
				updatingWindow?.Close();
				Mouse.OverrideCursor = null;
				if (updateStarted)
				{
					OngoingBundleUpdates = int.Max(0, OngoingBundleUpdates - 1);
					IsUpdatingBundles = false;
				}
				OnBundleUpdateFinished?.Invoke(this);
			}
		}
		private async Task UpdateBundleFilesInternal()
		{
			if (!HasEspConnection())
			{
				Application.Current.Dispatcher.Invoke((Action)delegate { Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0025", "PC needs to be connected to the ControllerBox to update the bundles."); });
				OnFinishedEspSetup?.Invoke(this, false);
				return;
			}

			await EnsureDriveConnectedAsync();
			RenameGMHIFileToBundleFile();

			XmlProjectSettings settings = Helpers.GetProjectSettings();
			string bundleSourceDirectory = settings.BundleFilesDirectory;
			ValidateBundleDirectories(bundleSourceDirectory);

			Log(LogLevel.Info, "UpdateBundleFilesInternal -> Checking which bundles need an update.");
			string[] bundleFoldersOnDrive = Directory.GetDirectories(Drive);
			string[] bundleFoldersOnPc = Directory.GetDirectories(bundleSourceDirectory);

			HashSet<string?> pcBundleNames = GetDirectoryNames(bundleFoldersOnPc);
			HashSet<string?> driveBundleNames = GetDirectoryNames(bundleFoldersOnDrive);

			List<string> bundlesToDelete = bundleFoldersOnDrive.Where(path => !pcBundleNames.Contains(Path.GetFileName(path))).ToList();
			List<string> bundlesToCopy = bundleFoldersOnPc.Where(path => !driveBundleNames.Contains(Path.GetFileName(path))).ToList();

			Log(LogLevel.Info, $"UpdateBundleFilesInternal -> Found " + $"{bundlesToDelete.Count} bundle(s) to delete and " + $"{bundlesToCopy.Count} bundle(s) to copy.");

			DeleteBundles(bundlesToDelete);

			CopyBundles(bundlesToCopy);

			settings.LastBundleUpdateDate = DateTime.Now;
			settings.TrySave(ProjectSettingsPath);

			Log(LogLevel.Info, "UpdateBundleFilesInternal -> Bundle update completed.");
		}
		private async Task EnsureDriveConnectedAsync()
		{
			if (Directory.Exists(Drive)) return;
			Log(LogLevel.Info, $"EnsureDriveConnected -> Drive '{Drive}' is not currently available.");
			if (!await _driveSwitcher.ConnectToPcAsync()) throw new IOException($"Failed to connect drive '{EspInfo.DriveName}'.");
		}
		private void ValidateBundleDirectories(string bundleSourceDirectory)
		{
			if (!Directory.Exists(bundleSourceDirectory)) throw new DirectoryNotFoundException($"Bundle files directory does not exist: {bundleSourceDirectory}");
			if (!Directory.Exists(Drive)) throw new DirectoryNotFoundException($"Drive could not be found: {Drive}");
		}
		private static HashSet<string?> GetDirectoryNames(IEnumerable<string> directories)
		{
			return directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
		}
		private void DeleteBundles(IEnumerable<string> bundleFolders)
		{
			Log(LogLevel.Info, "UpdateBundleFilesInternal -> Attempting to delete old bundles from drive.");
			foreach (string bundleFolder in bundleFolders)
			{
				string bundleName = Path.GetFileName(bundleFolder);
				Log(LogLevel.Info, $"UpdateBundleFilesInternal -> Deleting bundle: {bundleName}");
				Directory.Delete(bundleFolder, recursive: true);
			}
		}
		private void CopyBundles(IEnumerable<string> sourceBundleFolders)
		{
			Log(LogLevel.Info, "UpdateBundleFilesInternal -> Attempting to copy new bundles to drive.");
			foreach (string sourceBundleFolder in sourceBundleFolders)
			{
				string bundleName = Path.GetFileName(sourceBundleFolder);
				string targetBundleFolder = Path.Combine(Drive, bundleName);

				Log(LogLevel.Info, $"UpdateBundleFilesInternal -> Copying bundle: {bundleName}");
				CopyDirectory(sourceBundleFolder, targetBundleFolder);
			}
		}
		private static void CopyDirectory(string sourceDirectory, string targetDirectory)
		{
			Directory.CreateDirectory(targetDirectory);
			foreach (string sourceDirectoryPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				string relativeDirectoryPath = Path.GetRelativePath(sourceDirectory, sourceDirectoryPath);
				string targetDirectoryPath = Path.Combine(targetDirectory, relativeDirectoryPath);
				Directory.CreateDirectory(targetDirectoryPath);
			}

			foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				string relativeFilePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
				string targetFilePath = Path.Combine(targetDirectory, relativeFilePath);
				File.Copy(sourceFilePath, targetFilePath, overwrite: true);
			}
		}


		// ***************
		// Phoenix Process
		private async void StartProcess(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? machine)
		{
			if (switcherLogic != this || IsPhoenixSetupOngoing) return;
			BeginPhoenixSetup();

			try
			{
				Mouse.OverrideCursor = Cursors.Wait;

				if (!HasEspConnection())
				{
					CancelProcess("EspController connection has not been established yet. " + "Wait or retry connecting.");
					StatusDelegates.UpdateStatus(this, StatusLevel.Error, "ID_02_0023", "EspController connection has not been established yet. Wait or retry connecting.");
					return;
				}

				if (machine is null)
				{
					CancelProcess("Selected a machine with invalid data.");
					Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0001", "Invalid machine selected.");
					return;
				}

				if (IsUpdatingBundles)
				{
					CancelProcess("Bundles are being updated. Please wait.");
					Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0017", "Bundles are being updated please wait.");
					return;
				}

				StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0006", "Process started setting up everything to setup 'Phoenix screen'");
				Log(LogLevel.Info, "StartProcess -> Starting Phoenix process for selected bundle.");

				// Restore existing Phoenix directory to original bundle name if needed
				if (Directory.Exists(PhoenixFilePath)) RenameGMHIFileToBundleFile();

				StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0019", "Renaming selected 'Bundle file' to 'GHMIFile'");
				if (!RenameBundleFileToGMHIFile(machine.Bundle_version))
				{
					CancelProcess( "Failed to setup Phoenix file from selected bundle.");
					Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0003", "Failed to find matching bundle files for selected vehicle. Try updating bundle files.");
					return;
				}

				XmlProjectSettings settings = Helpers.GetProjectSettings();

				bool driveSwitchSucceeded;
				if (settings.ShouldSwitchDriveBeforePower) driveSwitchSucceeded = await SwitchDriveBeforePowerAsync(settings);
				else driveSwitchSucceeded = await SwitchDriveAfterPowerAsync(settings);

				if (!driveSwitchSucceeded)
				{
					CancelProcess("Failed to switch drive.");
					return;
				}

				Log(LogLevel.Info, "StartProcess -> Drive switched successfully. " + "Invoking process started event.");
				OnProcessStarted?.Invoke(this);
				StatusDelegates.UpdateStatus(this, StatusLevel.Instruction, "ID_02_0007", "Complete setup on 'Phoenix Screen' and press 'Power off' once done.");
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"StartProcess -> Exception occurred: {ex.Message}");
				_logManager?.LogEntireException(ex);
				EndPhoenixSetup();
				Helpers.ShowOkMessageBox(Application.Current.MainWindow, ex.Message);
			}
			finally
			{
				Mouse.OverrideCursor = null;
			}
		}
		private void BeginPhoenixSetup()
		{
			IsPhoenixSetupOngoing = true;
			ActiveSetups++;
		}
		private void EndPhoenixSetup()
		{
			if (!IsPhoenixSetupOngoing) return;
			IsPhoenixSetupOngoing = false;
			ActiveSetups = int.Max(0, ActiveSetups - 1);
		}
		private void CancelProcess(string reason)
		{
			Log(LogLevel.Warn, $"Process cancelled -> {reason}");
			EndPhoenixSetup();
			OnProcessCancelled?.Invoke(this);
		}
		private async Task<bool> SwitchDriveBeforePowerAsync(XmlProjectSettings settings)
		{
			StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0021", "Switching drive.");
			if (!await SwitchDriveAsync()) return false;

			await Task.Delay(TimeSpan.FromSeconds(settings.DriveSwitchWaitTimeSec));
			StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0018", "Switching power to Phoenix PCM");
			SwitchPowerToPhoenix(true);
			return true;
		}
		private async Task<bool> SwitchDriveAfterPowerAsync(XmlProjectSettings settings)
		{
			StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0018", "Switching power to Phoenix PCM");
			SwitchPowerToPhoenix(true);
			Log(LogLevel.Info, "SwitchDriveAfterPower -> Waiting until Phoenix has started before switching drive.");

			await Task.Delay(TimeSpan.FromSeconds(settings.DriveSwitchWaitTimeSec));

			StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0021", "Switching drive.");
			return await SwitchDriveAsync();
		}
		private async Task<bool> SwitchDriveAsync()
		{
			Log(LogLevel.Info, "SwitchDrive -> Attempting to safely eject drive before switching.");
			_driveSwitcher.TryEjectDrive();
			if (await _driveSwitcher.SwitchConnectionAsync())
			{
				Log(LogLevel.Info, "SwitchDrive -> Drive switched successfully.");
				return true;
			}
			Log(LogLevel.Error, "SwitchDrive -> Failed to switch drive.");
			return false;
		}
		
		private void TurnOffProcess(PhoenixSwitcherLogic? switcherLogic)
		{
			if (switcherLogic != this) return;

			StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0018", "Switching off power to Phoenix PCM");
			SwitchPowerToPhoenix(false);
		}
		
		private void TestProcess(PhoenixSwitcherLogic? switcherLogic)
		{
			if (switcherLogic != this) return;
			
			Log(LogLevel.Error, "TestProcess -> Switching power to Phoenix PCM.");
			SwitchPowerToPhoenix(true);
		}
		
		private async void FinishProcess(PhoenixSwitcherLogic? switcherLogic)
		{
			if (switcherLogic != this) return;
			EndPhoenixSetup();

			StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0008", "Process finished, resetting to start");
			Log(LogLevel.Info, "FinishProcess -> Phoenix process has finished. Resetting state back to start.");
			Mouse.OverrideCursor = Cursors.Wait;
			try
			{
				if (await _driveSwitcher.SwitchToPcAsync())
				{
					Log(LogLevel.Info, "FinishProcess -> Attempting to cleanup drive.");
					CleanupDrive();
				}
				else
				{
					Log(LogLevel.Warn, "FinishProcess -> Failed to switch drive back to PC.");
				}
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"FinishProcess -> Exception occurred: {ex.Message}");
				_logManager?.LogEntireException(ex);
			}
			finally
			{
				Log(LogLevel.Info, "FinishProcess -> Calling process finished event.");
				OnProcessFinished?.Invoke(this);
				Mouse.OverrideCursor = null;
			}
		}

		// ***********
		// ESP Helper
		private void SwitchPowerToPhoenix(bool state)
		{
			Log(LogLevel.Info, $"SwitchPowerToPhoenix -> Switching Phoenix power to {state}.");
			if (!HasEspConnection()) return;

			if (!_espController.SetRelay2(state))
			{
				Log(LogLevel.Error, $"SwitchPowerToPhoenix -> Failed to switch Phoenix power.");
			}
		}


		// *************
		// Drive Cleanup
		private void CleanupDrive()
		{
			try
			{
				Log(LogLevel.Info, "CleanupDrive -> Cleaning up drive for next use.");
				RenameGMHIFileToBundleFile();
				if (!Directory.Exists(Drive))
				{
					Log(LogLevel.Warn, "CleanupDrive -> Drive no longer exists.");
					return;
				}

				Log(LogLevel.Info, "CleanupDrive -> Removing files generated by Phoenix screen.");
				foreach (string folder in Directory.GetDirectories(Drive))
				{
					// Cleanup Drive folders
					string folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
					if (ShouldKeepDriveFolder(folderName)) return;
					try
					{
						Directory.Delete(folder, recursive: true);
						Log(LogLevel.Info, $"CleanupDrive -> Deleted folder: {folder}");
					}
					catch (IOException ex)
					{
						Log(LogLevel.Warn, $"CleanupDrive -> Skipped folder '{folder}': {ex.Message}");
					}
					catch (UnauthorizedAccessException ex)
					{
						Log(LogLevel.Warn, $"CleanupDrive -> Access denied for folder '{folder}': {ex.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, "CleanupDrive -> Failed to cleanup drive properly.");
				_logManager?.LogEntireException(ex);
				Helpers.ShowOkMessageBox(Application.Current.MainWindow, ex.Message);
			}
		}
		private static bool ShouldKeepDriveFolder(string folderName)
		{
			if (folderName.StartsWith(BundleDirectoryPrefix, StringComparison.OrdinalIgnoreCase)) return true; 
			if (string.Equals(folderName, PhoenixFileName, StringComparison.OrdinalIgnoreCase)) return true;

            // Windows/system folders that should not be touched.
			return string.Equals(folderName, "System Volume Information", StringComparison.OrdinalIgnoreCase)
				   || string.Equals(folderName, "WPSettings.dat", StringComparison.OrdinalIgnoreCase)
				   || string.Equals(folderName, "IndexerVolumeGuid", StringComparison.OrdinalIgnoreCase);
		}
		private void RenameGMHIFileToBundleFile()
		{
			Log(LogLevel.Info, "RenameGMHIFileToBundleFile -> Resetting potential Phoenix file back to its bundle file name.");
			if (!Directory.Exists(Drive))
			{
				Log(LogLevel.Error, $"RenameGMHIFileToBundleFile -> Failed to find drive: {Drive}, driveName: {EspInfo.DriveName}");
				throw new IOException(Helpers.TryGetLocalizedText("ID_02_0004", "Could not find drive with DriveName: ") + EspInfo.DriveName);
			}

			if (!Directory.Exists(PhoenixFilePath))
			{
				Log(LogLevel.Info, "RenameGMHIFileToBundleFile -> No Phoenix file found.");
				return;
			}

			string? bundleManifest = Directory.GetFiles(PhoenixFilePath, "*BundleManifest*", SearchOption.TopDirectoryOnly).FirstOrDefault();
			if (bundleManifest is null)
			{
				Log(LogLevel.Warn, "RenameGMHIFileToBundleFile -> Could not find BundleManifest file.");
				return;
			}

			Log(LogLevel.Info, "RenameGMHIFileToBundleFile -> Found bundle manifest file.");
			string fileName = Path.GetFileNameWithoutExtension(bundleManifest);
			int separatorIndex = fileName.LastIndexOf('_');
			if (separatorIndex < 0 || separatorIndex == fileName.Length - 1)
			{
				Log(LogLevel.Warn, $"RenameGMHIFileToBundleFile -> Could not determine bundle version from '{fileName}'.");
				return;
			}

			string bundleVersion =fileName.Substring(separatorIndex + 1);
			bundleVersion = Helpers.RemoveExtraZeroFromVersionName(bundleVersion);
			string targetPath =Path.Combine(Drive, BundleDirectoryPrefix + bundleVersion);
			try
			{
				Log(LogLevel.Info, $"RenameGMHIFileToBundleFile -> Renaming Phoenix directory to '{targetPath}'.");
				Directory.Move(PhoenixFilePath, targetPath);
			}
			catch (IOException ex)
			{
				Log(LogLevel.Error, $"RenameGMHIFileToBundleFile -> Failed to rename " + $"'{PhoenixFilePath}' to '{targetPath}': {ex.Message}");
				_logManager?.LogEntireException(ex);
				throw;
			}
		}
		private bool RenameBundleFileToGMHIFile(string fileName)
		{
			Log(LogLevel.Info, "RenameBundleFileToGMHIFile -> Trying to change selected bundle filename to Phoenix filename.");
			if (!Directory.Exists(Drive))
			{
				Log(LogLevel.Error, "RenameBundleFileToGMHIFile -> Drive with bundles not found.");
				return false;
			}
			string bundlePath = Path.Combine(Drive, BundleDirectoryPrefix + fileName);
			if (!Directory.Exists(bundlePath))
			{
				Log(LogLevel.Error, $"RenameBundleFileToGMHIFile -> Bundle with name '{fileName}' does not exist.");
				return false;
			}
			if (Directory.Exists(PhoenixFilePath))
			{
				Log(LogLevel.Error, $"RenameBundleFileToGMHIFile -> Phoenix directory already exists: {PhoenixFilePath}");
				return false;
			}

			try
			{
				Directory.Move(bundlePath, PhoenixFilePath);
				Log(LogLevel.Info, $"RenameBundleFileToGMHIFile -> Changed bundle '{fileName}' to Phoenix filename.");
				return true;
			}
			catch (IOException ex)
			{
				Log(LogLevel.Error, $"RenameBundleFileToGMHIFile -> Failed to rename " + $"'{bundlePath}' to '{PhoenixFilePath}': {ex.Message}");
				_logManager?.LogEntireException(ex);
				return false;
			}
		}


		// *****
		// Other
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			UnregisterEvents();

			Disconnect();
			GC.SuppressFinalize(this);
		}

		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"{BoxText}PhoenixSwitcherLogic::{message}");
		}
	}
}
