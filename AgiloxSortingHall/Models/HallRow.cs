namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Reprezentuje jednu fyzickou řadu v hale.
    /// Obsahuje kapacitu, barvu, název a seznam všech slotů (pozic pro palety).
    /// </summary>
    public class HallRow
    {
        /// <summary>
        /// Primární klíč řady.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Název řady zobrazovaný uživateli (např. "Řada1").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Hex barva používaná pro vizuální zvýraznění řady.
        /// </summary>
        public string ColorHex { get; set; } = "#000000";

        /// <summary>
        /// Maximální počet pozic (slotů) v řadě.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Kód artiklu, který se v této řadě aktuálně nachází.
        /// </summary>
        public string Article { get; set; } = string.Empty;

        /// <summary>
        /// Kolekce všech slotů (pozic) v této řadě.
        /// </summary>
        public ICollection<PalletSlot> Slots { get; set; } = new List<PalletSlot>();
    }
}
