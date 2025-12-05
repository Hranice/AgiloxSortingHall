namespace AgiloxSortingHall.Models
{
    public enum AgiloxOrderStatus
    {
        Created,            // objednávka vytvořena na Agiloxu
        Started,            // order_started
        PickingUp,          // někde kolem station_entered / target_reached pickup
        DrivingToTable,     // jede na místo dropu
        Delivering,         // řeší drop
        Done,               // order_done
        Cancelled,
        Error
    }

}
