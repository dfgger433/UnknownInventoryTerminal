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

        private Texture2D? LoadAsset(string category, string name)
        {
            var key = category + "/" + name;
            if (_textures.TryGetValue(key, out var cached)) return cached ? cached : null;
            Texture2D? texture = null;
            try
            {
                var root = Path.GetDirectoryName(typeof(Ae2TerminalSkin).Assembly.Location)!;
                var path = Path.Combine(root, "Assets", "AE2_New", category, name + ".png");
                if (!File.Exists(path))
                {
                    // Fallback to old path if not found in new structure
                    path = Path.Combine(root, "Assets", "AE2", name + ".png");
                }
                
                var bytes = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "UIT.AE2." + key,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!ImageConversion.LoadImage(texture, bytes, true)) throw new InvalidDataException("Invalid PNG");
                _textures[key] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                if (texture) UnityEngine.Object.Destroy(texture);
                _textures[key] = null;
                _log.LogWarning("AE2 terminal texture unavailable (" + key + "): " + ex.Message);
                return null;
            }
        }

        internal bool Draw(Rect bounds, string category, string name, int x, int y, int w, int h, Color fallback)
        {
            if (Event.current.type != EventType.Repaint) return true;
            var texture = LoadAsset(category, name);
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
            image.overrideSprite = null;
            image.sprite = null;
            image.alphaHitTestMinimumThreshold = 0;
            // Updated to use the new category/name signature
            Apply(image, Terminal, "background", 255, 255, 1, 1, Color.clear);
            if (!image.sprite) image.canvasRenderer.SetAlpha(0);
        }

        internal void Apply(Image image, string category, string name, int x, int y, int w, int h, Color fallback, Vector4 border = default)
        {
            var key = (category, name, new RectInt(x, y, w, h));
            if (!_sprites.TryGetValue((category + "/" + name, new RectInt(x, y, w, h)), out var sprite))
            {
                var texture = LoadAsset(category, name);
                if (texture)
                {
                    sprite = Sprite.Create(texture, new Rect(x, texture!.height - y - h, w, h),
                        new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
                    _sprites[(category + "/" + name, new RectInt(x, y, w, h))] = sprite;
                }
            }
            image.sprite = sprite;
            image.type = border != default ? Image.Type.Sliced : Image.Type.Simple;
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
