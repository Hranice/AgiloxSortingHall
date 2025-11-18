namespace AgiloxSortingHall.Models
{
    public class HallRow
    {
        public int Id { get; set; }

        // Např. "Řada1", "Řada2" – zobrazí se nahoře nad sloupcem
        public string Name { get; set; } = string.Empty;

        // Barva rámečku řady (např. #0078D7), jen vizuální informace
        public string ColorHex { get; set; } = "#000000";

        // Počet pozic v řadě (počet čtverečků ve sloupci)
        public int Capacity { get; set; }

        // Např. "14124", "13134"
        public string Article { get; set; } = string.Empty;

        // Navigační vlastnost – všechny palety v této řadě
        public ICollection<PalletSlot> Slots { get; set; } = new List<PalletSlot>();
    }
}
