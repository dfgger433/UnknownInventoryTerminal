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
            if (image is Ae2Image.MainSlot or Ae2Image.EquipmentSlot)
                region.Width.Should().Be(Ae2TerminalLayout.GridWidth, $"{image} is one six-column range, not a single cell");

            var texturePath = TextureFilePath(region.Texture);
            File.Exists(texturePath).Should().BeTrue($"{image} references a migrated PNG at {texturePath}");
            var (width, height) = ReadPngDimensions(texturePath);
            region.Fits(width, height).Should().BeTrue(
                $"{image} source rectangle [{region.X}, {region.Y}, {region.Width}, {region.Height}] must fit {region.Texture} ({width}x{height})");
        }
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
}
