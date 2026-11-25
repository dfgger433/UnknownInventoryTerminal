// SPDX-License-Identifier: GPL-3.0-only

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

        /// <summary>Tests name, ID, container and optional providers. Use | for OR and whitespace for AND.</summary>
        /// <param name="entry">Item projection to test.</param>
        /// <param name="query">Case-insensitive terms. An empty query matches all. Prefix a term with * to search only IDs.</param>
        /// <returns>True if every term in at least one alternative matches.</returns>
        public bool Matches(InventoryEntry entry, string? query) => Compile(query)(entry);

        internal Func<InventoryEntry, bool> Compile(string? query)
        {
            var alternatives = (query ?? "").Split('|')
                .Select(part => part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Where(terms => terms.Length != 0).ToArray();
            return entry =>
            {
                if (entry == null) throw new ArgumentNullException(nameof(entry));
                return alternatives.Length == 0 || alternatives.Any(terms => terms.All(term => MatchesTerm(entry, term)));
            };
        }

        private bool MatchesTerm(InventoryEntry entry, string term)
        {
            if (term[0] == '*') return Contains(entry.TypeId, term.Substring(1));
            if (Contains(entry.DisplayName, term) || Contains(entry.TypeId, term) || Contains(entry.ContainerName, term)) return true;
            foreach (var provider in _matchers.ToArray())
            {
                if (provider.Disabled) continue;
                try { if (provider.Matcher(entry, term)) return true; }
                catch (Exception ex)
                {
                    provider.Disabled = true;
                    _onFailure?.Invoke(ex);
                }
            }
            return false;
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
