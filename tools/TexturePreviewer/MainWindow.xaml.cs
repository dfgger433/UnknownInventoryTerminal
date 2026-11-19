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

    public MainWindow()
    {
        InitializeComponent();
        RegionList.ItemsSource = _regions;
        _reloadTimer.Tick += (_, _) => { _reloadTimer.Stop(); LoadAssets(); };
        Loaded += (_, _) =>
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "Assets", "AE2");
                if (!File.Exists(Path.Combine(candidate, "screens", "terminals", "terminal.json"))) continue;
                SetRoot(candidate);
                return;
            }
        };
        Closed += (_, _) => _watcher?.Dispose();
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
                    if ((id is Ae2Image.MainSlot or Ae2Image.EquipmentSlot) && region.Width % Ae2TerminalLayout.Columns != 0)
                        warnings.Add($"{name}: 六格贴图宽度 {region.Width} 不能被 6 整除");
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

    private BitmapSource Crop(Ae2ImageRegion region, int column = -1)
    {
        return Crop(LoadBitmap(TexturePath(region.Texture)), region, column);
    }

    private static BitmapSource Crop(BitmapSource bitmap, Ae2ImageRegion region, int column = -1)
    {
        if (column >= 0 && (region.Width % Ae2TerminalLayout.Columns != 0 || region.Border != 0))
            throw new InvalidDataException("六格条必须等宽且不能设置九宫格边宽。");
        var width = column < 0 ? region.Width : region.Width / Ae2TerminalLayout.Columns;
        var x = region.X + (column < 0 ? 0 : column * width);
        if (column >= Ae2TerminalLayout.Columns || !region.Fits(bitmap.PixelWidth, bitmap.PixelHeight))
            throw new InvalidDataException("裁切区域超出图集边界。");
        var crop = new CroppedBitmap(bitmap, new Int32Rect(x, region.Y, width, region.Height));
        crop.Freeze();
        return crop;
    }

    private void RegionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
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
        DetailRect.Text = $"srcRect  [{region.X}, {region.Y}, {region.Width}, {region.Height}]" + (region.Border > 0 ? $"   ·   border {region.Border}px" : "");
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

    private void RenderComposite()
    {
        CompositeCanvas.Children.Clear();
        if (_style is null) return;
        const int scale = 2;
        const int rail = Ae2TerminalLayout.Rail;
        const int board = Ae2TerminalLayout.BoardWidth;
        const int header = Ae2TerminalLayout.Header;
        const int footer = Ae2TerminalLayout.Footer;
        const int rows = 3, mainRows = 1, equipmentRows = 4;
        var panelHeight = rows * Ae2TerminalLayout.Pitch + Ae2TerminalLayout.DetailHeight + Ae2TerminalLayout.EquipmentHeader + (mainRows + equipmentRows) * Ae2TerminalLayout.Pitch;
        const int canvasLogicalWidth = 300;
        var width = canvasLogicalWidth * scale;
        var height = (header + footer) * scale + panelHeight * scale;
        CompositeCanvas.Width = width;
        CompositeCanvas.Height = height;
        CompositeHost.Width = width + 20;
        CompositeHost.Height = height + 20;
        CompositeCanvas.Background = new SolidColorBrush(Color.FromRgb(23, 27, 32));
        AddSolid(rail, header, board, panelHeight, scale, Color.FromRgb(199, 201, 210));

        // Header and footer frame.
        AddRegion(Ae2Image.Header, rail, 0, board - 2, header, scale);
        AddRegion(Ae2Image.HeaderEdge, rail + board - 2, 0, 2, header, scale);
        AddLabel("UNKNOWN INVENTORY TERMINAL", rail + 7, 2, 48, 17, scale, 8, true);
        AddRegion(Ae2Image.Search, rail + 55, 4, board - 79, 13, scale);
        AddLabel("Search items...", rail + 56, 5, board - 80, 11, scale, 6, false, Color.FromRgb(110, 114, 128));
        AddRegion(Ae2Image.Button, rail + board - 22, 2, 18, 20, scale);
        AddRegion(Ae2Image.Close, rail + board - 21, 4, 16, 16, scale);

        // Inventory rows and rails.
        for (var i = 0; i < rows; i++)
        {
            var y = header + i * Ae2TerminalLayout.Pitch;
            var left = i == 0 ? Ae2Image.FirstRow : i == rows - 1 ? Ae2Image.LastRow : Ae2Image.Row;
            var right = i == 0 ? Ae2Image.FirstRail : i == rows - 1 ? Ae2Image.LastRail : Ae2Image.Rail;
            AddRegion(left, rail, y, Ae2TerminalLayout.Inset + Ae2TerminalLayout.GridWidth, Ae2TerminalLayout.Pitch, scale);
            AddRegion(right, rail + Ae2TerminalLayout.Inset + Ae2TerminalLayout.GridWidth, y, board - Ae2TerminalLayout.Inset - Ae2TerminalLayout.GridWidth, Ae2TerminalLayout.Pitch, scale);
        }
        AddRegion(Ae2Image.Thumb, rail + Ae2TerminalLayout.ScrollbarLeft, header + 7, 12, 15, scale);

        // Lower loadout panel uses mock item glyphs to reveal the six slot strips.
        var lower = header + rows * Ae2TerminalLayout.Pitch;
        AddRegion(Ae2Image.LoadoutBackground, rail, lower, board, Ae2TerminalLayout.DetailHeight + Ae2TerminalLayout.EquipmentHeader + (mainRows + equipmentRows) * Ae2TerminalLayout.Pitch, scale);
        AddLabel("Container storage", rail + 54, lower + 6, 80, 12, scale, 6, false, Color.FromRgb(82, 85, 100));
        AddNineSlice(Ae2Image.BackButton, "‹  Back", rail + 7, lower + 2, 41, 18, scale);
        var slotsY = lower + Ae2TerminalLayout.DetailHeight;
        for (var i = 0; i < mainRows; i++)
            for (var c = 0; c < Ae2TerminalLayout.Columns; c++)
            {
                var x = rail + Ae2TerminalLayout.Inset + c * Ae2TerminalLayout.Pitch;
                var y = slotsY + i * Ae2TerminalLayout.Pitch;
                AddRegion(Ae2Image.MainSlot, x, y, Ae2TerminalLayout.Pitch, Ae2TerminalLayout.Pitch, scale, c);
                AddLabel(c.ToString(), x + 10, y + 10, 7, 7, scale, 5, false, Color.FromRgb(78, 80, 93));
            }
        var equipmentY = slotsY + mainRows * Ae2TerminalLayout.Pitch + Ae2TerminalLayout.EquipmentHeader;
        AddLabel("Equipment", rail + Ae2TerminalLayout.Inset, equipmentY - 13, 60, 10, scale, 6, true);
        for (var i = 0; i < equipmentRows; i++)
            for (var c = 0; c < Ae2TerminalLayout.Columns; c++)
            {
                var x = rail + Ae2TerminalLayout.Inset + c * Ae2TerminalLayout.Pitch;
                var y = equipmentY + i * Ae2TerminalLayout.Pitch;
                AddRegion(Ae2Image.EquipmentSlot, x, y, Ae2TerminalLayout.Pitch, Ae2TerminalLayout.Pitch, scale, c);
            }

        var footerY = header + panelHeight;
        AddRegion(Ae2Image.FooterLeft, rail, footerY, 2, footer, scale);
        AddRegion(Ae2Image.FooterCenter, rail + 2, footerY, board - 4, footer, scale);
        AddRegion(Ae2Image.FooterRight, rail + board - 2, footerY, 2, footer, scale);
        AddLabel("静态示例  ·  贴图预览", rail + 7, footerY + 1, 127, 13, scale, 6, false, Color.FromRgb(82, 85, 100));

        // Toolbar buttons are drawn in the separate 24px rail, then their icon crops are layered above.
        AddToolbarButton(Ae2Image.SortName, 3, 22, scale);
        AddToolbarButton(Ae2Image.SortAscending, 3, 43, scale);
        AddToolbarButton(Ae2Image.Refresh, 3, 64, scale);

        // State samples are placed in a labeled strip outside the assembled terminal.
        var stateX = rail + board + 14;
        var stateCaption = new TextBlock { Text = "交互状态", Foreground = (Brush)FindResource("AccentBrush"), FontSize = 11, FontWeight = FontWeights.SemiBold };
        Canvas.SetLeft(stateCaption, stateX * scale);
        Canvas.SetTop(stateCaption, 2 * scale);
        CompositeCanvas.Children.Add(stateCaption);
        AddRegion(Ae2Image.Search, stateX, 20, 112, 13, scale);
        AddLabel("搜索 Search", stateX, 34, 80, 10, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddRegion(Ae2Image.SearchFocused, stateX, 44, 112, 13, scale);
        AddLabel("聚焦 Focused", stateX, 58, 80, 10, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddButton(Ae2Image.Button, "", stateX, 72, scale);
        AddRegion(Ae2Image.SortName, stateX + 1, 74, 16, 16, scale);
        AddLabel("普通按钮", stateX + 22, 74, 60, 14, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddButton(Ae2Image.ButtonHover, "", stateX, 96, scale);
        AddRegion(Ae2Image.SortAmount, stateX + 1, 98, 16, 16, scale);
        AddLabel("悬停按钮", stateX + 22, 98, 60, 14, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddNineSlice(Ae2Image.BackButton, "‹  返回", stateX, 122, 41, 18, scale);
        AddLabel("返回按钮", stateX + 45, 124, 54, 14, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddNineSlice(Ae2Image.BackButtonHover, "‹  悬停", stateX, 146, 41, 18, scale);
        AddRegion(Ae2Image.Thumb, stateX, 174, 12, 15, scale);
        AddLabel("可滚动", stateX + 18, 174, 50, 14, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddRegion(Ae2Image.ThumbDisabled, stateX + 64, 174, 12, 15, scale);
        AddLabel("禁用", stateX + 82, 174, 35, 14, scale, 6, false, Color.FromRgb(150, 160, 168));
        AddRegion(Ae2Image.Tooltip, stateX, 202, 112, 15, scale);
        AddLabel("Tooltip sample", stateX + 2, 203, 108, 13, scale, 6, false);
    }

    private void AddRegion(Ae2Image id, int x, int y, int width, int height, int scale, int column = -1)
    {
        if (_style is null || !_style.TryGet(id, out var region)) return;
        try
        {
            var image = new Image { Source = Crop(region, column), Width = width * scale, Height = height * scale, Stretch = Stretch.Fill, SnapsToDevicePixels = true };
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

    private void AddToolbarButton(Ae2Image icon, int x, int y, int scale)
    {
        AddRegion(Ae2Image.Button, x, y, 18, 20, scale);
        AddRegion(icon, x + 1, y + 2, 16, 16, scale);
    }

    private void AddButton(Ae2Image face, string fallback, int x, int y, int scale, bool wide = false)
    {
        AddRegion(face, x, y, wide ? 41 : 18, wide ? 18 : 20, scale);
        if (!string.IsNullOrEmpty(fallback)) AddLabel(fallback, x + 1, y + 1, wide ? 39 : 16, wide ? 16 : 18, scale, wide ? 6 : 7, false);
    }

    private void AddNineSlice(Ae2Image id, string label, int x, int y, int width, int height, int scale)
    {
        if (_style is null || !_style.TryGet(id, out var region) || region.Border <= 0) return;
        var source = Crop(region);
        var b = region.Border;
        var xs = new[] { 0, b, region.Width - b, region.Width };
        var ys = new[] { 0, b, region.Height - b, region.Height };
        var dx = new[] { 0, b, width - b, width };
        var dy = new[] { 0, b, height - b, height };
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 3; col++)
            {
                var piece = new CroppedBitmap(source, new Int32Rect(xs[col], ys[row], xs[col + 1] - xs[col], ys[row + 1] - ys[row]));
                piece.Freeze();
                var image = new Image { Source = piece, Width = (dx[col + 1] - dx[col]) * scale, Height = (dy[row + 1] - dy[row]) * scale,
                    Stretch = Stretch.Fill, SnapsToDevicePixels = true };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                Canvas.SetLeft(image, (x + dx[col]) * scale);
                Canvas.SetTop(image, (y + dy[row]) * scale);
                CompositeCanvas.Children.Add(image);
            }
        AddLabel(label, x + b, y + 1, width - 2 * b, height - 2, scale, 6, false);
    }

    private void AddSolid(int x, int y, int width, int height, int scale, Color color)
    {
        var rect = new Rectangle { Width = width * scale, Height = height * scale, Fill = new SolidColorBrush(color), IsHitTestVisible = false };
        Canvas.SetLeft(rect, x * scale);
        Canvas.SetTop(rect, y * scale);
        CompositeCanvas.Children.Add(rect);
    }

    private void AddLabel(string text, int x, int y, int width, int height, int scale, int fontSize, bool bold, Color? color = null)
    {
        var label = new TextBlock { Text = text, Width = width * scale, Height = height * scale, FontSize = fontSize * scale,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = color is { } c ? new SolidColorBrush(c) : new SolidColorBrush(Color.FromRgb(48, 50, 58)),
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Canvas.SetLeft(label, x * scale);
        Canvas.SetTop(label, y * scale);
        CompositeCanvas.Children.Add(label);
    }
}
