using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Threading;
using UnknownInventoryTerminal;

namespace UnknownInventoryTerminal.TexturePreviewer;

public partial class MainWindow : Window
{
    private static readonly string[] RegionNames = Enum.GetNames<Ae2Image>();
    private readonly ObservableCollection<string> _regions = new();
    private readonly DispatcherTimer _reloadTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private FileSystemWatcher? _watcher;
    private Ae2ScreenStyle? _style;
    private string? _root;
    private string? _selected;
    private bool _loading;
    private string _debugComponent = "Terminal";
    private double _debugOffsetX, _debugOffsetY;

    public MainWindow()
    {
        InitializeComponent();
        RegionList.ItemsSource = _regions;
        DebugComponent.ItemsSource = new[] { "Terminal", "Toolbar", "Search", "Tooltip", "Storage", "Equipment" };
        DebugComponent.SelectedIndex = 0;
        _reloadTimer.Tick += (_, _) => { _reloadTimer.Stop(); LoadAssets(); };
        Loaded += (_, _) =>
        {
            var root = FindDefaultResourceRoot(AppContext.BaseDirectory);
            if (root is not null) SetRoot(root);
        };
        Closed += (_, _) => _watcher?.Dispose();
    }

    // A published EXE lives under dist/, whose built copy would hide live edits in the project assets.
    internal static string? FindDefaultResourceRoot(string start)
    {
        string? nearest = null;
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Assets", "AE2");
            if (!File.Exists(Path.Combine(candidate, "screens", "terminals", "terminal.json"))) continue;
            if (File.Exists(Path.Combine(directory.FullName, "src", "UnknownInventoryTerminal", "UnknownInventoryTerminal.csproj")))
                return candidate;
            nearest ??= candidate;
        }
        return nearest;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 AE2 资源目录（包含 screens 和 textures）", Multiselect = false };
        if (dialog.ShowDialog(this) == true) SetRoot(dialog.FolderName);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadAssets();

