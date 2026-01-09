using System.Windows;
using CosntCommonLibrary.Settings;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.Windows
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        private UpdateWindowViewModel _viewModel = new UpdateWindowViewModel();
        public UpdateWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();
        }
        private void OnLanguageChanged()
        {
            _viewModel.WindowName = Helpers.TryGetLocalizedText("ID_09_0001", "Update Window");
            _viewModel.UpdatingText = Helpers.TryGetLocalizedText("ID_09_0002", "Updating bundles, please wait...");
        }
    }
}
