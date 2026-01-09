using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            _viewModel.WindowName = Helpers.TryGetLocalizedText("ID_07_0001", "Phoenix Switcher - About");

            AssemblyName ExecutingAssemblyName = new AssemblyName(Assembly.GetExecutingAssembly().FullName ?? "");
            string Major = "0", Minor = "0", Build = "0", Revision = "0";
            if (ExecutingAssemblyName.Version != null)
            {
                Major = ExecutingAssemblyName.Version.Major.ToString();
                Minor = ExecutingAssemblyName.Version.Minor.ToString();
                Build = ExecutingAssemblyName.Version.Build.ToString();
                Revision = ExecutingAssemblyName.Version.Revision.ToString();
            }
            _viewModel.VersionText = $"{Helpers.TryGetLocalizedText("ID_07_0002", "Phoenix Switcher:")} {Major}.{Minor}.{Build}.{Revision}";

            DateTime now = DateTime.Now;
            _viewModel.CopyrightText = $"Copyright 2025-{now.Year} COS&T";
            _viewModel.CreatorText = $"{Helpers.TryGetLocalizedText("ID_07_0003", "Designed by:")} Miel Vandewal";
        }
    }
}
