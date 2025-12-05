using System.Text.Json.Serialization;

namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// DTO objekt reprezentující callback zprávu posílanou systémem Agilox.
    /// Obsahuje identifikaci řady, stolu, OrderId a typ události (event),
    /// která popisuje, v jaké fázi se aktuálně nachází workflow.
    /// </summary>
    public class AgiloxCallbackDto
    {
        /// <summary>
        /// Název řady, ke které se callback vztahuje.
        /// Odpovídá hodnotě proměnné <c>@ZAKLIKNUTARADA</c> odeslané ve workflow.
        /// </summary>
        [JsonPropertyName("row")]
        public string Row { get; set; } = "";

        /// <summary>
        /// Název pracovního stolu, pro který byla paleta doručována.
        /// Odpovídá hodnotě proměnné <c>@PRIJEMCE</c> použití ve workflow.
        /// </summary>
        [JsonPropertyName("table")]
        public string Table { get; set; } = "";

        /// <summary>
        /// Identifikátor objednávky (workflow ID) vygenerovaný Agiloxem.
        /// Slouží k jednoznačné identifikaci RowCall zadaného na backendu.
        /// </summary>
        [JsonPropertyName("orderid")]
        public long OrderId { get; set; }

        /// <summary>
        /// Typ události zaslané Agiloxem (např. <c>order_started</c>, 
        /// <c>station_left</c>, <c>order_done</c>, apod.).
        /// Pomáhá určit, ve které fázi zpracování se aktuální order nachází.
        /// </summary>
        [JsonPropertyName("event")]
        public string? EventType { get; set; }
    }
}
