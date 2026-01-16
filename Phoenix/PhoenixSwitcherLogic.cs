using System.IO;
using System.Windows;
using System.Windows.Input;
using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Tools.Usb;
using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using PhoenixSwitcher.ControlTemplates;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.Windows;

namespace PhoenixSwitcher
{
    public class PhoenixSwitcherLogic
    {
        private Esp32Controller? _espController;
        private UsbTool _usbTool;

        private readonly Logger? _logger;

        private const string _phoenixFileName = "GHMIFiles";
        private string _phoenixFilePath = "";
        private string _drive = "";

        private bool _bExecuteDelayedBundleUpdate = false;
        private bool _bIsPhoenixSetupOngoing = false;
        private bool _bIsUpdatingBundles = false;

        public delegate void ProcessStartedHandler();
        public static event ProcessStartedHandler? OnProcessStarted;
        public delegate void ProcessFinishedHandler();
        public static event ProcessFinishedHandler? OnProcessFinished;
        public delegate void ProcessCancelledHandler();
        public static event ProcessCancelledHandler? OnProcessCancelled;

        public PhoenixSwitcherLogic(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"PhoenixSwitcherLogic::Constructor -> Start");
            _usbTool = new UsbTool();

            MachineInfoWindow.OnStartBundleProcess += StartProcess;
            MachineInfoWindow.OnProcessFinished += FinishProcess;
            _logger?.LogInfo($"PhoenixSwitcherLogic::Constructor -> End");
        }


