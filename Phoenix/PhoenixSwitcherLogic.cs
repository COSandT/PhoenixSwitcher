using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Tools.Usb;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using Microsoft.IdentityModel.Tokens;
using PhoenixSwitcher.ControlTemplates;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;
using PhoenixSwitcher.Windows;

namespace PhoenixSwitcher
{
    public class PhoenixSwitcherLogic
    {
        private Esp32Controller _espController;
        private UsbTool _usbTool;
        private readonly Logger? _logger;
        private const string _phoenixFileName = "GHMIFiles";
        private string _phoenixFilePath = string.Empty;
        private string _drive = string.Empty;
        private bool _bExecuteDelayedBundleUpdate = false;
        private bool _bWasInitialized = false;


        public static int NumConnectedEspControllers = 0;
        public static int NumOngoingBundleUpdates = 0;
        public static int NumActiveSetups = 0;
        public string DriveName { get; private set; } = string.Empty;
        public string BoxName { get; private set; } = string.Empty;
        public string EspID { get; private set; } = string.Empty;
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

        public PhoenixSwitcherLogic(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"PhoenixSwitcherLogic::Constructor -> Start");

            _espController = new Esp32Controller();
            _usbTool = new UsbTool();

            MachineInfoWindow.OnShutOffPower += SwitchPowerToPhoenix;
            MachineInfoWindow.OnTest += SwitchPowerToPhoenix;
            MachineInfoWindow.OnStartBundleProcess += StartProcess;
            MachineInfoWindow.OnProcessFinished += FinishProcess;
            _logger?.LogInfo($"PhoenixSwitcherLogic::Constructor -> End");
        }


