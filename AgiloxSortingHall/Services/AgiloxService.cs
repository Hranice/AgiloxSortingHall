using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Models;
using AgiloxSortingHall.Pages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Services
{
    public class AgiloxService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<StulModel> _logger;
        private readonly IHubContext<HallHub> _hub;

        public AgiloxService(ILogger<StulModel> logger, AppDbContext db, IHubContext<HallHub> hub)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
        }

        public async Task ProcessCallbackAsync(AgiloxCallbackDto dto)
        {
            var call = await _db.RowCalls
                .Include(c => c.HallRow)
                    .ThenInclude(r => r.Slots)
                .Include(c => c.WorkTable)
                .Where(c =>
                    c.Status == RowCallStatus.Pending &&
                    c.RequestId == dto.requestId)
                .FirstOrDefaultAsync();

            if (call == null)
            {
                return;
            }

            // odebereme spodní paletu v dané řadě
            var bottomSlot = call.HallRow.Slots
                .Where(s => s.State == PalletState.Occupied)
                .OrderBy(s => s.PositionIndex)
                .FirstOrDefault();

            if (bottomSlot != null)
                bottomSlot.State = PalletState.Empty;

            call.Status = RowCallStatus.Delivered;

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");
        }
    }
}