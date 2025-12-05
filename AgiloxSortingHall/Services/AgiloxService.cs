using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Models;
using AgiloxSortingHall.Pages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Services
{
    /// <summary>
    /// Aplikační logika zpracovávající callbacky z Agilox systému
    /// a aktualizující stav řad, palet a fronty RowCall.
    /// </summary>
    public class AgiloxService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AgiloxService> _logger;
        private readonly IHubContext<HallHub> _hub;

        /// <summary>
        /// Inicializuje službu AgiloxService se závislostmi na DB, loggeru a SignalR hubu.
        /// </summary>
        public AgiloxService(ILogger<AgiloxService> logger, AppDbContext db, IHubContext<HallHub> hub)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
        }

        /// <summary>
        /// Zpracuje callback od Agiloxu:
        /// najde odpovídající RowCall podle OrderId,
        /// označí jej jako doručený a uvolní spodní obsazený slot v řadě.
        /// </summary>
        /// <param name="dto">
        /// Data z callbacku obsahující OrderId a případný typ eventu (EventType).
        /// </param>
        public async Task ProcessCallbackAsync(AgiloxCallbackDto dto)
        {
            _logger.LogInformation(
                "Processing Agilox callback: row={Row}, table={Table}, orderid={OrderId}, event={Event}",
                dto.Row, dto.Table, dto.OrderId, dto.EventType);

            var call = await _db.RowCalls
                .Include(c => c.HallRow)
                    .ThenInclude(r => r.Slots)
                .Include(c => c.WorkTable)
                .Where(c =>
                    c.Status == RowCallStatus.Pending &&
                    c.OrderId == dto.OrderId)
                .FirstOrDefaultAsync();

            if (call == null)
            {
                _logger.LogWarning(
                    "No pending RowCall found for OrderId={OrderId} (row={Row}, table={Table}).",
                    dto.OrderId, dto.Row, dto.Table);
                return;
            }

            // uložíme poslední event z Agiloxu
            call.LastAgiloxEvent = dto.EventType;

            // sanity check na jméno řady / stolu
            if (!string.Equals(call.HallRow.Name, dto.Row, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(call.WorkTable.Name, dto.Table, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Agilox callback row/table mismatch. Call has row={RowDb}, table={TableDb}, callback row={RowDto}, table={TableDto}, orderid={OrderId}.",
                    call.HallRow.Name, call.WorkTable.Name,
                    dto.Row, dto.Table,
                    dto.OrderId);
            }

            // pro order_done uvolníme paletu a označíme jako doručené
            if (string.Equals(dto.EventType, "order_done", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(dto.EventType))
            {
                var bottomSlot = call.HallRow.Slots
                    .Where(s => s.State == PalletState.Occupied)
                    .OrderBy(s => s.PositionIndex)
                    .FirstOrDefault();

                if (bottomSlot != null)
                {
                    bottomSlot.State = PalletState.Empty;
                }
                else
                {
                    _logger.LogWarning(
                        "No occupied slot found in row {Row} for RowCall {RowCallId} when processing callback.",
                        call.HallRow.Name, call.Id);
                }

                call.Status = RowCallStatus.Delivered;

                _logger.LogInformation(
                    "RowCall {RowCallId} marked as Delivered. Freed slot {SlotId}.",
                    call.Id, bottomSlot?.Id);
            }

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");
        }





        private void UpdateOrderStatusFromEvent(RowCall call, string? ev)
        {
            if (string.IsNullOrWhiteSpace(ev))
                return;

            call.LastAgiloxEvent = ev;

            switch (ev)
            {
                case "order_created":
                    call.OrderStatus = AgiloxOrderStatus.Created;
                    break;

                case "order_started":
                    call.OrderStatus = AgiloxOrderStatus.Started;
                    break;

                case "station_entered":
                case "station_left":
                case "target_pre_pos_reached":
                case "target_prepre_pos_reached":
                    // tady si můžeš hrát – příklad:
                    call.OrderStatus = AgiloxOrderStatus.PickingUp;
                    break;

                case "target_reached":
                    // třeba "jede ke stolu" vs "na cestě" – podle typu akce
                    call.OrderStatus = AgiloxOrderStatus.DrivingToTable;
                    break;

                case "order_done":
                    call.OrderStatus = AgiloxOrderStatus.Done;
                    break;

                case "order_canceled":
                    call.OrderStatus = AgiloxOrderStatus.Cancelled;
                    break;

                case "no_route":
                case "no_station_left":
                case "timeout":
                case "obstruction_timeout":
                case "max_retries":
                case "overload":
                    call.OrderStatus = AgiloxOrderStatus.Error;
                    break;

                default:
                    // necháme poslední známý stav, jen log
                    _logger.LogInformation("Unhandled Agilox event '{Event}' for RowCall {RowCallId}.", ev, call.Id);
                    break;
            }
        }


    }
}