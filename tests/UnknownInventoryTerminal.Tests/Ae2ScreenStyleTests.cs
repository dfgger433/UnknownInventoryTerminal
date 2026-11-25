// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class Ae2ScreenStyleTests
{
    private static readonly string BundledAssetRoot = Path.Combine(AppContext.BaseDirectory, "assets", "unknowninventoryterminal");

    [TestMethod]
    public void MigratedTerminalStyleDefinesEveryImageAndFitsItsPng()
    {
        var jsonPath = TerminalAssets.ScreenPath(BundledAssetRoot);
        File.Exists(jsonPath).Should().BeTrue($"the migrated terminal screen definition must be copied to test output: {jsonPath}");

        var style = Ae2ScreenStyle.Parse(File.ReadAllText(jsonPath));
        style.Warnings.Should().BeEmpty("the shipped screen definition must declare all supported images");

        foreach (Ae2Image image in Enum.GetValues(typeof(Ae2Image)))
        {
            style.TryGet(image, out var region).Should().BeTrue($"{image} must have a region");
            region.Border.Should().Be(image switch
            {
                Ae2Image.SettingsBackground => 4,
                Ae2Image.Tooltip or Ae2Image.SettingsOptionPressed => 3,
                Ae2Image.SettingsOption or Ae2Image.SettingsOptionDisabled or Ae2Image.SettingsOptionHover or Ae2Image.SettingsOptionSelected => 2,
                _ => 0
            });
            if (Ae2ScreenStyle.IsBoardPart(image))
            {
                region.Width.Should().Be(Ae2TerminalLayout.BoardWidth, "board parts are stacked unscaled");
                region.Height.Should().Be(Ae2ScreenStyle.BoardPartHeight(image), "board parts are stacked unscaled");
            }

            var texturePath = TextureFilePath(region.Texture);
            File.Exists(texturePath).Should().BeTrue($"{image} references a migrated PNG at {texturePath}");
            var (width, height) = ReadPngDimensions(texturePath);
            region.Fits(width, height).Should().BeTrue(
                $"{image} source rectangle [{region.X}, {region.Y}, {region.Width}, {region.Height}] must fit {region.Texture} ({width}x{height})");
        }
    }

    [TestMethod]
    [DataRow((int)Ae2Image.SettingsDropDownClosed, 0, 0, 20)]
    [DataRow((int)Ae2Image.SettingsDropDownClosedDisabled, 0, 32, 18)]
    [DataRow((int)Ae2Image.SettingsDropDownClosedHover, 0, 64, 19)]
    [DataRow((int)Ae2Image.SettingsDropDownClosedPressed, 0, 96, 18)]
    [DataRow((int)Ae2Image.SettingsDropDownOpen, 112, 64, 19)]
    [DataRow((int)Ae2Image.SettingsDropDownOpenDisabled, 112, 32, 18)]
    [DataRow((int)Ae2Image.SettingsDropDownOpenHover, 112, 64, 19)]
    [DataRow((int)Ae2Image.SettingsDropDownOpenPressed, 112, 96, 18)]
    [DataRow((int)Ae2Image.SettingsOption, 0, 128, 19)]
    [DataRow((int)Ae2Image.SettingsOptionDisabled, 0, 160, 17)]
    [DataRow((int)Ae2Image.SettingsOptionHover, 112, 192, 17)]
    [DataRow((int)Ae2Image.SettingsOptionSelected, 0, 192, 18)]
    public void SettingsDropdownKeepsAllSourceEdges(int image, int x, int y, int height)
    {
        var style = ReadBundledStyle();
        style.TryGet((Ae2Image)image, out var region).Should().BeTrue();
        region.Texture.Should().Be("gui/widgets/drop_down_list.png");
        (region.X, region.Y, region.Width, region.Height).Should().Be((x, y, Ae2SettingsLayout.FieldWidth, height));
        using var png = PngPixels.Read(TextureFilePath(region.Texture));
        png.Alpha(x + region.Width - 1, y + 1).Should().BeGreaterThan(0);
        png.Alpha(x + region.Width / 2, y + region.Height).Should().Be(0);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    public void DropdownCaptionsStayInsideEveryFaceAndClearTheArrow(int scale)
    {
        var style = ReadBundledStyle();
        foreach (var image in Enum.GetValues<Ae2Image>().Where(image => image.ToString().StartsWith("SettingsDropDown")))
        {
            style.TryGet(image, out var region).Should().BeTrue();
            var (x, y, width, height) = Ae2SettingsLayout.CaptionBounds(region.Width * scale, region.Height * scale, scale, field: true);
            (x + width).Should().Be((region.Width - 16) * scale);
            (y + height).Should().Be((region.Height - 3) * scale);
        }
        var option = Ae2SettingsLayout.CaptionBounds(112 * scale, 19 * scale, scale, field: false);
        option.Should().Be((4 * scale, scale, 104 * scale, 15 * scale));
    }

    [TestMethod]
    [DataRow(true, false, false, false, (int)Ae2Image.SettingsOption, 0, 0xFF171920u)]
    [DataRow(true, false, false, true, (int)Ae2Image.SettingsOptionSelected, 0, 0xFF171920u)]
    [DataRow(true, true, false, false, (int)Ae2Image.SettingsOptionHover, 1, 0xFF294F70u)]
    [DataRow(true, true, false, true, (int)Ae2Image.SettingsOptionHover, 1, 0xFF294F70u)]
    [DataRow(true, true, true, false, (int)Ae2Image.SettingsOptionPressed, 2, 0xFF263D5Bu)]
    [DataRow(true, true, true, true, (int)Ae2Image.SettingsOptionPressed, 2, 0xFF263D5Bu)]
    [DataRow(false, true, true, true, (int)Ae2Image.SettingsOptionDisabled, 2, 0xFFBEC2D2u)]
    public void ExpandedOptionCaptionFollowsTheSameStateAsTheFace(bool enabled, bool hovered, bool pressed, bool selected,
        int image, int offset, uint expectedInk)
    {
        var face = Ae2DropdownAppearance.Option(enabled, hovered, pressed, selected);
        face.Should().Be((Ae2Image)image);
        foreach (var scale in new[] { 1, 2, 3, 4 })
        {
            var (top, height, caption, ink) = Ae2DropdownAppearance.OptionLayout(face, scale);
            top.Should().Be(offset * scale);
            (top + height).Should().Be(Ae2SettingsLayout.OptionHeight * scale);
            caption.Should().Be((4 * scale, top + scale, 104 * scale, 15 * scale));
            ink.Should().Be(expectedInk);
        }
    }

    [TestMethod]
    public void ExpandedOptionPressUsesTheButtonAtlasWithBordersAndFitsItsRow()
    {
        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.SettingsOptionPressed, out var region).Should().BeTrue();
        style.TryGet(Ae2Image.ButtonPressed, out var toolbar).Should().BeTrue();
        (region.Texture, region.X, region.Y, region.Width, region.Height)
            .Should().Be((toolbar.Texture, toolbar.X, toolbar.Y, toolbar.Width, toolbar.Height));
        using var png = PngPixels.Read(TextureFilePath(region.Texture));
        for (var x = region.X; x < region.X + region.Width; x++)
        {
            png.Alpha(x, region.Y).Should().Be(255);
            png.Alpha(x, region.Y + region.Height - 1).Should().Be(255);
        }
        foreach (var scale in new[] { 1, 2, 3, 4 })
        {
            var (top, height, caption, _) = Ae2DropdownAppearance.OptionLayout(Ae2Image.SettingsOptionPressed, scale);
            height.Should().BeGreaterThan(2 * region.Border * scale);
            (caption.Y + caption.Height).Should().BeLessThan(top + height);
        }
    }

    [TestMethod]
    [DataRow(false, true, false, false, (int)Ae2Image.SettingsDropDownClosed, 0, 0xFF413F54u)]
    [DataRow(false, true, true, false, (int)Ae2Image.SettingsDropDownClosedHover, 1, 0xFF294F70u)]
    [DataRow(false, true, true, true, (int)Ae2Image.SettingsDropDownClosedPressed, 2, 0xFF263D5Bu)]
    [DataRow(false, false, true, true, (int)Ae2Image.SettingsDropDownClosedDisabled, 2, 0xFFBEC2D2u)]
    [DataRow(true, true, false, false, (int)Ae2Image.SettingsDropDownOpen, 1, 0xFF413F54u)]
    [DataRow(true, true, true, false, (int)Ae2Image.SettingsDropDownOpenHover, 1, 0xFF294F70u)]
    [DataRow(true, true, true, true, (int)Ae2Image.SettingsDropDownOpenPressed, 2, 0xFF263D5Bu)]
    [DataRow(true, false, true, true, (int)Ae2Image.SettingsDropDownOpenDisabled, 2, 0xFFBEC2D2u)]
    public void DropdownFieldUsesOneStateForItsFaceAndContents(bool open, bool enabled, bool hover, bool pressed,
        int image, int offset, uint ink)
    {
        var visual = Ae2DropdownAppearance.Field(open, enabled, hover, pressed);
        visual.Face.Should().Be((Ae2Image)image);
        visual.Ink.Should().Be(ink);
        var style = ReadBundledStyle();
        style.TryGet(visual.Face, out var region).Should().BeTrue();
        using var png = PngPixels.Read(TextureFilePath(region.Texture));
        uint Pixel(int x, int y)
        {
            // The PNG reader returns packed ABGR; the appearance helper uses ARGB.
            var abgr = png.Pixel(region.X + x, region.Y + y);
            return (abgr & 0xFF00FF00u) | ((abgr & 0xFFu) << 16) | ((abgr >> 16) & 0xFFu);
        }
        var painted = new HashSet<(int X, int Y)>();
        Ae2DropdownAppearance.TryPaintChevron(region.Width, region.Height, open, Pixel, (x, y) => painted.Add((x, y)))
            .Should().BeTrue();
        painted.Should().HaveCount(9).And.Contain((98, open ? 6 : 10));
        Ae2DropdownAppearance.TryPaintChevron(region.Width, region.Height, !open, Pixel,
            (_, _) => Assert.Fail("A custom or mismatched chevron must not be partially recolored.")).Should().BeFalse();
        Pixel(80, 8).Should().Be(visual.FallbackFill);
        Ae2DropdownAppearance.BundledFieldHeight(visual.Face).Should().Be(region.Height);
        foreach (var scale in new[] { 1, 2, 3, 4 })
        {
            var (top, height, caption) = Ae2DropdownAppearance.FieldLayout(region.Height, scale);
            top.Should().Be(offset * scale);
            (caption.Y + caption.Height / 2).Should().Be(top + 8 * scale);
            (top + height).Should().Be(Ae2SettingsLayout.FieldHeight * scale);
        }
    }

    [TestMethod]
    [DataRow(false, false, (int)Ae2Image.SettingsToggleOff, 0, 28, 0)]
    [DataRow(true, false, (int)Ae2Image.SettingsToggleOn, 0, 40, 10)]
    [DataRow(false, true, (int)Ae2Image.SettingsToggleOffHover, 22, 28, 2)]
    [DataRow(true, true, (int)Ae2Image.SettingsToggleOnHover, 22, 40, 8)]
    public void KeepDraggingSliderUsesTheMatchingAtlasFrameAtNativeSize(bool value, bool hovered, int image, int x, int y, int knobX)
    {
        var face = Ae2ToggleAppearance.Face(value, hovered);
        face.Should().Be((Ae2Image)image);
        ReadBundledStyle().TryGet(face, out var region).Should().BeTrue();
        (region.Texture, region.X, region.Y, region.Width, region.Height).Should().Be(("gui/settings/toggle.png", x, y, 22, 12));

        using var png = PngPixels.Read(TextureFilePath(region.Texture));
        for (var px = 0; px < region.Width; px++)
        {
            png.Alpha(x + px, y).Should().Be(px >= knobX && px < knobX + 12 ? (byte)255 : (byte)0,
                "only the knob protrudes above the track");
            png.Alpha(x + px, y + region.Height - 1).Should().Be(255, "the full track reaches the last scanline");
        }

        foreach (var scale in new[] { 1, 2, 3, 4 })
        {
            const int width = Ae2SettingsLayout.FieldWidth, height = Ae2SettingsLayout.KeepDraggingHeight;
            var control = Ae2SettingsLayout.ToggleControlBounds(height, region.Width, region.Height, scale);
            var caption = Ae2SettingsLayout.ToggleCaptionBounds(width, height, region.Width, scale);
            control.Should().Be((2 * scale, scale, 22 * scale, 12 * scale));
            (caption.X - control.X - control.Width).Should().Be(4 * scale);
            (caption.X + caption.Width).Should().Be((width - 4) * scale);
            (control.Y + control.Height).Should().BeLessThan(height * scale);
            caption.Height.Should().Be(height * scale);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    public void ExpandedSettingsOptionsStayJoinedAndDoNotCoverTheNextField(int rows)
    {
        var list = Ae2SettingsLayout.ListY(placement: false);
        list.Should().Be(Ae2SettingsLayout.LanguageY + Ae2SettingsLayout.FieldHeight);
        var placement = Ae2SettingsLayout.FieldY(placement: true, rows);
        (placement - Ae2SettingsLayout.LabelHeight).Should().BeGreaterThanOrEqualTo(list + rows * Ae2SettingsLayout.OptionHeight);
        var toggle = Ae2SettingsLayout.KeepDraggingY(rows);
        (placement + Ae2SettingsLayout.FieldHeight).Should().BeLessThan(toggle);
        (Ae2SettingsLayout.ListY(placement: true) + rows * Ae2SettingsLayout.OptionHeight).Should().BeLessThan(toggle);
        (toggle + Ae2SettingsLayout.KeepDraggingHeight).Should().BeLessThan(Ae2SettingsLayout.PageHeight(rows));
    }

    [TestMethod]
    public void SettingsBackgroundKeepsBothEdgesAtEveryTerminalScale()
    {
        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.SettingsBackground, out var region).Should().BeTrue();
        region.Texture.Should().Be("gui/settings/settings_background.png");
        (region.X, region.Y, region.Width, region.Height).Should().Be((0, 0, 124, 82));
        using var bitmap = PngPixels.Read(TextureFilePath(region.Texture));
        foreach (var x in new[] { region.X, region.X + region.Width - 1 })
        foreach (var y in new[] { region.Y, region.Y + region.Height - 1 })
            bitmap.Alpha(x, y).Should().BeGreaterThan(0, "the crop must include the actual border, not transparent atlas padding");
        foreach (var length in new[] { 124, 82 })
        foreach (var scale in new[] { 1, 2, 3, 4 })
        {
            var size = length * scale;
            var border = region.Border * scale;
            Ae2NineSlice.Span(size, border, 0).Should().Be((0, border));
            Ae2NineSlice.Span(size, border, 1).Should().Be((border, size - 2 * border));
            Ae2NineSlice.Span(size, border, 2).Should().Be((size - border, border));
        }
    }

    [TestMethod]
    [DataRow((int)Ae2Image.Thumb, "gui/storage/scroll_thumb.png")]
    [DataRow((int)Ae2Image.ThumbDisabled, "gui/storage/scroll_thumb_disabled.png")]
    public void ScrollThumbUsesTheWholeNativeAe2Sprite(int image, string texture)
    {
        var style = ReadBundledStyle();

        style.TryGet((Ae2Image)image, out var region).Should().BeTrue();
        region.Texture.Should().Be(texture);
        (region.X, region.Y, region.Width, region.Height).Should().Be((0, 0, 12, 15));
        ReadPngDimensions(TextureFilePath(region.Texture)).Should().Be((region.Width, region.Height));
    }

    [TestMethod]
    public void ModernityFluidSliderAssetsMatchPinnedSourceAndCropContract()
    {
        var style = ReadBundledStyle();
        var railPath = TextureFilePath("gui/fluid/fluid_slider_rail.png");
        var thumbPath = TextureFilePath("gui/fluid/fluid_slider_thumb.png");
        ReadPngDimensions(railPath).Should().Be((82, 18));
        ReadPngDimensions(thumbPath).Should().Be((5, 18));
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(railPath)))
            .Should().Be("E106B3436DCA078D64DD2EEE22D4C5D132D6002EB193141A852B216ABDE4F154");
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(thumbPath)))
            .Should().Be("F80AC493277EF8793754724B1BE00C6096995A5DDFDB950CE68B3B9ADA9D66C4");

        foreach (var (image, texture, x, y, width, height) in new[]
        {
            (Ae2Image.FluidRailLeft, "gui/fluid/fluid_slider_rail.png", 1, 7, 1, 4),
            (Ae2Image.FluidRailMiddle, "gui/fluid/fluid_slider_rail.png", 2, 7, 1, 4),
            (Ae2Image.FluidRailRight, "gui/fluid/fluid_slider_rail.png", 80, 7, 1, 4),
            (Ae2Image.FluidThumb, "gui/fluid/fluid_slider_thumb.png", 0, 0, 5, 18)
        })
        {
            style.TryGet(image, out var region).Should().BeTrue();
            (region.Texture, region.X, region.Y, region.Width, region.Height).Should().Be((texture, x, y, width, height));
            region.Fits(ReadPngDimensions(TextureFilePath(region.Texture)).Width,
                ReadPngDimensions(TextureFilePath(region.Texture)).Height).Should().BeTrue();
        }
    }

    [TestMethod]
    public void CodeAndArtworkLicensesStaySeparateAndCreativeCorePermissionIsDocumented()
    {
        var repository = FindRepositoryRoot(AppContext.BaseDirectory);
        var licensePath = Path.Combine(repository, "assets", "unknowninventoryterminal", "licenses", "modernity", "LICENSE.txt");
        File.Exists(licensePath).Should().BeTrue();
        var licenseBytes = File.ReadAllBytes(licensePath);
        licenseBytes.Length.Should().Be(20849);
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(licenseBytes))
            .Should().Be("1349A4B6148492B44F629E64EED676612E234FE9A839E4F3B277C1482C8849F1");
        Encoding.UTF8.GetString(licenseBytes).Should().StartWith("Attribution-NonCommercial-ShareAlike 4.0 International");

        var codeLicense = File.ReadAllText(Path.Combine(repository, "LICENSE"));
        codeLicense.Should().Contain("GNU GENERAL PUBLIC LICENSE").And.Contain("Version 3, 29 June 2007")
            .And.Contain("END OF TERMS AND CONDITIONS");
        var notice = File.ReadAllText(Path.Combine(repository, "assets", "unknowninventoryterminal", "NOTICE.txt"));
        var normalizedNotice = System.Text.RegularExpressions.Regex.Replace(notice, @"\s+", " ");
        normalizedNotice.Should().Contain("GPL-3.0-only").And.Contain("does not relicense the textures")
            .And.Contain("https://creativecommons.org/licenses/by-nc-sa/3.0/")
            .And.Contain("https://creativecommons.org/licenses/by-nc-sa/4.0/")
            .And.Contain("does not establish CC BY-NC-SA 4.0 permission for the CreativeCore originals");

        var project = File.ReadAllText(Path.Combine(repository, "src", "UnknownInventoryTerminal", "UnknownInventoryTerminal.csproj"));
        project.Should().Contain("Include=\"..\\..\\LICENSE\" Link=\"LICENSE\"");
        project.Should().Contain("assets\\unknowninventoryterminal\\licenses\\modernity\\LICENSE.txt");
        project.Should().Contain("assets\\unknowninventoryterminal\\textures\\%(RecursiveDir)");
        var installer = File.ReadAllText(Path.Combine(repository, "scripts", "Install.ps1"));
        installer.Should().Contain("'LICENSE',");
        installer.Should().Contain("licenses\\modernity\\LICENSE.txt");
        installer.Should().Contain("textures\\gui\\fluid\\fluid_slider_rail.png");
        installer.Should().Contain("textures\\gui\\fluid\\fluid_slider_thumb.png");
    }

    private static string FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "assets", "unknowninventoryterminal", "licenses", "modernity", "LICENSE.txt")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not find the project assets from the test output directory.");
    }

    [TestMethod]
    [DataRow((int)Ae2Image.DragDrain, 1, 97, 14, 14, 2, 2, 10, 11)]
    [DataRow((int)Ae2Image.DragUse, 33, 97, 14, 14, 2, 2, 10, 11)]
    [DataRow((int)Ae2Image.DragPin, 53, 21, 6, 8, 0, 0, 6, 8)]
    [DataRow((int)Ae2Image.DragUnpin, 69, 20, 6, 9, 0, 0, 6, 9)]
    public void DragActionCropsContainVisibleIcons(int image, int x, int y, int width, int height,
        int visibleX, int visibleY, int visibleWidth, int visibleHeight)
    {
        var style = ReadBundledStyle();
        style.TryGet((Ae2Image)image, out var region).Should().BeTrue();
        region.Texture.Should().Be("gui/terminal/terminal_controls.png");
        (region.X, region.Y, region.Width, region.Height).Should().Be((x, y, width, height));
        using var png = PngPixels.Read(TextureFilePath(region.Texture));
        var visible = Ae2ToolbarAppearance.VisibleBounds(region.Width, region.Height,
            (px, py) => png.Alpha(region.X + px, region.Y + py));
        visible.Should().Be((visibleX, visibleY, visibleWidth, visibleHeight));
    }

    [TestMethod]
    public void LockAndUnlockIconsShareTheToolbarHorizontalCenterAtEveryScale()
    {
        var style = ReadBundledStyle();
        using var png = PngPixels.Read(TextureFilePath("gui/terminal/terminal_controls.png"));
        var icons = new[] { Ae2Image.DragPin, Ae2Image.DragUnpin };
        foreach (var scale in new[] { 1, 2, 3, 4 })
        foreach (var faceHeight in new[] { 20, 19 })
        {
            var offsets = icons.Select(icon =>
            {
                style.TryGet(icon, out var region).Should().BeTrue();
                var visible = Ae2ToolbarAppearance.VisibleBounds(region.Width, region.Height,
                    (x, y) => png.Alpha(region.X + x, region.Y + y));
                visible.Should().NotBeNull();
                return Ae2ToolbarAppearance.CenterIcon(visible!.Value, 18, faceHeight, scale);
            }).ToArray();

            offsets[0].X.Should().Be(offsets[1].X,
                $"lock state must not jump horizontally when switching at scale {scale} and face height {faceHeight}");
            offsets[0].X.Should().Be(6 * scale);
        }
    }

    [TestMethod]
    public void PinnedRowUsesTheDrawnSixSlotStripAtNativeSize()
    {
        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.PinnedRow, out var row).Should().BeTrue();
        (row.Texture, row.X, row.Y, row.Width, row.Height).Should()
            .Be(("gui/terminal/terminal_background.png", 0, 204, Ae2TerminalLayout.GridWidth, Ae2TerminalLayout.Pitch));
        using var png = PngPixels.Read(TextureFilePath(row.Texture));
        png.Alpha(row.X, row.Y - 1).Should().Be(0);
        png.Alpha(row.X, row.Y + row.Height).Should().Be(0);
        for (var i = 0; i < Ae2TerminalLayout.Columns; i++)
        {
            var left = row.X + i * Ae2TerminalLayout.Pitch;
            png.Alpha(left, row.Y).Should().Be(255);
            png.Pixel(left + 9, row.Y + 7).Should().NotBe(png.Pixel(left + 3, row.Y + 7));
        }
    }

    [TestMethod]
    public void ToolbarHasDistinctPressedAndDisabledStates()
    {
        Ae2ToolbarAppearance.Face(false, false, false, true).Should().Be(Ae2Image.Button);
        Ae2ToolbarAppearance.Face(false, true, false, true).Should().Be(Ae2Image.ButtonFocus);
        Ae2ToolbarAppearance.Face(true, false, false, true).Should().Be(Ae2Image.ButtonHover);
        Ae2ToolbarAppearance.Face(true, true, true, true).Should().Be(Ae2Image.ButtonPressed);
        Ae2ToolbarAppearance.Face(false, false, true, false).Should().Be(Ae2Image.ButtonDisabled);

        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.ButtonPressed, out var pressed).Should().BeTrue();
        style.TryGet(Ae2Image.ButtonDisabled, out var disabled).Should().BeTrue();
        (pressed.X, pressed.Y, pressed.Width, pressed.Height).Should().Be((181, 65, 18, 19));
        (disabled.X, disabled.Y, disabled.Width, disabled.Height).Should().Be((199, 65, 18, 19));
    }

    [TestMethod]
    [DataRow(false, false, (int)Ae2Image.Button, 145, 20, 0)]
    [DataRow(false, true, (int)Ae2Image.ButtonFocus, 163, 20, 0)]
    [DataRow(true, false, (int)Ae2Image.ButtonHover, 181, 19, 1)]
    [DataRow(true, true, (int)Ae2Image.ButtonHover, 181, 19, 1)]
    public void ToolbarFaceFollowsAe2StateOrderAtOneToOnePixels(bool hovered, bool focused, int image, int x, int height, int offset)
    {
        var style = ReadBundledStyle();
        var face = Ae2ToolbarAppearance.Face(hovered, focused);

        face.Should().Be((Ae2Image)image);
        Ae2ToolbarAppearance.OffsetY(face).Should().Be(offset);
        style.TryGet(face, out var region).Should().BeTrue();
        region.Texture.Should().Be("gui/terminal/terminal_controls.png");
        (region.X, region.Y, region.Width, region.Height).Should().Be((x, 65, 18, height));
        (region.Height + offset).Should().Be(Ae2TerminalLayout.ButtonHeight,
            "the shorter inset face still fills the button down to the same bottom edge");
    }

    [TestMethod]
    public void TooltipUsesTheOriginalSearchFrameWithFixedBevel()
    {
        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.Tooltip, out var hint).Should().BeTrue();
        (hint.Texture, hint.X, hint.Y, hint.Width, hint.Height, hint.Border).Should()
            .Be(("gui/widgets/text_field.png", 0, 0, 128, 12, 3));

        using var png = PngPixels.Read(TextureFilePath(hint.Texture));
        for (var y = 0; y < hint.Height; y++)
            for (var x = 0; x < hint.Width; x++)
                png.Alpha(hint.X + x, hint.Y + y).Should().Be(255, "the grid must not show through the hint frame");
        var fill = png.Pixel(hint.X + hint.Border, hint.Y + hint.Border);
        for (var y = hint.Border; y < hint.Height - hint.Border; y++)
            for (var x = hint.Border; x < hint.Width - hint.Border; x++)
                png.Pixel(hint.X + x, hint.Y + y).Should().Be(fill, "only the flat face may stretch in both directions");
    }

    [TestMethod]
    public void IconCenterUsesVisiblePixelsAndKeepsIntegerScreenCoordinates()
    {
        var visible = Ae2ToolbarAppearance.VisibleBounds(16, 16,
            (x, y) => x is >= 4 and <= 12 && y is >= 3 and <= 13 ? (byte)255 : (byte)0);

        visible.Should().Be((4, 3, 9, 11));
        Ae2ToolbarAppearance.CenterIcon(visible!.Value, 18, 20, 4).Should().Be((2, 6));
        Ae2ToolbarAppearance.CenterIcon(visible.Value, 18, 19, 3).Should().Be((1, 3));
        Ae2ToolbarAppearance.VisibleBounds(16, 16, (_, _) => 0).Should().BeNull();
    }

    [TestMethod]
    public void BoardSectionsMatchTheGridsDrawnInTerminalPng()
    {
        var path = TextureFilePath("gui/terminal/terminal_background.png");
        using var bitmap = PngPixels.Read(path);
        var style = Ae2ScreenStyle.Parse(File.ReadAllText(TerminalAssets.ScreenPath(BundledAssetRoot)));
        style.TryGet(Ae2Image.Bottom, out var bottom).Should().BeTrue();
        // The shipped parts are cut from the 3-row board, which is the Small layout.
        var layout = new Ae2TerminalLayout(1280, 720);
        bottom.Y.Should().Be(layout.StatusTop, "the bottom part starts right below the last storage row");

        // Top-left frame pixels of every slot grid and the scrollbar groove sit where the layout places controls.
        bitmap.IsGridCorner(Ae2TerminalLayout.Inset, Ae2TerminalLayout.Header).Should().BeTrue("storage grid starts at the header edge");
        bitmap.IsGridCorner(Ae2TerminalLayout.Inset, layout.EquipmentTop).Should().BeTrue("equipment grid starts below the status gap");
        bitmap.IsGridCorner(Ae2TerminalLayout.Inset, layout.MainTop).Should().BeTrue("main grid starts below the equipment grid");
        bitmap.IsGridCorner(Ae2TerminalLayout.GrooveLeft, Ae2TerminalLayout.Header).Should().BeTrue("the scrollbar groove starts at its declared column");
        bitmap.Alpha(Ae2TerminalLayout.BoardWidth - 1, layout.BoardHeight - 1).Should().Be(0,
            "the area below the scrollbar is transparent in the PNG and must stay without a background");
    }

    [TestMethod]
    public void MiddleRowRepeatsAndTheScrollbarGrooveRunsThroughEveryRow()
    {
        using var bitmap = PngPixels.Read(TextureFilePath("gui/terminal/terminal_background.png"));
        var style = Ae2ScreenStyle.Parse(File.ReadAllText(TerminalAssets.ScreenPath(BundledAssetRoot)));
        style.TryGet(Ae2Image.Row, out var row).Should().BeTrue();
        // Repeated middle rows extend the groove, so its inside must reach the first and last pixel row of the part.
        foreach (var y in new[] { row.Y, row.Y + row.Height - 1 })
        {
            bitmap.Pixel(Ae2TerminalLayout.GrooveLeft + 1, y).Should().NotBe(bitmap.Pixel(Ae2TerminalLayout.GrooveLeft - 1, y),
                $"the middle row carries the scrollbar groove at y={y}");
            bitmap.Pixel(Ae2TerminalLayout.GrooveLeft + 1, y).Should().Be(bitmap.Pixel(Ae2TerminalLayout.GrooveLeft + 1, row.Y + row.Height / 2));
        }
    }

    [TestMethod]
    [DataRow((int)Ae2Image.VerticalButtonsBg)]
    [DataRow((int)Ae2Image.VerticalButtonsBgBottom)]
    public void VerticalButtonBackgroundMatchesAe2NineSliceBorders(int image)
    {
        ReadBundledStyle().TryGet((Ae2Image)image, out var region).Should().BeTrue();
        using var bitmap = PngPixels.Read(TextureFilePath(region.Texture));
        bitmap.Width.Should().Be(Ae2TerminalLayout.ButtonBarWidth);
        bitmap.Height.Should().BeGreaterThan(Ae2TerminalLayout.ButtonBarTopBorder + Ae2TerminalLayout.ButtonBarBottomBorder);
        // Between AE2's 2-row top and 4-row bottom border, every row is the same, so stretching it keeps the art.
        for (var y = Ae2TerminalLayout.ButtonBarTopBorder + 1; y < bitmap.Height - Ae2TerminalLayout.ButtonBarBottomBorder; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.Pixel(x, y).Should().Be(bitmap.Pixel(x, Ae2TerminalLayout.ButtonBarTopBorder), $"middle row {y} must repeat");
    }

    [TestMethod]
    public void RecoveredButtonBarVariantMatchesItsArchivedPixelTable()
    {
        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.VerticalButtonsBgBottom, out var originalRegion).Should().BeTrue();
        style.TryGet(Ae2Image.VerticalButtonsBgVariant, out var recoveredRegion).Should().BeTrue();
        recoveredRegion.Texture.Should().Be("gui/widgets/vertical_button_bar_variant.png");
        (recoveredRegion.X, recoveredRegion.Y, recoveredRegion.Width, recoveredRegion.Height)
            .Should().Be((0, 0, 21, 26));

        using var original = PngPixels.Read(TextureFilePath(originalRegion.Texture));
        using var recovered = PngPixels.Read(TextureFilePath(recoveredRegion.Texture));
        (recovered.Width, recovered.Height).Should().Be((21, 26));
        var expectedChanges = new Dictionary<(int X, int Y), uint>
        {
            [(20, 23)] = 0xFFF2F2F2,
            [(20, 24)] = 0xFFF2F2F2,
            [(20, 25)] = 0xFFF2F2F2
        };
        var actualChanges = new HashSet<(int X, int Y)>();
        for (var y = 0; y < recovered.Height; y++)
        for (var x = 0; x < recovered.Width; x++)
        {
            var pixel = recovered.Pixel(x, y);
            if (pixel == original.Pixel(x, y)) continue;
            actualChanges.Add((x, y));
            expectedChanges.TryGetValue((x, y), out var expectedPixel);
            pixel.Should().Be(expectedPixel, $"pixel ({x}, {y}) must follow the archived variant table");
        }
        actualChanges.Should().BeEquivalentTo(expectedChanges.Keys,
            "the archived table differs from the bottom-joining sprite at exactly three right-edge pixels");
    }

    [TestMethod]
    [DataRow(4, false, false)]
    [DataRow(5, false, false)]
    [DataRow(0, true, false)]
    [DataRow(0, false, true)]
    [DataRow(0, true, true)]
    public void ButtonBarSlicesMeetTheBoardWithoutBreakingItsBevel(int toolbarButtons, bool drain, bool use)
    {
        using var bar = PngPixels.Read(TextureFilePath("gui/widgets/vertical_button_bar.png"));
        using var board = PngPixels.Read(TextureFilePath("gui/terminal/terminal_background.png"));
        var flushWithBoardTop = toolbarButtons > 0;
        var layout = new Ae2TerminalLayout(320, 240);
        var actions = (drain ? TerminalDragAction.Drain : TerminalDragAction.None) |
            (use ? TerminalDragAction.Use : TerminalDragAction.None);
        var boardY = flushWithBoardTop ? Ae2TerminalLayout.ButtonBarY : layout.DragActionBackgroundBounds(actions).Y;
        var height = flushWithBoardTop ? Ae2TerminalLayout.ButtonBarHeight(toolbarButtons) : layout.DragActionBackgroundBounds(actions).Height;
        var end = 0;
        for (var i = 0; i < Ae2TerminalLayout.ButtonBarPatchCount; i++)
        {
            var (sourceY, sourceWidth, sourceRows) = Ae2TerminalLayout.ButtonBarPatch(bar.Height, i, flushWithBoardTop);
            var (y, width, rows) = Ae2TerminalLayout.ButtonBarPatch(height, i, flushWithBoardTop);
            y.Should().Be(end, "each slice meets the preceding slice exactly");
            sourceWidth.Should().Be(width);
            (sourceY + sourceRows).Should().BeLessThanOrEqualTo(bar.Height);
            if (i != 2) rows.Should().Be(sourceRows, "only the uniform middle can stretch");
            end += rows;
        }
        end.Should().Be(height);

        var (_, topWidth, _) = Ae2TerminalLayout.ButtonBarPatch(height, 0, flushWithBoardTop);
        var firstBoardColumn = Ae2TerminalLayout.Rail - Ae2TerminalLayout.ButtonBarX;
        var bevel = firstBoardColumn + 1;
        uint ComposedPixel(int x) => x < topWidth ? bar.Pixel(x, 0) : board.Pixel(x - firstBoardColumn, boardY);
        ComposedPixel(firstBoardColumn).Should().Be(bar.Pixel(0, 0), "the outer outline connects to the board");
        if (flushWithBoardTop) ComposedPixel(bevel).Should().Be(board.Pixel(1, 0), "the toolbar extends the board's top outline");
        else ComposedPixel(bevel).Should().Be(board.Pixel(1, boardY), "the lower action bar leaves the board's light bevel visible");
    }

    [TestMethod]
    [DataRow((int)TerminalHeight.Small, (int)(TerminalDragAction.Drain | TerminalDragAction.Use | TerminalDragAction.Pin), (int)TerminalDragAction.None, (int)Ae2Image.VerticalButtonsBgBottom)]
    [DataRow((int)TerminalHeight.Small, (int)(TerminalDragAction.Drain | TerminalDragAction.Pin), (int)TerminalDragAction.None, (int)Ae2Image.VerticalButtonsBgBottom)]
    [DataRow((int)TerminalHeight.Small, (int)(TerminalDragAction.Use | TerminalDragAction.Pin), (int)TerminalDragAction.Use, (int)Ae2Image.VerticalButtonsBg)]
    [DataRow((int)TerminalHeight.Small, (int)TerminalDragAction.Use, (int)TerminalDragAction.Use, (int)Ae2Image.VerticalButtonsBgBottom)]
    [DataRow((int)TerminalHeight.Medium, (int)(TerminalDragAction.Drain | TerminalDragAction.Use | TerminalDragAction.Pin), (int)TerminalDragAction.None, (int)Ae2Image.VerticalButtonsBg)]
    public void DragButtonBarUsesTheHandDrawnEndCapOnlyWhenFlushWithTheBoard(int style, int actions, int armed, int expectedArt)
    {
        var layout = new Ae2TerminalLayout(1920, 1080, (TerminalHeight)style);
        var (_, top, _, height) = layout.DragActionBackgroundBounds((TerminalDragAction)actions, (TerminalDragAction)armed);
        var art = Ae2ButtonBarAppearance.DragBackground(layout, top, height);
        art.Should().Be((Ae2Image)expectedArt);
        if (art != Ae2Image.VerticalButtonsBgBottom) return;

        ReadBundledStyle().TryGet(art, out var region).Should().BeTrue();
        using var bar = PngPixels.Read(TextureFilePath(region.Texture));
        using var board = PngPixels.Read(TextureFilePath("gui/terminal/terminal_background.png"));
        // These patches are never stretched. Compare the final column of the rail to the adjacent board pixels.
        var (_, topWidth, _) = Ae2TerminalLayout.ButtonBarPatch(region.Height, 0, flushWithBoardTop: false);
        bar.Pixel(region.X + topWidth - 1, region.Y).Should().Be(board.Pixel(0, top / layout.Scale));
        var patch = Ae2TerminalLayout.ButtonBarPatchCount - 1;
        var (sourceY, width, rows) = Ae2TerminalLayout.ButtonBarPatch(region.Height, patch, flushWithBoardTop: false);
        var (destY, _, _) = Ae2TerminalLayout.ButtonBarPatch(height / layout.Scale, patch, flushWithBoardTop: false);
        for (var row = 0; row < rows; row++)
            bar.Pixel(region.X + width - 1, region.Y + sourceY + row)
                .Should().Be(board.Pixel(Ae2TerminalLayout.ButtonBarJoinWidth, top / layout.Scale + destY + row),
                    $"the bottom bevel and outline must be continuous at row {row}");
    }

    [TestMethod]
    public void EmptyRootObjectSkipsEveryRegionAndReportsWarnings()
    {
        var style = Ae2ScreenStyle.Parse("{}");

        style.Warnings.Should().HaveCount(Enum.GetValues(typeof(Ae2Image)).Length);
        foreach (Ae2Image image in Enum.GetValues(typeof(Ae2Image)))
            style.TryGet(image, out _).Should().BeFalse();
    }

    [TestMethod]
    public void ParsesValidRegionAndDefaultsOptionalBorderToZero()
    {
        var style = Ae2ScreenStyle.Parse("""
            { "images": { "button": { "texture": "unknowninventoryterminal:textures/gui/widgets/buttons.png", "srcRect": [1, 2, 18, 18] } } }
            """);

        style.TryGet(Ae2Image.Button, out var region).Should().BeTrue();
        region.Texture.Should().Be("gui/widgets/buttons.png");
        region.X.Should().Be(1);
        region.Y.Should().Be(2);
        region.Width.Should().Be(18);
        region.Height.Should().Be(18);
        region.Border.Should().Be(0);
    }

    [TestMethod]
    public void ParsesOptionalBorderAndChecksRectangleBounds()
    {
        var style = Ae2ScreenStyle.Parse("""
            { "images": { "tooltip": { "texture": "unknowninventoryterminal:textures/gui/sprites/tooltip.png", "srcRect": [0, 0, 16, 16], "border": 6 } } }
            """);

        style.TryGet(Ae2Image.Tooltip, out var region).Should().BeTrue();
        region.Border.Should().Be(6);
        region.Fits(16, 16).Should().BeTrue();
        region.Fits(15, 16).Should().BeFalse();
        region.Fits(16, 15).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("5")]
    [DataRow("1.5")]
    [DataRow("\"6\"")]
    public void SkipsRegionsWithMalformedBorders(string border)
    {
        var style = Ae2ScreenStyle.Parse("{\"images\":{\"button\":{\"texture\":\"unknowninventoryterminal:textures/gui/widgets/buttons.png\",\"srcRect\":[0,0,10,10],\"border\":" + border + "}}}");

        style.TryGet(Ae2Image.Button, out _).Should().BeFalse();
        style.Warnings.Any(warning => warning.Contains("images.button", StringComparison.Ordinal)).Should().BeTrue();
    }

    [TestMethod]
    public void RejectsDuplicatePropertiesInTheJsonDocument()
    {
        var act = () => Ae2ScreenStyle.Parse("{\"images\":{\"button\":{\"texture\":\"unknowninventoryterminal:textures/gui/widgets/buttons.png\",\"texture\":\"unknowninventoryterminal:textures/gui/widgets/other.png\",\"srcRect\":[0,0,18,18]}}}");

        act.Should().Throw<JsonReaderException>();
    }

    [TestMethod]
    [DataRow("guis/buttons.png")]
    [DataRow("sprites/buttons.png")]
    [DataRow("textures/gui/widgets/buttons.png")]
    [DataRow("ae2:textures/gui/widgets/buttons.png")]
    [DataRow("unknowninventoryterminal:gui/widgets/buttons.png")]
    [DataRow("UnknownInventoryTerminal:textures/gui/widgets/buttons.png")]
    [DataRow("unknowninventoryterminal:textures/gui/../secret.png")]
    [DataRow("unknowninventoryterminal:textures/gui/widgets/buttons.jpg")]
    [DataRow("unknowninventoryterminal:textures/GUI/widgets/buttons.png")]
    [DataRow("unknowninventoryterminal:textures/gui//widgets/buttons.png")]
    [DataRow("unknowninventoryterminal:textures/gui/widgets/../buttons.png")]
    [DataRow("unknowninventoryterminal:textures/gui/widgets/sub\\buttons.png")]
    [DataRow("unknowninventoryterminal:textures/buttons.png")]
    [DataRow("unknowninventoryterminal:textures/creativecore/textures/gui/other.png")]
    public void SkipsRegionsWithDisallowedTexturePaths(string texture)
    {
        var json = "{\"images\":{\"button\":{\"texture\":" + JsonConvert.ToString(texture) + ",\"srcRect\":[0,0,18,18]}}}";
        var style = Ae2ScreenStyle.Parse(json);

        style.TryGet(Ae2Image.Button, out _).Should().BeFalse();
        style.Warnings.Any(warning => warning.Contains("images.button", StringComparison.Ordinal)).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("[0, 0, 0, 18]")]
    [DataRow("[-1, 0, 18, 18]")]
    [DataRow("[2147483648, 0, 18, 18]")]
    public void SkipsMalformedSourceRectangles(string sourceRect)
    {
        var style = Ae2ScreenStyle.Parse("{\"images\":{\"button\":{\"texture\":\"unknowninventoryterminal:textures/gui/widgets/buttons.png\",\"srcRect\":" + sourceRect + "}}}");

        style.TryGet(Ae2Image.Button, out _).Should().BeFalse();
        style.Warnings.Any(warning => warning.Contains("images.button", StringComparison.Ordinal)).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("{ ")]
    [DataRow("[]")]
    [DataRow("null")]
    public void RejectsInvalidOrNonObjectJsonRoot(string json)
    {
        var act = () => Ae2ScreenStyle.Parse(json);

        act.Should().Throw<JsonReaderException>();
    }

    private static Ae2ScreenStyle ReadBundledStyle() => Ae2ScreenStyle.Parse(File.ReadAllText(
        TerminalAssets.ScreenPath(BundledAssetRoot)));

    private static string TextureFilePath(string logicalTexture) => TerminalAssets.TexturePath(BundledAssetRoot, logicalTexture);

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        var signature = reader.ReadBytes(8);
        signature.Should().Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, "the texture must be a PNG");
        ReadBigEndianInt32(reader).Should().Be(13, "PNG IHDR is the first 13-byte chunk");
        reader.ReadBytes(4).Should().Equal(Encoding.ASCII.GetBytes("IHDR"));

        var width = ReadBigEndianInt32(reader);
        var height = ReadBigEndianInt32(reader);
        width.Should().BeGreaterThan(0);
        height.Should().BeGreaterThan(0);
        return (width, height);
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        bytes.Should().HaveCount(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    // Minimal non-interlaced PNG reader (4/8-bit palette or 8-bit RGBA) for checking the shipped board art.
    private sealed class PngPixels : IDisposable
    {
        private readonly int _width;
        private readonly byte[][] _rgba;
        private PngPixels(int width, byte[][] rgba) { _width = width; _rgba = rgba; }
        public void Dispose() { }

        internal static PngPixels Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            int width = 0, height = 0, colorType = 0, bitDepth = 0;
            byte[] palette = Array.Empty<byte>(), alpha = Array.Empty<byte>();
            using var idat = new MemoryStream();
            for (var offset = 8; offset < bytes.Length;)
            {
                var length = (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
                var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                var data = bytes.AsSpan(offset + 8, length);
                if (type == "IHDR")
                {
                    width = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
                    height = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
                    bitDepth = data[8];
                    colorType = data[9];
                    data[12].Should().Be(0, "the test reader does not support interlacing");
                }
                else if (type == "PLTE") palette = data.ToArray();
                else if (type == "tRNS") alpha = data.ToArray();
                else if (type == "IDAT") idat.Write(data);
                offset += 12 + length;
            }
            (colorType, bitDepth).Should().BeOneOf(new[] { (3, 4), (3, 8), (6, 8) });
            var filterBytesPerPixel = colorType == 3 ? 1 : 4;
            idat.Position = 0;
            using var inflater = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
            using var raw = new MemoryStream();
            inflater.CopyTo(raw);
            var source = raw.ToArray();
            var stride = (width * filterBytesPerPixel * bitDepth + 7) / 8;
            var previous = new byte[stride];
            var rows = new byte[height][];
            for (var y = 0; y < height; y++)
            {
                var filter = source[y * (stride + 1)];
                var line = source.AsSpan(y * (stride + 1) + 1, stride).ToArray();
                for (var i = 0; i < stride; i++)
                {
                    int left = i >= filterBytesPerPixel ? line[i - filterBytesPerPixel] : 0, up = previous[i];
                    var corner = i >= filterBytesPerPixel ? previous[i - filterBytesPerPixel] : 0;
                    line[i] = (byte)(line[i] + filter switch
                    {
                        1 => left,
                        2 => up,
                        3 => (left + up) / 2,
                        4 => Paeth(left, up, corner),
                        _ => 0
                    });
                }
                previous = line;
                var rgba = new byte[width * 4];
                for (var x = 0; x < width; x++)
                {
                    if (colorType == 6) Array.Copy(line, x * 4, rgba, x * 4, 4);
                    else
                    {
                        var index = bitDepth == 4 ? (line[x / 2] >> (x % 2 == 0 ? 4 : 0)) & 0xF : line[x];
                        Array.Copy(palette, index * 3, rgba, x * 4, 3);
                        rgba[x * 4 + 3] = index < alpha.Length ? alpha[index] : (byte)255;
                    }
                }
                rows[y] = rgba;
            }
            return new PngPixels(width, rows);
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        internal byte Alpha(int x, int y) => _rgba[y][x * 4 + 3];
        internal int Width => _width;
        internal int Height => _rgba.Length;
        internal uint Pixel(int x, int y) => BitConverter.ToUInt32(_rgba[y], x * 4);

        private bool IsFrame(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _rgba.Length &&
            Alpha(x, y) == 255 && _rgba[y][x * 4] >= 0xF0 && _rgba[y][x * 4 + 1] >= 0xF0 && _rgba[y][x * 4 + 2] >= 0xF0;

        // A grid's white frame starts at (x, y) when neither its left nor upper neighbour is frame.
        internal bool IsGridCorner(int x, int y) => IsFrame(x, y) && !IsFrame(x - 1, y) && !IsFrame(x, y - 1);
    }
}
