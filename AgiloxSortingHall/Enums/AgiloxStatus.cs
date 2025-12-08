namespace AgiloxSortingHall.Enums
{
    /// <summary>
    /// Status callbacku z Agiloxu.
    /// Odpovídá hodnotám v JSON poli <c>status</c> (např. "ok", "pallet_not_found", "occupied").
    /// </summary>
    public enum AgiloxStatus
    {
        /// <summary>
        /// Neznámá nebo neprázdná hodnota status.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Akce proběhla v pořádku (ok).
        /// </summary>
        Ok = 1,

        /// <summary>
        /// Paleta nebyla nalezena nebo je mimo toleranci (pallet_not_found).
        /// </summary>
        PalletNotFound = 2,

        /// <summary>
        /// Cílové místo je obsazené (occupied).
        /// </summary>
        Occupied = 3,

        /// <summary>
        /// Order byl zrušen (order_canceled).
        /// </summary>
        OrderCanceled = 4
    }
}
