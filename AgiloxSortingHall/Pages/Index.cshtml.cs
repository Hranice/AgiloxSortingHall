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
            // 1) Naèteme všechny stoly (vìtšinou jich je relativnì málo)
            var tables = await _db.WorkTables
                .OrderBy(t => t.Name)
                .ToListAsync();

            var tableIds = tables.Select(t => t.Id).ToList();

            if (!tableIds.Any())
            {
                Tables = new List<TableOverviewViewModel>();
                return;
            }

            // 2) Pending cally pro tyto stoly (max. pár kusù)
            var pendingCalls = await _db.RowCalls
                .Include(c => c.HallRow)
                .Where(c =>
                    tableIds.Contains(c.WorkTableId) &&
                    c.Status == RowCallStatus.Pending)
                .ToListAsync();

            // Do dictionary: tableId -> pending call (vezmeme vždy ten nejnovìjší pro jistotu)
            var pendingByTable = pendingCalls
                .GroupBy(c => c.WorkTableId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.RequestedAt).First()
                );

            // 3) Poslední call (libovolného stavu) pro každý stùl – dìláme v DB pøes GroupBy
            var lastCalls = await _db.RowCalls
                .Include(c => c.HallRow)
                .Where(c => tableIds.Contains(c.WorkTableId))
                .GroupBy(c => c.WorkTableId)
                .Select(g => g
                    .OrderByDescending(c => c.RequestedAt)
                    .First())
                .ToListAsync();

            var lastByTable = lastCalls
                .ToDictionary(c => c.WorkTableId, c => c);

            // 4) Poskládáme viewmodely pro index
            Tables = tables
                .Select(t =>
                {
                    pendingByTable.TryGetValue(t.Id, out var pending);
                    lastByTable.TryGetValue(t.Id, out var last);

                    return new TableOverviewViewModel
                    {
                        Table = t,
                        PendingCall = pending,
                        LastCall = last
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
