using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UnknownInventoryTerminal.TexturePreviewer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        var window = new MainWindow { Width = 1100, Height = 760, ShowInTaskbar = false };
        try
        {
            window.Show();
            window.UpdateLayout();
            var regions = (ListBox)window.FindName("RegionList");
            var viewport = (Viewbox)window.FindName("AtlasViewport");
            // The real window has a short atlas pane; make it even shorter on the second pass.
            // Expected crops come from the shipped screen JSON, so redrawn atlases only need their JSON kept in sync.
            var style = UnknownInventoryTerminal.Ae2ScreenStyle.Parse(File.ReadAllText(FindScreenJson()));
            CheckSelection(window, regions, viewport, "Close", Expected(style, UnknownInventoryTerminal.Ae2Image.Close));
            CheckSelection(window, regions, viewport, "ButtonHover", Expected(style, UnknownInventoryTerminal.Ae2Image.ButtonHover));
            viewport.Height = 96;
            CheckSelection(window, regions, viewport, "Thumb", Expected(style, UnknownInventoryTerminal.Ae2Image.Thumb));
            viewport.ClearValue(FrameworkElement.HeightProperty);
            viewport.Width = 100;
            CheckSelection(window, regions, viewport, "Bottom", Expected(style, UnknownInventoryTerminal.Ae2Image.Bottom));
            CheckComposite(window, style);
            if (Environment.GetEnvironmentVariable("UIT_PREVIEW_SNAPSHOT") is { Length: > 0 } snapshot) SaveComposite(window, snapshot);
            Console.WriteLine("Atlas layout passed: full atlas and aligned crops at four positions, including short and narrow viewports.");
            Console.WriteLine("Composite passed: board parts stacked at 2x with no fill behind them, button bar behind four toolbar buttons.");
        }
        finally
        {
            window.Close();
            app.Shutdown();
        }
    }

    private static void CheckSelection(Window window, ListBox regions, Viewbox viewport, string name, Int32Rect expected)
    {
        regions.SelectedItem = name;
        window.UpdateLayout();
        var image = (Image)window.FindName("AtlasImage");
        var overlay = (Canvas)window.FindName("AtlasOverlay");
        var crop = (Image)window.FindName("CropImage");
        if (image.Source is not BitmapSource { PixelWidth: 256, PixelHeight: 256 } atlas ||
            crop.Source is not CroppedBitmap bitmap || bitmap.SourceRect != expected || !ReferenceEquals(bitmap.Source, atlas))
            throw new InvalidOperationException($"{name}: atlas and crop do not use the same 256×256 source and expected region.");

        var imageBounds = BoundsIn(image, viewport);
        var outlineBounds = BoundsIn((FrameworkElement)overlay.Children[0], viewport);
        var scale = imageBounds.Height / atlas.PixelHeight;
        if (scale <= 0 || scale >= 1 || !Near(imageBounds.Width, imageBounds.Height) ||
            imageBounds.Left < -0.01 || imageBounds.Top < -0.01 ||
            imageBounds.Right > viewport.ActualWidth + 0.01 || imageBounds.Bottom > viewport.ActualHeight + 0.01)
            throw new InvalidOperationException($"{name}: full atlas {imageBounds} does not fit viewport {viewport.RenderSize}.");

        if (!Near(outlineBounds.Left, imageBounds.Left + expected.X * scale) ||
            !Near(outlineBounds.Top, imageBounds.Top + expected.Y * scale) ||
            !Near(outlineBounds.Width, expected.Width * scale) || !Near(outlineBounds.Height, expected.Height * scale))
            throw new InvalidOperationException($"{name}: outline {outlineBounds} is offset relative to atlas {imageBounds}.");
    }

    private static string FindScreenJson()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "Assets", "AE2", "screens", "terminals", "terminal.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Assets/AE2/screens/terminals/terminal.json was not found above the harness output.");
    }

    private static Int32Rect Expected(UnknownInventoryTerminal.Ae2ScreenStyle style, UnknownInventoryTerminal.Ae2Image id) =>
        style.TryGet(id, out var region) ? new Int32Rect(region.X, region.Y, region.Width, region.Height)
            : throw new InvalidOperationException($"{id} is missing from terminal.json.");

    private static void CheckComposite(Window window, UnknownInventoryTerminal.Ae2ScreenStyle style)
    {
        var canvas = (Canvas)window.FindName("CompositeCanvas");
        if (canvas.Children.OfType<System.Windows.Shapes.Rectangle>().Any())
            throw new InvalidOperationException("Composite still paints a solid fill behind the board.");
        // Small style: header, first, middle and last row, bottom, stacked without gaps at 2x their source size.
        var images = canvas.Children.OfType<Image>().ToList();
        var y = 0.0;
        foreach (var part in new[] { UnknownInventoryTerminal.Ae2Image.Header, UnknownInventoryTerminal.Ae2Image.FirstRow,
            UnknownInventoryTerminal.Ae2Image.Row, UnknownInventoryTerminal.Ae2Image.LastRow, UnknownInventoryTerminal.Ae2Image.Bottom })
        {
            var expected = Expected(style, part);
            var match = images.FirstOrDefault(image => image.Source is CroppedBitmap crop && crop.SourceRect == expected &&
                Canvas.GetTop(image) == y && image.Width == expected.Width * 2 && image.Height == expected.Height * 2);
            if (match is null) throw new InvalidOperationException($"Composite must draw {part} {expected} at y={y / 2} and 2x.");
            images.Remove(match);
            y += expected.Height * 2;
        }
        var bar = canvas.Children.OfType<Image>().Where(image => Equals(image.Tag, "ButtonBar")).ToArray();
        var barHeight = bar.Sum(image => image.Height);
        if (bar.Length != 3 || barHeight != (4 * 22 + 6) * 2 || bar.Any(image => image.Width != 21 * 2))
            throw new InvalidOperationException($"Button bar must be three 21 px wide slices, {4 * 22 + 6} px tall in total.");
    }

    // Optional visual check: writes the composite canvas to a PNG.
    private static void SaveComposite(Window window, string path)
    {
        var canvas = (Canvas)window.FindName("CompositeCanvas");
        canvas.Measure(new Size(canvas.Width, canvas.Height));
        canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
        var bitmap = new RenderTargetBitmap((int)canvas.Width, (int)canvas.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.01;
}
