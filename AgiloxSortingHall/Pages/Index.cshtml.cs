using AgiloxSortingHall.Data;
using AgiloxSortingHall.Dto;
using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Helpers;
using AgiloxSortingHall.Models;
using AgiloxSortingHall.ViewModels;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgiloxSortingHall.Pages
{
    /// <summary>
    /// Úvodní stránka – přehled všech stolů a jejich aktuálního stavu.
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
        /// Přehledové položky pro jednotlivé stoly
        /// (stůl + pending call + poslední call).
        /// </summary>
        public List<TableOverviewViewModel> Tables { get; set; } = new();

        /// <summary>
        /// Aktuálně zvolená kategorie z query stringu, např. ?category=Kontrola.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public WorkTableCategory? Category { get; set; }

        /// <summary>
        /// Možnosti do dropdownu filtru kategorií (bez Unknown).
        /// </summary>
        public IEnumerable<WorkTableCategory> CategoryOptions { get; } =
            Enum.GetValues<WorkTableCategory>()
                .Where(x => x != WorkTableCategory.Unknown);

        [TempData]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// JSON options pro deserializaci odpovědí z Agiloxu.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Vytvoří pojmenovaného HTTP klienta pro Agilox API.
        /// 
        /// Poznámka:
        /// - "Agilox" klient je registrován v Program.cs přes AddHttpClient("Agilox", ...).
        /// - BaseAddress se bere z appsettings.json (Agilox:BaseUrl).
        /// </summary>
        private HttpClient CreateAgiloxClient()
            => _httpClientFactory.CreateClient("Agilox");

        public async Task OnGetAsync()
        {
            var tablesQuery = _db.WorkTables.AsQueryable();

            if (Category.HasValue)
                tablesQuery = tablesQuery.Where(t => t.Category == Category.Value);

            // Nemáš .Name → řadíme podle friendly názvu
            var tables = await tablesQuery
                .OrderBy(t => t.DisplayName)
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
        /// Textový popis aktivity pro daný RowCall (kvůli kompatibilitě).
        /// </summary>
        public string GetActivityDescription(RowCall call)
            => AgiloxActivityDescriptionHelper.GetActivityDescription(call);

        public Task<IActionResult> OnPostDoneAsync(int tableId)
        {
            return SendTableWorkflowAsync(
                tableId: tableId,
                forceDestinationToBuffer: false,
                useInputStation: false,
                actionName: "OnPostDoneAsync");
        }

        public Task<IActionResult> OnPostEmptyPaletteAsync(int tableId)
        {
            return SendTableWorkflowAsync(
                tableId: tableId,
                forceDestinationToBuffer: true,
                useInputStation: true,
                actionName: "OnPostEmptyPaletteAsync");
        }

        private async Task<IActionResult> SendTableWorkflowAsync(
    int tableId,
    bool forceDestinationToBuffer,
    bool useInputStation,
    string actionName)
        {
            if (await HasPendingCallForTableAsync(tableId))
                return RedirectToPage(new { category = Category });

            var table = await _db.WorkTables.FindAsync(tableId);
            if (table == null)
            {
                _logger.LogWarning("{Action}: stůl {TableId} nebyl nalezen.", actionName, tableId);
                ErrorMessage = "Stůl nebyl nalezen.";
                return RedirectToPage(new { category = Category });
            }

            object destination;

            if (forceDestinationToBuffer || table.Category == WorkTableCategory.Kontrola2)
            {
                // finální cesta do oblasti Hotovo
                destination = await SelectRowNamesForDropAsync();
            }
            else if (table.Category == WorkTableCategory.Kontrola)
            {
                destination = "Kontrola2";
            }
            else
            {
                destination = "Kontrola";
            }

            // Původní kód 
            /*
            if (forceDestinationToBuffer || table.Category == WorkTableCategory.Kontrola)
            {
                var selectedRowNames = await SelectRowNamesForDropAsync();

                if (!selectedRowNames.Any())
                {
                    _logger.LogWarning(
                        "{Action}: nepodařilo se vybrat žádné cílové řady pro pokládání.",
                        actionName);

                    ErrorMessage = "Nepodařilo se určit cílové řady pro pokládání.";
                    return RedirectToPage(new { category = Category });
                }

                destination = selectedRowNames;
            }
            else
            {
                destination = "Kontrola";
            }
            */

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
            var station = useInputStation
     ? WorkTableStations.GetInputStation(table)
     : WorkTableStations.GetOutputStation(table);

            var payload = new Dictionary<string, object>
            {
                ["@TABLE"] = station,
                ["@DESTINATION"] = destination
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "{Action}: posílám workflow 502 pro stůl {Table}. Station={Station}, Destination={Destination}, Payload={Payload}",
                actionName,
                table.DisplayName,
                station,
                JsonSerializer.Serialize(destination),
                json);

            string responseBody;

            try
            {
                var response = await client.PostAsync("workflow/502", content);
                responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation(
                    "{Action}: Agilox odpověď pro stůl {Table}: {Body}",
                    actionName,
                    table.DisplayName,
                    responseBody);

                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(
                    ex,
                    "{Action}: timeout při volání Agiloxu pro stůl {Table}.",
                    actionName,
                    table.DisplayName);

                _db.RowCalls.Remove(call);
                await _db.SaveChangesAsync();

                ErrorMessage = "Nepodařilo se navázat spojení s Karlem.";
                return RedirectToPage(new { category = Category });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "{Action}: HTTP chyba při volání Agiloxu pro stůl {Table}.",
                    actionName,
                    table.DisplayName);

                _db.RowCalls.Remove(call);
                await _db.SaveChangesAsync();

                ErrorMessage = "Nepodařilo se navázat spojení s Karlem.";
                return RedirectToPage(new { category = Category });
            }

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
                            "{Action}: RowCall {RowCallId} pro stůl {Table} má OrderId={OrderId}",
                            actionName,
                            call.Id,
                            table.DisplayName,
                            call.OrderId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "{Action}: odpověď Agiloxu neobsahuje použitelné 'id'. Body={Body}",
                            actionName,
                            responseBody);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "{Action}: chyba při parsování odpovědi Agiloxu: {Body}",
                    actionName,
                    responseBody);
            }

            return RedirectToPage(new { category = Category });
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

        private async Task<List<string>> SelectRowNamesForDropAsync()
        {
            var orderedRowNames = await GetDropRowNamesOrderedAsync();
            if (!orderedRowNames.Any())
            {
                _logger.LogWarning("SelectRowNamesForDropAsync: orderedRowNames is empty.");
                return new List<string>();
            }

            var settings = await _db.HallSettings.FirstOrDefaultAsync();
            var strategy = settings?.DropRowSelectionStrategy ?? DropRowSelectionStrategy.NearestLeft;

            _logger.LogInformation(
                "SelectRowNamesForDropAsync: strategy = {Strategy}, orderedRowNames = [{Rows}]",
                strategy,
                string.Join(", ", orderedRowNames));

            var result = strategy switch
            {
                DropRowSelectionStrategy.NearestRight => orderedRowNames.AsEnumerable().Reverse().ToList(),
                DropRowSelectionStrategy.NearestLeft => orderedRowNames.ToList(),
                _ => orderedRowNames.ToList()
            };

            _logger.LogInformation(
                "SelectRowNamesForDropAsync: result = [{Rows}]",
                string.Join(", ", result));

            return result;
        }

        private async Task<List<string>> GetDropRowNamesOrderedAsync()
        {
            var stations = await FetchStationsAsync();
            if (stations == null || stations.Count == 0)
                return new List<string>();

            var settings = await _db.HallSettings.FirstOrDefaultAsync();
            var targetArea = settings?.DropStationAreaName ?? "Hotovo";

            var rowNames = stations.Values
                .Where(s =>
                    string.Equals(s.Type, "station", StringComparison.OrdinalIgnoreCase) &&
                    s.StationArea != null &&
                    s.StationArea.Any(a => string.Equals(a, targetArea, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(s => s.StationArea!)
                .Where(a =>
                    !string.Equals(a, targetArea, StringComparison.OrdinalIgnoreCase) &&
                    a.StartsWith("Řada", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => ExtractRowNumber(a))
                .ThenBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogInformation(
                "GetDropRowNamesOrderedAsync: targetArea = {TargetArea}, resulting rowNames = [{Rows}]",
                targetArea,
                string.Join(", ", rowNames));

            return rowNames;
        }

        private async Task<Dictionary<string, StationDto>?> FetchStationsAsync()
        {
            try
            {
                var http = CreateAgiloxClient();

                var json = await http.GetStringAsync("station");

                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return null;

                var result = new Dictionary<string, StationDto>(StringComparer.Ordinal);

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    try
                    {
                        var dto = prop.Value.Deserialize<StationDto>(_jsonOptions);
                        if (dto != null)
                            result[prop.Name] = dto;
                    }
                    catch (JsonException)
                    {
                        continue;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nepodařilo se načíst station z Agiloxu (GET /station).");
                return null;
            }
        }

        private static int ExtractRowNumber(string rowName)
        {
            if (string.IsNullOrWhiteSpace(rowName))
                return int.MaxValue;

            var match = Regex.Match(rowName, @"(\d+)$");
            if (!match.Success)
                return int.MaxValue;

            return int.TryParse(match.Groups[1].Value, out var number)
                ? number
                : int.MaxValue;
        }
    }
}
