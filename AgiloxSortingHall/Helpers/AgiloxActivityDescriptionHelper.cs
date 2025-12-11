using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Models;
using AgiloxSortingHall.ViewModels;

namespace AgiloxSortingHall.Helpers
{
    /// <summary>
    /// Převod Agilox callback dat (status + action) do UI-friendly viewmodelu.
    /// </summary>
    public static class AgiloxActivityDescriptionHelper
    {
        /// <summary>
        /// Vrací jen textový popis (zpětná kompatibilita).
        /// </summary>
        public static string GetActivityDescription(RowCall call)
            => GetActivityUi(call).Text;

        /// <summary>
        /// Kompletní UI model (text, barva, ikona, závažnost).
        /// </summary>
        public static AgiloxActivityViewModel GetActivityUi(RowCall call)
        {
            // Stále čekáme na paletu – skladník ji nepřinesl
            if (call.OrderId == null)
            {
                return new AgiloxActivityViewModel
                {
                    Text = "Čeká na doplnění palety skladníkem",
                    Severity = AgiloxSeverity.Info,
                    IconCss = "bi-box-seam",
                    TextCss = "text-warning"
                };
            }

            // Máme OrderId, ale robot ještě neposlal první callback
            if (string.IsNullOrWhiteSpace(call.LastAgiloxStatus) &&
                string.IsNullOrWhiteSpace(call.LastAgiloxAction))
            {
                return new AgiloxActivityViewModel
                {
                    Text = "Čeká se na reakci Agiloxu…",
                    Severity = AgiloxSeverity.Info,
                    IconCss = "bi-hourglass-split",
                    TextCss = "text-info"
                };
            }

            var status = ParseStatus(call.LastAgiloxStatus);
            var action = ParseAction(call.LastAgiloxAction);

            // Order canceled
            if (status == AgiloxStatus.OrderCanceled)
            {
                return new AgiloxActivityViewModel
                {
                    Text = "Požadavek byl zrušen",
                    Severity = AgiloxSeverity.Warning,
                    IconCss = "bi-slash-circle",
                    TextCss = "text-warning"
                };
            }

            // Specifické chování podle stavu
            switch (status)
            {
                // Paleta nebyla nalezena
                case AgiloxStatus.PalletNotFound:
                    return new AgiloxActivityViewModel
                    {
                        Text = "Paleta nebyla nalezena",
                        Severity = AgiloxSeverity.Error,
                        IconCss = "bi-exclamation-octagon-fill",
                        TextCss = "text-danger"
                    };

                // Cílový stůl obsazený
                case AgiloxStatus.Occupied:
                    return new AgiloxActivityViewModel
                    {
                        Text = "Cílový stůl je obsazený",
                        Severity = AgiloxSeverity.Error,
                        IconCss = "bi-exclamation-octagon-fill",
                        TextCss = "text-danger"
                    };

                // OK – odlišujeme pickup/drop
                case AgiloxStatus.Ok:

                    // PICKUP OK -> paleta je v převozu
                    if (action == AgiloxAction.Pickup)
                    {
                        return new AgiloxActivityViewModel
                        {
                            Text = "Paleta je v převozu",
                            Severity = AgiloxSeverity.Info,
                            IconCss = "bi-clock",
                            TextCss = "text-primary"
                        };
                    }

                    // DROP OK -> doručeno
                    if (action == AgiloxAction.Drop)
                    {
                        return new AgiloxActivityViewModel
                        {
                            Text = "Paleta byla doručena",
                            Severity = AgiloxSeverity.Info,
                            IconCss = "bi-check-circle-fill",
                            TextCss = "text-success"
                        };
                    }

                    // fallback
                    return new AgiloxActivityViewModel
                    {
                        Text = "Agilox úspěšně dokončil krok",
                        Severity = AgiloxSeverity.Info,
                        IconCss = "bi-check",
                        TextCss = "text-info"
                    };

                default:
                    return new AgiloxActivityViewModel
                    {
                        Text = "Agilox zpracovává požadavek",
                        Severity = AgiloxSeverity.Info,
                        IconCss = "bi-hourglass",
                        TextCss = "text-secondary"
                    };
            }
        }

        private static AgiloxStatus ParseStatus(string? raw)
        {
            if (raw == null) return AgiloxStatus.Unknown;

            return raw.Trim().ToLowerInvariant() switch
            {
                "ok" => AgiloxStatus.Ok,
                "pallet_not_found" => AgiloxStatus.PalletNotFound,
                "occupied" => AgiloxStatus.Occupied,
                "order_canceled" => AgiloxStatus.OrderCanceled,
                _ => AgiloxStatus.Unknown
            };
        }

        private static AgiloxAction ParseAction(string? raw)
        {
            if (raw == null) return AgiloxAction.Unknown;

            return raw.Trim().ToLowerInvariant() switch
            {
                "pickup" => AgiloxAction.Pickup,
                "drop" => AgiloxAction.Drop,
                _ => AgiloxAction.Unknown
            };
        }
    }
}
