using AgiloxSortingHall.Data;
using AgiloxSortingHall.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Pages
{
    public class SkladnikModel : PageModel
    {
        private readonly ILogger<SkladnikModel> _logger;
        private readonly AppDbContext _db;

        public SkladnikModel(ILogger<SkladnikModel> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        // Všechny øady vèetnì pozic (slotù)
        public List<HallRow> Rows { get; set; } = new();

        // Data pro textová pole dole (zadání artiklu pro každou øadu)
        [BindProperty]
        public Dictionary<int, string> NewArticleForRow { get; set; } = new();

        public async Task OnGetAsync()
        {
            Rows = await _db.HallRows
                .Include(r => r.Slots)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        // Pøidání palety do konkrétní øady
        public async Task<IActionResult> OnPostAddPalletAsync(int rowId)
        {
            Rows = await _db.HallRows
                .Include(r => r.Slots)
                .OrderBy(r => r.Name)
                .ToListAsync();

            if (!NewArticleForRow.TryGetValue(rowId, out var article) || article == null)
            {
                // nic nezadáno -> jen znovu zobrazíme
                return Page();
            }

            var row = Rows.First(r => r.Id == rowId);

            // Najdeme první prázdný slot (State == Empty)
            var emptySlot = row.Slots
                .Where(s => s.State == PalletState.Empty)
                .OrderBy(s => s.PositionIndex)
                .FirstOrDefault();

            if (emptySlot != null)
            {
                emptySlot.Article = article;
                emptySlot.State = PalletState.Occupied;
                await _db.SaveChangesAsync();
            }
            else
            {
                // pøípadnì zobrazit nìjakou hlášku, že øada je plná
                ModelState.AddModelError(string.Empty, $"Øada {row.Name} je plná.");
            }

            return Page();
        }
    }
}
