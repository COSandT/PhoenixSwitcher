

namespace PhoenixSwitcher.Delegates
{
    public enum StatusLevel
    {
        Instruction,
        Status,
        Error,
    }
    public class StatusDelegates
    {
        public delegate void UpdateStatusTextHandler(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, string locaTextId, string fallbackText);
        public static event UpdateStatusTextHandler? OnStatusTextUpdated;
        public static void UpdateStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, string locaTextId, string fallbackText) { OnStatusTextUpdated?.Invoke(switcherLogic, level, locaTextId, fallbackText); }


        public delegate void UpdateStatusPercentageHandler(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, int newPercentage);
        public static event UpdateStatusPercentageHandler? OnStatusPercentageUpdated;
        public static void UpdateStatusPercentage(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, int percentage) { OnStatusPercentageUpdated?.Invoke(switcherLogic, level, percentage); }


        public delegate void ClearStatusHandler(PhoenixSwitcherLogic? switcherLogic, StatusLevel level);
        public static event ClearStatusHandler? OnStatusCleared;
        public static void ClearStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level) { OnStatusCleared?.Invoke(switcherLogic, level); }

    }
}
