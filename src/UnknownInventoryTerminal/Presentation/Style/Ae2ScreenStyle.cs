// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnknownInventoryTerminal
{
    internal enum Ae2Image
    {
        // terminalStyle parts, as in AE2's base_terminal.json.
        Header, FirstRow, Row, LastRow, Bottom,
        Search, SearchFocused, Tooltip, Button, ButtonFocus, ButtonHover, ButtonPressed, ButtonDisabled,
        Close, SortName, SortAmount, SortAscending, SortDescending, Back, Settings, Refresh, Thumb, ThumbDisabled, NativeButtonFace,
        SettingsBackground, SettingsToggleOff, SettingsToggleOn, SettingsToggleOffHover, SettingsToggleOnHover,
        SettingsOption, SettingsOptionDisabled, SettingsOptionHover, SettingsOptionSelected, SettingsOptionPressed,
        SettingsDropDownClosed, SettingsDropDownClosedDisabled, SettingsDropDownClosedHover, SettingsDropDownClosedPressed,
        SettingsDropDownOpen, SettingsDropDownOpenDisabled, SettingsDropDownOpenHover, SettingsDropDownOpenPressed,
        TerminalStyleSmall, TerminalStyleMedium, TerminalStyleTall, TerminalStyleFull, VerticalButtonsBg, VerticalButtonsBgVariant,
        DragDrain, DragUse, DragPin, DragUnpin, UnavailableSlotBadge, PinnedRow, PinnedRowGutter, ScrollGrooveTop, VerticalButtonsBgBottom,
        FluidRailLeft, FluidRailMiddle, FluidRailRight, FluidThumb
    }

    internal static class Ae2ButtonBarAppearance
    {
        internal static Ae2Image DragBackground(Ae2TerminalLayout layout, int top, int height) =>
            top + height == layout.Height ? Ae2Image.VerticalButtonsBgBottom : Ae2Image.VerticalButtonsBg;

        internal static bool IsBackground(Ae2Image id) => id is Ae2Image.VerticalButtonsBg or Ae2Image.VerticalButtonsBgVariant or Ae2Image.VerticalButtonsBgBottom;
    }

    internal static class Ae2SettingsLayout
    {
        internal const int PanelWidth = 124, PanelHeight = 102;
        internal const int FieldX = 6, FieldWidth = 112, FieldHeight = 20;
        internal const int LanguageY = 27, PlacementY = 58, LabelHeight = 11, OptionHeight = 19, MaxVisibleOptions = 4;
        internal const int KeepDraggingHeight = 14;
        internal const int ToggleWidth = 22, ToggleHeight = 12;

        internal static int KeepDraggingY(int visibleOptions) => PlacementY + FieldHeight + 5 + visibleOptions * OptionHeight;

        internal static (int X, int Y, int Width, int Height) ToggleControlBounds(int height, int spriteWidth, int spriteHeight, int scale) =>
            (2 * scale, (height - spriteHeight) * scale / 2, spriteWidth * scale, spriteHeight * scale);

        internal static (int X, int Y, int Width, int Height) ToggleCaptionBounds(int width, int height, int spriteWidth, int scale) =>
            ((spriteWidth + 6) * scale, 0, (width - spriteWidth - 10) * scale, height * scale);

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
        internal static (Ae2Image Face, uint Ink, uint FallbackFill) Field(bool open, bool enabled, bool hovered, bool pressed)
        {
            // Each directional field frame already contains its chevron. The entire
            // field gets one state, including the color shared by arrow and caption.
            if (!enabled) return (open ? Ae2Image.SettingsDropDownOpenDisabled : Ae2Image.SettingsDropDownClosedDisabled,
                0xFFBEC2D2, 0xFF696D88);
            if (pressed) return (open ? Ae2Image.SettingsDropDownOpenPressed : Ae2Image.SettingsDropDownClosedPressed,
                0xFF263D5B, 0xFF9CD3FF);
            if (hovered) return (open ? Ae2Image.SettingsDropDownOpenHover : Ae2Image.SettingsDropDownClosedHover,
                0xFF294F70, 0xFF9CD3FF);
            return (open ? Ae2Image.SettingsDropDownOpen : Ae2Image.SettingsDropDownClosed,
                0xFF413F54, open ? 0xFF9CD3FF : 0xFF9A9FB4);
        }

        // Fixed caption box centered on the chevron; do not center it on the varying lower shadow.
        internal const int FieldContentHeight = 18;
        internal const int ChevronLeft = 94, ChevronTop = 6, ChevronWidth = 9, ChevronHeight = 5;
        internal static int ChevronRow(bool open, int column)
        {
            var distance = Math.Abs(column - ChevronWidth / 2);
            return open ? distance : ChevronHeight - 1 - distance;
        }

        internal static bool HasChevron(int width, int height, bool open, Func<int, int, uint> pixel)
        {
            if (width != Ae2SettingsLayout.FieldWidth || height < FieldContentHeight) return false;
            for (var column = 0; column < ChevronWidth; column++)
                if (pixel(ChevronLeft + column, ChevronTop + ChevronRow(open, column)) != 0xFF413F54) return false;
            return true;
        }

        // Recognize the complete original arrow before replacing any pixels. A custom
        // skin with different arrow artwork is drawn as-is, not stamped with a second arrow.
        internal static bool TryPaintChevron(int width, int height, bool open, Func<int, int, uint> pixel, Action<int, int> paint)
        {
            if (!HasChevron(width, height, open, pixel)) return false;
            for (var column = 0; column < ChevronWidth; column++)
                paint(ChevronLeft + column, ChevronTop + ChevronRow(open, column));
            return true;
        }

        internal static int BundledFieldHeight(Ae2Image face) => face switch
        {
            Ae2Image.SettingsDropDownClosed => 20,
            Ae2Image.SettingsDropDownClosedHover or Ae2Image.SettingsDropDownOpen or Ae2Image.SettingsDropDownOpenHover => 19,
            _ => 18
        };

        internal static (int Top, int Height, (int X, int Y, int Width, int Height) Caption) FieldLayout(int frameHeight, int scale)
        {
            var height = Math.Min(frameHeight, Ae2SettingsLayout.FieldHeight) * scale;
            var top = Ae2SettingsLayout.FieldHeight * scale - height;
            var caption = Ae2SettingsLayout.CaptionBounds(Ae2SettingsLayout.FieldWidth * scale,
                Math.Min(FieldContentHeight * scale, height), scale, field: true);
            caption.Y += top;
            return (top, height, caption);
        }

        internal static Ae2Image Option(bool enabled, bool hovered, bool pressed, bool selected) => !enabled ? Ae2Image.SettingsOptionDisabled
            : pressed ? Ae2Image.SettingsOptionPressed
            : hovered ? Ae2Image.SettingsOptionHover
            : selected ? Ae2Image.SettingsOptionSelected : Ae2Image.SettingsOption;

        internal static (int Top, int Height, (int X, int Y, int Width, int Height) Caption, uint Ink) OptionLayout(Ae2Image face, int scale)
        {
            // Lift neither the hit rectangle nor the bottom of the rail. The face and
            // caption each move one more pixel from rest to hover to press.
            var (offset, ink) = face switch
            {
                Ae2Image.SettingsOptionHover => (1, 0xFF294F70u),
                Ae2Image.SettingsOptionPressed => (2, 0xFF263D5Bu),
                Ae2Image.SettingsOptionDisabled => (2, 0xFFBEC2D2u),
                _ => (0, 0xFF171920u)
            };
            var top = offset * scale;
            var height = Ae2SettingsLayout.OptionHeight * scale;
            var caption = Ae2SettingsLayout.CaptionBounds(Ae2SettingsLayout.FieldWidth * scale, height, scale, field: false);
            caption.Y += top;
            return (top, height - top, caption, ink);
        }
    }

    internal static class Ae2ToggleAppearance
    {
        // The atlas keeps separate on/off hover frames so hovering animates the knob without changing the value.
        internal static Ae2Image Face(bool value, bool hovered) => value
            ? hovered ? Ae2Image.SettingsToggleOnHover : Ae2Image.SettingsToggleOn
            : hovered ? Ae2Image.SettingsToggleOffHover : Ae2Image.SettingsToggleOff;
    }

    internal static class Ae2ToolbarAppearance
    {
        // Toolbar buttons use the atlas' four visual states: normal, focused, pressed and disabled.
        // Keep the old overload for callers that only have hover/focus information.
        internal static Ae2Image Face(bool hovered, bool focused) => hovered ? Ae2Image.ButtonHover :
            focused ? Ae2Image.ButtonFocus : Ae2Image.Button;

        internal static Ae2Image Face(bool hovered, bool focused, bool pressed, bool enabled) => !enabled
            ? Ae2Image.ButtonDisabled
            : pressed ? Ae2Image.ButtonPressed
            : hovered ? Ae2Image.ButtonHover
            : focused ? Ae2Image.ButtonFocus : Ae2Image.Button;

        internal static int OffsetY(Ae2Image face) => face is Ae2Image.ButtonHover or Ae2Image.ButtonPressed or Ae2Image.ButtonDisabled ? 1 : 0;

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

    internal static class Ae2UnavailableSlotBadge
    {
        // The transparent atlas padding is removed before the visible icon is centered at
        // its native aspect ratio inside the slot.
        internal static (int X, int Y, int Width, int Height) Bounds(
            (int Width, int Height) visible, int scale)
        {
            var cell = Ae2TerminalLayout.Pitch * scale;
            return ((cell - visible.Width * scale) / 2, (cell - visible.Height * scale) / 2,
                visible.Width * scale, visible.Height * scale);
        }
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

    internal static class TerminalAssets
    {
        private const string Namespace = "unknowninventoryterminal";
        private const string TexturePrefix = Namespace + ":textures/";

        internal static string RootUnder(string directory) => Path.Combine(directory, "assets", Namespace);
        internal static string ScreenPath(string root) => Path.Combine(root, "screens", "terminals", "terminal.json");
        internal static string TexturePath(string root, string relative) =>
            Path.Combine(root, "textures", relative.Replace('/', Path.DirectorySeparatorChar));

        // Resolve only this mod's namespaced PNGs. Do not normalize legacy IDs or arbitrary filesystem paths.
        internal static bool TryGetTextureRelativePath(string id, out string path)
        {
            path = string.Empty;
            if (!id.StartsWith(TexturePrefix, StringComparison.Ordinal) || !id.EndsWith(".png", StringComparison.Ordinal))
                return false;

            var relative = id.Substring(TexturePrefix.Length);
            // Keep the resource layout aligned with NeoForge: every runtime texture lives below textures/gui/.
            // Subdirectories are allowed so terminal, settings, storage and fluid artwork can stay grouped by use.
            if (!relative.StartsWith("gui/", StringComparison.Ordinal))
                return false;
            foreach (var part in relative.Substring(0, relative.Length - 4).Split('/'))
            {
                if (part.Length == 0) return false;
                foreach (var c in part)
                    if (c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')) return false;
            }

            path = relative;
            return true;
        }
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
            if (entry["texture"] is not JValue { Value: string id } || !TerminalAssets.TryGetTextureRelativePath(id, out var path) ||
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
    }
}
