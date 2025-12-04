namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// DTO objekt reprezentující callback zprávu posílanou systémem Agilox.
    /// Obsahuje identifikaci řady, stolu, OrderId a volitelné sériové číslo.
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
    }
}
