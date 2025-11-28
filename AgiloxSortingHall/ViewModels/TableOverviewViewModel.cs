using AgiloxSortingHall.Models;

namespace AgiloxSortingHall.ViewModels
{
    /// <summary>
    /// Přehled jednoho stolu pro úvodní stránku:
    /// samotný stůl + případný čekající požadavek (RowCall).
    /// </summary>
    public class TableOverviewViewModel
    {
        /// <summary>
        /// Entita stolu z databáze.
        /// </summary>
        public WorkTable Table { get; set; } = null!;

        /// <summary>
        /// Nejnovější pending call pro tento stůl (pokud existuje).
        /// Stůl má v logice aplikace max. jeden pending call.
        /// </summary>
        public RowCall? PendingCall { get; set; }
    }
}
