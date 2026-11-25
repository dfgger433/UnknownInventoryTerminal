// SPDX-License-Identifier: GPL-3.0-only
using System;
using UnityEngine;

// Only the IMGUI inputs and recorded call arguments needed by the linked settings controls.
namespace UnityEngine
{
    internal readonly record struct Vector2(float x, float y);
    internal readonly record struct Vector2Int(int x, int y);
    internal readonly record struct Rect(float x, float y, float width, float height)
    {
        internal bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
    }

    internal readonly record struct Color32(byte r, byte g, byte b, byte a);
    internal readonly record struct Color(float r, float g, float b, float a)
    {
        internal static Color clear => default;
        public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    internal enum EventType { Layout, MouseDown, MouseDrag, Repaint, MouseUp, Ignore, Used }
    internal enum FocusType { Passive }

    internal sealed class Event(EventType initialType, Vector2 mouse)
    {
        internal static Event current { get; set; } = null!;
        internal EventType type { get; private set; } = initialType;
        internal Vector2 mousePosition { get; } = mouse;
        internal int button { get; init; }
        internal void Use() => type = EventType.Used;
        internal EventType GetTypeForControl(int id) => type is EventType.MouseDrag or EventType.MouseUp
            && GUIUtility.hotControl != 0 && GUIUtility.hotControl != id ? EventType.Ignore : type;
    }

    internal static class GUIUtility
    {
        private static int _nextId;
        internal static int hotControl { get; set; }
        // Each event traverses controls in the same order; capture survives between passes.
        internal static void BeginPass() => _nextId = 0;
        internal static int GetControlID(FocusType _) => ++_nextId;
    }

    internal static class GUI
    {
        internal static bool enabled { get; set; }
        internal static bool Button(Rect _, GUIContent content, GUIStyle style) => throw new NotSupportedException("Toggle is not under test.");
    }

    internal sealed class GUIContent(string text)
    {
        internal string Text { get; } = text;
        internal static GUIContent none { get; } = new("");
    }

    internal sealed class GUIStyle
    {
        internal static GUIStyle none { get; } = new();
        internal int fontSize { get; set; }
        internal State normal { get; } = new();
        internal (Rect Bounds, string Text, Color Tint, bool Hover, bool Active, bool On, bool Focus)? LastDraw { get; set; }
        internal Vector2 CalcSize(GUIContent _) => default; // Test captions fit without font resizing.
        internal void Draw(Rect bounds, GUIContent content, bool isHover, bool isActive, bool on, bool hasKeyboardFocus) =>
            LastDraw = (bounds, content.Text, normal.textColor, isHover, isActive, on, hasKeyboardFocus);

        internal sealed class State
        {
            internal Color textColor { get; set; }
        }
    }
}

namespace UnknownInventoryTerminal
{
    internal sealed class Ae2TerminalSkin
    {
        internal static readonly Color Slot = new Color32(163, 167, 184, 255);
        internal static readonly Color Focus = new Color32(170, 216, 255, 255);
        internal (Rect Bounds, Ae2Image Face, bool Open, uint Ink, uint Fill)? LastFrame { get; set; }
        internal (Rect Bounds, Ae2Image Face, int Scale, Color Fallback)? LastPanel { get; set; }

        internal Vector2Int? SourceSize(Ae2Image _) => null; // Exercise the bundled size fallback without loading assets.
        internal void DrawDropdownFrame(Rect bounds, Ae2Image id, bool open, uint ink, uint fallbackFill) =>
            LastFrame = (bounds, id, open, ink, fallbackFill);
        internal void DrawPanel(Rect bounds, Ae2Image id, int scale, Color fallback) =>
            LastPanel = (bounds, id, scale, fallback);
        internal bool Draw(Rect _, Ae2Image id, Color fallback) => throw new NotSupportedException("Toggle is not under test.");
    }
}
