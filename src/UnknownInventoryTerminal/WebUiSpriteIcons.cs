using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    // Cache against the sprite's lifetime, not its potentially reusable Unity instance ID.
    internal sealed class WebUiSpriteIcons
    {
        private const int MaxDimension = 32;
        private const int MaxPngBytes = 4 * 1024;
        private const int MaxPending = 128;

        private sealed class CachedIcon { internal string? DataUrl; }

        private readonly ConditionalWeakTable<Sprite, CachedIcon> _cache = new();
        private readonly Queue<WeakReference<Sprite>> _pending = new();
        private readonly ManualLogSource _log;
        private bool _loggedFailure;

        internal WebUiSpriteIcons(ManualLogSource log) { _log = log; }

        internal string? GetOrQueue(object? source)
        {
            if (source is not Sprite sprite || !sprite) return null;
            if (_cache.TryGetValue(sprite, out var icon)) return icon.DataUrl;
            if (_pending.Count < MaxPending)
            {
                _cache.Add(sprite, new CachedIcon());
                _pending.Enqueue(new WeakReference<Sprite>(sprite));
            }
            return null;
        }

        // GPU readback can stall; do at most one cache miss per frame, outside snapshot publishing.
        internal void Update()
        {
            if (!_pending.TryDequeue(out var pending) || !pending.TryGetTarget(out var sprite) || !sprite ||
                !_cache.TryGetValue(sprite, out var icon)) return;
            try { icon.DataUrl = Encode(sprite); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (!_loggedFailure) _log.LogDebug("WebUI item icon unavailable: " + ex.Message);
                _loggedFailure = true;
            }
        }

        private static string? Encode(Sprite sprite)
        {
            var texture = sprite.texture;
            // Tight or rotated atlas entries need mesh rendering; never show an unrelated atlas region.
            if (!texture || (sprite.packed && (sprite.packingMode != SpritePackingMode.Rectangle ||
                sprite.packingRotation != SpritePackingRotation.None)) || sprite.associatedAlphaSplitTexture) return null;
            var rect = sprite.textureRect;
            if (rect.width < 1 || rect.height < 1 || rect.xMin < 0 || rect.yMin < 0 ||
                rect.xMax > texture.width || rect.yMax > texture.height) return null;
            var reduction = Mathf.Min(1f, MaxDimension / Mathf.Max(rect.width, rect.height));
            var width = Mathf.Clamp(Mathf.RoundToInt(rect.width * reduction), 1, MaxDimension);
            var height = Mathf.Clamp(Mathf.RoundToInt(rect.height * reduction), 1, MaxDimension);
            var scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
            var offset = new Vector2(rect.x / texture.width, rect.y / texture.height);

            var previous = RenderTexture.active;
            RenderTexture? target = null;
            Texture2D? readback = null;
            try
            {
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, target, scale, offset);
                RenderTexture.active = target;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                var bytes = readback.EncodeToPNG();
                return bytes != null && bytes.Length > 0 && bytes.Length <= MaxPngBytes
                    ? "data:image/png;base64," + Convert.ToBase64String(bytes) : null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback) UnityEngine.Object.Destroy(readback);
                if (target) RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
