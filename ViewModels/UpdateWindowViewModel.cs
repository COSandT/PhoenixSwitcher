using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class UpdateWindowViewModel
    {
        public string WindowName { get; set; } = "";
        public string UpdatingText { get; set; } = "";
    }
}
