using System;

namespace PaperclipPerfector
{
    public class RefreshContext
    {
        private Action action;

        public void SetCallback(Action action)
        {
            this.action = action;
        }

        public void TriggerCallback()
        {
            action?.Invoke();
        }
    }
}
