using System.Collections.ObjectModel;
using PhoenixSwitcher.Models;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MachineListViewModel
    {
        public string MachineListHeaderText { get; set; } = "";
        public string SelectToScanText { get; set; } = "";
        public bool MachineListExpander { get; set; } = false;

        public ObservableCollection<MachineListItem> ListViewItems { get; set; } = new ObservableCollection<MachineListItem>();
    }
}
