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
        /// <param name="dto">Data z callbacku obsahující OrderId.</param>
        public async Task ProcessCallbackAsync(AgiloxCallbackDto dto)
        {
            _logger.LogInformation(
                "Processing Agilox callback: row={Row}, table={Table}",
                dto.row, dto.table);

            // najdeme pending RowCall pro daný stůl a řadu
            var call = await _db.RowCalls
                .Include(c => c.HallRow)
                    .ThenInclude(r => r.Slots)
                .Include(c => c.WorkTable)
                .Where(c =>
                    c.Status == RowCallStatus.Pending &&
                    c.WorkTable.Name == dto.table &&
                    c.HallRow.Name == dto.row)
                .OrderByDescending(c => c.RequestedAt) // pro jistotu, kdyby jich tam někdy bylo víc
                .FirstOrDefaultAsync();

            if (call == null)
            {
                _logger.LogWarning(
                    "No pending RowCall found for callback row={Row}, table={Table}.",
                    dto.row, dto.table);
                return;
            }

            // odebereme spodní paletu v dané řadě
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

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");
        }

    }
}