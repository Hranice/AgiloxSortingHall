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
    public class StulModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<StulModel> _logger;
        private readonly IHubContext<HallHub> _hub;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Inicializuje instanci StulModel, včetně databázového kontextu,
        /// loggeru, SignalR hubu a továrny na HttpClient.
        /// </summary>
        public StulModel(ILogger<StulModel> logger, AppDbContext db, IHubContext<HallHub> hub, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Aktuální stůl instance.
        /// </summary>
        public WorkTable Table { get; set; } = null!;

        /// <summary>
        /// Řady v hale spolu s jejich sloty.
        /// </summary>
        public List<HallRow> Rows { get; set; } = new();

        /// <summary>
        /// Aktuální čekající call (pokud existuje)
        /// </summary>
        public RowCall? PendingCall { get; set; }

        /// <summary>
        /// Všechny pending call-y pro vizualizaci fronty na mřížce.
        /// </summary>
        public List<RowCall> PendingCalls { get; set; } = new();

        /// <summary>
        /// Aktuálně zvolený režim zobrazení (rows / articles).
        /// </summary>
        public string ViewMode { get; set; } = "rows";

        /// <summary>
        /// Načte data pro stránku stolu: konkrétní stůl, všechny řady,
        /// aktuální čekající call daného stolu a všechny pending call-y
        /// pro zobrazení stavu fronty.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id, string? view)
        {
            Table = await _db.WorkTables.FindAsync(id)
                ?? throw new Exception("Stůl nenalezen");

            if (string.Equals(view, "articles", StringComparison.OrdinalIgnoreCase))
            {
                ViewMode = "articles";
            }
            else
            {
                ViewMode = "rows";
            }

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

        /// <summary>
        /// Stůl si "zavolá" konkrétní řadu.
        /// Vždy vytvoříme RowCall (aby byl vidět ve frontě),
        /// ale workflow na Agilox se spouští jen pokud je v řadě
        /// volná paleta (Occupied) nad rámec už odeslaných požadavků.
        /// </summary>
        public async Task<IActionResult> OnPostCallRowAsync(int id, int rowId)
        {
            // stůl může mít jen jeden pending call
            bool alreadyPending = await _db.RowCalls
                .AnyAsync(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending);

            if (alreadyPending)
                return RedirectToPage(new { id });

            var table = await _db.WorkTables.FindAsync(id);
            var row = await _db.HallRows
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == rowId);

            if (table == null || row == null)
                return RedirectToPage(new { id });

            // vytvoříme nový call – zatím jen Pending, bez RequestId
            var call = new RowCall
            {
                WorkTableId = id,
                HallRowId = rowId,
                Status = RowCallStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            _db.RowCalls.Add(call);
            await _db.SaveChangesAsync();

            // pokusíme se pro tuto řadu ihned spustit workflow,
            // pokud je k dispozici volná paleta
            await TryDispatchAgiloxForRowAsync(row, table);

            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage(new { id });
        }

        /// <summary>
        /// Zavolání "artiklu" – uživatel neřeší konkrétní řadu,
        /// jen řekne "chci tento artikl". Backend si vybere nějakou řadu
        /// s tímto artiklem (zatím bereme první podle názvu).
        /// </summary>
        public async Task<IActionResult> OnPostCallArticleAsync(int id, string article)
        {
            // stůl může mít jen jeden pending call
            bool alreadyPending = await _db.RowCalls
                .AnyAsync(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending);

            if (alreadyPending)
                return RedirectToPage(new { id });

            var table = await _db.WorkTables.FindAsync(id);
            if (table == null)
                return RedirectToPage(new { id });

            var rowsForArticle = await _db.HallRows
                .Include(r => r.Slots)
                .Where(r => r.Article == article)
                .OrderBy(r => r.Name)  // definujeme tím "zleva doprava"
                .ToListAsync();

            if (!rowsForArticle.Any())
            {
                // žádná řada s tímto artiklem
                return RedirectToPage(new { id });
            }

            var settings = await _db.HallSettings.FirstOrDefaultAsync();
            var strategy = settings?.RowSelectionStrategy ?? RowSelectionStrategy.MostFreePallets;

            HallRow selectedRow;

            switch (strategy)
            {
                case RowSelectionStrategy.NearestLeft:
                    // nejbližší vlevo -> první v seřazeném seznamu
                    selectedRow = rowsForArticle.First();
                    break;

                case RowSelectionStrategy.NearestRight:
                    // nejbližší vpravo -> poslední v seřazeném seznamu
                    selectedRow = rowsForArticle.Last();
                    break;

                case RowSelectionStrategy.MostFreePallets:
                default:
                    // spočteme volné palety pro každou řadu a vezmeme tu s největším počtem
                    var availableDict = await GetAvailablePalletsForRowsAsync(rowsForArticle);

                    // seřadíme podle: nejvíc volných, při shodě "nejvíc vlevo"
                    selectedRow = rowsForArticle
                        .OrderByDescending(r => availableDict.ContainsKey(r.Id) ? availableDict[r.Id] : 0)
                        .ThenBy(r => r.Name)
                        .First();
                    break;
            }

            // vytvoříme call do vybrané řady
            var call = new RowCall
            {
                WorkTableId = id,
                HallRowId = selectedRow.Id,
                Status = RowCallStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            _db.RowCalls.Add(call);
            await _db.SaveChangesAsync();

            // zkusíme rovnou spustit workflow
            await TryDispatchAgiloxForRowAsync(selectedRow, table);

            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage(new { id });
        }

        /// <summary>
        /// Zkusí spustit workflow na Agilaxe pro první čekající RowCall
        /// v dané řadě, pokud je k dispozici volná paleta.
        /// Volná paleta = počet Occupied slotů > počet callů, které už
        /// mají přiřazené RequestId (tj. už na ně běží workflow).
        /// </summary>
        private async Task TryDispatchAgiloxForRowAsync(HallRow row, WorkTable table)
        {
            // spočítáme počet fyzicky obsazených slotů (palet) v řadě
            var occupiedCount = row.Slots.Count(s => s.State == PalletState.Occupied);

            // kolik pending callů pro tuto řadu už má přiřazené requestId (tj. poslali jsme na Agiloxe)
            var dispatchedCount = await _db.RowCalls
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.RequestId != null)
                .CountAsync();

            // pokud není k dispozici žádná volná paleta, jen čekáme na doplnění
            if (occupiedCount <= dispatchedCount)
            {
                _logger.LogInformation("Řada {Row} nemá volnou paletu – workflow se zatím nespouští.", row.Name);
                return;
            }

            // vezmeme první čekající call bez RequestId (nejstarší)
            var callToDispatch = await _db.RowCalls
                .Include(c => c.WorkTable)
                .Include(c => c.HallRow)
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.RequestId == null)
                .OrderBy(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            if (callToDispatch == null)
            {
                // nikdo ve frontě nečeká – není co dispatchnout
                return;
            }

            // vygenerujeme si vlastní requestId, které pošleme Agiloxu
            var requestId = Guid.NewGuid().ToString("N");

            var client = _httpClientFactory.CreateClient("Agilox");

            var payload = new Dictionary<string, string>
            {
                ["@ZAKLIKNUTARADA"] = row.Name,                   // např. "Řada3"
                ["@PRIJEMCE"] = callToDispatch.WorkTable.Name,    // např. "Stůl 1"
                ["@REQUESTID"] = requestId
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("workflow/502", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Agilox odpověď pro řadu {Row}: {Body}", row.Name, responseBody);

            response.EnsureSuccessStatusCode();

            // uložíme si requestId do callu – to se nám vrátí v callbacku
            callToDispatch.RequestId = requestId;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Odeslán workflow 502 pro řadu {Row} a stůl {Table}, requestId={Req}",
                row.Name, callToDispatch.WorkTable.Name, requestId);
        }



        /// <summary>
        /// Zruší nejnovější pending RowCall daného stolu.
        /// Pokud má call přiřazené RequestId, pokusí se najít a zrušit
        /// související order i na Agiloxu. Poté označí call jako Cancelled.
        /// </summary>
        public async Task<IActionResult> OnPostCancelCallAsync(int id)
        {
            // Najdeme nejnovější pending call pro daný stůl
            var call = await _db.RowCalls
                .Include(c => c.HallRow)
                .Include(c => c.WorkTable)
                .Where(c => c.WorkTableId == id && c.Status == RowCallStatus.Pending)
                .OrderByDescending(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            if (call == null)
            {
                // Není co rušit
                return RedirectToPage(new { id });
            }

            // Pokud už jsme na tento call poslali workflow na Agiloxe
            // (tj. máme uložené naše REQUESTID), zkusíme zrušit i order na Agiloxu
            if (!string.IsNullOrEmpty(call.RequestId))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("Agilox");

                    // /order vrací obří objekt: { "<orderIdString>": { ... }, ... }
                    var ordersJson = await client.GetStringAsync("order");

                    using var doc = JsonDocument.Parse(ordersJson);
                    var root = doc.RootElement;

                    long? orderIdToCancel = null;

                    // projdeme všechny ordery na prvním levelu
                    foreach (var orderProp in root.EnumerateObject())
                    {
                        var orderObj = orderProp.Value;

                        // uvnitř hledáme sekci "vars"
                        if (!orderObj.TryGetProperty("vars", out var vars))
                            continue;

                        // a v ní @REQUESTID
                        if (!vars.TryGetProperty("@REQUESTID", out var reqIdProp))
                            continue;

                        var reqId = reqIdProp.GetString();

                        if (!string.Equals(reqId, call.RequestId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // našli jsme správný order -> vytáhneme "id"
                        if (orderObj.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        {
                            if (idProp.TryGetInt64(out var orderId))
                            {
                                orderIdToCancel = orderId;
                                break;
                            }
                        }
                    }

                    if (orderIdToCancel.HasValue)
                    {
                        // pošleme storno na /order
                        var cancelPayload = new
                        {
                            id = orderIdToCancel.Value,
                            cancel = "true"
                        };

                        var resp = await client.PostAsJsonAsync("order", cancelPayload);
                        resp.EnsureSuccessStatusCode();

                        _logger.LogInformation(
                            "Agilox order {OrderId} (requestId={Req}) byl zrušen.",
                            orderIdToCancel.Value,
                            call.RequestId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Agilox order pro requestId={Req} nebyl v /order nalezen, ruším jen v DB.",
                            call.RequestId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Chyba při rušení Agilox orderu pro requestId={Req}.",
                        call.RequestId);
                }
            }

            call.Status = RowCallStatus.Cancelled;
            await _db.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("HallUpdated");

            return RedirectToPage(new { id });
        }

        /// <summary>
        /// Vrátí počet "volných" palet v dané řadě – tj.
        /// počet fyzicky obsazených slotů mínus počet callů pro tuto řadu,
        /// které už mají přiřazené RequestId (Agilox).
        /// </summary>
        private async Task<Dictionary<int, int>> GetAvailablePalletsForRowsAsync(IEnumerable<HallRow> rows)
        {
            // seznam Id všech řad, které nás zajímají
            var rowIds = rows.Select(r => r.Id).ToList();

            // spočítáme dispatched call-y pro každou řadu, kterou řešíme
            var dispatchedPerRow = await _db.RowCalls
                .Where(c => rowIds.Contains(c.HallRowId) &&
                            c.Status == RowCallStatus.Pending &&
                            c.RequestId != null)
                .GroupBy(c => c.HallRowId)
                .Select(g => new
                {
                    RowId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var dispatchedDict = dispatchedPerRow.ToDictionary(x => x.RowId, x => x.Count);

            var result = new Dictionary<int, int>();

            foreach (var row in rows)
            {
                // fyzicky obsazené sloty = palety v řadě
                var occupiedCount = row.Slots.Count(s => s.State == PalletState.Occupied);

                // kolik z nich už je rozebráno Agiloxem (dispatched call-y)
                dispatchedDict.TryGetValue(row.Id, out var dispatchedCount);

                var available = occupiedCount - dispatchedCount;
                if (available < 0)
                    available = 0; // pro jistotu, kdyby se to někdy rozjelo

                result[row.Id] = available;
            }

            return result;
        }

    }
}
