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
    }
}
