// SPDX-License-Identifier: GPL-3.0-only
using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnityEngine;

namespace UnknownInventoryTerminal.Tests;

// Call-level contracts only: no Unity engine, texture assets or rendering.
[TestClass]
[DoNotParallelize]
public sealed class Ae2SettingsControlsTests
{
    private const int Scale = 2;
    private const string Text = "English";
    private static readonly Color OriginalTint = new Color32(70, 80, 90, 255);
    private readonly Ae2TerminalSkin _skin = new();
    private readonly GUIStyle _label = new() { fontSize = 16 };
    private readonly Ae2SettingsControls _controls;

    public Ae2SettingsControlsTests()
    {
        _controls = new(_skin);
        _label.normal.textColor = OriginalTint;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DropdownMovesCaptionWithFrameAndForwardsArrowState(bool open)
    {
        var bounds = new Rect(13, 27, 224, 40);
        var outside = new Vector2(bounds.x - 1, bounds.y);
        // Inside the hit rectangle, but above the inset face and right of the caption.
        var edge = new Vector2(bounds.x + bounds.width - 1, bounds.y);
        bool Draw() => _controls.Dropdown(bounds, Scale, Text, _label, open, enabled: true);

        AssertNoDraw(EventType.Layout, edge, Draw);
        AssertFrame(outside, open ? Ae2Image.SettingsDropDownOpen : Ae2Image.SettingsDropDownClosed,
            offset: open ? 1 : 0, 0xFF413F54u, open ? 0xFF9CD3FFu : 0xFF9A9FB4u);
        var hover = open ? Ae2Image.SettingsDropDownOpenHover : Ae2Image.SettingsDropDownClosedHover;
        AssertFrame(edge, hover, offset: 1, 0xFF294F70u, 0xFF9CD3FFu);
        AssertNoDraw(EventType.MouseDown, edge, Draw, consumed: true);
        AssertFrame(edge, open ? Ae2Image.SettingsDropDownOpenPressed : Ae2Image.SettingsDropDownClosedPressed,
            offset: 2, 0xFF263D5Bu, 0xFF9CD3FFu);
        AssertNoDraw(EventType.MouseUp, edge, Draw, consumed: true, clicked: true);
        GUIUtility.hotControl.Should().Be(0);
        AssertFrame(edge, hover, offset: 1, 0xFF294F70u, 0xFF9CD3FFu);

        void AssertFrame(Vector2 mouse, Ae2Image face, int offset, uint ink, uint fill)
        {
            Pass(EventType.Repaint, mouse, Draw).Should().BeFalse();
            var y = bounds.y + offset * Scale;
            _skin.LastFrame.Should().Be((new Rect(bounds.x, y, bounds.width, (20 - offset) * Scale), face, open, ink, fill));
            _skin.LastPanel.Should().BeNull();
            AssertCaption(new Rect(bounds.x + 4 * Scale, y + Scale, 92 * Scale, 14 * Scale), ink);
        }
    }

    [TestMethod]
    public void ExpandedOptionMovesCaptionWithPanelButKeepsTheWholeRowClickable()
    {
        var bounds = new Rect(13, 67, 224, 38);
        var outside = new Vector2(bounds.x - 1, bounds.y);
        var edge = new Vector2(bounds.x + bounds.width - 1, bounds.y);
        bool Draw() => _controls.Option(3, bounds, Scale, Text, _label, selected: false);

        AssertNoDraw(EventType.Layout, edge, Draw);
        AssertPanel(outside, Ae2Image.SettingsOption, offset: 0, 0xFF171920u, Ae2TerminalSkin.Slot);
        AssertPanel(edge, Ae2Image.SettingsOptionHover, offset: 1, 0xFF294F70u, Ae2TerminalSkin.Focus);
        AssertNoDraw(EventType.MouseDown, edge, Draw, consumed: true);
        AssertPanel(edge, Ae2Image.SettingsOptionPressed, offset: 2, 0xFF263D5Bu, Ae2TerminalSkin.Focus);
        AssertNoDraw(EventType.MouseUp, edge, Draw, consumed: true, clicked: true);
        GUIUtility.hotControl.Should().Be(0);
        AssertPanel(edge, Ae2Image.SettingsOptionHover, offset: 1, 0xFF294F70u, Ae2TerminalSkin.Focus);

        void AssertPanel(Vector2 mouse, Ae2Image face, int offset, uint ink, Color fallback)
        {
            Pass(EventType.Repaint, mouse, Draw).Should().BeFalse();
            var y = bounds.y + offset * Scale;
            _skin.LastPanel.Should().Be((new Rect(bounds.x, y, bounds.width, (19 - offset) * Scale), face, Scale, fallback));
            _skin.LastFrame.Should().BeNull();
            AssertCaption(new Rect(bounds.x + 4 * Scale, y + Scale, 104 * Scale, 15 * Scale), ink);
        }
    }

    [TestInitialize]
    public void ResetInput()
    {
        GUIUtility.hotControl = 0;
        GUI.enabled = true;
    }

    private bool Pass(EventType type, Vector2 mouse, Func<bool> draw)
    {
        Event.current = new Event(type, mouse);
        GUIUtility.BeginPass();
        _skin.LastFrame = null;
        _skin.LastPanel = null;
        _label.LastDraw = null;
        return draw();
    }

    private void AssertNoDraw(EventType type, Vector2 mouse, Func<bool> draw, bool consumed = false, bool clicked = false)
    {
        Pass(type, mouse, draw).Should().Be(clicked);
        Event.current.type.Should().Be(consumed ? EventType.Used : type);
        _skin.LastFrame.Should().BeNull();
        _skin.LastPanel.Should().BeNull();
        _label.LastDraw.Should().BeNull();
    }

    private void AssertCaption(Rect rect, uint ink)
    {
        Color tint = new Color32((byte)(ink >> 16), (byte)(ink >> 8), (byte)ink, (byte)(ink >> 24));
        _label.LastDraw.Should().Be((rect, Text, tint, false, false, false, false));
        _label.normal.textColor.Should().Be(OriginalTint);
    }
}
