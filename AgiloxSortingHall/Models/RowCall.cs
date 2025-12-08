using AgiloxSortingHall.Enums;

namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Reprezentuje požadavek pracovního stolu na paletu z konkrétní řady.
    /// Uchovává metadata o tom, kdy byl vytvořen, jeho aktuální stav
    /// a referenci na objednávku odeslanou do systému Agilox.
    /// </summary>
    public class RowCall
    {
        /// <summary>
        /// Primární klíč RowCallu.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID pracovního stolu, který požadavek vytvořil.
        /// </summary>
        public int WorkTableId { get; set; }

        /// <summary>
        /// Navigační vlastnost na pracovní stůl,
        /// ze kterého byl požadavek odeslán.
        /// </summary>
        public WorkTable WorkTable { get; set; } = null!;

        /// <summary>
        /// ID řady, ze které má být naložena paleta.
        /// </summary>
        public int HallRowId { get; set; }

        /// <summary>
        /// Navigační vlastnost na řadu, odkud má být paleta odebrána.
        /// </summary>
        public HallRow HallRow { get; set; } = null!;

        /// <summary>
        /// Datum a čas vytvoření požadavku (UTC).
        /// Pomáhá seřadit čekající požadavky a zobrazovat je v UI.
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stav požadavku — čeká, doručeno nebo zrušeno.
        /// Odráží aktuální výsledek zpracování workflow na straně Agiloxu.
        /// </summary>
        public RowCallStatus Status { get; set; } = RowCallStatus.Pending;

        /// <summary>
        /// Identifikátor objednávky (workflow ID) vygenerovaný systémem Agilox.
        /// Tento identifikátor používáme ke spárování callbacků s konkrétním požadavkem.
        /// Pokud je <c>null</c>, požadavek ještě nebyl odeslán do Agiloxu.
        /// </summary>
        public long? OrderId { get; set; } = null!;

        /// <summary>
        /// Poslední akce (action) zaslaná v Agilox callbacku
        /// (např. <c>pickup</c> nebo <c>drop</c>).
        /// Tato informace doplňuje <see cref="LastAgiloxStatus"/>,
        /// protože status <c>ok</c> může znamenat různé situace podle typu akce.
        /// Díky tomu dokážeme jednoznačně určit, zda bylo workflow v kroku naložení,
        /// nebo vyložení palety.
        /// </summary>
        public string? LastAgiloxAction { get; set; }

        /// <summary>
        /// Poslední status zaslaný z Agiloxu v callbacku
        /// (např. <c>ok</c>, <c>pallet_not_found</c>, <c>occupied</c>, <c>order_canceled</c>).
        /// Ukládá se přesně tak, jak přišel v JSON payloadu, pro účely zobrazení,
        /// diagnostiky a historického auditu.
        /// </summary>
        public string? LastAgiloxStatus { get; set; }
    }
}
