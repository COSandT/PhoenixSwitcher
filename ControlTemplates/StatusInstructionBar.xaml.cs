using System.Windows;
using System.Windows.Controls;
using CosntCommonLibrary.Settings;
using CosntCommonLibrary.Tools;
using PhoenixSwitcher.ViewModels;
using static PhoenixSwitcher.ControlTemplates.StatusInstructionBar;

namespace PhoenixSwitcher.ControlTemplates
{
    /// <summary>
    /// Interaction logic for InstructionBar.xaml
    /// </summary>
    public partial class StatusInstructionBar : UserControl
    {
        private Dictionary<long, Status> _activeStatusInstructions = new Dictionary<long, Status>();
        private InstructionBarViewModel _viewModel = new InstructionBarViewModel();
        private Logger? _logger;
        // Simple method to give every statusInstruction its own id for the dictionary.
        // Will just be incremented everytime a new instruction is added.
        static private long lastInstructionID = 0;

        // Struct holding all the status info.
        public struct Status
        {
            public Status(string locaTextID, string fallbackText) 
            {
                LocalizedTextID = locaTextID;
                FallbackText = fallbackText;
            }
            public int PercentageFinished;

            public string LocalizedTextID = "";
            public string FallbackText = "";
        }

        public delegate void RecievedInstructionHandler(Status status);
        public static event RecievedInstructionHandler? OnRecievedInstruction;
        public delegate void FinishedInstructionHandler(int id);
        public static event FinishedInstructionHandler? OnFinishedInstruction;

        public delegate void UpdateStatusPercentageHandler(int id, int percentage);
        public static event UpdateStatusPercentageHandler? OnStatusPercentageUpdated;

        public StatusInstructionBar()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
        }
        public void Init(Logger logger)
        {
            _logger = logger;
            _logger?.LogInfo($"StatusInstructionBar::Init -> Initializing StatusInstructionBar.");

            OnRecievedInstruction += RecievedStatusInstruction;
            OnFinishedInstruction += FinishedStatusInstruction;
            OnStatusPercentageUpdated += UpdateStatusPercentageForStatus;

            // Setup localization for window.
            LocalizationManager.GetInstance().OnActiveLanguageChanged += UpdatStatusInstructionText;
            _logger?.LogInfo($"StatusInstructionBar::Init -> Finished initializing StatusInstructionBar.");

            Status status = new Status("", "TODO: add instructions");
            OnRecievedInstruction.Invoke(status);
        }

        // Adds new instruction to the instruction stack
        private void RecievedStatusInstruction(Status instruction)
        {
            lastInstructionID++;
            _activeStatusInstructions.Add(lastInstructionID, instruction);

            _logger?.LogInfo($"StatusInstructionBar::RecievedStatusInstruction -> Recieved new instruction:\n" +
                $"\tLocalizedTextID: {instruction.LocalizedTextID}" +
                $"\tFallbackText: {instruction.FallbackText}");
            _logger?.LogInfo($"StatusInstructionBar::RecievedStatusInstruction -> Assigned ID: {lastInstructionID}");
            UpdatStatusInstructionText();
        }
        // Removes instruction with specified ID from the instruction stack.
        private void FinishedStatusInstruction(int id)
        {
            _logger?.LogInfo($"StatusInstructionBar::FinishedStatusInstruction -> Removing StatusInstruction with id: {id} from active StausInstruction list");
            if (!_activeStatusInstructions.Remove(id))
            {
                _logger?.LogWarning($"StatusInstructionBar::FinishedStatusInstruction -> No active StatusInstruction found with id: {id}");
            }
            UpdatStatusInstructionText();
        }


        // Updates the percentage of the StatusInstruction with the passed id.
        private void UpdateStatusPercentageForStatus(int id, int percentage)
        {
            _logger?.LogWarning($"StatusInstructionBar::UpdateStatusPercentageForStatus -> Set percentage for StatusInstruction with id: {id}");
            Status foundStatus;
            if (!_activeStatusInstructions.TryGetValue(id, out foundStatus))
            {
                _logger?.LogWarning($"StatusInstructionBar::UpdateStatusPercentageForStatus -> No active StatusInstruction found with id: {id}");
                return;
            }
            foundStatus.PercentageFinished = percentage;
            UpdateStatusPercentage();
        }
        // Updates the StatusPercentage of the progressbar based on first StatusInstruction on the 'stack'
        private void UpdateStatusPercentage()
        {
            _logger?.LogWarning($"StatusInstructionBar::UpdateStatusPercentage -> Updating status percentage for displayed StatusInstruction.");
            _viewModel.StatusPercentage = _activeStatusInstructions.Count > 0
                ? Math.Clamp(_activeStatusInstructions.First().Value.PercentageFinished, 0, 100)
                : 0;
        }
        // Updates the StatusInstruction text based on first StatusInstruction on the 'stack'
        private void UpdatStatusInstructionText()
        {
            _logger?.LogInfo($"StatusInstructionBar::UpdatStatusInstructionText -> Updating StatusInstruction text.");
            if (_activeStatusInstructions.Count <= 0)
            {
                _logger?.LogInfo($"StatusInstructionBar::UpdatStatusInstructionText -> No more active StatusInstructions, leaving text empty.");
                _viewModel.StatusInstructionText = "";
                return;
            }

            _logger?.LogInfo($"StatusInstructionBar::UpdatStatusInstructionText -> Updating text localized text to first statusinstruction in list");
            string fallbackText = _activeStatusInstructions.First().Value.FallbackText;
            string localizedTextId = _activeStatusInstructions.First().Value.LocalizedTextID;
            _viewModel.StatusInstructionText = Helpers.TryGetLocalizedText(localizedTextId, fallbackText);
        }
    }
}
