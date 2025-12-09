using AgiloxSortingHall.Enums;

namespace AgiloxSortingHall.ViewModels
{
    /// <summary>
    /// UI reprezentace aktuální aktivity Agiloxu pro konkrétní RowCall.
    /// Obsahuje text, závažnost a doporučené CSS/ikony pro zobrazení.
    /// </summary>
    public class AgiloxActivityViewModel
    {
        /// <summary>
        /// Hlavní text zprávy zobrazovaný uživateli.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Závažnost zprávy (informace, varování, chyba).
        /// </summary>
        public AgiloxSeverity Severity { get; init; } = AgiloxSeverity.Info;

        /// <summary>
        /// Název Bootstrap ikony (např. "bi-robot", "bi-exclamation-octagon-fill").
        /// </summary>
        public string IconCss { get; init; } = "bi-info-circle";

        /// <summary>
        /// CSS třída pro barvu textu (např. "text-info", "text-warning", "text-danger").
        /// </summary>
        public string TextCss { get; init; } = "text-secondary";
    }
}
