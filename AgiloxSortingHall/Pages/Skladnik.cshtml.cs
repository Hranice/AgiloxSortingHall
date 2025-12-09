using AgiloxSortingHall.Data;
using AgiloxSortingHall.Enums;
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
        /// pokud má øada volnou paletu. Jako OrderId použije ID,
        /// které vrátí Agilox v odpovìdi.
        /// </summary>
        private async Task TryDispatchAgiloxForRowAsync(HallRow row)
        {
            // pokud nemáme reálnì volnou paletu oproti tomu,
            // kolik už bìží pending callù, nic neposíláme
            if (!await HasFreePalletForDispatchAsync(row))
            {
                _logger.LogInformation(
                    "Øada {Row} nemá volnou paletu – workflow se zatím nespouští.",
                    row.Name);
                return;
            }

            // první èekající call bez OrderId
            var callToDispatch = await GetNextPendingCallAsync(row.Id);
            if (callToDispatch == null)
            {
                // nikdo ve frontì neèeká – není co dispatchnout
                return;
            }

            // pošli workflow a vezmi si raw body
            var responseBody = await SendWorkflowAsync(row, callToDispatch);

            // zkus vytáhnout ID z odpovìdi Agiloxu
            var agiloxId = TryParseAgiloxId(responseBody);
            if (agiloxId.HasValue)
            {
                callToDispatch.OrderId = agiloxId.Value;
            }
            else
            {
                _logger.LogWarning(
                    "Agilox odpovìï neobsahuje použitelné 'id', øada {Row}, body: {Body}",
                    row.Name, responseBody);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Skladník – odeslán workflow 501 pro øadu {Row} a stùl {Table}, OrderId={Req}",
                row.Name,
                callToDispatch.WorkTable.Name,
                callToDispatch.OrderId);
        }

        /// <summary>
        /// Zjistí, jestli má daná øada k dispozici volnou paletu
        /// nad rámec už rozjetých pending callù (s OrderId).
        /// </summary>
        private async Task<bool> HasFreePalletForDispatchAsync(HallRow row)
        {
            var occupiedCount = row.Slots.Count(s => s.State == PalletState.Occupied);

            var dispatchedCount = await _db.RowCalls
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.OrderId != null)
                .CountAsync();

            return occupiedCount > dispatchedCount;
        }

        /// <summary>
        /// Vrátí první pending call bez OrderId pro danou øadu,
        /// tj. nejstarší požadavek, který ještì nebyl poslán na Agilox.
        /// </summary>
        private async Task<RowCall?> GetNextPendingCallAsync(int hallRowId)
        {
            return await _db.RowCalls
                .Include(c => c.WorkTable)
                .Include(c => c.HallRow)
                .Where(c => c.HallRowId == hallRowId &&
                            c.Status == RowCallStatus.Pending &&
                            c.OrderId == null) // ještì neposlaný na Agilox
                .OrderBy(c => c.RequestedAt)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Odešle na Agilox workflow pro danou øadu a stùl
        /// a vrátí tìlo HTTP odpovìdi jako string.
        /// </summary>
        private async Task<string> SendWorkflowAsync(HallRow row, RowCall callToDispatch)
        {
            var client = _httpClientFactory.CreateClient("Agilox");

            var payload = new Dictionary<string, string>
            {
                ["@ROW"] = row.Name,
                ["@TABLE"] = callToDispatch.WorkTable.Name
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("workflow/501", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(
                "Agilox odpovìï pro øadu {Row}: {Body}",
                row.Name, responseBody);

            response.EnsureSuccessStatusCode();

            return responseBody;
        }

        /// <summary>
        /// Pokusí se z JSON odpovìdi Agiloxu vytáhnout hodnotu "id"
        /// jako long. Vrací null, pokud tam není nebo nejde pøevést.
        /// </summary>
        private long? TryParseAgiloxId(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);

                if (!doc.RootElement.TryGetProperty("id", out var idProp))
                    return null;

                // typicky èíslo: {"id":169956752581240004}
                if (idProp.ValueKind == JsonValueKind.Number &&
                    idProp.TryGetInt64(out var numericId))
                {
                    return numericId;
                }

                // fallback: kdyby to nìkdy poslali jako string: {"id":"169956752581240004"}
                if (idProp.ValueKind == JsonValueKind.String &&
                    long.TryParse(idProp.GetString(), out var stringId))
                {
                    return stringId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Nepodaøilo se parsovat odpovìï Agiloxu: {Body}",
                    responseBody);
            }

            return null;
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
                ErrorMessage = $"{row.Name} nemá žádnou paletu k odebrání.";
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
