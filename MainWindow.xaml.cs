using System.Windows;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Controls;

using AdonisUI;

using CosntCommonLibrary.Xml;
using CosntCommonLibrary.Helpers;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools.Logging;
using CosntCommonLibrary.Xml.PhoenixSwitcher;

using PhoenixSwitcher.Windows;
using PhoenixSwitcher.Phoenix;
using PhoenixSwitcher.Delegates;
using PhoenixSwitcher.ViewModels;
using PhoenixSwitcher.ControlTemplates;

namespace PhoenixSwitcher
{
    public partial class MainWindow : Window
    {
		private const string SettingsDirectory = @"C:\COSnT\PhoenixUpdater\Settings\";
		private const string ProjectSettingsPath = @"C:\COSnT\PhoenixUpdater\Settings\ProjectSettings.xml";
		private const string LastSuccessfulMachineListFileName = "LastSuccessfulPCMMachineList.xml";

		private const long DefaultGridResizeDelayMilliseconds = 1000;

		private readonly List<PhoenixSoftwareUpdater> _softwareUpdaters = new();
		private readonly MainWindowViewModel _viewModel = new();
		private readonly LogManager? _logManager;

		private int _gridColumns;
		private int _gridRows;

		private long _millisecondsToWaitGridUpdate = DefaultGridResizeDelayMilliseconds;

		private bool _canUpdateGrid = true;
		private bool _isClosing;
		private bool _isDisposed;

		public XmlProductionDataPCM? PCMMachineList { get; private set; }
		public static event EventHandler<XmlProductionDataPCM?>? OnMachineListUpdated;

		public MainWindow()
		{
			InitializeComponent();
			DataContext = _viewModel;
			
			XmlProjectSettings settings = Helpers.GetProjectSettings();
			LogManager.Initialize(settings.LogDirectory, settings.LogFileName);
			LocalizationManager.Initialize(SettingsDirectory);
			_logManager = LogManager.GetInstance();
			LogApplicationVersion();

			Log(LogLevel.Info, "Constructor -> Start initializing.");
			UpdateTheme(settings.Theme);
			InitializeEspControllers();
			InitializeLanguageSettings();
			Log(LogLevel.Info, "Constructor -> Finished initializing.");
		}
		private void SubscribeToEvents()
		{
			LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
		}
		private void UnsubscribeFromEvents()
		{
			LocalizationManager.GetInstance().OnActiveLanguageChanged -= OnLanguageChanged;
		}
		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			if (_isClosing)
			{
				base.OnClosing(e);
				return;
			}

			_isClosing = true;

			Log(LogLevel.Info, "OnClosing -> MainWindow is closing.");
			DisconnectSoftwareUpdaters();
			UnsubscribeFromEvents();
			base.OnClosing(e);
		}
		protected override void OnClosed(EventArgs e)
		{
			_isDisposed = true;
			base.OnClosed(e);
		}
		private void DisconnectSoftwareUpdaters()
		{
			foreach (PhoenixSoftwareUpdater updater in _softwareUpdaters)
			{
				try
				{
					updater.PhoenixSwitcher?.Disconnect();
				}
				catch (Exception ex)
				{
					Log(LogLevel.Error, $"DisconnectSoftwareUpdaters -> Failed to disconnect updater: {ex.Message}");
				}
			}
		}


