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

    /// <summary>
    /// Reprezentuje požadavek pracovního stolu na paletu z konkrétní řady.
    /// Sleduje stav, čas požadavku a identifikátor requestu odeslaného na Agilox.
    /// </summary>
    public class RowCall
    {
        /// <summary>
        /// Primární klíč RowCallu.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID stolu, který vyvolal požadavek.
        /// </summary>
        public int WorkTableId { get; set; }

        /// <summary>
        /// Navigační vlastnost na stůl, který požadavek vytvořil.
        /// </summary>
        public WorkTable WorkTable { get; set; } = null!;

        /// <summary>
        /// ID řady, ze které se má paleta odebrat.
        /// </summary>
        public int HallRowId { get; set; }

        /// <summary>
        /// Navigační vlastnost na cílovou řadu.
        /// </summary>
        public HallRow HallRow { get; set; } = null!;

        /// <summary>
        /// Datum a čas vytvoření požadavku (UTC).
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Aktuální stav požadavku (čeká, doručeno, zrušeno).
        /// </summary>
        public RowCallStatus Status { get; set; } = RowCallStatus.Pending;

        /// <summary>
        /// Identifikátor workflow odeslaného na Agilox,
        /// sloužící ke spárování s callbackem
        /// (generován lokálně při odeslání požadavku).
        /// </summary>
        public string? RequestId { get; set; } = null!;
    }

}
