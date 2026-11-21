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
            region.Border.Should().Be(image == Ae2Image.SettingsBackground ? 4 : 0);
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
    public void SettingsBackgroundKeepsBothEdgesAtEveryTerminalScale()
    {
        var style = ReadBundledStyle();
        style.TryGet(Ae2Image.SettingsBackground, out var region).Should().BeTrue();
        region.Texture.Should().Be("guis/background.png");
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
    public void VerticalButtonBackgroundMatchesAe2NineSliceBorders()
    {
        using var bitmap = PngPixels.Read(TextureFilePath("gui/sprites/vertical_buttons_bg.png"));
        bitmap.Width.Should().Be(Ae2TerminalLayout.ButtonBarWidth);
        bitmap.Height.Should().BeGreaterThan(Ae2TerminalLayout.ButtonBarTopBorder + Ae2TerminalLayout.ButtonBarBottomBorder);
        // Between AE2's 2-row top and 4-row bottom border, every row is the same, so stretching it keeps the art.
        for (var y = Ae2TerminalLayout.ButtonBarTopBorder + 1; y < bitmap.Height - Ae2TerminalLayout.ButtonBarBottomBorder; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.Pixel(x, y).Should().Be(bitmap.Pixel(x, Ae2TerminalLayout.ButtonBarTopBorder), $"middle row {y} must repeat");
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

    // Minimal non-interlaced 8-bit PNG reader (palette or RGBA) for checking the shipped board art.
    private sealed class PngPixels : IDisposable
    {
        private readonly int _width;
        private readonly byte[][] _rgba;
        private PngPixels(int width, byte[][] rgba) { _width = width; _rgba = rgba; }
        public void Dispose() { }

        internal static PngPixels Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            int width = 0, height = 0, colorType = 0;
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
                    data[8].Should().Be(8, "the test reader supports 8-bit channels");
                    colorType = data[9];
                    data[12].Should().Be(0, "the test reader does not support interlacing");
                }
                else if (type == "PLTE") palette = data.ToArray();
                else if (type == "tRNS") alpha = data.ToArray();
                else if (type == "IDAT") idat.Write(data);
                offset += 12 + length;
            }
            colorType.Should().BeOneOf(new[] { 3, 6 }, "terminal.png is a palette or RGBA image");
            var channels = colorType == 3 ? 1 : 4;
            idat.Position = 0;
            using var inflater = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
            using var raw = new MemoryStream();
            inflater.CopyTo(raw);
            var source = raw.ToArray();
            var stride = width * channels;
            var previous = new byte[stride];
            var rows = new byte[height][];
            for (var y = 0; y < height; y++)
            {
                var filter = source[y * (stride + 1)];
                var line = source.AsSpan(y * (stride + 1) + 1, stride).ToArray();
                for (var i = 0; i < stride; i++)
                {
                    int left = i >= channels ? line[i - channels] : 0, up = previous[i], corner = i >= channels ? previous[i - channels] : 0;
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
                    if (channels == 4) Array.Copy(line, x * 4, rgba, x * 4, 4);
                    else
                    {
                        var index = line[x];
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
