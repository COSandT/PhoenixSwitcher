using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MachineListViewModel
    {
        public string MachineListHeaderText { get; set; } = "";
        public string SelectToScanText { get; set; } = "";
        public bool MachineListExpander { get; set; } = false;
    }
}
