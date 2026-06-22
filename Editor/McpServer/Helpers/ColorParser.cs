using System;
using System.Globalization;
using UnityEngine;

namespace McpUnity.Helpers
{
    /// <summary>
    /// Utility class for parsing color values from various string formats.
    /// Supports hex colors (#RGB, #RGBA, #RRGGBB, #RRGGBBAA) and named colors.
    /// </summary>
    public static class ColorParser
    {
        /// <summary>
        /// Parse a color string to a Unity Color.
        /// Supports formats: #RGB, #RGBA, #RRGGBB, #RRGGBBAA, and common color names.
        /// </summary>
        /// <param name="colorString">The color string to parse</param>
        /// <param name="defaultColor">Default color if parsing fails</param>
        /// <returns>Parsed Color or defaultColor</returns>
        public static Color Parse(string colorString, Color defaultColor)
        {
            return TryParse(colorString, out var color) ? color : defaultColor;
        }

        /// <summary>
        /// Try to parse a color string. Returns false when the value is empty or unrecognized,
        /// so callers can surface a clear error instead of silently falling back to a default.
        /// </summary>
        /// <param name="colorString">The color string to parse</param>
        /// <param name="color">Parsed color (Color.white when parsing fails)</param>
        /// <returns>True if the string was parsed into a color</returns>
        public static bool TryParse(string colorString, out Color color)
        {
            color = Color.white;

            if (string.IsNullOrWhiteSpace(colorString))
                return false;

            colorString = colorString.Trim();

            // Try hex format
            if (colorString.StartsWith("#"))
            {
                if (TryParseHex(colorString, out color))
                    return true;
            }

            // Try Unity's built-in ColorUtility
            if (ColorUtility.TryParseHtmlString(colorString, out color))
                return true;

            // Try named colors
            if (TryParseNamedColor(colorString, out color))
                return true;

            color = Color.white;
            return false;
        }

        /// <summary>
        /// Try to parse a hex color string.
        /// </summary>
        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;

            // Remove # prefix
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            try
            {
                int r, g, b, a = 255;

                switch (hex.Length)
                {
                    case 3: // #RGB
                        r = int.Parse(hex.Substring(0, 1) + hex.Substring(0, 1), NumberStyles.HexNumber);
                        g = int.Parse(hex.Substring(1, 1) + hex.Substring(1, 1), NumberStyles.HexNumber);
                        b = int.Parse(hex.Substring(2, 1) + hex.Substring(2, 1), NumberStyles.HexNumber);
                        break;
                    case 4: // #RGBA
                        r = int.Parse(hex.Substring(0, 1) + hex.Substring(0, 1), NumberStyles.HexNumber);
                        g = int.Parse(hex.Substring(1, 1) + hex.Substring(1, 1), NumberStyles.HexNumber);
                        b = int.Parse(hex.Substring(2, 1) + hex.Substring(2, 1), NumberStyles.HexNumber);
                        a = int.Parse(hex.Substring(3, 1) + hex.Substring(3, 1), NumberStyles.HexNumber);
                        break;
                    case 6: // #RRGGBB
                        r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                        b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                        break;
                    case 8: // #RRGGBBAA
                        r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                        b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                        a = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                        break;
                    default:
                        return false;
                }

                color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Try to parse a named color.
        /// </summary>
        private static bool TryParseNamedColor(string name, out Color color)
        {
            color = Color.white;

            switch (name.ToLowerInvariant())
            {
                case "white": color = Color.white; return true;
                case "black": color = Color.black; return true;
                case "red": color = Color.red; return true;
                case "green": color = Color.green; return true;
                case "blue": color = Color.blue; return true;
                case "yellow": color = Color.yellow; return true;
                case "cyan": color = Color.cyan; return true;
                case "magenta": color = Color.magenta; return true;
                case "gray": case "grey": color = Color.gray; return true;
                case "clear": case "transparent": color = Color.clear; return true;
                case "orange": color = new Color(1f, 0.5f, 0f); return true;
                case "purple": color = new Color(0.5f, 0f, 0.5f); return true;
                case "pink": color = new Color(1f, 0.75f, 0.8f); return true;
                case "brown": color = new Color(0.6f, 0.3f, 0f); return true;
                default: return false;
            }
        }

        /// <summary>
        /// Convert a Color to hex string format.
        /// </summary>
        public static string ToHex(Color color, bool includeAlpha = false)
        {
            if (includeAlpha)
                return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
            else
                return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
    }
}
