namespace AgiloxSortingHall.Models
{
    public enum RowCallStatus
    {
        Pending = 0,   // Stůl čeká, až paleta přijede
        Delivered = 1, // Doručeno
        Cancelled = 2  // Zrušeno
    }

    public class RowCall
    {
        public int Id { get; set; }

        // Stůl, který volá paletu
        public int WorkTableId { get; set; }
        public WorkTable WorkTable { get; set; } = null!;

        // Informace, z jaké řady se má paleta vzít
        public int HallRowId { get; set; }
        public HallRow HallRow { get; set; } = null!;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public RowCallStatus Status { get; set; } = RowCallStatus.Pending;
    }
}
