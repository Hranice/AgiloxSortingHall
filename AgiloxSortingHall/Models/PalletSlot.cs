namespace AgiloxSortingHall.Models
{
    public enum PalletState
    {
        Empty = 0,    // Prázdná pozice
        Occupied = 1  // Je na ní nějaká paleta
    }

    public class PalletSlot
    {
        public int Id { get; set; }

        public int HallRowId { get; set; }
        public HallRow HallRow { get; set; } = null!;

        // Pořadí ve sloupci (0 = spodní level, pak nahoru)
        public int PositionIndex { get; set; }

        // Aktuální stav místa
        public PalletState State { get; set; } = PalletState.Empty;
    }
}
