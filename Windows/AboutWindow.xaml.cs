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
using System.Windows.Shapes;
using CosntCommonLibrary.Settings;
using PhoenixSwitcher.ViewModels;

namespace PhoenixSwitcher.Windows
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        private AboutWindowViewModel _viewModel = new AboutWindowViewModel();
        public AboutWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;

            LocalizationManager.GetInstance().OnActiveLanguageChanged += OnLanguageChanged;
            OnLanguageChanged();
        }
        private void OnLanguageChanged()
        {
            _viewModel.WindowName = Helpers.TryGetLocalizedText("TODO: LOCA", "Phoenix Switcher - About");
            _viewModel.VersionText = $"{Helpers.TryGetLocalizedText("TODO: LOCA", "Version:")} {Version.VersionNum}";

            DateTime now = DateTime.Now;
            _viewModel.CopyrightText = $"Copyright 2025-{now.Year} COS&T";
            _viewModel.CreatorText = $"{Helpers.TryGetLocalizedText("TODO: LOCA", "Designed by:")} Miel Vandewal";
        }
    }
}
