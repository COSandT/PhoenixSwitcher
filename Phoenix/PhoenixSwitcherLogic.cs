using System.IO;
using AdonisUI.Controls;
using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.Rest;
using CosntCommonLibrary.SQL.Models.PcmAppSetting;
using CosntCommonLibrary.Tools;
using CosntCommonLibrary.Tools.Usb;
using PhoenixSwitcher.ControlTemplates;

namespace PhoenixSwitcher
{
    public class PhoenixSwitcherLogic
    {
        private Esp32Controller? _espController;
        private PhoenixRest _phoenixRest;
        private UsbTool _usbTool;

        private readonly Logger? _logger;

        private const string _phoenixFileName = "GHMIFiles";
        private string _phoenixFilePath = "";
        private string _drive = "";
        private bool _bIsUpdatingBundles = false;

        public delegate void ProcessStartedHandler();
        public static event ProcessStartedHandler? OnProcessStarted;

        public PhoenixSwitcherLogic(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"PhoenixSwitcherLogic::Constructor -> Start");
            _phoenixRest = PhoenixRest.GetInstance();
            _usbTool = new UsbTool();

            MachineInfoWindow.OnStartBundleProcess += StartProcess;
            MachineInfoWindow.OnProcessFinished += FinishProcess;
            _logger?.LogInfo($"PhoenixSwitcherLogic::Constructor -> End");
        }


