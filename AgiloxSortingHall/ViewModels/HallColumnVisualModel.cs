using AgiloxSortingHall.Models;

namespace AgiloxSortingHall.ViewModels
{
    /// <summary>
    /// ViewModel pro vizualizaci jednoho sloupce (řady) v hale.
    /// Používají ho jak stránka skladníka, tak stránka stolu.
    /// </summary>
    public class HallColumnVisualModel
    {
        /// <summary>
        /// Řada, kterou vykreslujeme (např. Řada1).
        /// </summary>
        public HallRow Row { get; set; } = null!;

        /// <summary>
        /// Maximální počet pozic v jakékoliv řadě, kvůli kreslení
        /// shora dolů stejné výšky.
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// Všechny pending RowCall požadavky v systému (pro všechny řady).
        /// Komponenta si z nich sama vyfiltruje ty pro danou řadu.
        /// </summary>
        public List<RowCall> PendingCalls { get; set; } = new();

        /// <summary>
        /// ID aktuálního stolu – pro zvýraznění v popiscích.
        /// Pokud není potřeba zvýrazňovat, nechává se null.
        /// </summary>
        public int? CurrentTableId { get; set; }
    }
}
