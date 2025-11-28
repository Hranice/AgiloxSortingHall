namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Globální nastavení haly.
    /// Ovládá ji skladník.
    /// </summary>
    public class HallSettings
    {
        /// <summary>
        /// Primární klíč (typicky 1).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Zvolená strategie pro výběr řady při volání artiklu ze stolu.
        /// </summary>
        public RowSelectionStrategy RowSelectionStrategy { get; set; }
            = RowSelectionStrategy.MostFreePallets;
    }
}
