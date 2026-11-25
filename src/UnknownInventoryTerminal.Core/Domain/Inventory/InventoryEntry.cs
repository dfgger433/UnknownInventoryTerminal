// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;

namespace UnknownInventoryTerminal.Core
{
    /// <summary>A single real inventory instance. Aggregation never replaces this identity.</summary>
    public sealed class InventoryEntry
    {
        /// <summary>Creates an immutable projection of a native item.</summary>
        /// <param name="instanceKey">Unique identity within the current game process.</param>
        /// <param name="typeId">Case-sensitive game item ID.</param>
        /// <param name="displayName">Current localized item name.</param>
        /// <param name="containerKey">Unique parent container identity.</param>
        /// <param name="containerName">Human-readable parent path.</param>
        /// <param name="nativeItem">Real item reference; never a reconstructed item.</param>
        /// <param name="icon">Optional native sprite or texture.</param>
        public InventoryEntry(string instanceKey, string typeId, string displayName,
            string containerKey, string containerName, object nativeItem, object? icon = null)
        {
            if (string.IsNullOrEmpty(instanceKey)) throw new ArgumentException("Instance key is required.", nameof(instanceKey));
            InstanceKey = instanceKey;
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            DisplayName = string.IsNullOrEmpty(displayName) ? TypeId : displayName;
            ContainerKey = containerKey ?? throw new ArgumentNullException(nameof(containerKey));
            ContainerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
            NativeItem = nativeItem ?? throw new ArgumentNullException(nameof(nativeItem));
            Icon = icon;
        }
        /// <summary>Unique instance identity, independent of type ID.</summary>
        public string InstanceKey { get; }
        /// <summary>Native, case-sensitive item type ID.</summary>
        public string TypeId { get; }
        /// <summary>Localized display name.</summary>
        public string DisplayName { get; }
        /// <summary>Unique parent identity, so identical bags remain distinct.</summary>
        public string ContainerKey { get; }
        /// <summary>Display path of the containing bag, equipment, or hands.</summary>
        public string ContainerName { get; }
        /// <summary>Real game item. Consumers must validate ownership again before interaction.</summary>
        public object NativeItem { get; }
        /// <summary>Native sprite or texture; null when unavailable.</summary>
        public object? Icon { get; }
    }

    /// <summary>Display grouping mode. Neither mode mutates native inventories.</summary>
    public enum InventoryViewMode
    {
        /// <summary>Combine all instances with the same exact item type ID.</summary>
        Merged,
        /// <summary>Keep containers distinct and group types inside each container.</summary>
        ByContainer
    }

    /// <summary>A display row retains every underlying real instance.</summary>
    public sealed class InventoryRow
    {
        internal InventoryRow(IEnumerable<InventoryEntry> entries)
        {
            Instances = Array.AsReadOnly(entries.ToArray());
        }
        /// <summary>Instances represented by this row; choose one explicitly for interaction.</summary>
        public IReadOnlyList<InventoryEntry> Instances { get; }
        /// <summary>Number of real item objects, not fluid volume or ammunition loaded into a weapon.</summary>
        public int Count => Instances.Count;
        /// <summary>Representative entry for text and icon rendering.</summary>
        public InventoryEntry First => Instances[0];
    }

    /// <summary>Builds filtered, sorted display rows without changing the source snapshot.</summary>
    public static class InventoryProjection
    {
        /// <summary>Deduplicates instance identities, filters, then groups by exact type and parent identity.</summary>
        /// <param name="source">Snapshot of native item projections.</param>
        /// <param name="mode">Merged or by-container view.</param>
        /// <param name="search">Search matcher registry.</param>
        /// <param name="query">Search expression; whitespace separates AND terms and | separates alternatives.</param>
        /// <returns>Read-only deterministic display rows, preserving native instances.</returns>
        public static IReadOnlyList<InventoryRow> Build(IEnumerable<InventoryEntry> source,
            InventoryViewMode mode, SearchRegistry search, string? query)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (search == null) throw new ArgumentNullException(nameof(search));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var matches = search.Compile(query);
            var entries = source.Where(e => seen.Add(e.InstanceKey) && matches(e));
            return Array.AsReadOnly(entries.GroupBy(e => new { e.TypeId, Parent = mode == InventoryViewMode.ByContainer ? e.ContainerKey : "" })
                .Select(g => new InventoryRow(g.OrderBy(e => e.InstanceKey, StringComparer.Ordinal)))
                .OrderBy(r => mode == InventoryViewMode.ByContainer ? r.First.ContainerName : "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.First.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.First.TypeId, StringComparer.Ordinal)
                .ThenBy(r => r.First.ContainerKey, StringComparer.Ordinal).ToArray());
        }
    }
}
