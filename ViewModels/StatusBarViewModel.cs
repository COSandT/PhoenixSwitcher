using System.Windows.Media;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class StatusBarViewModel
    {
        public string MainStatusText { get; set; } = "";
        public int MainStatusPercentage { get; set; } = 0;
        public SolidColorBrush StatusColor { get; set; } = Brushes.White;
    }
}
