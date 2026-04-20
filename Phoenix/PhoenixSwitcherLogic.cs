using System.IO;
using System.Windows;
using System.Windows.Input;

using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.Tools.Usb;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;

using PhoenixSwitcher.Windows;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ControlTemplates;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PhoenixSwitcher
{
    public class PhoenixSwitcherLogic
    {
        private readonly SemaphoreSlim _relaySemaphore = new SemaphoreSlim(1, 1);

        private Esp32Controller _espController;
        private LogManager? _logManager;
        private UsbTool _usbTool;
        private const string _phoenixFileName = "GHMIFiles";
        private string _phoenixFilePath = string.Empty;
        private string _drive = string.Empty;
        private bool _bExecuteDelayedBundleUpdate = false;
        private bool _bWasInitialized = false; 

        public EspControllerInfo EspInfo { get; private set; }

        public static int NumConnectedEspControllers = 0;
        public static int NumOngoingBundleUpdates = 0;
        public static int NumActiveSetups = 0;

        public bool bIsPhoenixSetupOngoing { get; private set; } = false;
        public bool bIsUpdatingBundles { get; private set; } = false;
        public bool bIsInitializingEsp { get; private set; } = false;

        public delegate void ProcessFinishedEspSetup(PhoenixSwitcherLogic switcherLogic, bool bSuccess);
        public static event ProcessFinishedEspSetup? OnFinishedEspSetup;

        public delegate void ProcessStartedHandler(PhoenixSwitcherLogic switcherLogic);
        public static event ProcessStartedHandler? OnProcessStarted;
        public delegate void ProcessFinishedHandler(PhoenixSwitcherLogic switcherLogic);
        public static event ProcessFinishedHandler? OnProcessFinished;
        public delegate void ProcessCancelledHandler(PhoenixSwitcherLogic switcherLogic);
        public static event ProcessCancelledHandler? OnProcessCancelled;

        public delegate void ProcessStartedBundleUpdate(PhoenixSwitcherLogic switcherLogic);
        public static event ProcessStartedBundleUpdate? OnBundleUpdateStarted;
        public delegate void ProcessFinishedBundleUpdate(PhoenixSwitcherLogic switcherLogic);
        public static event ProcessFinishedBundleUpdate? OnBundleUpdateFinished;

        public PhoenixSwitcherLogic(EspControllerInfo controllerInfo)
        {
            EspInfo = controllerInfo;
            _logManager = LogManager.GetInstance();
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Constructor -> Start");

            _espController = new Esp32Controller();
            _usbTool = new UsbTool();

            MachineInfoWindow.OnShutOffPower += SwitchPowerToPhoenix;
            MachineInfoWindow.OnStartBundleProcess += StartProcess;
            MachineInfoWindow.OnProcessFinished += FinishProcess;
            MachineInfoWindow.OnTest += SwitchPowerToPhoenix;
            OnProcessCancelled += OnCancelled;
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Constructor -> End");
        }

        public async Task Init()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Init -> Initializing switcher logic.");
            await Internal_Init();
        }
        public async Task RetryInit()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::RetryInit -> Initializing switcher logic.");
            Disconnect();
            await Internal_Init();
        }

        private async Task Internal_Init()
        {
            bIsInitializingEsp = true;
            try
            {
                StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0024", "Attempting to connect to ControllerBox");
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Internal_Init -> Attempting to connect to box");
                await SetupEspController();
                CleanupDrive();

                _bWasInitialized = true;
                bIsInitializingEsp = false;
                NumConnectedEspControllers++;
                OnFinishedEspSetup?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logManager?.Log(LogLevel.Error, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Internal_Init -> Failed to connect to box");
                // exception here is already localized notmally.
                Helpers.ShowOkMessageBox(Application.Current.MainWindow, ex.Message);
                bIsInitializingEsp = false;
                OnFinishedEspSetup?.Invoke(this, false);
            }
        }
        public bool HasEspConnection()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tHasEspConnection -> Check if box has proper connection.");
            bool result = _espController != null && _espController.IsConnected;
            if (!result && _bWasInitialized)
            {
                NumConnectedEspControllers--;
                _bWasInitialized = false;
            }
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tHasEspConnection -> Has connection result: {result}.");
            return result;
        }

        public void Disconnect()
        {
            if (HasEspConnection())
            {
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Disconnect -> Disconnecting EspController");
                NumConnectedEspControllers--;
                _espController.Disconnect();
                _bWasInitialized = false;
            }
        }

        public void UpdateBundleFilesOnDrive()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::UpdateBundleFiles -> Started updating bundle files.");
            Application.Current.Dispatcher.Invoke((Action)async delegate
            {
                UpdateWindow updatingWindow = new UpdateWindow();
                try
                {
                    OnBundleUpdateStarted?.Invoke(this);
                    NumOngoingBundleUpdates++;
                    bIsUpdatingBundles = true;
                    Mouse.OverrideCursor = Cursors.Wait;

                    updatingWindow.Show();
                    updatingWindow.Topmost = true;
                    await Task.Run(() => UpdateBundleFiles_Internal());
                    updatingWindow.Close();

                    Mouse.OverrideCursor = null;
                    bIsUpdatingBundles = false;
                    _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::UpdateBundleFiles -> Finished updating bundle file.");
                }
                catch (Exception ex)
                {
                    updatingWindow.Close();
                    _logManager?.Log(LogLevel.Error, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::UpdateBundleFiles -> Exception occurred: {ex.Message}");
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0015", "Failed to update the bundles. look at logs for what went wrong");
                }

                NumOngoingBundleUpdates--;
                bIsUpdatingBundles = false;
                Mouse.OverrideCursor = null;
                OnBundleUpdateFinished?.Invoke(this);
            });
        }
        private async Task UpdateBundleFiles_Internal()
        {
            if (bIsPhoenixSetupOngoing)
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    // Proecess is still running when bundle update is supposed to happen.
                    // Delay update until after process has finished.
                    _bExecuteDelayedBundleUpdate = true;
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0016", "Phoenix setup was ongoing while bundle update was supposed to happen. Delaying update until after setup has completed.");
                });
                return;
            }
            if (!HasEspConnection())
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0025", "PC needs to be connected to the ControllerBox to update the bundles.");

                });
                OnFinishedEspSetup?.Invoke(this, false);
                return;
            }

            if (!Directory.Exists(_drive)) await ConnectDriveToPC();
            RenameGMHIFileToBundleFile();

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            List<string> bundleFoldersOnPC = Directory.GetDirectories(settings.BundleFilesDirectory).ToList();
            List<string> bundleFoldersOnDrive = Directory.GetDirectories(_drive).ToList();

            //remove the bundles that are on both as they do not need to be added or removed.
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::UpdateBundleFiles -> Checking which bundles need an update.");
            for (int i = bundleFoldersOnPC.Count - 1; i >= 0; --i)
            {
                for (int j = bundleFoldersOnDrive.Count - 1; j >= 0; --j)
                {
                    if (Path.GetFileName(bundleFoldersOnPC[i]) == Path.GetFileName(bundleFoldersOnDrive[j]))
                    {
                        bundleFoldersOnDrive.RemoveAt(j);
                        bundleFoldersOnPC.RemoveAt(i);
                        break;
                    }
                }
            }

            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::UpdateBundleFiles -> Attempt to delete old bundles still on drive.");
            foreach (string oldBundleFolder in bundleFoldersOnDrive)
            {
                Directory.Delete(oldBundleFolder, true);
            }

            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::UpdateBundleFiles -> Attempt to download new bundles not on drive yet.");
            foreach (string newBundleFolder in bundleFoldersOnPC)
            {
                string targetPath = _drive + Path.GetFileName(newBundleFolder);
                string sourcePath = settings.BundleFilesDirectory + Path.GetFileName(newBundleFolder);
                // Create bundle directory and any subdirectories.
                Directory.CreateDirectory(targetPath);
                foreach (string dirPath in Directory.GetDirectories(targetPath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
                }
                //Copy all the files to the drive.
                foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
                }
            }


            settings.LastBundleUpdateDate = DateTime.Now;
            settings.TrySave($"C:\\COSnT\\PhoenixUpdater\\Settings\\ProjectSettings.xml");
        }
        private async void StartProcess(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? machine)
        {
            try
            {
                if (switcherLogic != this && !bIsPhoenixSetupOngoing) return;
                bIsPhoenixSetupOngoing = true;
                NumActiveSetups++;

                Mouse.OverrideCursor = Cursors.Wait;
                if (!HasEspConnection())
                {
                    OnProcessCancelled?.Invoke(this);
                    _logManager?.Log(LogLevel.Warn, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::StartProcess -> No EspController connected, cannot start.");
                    //Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0023", "EspController connection has not been established yet. Wait or retry connecting.");
                    StatusDelegates.UpdateStatus(this, StatusLevel.Error, "ID_02_0023", "EspController connection has not been established yet. Wait or retry connecting.");
                    return;
                }
                StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0006", "Process started setting up everything to setup 'Phoenix screen'");

                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::StartProcess -> Start the phoenix process for selected bundle.");
                if (machine == null)
                {
                    OnProcessCancelled?.Invoke(this);
                    _logManager?.Log(LogLevel.Warn, $"PhoenixSwitcherLogic::StartProcess -> Selected a machine with invalid data.");
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0001", "Invalid machine selected");
                    return;
                }

                if (bIsUpdatingBundles)
                {
                    OnProcessCancelled?.Invoke(this);
                    _logManager?.Log(LogLevel.Warn, $"PhoenixSwitcherLogic::StartProcess -> Is updating bundles please wait until done.");
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0017", "Bundles are being updated please wait.");
                    return;
                }

                // Check if a PhoenixFile already exists.
                // If it does make sure it gets set to old name as we do not want to overwrite.
                // Normally should only happen if program was shut down or crashed in the middle of the process.
                if (Directory.Exists(_phoenixFilePath)) RenameGMHIFileToBundleFile();

                StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0019", "Renaming selected 'Bundle file' to 'GHMIFile'");
                if (!await RenameBundleFileToGMHIFile(machine.Bundle_version))
                {
                    OnProcessCancelled?.Invoke(this);
                    _logManager?.Log(LogLevel.Warn, $"PhoenixSwitcherLogic::StartProcess -> Failed to setup phoenix file from selected bundle.");
                    Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0003", "Failed to find matching bundle files for selected vehicle. Try updating bundle files.");
                    return;
                }

                XmlProjectSettings settings = Helpers.GetProjectSettings();
                if (settings.ShouldSwitchDriveBeforePower)
                {
                    StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0021", "Switching drive.");

                    if (!UsbEjectTool.SafeRemove(_drive)) _logManager?.Log(LogLevel.Warn, "Failed to safe eject. switching drive unsafely.");
                    if (!SwitchDriveConnection())
                    {
                        OnProcessCancelled?.Invoke(this);
                        Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0026", "Failed to switch drives properly going back to Start");
                        return;
                    }
                    await Task.Delay(settings.DriveSwitchWaitTimeSec * 1000);
                    StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0018", "Switching power to Phoenix PCM");
                    SwitchPowerToPhoenix(this, true);
                }
                else
                {
                    StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0018", "Switching power to Phoenix PCM");
                    SwitchPowerToPhoenix(this, true);
                    await Task.Delay(settings.DriveSwitchWaitTimeSec * 1000);
                    StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0021", "Switching drive.");
                    if (!UsbEjectTool.SafeRemove(_drive)) _logManager?.Log(LogLevel.Warn, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::Failed to safe eject. switching drive unsafely."); 
                    if (!SwitchDriveConnection())
                    {
                        SwitchPowerToPhoenix(this, false);
                        OnProcessCancelled?.Invoke(this);
                        Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_02_0026", "Failed to switch drives properly going back to Start");
                        return;
                    }
                }

                // Wait with showing finished button atleast until the drive is no longer connected.
                // User cannot complete process before the drive has switched properly.
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::StartProcess -> Invoking process started delegate once drive has properly switched.");

                OnProcessStarted?.Invoke(this);
                Mouse.OverrideCursor = null;

                StatusDelegates.UpdateStatus(this, StatusLevel.Instruction, "ID_02_0007", "Complete setup on 'Phoenix Screen' and press 'Power off' once done.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private async void FinishProcess(PhoenixSwitcherLogic? switcherLogic)
        {
            if (switcherLogic != this) return;

            NumActiveSetups--;
            bIsPhoenixSetupOngoing = false;
            StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0008", "Process finished, resetting to start");
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::FinishProcess -> Phoenix process has finished. Switch drive back. and reset state back to start.");
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                SwitchPowerToPhoenix(this, false);
                if (await ConnectDriveToPC()) CleanupDrive();
            }
            finally
            {
                OnProcessFinished?.Invoke(this);
                Mouse.OverrideCursor = null;
            }

            if (_bExecuteDelayedBundleUpdate)
            {
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::FinishProcess -> Executing delayed bundle update.");
                UpdateBundleFilesOnDrive();
                _bExecuteDelayedBundleUpdate = false;
            }
            StatusDelegates.UpdateStatus(this, StatusLevel.Instruction, "ID_02_0005", "Select machine from list or use scanner.");
        }

        private void OnCancelled(PhoenixSwitcherLogic switcherLogic)
        {
            NumActiveSetups--;
            bIsPhoenixSetupOngoing = false;
            Mouse.OverrideCursor = null;
        }


        // Helpers
        private async Task SetupEspController()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Start setup for Esp32Controller.");
            bool result = false;
            if (EspInfo.COMPortID > 0)
            {
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Attempting to connect using ComportID: {EspInfo.COMPortID}");
                for (int i = 0; i < 5; ++i)
                {
                    result = _espController.Connect(EspInfo.COMPortID);
                    if (result) break;
                    _logManager?.Log(LogLevel.Warn, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Failed connect retry attempt: {i + 1}");
                    await Task.Delay(500);
                }
            }
            else
            {
                _logManager?.Log(LogLevel.Warn, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Attempting to connect without ComportID");
                await _espController.Connect(EspInfo.EspID);
            }

            if (!_espController.IsConnected)
            {
                _logManager?.Log(LogLevel.Error, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Unable to connect to EspController");
                string part1 = Helpers.TryGetLocalizedText("ID_02_0022", "Missing USB connection to the box with name: ");
                string part2 = Helpers.TryGetLocalizedText("ID_02_0023", "Check USB Connection and press 'Retry'.");
                StatusDelegates.UpdateStatus(this, StatusLevel.Error, $"{part1}{EspInfo.BoxName}{part2}");
                throw new Exception($"{part1}{EspInfo.BoxName}, EspID: {EspInfo.EspID}, DriveName: {EspInfo.DriveName}");
            }
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Connection successfull");
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetupEspController -> Switching all Esp32 relais to false");
            _espController.SetAllRelays(false);
            await ConnectDriveToPC();
        }
        private async Task<bool> ConnectDriveToPC()
        {
            int numTries = 0;
            int waitTimeMs = 5000;
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ConnectDriveToPC -> attempting to switch usb drive connection to this pc.");
            while (!IsDriveConnectedToPC())
            {
                SwitchDriveConnection();
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ConnectDriveToPC -> waiting {waitTimeMs}ms before checking if drive is connected.");
                if (numTries > 3) return false;
                await Task.Delay(waitTimeMs);
                numTries++;
            }
            return true;
        }
        private bool SwitchDriveConnection()
        {
            if (!HasEspConnection()) return false;

            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SwitchDriveConnection -> Set Relais 1 to true to start drive switch.");
            if (_espController?.SetRelay1(true) == -1) return false;

            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SwitchDriveConnection -> Wait 500ms before switching Relais 1 to false again.");
            Thread.Sleep(500);

            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SwitchDriveConnection -> Set Relais 1 to false to complete the drive switch.");
            if (_espController?.SetRelay1(false) == -1) return false;

            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SwitchDriveConnection -> Drive switch finished.");
            return true;
        }
        
        private bool IsDriveConnectedToPC()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::IsDriveConnectedToPC -> Checking if drive is connected to pc.");
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::IsDriveConnectedToPC -> Drivename we are looking for: {EspInfo.DriveName}");
            _drive = _usbTool.GetDrive(EspInfo.DriveName).DriveLetter;
            _phoenixFilePath = _drive + _phoenixFileName;
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::IsDriveConnectedToPC -> Resulting found path: {_phoenixFilePath}");
            return !string.IsNullOrEmpty(_drive);
        }
        private void SwitchPowerToPhoenix(PhoenixSwitcherLogic? switcherLogic, bool result)
        {
            if (switcherLogic != this) return;
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SwitchPowerToPhoenix -> Use relais to switch power of phoenix on/off");
            if (!HasEspConnection()) return;
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SwitchPowerToPhoenix -> Switch Relais to: {result}");
            _espController?.SetRelay2(result);
        }
        private void CleanupDrive()
        {
            try
            {
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::CleanupDrive -> Cleaning up drive for next use.");
                RenameGMHIFileToBundleFile();

                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::CleanupDrive -> Removing any files generated by phoenix screen that are no longer used.");
                // Remove any folders/files that are not bundle files
                List<string> foldersOnDrive = Directory.GetDirectories(_drive).ToList();
                foreach (string folder in foldersOnDrive)
                {
                    if (!folder.Contains("PCMBUNDLE_"))
                    {
                        Directory.Delete(folder, true);
                        _logManager?.Log(LogLevel.Info, $"PhoenixSwitcherLogic::CleanupDrive -> Deleted file: {folder}");
                    }
                }
            }
            catch (Exception ex)
            {
                // The exceptions here is already a localized messege.
                _logManager?.Log(LogLevel.Error, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::CleanupDrive -> Failed to cleanup drive properly.");
                Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "", ex.Message);
            }
        }
        private void RenameGMHIFileToBundleFile()
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> resetting potential phoenix file back to its bundle file name.");
            // if there is none do not care.
            if (!Directory.Exists(_drive))
            {
                throw new Exception(Helpers.TryGetLocalizedText("ID_02_0004", "Could not find drive with DriveName: ") + EspInfo.DriveName);
            }
            if (!Directory.Exists(_phoenixFilePath))
            {
                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> No phoenix file found returning.");
                return;
            }

            // Find the BundleManifest.xml file.
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Look for bundle manifest file which contains the bundle version name.");
            List<string> files = Directory.GetFiles(_phoenixFilePath).ToList();
            foreach (string file in files)
            {
                if (!file.Contains("BundleManifest")) continue;

                _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Found bundle manifest file.");
                // Get bundle version from filename.
                string fileName = Path.GetFileNameWithoutExtension(file);
                int startidx = fileName.LastIndexOf("_") + 1;
                fileName = fileName.Substring(startidx);
                try
                {
                    // Rename file directory to bundle version.
                    _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Change name to original bundle name.");
                    fileName = Helpers.RemoveExtraZeroFromVersionName(fileName);
                    Directory.Move(_phoenixFilePath, _drive + "PCMBUNDLE_" + fileName);
                }
                catch { }
                break;
            }
        }
        private async Task<bool> RenameBundleFileToGMHIFile(string bundleFileName)
        {
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Try change selected bundle filename to Phoenix filename");
            if (!IsDriveConnectedToPC())
            {
                await ConnectDriveToPC();
            }
            string filePath = _drive + "PCMBUNDLE_" + bundleFileName;

            if (!Directory.Exists(filePath))
            {
                _logManager?.Log(LogLevel.Error, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Bundle with name: {bundleFileName} does not exist.");
                return false;
            }

            Directory.Move(filePath, _phoenixFilePath);
            _logManager?.Log(LogLevel.Info, $"Box: {EspInfo.BoxName}\tPhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Changing bundle: {bundleFileName} to Phoenix filename.");
            return true;
        }

    }
}
