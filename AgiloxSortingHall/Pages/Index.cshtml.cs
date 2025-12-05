using AgiloxSortingHall.Data;
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

        public string GetActivityDescription(RowCall call)
        {
            // Ještì nemáme OrderId -> požadavek existuje jen u tebe, ne u Agiloxe
            if (call.OrderId == null)
                return "èeká na doplnìní palety skladníkem";

            // Máme OrderId -> koukneme na poslední event z Agiloxu
            return call.LastAgiloxEvent switch
            {
                "order_created" => "objednávka vytvoøena v Agiloxu",
                "order_started" => "Agilox zahájil zpracování požadavku",
                "station_entered" => "najíždí do stanice s paletou",
                "station_left" => "opustil stanici s paletou",
                "target_pre_pos_reached"
                  or "target_prepre_pos_reached"
                  or "target_reached" => "jede ke stolu s paletou",
                "order_done" => "paleta doruèena ke stolu",
                "order_canceled" => "požadavek byl zrušen",
                "no_route" => "nelze najít trasu k cíli",
                "no_station_left" => "není dostupná vhodná stanice pro akci",
                "timeout"
                  or "obstruction_timeout" => "èeká kvùli pøekážce nebo timeoutu",
                _ => "Agilox zpracovává požadavek"
            };
        }
    }
}
