using AgiloxSortingHall.Data;
using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Helpers;
using AgiloxSortingHall.Models;
using AgiloxSortingHall.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace AgiloxSortingHall.Pages
{
    /// <summary>
    /// Úvodní stránka – pøehled všech stolù a jejich aktuálního stavu.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILogger<IndexModel> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public IndexModel(
            AppDbContext db,
            ILogger<IndexModel> logger,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Pøehledové položky pro jednotlivé stoly
        /// (stùl + pending call + poslední call).
        /// </summary>
        public List<TableOverviewViewModel> Tables { get; set; } = new();

        public async Task OnGetAsync()
        {
            var tables = await _db.WorkTables
                .OrderBy(t => t.Name)
                .ToListAsync();

            var tableIds = tables.Select(t => t.Id).ToList();
            if (!tableIds.Any())
            {
                Tables = new();
                return;
            }

            var pendingCalls = await _db.RowCalls
                .Include(c => c.HallRow)
                .Where(c =>
                    tableIds.Contains(c.WorkTableId) &&
                    c.Status == RowCallStatus.Pending)
                .ToListAsync();

            var pendingByTable = pendingCalls
                .GroupBy(c => c.WorkTableId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.RequestedAt).First()
                );

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
        /// Textový popis aktivity pro daný RowCall (kvùli kompatibilitì).
        /// </summary>
        public string GetActivityDescription(RowCall call)
            => AgiloxActivityDescriptionHelper.GetActivityDescription(call);

        /// <summary>
        /// Handler pro tlaèítko "Hotovo" na indexu.
        /// Pošle na Agilox workflow 501, aby odvezl paletu od stolu
        /// do øady "hotovo".
        /// </summary>
        public async Task<IActionResult> OnPostDoneAsync(int tableId)
        {
            var table = await _db.WorkTables.FindAsync(tableId);
            if (table == null)
            {
                _logger.LogWarning("OnPostDoneAsync: stùl {TableId} nebyl nalezen.", tableId);
                return RedirectToPage();
            }

            // vytvoøíme RowCall bez øady – reprezentuje "odvoz od stolu"
            var call = new RowCall
            {
                WorkTableId = table.Id,
                HallRowId = null,
                Status = RowCallStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            _db.RowCalls.Add(call);
            await _db.SaveChangesAsync();

            var client = _httpClientFactory.CreateClient("Agilox");

            var payload = new Dictionary<string, string>
            {
                ["@TABLE"] = table.Name
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "OnPostDoneAsync: posílám workflow 502 pro stùl {Table}. Payload={Payload}",
                table.Name,
                json);

            var response = await client.PostAsync("workflow/502", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation(
                "OnPostDoneAsync: Agilox odpovìï pro stùl {Table}: {Body}",
                table.Name,
                responseBody);

            response.EnsureSuccessStatusCode();

            // zkus vytáhnout ID z odpovìdi Agiloxu a uložit do RowCall.OrderId
            try
            {
                using var doc = JsonDocument.Parse(responseBody);

                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    long? agiloxId = null;

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

                    if (agiloxId.HasValue)
                    {
                        call.OrderId = agiloxId.Value;
                        await _db.SaveChangesAsync();
                        _logger.LogInformation(
                            "OnPostDoneAsync: RowCall {RowCallId} pro stùl {Table} má OrderId={OrderId}",
                            call.Id, table.Name, call.OrderId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "OnPostDoneAsync: odpovìï Agiloxu neobsahuje použitelné 'id'. Body={Body}",
                            responseBody);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OnPostDoneAsync: chyba pøi parsování odpovìdi Agiloxu: {Body}",
                    responseBody);
            }

            return RedirectToPage();
        }


    }
}