        public async Task Init()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::Init -> Initializing switcher logic");
            await SetupEspController();
            CleanupDrive();
        }

        public void UpdateBundleFilesOnDrive()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Started updating bundle files.");
            try
            {
                Application.Current.Dispatcher.Invoke((Action)async delegate
                {
                    _bIsUpdatingBundles = true;
                    Mouse.OverrideCursor = Cursors.Wait;

                    UpdateWindow updatingWindow = new UpdateWindow();
                    updatingWindow.Show();
                    await Task.Run(() => UpdateBundleFiles_Internal());
                    updatingWindow.Close();

                    Mouse.OverrideCursor = null;
                    _bIsUpdatingBundles = false;
                    _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Finished updating bundle file.");
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    Mouse.OverrideCursor = null;
                    _bIsUpdatingBundles = false;
                });
                _logger?.LogError($"PhoenixSwitcherLogic::UpdateBundleFiles -> Exception occurred: {ex.Message}");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0015", "Failed to update the bundles. look at logs for what went wrong");
            }
        }
        private async Task UpdateBundleFiles_Internal()
        {
            try
            {
                if (_bIsPhoenixSetupOngoing)
                {
                    // Proecess is still running when bundle update is supposed to happen.
                    // Delay update until after process has finished.
                    _bExecuteDelayedBundleUpdate = true;
                    Helpers.ShowLocalizedOkMessageBox("ID_02_0016", "Phoenix setup was ongoing while bundle update was supposed to happen. Delaying update until after setup has completed.");
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
                _logger?.LogError($"PhoenixSwitcherLogic::UpdateBundleFiles -> Failed to update bundle files exception: {ex.Message}");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0015", "Failed to update the bundles. look at logs for what went wrong");
            }
        }
        private async void StartProcess(XmlMachinePCM? machine)
        {
            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_02_0006", "Process started setting up everything to setup 'Phoenix screen'");

            _logger?.LogInfo($"PhoenixSwitcherLogic::StartProcess -> Start the phoenix process for selected bundle.");
            if (machine == null || machine.Ops == null)
            {
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Selected a machine with invalid data.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0001", "Invalid machine selected");
                OnProcessCancelled?.Invoke();
                return;
            }

            if (_bIsUpdatingBundles)
            {
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Is updating bundles please wait until done.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0017", "Bundles are being updated please wait.");
                OnProcessCancelled?.Invoke();
                return;
            }

            // Check if a PhoenixFile already exists.
            // If it does make sure it gets set to old name as we do not want to overwrite.
            // Normally should only happen if program was shut down or crashed in the middle of the process.
            if (Directory.Exists(_phoenixFilePath)) RenameGMHIFileToBundleFile();

            XmlModulePCM pcmModule = machine.Ops.Modules.First();
            BundleSelection? bundle = await PhoenixRest.GetInstance().GetPcmAppSettings(machine.N17.Substring(0, 4), pcmModule.PCMT, pcmModule.PCMG, machine.DT);

            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_02_0019", "Renaming selected 'Bundle file' to 'GHMIFile'");
            if (bundle == null || bundle.Bundle == null || !await RenameBundleFileToGMHIFile(bundle.Bundle))
            {
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Failed to setup phoenix file from selected bundle.");
                Helpers.ShowLocalizedOkMessageBox("ID_02_0003", "Failed to find matching bundle files for selected vehicle. Try updating bundle files.");
                OnProcessCancelled?.Invoke();
                return;
            }

            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_02_0018", "Switching power to Phoenix PCM");
            SwitchPowerToPhoenix(true);

            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_02_0020", "Waiting for bootup to switch drive.");
            await Task.Delay(20000);
            // Switch drive to other device.
            await SwitchDriveConnection();

            // Wait with showing finished button atleast until the drive is no longer connected.
            // User cannot complete process before the drive has switched properly.
            _logger?.LogInfo($"PhoenixSwitcherLogic::StartProcess -> Invoking process started delegate once drive has properly switched.");
            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_02_0021", "Switching drive.");
            while (IsDriveConnectedToPC())
            {
                await Task.Delay(500);
            }
            OnProcessStarted?.Invoke();

            StatusDelegates.UpdateStatus(StatusLevel.Instruction, "ID_02_0007", "Complete setup on 'Phoenix Screen' and press finish once done.");
        }
        private async void FinishProcess()
        {
            OnProcessFinished?.Invoke();
            StatusDelegates.UpdateStatus(StatusLevel.Status, "ID_02_0008", "Process finished, resetting to start");

            _logger?.LogInfo($"PhoenixSwitcherLogic::FinishProcess -> Phoenix process has finished. Switch drive back. and reset state back to start.");
            SwitchPowerToPhoenix(false);
            await ConnectDriveToPC();

            // Switch filename back to proper bundle name.
            RenameGMHIFileToBundleFile();

            CleanupDrive();
            if (_bExecuteDelayedBundleUpdate)
            {
                // Execute delayed bundle update now that process has finished.
                await Task.Run(() => UpdateBundleFilesOnDrive());
                _bExecuteDelayedBundleUpdate = false;
            }
            StatusDelegates.UpdateStatus(StatusLevel.Instruction, "ID_02_0005", "Select machine from list or use scanner.");
        }

        // Helpers
        private async Task SetupEspController()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SetupEspController -> Start setup for Esp32Controller");
            _espController = new Esp32Controller();
            await _espController.Connect();

            _espController.SetAllRelays(false);
            await ConnectDriveToPC();
        }
        private async Task ConnectDriveToPC()
        {
            int waitTimeMs = 5000;
            _logger?.LogInfo($"PhoenixSwitcherLogic::ConnectDriveToPC -> attempting to connect the drive to this pc.");
            while (!IsDriveConnectedToPC())
            {
                await SwitchDriveConnection();
                _logger?.LogInfo($"PhoenixSwitcherLogic::ConnectDriveToPC -> waiting {waitTimeMs}ms before checking if drive is connected.");
                await Task.Delay(waitTimeMs);
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
            _drive = _usbTool.GetDrive("PHOENIXD").DriveLetter;
            _phoenixFilePath = _drive + _phoenixFileName;
            return !string.IsNullOrEmpty(_drive);
        }
        private void SwitchPowerToPhoenix(bool result)
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SwitchPowerToPhoenix -> Use relais to switch power of phoenix on/off");
            if (_espController != null)
            {
                _espController.SetRelay2(result);
            }
        }

        private void CleanupDrive()
        {
            RenameGMHIFileToBundleFile();

            //re Remove any folders/files that are nt bundle files
            List<string> foldersOnDrive = Directory.GetDirectories(_drive).ToList();
            foreach (string folder in foldersOnDrive)
            {
                if (!folder.Contains("PCMBUNDLE_"))
                {
                    Directory.Delete(folder, true);
                }
            }
        }
        private void RenameGMHIFileToBundleFile()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> resetting potential phoenix file back to its bundle file name.");
            // if there is none do not care.
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
    }
}
