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
        public string ViewMode { get; set; } = "articles";

        /// <summary>
        /// Načte data pro stránku stolu: konkrétní stůl, všechny řady,
        /// aktuální čekající call daného stolu a všechny pending call-y
        /// pro zobrazení stavu fronty.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id, string? view)
        {
            Table = await _db.WorkTables.FindAsync(id)
                ?? throw new Exception("Stůl nenalezen");

            if (string.Equals(view, "rows", StringComparison.OrdinalIgnoreCase))
            {
                ViewMode = "rows";
            }
            else
            {
                ViewMode = "articles";
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
            if (await HasPendingCallForTableAsync(id))
                return RedirectToPage(new { id });

            var table = await _db.WorkTables.FindAsync(id);
            var row = await _db.HallRows
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == rowId);

            if (table == null || row == null)
                return RedirectToPage(new { id });

            await CreateCallAndDispatchAsync(table, row);

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
            if (await HasPendingCallForTableAsync(id))
                return RedirectToPage(new { id });

            var table = await _db.WorkTables.FindAsync(id);
            if (table == null)
                return RedirectToPage(new { id });

            var selectedRow = await SelectRowForArticleAsync(article);
            if (selectedRow == null)
            {
                // žádná řada s tímto artiklem
                return RedirectToPage(new { id });
            }

            await CreateCallAndDispatchAsync(table, selectedRow);

            await _hub.Clients.All.SendAsync("HallUpdated");
            return RedirectToPage(new { id });
        }

        /// <summary>
        /// Vrátí true, pokud daný stůl už má nějaký pending RowCall.
        /// </summary>
        private Task<bool> HasPendingCallForTableAsync(int tableId)
        {
            return _db.RowCalls
                .AnyAsync(c => c.WorkTableId == tableId &&
                               c.Status == RowCallStatus.Pending);
        }

        /// <summary>
        /// Vytvoří pending RowCall pro daný stůl a řadu
        /// a pokusí se ihned spustit workflow na Agiloxe.
        /// </summary>
        private async Task CreateCallAndDispatchAsync(WorkTable table, HallRow row)
        {
            var call = new RowCall
            {
                WorkTableId = table.Id,
                HallRowId = row.Id,
                Status = RowCallStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            _db.RowCalls.Add(call);
            await _db.SaveChangesAsync();

            await TryDispatchAgiloxForRowAsync(row, table);
        }

        /// <summary>
        /// Vybere vhodnou řadu pro daný artikl podle aktuální strategie.
        /// Vrátí null, pokud žádná řada s tímto artiklem neexistuje.
        /// </summary>
        private async Task<HallRow?> SelectRowForArticleAsync(string article)
        {
            var rowsForArticle = await _db.HallRows
                .Include(r => r.Slots)
                .Where(r => r.Article == article)
                .OrderBy(r => r.Name)
                .ToListAsync();

            if (!rowsForArticle.Any())
                return null;

            var settings = await _db.HallSettings.FirstOrDefaultAsync();
            var strategy = settings?.RowSelectionStrategy ?? RowSelectionStrategy.MostFreePallets;

            switch (strategy)
            {
                case RowSelectionStrategy.NearestLeft:
                    return rowsForArticle.First();

                case RowSelectionStrategy.NearestRight:
                    return rowsForArticle.Last();

                case RowSelectionStrategy.MostFreePallets:
                default:
                    var availableDict = await GetAvailablePalletsForRowsAsync(rowsForArticle);

                    return rowsForArticle
                        .OrderByDescending(r => availableDict.TryGetValue(r.Id, out var v) ? v : 0)
                        .ThenBy(r => r.Name)
                        .First();
            }
        }


        /// <summary>
        /// Zkusí spustit workflow na Agiloxe pro první čekající RowCall
        /// v dané řadě, pokud je k dispozici volná paleta.
        /// Volná paleta = počet Occupied slotů > počet callů, které už
        /// mají přiřazené OrderId (tj. už na ně běží workflow).
        /// OrderId je ID workflow vygenerované Agiloxem.
        /// </summary>
        private async Task TryDispatchAgiloxForRowAsync(HallRow row, WorkTable table)
        {
            // spočítáme počet fyzicky obsazených slotů (palet) v řadě
            var occupiedCount = row.Slots.Count(s => s.State == PalletState.Occupied);

            // kolik pending callů pro tuto řadu už má přiřazené OrderId (tj. poslali jsme na Agiloxe)
            var dispatchedCount = await _db.RowCalls
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.OrderId != null) // long? != null = už běží workflow
                .CountAsync();

            // pokud není k dispozici žádná volná paleta, jen čekáme na doplnění
            if (occupiedCount <= dispatchedCount)
            {
                _logger.LogInformation("Řada {Row} nemá volnou paletu – workflow se zatím nespouští.", row.Name);
                return;
            }

            // vezmeme první čekající call bez OrderId (nejstarší)
            var callToDispatch = await _db.RowCalls
                .Include(c => c.WorkTable)
                .Include(c => c.HallRow)
                .Where(c => c.HallRowId == row.Id &&
                            c.Status == RowCallStatus.Pending &&
                            c.OrderId == null)
                .OrderBy(c => c.RequestedAt)
                .FirstOrDefaultAsync();

            if (callToDispatch == null)
            {
                // nikdo ve frontě nečeká – není co dispatchnout
                return;
            }

            var client = _httpClientFactory.CreateClient("Agilox");

            var payload = new Dictionary<string, string>
            {
                ["@ZAKLIKNUTARADA"] = row.Name,                    // např. "Řada3"
                ["@PRIJEMCE"] = callToDispatch.WorkTable.Name // např. "Stůl 1"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("workflow/502", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Agilox odpověď pro řadu {Row}: {Body}", row.Name, responseBody);

            response.EnsureSuccessStatusCode();

            // pokusíme se vytáhnout ID z odpovědi Agiloxu
            long? agiloxId = null;
            try
            {
                using var doc = JsonDocument.Parse(responseBody);

                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    if (idProp.ValueKind == JsonValueKind.Number &&
                        idProp.TryGetInt64(out var numericId))
                    {
                        agiloxId = numericId;
                    }
                    else if (idProp.ValueKind == JsonValueKind.String &&
                             long.TryParse(idProp.GetString(), out var stringId))
                    {
                        agiloxId = stringId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při parsování odpovědi Agiloxu: {Body}", responseBody);
            }

            if (agiloxId.HasValue)
            {
                callToDispatch.OrderId = agiloxId.Value;
            }
            else
            {
                _logger.LogWarning(
                    "Agilox odpověď pro řadu {Row} neobsahuje platné 'id', OrderId zůstává null. Body: {Body}",
                    row.Name, responseBody);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Odeslán workflow 502 pro řadu {Row} a stůl {Table}, OrderId={Req}",
                row.Name, callToDispatch.WorkTable.Name, callToDispatch.OrderId);
        }




        /// <summary>
        /// Zruší nejnovější pending RowCall daného stolu.
        /// Pokud má call přiřazené OrderId (ID workflow v Agiloxu),
        /// pokusí se zrušit související order i na Agiloxu přímo podle tohoto ID.
        /// Poté označí call jako Cancelled.
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
            // (tj. máme uložené Agilox ID v OrderId), zkusíme zrušit i order na Agiloxu
            if (call.OrderId != null)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("Agilox");

                    var cancelPayload = new
                    {
                        id = call.OrderId.Value,
                        cancel = "true"
                    };

                    var resp = await client.PostAsJsonAsync("order", cancelPayload);
                    resp.EnsureSuccessStatusCode();

                    _logger.LogInformation(
                        "Agilox order {OrderId} pro stůl {Table} byl zrušen.",
                        call.OrderId.Value,
                        call.WorkTable.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Chyba při rušení Agilox orderu pro OrderId={Req}.",
                        call.OrderId);
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
        /// které už mají přiřazené OrderId (Agilox).
        /// </summary>
        private async Task<Dictionary<int, int>> GetAvailablePalletsForRowsAsync(IEnumerable<HallRow> rows)
        {
            // seznam Id všech řad, které nás zajímají
            var rowIds = rows.Select(r => r.Id).ToList();

            // spočítáme dispatched call-y pro každou řadu, kterou řešíme
            var dispatchedPerRow = await _db.RowCalls
                .Where(c => rowIds.Contains(c.HallRowId) &&
                            c.Status == RowCallStatus.Pending &&
                            c.OrderId != null)
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

        public string GetActivityDescription(RowCall call)
        {
            // Ještě nemáme OrderId -> požadavek je jen v systému, Agilox o něm neví.
            if (call.OrderId == null)
                return "čeká na doplnění palety skladníkem";

            // Máme OrderId -> koukneme na poslední event z Agiloxu
            return call.LastAgiloxEvent switch
            {
                "order_created" => "objednávka vytvořena v Agiloxu",
                "order_started" => "Agilox zahájil zpracování požadavku",
                "station_entered" => "najíždí do stanice s paletou",
                "station_left" => "opustil stanici s paletou",
                "target_pre_pos_reached"
                  or "target_prepre_pos_reached"
                  or "target_reached" => "jede ke stolu s paletou",
                "order_done" => "paleta byla doručena ke stolu",
                "order_canceled" => "požadavek byl zrušen",
                "no_route" => "nelze najít trasu k cíli",
                "no_station_left" => "není dostupná vhodná stanice pro akci",
                "timeout"
                  or "obstruction_timeout" => "čeká kvůli překážce nebo timeoutu",
                _ => "Agilox zpracovává požadavek"
            };
        }
    }
}