		// **********
		// Initialize
		private async void InitializeEspControllers()
		{
			Log(LogLevel.Info, "InitializeEspControllers -> Start initializing.");
			Mouse.OverrideCursor = Cursors.Wait;
			try
			{
				XmlProjectSettings settings = Helpers.GetProjectSettings();
				List<EspControllerInfo> activeControllers = GetActiveControllers(settings);
				Log(LogLevel.Info, $"InitializeEspControllers -> Settings contains {activeControllers.Count} active ESP controllers.");
				
				SoftwareUpdaterGrid.Visibility = Visibility.Hidden;
				ConfigureGridDimensions(activeControllers.Count);
				CreateSoftwareUpdaterGrid(activeControllers);
				UpdateGridSize();

				SoftwareUpdaterGrid.Visibility = Visibility.Visible;
				//ScheduleMachineListUpdate(settings);
				await UpdatePcmMachineListAsync();
				await InitializeSoftwareUpdaters();
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, "InitializeEspControllers -> Exception occurred while initializing ESP controllers.");
				_logManager?.LogEntireException(ex);
			}
			finally
			{
				Mouse.OverrideCursor = null;
			}
		}
		private static List<EspControllerInfo> GetActiveControllers(XmlProjectSettings settings)
		{
			return settings.EspControllers.Where(controller => controller.bIsActive).ToList();
		}
		private void ConfigureGridDimensions(int controllerCount)
		{
			if (controllerCount <= 0)
			{
				_gridRows = 0;
				_gridColumns = 0;
				return;
			}
			_gridRows = Math.Max(1, (int)Math.Round(Math.Sqrt(controllerCount)));
			_gridColumns = Math.Max(1, (int)Math.Ceiling((double)controllerCount / _gridRows));
		}
		private void CreateSoftwareUpdaterGrid(List<EspControllerInfo> activeControllers)
		{
			SoftwareUpdaterGrid.Children.Clear();
			if (activeControllers.Count == 0) return;

			for (int row = 0; row < _gridRows; row++)
			{
				StackPanel panel = CreateGridRow();
				SoftwareUpdaterGrid.Children.Add(panel);
				for (int column = 0; column < _gridColumns; column++)
				{
					int index = row * _gridColumns + column;
					if (index >= activeControllers.Count) break;

					EspControllerInfo controller = activeControllers[index];
					PhoenixSoftwareUpdater updater = CreateSoftwareUpdater(controller);
					panel.Children.Add(updater);
				}
			}
		}
		private static StackPanel CreateGridRow()
		{
			return new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
		}
		private PhoenixSoftwareUpdater CreateSoftwareUpdater(EspControllerInfo controller)
		{
			PhoenixSoftwareUpdater updater = new PhoenixSoftwareUpdater(controller, PCMMachineList);
			string info = $"DriveName: {controller.DriveName}, ComID: {controller.COMPortID}, EspID: {controller.EspID}";
			Log(LogLevel.Info, $"CreateSoftwareUpdater -> Generating updater for controller: {info}");
			updater.HorizontalAlignment = HorizontalAlignment.Stretch;
			updater.VerticalAlignment = VerticalAlignment.Stretch;
			_softwareUpdaters.Add(updater);
			return updater;
		}
		//private void ScheduleMachineListUpdate(XmlProjectSettings settings)
		//{
		//	  TaskScheduler.GetInstance().ScheduleTask(settings.TimeToUpdateBundleAt.Hours, settings.TimeToUpdateBundleAt.Minutes, settings.TimeToUpdateBundleAt.Seconds, 24, UpdatePcmMachineListAsync);
		//}
		private async Task InitializeSoftwareUpdaters()
		{
			foreach (PhoenixSoftwareUpdater updater in _softwareUpdaters)
			{
				try
				{
					await updater.InitPhoenixSwitcherAsync();
				}
				catch (Exception ex)
				{
					Log(LogLevel.Error, $"InitializeSoftwareUpdaters -> Failed to initialize updater: {ex.Message}");
					_logManager?.LogEntireException(ex);
				}
			}
		}
		private void InitializeLanguageSettings()
		{
			LanguageSettings.Items.Clear();
			foreach (string language in LocalizationManager.GetInstance().AvailableLanguages)
			{
				MenuItem item = new MenuItem{ Header = language, IsCheckable = true };
				item.Click += ChangeLanguage_Click;
				LanguageSettings.Items.Add(item);
			}
			SubscribeToEvents();
			UpdateLocalizedText();
		}


		// ****
		// Grid
		private void SoftwareUpdaterGrid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			ScheduleGridSizeUpdate(500);
		}
		private async void ScheduleGridSizeUpdate(long delayMilliseconds)
		{
			if (_isDisposed || _isClosing) return;

			_millisecondsToWaitGridUpdate = Math.Max(0, delayMilliseconds);
			if (!_canUpdateGrid) return;

			_canUpdateGrid = false;
			try
			{
				while (_millisecondsToWaitGridUpdate > 0)
				{
					Stopwatch stopwatch = Stopwatch.StartNew();
					await Task.Delay(250);
					_millisecondsToWaitGridUpdate -= stopwatch.ElapsedMilliseconds;
					if (_isDisposed || _isClosing) return;
				}

				UpdateGridSize();
			}
			finally
			{
				_canUpdateGrid = true;
			}
		}
		private void UpdateGridSize()
		{
			if (_gridRows <= 0 || _gridColumns <= 0) return; 
			if (GridBorder.ActualHeight <= 0 || GridBorder.ActualWidth <= 0) return; 

			double rowHeight = GridBorder.ActualHeight / _gridRows;
			double updaterWidth = GridBorder.ActualWidth / _gridColumns;
			foreach (StackPanel panel in SoftwareUpdaterGrid.Children.OfType<StackPanel>())
			{
				panel.MaxHeight = rowHeight;
				panel.Height = rowHeight;
				panel.MaxWidth = GridBorder.ActualWidth;
				panel.Width = GridBorder.ActualWidth;
				foreach (PhoenixSoftwareUpdater updater in panel.Children.OfType<PhoenixSoftwareUpdater>())
				{
					updater.MaxHeight = rowHeight;
					updater.MaxWidth = updaterWidth;
					updater.Height = rowHeight;
					updater.Width = updaterWidth;
				}
			}
		}


		// ************
		// Machine List
		public async Task UpdatePcmMachineListAsync()
		{
			if (_isDisposed || _isClosing) return; 

			StatusDelegates.UpdateStatus(null, StatusLevel.Status, "ID_03_0004", "Updating pcm machine list, please wait.");
			Log(LogLevel.Info, "UpdatePcmMachineList -> Started updating PCM machine list.");
			Mouse.OverrideCursor = Cursors.Wait;
			try
			{
				Log(LogLevel.Info, "UpdatePcmMachineList -> Getting machine file from REST API.");
				if (!await PhoenixRest.GetInstance().IsApiRunning())
				{
					Log(LogLevel.Error, "UpdatePcmMachineList -> REST API is not running is required for both machine list and getting correct bundles.");
					Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_03_Unknown", "REST API is not running, pls contact ur system administrators.");
					TryLoadBackupMachineList();
				}
				else
				{
					XmlProductionDataPCM? machineList = await PhoenixRest.GetInstance().GetPCMMachineFile();
					if (machineList == null || machineList.Machines.Count == 0) throw new InvalidOperationException("PCM machine list is null or empty.");

					PCMMachineList = machineList;
					OnMachineListUpdated?.Invoke(this, PCMMachineList);
					SaveSuccessfulMachineList(PCMMachineList);
					Log(LogLevel.Info, "UpdatePcmMachineList -> Successfully updated PCM machine list.");
				}
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"UpdatePcmMachineList -> Failed to update PCM machine list: {ex.Message}");
				_logManager?.LogEntireException(ex);
				StatusDelegates.UpdateStatus(null, StatusLevel.Status, "ID_03_0005", "Failed to update pcm machine list.");
				Helpers.ShowLocalizedOkMessageBox(Application.Current.MainWindow, "ID_03_0005", "Failed to update pcm machine list. Will try to use backup list.");
				TryLoadBackupMachineList();
			}
			finally
			{
				Mouse.OverrideCursor = null;
				Log(LogLevel.Info, "UpdatePcmMachineList -> Finished updating PCM machine list.");
			}
		}
		private void SaveSuccessfulMachineList(XmlProductionDataPCM machineList)
		{
			try
			{
				string path = System.IO.Path.Combine(SettingsDirectory, LastSuccessfulMachineListFileName);
				machineList.TrySave(path);
				Log(LogLevel.Info, $"SaveSuccessfulMachineList -> Saved machine list to '{path}'.");
			}
			catch (Exception ex)
			{
				Log(LogLevel.Warn, $"SaveSuccessfulMachineList -> Failed to save backup machine list: {ex.Message}");
			}
		}
		private void TryLoadBackupMachineList()
		{
			try
			{
				Log(LogLevel.Info, "TryLoadBackupMachineList -> Attempting to load last successful machine list.");
				XmlSettingsHelper<XmlProductionDataPCM> machineListSettings = new XmlSettingsHelper<XmlProductionDataPCM>(LastSuccessfulMachineListFileName, SettingsDirectory);
				machineListSettings.Load();
				XmlProductionDataPCM? backup = machineListSettings.Settings;
				if (backup?.Machines.Count <= 0)
				{
					Log(LogLevel.Warn, "TryLoadBackupMachineList -> Backup machine list is empty.");
					return;
				}

				PCMMachineList = backup;
				OnMachineListUpdated?.Invoke(this, PCMMachineList);
				Log(LogLevel.Info, "TryLoadBackupMachineList -> Successfully loaded backup machine list.");
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"TryLoadBackupMachineList -> Failed to load backup machine list: {ex.Message}");
				_logManager?.LogEntireException(ex);
			}
		}


		// ************
		// Click Events
		private void ChangeSettings_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "ChangeSettings_Click -> Opening settings editor.");
			SettingsWindow settingsWindow = new SettingsWindow{ Topmost = true };
			settingsWindow.ShowDialog();
		}
		private void ChangeLanguage_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem item) return;
			if (item.Header is not string language || string.IsNullOrWhiteSpace(language)) return;

			LocalizationManager.GetInstance().SetActiveLanguage(language);
		}
		private void UpdateBundleFiles_Click(object sender, RoutedEventArgs e)
		{
			UpdateBundleFiles();
		}
		private async void UpdateMachineList_Click(object sender, RoutedEventArgs e)
		{
			await UpdatePcmMachineListAsync();
		}
		private void About_Click(object sender, RoutedEventArgs e)
		{
			Log(LogLevel.Info, "About_Click -> Opening About window.");
			AboutWindow aboutWindow = new AboutWindow{ Topmost = true };
			aboutWindow.ShowDialog();
		}
		private void SwitchThemeDark_Click(object sender, RoutedEventArgs e)
		{
			UpdateTheme("Dark");
		}
		private void SwitchThemeLight_Click(object sender, RoutedEventArgs e)
		{
			UpdateTheme("Light");
		}
		private void UpdateTheme(string theme)
		{
			XmlProjectSettings settings = Helpers.GetProjectSettings();
			Uri colorScheme;
			switch (theme)
			{
				case "Light":
					colorScheme = ResourceLocator.LightColorScheme;
					settings.Theme = "Light";
					DarkButton.IsChecked = false;
					LightButton.IsChecked = true;
					break;
				case "Dark":
				default:
					colorScheme = ResourceLocator.DarkColorScheme;
					settings.Theme = "Dark";
					DarkButton.IsChecked = true;
					LightButton.IsChecked = false;
					break;
			}

			ResourceLocator.SetColorScheme(Application.Current.Resources, colorScheme);
			try
			{
				settings.TrySave(ProjectSettingsPath);
			}
			catch (Exception ex)
			{
				Log(LogLevel.Error, $"UpdateTheme -> Failed to save theme settings: {ex.Message}");
			}
		}


		// ************
		// Localization
		private void OnLanguageChanged()
		{
			if (_isDisposed) return;

			Log(LogLevel.Info, "OnLanguageChanged -> Updating localized text.");
			UpdateLocalizedText();
		}
		private void UpdateLocalizedText()
		{
			_viewModel.WindowName = Helpers.TryGetLocalizedText("ID_01_0001", "Phoenix Switcher");
			_viewModel.SettingsText = Helpers.TryGetLocalizedText("ID_01_0002", "Settings");
			_viewModel.ProgramSettingsText = Helpers.TryGetLocalizedText("ID_01_0003", "Program Settings"); 
			_viewModel.LanguageSettingsText = Helpers.TryGetLocalizedText("ID_01_0004", "Languages");
			_viewModel.HelpText = Helpers.TryGetLocalizedText("ID_01_0005", "UpdateBundleFiles");
			_viewModel.AboutText = Helpers.TryGetLocalizedText("ID_01_0006", "UpdateMachineList");
			_viewModel.UpdateText = Helpers.TryGetLocalizedText("ID_01_0007", "UpdateBundleFiles");
			_viewModel.UpdateBundleFilesText = Helpers.TryGetLocalizedText("ID_01_0008", "UpdateBundleFiles");
			_viewModel.UpdateMachineListText = Helpers.TryGetLocalizedText("ID_01_0009", "UpdateMachineList");
			_viewModel.ThemeText = Helpers.TryGetLocalizedText("ID_01_0010", "Theme");
			_viewModel.DarkModeText = Helpers.TryGetLocalizedText("ID_01_0011", "Dark Mode");
			_viewModel.LightModeText = Helpers.TryGetLocalizedText("ID_01_0012", "Light Mode");
			UpdateLanguageMenuSelection();
		}
		private void UpdateLanguageMenuSelection()
		{
			string activeLanguage = LocalizationManager.GetInstance().GetActiveLanguage();
			foreach (MenuItem item in LanguageSettings.Items.OfType<MenuItem>())
			{
				item.IsChecked = string.Equals(item.Header as string, activeLanguage, StringComparison.Ordinal);
			}
		}

		// *****
		// Other
		private void UpdateBundleFiles()
		{
			Log(LogLevel.Info, "UpdateBundleFiles -> Updating bundle files for all controllers.");
			foreach (PhoenixSoftwareUpdater updater in _softwareUpdaters)
			{
				try
				{
					updater.UpdateBundleFiles();
				}
				catch (Exception ex)
				{
					Log(LogLevel.Error, $"UpdateBundleFiles -> Failed to update controller bundle files: {ex.Message}");
					_logManager?.LogEntireException(ex);
				}
			}
		}


		// *******
		// Logging
		private void LogApplicationVersion()
		{
			string version = GetApplicationVersion();
			_logManager?.Log(LogLevel.Spacing, "\n\n\n");
			_logManager?.Log(LogLevel.Info, "________________________________");
			_logManager?.Log(LogLevel.Info, $"PhoenixSwitcher {version}");
		}
		private static string GetApplicationVersion()
		{
			AssemblyName assemblyName = new AssemblyName(Assembly.GetExecutingAssembly().FullName ?? string.Empty);
			Version? version = assemblyName.Version;
			if (version == null) return "Version: 0.0.0.0";
			return $"Version: {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
		}
		private void Log(LogLevel level, string message)
		{
			_logManager?.Log(level, $"MainWindow::{message}");
		}

	}
}