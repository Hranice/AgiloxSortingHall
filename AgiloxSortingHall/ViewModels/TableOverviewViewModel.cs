using AgiloxSortingHall.Models;

namespace AgiloxSortingHall.ViewModels
{
    /// <summary>
    /// Přehledová položka pro jeden stůl – obsahuje stůl,
    /// případný pending call a poslední call (libovolného stavu).
    /// </summary>
    public class TableOverviewViewModel
    {
        /// <summary>
        /// Pracovní stůl.
        /// </summary>
        public WorkTable Table { get; set; } = null!;

        /// <summary>
        /// Aktuální čekající (pending) požadavek pro tento stůl, pokud existuje.
        /// </summary>
        public RowCall? PendingCall { get; set; }

        /// <summary>
        /// Poslední RowCall pro tento stůl dle RequestedAt (může být Delivered/Cancelled),
        /// slouží k zobrazení posledního známého stavu (např. pallet_not_found).
        /// </summary>
        public RowCall? LastCall { get; set; }
    }
}
