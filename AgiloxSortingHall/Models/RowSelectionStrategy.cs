namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Způsob, jakým se vybírá konkrétní řada pro daný artikl.
    /// Určuje skladník na své stránce.
    /// </summary>
    public enum RowSelectionStrategy
    {
        /// <summary>
        /// Řada, kde je aktuálně k dispozici nejvíce volných palet
        /// (obsazené sloty mínus již odeslané call-y na Agilox).
        /// </summary>
        MostFreePallets = 0,

        /// <summary>
        /// Nejblíž vlevo – tj. podle pořadí řad zleva.
        /// </summary>
        NearestLeft = 1,

        /// <summary>
        /// Nejblíž vpravo – tj. podle pořadí řad zprava.
        /// </summary>
        NearestRight = 2
    }
}
