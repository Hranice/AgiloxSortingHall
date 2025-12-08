namespace AgiloxSortingHall.Enums
{
    /// <summary>
    /// Typ akce v callbacku z Agiloxu.
    /// Odpovídá hodnotám v JSON poli <c>action</c> (např. "pickup", "drop").
    /// </summary>
    public enum AgiloxAction
    {
        /// <summary>
        /// Neznámá nebo neprázdná hodnota action.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Akce vyzvednutí palety (pickup).
        /// </summary>
        Pickup = 1,

        /// <summary>
        /// Akce vyložení palety (drop).
        /// </summary>
        Drop = 2
    }
}
