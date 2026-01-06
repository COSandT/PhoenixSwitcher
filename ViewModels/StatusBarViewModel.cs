using System.Windows;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class StatusBarViewModel
    {
        public string MainStatusText { get; set; } = "";
        public string L1StatusText { get; set; } = "";
        public string L2StatusText { get; set; } = "";

        public int MainStatusPercentage { get; set; } = 0;
        public int L1StatusPercentage { get; set; } = 0;
        public int L2StatusPercentage { get; set; } = 0;

        public Visibility L1StatusVisibility { get; set; } = Visibility.Visible;
        public Visibility L2StatusVisibility { get; set; } = Visibility.Collapsed;
    }
}
