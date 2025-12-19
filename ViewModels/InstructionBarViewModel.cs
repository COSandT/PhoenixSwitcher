using PropertyChanged;

namespace PhoenixSwitcher.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class InstructionBarViewModel
    {
        public int StatusPercentage { get; set; } = 0;
        public string StatusInstructionText { get; set; } = "";
    }
}
