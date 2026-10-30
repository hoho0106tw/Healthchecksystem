using HealthSys_20260428_v2.csproj.Models;
using Microsoft.AspNetCore.Mvc;

namespace HealthSys_20260428_v2.csproj.Controllers
{
    public class ThemeSettingsController : Controller
    {
        private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
        {
            "light",
            "dark",
            "gray",
            "simple",
            "system"
        };

        private static readonly HashSet<string> AllowedDensities = new(StringComparer.OrdinalIgnoreCase)
        {
            "comfortable",
            "standard",
            "compact"
        };

        private static readonly HashSet<string> AllowedFontSizes = new(StringComparer.OrdinalIgnoreCase)
        {
            "small",
            "medium",
            "large"
        };

        public IActionResult ThemeSettings()
        {
            var model = ThemeSettingsViewModel.CreateDefault();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Apply(ThemeSettingsApplyPostModel input)
        {
            var model = ThemeSettingsViewModel.CreateDefault();
            var themeMode = NormalizeAllowed(input.ThemeMode, AllowedModes, model.DefaultMode);
            var accentColor = IsHexColor(input.AccentColor ?? string.Empty) ? input.AccentColor!.Trim() : model.DefaultColor;
            var density = NormalizeAllowed(input.Density, AllowedDensities, model.DefaultDensity);
            var fontSize = NormalizeAllowed(input.FontSize, AllowedFontSizes, model.DefaultFont);

            return Json(new
            {
                applied = true,
                theme = new
                {
                    mode = themeMode,
                    color = accentColor,
                    radius = Math.Clamp(input.Radius, 4, 18),
                    rowHeight = Math.Clamp(input.RowHeight, 32, 52),
                    density,
                    font = fontSize,
                    textColor = IsHexColor(input.TextColor ?? string.Empty) ? input.TextColor!.Trim() : "",
                    zoom = input.PageZoom?.Trim() switch
                    {
                        "90%" => "90%",
                        "110%" => "110%",
                        "125%" => "125%",
                        _ => "100%（預設）"
                    },
                    fixedHeader = input.FixedHeader,
                    cardShadow = input.CardShadow,
                    motion = input.Motion
                }
            });
        }

        private static string NormalizeAllowed(string? value, HashSet<string> allowedValues, string fallback)
        {
            var normalizedValue = (value ?? string.Empty).Trim();

            return allowedValues.Contains(normalizedValue) ? normalizedValue : fallback;
        }

        private static bool IsHexColor(string color)
        {
            var normalizedColor = color.Trim();

            if (normalizedColor.Length is not (4 or 7) || normalizedColor[0] != '#')
            {
                return false;
            }

            return normalizedColor.Skip(1).All(IsHexDigit);
        }

        private static bool IsHexDigit(char value) =>
            value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
