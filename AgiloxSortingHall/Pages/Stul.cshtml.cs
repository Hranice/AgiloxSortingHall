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

        /// <summary>
        /// Všechny pending call-y pro vizualizaci fronty na møížce.
        /// </summary>
        public List<RowCall> PendingCalls { get; set; } = new();


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

            PendingCalls = await _db.RowCalls
            .Include(c => c.WorkTable)
            .Where(c => c.Status == RowCallStatus.Pending)
            .OrderBy(c => c.RequestedAt)
            .ToListAsync();


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

            var row = await _db.HallRows
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == call.HallRowId);

            if (row != null)
            {
                var rowQueue = await _db.RowCalls
                    .Where(c => c.HallRowId == row.Id && c.Status == RowCallStatus.Pending)
                    .OrderBy(c => c.RequestedAt)
                    .ToListAsync();

                var occupiedSlots = row.Slots
                    .Where(s => s.State == PalletState.Occupied)
                    .OrderBy(s => s.PositionIndex)
                    .ToList();

                var callIndex = rowQueue.FindIndex(c => c.Id == call.Id);

                // Pokud má tahle øada ménì palet než je poøadí callu,
                // tahle konkrétní žádost zatím nemá "svou" fyzickou paletu.
                // V takovém pøípadì fyzicky nic neodebíráme, jen oznaèíme call jako doruèený.
                if (callIndex >= 0 && callIndex < occupiedSlots.Count)
                {
                    var assignedSlot = occupiedSlots[callIndex];

                    assignedSlot.State = PalletState.Empty;
                }
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
