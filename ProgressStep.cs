using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixSwitcher
{
    public class ProgressStep
    {
        private List<ProgressStep> _requiredCompletedSteps;
        //private List<object>
        private bool _bIsCompleted;
        public bool bIsCompleted { get { return _bIsCompleted; } }
        public string StepDescription { get; private set; }

        public ProgressStep(List<ProgressStep> requiredSteps, string description) 
        {
            _requiredCompletedSteps = requiredSteps;
            StepDescription = description;
        }

        public void Activate()
        {
            bool bCanStart = true;
            foreach (ProgressStep step in _requiredCompletedSteps)
            {
                if (!step.bIsCompleted)
                {
                    bCanStart = false;
                    break;
                }
            }
            if (bCanStart)
            {

            }
        }
    }
}
