using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace AgiloxSortingHall.Pages
{
    public class SkladnikModel : PageModel
    {
        private readonly ILogger<SkladnikModel> _logger;
        private readonly AppDbContext _db;
        private readonly IHubContext<HallHub> _hub;
        private readonly IHttpClientFactory _httpClientFactory;

        public SkladnikModel(ILogger<SkladnikModel> logger, AppDbContext db, IHubContext<HallHub> hub, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Všechny øady v hale (pro vizualizaci i pro manipulaci).
        /// </summary>
        public List<HallRow> Rows { get; set; } = new();

        /// <summary>
        /// Hodnota textového pole pro každou øadu
        /// (klíè = Id øady, hodnota = název artiklu, který je zadaný v textboxu).
        /// </summary>
        [BindProperty]
        public Dictionary<int, string?> RowArticle { get; set; } = new();

        /// <summary>
        /// Všechny pending požadavky z jednotlivých stolù,
        /// používá se ve vizualizaci fronty.
        /// </summary>
        public List<RowCall> PendingCalls { get; set; } = new();

        /// <summary>
        /// Aktuálnì použitá strategie výbìru øady pro artikly,
        /// kterou nastavuje skladník.
        /// </summary>
        public RowSelectionStrategy CurrentStrategy { get; set; } = RowSelectionStrategy.MostFreePallets;

        [TempData]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Naètení stránky skladníka – øady, pending call-y a nastavení haly.
        /// </summary>
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

            // Všechny pending call-y pro vizualizaci fronty
            PendingCalls = await _db.RowCalls
                .Include(c => c.WorkTable)
                .Where(c => c.Status == RowCallStatus.Pending)
                .OrderBy(c => c.RequestedAt)
                .ToListAsync();

            var settings = await _db.HallSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                // Pokud nastavení ještì neexistuje, založíme defaultní øádek
                settings = new HallSettings
                {
                    Id = 1,
                    RowSelectionStrategy = RowSelectionStrategy.MostFreePallets
                };

                _db.HallSettings.Add(settings);
                await _db.SaveChangesAsync();
            }

            CurrentStrategy = settings.RowSelectionStrategy;
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
        /// Pøidání jedné palety do dané øady (do nejbližšího volného slotu).
        /// </summary>
        public async Task<IActionResult> OnPostAddPalletAsync(int rowId)
        {
            var row = await _db.HallRows
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == rowId);
            if (row == null)
            {
                ErrorMessage = "Øada nebyla nalezena.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(row.Article))
            {
                ErrorMessage = $"Øada {row.Name} nemá uložený název artiklu. Nejprve ho zadejte a uložte.";
                return RedirectToPage();
            }

            // vezmeme "nejvyšší" volný slot (nejblíž ke skladníkovi)
            var emptySlot = row.Slots
                .Where(s => s.State == PalletState.Empty)
                .OrderByDescending(s => s.PositionIndex)
                .FirstOrDefault();

            if (emptySlot == null)
            {
                ErrorMessage = $"Øada {row.Name} je plnì obsazená.";
                return RedirectToPage();
            }

            emptySlot.State = PalletState.Occupied;

            await _db.SaveChangesAsync();

            // po doplnìní palety zkusíme odpálit workflow pro první èekající call
            await TryDispatchAgiloxForRowAsync(row);

            await _hub.Clients.All.SendAsync("HallUpdated");
            return RedirectToPage();
        }

        /// <summary>
        /// Spustí workflow pro první èekající call,
        /// pokud má øada volnou paletu.
        /// </summary>
        private async Task TryDispatchAgiloxForRowAsync(HallRow row)
        {
            // spoèítáme obsazené sloty
            var occupiedCount = row.Slots.Count(s => s.State == PalletState.Occupied);

            // kolik pending callù už má requestId (tj. rozjetý Agilox)
            var dispatchedCount = await _db.RowCalls
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.RequestId != null)
                .CountAsync();

            if (occupiedCount <= dispatchedCount)
            {
                // i po doplnìní palety nejsme nad limitem – nic neposíláme
                return;
            }

            var callToDispatch = await _db.RowCalls
                .Include(c => c.WorkTable)
                .Include(c => c.HallRow)
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.RequestId == null)
                .OrderBy(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            if (callToDispatch == null)
                return;

            var requestId = Guid.NewGuid().ToString("N");

            var client = _httpClientFactory.CreateClient("Agilox");

            var payload = new Dictionary<string, string>
            {
                ["@ZAKLIKNUTARADA"] = row.Name,
                ["@PRIJEMCE"] = callToDispatch.WorkTable.Name,
                ["@REQUESTID"] = requestId
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("workflow/502", content);
            response.EnsureSuccessStatusCode();

            callToDispatch.RequestId = requestId;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Skladník – odeslán workflow 502 pro øadu {Row} a stùl {Table}, requestId={Req}",
                row.Name, callToDispatch.WorkTable.Name, requestId);
        }

        /// <summary>
        /// Odebrání jedné palety z dané øady.
        /// Odebírá se "shora" – slot s nejnižším PositionIndex, který je obsazený.
        /// Používá se pro opravu omylem pøidané palety.
        /// </summary>
        public async Task<IActionResult> OnPostRemovePalletAsync(int rowId)
        {
            // Najdeme øadu i se sloty
            var row = await _db.HallRows
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == rowId);

            if (row == null)
            {
                ErrorMessage = "Øada nebyla nalezena.";
                return RedirectToPage();
            }

            var frontSlot = row.Slots
                .Where(s => s.State == PalletState.Occupied)
                .OrderBy(s => s.PositionIndex)
                .FirstOrDefault();

            if (frontSlot == null)
            {
                ErrorMessage = $"Øada {row.Name} nemá žádnou paletu k odebrání.";
                return RedirectToPage();
            }

            // Slot vyprázdníme
            frontSlot.State = PalletState.Empty;

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage();
        }

        /// <summary>
        /// Skladník zmìní strategii výbìru øady (radio buttony na UI).
        /// Tuto strategii pak používají stoly pøi volání artiklu.
        /// </summary>
        public async Task<IActionResult> OnPostSetRowSelectionStrategyAsync(RowSelectionStrategy strategy)
        {
            var settings = await _db.HallSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new HallSettings
                {
                    Id = 1
                };
                _db.HallSettings.Add(settings);
            }

            settings.RowSelectionStrategy = strategy;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Skladník – zmìnìna RowSelectionStrategy na {Strategy}", strategy);

            return RedirectToPage();
        }
    }
}
