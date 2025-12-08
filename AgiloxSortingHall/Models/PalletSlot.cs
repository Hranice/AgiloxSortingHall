using AgiloxSortingHall.Enums;

namespace AgiloxSortingHall.Models
{
    /// <summary>
    /// Jedna pozice ve sloupci řady.
    /// Obsahuje index pozice a informaci o aktuálním stavu.
    /// </summary>
    public class PalletSlot
    {
        /// <summary>
        /// Primární klíč slotu.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Cizí klíč na řadu, ke které tento slot patří.
        /// </summary>
        public int HallRowId { get; set; }

        /// <summary>
        /// Navigační vlastnost na řadu, ke které slot patří.
        /// </summary>
        public HallRow HallRow { get; set; } = null!;

        /// <summary>
        /// Index pozice ve sloupci (0 = spodní pozice).
        /// </summary>
        public int PositionIndex { get; set; }

        /// <summary>
        /// Aktuální stav pozice (prázdná, obsazená, v převozu).
        /// </summary>
        public PalletState State { get; set; } = PalletState.Empty;
    }
}
