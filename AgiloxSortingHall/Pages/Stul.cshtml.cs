using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Pages
{
    public class StulModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<StulModel> _logger;
        private readonly IHubContext<HallHub> _hub;

        public StulModel(ILogger<StulModel> logger, AppDbContext db, IHubContext<HallHub> hub)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
        }

        public WorkTable Table { get; set; } = null!;
        public List<HallRow> Rows { get; set; } = new();

        // Aktuální èekající call (pokud existuje)
        public RowCall? PendingCall { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Table = await _db.WorkTables.FindAsync(id)
                ?? throw new Exception("Stùl nenalezen");

            Rows = await _db.HallRows
                .Include(r => r.Slots)
                .OrderBy(r => r.Name)
                .ToListAsync();

            PendingCall = await _db.RowCalls
                .Include(c => c.HallRow)
                .Where(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending)
                .OrderByDescending(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            return Page();
        }

        // Stùl si "zavolá" konkrétní øadu
        public async Task<IActionResult> OnPostCallRowAsync(int id, int rowId)
        {
            // id = WorkTableId (z route), rowId = požadovaná øada
            bool alreadyPending = await _db.RowCalls
                .AnyAsync(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending);

            if (!alreadyPending)
            {
                var call = new RowCall
                {
                    WorkTableId = id,
                    HallRowId = rowId,
                    Status = RowCallStatus.Pending
                };
                _db.RowCalls.Add(call);
                await _db.SaveChangesAsync();

                await _hub.Clients.All.SendAsync("HallUpdated");
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostConfirmDeliveredAsync(int id)
        {
            var call = await _db.RowCalls
                .Where(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending)
                .OrderByDescending(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            if (call == null)
            {
                return RedirectToPage(new { id });
            }

            var bottomSlot = await _db.PalletSlots
                .Where(s => s.HallRowId == call.HallRowId && s.State == PalletState.Occupied)
                .OrderBy(s => s.PositionIndex)  // odspoda nahoru
                .FirstOrDefaultAsync();

            if (bottomSlot != null)
            {
                bottomSlot.State = PalletState.Empty;
            }

            call.Status = RowCallStatus.Delivered;

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage(new { id });
        }


        public async Task<IActionResult> OnPostCancelCallAsync(int id)
        {
            var call = await _db.RowCalls
                .Where(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending)
                .OrderByDescending(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            if (call != null)
            {
                call.Status = RowCallStatus.Cancelled;
                await _db.SaveChangesAsync();
            }

            return RedirectToPage(new { id });
        }
    }
}
