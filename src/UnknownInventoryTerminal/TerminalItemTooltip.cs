using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    // The item tooltip is painted in one IMGUI pass so the NEI font can sit above our own frame.
    internal sealed class TerminalItemTooltip
    {
        private readonly ManualLogSource _log;
        private readonly Ae2TerminalSkin _skin;
        private readonly GUIContent _content = new GUIContent();
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Icon> _icons = new List<Icon>();
        private string _title = "", _body = "", _group = "";
        private Item? _lastFailure;
        private Vector2 _pointer;
        private int _scale, _screenWidth, _screenHeight;
        private float _width, _height, _textScale, _padding;
        private bool _visible, _layoutDirty = true;
        private TMP_SpriteAsset? _titleSprites, _bodySprites;
        private GUIStyle? _titleStyle, _bodyStyle, _groupStyle;

        private readonly struct Label(Rect rect, string rich, string shadow, GUIStyle style)
        {
            internal readonly Rect Bounds = rect;
            internal readonly string Rich = rich, Shadow = shadow;
            internal readonly GUIStyle Style = style;
        }

        private readonly struct Icon(Rect rect, Texture sheet, Rect uv, Color32 tint)
        {
            internal readonly Rect Bounds = rect, UV = uv;
            internal readonly Texture Sheet = sheet;
            internal readonly Color32 Tint = tint;
        }

        internal TerminalItemTooltip(ManualLogSource log, Ae2TerminalSkin skin) { _log = log; _skin = skin; }

        internal void Hide() => _visible = false;

        internal void Show(Item item, int count, Vector2 pointer, int scale)
        {
            try
            {
                var (title, body) = TerminalItemTooltipContent.Read(item);
                if (string.IsNullOrWhiteSpace(title)) { Hide(); return; }
                var group = count > 1 ? TerminalText.Format("inventory.tooltip.group", count) : "";
                body = (body ?? "").Trim(); // NEI trims the native formatter's trailing line breaks.
                if (_title != title || _body != body || _group != group || _scale != scale) _layoutDirty = true;
                _title = title; _body = body; _group = group; _scale = scale;
                _pointer = new Vector2(pointer.x, Screen.height - pointer.y);
                _visible = true;
                _lastFailure = null;
            }
            catch (Exception ex)
            {
                Hide();
                if (_lastFailure != item) _log.LogWarning("Could not read item tooltip for " + item.id + ": " + ex.Message);
                _lastFailure = item;
            }
        }

        internal void Draw()
        {
            if (!_visible || Event.current.type != EventType.Repaint) return;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var margin = 4 * _scale;
            var titleSprites = SpriteAsset(title: true);
            var bodySprites = SpriteAsset(title: false);
            if (_layoutDirty || _screenWidth != screenWidth || _screenHeight != screenHeight ||
                _titleSprites != titleSprites || _bodySprites != bodySprites)
            {
                var availableWidth = screenWidth - 2 * margin;
                var availableHeight = screenHeight - 2 * margin;
                var desiredScale = TerminalItemTooltipLayout.ReadableScale(screenWidth, screenHeight);
                _padding = Mathf.Ceil(TerminalItemTooltipLayout.Padding * desiredScale);
                if (availableWidth <= 2 * _padding || availableHeight <= 2 * _padding) return;
                _titleSprites = titleSprites;
                _bodySprites = bodySprites;
                Layout(availableWidth, availableHeight, desiredScale);
                _screenWidth = screenWidth;
                _screenHeight = screenHeight;
                _layoutDirty = false;
            }

            var (x, y) = TerminalItemTooltipLayout.Place(_pointer.x, _pointer.y, _width, _height,
                screenWidth, screenHeight, 5 * _scale, margin);
            var origin = new Vector2(Mathf.Round(x), Mathf.Round(y));
            var color = GUI.color;
            var contentColor = GUI.contentColor;
            var matrix = GUI.matrix;
            var depth = GUI.depth;
            try
            {
                // NEI itself draws at depth 100. Keep our tooltip together and above background inventory.
                GUI.depth = 99;
                GUI.color = GUI.contentColor = Color.white;
                _skin.DrawPanel(new Rect(origin.x, origin.y, _width, _height), Ae2Image.Tooltip, _scale,
                    new Color32(154, 159, 180, 255));
                origin += Vector2.one * _padding;
                GUIUtility.ScaleAroundPivot(Vector2.one * _textScale, origin);
                foreach (var label in _labels) DrawLabel(label, origin + Vector2.one, shadow: true);
                foreach (var label in _labels) DrawLabel(label, origin, shadow: false);
                foreach (var icon in _icons)
                {
                    var rect = icon.Bounds;
                    rect.position += origin;
                    GUI.color = icon.Tint;
                    GUI.DrawTextureWithTexCoords(rect, icon.Sheet, icon.UV);
                }
            }
            finally { GUI.depth = depth; GUI.matrix = matrix; GUI.color = color; GUI.contentColor = contentColor; }
        }

        private void Layout(float availableWidth, float availableHeight, float desiredScale)
        {
            // NEI uses the active IMGUI skin's default font, not a separate font asset or TMP SDF material.
            _titleStyle ??= TextStyle(TerminalItemTooltipLayout.TitleSize, TerminalTooltipText.TitleColor);
            _bodyStyle ??= TextStyle(TerminalItemTooltipLayout.DetailSize, TerminalTooltipText.BodyColor);
            _groupStyle ??= TextStyle(TerminalItemTooltipLayout.DetailSize, TerminalTooltipText.GroupColor, rich: false);
            var title = TerminalTooltipText.Parse(_title, TerminalTooltipText.TitleColor);
            var body = TerminalTooltipText.Parse(_body, TerminalTooltipText.BodyColor);
            var group = TerminalTooltipText.Parse(_group, TerminalTooltipText.GroupColor, rich: false);
            _labels.Clear();
            _icons.Clear();
            var wanted = Mathf.Max(Measure(title, _titleStyle, _titleSprites, float.PositiveInfinity, 0, false),
                Mathf.Max(Measure(body, _bodyStyle, _bodySprites, float.PositiveInfinity, 0, false),
                    Measure(group, _groupStyle, null, float.PositiveInfinity, 0, false)));
            var width = TerminalItemTooltipLayout.TextWidth(wanted, (availableWidth - 2 * _padding) / desiredScale);
            var height = Measure(title, _titleStyle, _titleSprites, width, 0, true);
            if (body.Count > 0)
            {
                height += TerminalItemTooltipLayout.DetailGap;
                height += Measure(body, _bodyStyle, _bodySprites, width, height, true);
            }
            if (group.Count > 0)
            {
                height += TerminalItemTooltipLayout.GroupGap;
                height += Measure(group, _groupStyle, null, width, height, true);
            }
            _textScale = TerminalItemTooltipLayout.FitScale(desiredScale, availableHeight - 2 * _padding, height);
            _width = Mathf.Ceil(width * _textScale) + 2 * _padding;
            _height = Mathf.Ceil(height * _textScale) + 2 * _padding;
        }

        // Icon-free paragraphs go through Unity's own wrapping, just like NEI. On lines with sprites,
        // wrap explicit font-measured runs instead of relying on rich-text cursor indices in the IMGUI backend.
        private float Measure(IReadOnlyList<TerminalTooltipText.Run> runs, GUIStyle style, TMP_SpriteAsset? sprites,
            float width, float top, bool place)
        {
            if (runs.Count == 0) return 0;
            var start = top;
            var widest = 0f;
            var line = new List<TerminalTooltipText.Run>();
            foreach (var run in runs)
            {
                if (run.IsSprite) { line.Add(run); continue; }
                var text = run.Text.Replace("\r\n", "\n").Replace('\r', '\n');
                var offset = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] != '\n') continue;
                    if (i > offset) line.Add(run.WithText(text.Substring(offset, i - offset)));
                    FinishLine();
                    offset = i + 1;
                }
                if (offset < text.Length) line.Add(run.WithText(text.Substring(offset)));
            }
            FinishLine();
            return place ? top - start : widest;

            void FinishLine()
            {
                float rowHeight = Mathf.Ceil(style.lineHeight);
                if (!line.Exists(run => run.IsSprite))
                {
                    var (rich, shadow) = style.richText ? Combined(line) : (Literal(line), Literal(line));
                    _content.text = rich;
                    if (place)
                    {
                        var height = Mathf.Max(rowHeight, Mathf.Ceil(style.CalcHeight(_content, width)));
                        if (rich.Length > 0) _labels.Add(new Label(new Rect(0, top, width, height), rich, shadow, style));
                        top += height;
                    }
                    else widest = Mathf.Max(widest, style.CalcSize(_content).x);
                }
                else if (place) top = PlaceIconLine(line, style, sprites, width, top, rowHeight);
                else widest = Mathf.Max(widest, IconLineWidth(line, style, sprites, rowHeight));
                line.Clear();
            }
        }

        private float IconLineWidth(List<TerminalTooltipText.Run> line, GUIStyle style, TMP_SpriteAsset? sprites, float rowHeight)
        {
            var width = 0f;
            foreach (var run in line)
            {
                if (!run.IsSprite) width += RunWidth(run, style);
                else if (TryIcon(sprites, run, rowHeight, out var icon)) width += icon.Bounds.width + 2f;
            }
            return width;
        }

        private float PlaceIconLine(List<TerminalTooltipText.Run> line, GUIStyle style, TMP_SpriteAsset? sprites,
            float width, float y, float rowHeight)
        {
            var x = 0f;
            var spanStart = 0f;
            var span = new List<TerminalTooltipText.Run>();
            foreach (var run in line)
            {
                if (run.IsSprite)
                {
                    if (!TryIcon(sprites, run, rowHeight, out var icon)) continue;
                    FlushSpan();
                    var rect = icon.Bounds;
                    if (rect.width > width) { rect.height *= width / rect.width; rect.width = width; }
                    if (x > 0f && x + rect.width > width) Next();
                    rect.position = new Vector2(x, y + (rowHeight - rect.height) / 2f);
                    _icons.Add(new Icon(rect, icon.Sheet, icon.UV, icon.Tint));
                    x += rect.width + 2f;
                    continue;
                }

                if (Append(run)) continue;
                // Only split a run when it will not fit as a whole; preserve font shaping within each span.
                foreach (var word in TerminalTooltipText.WrapParts(run.Text))
                {
                    var chunk = run.WithText(word);
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        if (x > 0f) Append(chunk);
                        continue;
                    }
                    if (Append(chunk)) continue;
                    if (x > 0f) Next();
                    if (Append(chunk)) continue;
                    var elements = StringInfo.GetTextElementEnumerator(word);
                    while (elements.MoveNext())
                    {
                        var glyph = run.WithText(elements.GetTextElement());
                        if (Append(glyph)) continue;
                        if (x > 0f) Next();
                        if (!Append(glyph)) { spanStart = 0; span.Add(glyph); x = width; }
                    }
                }
            }
            FlushSpan();
            return y + rowHeight;

            bool Append(TerminalTooltipText.Run run)
            {
                if (span.Count == 0) spanStart = x;
                span.Add(run);
                _content.text = Combined(span).Rich;
                var end = spanStart + style.CalcSize(_content).x;
                if (end <= width) { x = end; return true; }
                span.RemoveAt(span.Count - 1);
                return false;
            }

            void FlushSpan()
            {
                if (span.Count == 0) return;
                var (rich, shadow) = Combined(span);
                _labels.Add(new Label(new Rect(spanStart, y, Mathf.Min(width - spanStart + 1f, Mathf.Ceil(x - spanStart) + 1f), rowHeight), rich, shadow, style));
                span.Clear();
            }

            void Next() { FlushSpan(); x = 0f; y += rowHeight; }
        }

        private float RunWidth(TerminalTooltipText.Run run, GUIStyle style)
        {
            _content.text = TerminalTooltipText.Markup(run, false);
            return style.CalcSize(_content).x;
        }

        private static (string Rich, string Shadow) Combined(List<TerminalTooltipText.Run> line)
        {
            var rich = new StringBuilder();
            var shadow = new StringBuilder();
            foreach (var run in line)
            {
                rich.Append(TerminalTooltipText.Markup(run, false));
                shadow.Append(TerminalTooltipText.Markup(run, true));
            }
            return (rich.ToString(), shadow.ToString());
        }

        private static string Literal(List<TerminalTooltipText.Run> line) => line.Count == 0 ? "" : line[0].Text;

        private void DrawLabel(Label label, Vector2 origin, bool shadow)
        {
            var rect = label.Bounds;
            rect.position += origin;
            _content.text = shadow ? label.Shadow : label.Rich;
            var normal = label.Style.normal;
            var foreground = normal.textColor;
            if (shadow) normal.textColor = new Color32(25, 24, 39, 255);
            try { label.Style.Draw(rect, _content, false, false, false, false); }
            finally { if (shadow) normal.textColor = foreground; }
        }

        private static GUIStyle TextStyle(int size, string rgb, bool rich = true)
        {
            var style = new GUIStyle
            {
                fontSize = size, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip,
                wordWrap = true, richText = rich
            };
            if (ColorUtility.TryParseHtmlString("#" + rgb, out var color)) style.normal.textColor = color;
            return style;
        }

        private static TMP_SpriteAsset? SpriteAsset(bool title)
        {
            var native = GlobalDark.main;
            var label = native ? (title ? native.tooltipNameObj : native.tooltipDescObj) : null;
            return label && label!.spriteAsset ? label.spriteAsset : TMP_Settings.defaultSpriteAsset;
        }

        private static bool TryIcon(TMP_SpriteAsset? asset, TerminalTooltipText.Run run, float rowHeight, out Icon icon)
        {
            icon = default;
            if (!asset) return false;
            var index = run.Sprite;
            if (run.SpriteName is { } name)
                asset = TMP_SpriteAsset.SearchForSpriteByHashCode(asset, TMP_TextUtilities.GetSimpleHashCode(name), true, out index);
            if (!asset || !asset!.spriteSheet || index < 0 || index >= asset.spriteCharacterTable.Count) return false;
            var glyph = asset.spriteCharacterTable[index]?.glyph;
            if (glyph == null || glyph.glyphRect.width <= 0 || glyph.glyphRect.height <= 0) return false;
            var rect = glyph.glyphRect;
            var sheet = asset.spriteSheet;
            var height = Mathf.Min(rowHeight - 2f, 11f);
            var width = height * rect.width / rect.height;
            var uv = new Rect((float)rect.x / sheet.width, (float)rect.y / sheet.height,
                (float)rect.width / sheet.width, (float)rect.height / sheet.height);
            ColorUtility.TryParseHtmlString("#" + run.Color, out var color);
            if (!run.Tint) color = new Color(1, 1, 1, color.a);
            icon = new Icon(new Rect(0, 0, width, height), sheet, uv, color);
            return true;
        }
    }
}
