using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal sealed class Ae2TerminalSkin : IDisposable
    {
        internal static readonly Color Ink = new Color32(48, 50, 58, 255);
        internal static readonly Color Shell = new Color32(199, 201, 210, 255);
        internal static readonly Color Slot = new Color32(163, 167, 184, 255);
        internal static readonly Color Focus = new Color32(170, 216, 255, 255);
        private static readonly Color SlotHoverFill = Argb(Ae2SlotAppearance.HoverFill);
        private static readonly Color SlotHoverBorder = Argb(Ae2SlotAppearance.HoverBorder);
        private static readonly string Root = Path.Combine(
            Path.GetDirectoryName(typeof(Ae2TerminalSkin).Assembly.Location)!, "Assets", "AE2");
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Texture2D?> _textures = new Dictionary<string, Texture2D?>();
        private readonly Dictionary<Ae2Image, Ae2ImageRegion?> _regions = new Dictionary<Ae2Image, Ae2ImageRegion?>();
        private readonly Dictionary<Ae2Image, Sprite> _sprites = new Dictionary<Ae2Image, Sprite>();
        private readonly Dictionary<(Ae2Image, int, int), Sprite> _slices = new Dictionary<(Ae2Image, int, int), Sprite>();
        private Ae2ScreenStyle? _style;
        private bool _loadedStyle;

        internal Ae2TerminalSkin(ManualLogSource log) { _log = log; }

        private void LoadStyle()
        {
            if (_loadedStyle) return;
            _loadedStyle = true;
            const string name = "screens/terminals/terminal.json";
            try
            {
                _style = Ae2ScreenStyle.Parse(File.ReadAllText(Path.Combine(Root, name)));
                foreach (var warning in _style.Warnings) _log.LogWarning("AE2 terminal screen: " + warning);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _log.LogWarning("AE2 terminal screen unavailable (" + name + "): " + ex.Message);
            }
        }

        private Texture2D? LoadTexture(string name)
        {
            if (_textures.TryGetValue(name, out var cached)) return cached;
            Texture2D? texture = null;
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(Root, "textures", name));
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "UIT.AE2." + name };
                // LoadImage can reset texture settings during decoding.
                if (!ImageConversion.LoadImage(texture, bytes, true)) throw new InvalidDataException("Invalid image");
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                _textures.Add(name, texture);
                return texture;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or UnityException)
            {
                if (texture) UnityEngine.Object.Destroy(texture);
                _textures.Add(name, null);
                _log.LogWarning("AE2 terminal texture unavailable (" + name + "): " + ex.Message);
                return null;
            }
        }

        private Ae2ImageRegion? Resolve(Ae2Image id)
        {
            if (_regions.TryGetValue(id, out var cached)) return cached;
            LoadStyle();
            Ae2ImageRegion? resolved = null;
            if (_style != null && _style.TryGet(id, out var region) && LoadTexture(region.Texture) is { } texture)
            {
                if (!region.Fits(texture.width, texture.height))
                    _log.LogWarning("AE2 terminal region outside its texture (" + id + "): " + region.Texture);
                else if (Ae2ScreenStyle.IsBoardPart(id) && (region.Width != Ae2TerminalLayout.BoardWidth ||
                    region.Height != Ae2ScreenStyle.BoardPartHeight(id) || region.Border != 0))
                    _log.LogWarning("AE2 terminal " + id + " must be " + Ae2TerminalLayout.BoardWidth + "x" +
                        Ae2ScreenStyle.BoardPartHeight(id) + " without a slice border; it is drawn as-is.");
                else if (id == Ae2Image.VerticalButtonsBg && (region.Width != Ae2TerminalLayout.ButtonBarWidth ||
                    region.Height <= Ae2TerminalLayout.ButtonBarTopBorder + Ae2TerminalLayout.ButtonBarBottomBorder))
                    _log.LogWarning("AE2 vertical button background must be " + Ae2TerminalLayout.ButtonBarWidth +
                        " px wide and taller than its borders.");
                else resolved = region;
            }
            _regions.Add(id, resolved);
            return resolved;
        }

        // Source pixel size, so callers can draw an image at its own size instead of stretching it.
        internal Vector2Int? SourceSize(Ae2Image id) => Resolve(id) is { } region ? new Vector2Int(region.Width, region.Height) : null;

        internal bool Draw(Rect bounds, Ae2Image id, Color fallback)
        {
            if (Event.current.type != EventType.Repaint) return true;
            if (Resolve(id) is { } region)
            {
                var texture = _textures[region.Texture]!;
                GUI.DrawTextureWithTexCoords(bounds, texture,
                    new Rect((float)region.X / texture.width, (float)(texture.height - region.Y - region.Height) / texture.height,
                        (float)region.Width / texture.width, (float)region.Height / texture.height));
                return true;
            }
            if (fallback.a > 0)
            {
                var color = GUI.color;
                GUI.color = fallback;
                GUI.DrawTexture(bounds, Texture2D.whiteTexture);
                GUI.color = color;
            }
            return false;
        }

        internal Sprite? GetSprite(Ae2Image id)
        {
            if (_sprites.TryGetValue(id, out var sprite)) return sprite;
            if (Resolve(id) is not { } region) return null;
            var texture = _textures[region.Texture]!;
            sprite = Sprite.Create(texture, new Rect(region.X, texture.height - region.Y - region.Height, region.Width, region.Height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            _sprites.Add(id, sprite);
            return sprite;
        }

        // Rows [top, top + height) of a region, for images that AE2 draws as a vertical nine-slice.
        internal Sprite? GetRows(Ae2Image id, int top, int height)
        {
            if (Resolve(id) is not { } region || top < 0 || height <= 0 || top + height > region.Height) return null;
            var key = (id, top, height);
            if (_slices.TryGetValue(key, out var sprite)) return sprite;
            var texture = _textures[region.Texture]!;
            sprite = Sprite.Create(texture, new Rect(region.X, texture.height - region.Y - top - height, region.Width, height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            _slices.Add(key, sprite);
            return sprite;
        }

        // Images are used exactly as cut from their PNG: no slicing or tiling.
        internal void Apply(Image image, Ae2Image id, Color fallback)
        {
            var sprite = GetSprite(id);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = sprite ? Color.white : fallback;
        }

        private static Color32 Argb(uint value) => new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));

        // AE2 slot highlight: a one-pixel cyan outline and a translucent blue interior.
        internal static Image CreateSlotHighlight(Transform parent)
        {
            var root = NativeFlatInventoryPanel.Add(parent, "AE2 slot highlight", typeof(Image));
            var fill = root.GetComponent<Image>();
            fill.raycastTarget = false;
            fill.color = SlotHoverFill;
            NativeFlatInventoryPanel.Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
            for (var side = 0; side < 4; ++side)
            {
                var edge = NativeFlatInventoryPanel.Add(root.transform, "Edge " + side, typeof(Image)).GetComponent<Image>();
                edge.raycastTarget = false;
                edge.color = SlotHoverBorder;
            }
            root.SetActive(false);
            return fill;
        }

        internal static void SizeSlotHighlight(Image fill, int scale)
        {
            var root = fill.rectTransform;
            for (var side = 0; side < 4; ++side)
            {
                var edge = (RectTransform)root.GetChild(side);
                // Horizontal edges stretch across the entire width; vertical edges connect them.
                var anchor = side % 2;
                edge.anchorMin = side < 2 ? new Vector2(0, anchor) : new Vector2(anchor, 0);
                edge.anchorMax = side < 2 ? new Vector2(1, anchor) : new Vector2(anchor, 1);
                edge.offsetMin = side switch
                {
                    0 => Vector2.zero, 1 => new Vector2(0, -scale),
                    2 => new Vector2(0, scale), _ => new Vector2(-scale, scale)
                };
                edge.offsetMax = side switch
                {
                    0 => new Vector2(0, scale), 1 => Vector2.zero,
                    2 => new Vector2(scale, -scale), _ => new Vector2(0, -scale)
                };
            }
        }

        internal void ApplyButtonFace(Image image)
        {
            image.overrideSprite = null;
            image.alphaHitTestMinimumThreshold = 0;
            Apply(image, Ae2Image.NativeButtonFace, Color.clear);
            if (!image.sprite) image.canvasRenderer.SetAlpha(0);
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites.Values) if (sprite) UnityEngine.Object.Destroy(sprite);
            foreach (var sprite in _slices.Values) if (sprite) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in _textures.Values) if (texture) UnityEngine.Object.Destroy(texture);
            _sprites.Clear();
            _slices.Clear();
            _textures.Clear();
            _regions.Clear();
        }
    }
}