    private void SetRoot(string path)
    {
        try
        {
            var root = Path.GetFullPath(path);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                Filter = "*.*"
            };
            watcher.Changed += QueueReload;
            watcher.Created += QueueReload;
            watcher.Deleted += QueueReload;
            watcher.Renamed += QueueReload;
            _watcher?.Dispose();
            _watcher = watcher;
            _root = root;
            FolderLabel.Text = _root;
            _watcher.EnableRaisingEvents = true;
            LoadAssets();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusBox.Text = ex.Message;
            LiveIndicator.Text = "● 无法监视资源目录";
            LiveIndicator.Foreground = Brushes.OrangeRed;
        }
    }

    private void QueueReload(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || e.FullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            Dispatcher.BeginInvoke(() => { _reloadTimer.Stop(); _reloadTimer.Start(); });
    }

    private void LoadAssets()
    {
        if (_loading || _root is null) return;
        _loading = true;
        try
        {
            var jsonPath = Path.Combine(_root, "screens", "terminals", "terminal.json");
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("找不到 screens/terminals/terminal.json", jsonPath);
            _style = Ae2ScreenStyle.Parse(File.ReadAllText(jsonPath));
            var previouslySelected = _selected;
            _regions.Clear();
            foreach (var name in RegionNames)
                if (_style.TryGet(Enum.Parse<Ae2Image>(name), out _)) _regions.Add(name);
            RegionCountLabel.Text = $"{_regions.Count} / {RegionNames.Length} 个有效区域";
            var warnings = new List<string>(_style.Warnings);
            foreach (var name in _regions)
            {
                var id = Enum.Parse<Ae2Image>(name);
                _style.TryGet(id, out var region);
                try
                {
                    var bitmap = LoadBitmap(TexturePath(region.Texture));
                    if (!region.Fits(bitmap.PixelWidth, bitmap.PixelHeight)) warnings.Add($"{name}: 裁切 [{region.X}, {region.Y}, {region.Width}, {region.Height}] 超出 {bitmap.PixelWidth}×{bitmap.PixelHeight}");
                    if (Ae2ScreenStyle.IsBoardPart(id) && (region.Width != Ae2TerminalLayout.BoardWidth || region.Height != Ae2ScreenStyle.BoardPartHeight(id)))
                        warnings.Add($"{name}: 底板分段须为 {Ae2TerminalLayout.BoardWidth}×{Ae2ScreenStyle.BoardPartHeight(id)}，游戏内不会拉伸它");
                    if (id == Ae2Image.VerticalButtonsBg && region.Width != Ae2TerminalLayout.ButtonBarWidth)
                        warnings.Add($"{name}: 按钮栏背景须为 {Ae2TerminalLayout.ButtonBarWidth} 像素宽");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
                {
                    warnings.Add($"{name}: {region.Texture} 无法读取 ({ex.Message})");
                }
            }
            StatusBox.Text = warnings.Count == 0 ? "屏幕定义和所有图集区域均有效。" : string.Join(Environment.NewLine, warnings);
            RenderComposite();
            if (!string.IsNullOrEmpty(previouslySelected) && _regions.Contains(previouslySelected)) RegionList.SelectedItem = previouslySelected;
            else RegionList.SelectedIndex = _regions.Count > 0 ? 0 : -1;
            if (RegionList.SelectedItem is string selectedName) ShowRegion(selectedName);
            else ClearAtlasPreview();
            LiveIndicator.Text = "● 实时监视中";
            LiveIndicator.Foreground = (Brush)FindResource("AccentBrush");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or ArgumentException)
        {
            _style = null;
            _regions.Clear();
            ClearAtlasPreview();
            RegionCountLabel.Text = "资源无法载入";
            StatusBox.Text = ex.Message;
            CompositeCanvas.Children.Clear();
            LiveIndicator.Text = "● 载入失败";
            LiveIndicator.Foreground = Brushes.OrangeRed;
        }
        finally { _loading = false; }
    }

    private string TexturePath(string logical) => Path.Combine(_root!, "textures", logical.Replace('/', Path.DirectorySeparatorChar));

    private static BitmapSource LoadBitmap(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private BitmapSource Crop(Ae2ImageRegion region) => Crop(LoadBitmap(TexturePath(region.Texture)), region);

    private static BitmapSource Crop(BitmapSource bitmap, Ae2ImageRegion region)
    {
        if (!region.Fits(bitmap.PixelWidth, bitmap.PixelHeight))
            throw new InvalidDataException("裁切区域超出图集边界。");
        var crop = new CroppedBitmap(bitmap, new Int32Rect(region.X, region.Y, region.Width, region.Height));
        crop.Freeze();
        return crop;
    }

    private void RegionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RegionList.SelectedItem is string name) ShowRegion(name);
    }

    private void DebugComponent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DebugComponent.SelectedItem is string name) _debugComponent = name;
        ApplyDebugPreview();
    }

    private void DebugOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _debugOffsetX = DebugOffsetX.Value;
        _debugOffsetY = DebugOffsetY.Value;
        DebugOffsetXText.Text = _debugOffsetX.ToString("0");
        DebugOffsetYText.Text = _debugOffsetY.ToString("0");
        ApplyDebugPreview();
    }

    private void SimulationChanged(object sender, RoutedEventArgs e) => ApplyDebugPreview();

    private void ResetDebug_Click(object sender, RoutedEventArgs e)
    {
        DebugOffsetX.Value = 0;
        DebugOffsetY.Value = 0;
        SimulateHover.IsChecked = false;
        SimulateFocus.IsChecked = false;
        SimulateClick.IsChecked = false;
        ApplyDebugPreview();
    }

    private void ApplyDebugPreview()
    {
        if (!IsLoaded) return;
        RenderComposite();
        if (RegionList.SelectedItem is string name) ShowRegion(name);
    }

    private void ShowRegion(string name)
    {
        if (_style is null) return;
        _selected = name;
        var id = Enum.Parse<Ae2Image>(name);
        if (!_style.TryGet(id, out var region)) return;
        DetailName.Text = name;
        DetailTexture.Text = region.Texture;
        DetailRect.Text = $"srcRect  [{region.X}, {region.Y}, {region.Width}, {region.Height}]";
        try
        {
            var atlas = LoadBitmap(TexturePath(region.Texture));
            var crop = Crop(atlas, region);
            // The Viewbox scales the whole host, so the bitmap and outline share one pixel coordinate space.
            AtlasHost.Width = atlas.PixelWidth;
            AtlasHost.Height = atlas.PixelHeight;
            AtlasImage.Source = atlas;
            CropImage.Source = crop;
            AtlasLabel.Text = $"{region.Texture} · {atlas.PixelWidth} × {atlas.PixelHeight}";
            DrawAtlasSelection(region);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException or InvalidDataException)
        {
            ClearAtlasPreview();
            StatusBox.Text = $"{name}: {ex.Message}";
        }
    }

    private void ClearAtlasPreview()
    {
        AtlasHost.Width = AtlasHost.Height = 0;
        AtlasImage.Source = CropImage.Source = null;
        AtlasOverlay.Children.Clear();
        AtlasLabel.Text = "";
    }

    private void DrawAtlasSelection(Ae2ImageRegion region)
    {
        AtlasOverlay.Children.Clear();
        var outline = new Rectangle
        {
            Width = region.Width,
            Height = region.Height,
            Stroke = (Brush)FindResource("AccentBrush"),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(45, 116, 213, 194))
        };
        Canvas.SetLeft(outline, region.X);
        Canvas.SetTop(outline, region.Y);
        AtlasOverlay.Children.Add(outline);
    }

    // Mirrors the game: stack the board regions at an integer scale, preserving their alpha.
    private void RenderComposite()
    {
        CompositeCanvas.Children.Clear();
        if (_style is null) return;
        const int scale = 2;
        const int rail = Ae2TerminalLayout.Rail;
        const int board = Ae2TerminalLayout.BoardWidth;
        var stateX = rail + board + 14;
        var width = (stateX + 132) * scale;
        var height = 246 * scale;
        CompositeCanvas.Width = width;
        CompositeCanvas.Height = height;
        CompositeHost.Width = width + 20;
        CompositeHost.Height = height + 20;
        CompositeCanvas.Background = new SolidColorBrush(Color.FromRgb(23, 27, 32));

        // Small terminal style: three storage rows, stacked from the board parts as in AE2's MEStorageScreen.drawBG.
        var layout = new Ae2TerminalLayout(width, Ae2TerminalLayout.BaseBoardHeight + 2 * Ae2TerminalLayout.Margin);
        var boardY = 0;
        foreach (var part in BoardParts(layout.Rows))
        {
            AddImage(part, rail, boardY, scale);
            boardY += Ae2ScreenStyle.BoardPartHeight(part);
        }
        AddButtonBar(Ae2TerminalLayout.ToolbarButtons, scale);
        AddSlotHighlight(rail + Ae2TerminalLayout.Inset + Ae2TerminalLayout.Pitch, Ae2TerminalLayout.Header, scale);
        var muted = Color.FromRgb(82, 85, 100);
        AddLabel("Terminal", rail + 7, 2, 46, 15, scale, 8, true);
        var search = SourceSize(Ae2Image.Search, Ae2TerminalLayout.SearchWidth, 12);
        var searchWidth = Math.Min(search.Width, Ae2TerminalLayout.SearchWidth);
        AddRegion(Ae2Image.Search, Ae2TerminalLayout.SearchX, Ae2TerminalLayout.SearchY, searchWidth,
            Math.Min(search.Height, Ae2TerminalLayout.SearchHeight), scale);
        AddLabel("Search items...", Ae2TerminalLayout.SearchX + 3, Ae2TerminalLayout.SearchY,
            Math.Max(0, searchWidth - 5), 12, scale, 6, false, Colors.White);

        var thumb = SourceSize(Ae2Image.Thumb, Ae2TerminalLayout.DefaultThumbWidth, Ae2TerminalLayout.DefaultThumbHeight);
        AddImage(Ae2Image.Thumb, rail + Ae2TerminalLayout.ThumbLeft(thumb.Width), Ae2TerminalLayout.Header + Ae2TerminalLayout.GrooveBorder, scale);
        AddLabel("12 / 30 items  |  5 groups", rail + Ae2TerminalLayout.Inset, layout.StatusTop, 62, Ae2TerminalLayout.StatusHeight, scale, 6, false, muted);
        AddLabel("Equipment 1–3/4 ↕", rail + Ae2TerminalLayout.Inset + 64, layout.StatusTop, Ae2TerminalLayout.GridWidth - 64,
            Ae2TerminalLayout.StatusHeight, scale, 6, false, muted, TextAlignment.Right);

        AddToolbarButton(Ae2Image.Close, Ae2TerminalLayout.CloseX, Ae2TerminalLayout.CloseY, scale);
        // Toolbar buttons sit on vertical_buttons_bg.png left of the board, in game order.
        AddToolbarButton(Ae2Image.SortName, Ae2TerminalLayout.ToolbarX, Ae2TerminalLayout.ToolbarY(0), scale);
        AddToolbarButton(Ae2Image.SortAscending, Ae2TerminalLayout.ToolbarX, Ae2TerminalLayout.ToolbarY(1), scale);
        AddToolbarButton(Ae2Image.TerminalStyleSmall, Ae2TerminalLayout.ToolbarX, Ae2TerminalLayout.ToolbarY(2), scale);
        AddToolbarButton(Ae2Image.Back, Ae2TerminalLayout.ToolbarX, Ae2TerminalLayout.ToolbarY(3), scale);
        foreach (UIElement element in CompositeCanvas.Children)
            Canvas.SetTop(element, Canvas.GetTop(element) + Ae2TerminalLayout.ChromeTopPadding * scale);

        // State samples are shown at their own pixel size beside the terminal.
        var stateCaption = new TextBlock { Text = "交互状态", Foreground = (Brush)FindResource("AccentBrush"), FontSize = 11, FontWeight = FontWeights.SemiBold };
        Canvas.SetLeft(stateCaption, stateX * scale);
        Canvas.SetTop(stateCaption, 2 * scale);
        CompositeCanvas.Children.Add(stateCaption);
        var caption = Color.FromRgb(150, 160, 168);
        var y = 20;
        AddImage(Ae2Image.Search, stateX, y, scale);
        AddLabel("搜索 Search", stateX, y + SourceSize(Ae2Image.Search, 0, 12).Height + 1, 80, 10, scale, 6, false, caption);
        y += 26;
        AddImage(Ae2Image.SearchFocused, stateX, y, scale);
        AddLabel("聚焦 Focused", stateX, y + SourceSize(Ae2Image.SearchFocused, 0, 12).Height + 1, 80, 10, scale, 6, false, caption);
        y += 28;
        AddToolbarButton(Ae2Image.SortName, stateX, y, scale);
        AddLabel("普通按钮", stateX + 22, y + 3, 60, 14, scale, 6, false, caption);
        y += 24;
        AddToolbarButton(Ae2Image.SortName, stateX, y, scale, Ae2ToolbarAppearance.Face(hovered: true, focused: false));
        AddLabel("悬停 / 下沉", stateX + 22, y + 3, 44, 14, scale, 6, false, caption);
        AddToolbarButton(Ae2Image.SortName, stateX + 66, y, scale, Ae2ToolbarAppearance.Face(hovered: false, focused: true));
        AddLabel("聚焦按钮", stateX + 88, y + 3, 40, 14, scale, 6, false, caption);
        y += 26;
        AddImage(Ae2Image.Thumb, stateX, y, scale);
        AddLabel("可滚动", stateX + 20, y, 40, 14, scale, 6, false, caption);
        AddImage(Ae2Image.ThumbDisabled, stateX + 60, y, scale);
        AddLabel("禁用", stateX + 80, y, 35, 14, scale, 6, false, caption);
        y += 24;
        AddImage(Ae2Image.Tooltip, stateX, y, scale);
        AddLabel("Tooltip sample", stateX + 2, y, 108, SourceSize(Ae2Image.Tooltip, 0, 12).Height, scale, 6, false, Colors.White);
        y += 20;
        // Terminal style icons, in the order the button cycles through them.
        var styles = new[] { Ae2Image.TerminalStyleSmall, Ae2Image.TerminalStyleMedium, Ae2Image.TerminalStyleTall, Ae2Image.TerminalStyleFull };
        for (var i = 0; i < styles.Length; i++) AddImage(styles[i], stateX + i * 20, y, scale);
        AddLabel("终端风格 短/中/长/最长", stateX, y + 17, 110, 10, scale, 6, false, caption);
        ApplyDebugOffsets(scale);
    }

    private void ApplyDebugOffsets(int scale)
    {
        var offset = new Vector(_debugOffsetX * scale, _debugOffsetY * scale);
        foreach (UIElement element in CompositeCanvas.Children)
        {
            var tag = element is FrameworkElement framework ? framework.Tag?.ToString() : null;
            if (!MatchesDebugComponent(tag)) continue;
            element.RenderTransform = new TranslateTransform(offset.X, -offset.Y);
        }
        if (SimulateHover.IsChecked == true || SimulateFocus.IsChecked == true || SimulateClick.IsChecked == true)
        {
            var face = CompositeCanvas.Children.OfType<Image>().FirstOrDefault(image =>
                image.Source is CroppedBitmap crop && crop.SourceRect == RegionRect(Ae2Image.Button));
            if (face is not null)
            {
                face.Opacity = SimulateClick.IsChecked == true ? 0.68 : 0.82;
                face.ToolTip = SimulateClick.IsChecked == true ? "点击状态 / pressed" :
                    SimulateHover.IsChecked == true ? "悬停状态 / hover" : "聚焦状态 / focused";
            }
        }
    }

    private bool MatchesDebugComponent(string? tag) => _debugComponent switch
    {
        "Toolbar" => tag == "ButtonBar" || tag == Ae2Image.Button.ToString() || tag == Ae2Image.ButtonHover.ToString() || tag == Ae2Image.ButtonFocus.ToString(),
        "Search" => tag == Ae2Image.Search.ToString() || tag == Ae2Image.SearchFocused.ToString(),
        "Tooltip" => tag == Ae2Image.Tooltip.ToString(),
        "Storage" => tag == Ae2Image.Header.ToString() || tag == Ae2Image.FirstRow.ToString() || tag == Ae2Image.Row.ToString() || tag == Ae2Image.LastRow.ToString() || tag == Ae2Image.Bottom.ToString(),
        "Equipment" => tag == "SlotHover",
        _ => true
    };

    private Int32Rect RegionRect(Ae2Image id) => _style is not null && _style.TryGet(id, out var region)
        ? new Int32Rect(region.X, region.Y, region.Width, region.Height) : new Int32Rect();

    internal static IReadOnlyList<Ae2Image> BoardParts(int rows)
    {
        var parts = new List<Ae2Image> { Ae2Image.Header, Ae2Image.FirstRow };
        for (var i = 2; i < rows; i++) parts.Add(Ae2Image.Row);
        parts.Add(Ae2Image.LastRow);
        parts.Add(Ae2Image.Bottom);
        return parts;
    }

    // Mirrors AE2 VerticalButtonBar: fixed 2-row top and 4-row bottom, stretched middle.
    private void AddButtonBar(int buttons, int scale)
    {
        if (_style is null || !_style.TryGet(Ae2Image.VerticalButtonsBg, out var region)) return;
        const int top = Ae2TerminalLayout.ButtonBarTopBorder, bottom = Ae2TerminalLayout.ButtonBarBottomBorder;
        if (region.Height <= top + bottom) return;
        var height = Ae2TerminalLayout.ButtonBarHeight(buttons);
        AddRows(region, 0, top, Ae2TerminalLayout.ButtonBarY, top, scale);
        AddRows(region, top, region.Height - top - bottom, Ae2TerminalLayout.ButtonBarY + top, height - top - bottom, scale);
        AddRows(region, region.Height - bottom, bottom, Ae2TerminalLayout.ButtonBarY + height - bottom, bottom, scale);
    }

    private void AddRows(Ae2ImageRegion region, int sourceTop, int sourceHeight, int y, int height, int scale)
    {
        try
        {
            var rows = new Ae2ImageRegion(region.Texture, region.X, region.Y + sourceTop, region.Width, sourceHeight, 0);
            var image = new Image { Source = Crop(rows), Width = region.Width * scale, Height = height * scale, Stretch = Stretch.Fill, SnapsToDevicePixels = true, Tag = "ButtonBar" };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(image, Ae2TerminalLayout.ButtonBarX * scale);
            Canvas.SetTop(image, y * scale);
            CompositeCanvas.Children.Add(image);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException or InvalidDataException)
        {
            StatusBox.Text = $"{Ae2Image.VerticalButtonsBg}: {ex.Message}";
        }
    }

    private (int Width, int Height) SourceSize(Ae2Image id, int width, int height) =>
        _style is not null && _style.TryGet(id, out var region) ? (region.Width, region.Height) : (width, height);

    private void AddImage(Ae2Image id, int x, int y, int scale)
    {
        if (_style is not null && _style.TryGet(id, out var region)) AddRegion(id, x, y, region.Width, region.Height, scale);
    }

    private void AddRegion(Ae2Image id, int x, int y, int width, int height, int scale)
    {
        if (_style is null || !_style.TryGet(id, out var region)) return;
        try
        {
            var image = new Image { Source = Crop(region), Width = width * scale, Height = height * scale, Stretch = Stretch.Fill, SnapsToDevicePixels = true, Tag = id.ToString() };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(image, x * scale);
            Canvas.SetTop(image, y * scale);
            CompositeCanvas.Children.Add(image);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException)
        {
            StatusBox.Text = $"{id}: {ex.Message}";
        }
    }

    private void AddSlotHighlight(int x, int y, int scale)
    {
        static SolidColorBrush Brush(uint argb) => new(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        var size = Ae2TerminalLayout.Pitch * scale;
        var highlight = new Border { Width = size, Height = size, BorderThickness = new Thickness(scale),
            BorderBrush = Brush(Ae2SlotAppearance.HoverBorder), Background = Brush(Ae2SlotAppearance.HoverFill),
            SnapsToDevicePixels = true, IsHitTestVisible = false, Tag = "SlotHover" };
        Canvas.SetLeft(highlight, x * scale);
        Canvas.SetTop(highlight, y * scale);
        CompositeCanvas.Children.Add(highlight);
    }

    private void AddToolbarButton(Ae2Image icon, int x, int y, int scale, Ae2Image face = Ae2Image.Button)
    {
        y += Ae2ToolbarAppearance.OffsetY(face);
        AddImage(face, x, y, scale);
        if (_style is null || !_style.TryGet(icon, out var region)) return;
        try
        {
            var bitmap = Crop(region);
            var rgba = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var stride = checked(region.Width * 4);
            var pixels = new byte[checked(stride * region.Height)];
            rgba.CopyPixels(pixels, stride, 0);
            var visible = Ae2ToolbarAppearance.VisibleBounds(region.Width, region.Height,
                (ix, iy) => pixels[iy * stride + ix * 4 + 3]);
            if (visible is not { } bounds) return;
            var size = SourceSize(face, Ae2TerminalLayout.ButtonWidth, Ae2TerminalLayout.ButtonHeight);
            var (dx, dy) = Ae2ToolbarAppearance.CenterIcon(bounds, size.Width, size.Height, scale);
            var image = new Image { Source = bitmap, Width = region.Width * scale, Height = region.Height * scale,
                Stretch = Stretch.Fill, SnapsToDevicePixels = true };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(image, x * scale + dx);
            Canvas.SetTop(image, y * scale + dy);
            CompositeCanvas.Children.Add(image);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException or OverflowException)
        {
            StatusBox.Text = $"{icon}: {ex.Message}";
        }
    }


    private void AddLabel(string text, int x, int y, int width, int height, int scale, int fontSize, bool bold, Color? color = null,
        TextAlignment alignment = TextAlignment.Left)
    {
        var label = new TextBlock { Text = text, Width = width * scale, Height = height * scale, FontSize = fontSize * scale, TextAlignment = alignment,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = color is { } c ? new SolidColorBrush(c) : new SolidColorBrush(Color.FromRgb(48, 50, 58)),
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Canvas.SetLeft(label, x * scale);
        Canvas.SetTop(label, y * scale);
        CompositeCanvas.Children.Add(label);
    }
}
