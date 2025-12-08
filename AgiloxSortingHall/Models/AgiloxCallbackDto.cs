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
        /// Identifikátor objednávky (workflow ID) vygenerovaný Agiloxem.
        /// Slouží k jednoznačné identifikaci RowCall zadaného na backendu.
        /// </summary>
        [JsonPropertyName("orderid")]
        public long OrderId { get; set; }

        /// <summary>
        /// Kategorie události popisující fázi workflow (např. <c>drop</c>, <c>picup</c>).
        /// </summary>
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        /// <summary>
        /// Reference na zakliknutou řadu, ze které se aktuálně doručuje paleta.
        /// (např. <c>Řada 1</c>, <c>Řada 2</c>, apod.).
        /// </summary>
        [JsonPropertyName("row")]
        public string? Row { get; set; }

        /// <summary>
        /// Reference na zakliknutou stůl, pro který se aktuálně doručuje paleta.
        /// (např. <c>Stůl 1</c>, <c>Stůl 2</c>, apod.).
        /// </summary>
        [JsonPropertyName("table")]
        public string? Table { get; set; }

        /// <summary>
        /// Aktuální status zaslaný Agiloxem (např. <c>ok</c>, 
        /// <c>pallet_not_found</c>, <c>occupied</c>, apod.).
        /// Pomáhá určit, ve které fázi zpracování se aktuální order nachází.
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
