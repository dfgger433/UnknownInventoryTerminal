// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnknownInventoryTerminal
{
    internal static class TerminalText
    {
        internal const string English = "en_us";
        internal const string Chinese = "zh_cn";
        private static IReadOnlyDictionary<string, string> _fallback = new Dictionary<string, string>();
        private static IReadOnlyDictionary<string, string> _english = new Dictionary<string, string>();
        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase);
        private static IReadOnlyList<string> _languages = new[] { English, Chinese };
        internal static IReadOnlyList<string> Languages => _languages;
        internal static string Language { get; private set; } = English;

        internal static void Initialize(string pluginPath, Action<string> warn)
        {
            var assembly = typeof(TerminalText).Assembly;
            using var fallback = assembly.GetManifestResourceStream("UnknownInventoryTerminal.Lang.en_us.json")!;
            using var reader = new StreamReader(fallback);
            _fallback = Parse(reader.ReadToEnd());
            var directory = Path.Combine(Path.GetDirectoryName(pluginPath) ?? "", "Lang");
            var englishOverrides = Load(Path.Combine(directory, English + ".json"), warn, optional: true);
            var english = new Dictionary<string, string>(_fallback, StringComparer.Ordinal);
            foreach (var entry in englishOverrides) english[entry.Key] = entry.Value;
            _english = english;
            Translations.Clear();
            Translations.Add(English, _english);
            Translations.Add(Chinese, Load(Path.Combine(directory, Chinese + ".json"), warn, optional: false));
            var languages = new List<string> { English, Chinese };
            try
            {
                if (Directory.Exists(directory))
                {
                    var files = Directory.GetFiles(directory, "*.json");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (var path in files)
                    {
                        var id = Path.GetFileNameWithoutExtension(path);
                        if (Translations.ContainsKey(id)) continue;
                        var messages = Load(path, warn, optional: false);
                        // A language file declares its own display name before it can appear in the menu.
                        if (!messages.ContainsKey("language.name")) continue;
                        Translations.Add(id, messages);
                        languages.Add(id);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { warn("Could not enumerate terminal languages: " + ex.Message); }
            _languages = languages.AsReadOnly();
            Select(Language);
        }

        internal static string ResolveLanguage(string? language)
        {
            foreach (var id in _languages)
                if (string.Equals(id, language, StringComparison.OrdinalIgnoreCase)) return id;
            if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) return English;
            if (string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase)) return Chinese;
            return English;
        }

        internal static void Select(string? language) => Language = ResolveLanguage(language);

        internal static string LanguageName(string id) => Translations.TryGetValue(id, out var messages) &&
            messages.TryGetValue("language.name", out var name) ? name : Get("settings.Language." + id, id);

        internal static string Get(string key, string? fallback = null) =>
            (Translations.TryGetValue(Language, out var messages) && messages.TryGetValue(key, out var translated)) ? translated :
            _english.TryGetValue(key, out var english) ? english : fallback ?? key;

        internal static string Format(string key, params object[] args)
        {
            var selected = Translations.TryGetValue(Language, out var messages) && messages.TryGetValue(key, out var text)
                ? text : null;
            var english = _english.TryGetValue(key, out var englishText) ? englishText : null;
            var builtin = _fallback.TryGetValue(key, out var builtinText) ? builtinText : null;
            if (TryFormat(selected, english ?? builtin, args, out var result) ||
                TryFormat(english, builtin, args, out result) ||
                TryFormat(builtin, null, args, out result))
                return result;
            return key;
        }

        private static bool TryFormat(string? format, string? requiredTemplate, object[] args, out string result)
        {
            result = "";
            if (format == null || !HasRequiredArguments(format, requiredTemplate, args.Length)) return false;
            try { result = string.Format(CultureInfo.InvariantCulture, format, args); return true; }
            catch (FormatException) { return false; }
        }

        private static bool HasRequiredArguments(string format, string? requiredTemplate, int count)
        {
            if (!TryArgumentSet(format, out var actual)) return false;
            foreach (var index in actual) if (index >= count) return false;
            if (requiredTemplate == null) return true;
            if (!TryArgumentSet(requiredTemplate, out var required)) return false;
            foreach (var index in required) if (!actual.Contains(index)) return false;
            return true;
        }

        private static bool TryArgumentSet(string format, out HashSet<int> arguments)
        {
            arguments = new HashSet<int>();
            for (var i = 0; i < format.Length; i++)
            {
                if (format[i] == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{') { i++; continue; }
                    var start = i + 1;
                    var end = start;
                    while (end < format.Length && char.IsDigit(format[end])) end++;
                    if (end == start || !int.TryParse(format.Substring(start, end - start), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var index)) return false;
                    if (end >= format.Length || (format[end] != ',' && format[end] != ':' && format[end] != '}')) return false;
                    if (format[end] == ',')
                    {
                        end++;
                        while (end < format.Length && format[end] == ' ') end++;
                        if (end < format.Length && (format[end] == '-' || format[end] == '+')) end++;
                        var digits = end;
                        while (end < format.Length && char.IsDigit(format[end])) end++;
                        if (end == digits || end >= format.Length || (format[end] != ':' && format[end] != '}')) return false;
                    }
                    if (format[end] == ':')
                    {
                        end++;
                        while (end < format.Length && format[end] != '}') end++;
                        if (end >= format.Length) return false;
                    }
                    if (format[end] == '}') { arguments.Add(index); i = end; }
                }
                else if (format[i] == '}')
                {
                    if (i + 1 < format.Length && format[i + 1] == '}') { i++; continue; }
                    return false;
                }
            }
            return true;
        }

        private static IReadOnlyDictionary<string, string> Load(string path, Action<string> warn, bool optional)
        {
            try { return Parse(File.ReadAllText(path)); }
            catch (FileNotFoundException) when (optional) { }
            catch (DirectoryNotFoundException) when (optional) { }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            { warn("Could not load terminal language file " + Path.GetFileName(path) + ": " + ex.Message); }
            return new Dictionary<string, string>();
        }

        internal static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var parsed = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (var entry in parsed.Properties())
                if (entry.Value.Type == JTokenType.String && entry.Value.Value<string>() is string value && !string.IsNullOrWhiteSpace(value))
                    result.Add(entry.Name, value);
            return result;
        }
    }
}
