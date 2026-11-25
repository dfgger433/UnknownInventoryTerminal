// SPDX-License-Identifier: GPL-3.0-only
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UnknownInventoryTerminal;

internal static class DropdownPreview
{
    private const int Scale = 4;
    private const int ImageWidth = 544, ImageHeight = 184;
    private const int FieldWidth = Ae2SettingsLayout.FieldWidth * Scale;
    private const int FieldHeight = Ae2SettingsLayout.FieldHeight * Scale;
    private const int FieldLeft = (ImageWidth - FieldWidth) / 2, FieldTop = 54;
    private const uint Background = 0xFF202630;
    private static readonly Typeface Font = new("Segoe UI");

    internal static void Check(Ae2ScreenStyle style, string assetRoot)
    {
        var frames = new[]
        {
            Render(style, assetRoot, "Normal", hovered: false, pressed: false),
            Render(style, assetRoot, "Hover", hovered: true, pressed: false),
            Render(style, assetRoot, "Pressed", hovered: true, pressed: true)
        };

        for (var i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            // The top moves, but the three different-height faces all meet the fixed bottom edge.
            if (frame.Face.Top != FieldTop + i * Scale || frame.Face.Bottom != FieldTop + FieldHeight - 1)
                throw new InvalidOperationException($"{frame.Name}: face Y={RelativeY(frame.Face)} does not match the fixed hit box.");
            if (i > 0)
            {
                var previous = frames[i - 1];
                CheckStep(frame.Name, "caption", previous.Caption, frame.Caption);
                CheckStep(frame.Name, "arrow", previous.Arrow, frame.Arrow);
            }
            Console.WriteLine($"Dropdown {frame.Name} ({Scale}x, relative Y): face {RelativeY(frame.Face)}, " +
                $"caption {RelativeY(frame.Caption)}, arrow {RelativeY(frame.Arrow)}.");
        }

        if (Environment.GetEnvironmentVariable("UIT_DROPDOWN_PREVIEW_DIR") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        foreach (var frame in frames)
        {
            var path = Path.Combine(directory, $"dropdown-{frame.Name.ToLowerInvariant()}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frame.Image));
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
    }

    private static PreviewFrame Render(Ae2ScreenStyle style, string assetRoot, string name, bool hovered, bool pressed)
    {
        const bool open = false;
        var (face, ink, fill) = Ae2DropdownAppearance.Field(open, enabled: true, hovered: hovered, pressed: pressed);
        if (!style.TryGet(face, out var region))
            throw new InvalidOperationException($"{face} is missing from terminal.json.");
        var (top, height, caption) = Ae2DropdownAppearance.FieldLayout(region.Height, Scale);
        var crop = RecolorCrop(assetRoot, region, open, ink, name);

        var artwork = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(artwork, BitmapScalingMode.NearestNeighbor);
        using (var drawing = artwork.Open())
        {
            drawing.DrawRectangle(Brush(Background), null, new Rect(0, 0, ImageWidth, ImageHeight));
            drawing.DrawText(Text($"Language / {name}", 18, Brushes.White), new Point(FieldLeft, 17));
            drawing.DrawText(Text("4x atlas  |  Fixed hit-area guides", 14, Brush(0xFFAAB6C8)), new Point(FieldLeft, 149));
            // Mark the original hit box at the same coordinates on every image, outside the sprite.
            var guide = Brush(0xFF8090A8);
            foreach (var y in new[] { FieldTop, FieldTop + FieldHeight })
            {
                drawing.DrawRectangle(guide, null, new Rect(FieldLeft - 22, y, 14, 1));
                drawing.DrawRectangle(guide, null, new Rect(FieldLeft + FieldWidth + 8, y, 14, 1));
            }
            drawing.DrawImage(crop, new Rect(FieldLeft, FieldTop + top, FieldWidth, height));
        }

        var visual = new DrawingVisual();
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var drawing = visual.RenderOpen()) drawing.DrawDrawing(artwork);
        var withoutCaption = PixelReader(Rasterize(visual));
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawDrawing(artwork);
            var bounds = new Rect(FieldLeft + caption.X, FieldTop + caption.Y, caption.Width, caption.Height);
            var text = Text("English", 8 * Scale, Brush(ink));
            drawing.PushClip(new RectangleGeometry(bounds));
            // Game text is left-aligned inside the caption and centered vertically.
            drawing.DrawText(text, new Point(bounds.Left, bounds.Top + (bounds.Height - text.Height) / 2));
            drawing.Pop();
        }
        var image = Rasterize(visual);
        var rendered = PixelReader(image);

        // Sample only actual WPF output, not the layout or the arrow callback. The center
        // column never crosses the caption. Caption diffs must be over the flat face, never the bevel.
        var faceRows = Scan(new Int32Rect(FieldLeft + FieldWidth / 2, FieldTop, 1, FieldHeight),
            (x, y) => rendered(x, y) != Background, $"{name} face");
        var captionRows = Scan(new Int32Rect(FieldLeft + 3 * Scale, FieldTop, 87 * Scale, FieldHeight),
            (x, y) => withoutCaption(x, y) == fill && rendered(x, y) != fill, $"{name} caption");
        var arrowRows = Scan(new Int32Rect(FieldLeft + 90 * Scale, FieldTop + 3 * Scale, 18 * Scale, 12 * Scale),
            (x, y) => rendered(x, y) == ink, $"{name} arrow");
        if (arrowRows.Count != 9 * Scale * Scale)
            throw new InvalidOperationException($"{name}: expected nine scaled texels of arrow ink, found {arrowRows.Count} pixels.");

        return new PreviewFrame(name, image, faceRows, captionRows, arrowRows);
    }

    private static BitmapSource RecolorCrop(string assetRoot, Ae2ImageRegion region, bool open, uint ink, string name)
    {
        using var stream = File.OpenRead(TerminalAssets.TexturePath(assetRoot, region.Texture));
        var atlas = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad).Frames[0];
        var crop = new CroppedBitmap(atlas, new Int32Rect(region.X, region.Y, region.Width, region.Height));
        var bgra = new FormatConvertedBitmap(crop, PixelFormats.Bgra32, null, 0);
        var (pixels, stride) = CopyPixels(bgra);
        int Offset(int x, int y) => y * stride + x * 4;
        // BGRA bytes read as a little-endian uint are packed ARGB. The crop and callback both use a top-left origin.
        if (!Ae2DropdownAppearance.TryPaintChevron(region.Width, region.Height, open,
                (x, y) => BinaryPrimitives.ReadUInt32LittleEndian(pixels.AsSpan(Offset(x, y))),
                (x, y) => BinaryPrimitives.WriteUInt32LittleEndian(pixels.AsSpan(Offset(x, y)), ink)))
            throw new InvalidOperationException($"{name}: the shipped {region.Texture} crop has no recognized chevron.");
        return BitmapSource.Create(region.Width, region.Height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    }

    private static RenderTargetBitmap Rasterize(Visual visual)
    {
        var bitmap = new RenderTargetBitmap(ImageWidth, ImageHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static (byte[] Pixels, int Stride) CopyPixels(BitmapSource source)
    {
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return (pixels, stride);
    }

    private static Func<int, int, uint> PixelReader(RenderTargetBitmap bitmap)
    {
        // The target background is opaque, so its premultiplied BGRA has the same bytes as straight BGRA.
        var (pixels, stride) = CopyPixels(bitmap);
        return (x, y) => BinaryPrimitives.ReadUInt32LittleEndian(pixels.AsSpan(y * stride + x * 4));
    }

    private static Rows Scan(Int32Rect area, Func<int, int, bool> matches, string part)
    {
        var top = area.Y + area.Height;
        var bottom = -1;
        var count = 0;
        for (var y = area.Y; y < area.Y + area.Height; y++)
        for (var x = area.X; x < area.X + area.Width; x++)
        {
            if (!matches(x, y)) continue;
            top = Math.Min(top, y);
            bottom = y;
            count++;
        }
        return count > 0 ? new Rows(top, bottom, count) : throw new InvalidOperationException($"No rendered pixels found for {part}.");
    }

    private static void CheckStep(string name, string part, Rows previous, Rows current)
    {
        if (current.Top - previous.Top != Scale || current.Bottom - previous.Bottom != Scale)
            throw new InvalidOperationException($"{name}: {part} Y={RelativeY(current)} must move down {Scale} pixels from {RelativeY(previous)}.");
    }

    private static string RelativeY(Rows rows) => $"{rows.Top - FieldTop}..{rows.Bottom - FieldTop}";

    private static FormattedText Text(string text, int emSize, Brush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, emSize, brush, pixelsPerDip: 1);

    private static SolidColorBrush Brush(uint argb) =>
        new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

    private readonly record struct Rows(int Top, int Bottom, int Count);
    private sealed record PreviewFrame(string Name, RenderTargetBitmap Image, Rows Face, Rows Caption, Rows Arrow);
}
