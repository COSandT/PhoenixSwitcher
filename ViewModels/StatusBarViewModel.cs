using System.Windows;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class StatusBarViewModel
    {
        public string MainStatusText { get; set; } = "";
        public int MainStatusPercentage { get; set; } = 0;
    }
}
