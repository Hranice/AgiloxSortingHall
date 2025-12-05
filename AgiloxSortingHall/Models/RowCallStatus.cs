namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Stav požadavku na paletu – čeká, doručeno, zrušeno.
    /// </summary>
    public enum RowCallStatus
    {
        /// <summary>
        /// Požadavek čeká na vyřízení (nebo na doručení).
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Požadavek byl úspěšně doručen.
        /// </summary>
        Delivered = 1,

        /// <summary>
        /// Požadavek byl zrušen uživatelem nebo systémem.
        /// Funfact: Angláni prý používají cancelled namísto canceled.
        /// </summary>
        Cancelled = 2
    }
}
