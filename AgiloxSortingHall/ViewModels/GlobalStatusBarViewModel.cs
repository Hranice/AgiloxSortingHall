namespace AgiloxSortingHall.ViewModels
{
    public class GlobalStatusBarViewModel
    {
        public bool HasActive { get; set; }

        public string? RowName { get; set; }

        public string? Article { get; set; }

        public string? TableName { get; set; }

        public long? OrderId { get; set; }

        public string ActivityDescription { get; set; } = "";
    }
}