        public async Task Init()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::Init -> Initializing switcher logic");
            await SetupEspController();
        }


        public async Task UpdateBundleFiles()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Started updating bundle files");
            _bIsUpdatingBundles = true;
            _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Get list of bundle files from rest api");
            List<string> driveFiles = Directory.GetDirectories(_drive).ToList();
            List<FileDetail>? bundleFiles = await _phoenixRest.GetBundleFiles();
            ResetPhoenixFileToBundleFile();

            Task downloadTask = DownloadNewBundles(bundleFiles, driveFiles);
            Task deleteTask = DeleteOldBundles(bundleFiles, driveFiles);
            await downloadTask;
            await deleteTask;
            _bIsUpdatingBundles = false;
            _logger?.LogInfo($"PhoenixSwitcherLogic::UpdateBundleFiles -> Finished updating bundle files");
        }
        private async void StartProcess(BundleSelection? bundleSelection)
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::StartProcess -> Start the phoenix process for selected bundle.");
            if (bundleSelection == null)
            {
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Selected bundle was invalid.");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", "Invalid selected bundle.");
                return;
            }

            if (!IsDriveConnectedToPC())
            {
                _logger?.LogError($"PhoenixSwitcherLogic::StartProcess -> Drive somehow got disconnected from pc. Did you disconnect it you idiot?");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", "Drive is no longer connected to pc.");
                return;
            }

            if (_bIsUpdatingBundles)
            {
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Program is busy updating bundles. Please wait until finished.");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", "Program is busy updating bundles. Please wait until finished.");
                return;
            }

            // Check if a PhoenixFile already exists.
            // If it does make sure it gets set to old name as we do not want to overwrite.
            // Normally should only happen if program was shut down or crashed in the middle of the process.
            if (Directory.Exists(_phoenixFilePath)) ResetPhoenixFileToBundleFile();

            if (!await SetPhoenixFileFromBundleFile(bundleSelection.Bundle))
            {
                _logger?.LogWarning($"PhoenixSwitcherLogic::StartProcess -> Failed to setup phoenix file from selected bundle.");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", "Failed to setup phoenix file from selected bundle.");
                return;
            }

            // Switch drive to other device.
            await SwitchDriveConnection();

            // Wait with showing finished button atleast until the drive is no longer connected.
            // User cannot complete process before the drive has switched properly.
            _logger?.LogInfo($"PhoenixSwitcherLogic::StartProcess -> Invoking process started delegate once drive has properly switched.");
            while (IsDriveConnectedToPC())
            {
                await Task.Delay(500);
            }
            await SwitchPowerToPhoenix();
            OnProcessStarted?.Invoke();
        }
        private async void FinishProcess()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::FinishProcess -> Phoenix process has finished. Switch drive back. and reset state back to start.");
            Task connectTask = ConnectDriveToPC();
            Task powerTask = SwitchPowerToPhoenix();
            await connectTask;
            await powerTask;

            // Switch filename back to proper bundle name.
            ResetPhoenixFileToBundleFile();

            // Check for any noew bundle updates.
            await UpdateBundleFiles();
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
        private async Task SwitchPowerToPhoenix()
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SwitchPowerToPhoenix -> Use relais to switch power of phoenix on/off");
            if (_espController != null)
            {
                _espController.SetRelay2(true);
                await Task.Delay(500);
                _espController.SetRelay2(false);
            }
        }


        private void ResetPhoenixFileToBundleFile()
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
                // Rename file directory to bundle version.
                _logger?.LogInfo($"PhoenixSwitcherLogic::ResetPhoenixFileToBundleFile -> Change name to original bundle name.");
                fileName = Helpers.RemoveExtraZeroFromVersionName(fileName);
                Directory.Move(_phoenixFilePath, _drive + fileName);
                break;
            }
        }
        private async Task<bool> SetPhoenixFileFromBundleFile(string bundleFileName)
        {
            _logger?.LogInfo($"PhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Try change selected bundle filename to Phoenix filename");
            if (!IsDriveConnectedToPC())
            {
                await ConnectDriveToPC();
            }
            string filePath = _drive + bundleFileName;

            // TODO: report error
            if (!Directory.Exists(filePath))
            {
                _logger?.LogError($"PhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Bundle with name: {bundleFileName} does not exist.");
                return false;
            }

            Directory.Move(filePath, _phoenixFilePath);
            _logger?.LogInfo($"PhoenixSwitcherLogic::SetPhoenixFileFromBundleFile -> Changing bundle: {bundleFileName} to Phoenix filename.");
            return true;
        }
        private Task<bool> DeleteOldBundles(List<FileDetail>? bundleFiles, List<string> driveFiles)
        {
            try
            {
                _logger?.LogInfo($"PhoenixSwitcherLogic::DeleteOldBundles -> Attempt to delete old bundles still on drive.");
                if (bundleFiles == null || driveFiles == null) return Task.FromResult(false);

                foreach (string directory in driveFiles)
                {
                    bool bFoundBundleDir = false;
                    foreach (FileDetail fileDetail in bundleFiles)
                    {
                        if (directory.Contains(Path.GetFileNameWithoutExtension(fileDetail.FileName)))
                        {
                            bFoundBundleDir = true;
                            break;
                        }
                    }
                    if (!bFoundBundleDir)
                    {
                        _logger?.LogInfo($"PhoenixSwitcherLogic::DeleteOldBundles -> Old bundle: {directory} found. deleting bundle.");
                        Directory.Delete(directory, true);
                    }
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"PhoenixSwitcherLogic::DeleteOldBundles -> Delete got interupted. Exception: {ex.Message}");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", "Deleting got interuppted, check logs for exception.");
                return Task.FromResult(false);
            }
        }
        private async Task<bool> DownloadNewBundles(List<FileDetail>? bundleFiles, List<string> driveFiles)
        {
            try
            {
                _logger?.LogInfo($"PhoenixSwitcherLogic::DownloadNewBundles -> Attempt to download new bundles not on drive yet.");
                if (bundleFiles == null || driveFiles == null) return false;

                foreach (FileDetail fileDetail in bundleFiles)
                {
                    bool bFoundBundleDir = false;
                    foreach (string directory in driveFiles)
                    {
                        if (directory.Contains(Path.GetFileNameWithoutExtension(fileDetail.FileName)))
                        {
                            bFoundBundleDir = true;
                            break;
                        }
                    }
                    if (!bFoundBundleDir)
                    {
                        // Download file
                        _logger?.LogInfo($"PhoenixSwitcherLogic::DownloadNewBundles -> New bundle: {Path.GetFileNameWithoutExtension(fileDetail.FileName)} found. downloading bundle.");
                        await _phoenixRest.DownloadBundleFiles(fileDetail, _drive);
                        // Unzip and rename the extracted folder back to zipped file name.
                        _logger?.LogInfo($"PhoenixSwitcherLogic::DownloadNewBundles -> Unzipping bundle and giving it proper name.");
                        System.IO.Compression.ZipFile.ExtractToDirectory(_drive + fileDetail.FileName, _drive);
                        Directory.Move(_phoenixFilePath, _drive + Path.GetFileNameWithoutExtension(fileDetail.FileName));
                        // Delete the zip
                        File.Delete(_drive + fileDetail.FileName);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // TODO: report error.
                _logger?.LogError($"PhoenixSwitcherLogic::DownloadNewBundles -> Download got interupted. Exception: {ex.Message}");
                Helpers.ShowLocalizedOkMessageBox("TODO: LOCA", "Download got interuppted, check logs for exception.");
                return false;
            }
        }
    }
}
