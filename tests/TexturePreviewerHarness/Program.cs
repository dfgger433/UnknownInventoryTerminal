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
            CheckSelection(window, regions, viewport, "Close", new Int32Rect(96, 0, 16, 16));
            CheckSelection(window, regions, viewport, "ButtonHover", new Int32Rect(212, 128, 18, 20));
            viewport.Height = 96;
            CheckSelection(window, regions, viewport, "Refresh", new Int32Rect(232, 200, 8, 8));
            viewport.ClearValue(FrameworkElement.HeightProperty);
            viewport.Width = 100;
            CheckSelection(window, regions, viewport, "Refresh", new Int32Rect(232, 200, 8, 8));
            Console.WriteLine("Atlas layout passed: full atlas and aligned crops at three positions, including short and narrow viewports.");
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

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.01;
}
