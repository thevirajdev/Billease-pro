using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using BillingSuite.App.Interfaces;

namespace BillingSuite.App.Services
{
    public static class AiContextManager
    {
        private static IAiControllable? _activeForm;
        public static event Action<IAiControllable?>? ActiveFormChanged;

        /// <summary>
        /// Registers a form as the current active AI context.
        /// </summary>
        public static void RegisterActiveForm(IAiControllable form)
        {
            _activeForm = form;
            ActiveFormChanged?.Invoke(form);
            
            if (form is Form f)
            {
                f.FormClosing += (s, e) => {
                    if (_activeForm == form)
                    {
                        _activeForm = null;
                        ActiveFormChanged?.Invoke(null);
                    }
                };
            }
        }

        /// <summary>
        /// Unregisters a form from being the active context.
        /// </summary>
        public static void UnregisterActiveForm(IAiControllable form)
        {
            if (_activeForm == form)
            {
                _activeForm = null;
                ActiveFormChanged?.Invoke(null);
            }
        }

        /// <summary>
        /// Gets the current AI-friendly state of the active form.
        /// </summary>
        public static object? GetCurrentContextData()
        {
            return _activeForm?.GetAiContext();
        }

        /// <summary>
        /// Attempts to perform an AI-requested action on the active form.
        /// </summary>
        public static async System.Threading.Tasks.Task<bool> PerformActiveActionAsync(string action, object data)
        {
            if (_activeForm == null) return false;
            return await _activeForm.PerformAiAction(action, data);
        }

        /// <summary>
        /// Gets the name of the currently active form.
        /// </summary>
        public static string GetActiveFormName()
        {
            return (_activeForm as Form)?.Name ?? (_activeForm as Form)?.Text ?? "Main Dashboard";
        }
    }
}
