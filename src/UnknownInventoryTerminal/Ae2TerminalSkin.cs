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
        private static readonly string Root = Path.Combine(
            Path.GetDirectoryName(typeof(Ae2TerminalSkin).Assembly.Location)!, "Assets", "AE2");
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Texture2D?> _textures = new Dictionary<string, Texture2D?>();
        private readonly Dictionary<Ae2Image, Ae2ImageRegion?> _regions = new Dictionary<Ae2Image, Ae2ImageRegion?>();
        private readonly Dictionary<(Ae2Image, int), Sprite> _sprites = new Dictionary<(Ae2Image, int), Sprite>();
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
                else if ((id is Ae2Image.MainSlot or Ae2Image.EquipmentSlot) &&
                    (region.Width % Ae2TerminalLayout.Columns != 0 || region.Border != 0))
                    _log.LogWarning("AE2 terminal slot strip must divide into six equal cells and have no slice border (" + id + ").");
                else resolved = region;
            }
            _regions.Add(id, resolved);
            return resolved;
        }

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

        internal Sprite? GetSprite(Ae2Image id, int column = -1)
        {
            if (_sprites.TryGetValue((id, column), out var sprite)) return sprite;
            if (Resolve(id) is not { } region) return null;
            var texture = _textures[region.Texture]!;
            var width = column < 0 ? region.Width : region.Width / Ae2TerminalLayout.Columns;
            var x = region.X + (column < 0 ? 0 : column * width);
            var border = Vector4.one * region.Border;
            sprite = Sprite.Create(texture, new Rect(x, texture.height - region.Y - region.Height, width, region.Height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            _sprites.Add((id, column), sprite);
            return sprite;
        }

        internal void Apply(Image image, Ae2Image id, Color fallback, int column = -1)
        {
            var sprite = GetSprite(id, column);
            image.sprite = sprite;
            image.type = sprite && sprite!.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = sprite ? Color.white : fallback;
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
            foreach (var texture in _textures.Values) if (texture) UnityEngine.Object.Destroy(texture);
            _sprites.Clear();
            _textures.Clear();
            _regions.Clear();
        }
    }
}
