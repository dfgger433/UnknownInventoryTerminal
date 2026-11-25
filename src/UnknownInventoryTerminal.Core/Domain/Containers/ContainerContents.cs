// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;

namespace UnknownInventoryTerminal.Core
{
    /// <summary>Walks the contents of known inventory containers without indexing their root shells.</summary>
    public static class ContainerContents
    {
        /// <summary>Visits each item reached through a verified direct container edge once.</summary>
        /// <typeparam name="TItem">Item reference type with stable instance equality.</typeparam>
        /// <typeparam name="TContainer">Container reference type with stable instance equality.</typeparam>
        /// <param name="roots">The owner's root containers.</param>
        /// <param name="getChildren">Returns the candidate direct items under a container.</param>
        /// <param name="getParent">Returns an item's actual direct container, or null.</param>
        /// <param name="getContainer">Returns the container represented by an item, or null.</param>
        /// <returns>Verified items with their immediate containers.</returns>
        public static IEnumerable<(TItem Item, TContainer Container)> Enumerate<TItem, TContainer>(
            IEnumerable<TContainer> roots, Func<TContainer, IEnumerable<TItem>> getChildren,
            Func<TItem, TContainer?> getParent, Func<TItem, TContainer?> getContainer)
            where TItem : class where TContainer : class
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (getChildren == null) throw new ArgumentNullException(nameof(getChildren));
            if (getParent == null) throw new ArgumentNullException(nameof(getParent));
            if (getContainer == null) throw new ArgumentNullException(nameof(getContainer));

            var visitedContainers = new HashSet<TContainer>(roots);
            var pending = new Stack<TContainer>(visitedContainers);
            var seenItems = new HashSet<TItem>();
            while (pending.Count > 0)
            {
                var container = pending.Pop();
                foreach (var item in getChildren(container))
                {
                    var parent = getParent(item);
                    if (parent == null || !visitedContainers.Comparer.Equals(parent, container) || !seenItems.Add(item)) continue;
                    var nested = getContainer(item);
                    if (nested != null && visitedContainers.Add(nested)) pending.Push(nested);
                    yield return (item, container);
                }
            }
        }
    }
}
