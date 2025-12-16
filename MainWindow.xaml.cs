using System.IO;
using System.Windows;
using CosntCommonLibrary.Esp32;
using CosntCommonLibrary.merge.Tools;
using CosntCommonLibrary.Rest;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools.Usb;
using CosntCommonLibrary.Xml.PhoenixSwitcher;
using Microsoft.IdentityModel.Tokens;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel = new MainWindowViewModel();
        private Logger _logger;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            XmlProjectSettings settings = Helpers.GetProjectSettings();
            _logger = new Logger(settings.LogFileName, settings.LogDirectory);


            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            // Call once to setup initial language.
            OnLanguageChanged();





            PhoenixRest phoenixRest = PhoenixRest.GetInstance();
            Esp32Controller esp32Controller = new Esp32Controller();
            while (!esp32Controller.IsConnected) { Thread.Yield(); }

            // Connect to drive and get list of all files that need to be on drive.
            string drive = "F:\\";
            Task connectToDrive = ConnectToDrive(esp32Controller, drive);
            Task<List<FileDetail>> bundleFiles = phoenixRest.GetBundleFiles();

            //Wait until both are done.
            connectToDrive.Wait();
            bundleFiles.Wait();

            // Download new bundles
            List<string> directories = Directory.GetDirectories(drive).ToList();
            foreach (string directory in directories)
            {
                bool bFoundBundleDir = false;
                foreach (FileDetail fileDetail in bundleFiles.Result)
                {
                    if (Path.GetFileNameWithoutExtension(fileDetail.FileName).Contains(directory))
                    {
                        bFoundBundleDir = true;
                        break;
                    }
                }
                if (!bFoundBundleDir)
                {
                    Directory.Delete($"F:\\{directory}", true);
                    directories.Remove(directory);
                }
            }

            // Removing old/unused bundles
            foreach (FileDetail fileDetail in bundleFiles.Result)
            {
                bool bFoundBundleDir = false;
                foreach (string directory in directories)
                {
                    if (Path.GetFileNameWithoutExtension(fileDetail.FileName).Contains(directory))
                    {
                        bFoundBundleDir = true;
                        break;
                    }
                }
                if (!bFoundBundleDir)
                {
                    // TODO: download file, unzip it and delete the zip. 
                    Task<bool> t = phoenixRest.GetDownloadBundleFileWithCallback(fileDetail, drive);
                    t.Wait();
                }
            }
        }

        private void OnLanguageChanged()
        {
            try
            {
                MainWindowViewModel viewModel = (MainWindowViewModel)this.DataContext;
                if (viewModel == null) throw new Exception("Incorrect ViewModel");

                LocalizationManager localizationManager = LocalizationManager.GetInstance();
                if (localizationManager == null) throw new Exception("Cannot get LocalizationManager");

                viewModel.WindowName = localizationManager.GetEntryForActiveLanguage("ID_01_0001", "Phoe Swi");
                viewModel.SettingsText = localizationManager.GetEntryForActiveLanguage("ID_01_0005", "Set");
                viewModel.LanguageSettingsText = localizationManager.GetEntryForActiveLanguage("ID_01_0006", "Lan Set");
                viewModel.AboutText = localizationManager.GetEntryForActiveLanguage("ID_01_0007", "Ab");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void ChangeSettings_Click(object sender, RoutedEventArgs e)
        {

        }
        private void About_Click(object sender, RoutedEventArgs e)
        {

        }


        private async Task ConnectToDrive(Esp32Controller esp32Controller, string drivePath)
        {
            esp32Controller.SetAllRelays(false);

            UsbTool usbTool = new UsbTool();
            while (string.IsNullOrEmpty((await usbTool.GetDrive("PHOENIXD")).DriveLetter))
            {
                esp32Controller.SetAllRelays(true);
                esp32Controller.SetAllRelays(false);

                Thread.Sleep(5000);
            }
        }
    }
}