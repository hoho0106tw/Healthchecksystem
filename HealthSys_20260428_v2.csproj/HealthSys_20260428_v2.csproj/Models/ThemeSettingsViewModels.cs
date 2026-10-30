namespace HealthSys_20260428_v2.csproj.Models
{
    public class ThemeSettingsViewModel
    {
        public string Title { get; init; } = "佈景主題";

        public string DefaultMode { get; init; } = "light";

        public string DefaultColor { get; init; } = "#1677e8";

        public string DefaultRadius { get; init; } = "8";

        public string DefaultRowHeight { get; init; } = "40";

        public string DefaultDensity { get; init; } = "standard";

        public string DefaultFont { get; init; } = "medium";

        public static ThemeSettingsViewModel CreateDefault() => new();
    }

    public class ThemeSettingsApplyPostModel
    {
        public string? ThemeMode { get; set; }

        public string? AccentColor { get; set; }

        public int Radius { get; set; }

        public int RowHeight { get; set; }

        public string? Density { get; set; }

        public string? FontSize { get; set; }
        public string? TextColor { get; set; }

        public string? PageZoom { get; set; }

        public bool FixedHeader { get; set; }

        public bool CardShadow { get; set; }

        public bool Motion { get; set; }
    }
}
