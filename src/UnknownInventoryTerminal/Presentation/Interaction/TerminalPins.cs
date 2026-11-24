using System;
using System.Collections.Generic;

namespace UnknownInventoryTerminal
{
    // Pins refer to native objects for the current body, never item types or saved instance IDs.
    internal sealed class TerminalPins<TOwner, TItem>(int pageSize) where TOwner : class where TItem : class
    {
        private readonly List<TItem> _items = new();
        private TOwner? _owner;
        internal int Page { get; private set; }
        internal int PageCount => (_items.Count + pageSize - 1) / pageSize;
        internal bool Contains(TItem item) => IndexOf(item) >= 0;

        private int IndexOf(TItem item)
        {
            for (var i = 0; i < _items.Count; i++)
                if (ReferenceEquals(_items[i], item)) return i;
            return -1;
        }

        internal void SetOwner(TOwner owner)
        {
            if (ReferenceEquals(_owner, owner)) return;
            _owner = owner;
            _items.Clear();
            Page = 0;
        }

        internal void Toggle(TItem item)
        {
            var index = IndexOf(item);
            if (index >= 0)
            {
                _items.RemoveAt(index);
                ClampPage();
            }
            else
            {
                _items.Add(item);
                Page = PageCount - 1;
            }
        }

        internal void Prune(Predicate<TItem> keep)
        {
            _items.RemoveAll(item => !keep(item));
            ClampPage();
        }

        internal void MovePage(int direction)
        {
            if (PageCount > 1) Page = (Page + Math.Sign(direction) + PageCount) % PageCount;
        }

        internal IEnumerable<TItem> VisibleItems()
        {
            var end = Math.Min((Page + 1) * pageSize, _items.Count);
            for (var i = Page * pageSize; i < end; i++) yield return _items[i];
        }

        private void ClampPage() => Page = Math.Min(Page, Math.Max(0, PageCount - 1));
    }
}
