using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class InstructionBarViewModel
    {
        public string InstructionText { get; set; } = "";
    }
}
