using System;
using System.Collections.Generic;
using System.Linq;

namespace UnknownInventoryTerminal.Core
{
    /// <summary>Extensible search. Call on the Unity main thread, including registration and disposal.</summary>
    public sealed class SearchRegistry
    {
        private readonly List<Registration> _matchers = new List<Registration>();
        private readonly Action<Exception>? _onFailure;

        /// <summary>Creates an independent search registry.</summary>
        /// <param name="onFailure">Optional diagnostic sink, called once when a failing matcher is disabled.</param>
        public SearchRegistry(Action<Exception>? onFailure = null) { _onFailure = onFailure; }

        /// <summary>Adds an OR matcher for each query term, such as pinyin matching of the display name.</summary>
        /// <param name="matcher">Returns true when the item matches the supplied term.</param>
        /// <returns>A registration token; dispose it to unregister when the caller unloads.</returns>
        public IDisposable RegisterMatcher(Func<InventoryEntry, string, bool> matcher)
        {
            var registration = new Registration(this, matcher ?? throw new ArgumentNullException(nameof(matcher)));
            _matchers.Add(registration);
            return registration;
        }

        /// <summary>Tests name, exact ID text, container path and optional providers using AND between terms.</summary>
        /// <param name="entry">Item projection to test.</param>
        /// <param name="query">Case-insensitive whitespace-delimited terms; empty matches all.</param>
        /// <returns>True if every term matches at least one built-in field or external matcher.</returns>
        public bool Matches(InventoryEntry entry, string? query)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var terms = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                if (Contains(entry.DisplayName, term) || Contains(entry.TypeId, term) || Contains(entry.ContainerName, term)) continue;
                bool matched = false;
                foreach (var provider in _matchers.ToArray())
                {
                    if (provider.Disabled) continue;
                    try { if (provider.Matcher(entry, term)) { matched = true; break; } }
                    catch (Exception ex)
                    {
                        provider.Disabled = true;
                        _onFailure?.Invoke(ex);
                    }
                }
                if (!matched) return false;
            }
            return true;
        }

        private static bool Contains(string value, string term) => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        private sealed class Registration : IDisposable
        {
            internal readonly Func<InventoryEntry, string, bool> Matcher;
            internal bool Disabled;
            private readonly SearchRegistry _owner;
            internal Registration(SearchRegistry owner, Func<InventoryEntry, string, bool> matcher) { _owner = owner; Matcher = matcher; }
            public void Dispose() { Disabled = true; _owner._matchers.Remove(this); }
        }
    }

    /// <summary>Public search integration point, available without referencing Unity or the plugin assembly.</summary>
    public static class SearchApi
    {
        private static string _query = "";
        /// <summary>Registry used by the terminal. Register additional matching rules on the Unity main thread.</summary>
        public static SearchRegistry Matchers { get; } = new SearchRegistry();
        /// <summary>Gets or sets the terminal query. Set from the Unity main thread; null becomes empty.</summary>
        public static string Query { get => _query; set => _query = value ?? ""; }
    }
}
