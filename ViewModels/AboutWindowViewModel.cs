using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class AboutWindowViewModel
    {
        public string WindowName { get; set; } = "";
        public string VersionText { get; set; } = "";
        public string CopyrightText { get; set; } = "";
        public string CreatorText { get; set; } = "";
    }
}
