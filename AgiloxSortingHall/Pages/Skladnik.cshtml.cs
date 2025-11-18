using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Pages
{
    public class SkladnikModel : PageModel
    {
        private readonly ILogger<SkladnikModel> _logger;
        private readonly AppDbContext _db;
        private readonly IHubContext<HallHub> _hub;

        public SkladnikModel(ILogger<SkladnikModel> logger, AppDbContext db, IHubContext<HallHub> hub)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
        }

        public List<HallRow> Rows { get; set; } = new();

        // Hodnota textového pole pro každou øadu
        // klíè = Id øady, hodnota = název artiklu, který je zadaný v textboxu
        [BindProperty]
        public Dictionary<int, string?> RowArticle { get; set; } = new();

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            Rows = await _db.HallRows
                .Include(r => r.Slots)
                .OrderBy(r => r.Name)
                .ToListAsync();

            foreach (var r in Rows)
            {
                RowArticle[r.Id] = r.Article;
            }
        }


        /// <summary>
        /// Uložení / zmìna názvu artiklu pro danou øadu.
        /// Povolené pouze pokud je øada prázdná (žádný slot není obsazený).
        /// </summary>
        public async Task<IActionResult> OnPostSetArticleAsync(int rowId)
        {
            var row = await _db.HallRows
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == rowId);

            if (row == null)
            {
                ErrorMessage = "Øada nebyla nalezena.";
                return RedirectToPage();
            }

            RowArticle.TryGetValue(rowId, out var articleFromForm);
            articleFromForm = articleFromForm?.Trim();

            if (string.IsNullOrWhiteSpace(articleFromForm))
            {
                ErrorMessage = $"Název artiklu pro øadu {row.Name} nesmí být prázdný.";
                return RedirectToPage();
            }

            bool anyOccupied = row.Slots.Any(s => s.State == PalletState.Occupied);
            if (anyOccupied)
            {
                ErrorMessage =
                    $"Øada {row.Name} není prázdná, nelze zmìnit název artiklu, " +
                    $"dokud neodeberete všechny palety.";
                return RedirectToPage();
            }

            row.Article = articleFromForm;

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage();
        }


        /// <summary>
        /// Pøidání jedné palety do dané øady.
        /// Palety se pøidávají shora dolù – hledáme prázdný slot s nejvyšším indexem.
        /// Název artiklu musí být uložený (row.Article nenulové).
        /// </summary>
        public async Task<IActionResult> OnPostAddPalletAsync(int rowId)
        {
            var row = await _db.HallRows.FirstOrDefaultAsync(r => r.Id == rowId);
            if (row == null)
            {
                ErrorMessage = "Øada nebyla nalezena.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(row.Article))
            {
                ErrorMessage =
                    $"Øada {row.Name} nemá uložený název artiklu. " +
                    $"Nejprve ho zadejte a uložte.";
                return RedirectToPage();
            }

            var emptySlot = await _db.PalletSlots
                .Where(s => s.HallRowId == rowId && s.State == PalletState.Empty)
                .OrderByDescending(s => s.PositionIndex)
                .FirstOrDefaultAsync();

            if (emptySlot == null)
            {
                ErrorMessage = $"Øada {row.Name} je plnì obsazená.";
                return RedirectToPage();
            }

            emptySlot.State = PalletState.Occupied;

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage();
        }


        /// <summary>
        /// Znovu naète øady + doplní RowArticle podle aktuálního stavu DB
        /// a vrátí Page() – používá se v chybových vìtvích POST handlerù.
        /// </summary>
        private async Task<IActionResult> ReloadWithModelStateAsync()
        {
            Rows = await _db.HallRows
                .Include(r => r.Slots)
                .OrderBy(r => r.Name)
                .ToListAsync();

            foreach (var r in Rows)
                RowArticle[r.Id] = r.Article;

            return Page();
        }
    }
}
