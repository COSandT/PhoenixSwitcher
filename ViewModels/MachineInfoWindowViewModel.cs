using System.Windows;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MachineInfoWindowViewModel
    {
        public Visibility StartButtonVisibility { get; set; } = Visibility.Hidden;
        public Visibility TestButtonVisibility { get; set; } = Visibility.Hidden;
        public Visibility RetryButtonVisibility { get; set; } = Visibility.Hidden;
        public Visibility ShutDownPhoenixButtonVisibility { get; set; } = Visibility.Hidden;
        public Visibility FinishButtonVisibility { get; set; } = Visibility.Hidden;

        public string StartButtonText { get; set; } = "";
        public string TestButtonText { get; set; } = "";
        public string RetryButtonText { get; set; } = "";
        public string ShutDownPhoenixText { get; set; } = "";
        public string FinishButtonText { get; set; } = "";

        // Selected Machine Info
        public string ControllerBoxName { get; set; } = "";
        public string SelectedMachineHeaderText { get; set; } = "";

        // Machine Number Long
        public string MachineN17DescriptionText { get; set; } = "";
        public string MachineN17ValueText { get; set; } = "";
        // Machine Number Short
        public string MachineN9DescriptionText { get; set; } = "";
        public string MachineN9ValueText { get; set; } = "";
        // Machine Type
        public string MachineTypeDescriptionText { get; set; } = "";
        public string MachineTypeValueText { get; set; } = "";
        // Series Type
        public string SeriesDescriptionText { get; set; } = "";
        public string SeriesValueText { get; set; } = "";
        // Display Type
        public string DisplayTypeDescriptionText { get; set; } = "";
        public string DisplayTypeValueText { get; set; } = "";
        // VAN
        public string VANDescriptionText { get; set; } = "";
        public string VANValueText { get; set; } = "";
        // Bundle
        public string BundleDescriptionText { get; set; } = "";
        public string BundleValueText { get; set; } = "";

    }
}
