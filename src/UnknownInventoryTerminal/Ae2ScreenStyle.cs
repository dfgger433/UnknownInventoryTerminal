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
        Close, SortName, SortAmount, SortAscending, SortDescending, Back, Settings, Refresh, Thumb, ThumbDisabled, NativeButtonFace,
        SettingsBackground, SettingsToggleOff, SettingsToggleOn, SettingsToggleOffHover, SettingsToggleOnHover,
        SettingsOption, SettingsOptionDisabled, SettingsOptionHover, SettingsOptionSelected,
        SettingsDropDownClosed, SettingsDropDownClosedDisabled, SettingsDropDownClosedHover, SettingsDropDownClosedPressed,
        SettingsDropDownOpen, SettingsDropDownOpenDisabled, SettingsDropDownOpenHover,
        TerminalStyleSmall, TerminalStyleMedium, TerminalStyleTall, TerminalStyleFull, VerticalButtonsBg,
        DragDrain, DragUse
    }

    internal static class Ae2SettingsLayout
    {
        internal const int PanelWidth = 124, PanelHeight = 102;
        internal const int FieldX = 6, FieldWidth = 112, FieldHeight = 20;
        internal const int LanguageY = 27, PlacementY = 58, LabelHeight = 11, OptionHeight = 19, MaxVisibleOptions = 4;
        internal const int KeepDraggingHeight = 14;
        internal const int ToggleWidth = 28, ToggleHeight = 14, ToggleKnobWidth = 8, ToggleKnobInset = 2;

        internal static int KeepDraggingY(int visibleOptions) => PlacementY + FieldHeight + 5 + visibleOptions * OptionHeight;

        internal static (int X, int Y, int Width, int Height) ToggleControlBounds(int width, int height, int scale) =>
            ((width - ToggleWidth - 2) * scale, ((height - ToggleHeight) / 2) * scale,
                ToggleWidth * scale, ToggleHeight * scale);

        internal static (int X, int Y, int Width, int Height) ToggleCaptionBounds(int width, int height, int scale) =>
            (4 * scale, ((height - Math.Min(height, ToggleHeight)) / 2) * scale,
                (width - ToggleWidth - 10) * scale, Math.Min(height, ToggleHeight) * scale);

        internal static int FieldY(bool placement, int languageRows) =>
            placement ? PlacementY + languageRows * OptionHeight : LanguageY;

        internal static int ListY(bool placement) => FieldY(placement, 0) + FieldHeight;
        internal static int PageHeight(int rows) => PanelHeight + rows * OptionHeight;

        // Leave the bevel/shadow and (on the field) the arrow column outside the caption.
        internal static (int X, int Y, int Width, int Height) CaptionBounds(int width, int height, int scale, bool field) =>
            (4 * scale, scale, width - (field ? 20 : 8) * scale, height - 4 * scale);
    }

    internal static class Ae2DropdownAppearance
    {
        internal static Ae2Image Field(bool open, bool enabled, bool hovered, bool pressed) => open
            ? !enabled ? Ae2Image.SettingsDropDownOpenDisabled : hovered && !pressed ? Ae2Image.SettingsDropDownOpenHover : Ae2Image.SettingsDropDownOpen
            : !enabled ? Ae2Image.SettingsDropDownClosedDisabled : pressed ? Ae2Image.SettingsDropDownClosedPressed : hovered ? Ae2Image.SettingsDropDownClosedHover : Ae2Image.SettingsDropDownClosed;

        internal static Ae2Image Option(bool enabled, bool hovered, bool selected) => !enabled ? Ae2Image.SettingsOptionDisabled
            : hovered ? Ae2Image.SettingsOptionHover : selected ? Ae2Image.SettingsOptionSelected : Ae2Image.SettingsOption;
    }

    internal static class Ae2ToggleAppearance
    {
        internal static uint TrackColor(bool value, bool hovered) => value
            ? hovered ? 0xFF48A889 : 0xFF358A70
            : hovered ? 0xFF737D8A : 0xFF606975;

        internal static int KnobX(bool value) => value
            ? Ae2SettingsLayout.ToggleWidth - Ae2SettingsLayout.ToggleKnobInset - Ae2SettingsLayout.ToggleKnobWidth
            : Ae2SettingsLayout.ToggleKnobInset;
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

    internal static class Ae2NineSlice
    {
        // Source cuts stay in texels; destination cuts reserve whole UI pixels at the terminal scale.
        internal static (int Start, int Length) Span(int length, int border, int part) => part switch
        {
            0 => (0, border),
            1 => (border, length - 2 * border),
            _ => (length - border, border)
        };
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
