using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace AgiloxSortingHall.Helpers
{
    public static class WorkTableStations
    {
        public static string GetInputStation(WorkTable t)
            => t.InputStationName;

        public static string GetOutputStation(WorkTable t)
            => t.OutputStationName;

        public static string GetUiName(WorkTable t)
            => t.DisplayName;

        // Kontrola --> hotovo
        /*
        public static string GetDestination(WorkTable t)
            => t.Category != WorkTableCategory.Kontrola ? "Kontrola" : "Hotovo";
        */

        // Kontrola --> Kontrola2 --> hotovo
        public static string GetDestination(WorkTable t) => t.Category switch
        {
            WorkTableCategory.Kontrola => "Kontrola2",
            WorkTableCategory.Kontrola2 => "Hotovo",
            _ => "Kontrola"
        };
        public static bool MatchesStation(WorkTable t, string? station)
        {
            if (string.IsNullOrWhiteSpace(station)) return false;

            return string.Equals(t.InputStationName, station, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.OutputStationName, station, StringComparison.OrdinalIgnoreCase);
        }

        public static string StationsToString(WorkTable t)
            => $"in={t.InputStationName}, out={t.OutputStationName}";
    }
}
