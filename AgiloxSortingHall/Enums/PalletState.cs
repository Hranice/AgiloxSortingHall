namespace AgiloxSortingHall.Enums
{
    /// <summary>
    /// Stav jedné pozice v řadě.
    /// </summary>
    public enum PalletState
    {
        /// <summary>
        /// Pozice je prázdná.
        /// </summary>
        Empty = 0,

        /// <summary>
        /// Pozice obsahuje paletu.
        /// </summary>
        Occupied = 1,

        /// <summary>
        /// Paleta z této pozice je právě v převozu.
        /// </summary>
        InTransit = 2
    }
}
