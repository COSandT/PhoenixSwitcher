using System.Windows;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MachineInfoWindowViewModel
    {
        public Visibility StartButtonVisibility { get; set; } = Visibility.Hidden;
        public Visibility FinishButtonVisibility { get; set; } = Visibility.Hidden;

        public string StartButtonText { get; set; } = "";
        public string FinishButtonText { get; set; } = "";

        // Selected Machine Info
        public string SelectedMachineHeaderText { get; set; } = "";
        // Machine Type
        public string MachineNumberDescriptionText { get; set; } = "";
        public string MachineNumberValueText { get; set; } = "";
        // Machine Type
        public string MachineTypeDescriptionText { get; set; } = "";
        public string MachineTypeValueText { get; set; } = "";
        // PCM Type
        public string PCMTypeDescriptionText { get; set; } = "";
        public string PCMTypeValueText { get; set; } = "";
        // PCM Gen
        public string PCMGenDescriptionText { get; set; } = "";
        public string PCMGenValueText { get; set; } = "";
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
