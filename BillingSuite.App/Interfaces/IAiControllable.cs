using System.Collections.Generic;
using System.Threading.Tasks;

namespace BillingSuite.App.Interfaces
{
    public interface IAiControllable
    {
        /// <summary>
        /// Gets the current state of the form for the AI to understand.
        /// </summary>
        object GetAiContext();

        /// <summary>
        /// Performs an action suggested by the AI directly on the form.
        /// </summary>
        Task<bool> PerformAiAction(string action, object data);
    }
}
