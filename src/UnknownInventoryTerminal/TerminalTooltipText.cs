using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnknownInventoryTerminal
{
    // NEI's TMP-to-IMGUI color/tag rules, with sprite tags kept as explicit draw tokens.
    internal static class TerminalTooltipText
    {
        internal const string TitleColor = "F4F2FB", BodyColor = "BDB4E1", GroupColor = "C3C6D8", ShadowColor = "191827";
        private static readonly Regex Tags = new Regex(@"<(?<tag>/?[a-z]+|#[0-9a-f]+)(?=[\s=/>])(?<args>[^<>]*)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Hex = new Regex(@"\A[0-9a-f]+\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SpriteIndex = new Regex(@"(?:^|\s)index\s*=\s*[""']?(?<value>[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SpriteName = new Regex(@"(?:^|\s)name\s*=\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)'|(?<value>[^\s/]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SpriteTint = new Regex(@"(?:^|\s)tint\s*=\s*[""']?1(?=[""'\s/]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> GameOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "sprite", "size", "u", "s", "align", "font", "voffset", "link" };

        internal readonly struct Run
        {
            internal string Text { get; }
            internal string Color { get; }
            internal bool Bold { get; }
            internal bool Italic { get; }
            internal int Sprite { get; }
            internal string? SpriteName { get; }
            internal bool IsSprite => Sprite >= 0 || SpriteName != null;
            internal bool Tint { get; }

            internal Run(string text, string color, bool bold = false, bool italic = false,
                int sprite = -1, bool tint = false, string? spriteName = null)
            { Text = text; Color = color; Bold = bold; Italic = italic; Sprite = sprite; Tint = tint; SpriteName = spriteName; }

            internal Run WithText(string text) => new Run(text, Color, Bold, Italic);
        }

        internal static IReadOnlyList<Run> Parse(string text, string baseRgb, bool rich = true)
        {
            var runs = new List<Run>();
            var buffer = new StringBuilder();
            var colors = new Stack<string>();
            var baseColor = baseRgb + "FF";
            var color = baseColor;
            var bold = 0;
            var italic = 0;
            var position = 0;
            if (rich)
            {
                foreach (Match match in Tags.Matches(text))
                {
                    buffer.Append(text, position, match.Index - position);
                    var tag = match.Groups["tag"].Value.ToLowerInvariant();
                    var args = match.Groups["args"].Value.Trim();
                    var value = args.StartsWith("=", StringComparison.Ordinal) ? args.Substring(1).Trim().Trim('"', '\'') : "";
                    if (tag == "/color" && args.Length == 0)
                    {
                        Flush();
                        if (colors.Count > 0) colors.Pop();
                        color = colors.Count > 0 ? colors.Peek() : baseColor;
                    }
                    else if (tag == "alpha" && value.Length == 3 && value[0] == '#' && Hex.IsMatch(value.Substring(1)))
                    {
                        Flush();
                        color = color.Substring(0, 6) + value.Substring(1).ToUpperInvariant();
                    }
                    else if ((tag == "color" || tag[0] == '#') && ParseColor(tag == "color" ? value : tag) is { } rgba)
                    {
                        Flush();
                        colors.Push(rgba);
                        color = rgba;
                    }
                    else if (tag == "br" && args is "" or "/") buffer.Append('\n');
                    else if ((tag is "b" or "/b" or "i" or "/i") && args.Length == 0)
                    {
                        Flush();
                        if (tag is "b" or "/b") bold = Math.Max(0, bold + (tag == "b" ? 1 : -1));
                        else italic = Math.Max(0, italic + (tag == "i" ? 1 : -1));
                    }
                    else if (tag == "sprite")
                    {
                        var index = SpriteIndex.Match(args).Groups["value"].Value;
                        if (index.Length == 0) index = value; // TMP also accepts <sprite=3>.
                        var name = SpriteName.Match(args).Groups["value"].Value;
                        var hasIndex = int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var sprite);
                        if (name.Length > 0 || hasIndex)
                        {
                            Flush();
                            runs.Add(new Run("", color, sprite: hasIndex ? sprite : -1, tint: SpriteTint.IsMatch(args),
                                spriteName: name.Length > 0 ? name : null));
                        }
                    }
                    else if (!GameOnly.Contains(tag.TrimStart('/'))) buffer.Append(match.Value);
                    position = match.Index + match.Length;
                }
            }
            buffer.Append(text, position, text.Length - position);
            Flush();
            return runs;

            void Flush()
            {
                if (buffer.Length == 0) return;
                runs.Add(new Run(buffer.ToString(), color, bold > 0, italic > 0));
                buffer.Clear();
            }
        }

        // Only icon-bearing lines need manual wrap. Keep ordinary words intact and allow CJK breaks.
        internal static IEnumerable<string> WrapParts(string text)
        {
            var elements = StringInfo.GetTextElementEnumerator(text);
            var start = 0;
            var currentKind = -1;
            while (elements.MoveNext())
            {
                var index = elements.ElementIndex;
                var scalar = char.IsSurrogatePair(text, index) ? char.ConvertToUtf32(text, index) : text[index];
                var kind = char.IsWhiteSpace(text, index) ? 1 : scalar is >= 0x2e80 and <= 0x9fff or
                    >= 0xac00 and <= 0xd7af or >= 0xf900 and <= 0xfaff or >= 0x20000 and <= 0x323af ? 2 : 0;
                if (index > start && (kind != currentKind || kind == 2))
                {
                    yield return text.Substring(start, index - start);
                    start = index;
                }
                currentKind = kind;
            }
            if (start < text.Length) yield return text.Substring(start);
        }

        // Runs have balanced color/emphasis in both passes; sprites are painted separately from IMGUI text.
        internal static string Markup(Run run, bool shadow)
        {
            var color = shadow ? ShadowColor + run.Color.Substring(6) : run.Color;
            return "<color=#" + color + ">" + (run.Bold ? "<b>" : "") + (run.Italic ? "<i>" : "") + run.Text +
                (run.Italic ? "</i>" : "") + (run.Bold ? "</b>" : "") + "</color>";
        }

        private static string? ParseColor(string value)
        {
            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = value.Substring(1).ToUpperInvariant();
                if (hex.Length is not (3 or 4 or 6 or 8) || !Hex.IsMatch(hex)) return null;
                if (hex.Length <= 4)
                {
                    var expanded = new StringBuilder(hex.Length * 2);
                    foreach (char c in hex) expanded.Append(c, 2);
                    hex = expanded.ToString();
                }
                return hex.Length == 6 ? hex + "FF" : hex;
            }
            return value.ToLowerInvariant() switch
            {
                "red" => "FF0000FF", "lightblue" => "ADD8E6FF", "blue" => "0000FFFF", "grey" => "808080FF",
                "black" => "000000FF", "green" => "00FF00FF", "white" => "FFFFFFFF", "orange" => "FFA500FF",
                "purple" => "A020F0FF", "yellow" => "FFFF00FF", _ => null
            };
        }
    }
}
