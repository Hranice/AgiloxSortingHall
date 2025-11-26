namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// DTO objekt reprezentující callback zprávu posílanou systémem Agilox.
    /// Obsahuje identifikaci řady, stolu, requestId a volitelné sériové číslo.
    /// </summary>
    public class AgiloxCallbackDto
    {
        /// <summary>
        /// Název řady, ke které se callback vztahuje (např. "Řada3").
        /// </summary>
        public string row { get; set; } = "";

        /// <summary>
        /// Název pracovního stolu, ke kterému se doručovala paleta.
        /// </summary>
        public string table { get; set; } = "";

        /// <summary>
        /// Identifikátor požadavku, který byl vygenerován
        /// (lokálně) při odeslání na Agilox a vrací se v callbacku pro spárování.
        /// </summary>
        public string? requestId { get; set; }

        /// <summary>
        /// Volitelné sériové číslo Agiloxe,
        /// pokud jej Agilox ve zprávě poskytuje.
        /// </summary>
        public long? serial { get; set; }
    }
}
