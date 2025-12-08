using AgiloxSortingHall.Data;
using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Helpers;
using AgiloxSortingHall.Models;
using AgiloxSortingHall.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Pages
{
    /// <summary>
    /// Úvodní stránka – pøehled všech stolù a jejich aktuálního stavu.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(AppDbContext db, ILogger<IndexModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Pøehledové položky pro jednotlivé stoly
        /// (stùl + pøípadný pending call).
        /// </summary>
        public List<TableOverviewViewModel> Tables { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Naèteme všechny stoly
            var tables = await _db.WorkTables
                .OrderBy(t => t.Name)
                .ToListAsync();

            // Naèteme všechny pending call-y (vèetnì øady kvùli zobrazení)
            var pendingCalls = await _db.RowCalls
                .Include(c => c.HallRow)
                .Where(c => c.Status == RowCallStatus.Pending)
                .OrderByDescending(c => c.RequestedAt)
                .ToListAsync();

            // Pro každý stùl najdeme jeho pending call (pokud nìjaký má)
            Tables = tables
                .Select(t =>
                {
                    var call = pendingCalls.FirstOrDefault(c => c.WorkTableId == t.Id);
                    return new TableOverviewViewModel
                    {
                        Table = t,
                        PendingCall = call
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Vrátí textový popis aktuální aktivity pro daný RowCall,
        /// založený na OrderId, posledním Agilox statusu a akci.
        /// </summary>
        public string GetActivityDescription(RowCall call)
      => AgiloxActivityDescriptionHelper.GetActivityDescription(call);
       
    }
}
