using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnknownInventoryTerminal
{
    internal enum Ae2Image
    {
        // terminalStyle parts, as in AE2's base_terminal.json.
        Header, FirstRow, Row, LastRow, Bottom,
        Search, SearchFocused, Tooltip, Button, ButtonFocus, ButtonHover,
        Close, SortName, SortAmount, SortAscending, SortDescending, Back, Refresh, Thumb, ThumbDisabled, NativeButtonFace,
        TerminalStyleSmall, TerminalStyleMedium, TerminalStyleTall, TerminalStyleFull, VerticalButtonsBg
    }

    internal static class Ae2ToolbarAppearance
    {
        // AE2 IconButton: hover takes precedence over focus and moves the face and icon together.
        internal static Ae2Image Face(bool hovered, bool focused) => hovered ? Ae2Image.ButtonHover :
            focused ? Ae2Image.ButtonFocus : Ae2Image.Button;

        internal static int OffsetY(Ae2Image face) => face == Ae2Image.ButtonHover ? 1 : 0;

        // Icon crops may have unequal transparent margins. Measure their visible pixels, not the crop rectangle.
        internal static (int X, int Y, int Width, int Height)? VisibleBounds(int width, int height, Func<int, int, byte> alpha)
        {
            var left = width;
            var top = height;
            var right = -1;
            var bottom = -1;
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    if (alpha(x, y) == 0) continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            return right < 0 ? null : (left, top, right - left + 1, bottom - top + 1);
        }

        // Return screen-pixel offsets to keep the art crisp, including at odd scales or for odd-width artwork.
        internal static (int X, int Y) CenterIcon((int X, int Y, int Width, int Height) visible, int faceWidth, int faceHeight, int scale) =>
            ((faceWidth - visible.Width) * scale / 2 - visible.X * scale,
                (faceHeight - visible.Height) * scale / 2 - visible.Y * scale);
    }

    internal static class Ae2SlotAppearance
    {
        internal const uint HoverFill = 0x669CD3FF;
        internal const uint HoverBorder = 0xFFDAFFFF;
    }

    // AE2 uses top-left source coordinates: [x, y, width, height].
    internal readonly struct Ae2ImageRegion
    {
        internal string Texture { get; }
        internal int X { get; }
        internal int Y { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal int Border { get; }

        internal Ae2ImageRegion(string texture, int x, int y, int width, int height, int border)
        {
            Texture = texture;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Border = border;
        }

        internal bool Fits(int textureWidth, int textureHeight) => Width > 0 && Height > 0 && X >= 0 && Y >= 0 &&
            (long)X + Width <= textureWidth && (long)Y + Height <= textureHeight;
    }

    // Reads only the AE2-style blitter declarations used by this mod, not Minecraft's full screen schema.
    internal sealed class Ae2ScreenStyle
    {
        private readonly Dictionary<Ae2Image, Ae2ImageRegion> _regions = new Dictionary<Ae2Image, Ae2ImageRegion>();
        private readonly List<string> _warnings = new List<string>();
        internal IReadOnlyList<string> Warnings => _warnings;
        internal bool TryGet(Ae2Image image, out Ae2ImageRegion region) => _regions.TryGetValue(image, out region);

        internal static Ae2ScreenStyle Parse(string json)
        {
            var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var style = new Ae2ScreenStyle();
            foreach (Ae2Image image in Enum.GetValues(typeof(Ae2Image)))
            {
                var section = IsBoardPart(image) ? "terminalStyle" : "images";
                var name = image.ToString();
                name = char.ToLowerInvariant(name[0]) + name.Substring(1);
                var entry = (root[section] as JObject)?[name] as JObject;
                if (entry != null && TryParseRegion(entry, out var region)) style._regions.Add(image, region);
                else style._warnings.Add(section + "." + name + " is missing or invalid");
            }
            return style;
        }

        internal static bool IsBoardPart(Ae2Image image) => image <= Ae2Image.Bottom;

        // Source height each board part must have so the layout's section positions match the PNG.
        internal static int BoardPartHeight(Ae2Image image) => image switch
        {
            Ae2Image.Header => Ae2TerminalLayout.Header,
            Ae2Image.Bottom => Ae2TerminalLayout.Bottom,
            _ => Ae2TerminalLayout.Pitch
        };

        private static bool TryParseRegion(JObject entry, out Ae2ImageRegion region)
        {
            region = default;
            if (entry["texture"] is not JValue { Value: string path } || !IsTexturePath(path) ||
                entry["srcRect"] is not JArray rect || rect.Count != 4 ||
                !TryNonnegativeInt(rect[0], out var x) || !TryNonnegativeInt(rect[1], out var y) ||
                !TryNonnegativeInt(rect[2], out var width) || width == 0 ||
                !TryNonnegativeInt(rect[3], out var height) || height == 0) return false;

            var border = 0;
            if (entry.TryGetValue("border", out var borderToken) &&
                (!TryNonnegativeInt(borderToken, out border) || border > (Math.Min(width, height) - 1) / 2)) return false;

            region = new Ae2ImageRegion(path, x, y, width, height, border);
            return true;
        }

        private static bool TryNonnegativeInt(JToken token, out int number)
        {
            // Very large JSON integers may be represented by BigInteger, not long.
            if (token is JValue { Type: JTokenType.Integer, Value: long value } && value >= 0 && value <= int.MaxValue)
            {
                number = (int)value;
                return true;
            }
            number = 0;
            return false;
        }

        private static bool IsTexturePath(string path)
        {
            if ((!path.StartsWith("guis/", StringComparison.Ordinal) && !path.StartsWith("gui/sprites/", StringComparison.Ordinal)) ||
                !path.EndsWith(".png", StringComparison.Ordinal)) return false;
            var parts = path.Substring(0, path.Length - 4).Split('/');
            foreach (var part in parts)
            {
                if (part.Length == 0) return false;
                foreach (var c in part)
                    if (c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')) return false;
            }
            return true;
        }
    }
}
