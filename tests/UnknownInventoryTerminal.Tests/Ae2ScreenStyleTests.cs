using System;
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
    [TestMethod]
    public void MigratedTerminalStyleDefinesEveryImageAndFitsItsPng()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AE2", "screens", "terminals", "terminal.json");
        File.Exists(jsonPath).Should().BeTrue($"the migrated terminal screen definition must be copied to test output: {jsonPath}");

        var style = Ae2ScreenStyle.Parse(File.ReadAllText(jsonPath));
        style.Warnings.Should().BeEmpty("the shipped screen definition must declare all supported images");

        foreach (Ae2Image image in Enum.GetValues(typeof(Ae2Image)))
        {
            style.TryGet(image, out var region).Should().BeTrue($"{image} must have a region");
            region.Border.Should().Be(image switch
            {
                Ae2Image.SettingsBackground => 4,
                Ae2Image.Tooltip => 3,
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
    [DataRow((int)Ae2Image.SettingsDropDownClosedHover, 0, 96, 18)]
    [DataRow((int)Ae2Image.SettingsDropDownClosedPressed, 0, 64, 19)]
    [DataRow((int)Ae2Image.SettingsDropDownOpen, 112, 64, 19)]
    [DataRow((int)Ae2Image.SettingsDropDownOpenDisabled, 112, 32, 18)]
    [DataRow((int)Ae2Image.SettingsDropDownOpenHover, 112, 96, 18)]
    [DataRow((int)Ae2Image.SettingsOption, 0, 128, 19)]
    [DataRow((int)Ae2Image.SettingsOptionDisabled, 0, 160, 17)]
    [DataRow((int)Ae2Image.SettingsOptionHover, 112, 192, 17)]
    [DataRow((int)Ae2Image.SettingsOptionSelected, 0, 192, 18)]
    public void SettingsDropdownKeepsAllSourceEdges(int image, int x, int y, int height)
    {
        var style = ReadBundledStyle();
        style.TryGet((Ae2Image)image, out var region).Should().BeTrue();
        region.Texture.Should().Be("gui/sprites/drop_down_list.png");
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
    public void SettingsStatesGiveDisabledAndHoverDistinctSprites()
    {
        Ae2DropdownAppearance.Field(false, false, true, true).Should().Be(Ae2Image.SettingsDropDownClosedDisabled);
        Ae2DropdownAppearance.Field(false, true, true, false).Should().Be(Ae2Image.SettingsDropDownClosedHover);
        Ae2DropdownAppearance.Field(true, true, false, false).Should().Be(Ae2Image.SettingsDropDownOpen);
        Ae2DropdownAppearance.Field(true, true, true, false).Should().Be(Ae2Image.SettingsDropDownOpenHover);
        Ae2DropdownAppearance.Option(false, true, true).Should().Be(Ae2Image.SettingsOptionDisabled);
        Ae2DropdownAppearance.Option(true, false, true).Should().Be(Ae2Image.SettingsOptionSelected);
        Ae2DropdownAppearance.Option(true, true, true).Should().Be(Ae2Image.SettingsOptionHover);
        Ae2DropdownAppearance.Option(true, false, false).Should().Be(Ae2Image.SettingsOption);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    public void KeepDraggingSliderAndCaptionStayInsideTheirRow(int scale)
    {
        var control = Ae2SettingsLayout.ToggleControlBounds(Ae2SettingsLayout.FieldWidth, Ae2SettingsLayout.KeepDraggingHeight, scale);
        var caption = Ae2SettingsLayout.ToggleCaptionBounds(Ae2SettingsLayout.FieldWidth, Ae2SettingsLayout.KeepDraggingHeight, scale);

        control.Should().Be(((Ae2SettingsLayout.FieldWidth - Ae2SettingsLayout.ToggleWidth - 2) * scale, 0,
            Ae2SettingsLayout.ToggleWidth * scale, Ae2SettingsLayout.ToggleHeight * scale));
        caption.X.Should().Be(4 * scale);
        (caption.X + caption.Width).Should().BeLessThan(control.X, "the label must not collide with the switch");
        (control.X + control.Width).Should().BeLessThanOrEqualTo(Ae2SettingsLayout.FieldWidth * scale);
        caption.Height.Should().Be(Ae2SettingsLayout.KeepDraggingHeight * scale);
    }

    [TestMethod]
    public void KeepDraggingSliderKnobMovesBetweenBothEnds()
    {
        Ae2ToggleAppearance.KnobX(value: false).Should().Be(Ae2SettingsLayout.ToggleKnobInset);
        Ae2ToggleAppearance.KnobX(value: true).Should().Be(
            Ae2SettingsLayout.ToggleWidth - Ae2SettingsLayout.ToggleKnobInset - Ae2SettingsLayout.ToggleKnobWidth);
        Ae2ToggleAppearance.TrackColor(value: false, hovered: false).Should().NotBe(
            Ae2ToggleAppearance.TrackColor(value: true, hovered: false));
        Ae2ToggleAppearance.TrackColor(value: false, hovered: true).Should().NotBe(
            Ae2ToggleAppearance.TrackColor(value: false, hovered: false));
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
        region.Texture.Should().Be("guis/background.png");
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
    [DataRow((int)Ae2Image.Thumb, "gui/sprites/big_scroller.png")]
    [DataRow((int)Ae2Image.ThumbDisabled, "gui/sprites/big_scroller_disabled.png")]
    public void ScrollThumbUsesTheWholeNativeAe2Sprite(int image, string texture)
    {
        var style = ReadBundledStyle();

        style.TryGet((Ae2Image)image, out var region).Should().BeTrue();
        region.Texture.Should().Be(texture);
        (region.X, region.Y, region.Width, region.Height).Should().Be((0, 0, 12, 15));
        ReadPngDimensions(TextureFilePath(region.Texture)).Should().Be((region.Width, region.Height));
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
        region.Texture.Should().Be("guis/states.png");
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
        using var png = PngPixels.Read(TextureFilePath("guis/states.png"));
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
            .Be(("guis/terminal.png", 0, 204, Ae2TerminalLayout.GridWidth, Ae2TerminalLayout.Pitch));
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
        region.Texture.Should().Be("guis/states.png");
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
            .Be(("guis/text_field.png", 0, 0, 128, 12, 3));

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
        var path = TextureFilePath("guis/terminal.png");
        using var bitmap = PngPixels.Read(path);
        var style = Ae2ScreenStyle.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "AE2", "screens", "terminals", "terminal.json")));
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
        using var bitmap = PngPixels.Read(TextureFilePath("guis/terminal.png"));
        var style = Ae2ScreenStyle.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "AE2", "screens", "terminals", "terminal.json")));
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
    [DataRow(4, false, false)]
    [DataRow(5, false, false)]
    [DataRow(0, true, false)]
    [DataRow(0, false, true)]
    [DataRow(0, true, true)]
    public void ButtonBarSlicesMeetTheBoardWithoutBreakingItsBevel(int toolbarButtons, bool drain, bool use)
    {
        using var bar = PngPixels.Read(TextureFilePath("gui/sprites/vertical_buttons_bg.png"));
        using var board = PngPixels.Read(TextureFilePath("guis/terminal.png"));
        var flushWithBoardTop = toolbarButtons > 0;
        var layout = new Ae2TerminalLayout(320, 240);
        var boardY = flushWithBoardTop ? Ae2TerminalLayout.ButtonBarY : layout.DragActionBackgroundBounds(drain, use).Y;
        var height = flushWithBoardTop ? Ae2TerminalLayout.ButtonBarHeight(toolbarButtons) : layout.DragActionBackgroundBounds(drain, use).Height;
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
    [DataRow((int)TerminalHeight.Small, true, true, true, (int)Ae2Image.VerticalButtonsBgBottom)]
    [DataRow((int)TerminalHeight.Small, false, false, true, (int)Ae2Image.VerticalButtonsBgBottom)]
    [DataRow((int)TerminalHeight.Small, true, true, false, (int)Ae2Image.VerticalButtonsBg)]
    [DataRow((int)TerminalHeight.Medium, true, true, true, (int)Ae2Image.VerticalButtonsBg)]
    public void DragButtonBarUsesTheHandDrawnEndCapOnlyWhenFlushWithTheBoard(int style, bool drain, bool use, bool pin, int expectedArt)
    {
        var layout = new Ae2TerminalLayout(1920, 1080, (TerminalHeight)style);
        var (_, top, _, height) = layout.DragActionBackgroundBounds(drain, use, pin);
        var art = Ae2ButtonBarAppearance.DragBackground(layout, top, height);
        art.Should().Be((Ae2Image)expectedArt);
        if (art != Ae2Image.VerticalButtonsBgBottom) return;

        ReadBundledStyle().TryGet(art, out var region).Should().BeTrue();
        using var bar = PngPixels.Read(TextureFilePath(region.Texture));
        using var board = PngPixels.Read(TextureFilePath("guis/terminal.png"));
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
            { "images": { "button": { "texture": "guis/buttons.png", "srcRect": [1, 2, 18, 18] } } }
            """);

        style.TryGet(Ae2Image.Button, out var region).Should().BeTrue();
        region.Texture.Should().Be("guis/buttons.png");
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
            { "images": { "tooltip": { "texture": "gui/sprites/tooltip.png", "srcRect": [0, 0, 16, 16], "border": 6 } } }
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
        var style = Ae2ScreenStyle.Parse("{\"images\":{\"button\":{\"texture\":\"guis/buttons.png\",\"srcRect\":[0,0,10,10],\"border\":" + border + "}}}");

        style.TryGet(Ae2Image.Button, out _).Should().BeFalse();
        style.Warnings.Any(warning => warning.Contains("images.button", StringComparison.Ordinal)).Should().BeTrue();
    }

    [TestMethod]
    public void RejectsDuplicatePropertiesInTheJsonDocument()
    {
        var act = () => Ae2ScreenStyle.Parse("{\"images\":{\"button\":{\"texture\":\"guis/buttons.png\",\"texture\":\"guis/other.png\",\"srcRect\":[0,0,18,18]}}}");

        act.Should().Throw<JsonReaderException>();
    }

    [TestMethod]
    [DataRow("../secret.png")]
    [DataRow("guis/../secret.png")]
    [DataRow("guis/buttons.jpg")]
    [DataRow("textures/guis/buttons.png")]
    [DataRow("Guis/buttons.png")]
    [DataRow("guis//buttons.png")]
    [DataRow("gui/sprites/../buttons.png")]
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
        var style = Ae2ScreenStyle.Parse("{\"images\":{\"button\":{\"texture\":\"guis/buttons.png\",\"srcRect\":" + sourceRect + "}}}");

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
        Path.Combine(AppContext.BaseDirectory, "Assets", "AE2", "screens", "terminals", "terminal.json")));

    private static string TextureFilePath(string logicalTexture)
    {
        var relative = logicalTexture.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(AppContext.BaseDirectory, "Assets", "AE2", "textures", relative);
    }

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
