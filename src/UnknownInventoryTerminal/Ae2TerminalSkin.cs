using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // Source rectangles are in AE2's top-left texture coordinates.
    internal sealed class Ae2TerminalSkin : IDisposable
    {
        internal const string Terminal = "terminal", States = "states", Search = "text_field";
        internal const string Thumb = "big_scroller", DisabledThumb = "big_scroller_disabled";
        internal static readonly Color Ink = new Color32(48, 50, 58, 255);
        internal static readonly Color Shell = new Color32(199, 201, 210, 255);
        internal static readonly Color Slot = new Color32(163, 167, 184, 255);
        internal static readonly Color Focus = new Color32(170, 216, 255, 255);
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Texture2D?> _textures = new Dictionary<string, Texture2D?>();
        private readonly Dictionary<(string, RectInt), Sprite> _sprites = new Dictionary<(string, RectInt), Sprite>();

        internal Ae2TerminalSkin(ManualLogSource log) { _log = log; }

        private Texture2D? Load(string name)
        {
            if (_textures.TryGetValue(name, out var cached)) return cached ? cached : null;
            Texture2D? texture = null;
            try
            {
                var root = Path.GetDirectoryName(typeof(Ae2TerminalSkin).Assembly.Location)!;
                var bytes = File.ReadAllBytes(Path.Combine(root, "Assets", "AE2", name + ".png"));
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "UIT.AE2." + name,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!ImageConversion.LoadImage(texture, bytes, true)) throw new InvalidDataException("Invalid PNG");
                _textures[name] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                if (texture) UnityEngine.Object.Destroy(texture);
                _textures[name] = null;
                _log.LogWarning("AE2 terminal texture unavailable (" + name + "): " + ex.Message);
                return null;
            }
        }

        internal bool Draw(Rect bounds, string atlas, int x, int y, int w, int h, Color fallback)
        {
            if (Event.current.type != EventType.Repaint) return true;
            var texture = Load(atlas);
            if (texture)
            {
                GUI.DrawTextureWithTexCoords(bounds, texture,
                    new Rect((float)x / texture!.width, (float)(texture.height - y - h) / texture.height,
                        (float)w / texture.width, (float)h / texture.height));
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

        internal void ApplyButtonFace(Image image)
        {
            // Unity rejects writes to the threshold while an unreadable sprite is assigned,
            // even if the new threshold is zero. Use rectangular hits before assigning the AE2 face.
            image.overrideSprite = null;
            image.sprite = null;
            image.alphaHitTestMinimumThreshold = 0;
            Apply(image, Terminal, 255, 255, 1, 1, Color.clear);
            if (!image.sprite) image.canvasRenderer.SetAlpha(0);
        }

        internal void Apply(Image image, string atlas, int x, int y, int w, int h, Color fallback)
        {
            var key = (atlas, new RectInt(x, y, w, h));
            if (!_sprites.TryGetValue(key, out var sprite))
            {
                var texture = Load(atlas);
                if (texture)
                {
                    sprite = Sprite.Create(texture, new Rect(x, texture!.height - y - h, w, h),
                        new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                    _sprites[key] = sprite;
                }
            }
            image.sprite = sprite;
            image.color = sprite ? Color.white : fallback;
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites.Values) if (sprite) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in _textures.Values) if (texture) UnityEngine.Object.Destroy(texture);
            _sprites.Clear();
            _textures.Clear();
        }
    }
}
