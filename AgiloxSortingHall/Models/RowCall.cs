using AgiloxSortingHall.Enums;

namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Reprezentuje požadavek pracovního stolu.
    /// Buď:
    /// - "řada -> stůl" (HallRowId vyplněné)
    /// - nebo "stůl -> pryč" (např. dokončení / odvoz od stolu, HallRowId může být null).
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
        /// Pro "normální" call řada->stůl je vyplněné,
        /// pro "Hotovo" (odvoz od stolu) může být null.
        /// </summary>
        public int? HallRowId { get; set; }

        /// <summary>
        /// Navigační vlastnost na řadu.
        /// Může být null u požadavků typu "stůl -> pryč".
        /// </summary>
        public HallRow? HallRow { get; set; }

        /// <summary>
        /// Datum a čas vytvoření požadavku (UTC).
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Aktuální stav požadavku (čeká, doručeno, zrušeno).
        /// </summary>
        public RowCallStatus Status { get; set; } = RowCallStatus.Pending;

        /// <summary>
        /// Identifikátor order vráceného od Agiloxe,
        /// sloužící ke spárování s callbackem
        /// (generován Agiloxem po odeslání požadavku).
        /// </summary>
        public long? OrderId { get; set; }

        /// <summary>
        /// Poslední status, který Agilox poslal v callbacku (AgiloxStatus).
        /// </summary>
        public string? LastAgiloxStatus { get; set; }

        /// <summary>
        /// Poslední akce, kterou Agilox nahlásil v callbacku (AgiloxAction).
        /// </summary>
        public string? LastAgiloxAction { get; set; }
    }
}
