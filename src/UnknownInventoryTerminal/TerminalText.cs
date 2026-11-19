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
        internal const string English = "en";
        internal const string Chinese = "zh-CN";
        private static IReadOnlyDictionary<string, string> _fallback = new Dictionary<string, string>();
        private static IReadOnlyDictionary<string, string> _english = new Dictionary<string, string>();
        private static IReadOnlyDictionary<string, string> _chinese = new Dictionary<string, string>();
        internal static string Language { get; private set; } = English;

        internal static void Initialize(string pluginPath, Action<string> warn)
        {
            var assembly = typeof(TerminalText).Assembly;
            using var fallback = assembly.GetManifestResourceStream("UnknownInventoryTerminal.Lang.en.json")!;
            using var reader = new StreamReader(fallback);
            _fallback = Parse(reader.ReadToEnd());
            var directory = Path.Combine(Path.GetDirectoryName(pluginPath) ?? "", "Lang");
            var englishOverrides = Load(Path.Combine(directory, English + ".json"), warn, optional: true);
            var english = new Dictionary<string, string>(_fallback, StringComparer.Ordinal);
            foreach (var entry in englishOverrides) english[entry.Key] = entry.Value;
            _english = english;
            _chinese = Load(Path.Combine(directory, Chinese + ".json"), warn, optional: false);
        }

        internal static void Select(string? language) => Language =
            string.Equals(language, Chinese, StringComparison.OrdinalIgnoreCase) ? Chinese : English;

        internal static string Get(string key, string? fallback = null) =>
            (Language == Chinese && _chinese.TryGetValue(key, out var translated)) ? translated :
            _english.TryGetValue(key, out var english) ? english : fallback ?? key;

        internal static string Format(string key, params object[] args)
        {
            try { return string.Format(CultureInfo.InvariantCulture, Get(key), args); }
            catch (FormatException)
            {
                // An invalid user-edited format must not break a terminal refresh.
                return _fallback.TryGetValue(key, out var original)
                    ? string.Format(CultureInfo.InvariantCulture, original, args) : key;
            }
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
