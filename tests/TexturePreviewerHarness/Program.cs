// SPDX-License-Identifier: GPL-3.0-only
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
            var screen = FindScreenJson();
            var project = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(screen)!, "..", "..", "..", ".."));
            var findRoot = typeof(MainWindow).GetMethod("FindDefaultResourceRoot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var fromPublish = findRoot.Invoke(null, new object[] { Path.Combine(project, "dist", "TexturePreviewer") });
            var sourceRoot = Path.Combine(project, "assets", "unknowninventoryterminal");
            if (!Equals(fromPublish, sourceRoot) || ((TextBlock)window.FindName("FolderLabel")).Text != sourceRoot)
                throw new InvalidOperationException($"Both development and published previewers must watch {sourceRoot}; published root: {fromPublish}.");
            if (((TextBox)window.FindName("StatusBox")).Text != "屏幕定义和所有图集区域均有效。")
                throw new InvalidOperationException("Previewer reported warnings while loading the new asset namespace.");
            CheckLegacyRootIsIgnored(findRoot, sourceRoot);
            var style = UnknownInventoryTerminal.Ae2ScreenStyle.Parse(File.ReadAllText(screen));
            CheckSelection(window, regions, viewport, "Close", Expected(style, UnknownInventoryTerminal.Ae2Image.Close));
            CheckSelection(window, regions, viewport, "ButtonHover", Expected(style, UnknownInventoryTerminal.Ae2Image.ButtonHover));
            viewport.Height = 96;
            CheckSelection(window, regions, viewport, "Thumb", Expected(style, UnknownInventoryTerminal.Ae2Image.Thumb), 12, 15);
            CheckSelection(window, regions, viewport, "ThumbDisabled", Expected(style, UnknownInventoryTerminal.Ae2Image.ThumbDisabled), 12, 15);
            CheckSelection(window, regions, viewport, "VerticalButtonsBgBottom", Expected(style, UnknownInventoryTerminal.Ae2Image.VerticalButtonsBgBottom), 21, 26);
            viewport.ClearValue(FrameworkElement.HeightProperty);
            viewport.Width = 100;
            CheckSelection(window, regions, viewport, "Bottom", Expected(style, UnknownInventoryTerminal.Ae2Image.Bottom));
            CheckComposite(window, style);
            if (Environment.GetEnvironmentVariable("UIT_PREVIEW_SNAPSHOT") is { Length: > 0 } snapshot) SaveComposite(window, snapshot);
            Console.WriteLine("Asset root and atlas layout passed: source textures preferred; five crops including independent scrollers.");
            Console.WriteLine("Composite passed: board, toolbar states, scroll thumb and shared tooltip frame render at 2x.");
            DropdownPreview.Check(style, sourceRoot);
        }
        finally
        {
            window.Close();
            app.Shutdown();
        }
    }

    private static void CheckSelection(Window window, ListBox regions, Viewbox viewport, string name, Int32Rect expected,
        int width = 256, int height = 256)
    {
        regions.SelectedItem = name;
        window.UpdateLayout();
        var image = (Image)window.FindName("AtlasImage");
        var overlay = (Canvas)window.FindName("AtlasOverlay");
        var crop = (Image)window.FindName("CropImage");
        if (image.Source is not BitmapSource atlas || atlas.PixelWidth != width || atlas.PixelHeight != height ||
            crop.Source is not CroppedBitmap bitmap || bitmap.SourceRect != expected || !ReferenceEquals(bitmap.Source, atlas))
            throw new InvalidOperationException($"{name}: atlas and crop do not use the same {width}×{height} source and expected region.");

        var imageBounds = BoundsIn(image, viewport);
        var outlineBounds = BoundsIn((FrameworkElement)overlay.Children[0], viewport);
        var scale = imageBounds.Height / atlas.PixelHeight;
        var mustShrink = width > viewport.ActualWidth || height > viewport.ActualHeight;
        if (scale <= 0 || scale > 1.01 || (mustShrink && scale >= 1) || !Near(imageBounds.Width, width * scale) ||
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
            var path = Path.Combine(directory.FullName, "assets", "unknowninventoryterminal", "screens", "terminals", "terminal.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("assets/unknowninventoryterminal/screens/terminals/terminal.json was not found above the harness output.");
    }

    private static void CheckLegacyRootIsIgnored(System.Reflection.MethodInfo findRoot, string sourceRoot)
    {
        // Put the fixture in the ignored harness output; the nearest legacy tree must lose to the real source tree.
        var fixture = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, $"uit-preview-root-{Guid.NewGuid():N}"));
        try
        {
            var oldScreenDir = Directory.CreateDirectory(Path.Combine(fixture.FullName, "Assets", "AE2", "screens", "terminals"));
            File.WriteAllText(Path.Combine(oldScreenDir.FullName, "terminal.json"), "{}");
            if (!Equals(findRoot.Invoke(null, new object[] { fixture.FullName }), sourceRoot))
                throw new InvalidOperationException("Previewer must ignore a legacy-only asset tree.");
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
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
        const int padding = UnknownInventoryTerminal.Ae2TerminalLayout.ChromeTopPadding;
        var y = (double)(padding * 2);
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
        if (bar.Length != 4 || barHeight != (4 * 22 + 6) * 2 || bar.Any(image => image.Width != 21 * 2))
            throw new InvalidOperationException("Button bar must span four side buttons and meet the top outline at full width.");
        for (var i = 1; i < bar.Length; i++)
            if (Canvas.GetTop(bar[i - 1]) + bar[i - 1].Height != Canvas.GetTop(bar[i]))
                throw new InvalidOperationException("Button bar slices must meet without gaps.");

        var slot = canvas.Children.OfType<Border>().Single(border => Equals(border.Tag, "SlotHover"));
        if (Canvas.GetLeft(slot) != 98 || Canvas.GetTop(slot) != (17 + padding) * 2 || slot.Width != 36 || slot.Height != 36 ||
            slot.BorderThickness != new Thickness(2) || slot.IsHitTestVisible)
            throw new InvalidOperationException("Slot hover must overlay the second grid cell at 2× and be visual only.");

        var thumb = RequireImage(UnknownInventoryTerminal.Ae2Image.Thumb, 145, 18 + padding);
        if (thumb.Width != 24 || thumb.Height != 30)
            throw new InvalidOperationException("The native scroller must draw at 2× its 12×15 sprite size.");
        var close = RequireImage(UnknownInventoryTerminal.Ae2Image.Button, 143, 0);
        var search = RequireImage(UnknownInventoryTerminal.Ae2Image.Search, 79, 3 + padding);
        if (Canvas.GetTop(close) + close.Height > Canvas.GetTop(thumb) || Canvas.GetLeft(search) + search.Width >= Canvas.GetLeft(close))
            throw new InvalidOperationException("The top-right close button must not obscure the thumb or search field.");
        var normal = RequireImage(UnknownInventoryTerminal.Ae2Image.Button, 179, 74);
        var hover = RequireImage(UnknownInventoryTerminal.Ae2Image.ButtonHover, 179, 99);
        var focus = RequireImage(UnknownInventoryTerminal.Ae2Image.ButtonFocus, 245, 98);
        if (Canvas.GetTop(hover) + hover.Height != Canvas.GetTop(focus) + focus.Height || normal.Height != focus.Height)
            throw new InvalidOperationException("The lowered hover face must share the focused face's bottom edge.");
        CheckCenteredIcon(close, UnknownInventoryTerminal.Ae2Image.Close, new Int32Rect(4, 4, 8, 9));
        CheckCenteredIcon(RequireImage(UnknownInventoryTerminal.Ae2Image.Button, 8, 3 + padding), UnknownInventoryTerminal.Ae2Image.SortName, new Int32Rect(3, 4, 10, 9));
        CheckCenteredIcon(RequireImage(UnknownInventoryTerminal.Ae2Image.Button, 8, 25 + padding), UnknownInventoryTerminal.Ae2Image.SortAscending, new Int32Rect(4, 3, 8, 11));
        CheckCenteredIcon(RequireImage(UnknownInventoryTerminal.Ae2Image.Button, 8, 47 + padding), UnknownInventoryTerminal.Ae2Image.TerminalStyleSmall, new Int32Rect(4, 4, 9, 9));
        if (!images.Any(image => image.Source is CroppedBitmap crop && crop.SourceRect == Expected(style, UnknownInventoryTerminal.Ae2Image.Settings)))
            throw new InvalidOperationException("Composite is missing the settings icon.");
        CheckCenteredIcon(hover, UnknownInventoryTerminal.Ae2Image.SortName, new Int32Rect(3, 4, 10, 9));

        var tooltip = images.Where(image => Equals(image.Tag, UnknownInventoryTerminal.Ae2Image.Tooltip.ToString())).ToArray();
        if (tooltip.Length != 9 || tooltip[0].Source is not CroppedBitmap corner ||
            corner.SourceRect != new Int32Rect(0, 0, 3, 3) || tooltip[0].Width != 6 || tooltip[0].Height != 6 ||
            tooltip[4].Source is not CroppedBitmap fill || fill.SourceRect != new Int32Rect(3, 3, 122, 6) ||
            tooltip[4].Width != 132 || tooltip[4].Height != 22)
            throw new InvalidOperationException("The shared tooltip must stretch the original search frame without scaling its corners.");

        void CheckCenteredIcon(Image face, UnknownInventoryTerminal.Ae2Image id, Int32Rect bounds)
        {
            var expected = Expected(style, id);
            var x = Canvas.GetLeft(face) + face.Width / 2 - (2 * bounds.X + bounds.Width);
            var y = Canvas.GetTop(face) + face.Height / 2 - (2 * bounds.Y + bounds.Height);
            var icon = images.FirstOrDefault(image => image.Source is CroppedBitmap crop && crop.SourceRect == expected &&
                Canvas.GetLeft(image) == x && Canvas.GetTop(image) == y && image.Width == expected.Width * 2 && image.Height == expected.Height * 2);
            if (icon is null || canvas.Children.IndexOf(face) >= canvas.Children.IndexOf(icon))
                throw new InvalidOperationException($"{id} must draw after the face with its visible pixels centered.");
        }

        Image RequireImage(UnknownInventoryTerminal.Ae2Image id, int x, int top)
        {
            var expected = Expected(style, id);
            return images.FirstOrDefault(image => image.Source is CroppedBitmap crop && crop.SourceRect == expected &&
                Canvas.GetLeft(image) == x * 2 && Canvas.GetTop(image) == top * 2)
                ?? throw new InvalidOperationException($"Composite is missing {id} at ({x}, {top}).");
        }
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
