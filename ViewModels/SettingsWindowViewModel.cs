using System.Collections.ObjectModel;
using CosntCommonViewLibrary;
using CosntCommonViewLibrary.SettingsControl.Models;
using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    class SettingsWindowViewModel
    {
        public string WindowName { get; set; } = "";
        public string FileText { get; set; } = "";
        public string SaveButtonText { get; set; } = "";
        public ObservableCollection<TabbItemModelReference> SettingsItemList { get; } = new ObservableCollection<TabbItemModelReference>();
    }
}
