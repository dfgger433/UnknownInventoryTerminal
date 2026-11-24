using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    /// <summary>Controls the local player's terminal window. Call only on the Unity main thread.</summary>
    public static class TerminalApi
    {
        /// <summary>Gets whether the terminal window is currently visible.</summary>
        public static bool IsOpen => Plugin.IsVisible;

        /// <summary>Attempts to open the terminal through its normal input and session checks.</summary>
        /// <returns>True if the terminal opened; otherwise false.</returns>
        public static bool TryOpen() => Plugin.Instance?.TryOpenTerminal() == true;

        /// <summary>Attempts to close the terminal through its normal session cleanup.</summary>
        /// <returns>True if the terminal closed; otherwise false.</returns>
        public static bool TryClose() => Plugin.Instance?.TryCloseTerminal() == true;

        /// <summary>Gets the current bounds of the terminal window.</summary>
        /// <param name="screenBounds">On success, Unity screen pixels measured from the bottom left; otherwise the default rectangle.</param>
        /// <returns>True if the terminal bounds are available.</returns>
        public static bool TryGetBounds(out Rect screenBounds)
        {
            if (Plugin.Instance is { } plugin) return plugin.TryGetTerminalBounds(out screenBounds);
            screenBounds = default;
            return false;
        }

        /// <summary>Gets the clipped extension root of the open native terminal, or null when unavailable.</summary>
        /// <remarks>Only add and remove your own children. Do not modify or destroy the root.</remarks>
        public static RectTransform? ExtensionRoot => Plugin.Instance?.ExtensionRoot;
    }

    /// <summary>Reads the terminal's local inventory. Call only on the Unity main thread.</summary>
    public static class TerminalItemsApi
    {
        /// <summary>Gets the latest cached storage snapshot and its latest filtered and grouped projection.</summary>
        /// <param name="entries">Unfiltered container contents; empty if the snapshot is unavailable.</param>
        /// <param name="filteredRows">Filtered groups retaining all matching member entries; empty if the snapshot is unavailable.</param>
        /// <returns>True if an open local terminal session supplies a snapshot. A valid snapshot may be empty.</returns>
        public static bool TryGetSnapshot(out IReadOnlyList<InventoryEntry> entries, out IReadOnlyList<InventoryRow> filteredRows)
        {
            if (Plugin.Instance is { } plugin) return plugin.TryGetTerminalSnapshot(out entries, out filteredRows);
            entries = Array.Empty<InventoryEntry>();
            filteredRows = Array.Empty<InventoryRow>();
            return false;
        }

        /// <summary>Resolves a currently indexed storage entry to its real, locally owned item.</summary>
        /// <param name="instanceKey">An instance key from the current open terminal snapshot, not a type ID or a persistent ID.</param>
        /// <param name="item">The live item on success; null on failure. Read only; do not retain it or mutate it through this API.</param>
        /// <returns>True if the indexed item is still valid and local.</returns>
        public static bool TryResolve(string instanceKey, [NotNullWhen(true)] out Item? item)
        {
            if (Plugin.Instance is { } plugin && plugin.TryResolveTerminalItem(instanceKey, out var resolved) && resolved is not null)
            {
                item = resolved;
                return true;
            }
            item = null;
            return false;
        }

        /// <summary>Gets unique live items held or worn by the local player in the open terminal session.</summary>
        /// <returns>A read-only list of real game items, or an empty list when none are available.</returns>
        /// <remarks>This list is not filtered by the terminal query and excludes items inside containers. Do not mutate the item references.</remarks>
        public static IReadOnlyList<Item> GetLoadout() => Plugin.Instance?.GetTerminalLoadout() ?? Array.Empty<Item>();
    }
}
