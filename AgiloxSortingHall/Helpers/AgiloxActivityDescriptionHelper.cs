using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Models;

namespace AgiloxSortingHall.Helpers
{
    /// <summary>
    /// Pomocné metody pro převod Agilox callback stavů (status + action)
    /// na čitelný text pro UI.
    /// </summary>
    public static class AgiloxActivityDescriptionHelper
    {
        /// <summary>
        /// Vrátí textový popis aktuální aktivity pro daný RowCall,
        /// založený na OrderId, posledním Agilox statusu a akci.
        /// </summary>
        public static string GetActivityDescription(RowCall call)
        {
            // Ještě nemáme OrderId -> požadavek je jen v systému, Agilox o něm neví.
            if (call.OrderId == null)
                return "čeká na doplnění palety skladníkem";

            // Máme OrderId, ale zatím žádná reakce z Agiloxu
            if (string.IsNullOrWhiteSpace(call.LastAgiloxStatus) &&
                string.IsNullOrWhiteSpace(call.LastAgiloxAction))
            {
                return "objednávka byla odeslána do Agiloxu, čeká se na první reakci robota";
            }

            var status = ParseStatus(call.LastAgiloxStatus);
            var action = ParseAction(call.LastAgiloxAction);

            // Stav order_canceled – nezávisle na akci
            if (status == AgiloxStatus.OrderCanceled)
            {
                return "požadavek byl zrušen, Agilox paletu bezpečně odkládá podle interního workflow";
            }

            // Standardní kombinace
            switch (status)
            {
                case AgiloxStatus.PalletNotFound:
                    // pickup + pallet_not_found -> paleta v řadě nebyla nalezena
                    return "paleta nebyla nalezena v řadě, požadavek nelze dokončit";

                case AgiloxStatus.Occupied:
                    // drop + occupied -> cíl (stůl) je obsazený
                    return "cílový stůl je obsazený, Agilox čeká na jeho uvolnění";

                case AgiloxStatus.Ok:
                    // OK se vyhodnocuje podle typu akce
                    return action switch
                    {
                        AgiloxAction.Pickup =>
                            "Agilox naložil paletu z řady a jede s ní ke stolu",

                        AgiloxAction.Drop =>
                            "paleta byla doručena ke stolu",

                        _ =>
                            "Agilox úspěšně dokončil krok workflow"
                    };

                case AgiloxStatus.Unknown:
                default:
                    return "Agilox zpracovává požadavek";
            }
        }

        /// <summary>
        /// Převede uložený string statusu na enum <see cref="AgiloxStatus"/>.
        /// Pracuje pouze s hodnotami popsanými v dokumentaci (ok, pallet_not_found, occupied, order_canceled).
        /// </summary>
        private static AgiloxStatus ParseStatus(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return AgiloxStatus.Unknown;

            return raw.Trim().ToLowerInvariant() switch
            {
                "ok" => AgiloxStatus.Ok,
                "pallet_not_found" => AgiloxStatus.PalletNotFound,
                "occupied" => AgiloxStatus.Occupied,
                "order_canceled" => AgiloxStatus.OrderCanceled,
                _ => AgiloxStatus.Unknown
            };
        }

        /// <summary>
        /// Převede uložený string akce na enum <see cref="AgiloxAction"/>.
        /// Očekává hodnoty "pickup" nebo "drop" dle dokumentace.
        /// </summary>
        private static AgiloxAction ParseAction(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return AgiloxAction.Unknown;

            return raw.Trim().ToLowerInvariant() switch
            {
                "pickup" => AgiloxAction.Pickup,
                "drop" => AgiloxAction.Drop,
                _ => AgiloxAction.Unknown
            };
        }
    }
}
