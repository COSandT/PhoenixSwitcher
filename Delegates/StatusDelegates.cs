

namespace PhoenixSwitcher.Delegates
{
    public enum StatusLevel
    {
        Main,
        L1,
        L2,
    }
    public class StatusDelegates
    {
        public delegate void UpdateStatusTextHandler(StatusLevel level, string locaTextId, string fallbackText);
        public static event UpdateStatusTextHandler? OnStatusTextUpdated;
        public static void UpdateStatus(StatusLevel level, string locaTextId, string fallbackText) { OnStatusTextUpdated?.Invoke(level, locaTextId, fallbackText); }


        public delegate void UpdateStatusPercentageHandler(StatusLevel level, int newPercentage);
        public static event UpdateStatusPercentageHandler? OnStatusPercentageUpdated;
        public static void UpdateStatusPercentage(StatusLevel level, int percentage) { OnStatusPercentageUpdated?.Invoke(level, percentage); }


        public delegate void ClearStatusHandler(StatusLevel level);
        public static event ClearStatusHandler? OnStatusCleared;
        public static void ClearStatus(StatusLevel level) { OnStatusCleared?.Invoke(level); }

    }
}