        public async Task Init(string espID, string driveName, string boxName)
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::Init -> Initializing switcher logic");
            DriveName = driveName;
            BoxName = boxName;
            EspID = espID;
            await Internal_Init();
        }
        public async void RetryInit()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::RetryInit -> Initializing switcher logic");
            if (DriveName.IsNullOrEmpty() || BoxName.IsNullOrEmpty() || EspID.IsNullOrEmpty()) return;
            await Internal_Init();
        }

        private async Task Internal_Init()
        {
            bIsInitializingEsp = true;
            try
            {
                StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0024", "Attempting to connect to ControllerBox");
                if (HasEspConnection())
                {
                    NumConnectedEspControllers--;
                    _espController.Disconnect();
                    _bWasInitialized = false;
                }
                await SetupEspController();
                CleanupDrive();

                _bWasInitialized = true;
                bIsInitializingEsp = false;
                NumConnectedEspControllers++;
                OnFinishedEspSetup?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                // exception here is already localized notmally.
                Helpers.ShowLocalizedOkMessageBox("", ex.Message);
                bIsInitializingEsp = false;
                OnFinishedEspSetup?.Invoke(this, false);
            }
        }

        public bool HasEspConnection()
        {
            bool result = _espController != null && _espController.IsConnected && _espController.Ping() != -1;
            if (!result && _bWasInitialized)
            {
                NumConnectedEspControllers--;
                _bWasInitialized = false;
            }
            return result;
        }

        public void UpdateBundleFilesOnDrive()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Started updating bundle files.");
            Application.Current.Dispatcher.Invoke((Action)async delegate
            {
                try
                {
                    OnBundleUpdateStarted?.Invoke(this);
                    NumOngoingBundleUpdates++;
                    bIsUpdatingBundles = true;
                    Mouse.OverrideCursor = Cursors.Wait;

                    UpdateWindow updatingWindow = new UpdateWindow();
                    updatingWindow.Show();
                    updatingWindow.Topmost = true;
                    await Task.Run(() => UpdateBundleFiles_Internal());
                    updatingWindow.Close();

                    Mouse.OverrideCursor = null;
                    bIsUpdatingBundles = false;
                    _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Finished updating bundle file.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"PhoenixSwitcherLogic::UpdateBundleFiles -> Exception occurred: {ex.Message}");
                    Helpers.ShowLocalizedOkMessageBox("ID_02_0015", "Failed to update the bundles. look at logs for what went wrong");
                }

                NumOngoingBundleUpdates--;
                bIsUpdatingBundles = false;
                Mouse.OverrideCursor = null;
                OnBundleUpdateFinished?.Invoke(this);
            });
            
        }
        private async Task UpdateBundleFiles_Internal()
        {
            try
            {
                if (bIsPhoenixSetupOngoing)
                {
                    // Proecess is still running when bundle update is supposed to happen.
                    // Delay update until after process has finished.
                    _bExecuteDelayedBundleUpdate = true;
                    Helpers.ShowLocalizedOkMessageBox("ID_02_0016", "Phoenix setup was ongoing while bundle update was supposed to happen. Delaying update until after setup has completed.");
                    return;
                }
                if (!HasEspConnection())
                {
                    Helpers.ShowLocalizedOkMessageBox("ID_02_0025", "PC needs to be connected to the ControllerBox to update the bundles.");
                    OnFinishedEspSetup?.Invoke(this, false);
                    return;
                }

                if (!Directory.Exists(_drive)) await ConnectDriveToPC();
                RenameGMHIFileToBundleFile();

                XmlProjectSettings settings = Helpers.GetProjectSettings();
                List<string> bundleFoldersOnPC = Directory.GetDirectories(settings.BundleFilesDirectory).ToList();
                List<string> bundleFoldersOnDrive = Directory.GetDirectories(_drive).ToList();

                //remove the bundles that are on both as they do not need to be added or removed.
                _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Checking which bundles need an update.");
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

                _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Attempt to delete old bundles still on drive.");
                foreach (string oldBundleFolder in bundleFoldersOnDrive)
                {
                    Directory.Delete(oldBundleFolder, true);
                }

                _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Attempt to download new bundles not on drive yet.");
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
                settings.TrySave($"{AppContext.BaseDirectory}//Settings//ProjectSettings.xml");
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    _logger?.LogError($"PhoenixSwitcherLogic::UpdateBundleFiles -> Failed to update bundle files exception: {ex.Message}");
                    Helpers.ShowLocalizedOkMessageBox("ID_02_0015", "Failed to update the bundles. look at logs for what went wrong");
                });
            }
        }
        private async void StartProcess(PhoenixSwitcherLogic? switcherLogic, PhoenixSwitcherDone? machine)
        {
            if (switcherLogic != this) return;

            if (!HasEspConnection())
            {
                OnProcessCancelled?.Invoke(this);
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> No EspController connected, cannot start.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0023", "EspController connection has not been established yet. Wait or retry connecting.");
                StatusDelegates.UpdateStatus(this, StatusLevel.Error, "ID_02_0023", "EspController connection has not been established yet. Wait or retry connecting.");
                return;
            }
            _logger?.LogInfo($"ESP PING: {_espController.Ping()}");
            StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0006", "Process started setting up everything to setup 'Phoenix screen'");

            _logger?.LogInfo($"PhoenixSwitcherLogic::StartProcess -> Start the phoenix process for selected bundle.");
            if (machine == null)
            {
                OnProcessCancelled?.Invoke(this);
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Selected a machine with invalid data.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0001", "Invalid machine selected");
                return;
            }

            if (bIsUpdatingBundles)
            {
                OnProcessCancelled?.Invoke(this);
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Is updating bundles please wait until done.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0017", "Bundles are being updated please wait.");
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
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Failed to setup phoenix file from selected bundle.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0003", "Failed to find matching bundle files for selected vehicle. Try updating bundle files.");
                return;
            }

            NumActiveSetups++;
            bIsPhoenixSetupOngoing = true;
            StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0018", "Switching power to Phoenix PCM");
            SwitchPowerToPhoenix(this, true);

            StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0020", "Waiting for bootup to switch drive.");

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            await Task.Delay(settings.DriveSwitchWaitTimeSec * 1000);
            // Switch drive to other device.
            await SwitchDriveConnection();

            // Wait with showing finished button atleast until the drive is no longer connected.
            // User cannot complete process before the drive has switched properly.
            _logger?.LogInfo($"PhoenixSwitcherLogic::StartProcess -> Invoking process started delegate once drive has properly switched.");
            StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0021", "Switching drive.");
            while (IsDriveConnectedToPC())
            {
                await Task.Delay(500);
            }
            OnProcessStarted?.Invoke(this);

            StatusDelegates.UpdateStatus(this, StatusLevel.Instruction, "ID_02_0007", "Complete setup on 'Phoenix Screen' and press 'Power off' once done.");
        }
        private async void FinishProcess(PhoenixSwitcherLogic? switcherLogic)
        {
            if (switcherLogic != this) return;

            NumActiveSetups--;
            bIsPhoenixSetupOngoing = false;
            StatusDelegates.UpdateStatus(this, StatusLevel.Status, "ID_02_0008", "Process finished, resetting to start");

            _logger?.LogInfo($"PhoenixSwitcherLogic::FinishProcess -> Phoenix process has finished. Switch drive back. and reset state back to start.");
            SwitchPowerToPhoenix(this, false);
            await ConnectDriveToPC();

            // Switch filename back to proper bundle name.
            RenameGMHIFileToBundleFile();

            CleanupDrive();
            OnProcessFinished?.Invoke(this);
            if (_bExecuteDelayedBundleUpdate)
            {
                UpdateBundleFilesOnDrive();
                _bExecuteDelayedBundleUpdate = false;
            }
            StatusDelegates.UpdateStatus(this, StatusLevel.Instruction, "ID_02_0005", "Select machine from list or use scanner.");
        }

        // Helpers
        private async Task SetupEspController()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SetupEspController -> Start setup for Esp32Controller");
            await _espController.Connect(EspID);

            if (!_espController.IsConnected)
            {
                _logger?.LogError($"PhoenixSwitcherLogic::SetupEspController -> Unable to connect to EspController");
                string part1 = Helpers.TryGetLocalizedText("ID_02_0022", "Missing USB connection to the box with name: ");
                string part2 = Helpers.TryGetLocalizedText("ID_02_0023", "Check USB Connection and press 'Retry'.");
                StatusDelegates.UpdateStatus(this, StatusLevel.Error, "", part1 + BoxName + " "+ part2);
                throw new Exception($"{part1}{BoxName}, EspID: {EspID}, DriveName: {DriveName}");
            }
            _espController.SetAllRelays(false);
            await ConnectDriveToPC();
        }
        private async Task ConnectDriveToPC()
        {
            int numTries = 0;
            int waitTimeMs = 5000;
            _logger?.LogInfo($"PhoenixSwitcherLogic::ConnectDriveToPC -> attempting to connect the drive to this pc.");
            while (!IsDriveConnectedToPC())
            {
                await SwitchDriveConnection();
                _logger?.LogInfo($"PhoenixSwitcherLogic::ConnectDriveToPC -> waiting {waitTimeMs}ms before checking if drive is connected.");
                if (numTries > 10) throw new Exception($"Failed to find drive for EspController with ID: {EspID} and name: {DriveName}");
                await Task.Delay(waitTimeMs);
                numTries++;
            }
        }
        private async Task SwitchDriveConnection()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SwitchDriveConnection -> Use relais to switch what device the drive is connected to.");
            if (_espController != null)
            {
                _espController.SetRelay1(true);
                await Task.Delay(500);
                _espController.SetRelay1(false);
            }    
        }
        private bool IsDriveConnectedToPC()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::IsDriveConnectedToPC -> Checking if drive is connected to pc.");
            _drive = _usbTool.GetDrive(DriveName).DriveLetter;
            _phoenixFilePath = _drive + _phoenixFileName;
            return !string.IsNullOrEmpty(_drive);
        }
        private void SwitchPowerToPhoenix(PhoenixSwitcherLogic? switcherLogic, bool result)
        {
            if (switcherLogic != this) return;
            _logger?.LogInfo($"PhoenixSwitcherLogic::SwitchPowerToPhoenix -> Use relais to switch power of phoenix on/off");
            if (_espController != null)
            {
                _espController.SetRelay2(result);
            }
        }

        private void CleanupDrive()
        {
            try
            {
                RenameGMHIFileToBundleFile();

                // Remove any folders/files that are nt bundle files
                List<string> foldersOnDrive = Directory.GetDirectories(_drive).ToList();
                foreach (string folder in foldersOnDrive)
                {
                    if (!folder.Contains("PCMBUNDLE_"))
                    {
                        Directory.Delete(folder, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    // The exceptions here is already a localized messege.
                    Helpers.ShowLocalizedOkMessageBox("", ex.Message);
                });
            }
        }
        private void RenameGMHIFileToBundleFile()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> resetting potential phoenix file back to its bundle file name.");
            // if there is none do not care.
            if (!Directory.Exists(_drive))
            {
                throw new Exception(Helpers.TryGetLocalizedText("ID_02_0004", "Could not find drive with DriveName: ") + DriveName);
            }
            if (!Directory.Exists(_phoenixFilePath))
            {
                _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> No phoenix file found returning.");
                return;
            }

            // Find the BundleManifest.xml file.
            _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Look for bundle manifest file which contains the bundle version name.");
            List<string> files = Directory.GetFiles(_phoenixFilePath).ToList();
            foreach (string file in files)
            {
                if (!file.Contains("BundleManifest")) continue;

                _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Found bundle manifest file.");
                // Get bundle version from filename.
                string fileName = Path.GetFileNameWithoutExtension(file);
                int startidx = fileName.LastIndexOf("_") + 1;
                fileName = fileName.Substring(startidx);
                try
                {
                    // Rename file directory to bundle version.
                    _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Change name to original bundle name.");
                    fileName = Helpers.RemoveExtraZeroFromVersionName(fileName);
                    Directory.Move(_phoenixFilePath, _drive + "PCMBUNDLE_" + fileName);
                }
                catch { }
                break;
            }
        }
        private async Task<bool> RenameBundleFileToGMHIFile(string bundleFileName)
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Try change selected bundle filename to Phoenix filename");
            if (!IsDriveConnectedToPC())
            {
                await ConnectDriveToPC();
            }
            string filePath = _drive + "PCMBUNDLE_" + bundleFileName;

            if (!Directory.Exists(filePath))
            {
                _logger?.LogError($"PhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Bundle with name: {bundleFileName} does not exist.");
                return false;
            }

            Directory.Move(filePath, _phoenixFilePath);
            _logger?.LogInfo($"PhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Changing bundle: {bundleFileName} to Phoenix filename.");
            return true;
        }

        internal void Init()
        {
            throw new NotImplementedException();
        }
    }
}
