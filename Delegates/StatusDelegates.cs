using PhoenixSwitcher.Phoenix;
using PhoenixSwitcher.ControlTemplates;

namespace PhoenixSwitcher.Delegates
{
    public class StatusDelegates
	{
		public static event Action<PhoenixSwitcherLogic?, StatusLevel, string, string>? OnLocaStatusTextUpdated;
		public static event Action<PhoenixSwitcherLogic?, StatusLevel, string>? OnStatusTextUpdated;
		public static event Action<PhoenixSwitcherLogic?, StatusLevel>? OnStatusCleared;

        public static void UpdateStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, string locaTextId, string fallbackText) { OnLocaStatusTextUpdated?.Invoke(switcherLogic, level, locaTextId, fallbackText); }
        public static void UpdateStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level, string text) { OnStatusTextUpdated?.Invoke(switcherLogic, level, text); }
        public static void ClearStatus(PhoenixSwitcherLogic? switcherLogic, StatusLevel level) { OnStatusCleared?.Invoke(switcherLogic, level); }

    }
}
