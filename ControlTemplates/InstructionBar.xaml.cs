using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CosntCommonLibrary.Settings;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for InstructionBar.xaml
    /// </summary>
    public partial class InstructionBar : UserControl
    {
        private InstructionBarViewModel _viewModel = new InstructionBarViewModel();
        private string _instructionTextID = "";
        public InstructionBar()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += UpdateInstructionText;
            UpdateInstructionText();
        }

        private void UpdateInstructionText()
        {
            try
            {
                LocalizationManager localizationManager = LocalizationManager.GetInstance();
                if (localizationManager == null) throw new Exception("Cannot get LocalizationManager");

                _viewModel.InstructionText = localizationManager.GetEntryForActiveLanguage(_instructionTextID, "instruction");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void SetInstructionTextID(string newTextID)
        {
            _instructionTextID = newTextID;
            UpdateInstructionText();
        }
    }
}
