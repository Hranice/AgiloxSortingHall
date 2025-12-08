using AgiloxSortingHall.Data;
using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Services
{
    /// <summary>
    /// Aplikační logika zpracovávající callbacky z Agilox systému
    /// a aktualizující stav řad, palet a fronty RowCall.
    /// </summary>
    public class AgiloxService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AgiloxService> _logger;
        private readonly IHubContext<HallHub> _hub;

        /// <summary>
        /// Inicializuje službu AgiloxService se závislostmi na DB, loggeru a SignalR hubu.
        /// </summary>
        public AgiloxService(ILogger<AgiloxService> logger, AppDbContext db, IHubContext<HallHub> hub)
        {
            _logger = logger;
            _db = db;
            _hub = hub;
        }

        /// <summary>
        /// Vstupní bod pro zpracování callbacku od Agiloxu.
        /// Podle <see cref="AgiloxCallbackDto.Action"/> a <see cref="AgiloxCallbackDto.Status"/>
        /// přepne na odpovídající obslužnou rutinu (pickup, drop, order_canceled).
        /// </summary>
        /// <param name="dto">
        /// Data z callbacku obsahující OrderId, Action, Status a případně Row/Table název.
        /// </param>
        public async Task ProcessCallbackAsync(AgiloxCallbackDto dto)
        {
            _logger.LogInformation(
                "Processing Agilox callback: orderid={OrderId}, action={Action}, row={Row}, table={Table}, status={Status}",
                dto.OrderId, dto.Action, dto.Row, dto.Table, dto.Status);

            // Převedeme string hodnoty na enumy – budeme s nimi dál pracovat typově bezpečně.
            var action = ParseAction(dto.Action);
            var status = ParseStatus(dto.Status);

            var call = await _db.RowCalls
                .Include(c => c.HallRow)
                    .ThenInclude(r => r.Slots)
                .Include(c => c.WorkTable)
                .Where(c =>
                    c.Status == RowCallStatus.Pending &&
                    c.OrderId == dto.OrderId)
                .FirstOrDefaultAsync();

            if (call == null)
            {
                _logger.LogWarning(
                    "No pending RowCall found for OrderId={OrderId} (row={Row}, table={Table}).",
                    dto.OrderId, dto.Row, dto.Table);
                return;
            }

            // uložíme poslední akci a status z Agiloxu (pro audit / debugging – původní string)
            call.LastAgiloxAction = dto.Action;
            call.LastAgiloxStatus = dto.Status;

            // sanity check na jméno řady / stolu – neblokuje zpracování, jen loguje.
            LogRowTableMismatchIfAny(call, dto);

            // 1) Speciální case – order_canceled (nezávisle na action).
            if (status == AgiloxStatus.OrderCanceled)
            {
                HandleOrderCanceled(call, dto);
            }
            // 2) Pickup/drop podle action.
            else if (action == AgiloxAction.Pickup)
            {
                HandlePickup(call, status, dto);
            }
            else if (action == AgiloxAction.Drop)
            {
                HandleDrop(call, status, dto);
            }
            else
            {
                // Neznámá akce – jen log.
                _logger.LogWarning(
                    "Received Agilox callback with unknown action='{Action}' for OrderId={OrderId}. Raw status='{Status}'",
                    dto.Action, dto.OrderId, dto.Status);
            }

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("HallUpdated");
        }

        #region Handlery pro jednotlivé typy callbacků

        /// <summary>
        /// Zpracuje callback pro akci "pickup" (naložení palety z řady).
        /// </summary>
        /// <param name="call">RowCall reprezentující požadavek stolu.</param>
        /// <param name="status">Status Agilox callbacku převedený na enum.</param>
        /// <param name="dto">Původní DTO pro logování (raw hodnoty).</param>
        private void HandlePickup(RowCall call, AgiloxStatus status, AgiloxCallbackDto dto)
        {
            switch (status)
            {
                case AgiloxStatus.Ok:
                    {
                        // Paleta byla fyzicky odebrána – nejnižší obsazený slot označíme jako InTransit.
                        var slot = GetBottomSlot(call.HallRow, PalletState.Occupied);

                        if (slot != null)
                        {
                            slot.State = PalletState.InTransit;

                            // RowCall stále čeká na drop, necháváme jej tedy Pending.
                            call.Status = RowCallStatus.Pending;

                            _logger.LogInformation(
                                "Pickup OK for RowCall {RowCallId}. Slot {SlotId} in row {RowName} set to InTransit.",
                                call.Id, slot.Id, call.HallRow.Name);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Pickup OK received but no occupied slot found in row {RowName} for RowCall {RowCallId}. " +
                                "Row state may be out-of-sync with Agilox.",
                                call.HallRow.Name, call.Id);
                        }

                        break;
                    }

                case AgiloxStatus.PalletNotFound:
                    {
                        // Agilox paletu nenašel – požadavek označíme jako zrušený.
                        call.Status = RowCallStatus.Cancelled;

                        _logger.LogWarning(
                            "Pickup pallet_not_found for RowCall {RowCallId} in row {RowName}. Marking RowCall as Cancelled.",
                            call.Id, call.HallRow.Name);
                        break;
                    }

                case AgiloxStatus.Unknown:
                default:
                    {
                        // Neznámý nebo neočekávaný status pro pickup – jen logujeme.
                        _logger.LogWarning(
                            "Unhandled pickup status '{Status}' for RowCall {RowCallId} (OrderId={OrderId}). Raw status='{RawStatus}'",
                            status, call.Id, dto.OrderId, dto.Status);
                        break;
                    }
            }
        }

        /// <summary>
        /// Zpracuje callback pro akci "drop" (vyložení palety).
        /// </summary>
        /// <param name="call">RowCall reprezentující požadavek stolu.</param>
        /// <param name="status">Status Agilox callbacku převedený na enum.</param>
        /// <param name="dto">Původní DTO pro logování (raw hodnoty).</param>
        private void HandleDrop(RowCall call, AgiloxStatus status, AgiloxCallbackDto dto)
        {
            switch (status)
            {
                case AgiloxStatus.Ok:
                    {
                        // Paleta byla úspěšně vyložena na stůl:
                        // - v řadě označíme nejnižší paletu v tranzitu (preferovaně) jako prázdnou.
                        //   Pokud žádná InTransit slot není, fallback na Occupied.
                        var slot = GetBottomSlot(call.HallRow, PalletState.InTransit)
                                   ?? GetBottomSlot(call.HallRow, PalletState.Occupied);

                        if (slot != null)
                        {
                            slot.State = PalletState.Empty;
                            call.Status = RowCallStatus.Delivered;

                            _logger.LogInformation(
                                "Drop OK for RowCall {RowCallId}. Freed slot {SlotId} in row {RowName}. RowCall marked as Delivered.",
                                call.Id, slot.Id, call.HallRow.Name);
                        }
                        else
                        {
                            // Nemáme ani InTransit ani Occupied – systém je v nekonzistentním stavu.
                            call.Status = RowCallStatus.Delivered;

                            _logger.LogWarning(
                                "Drop OK received but no InTransit/Occupied slot found in row {RowName} for RowCall {RowCallId}. " +
                                "RowCall marked as Delivered but row state may be incorrect.",
                                call.HallRow.Name, call.Id);
                        }

                        break;
                    }

                case AgiloxStatus.Occupied:
                    {
                        // Výdejní stanice je obsazená – Agilox čeká.
                        // V našem modelu ponecháme RowCall jako Pending, jen logujeme.
                        _logger.LogInformation(
                            "Drop occupied for RowCall {RowCallId} at table {TableName}. " +
                            "Target table is busy, Agilox is waiting.",
                            call.Id, call.WorkTable.Name);
                        break;
                    }

                case AgiloxStatus.PalletNotFound:
                    {
                        // Teoreticky by pallet_not_found u drop nastat neměl, ale ošetříme pro jistotu.
                        _logger.LogWarning(
                            "Drop pallet_not_found for RowCall {RowCallId}. This status is unexpected for drop callbacks. Raw status='{RawStatus}'",
                            call.Id, dto.Status);
                        break;
                    }

                case AgiloxStatus.Unknown:
                default:
                    {
                        _logger.LogWarning(
                            "Unhandled drop status '{Status}' for RowCall {RowCallId} (OrderId={OrderId}). Raw status='{RawStatus}'",
                            status, call.Id, dto.OrderId, dto.Status);
                        break;
                    }
            }
        }

        /// <summary>
        /// Zpracuje stav "order_canceled", který přichází typicky po rušení objednávky
        /// přes endpoint /order (workflow 500). Paleta je fyzicky vyřešena na straně Agiloxu,
        /// v našem systému požadavek označíme jako zrušený.
        /// </summary>
        private void HandleOrderCanceled(RowCall call, AgiloxCallbackDto dto)
        {
            // V tuto chvíli nevíme přesně, kde fyzicky paleta skončila (řeší WF500 v Agiloxu),
            // proto neměníme stavy slotů a pouze označíme RowCall jako zrušený.
            call.Status = RowCallStatus.Cancelled;

            _logger.LogInformation(
                "Order canceled received for RowCall {RowCallId} (OrderId={OrderId}). Marking RowCall as Cancelled.",
                call.Id, dto.OrderId);
        }

        #endregion

        #region Pomocné metody

        /// <summary>
        /// Převede string <see cref="AgiloxCallbackDto.Action"/> na enum <see cref="AgiloxAction"/>.
        /// </summary>
        private static AgiloxAction ParseAction(string? action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return AgiloxAction.Unknown;
            }

            return action.Trim().ToLowerInvariant() switch
            {
                "pickup" => AgiloxAction.Pickup,
                "drop" => AgiloxAction.Drop,
                _ => AgiloxAction.Unknown
            };
        }

        /// <summary>
        /// Převede string <see cref="AgiloxCallbackDto.Status"/> na enum <see cref="AgiloxStatus"/>.
        /// </summary>
        private static AgiloxStatus ParseStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return AgiloxStatus.Unknown;
            }

            switch (status.Trim().ToLowerInvariant())
            {
                case "ok":
                    return AgiloxStatus.Ok;
                case "pallet_not_found":
                    return AgiloxStatus.PalletNotFound;
                case "occupied":
                    return AgiloxStatus.Occupied;
                case "order_canceled":
                    return AgiloxStatus.OrderCanceled;
                default:
                    return AgiloxStatus.Unknown;
            }
        }

        /// <summary>
        /// Vrátí nejnižší slot v dané řadě, který má daný stav.
        /// Pokud žádný nevyhovuje, vrací <c>null</c>.
        /// </summary>
        private static PalletSlot? GetBottomSlot(HallRow row, PalletState state)
        {
            return row.Slots
                .Where(s => s.State == state)
                .OrderBy(s => s.PositionIndex)
                .FirstOrDefault();
        }

        /// <summary>
        /// Zaloguje případný rozdíl mezi řadou/stolem z našeho RowCallu a daty z callbacku.
        /// Neslouží k blokaci logiky – pouze ke snadnějšímu debugování.
        /// </summary>
        private void LogRowTableMismatchIfAny(RowCall call, AgiloxCallbackDto dto)
        {
            var rowMatches = string.Equals(call.HallRow.Name, dto.Row, StringComparison.OrdinalIgnoreCase);
            var tableMatches = string.Equals(call.WorkTable.Name, dto.Table, StringComparison.OrdinalIgnoreCase);

            if (!rowMatches || !tableMatches)
            {
                _logger.LogWarning(
                    "Agilox callback row/table mismatch. Call has row={RowDb}, table={TableDb}, callback row={RowDto}, table={TableDto}, orderid={OrderId}.",
                    call.HallRow.Name, call.WorkTable.Name,
                    dto.Row, dto.Table,
                    dto.OrderId);
            }
        }

        #endregion
    }
}
