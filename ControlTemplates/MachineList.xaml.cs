using System.Windows.Controls;
using System.Windows.Input;
using AdonisUI.Controls;
using CosntCommonLibrary.Settings;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{

    public partial class MachineList : UserControl
    {
        private MachineListViewModel _viewModel = new MachineListViewModel();

        public MachineList()
        {
            InitializeComponent();

            this.DataContext = _viewModel;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            // Call once to setup initial language.
            OnLanguageChanged();
        }

        private void OnLanguageChanged()
        {
            try
            {
                LocalizationManager localizationManager = LocalizationManager.GetInstance();
                if (localizationManager == null) throw new Exception("Cannot get LocalizationManager");

                _viewModel.MachineListHeaderText = localizationManager.GetEntryForActiveLanguage("ID_01_0002", "Mach Li");
                _viewModel.SelectToScanText = localizationManager.GetEntryForActiveLanguage("ID_01_0004", "-- Scan --");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ScannedMachineText_KeyUp(object sender, KeyEventArgs e)
        {

        }
    }
}
