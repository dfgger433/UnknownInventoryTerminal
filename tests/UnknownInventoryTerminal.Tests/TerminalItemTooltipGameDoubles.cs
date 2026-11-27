// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

// Minimal stand-ins for live items, inventory actions, tooltips and global liquid definitions.
internal sealed class Item
{
    internal ItemInfo Stats { get; init; } = new();
    internal string fullName { get; init; } = "Item";
    internal float totalWeight { get; init; }
    internal WaterContainerItem? Liquid { get; init; }
    internal Container? Container { get; init; }
    internal BatteryItem? battery { get; init; }
    internal Func<Item, (string, string)>? NativeTooltip { get; init; }

    internal T? GetComponent<T>() where T : class => Liquid as T ?? Container as T;

    internal bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : class
    {
        component = GetComponent<T>();
        return component != null;
    }
}

internal class ItemInfo
{
    internal string description { get; init; } = "Description";
    internal Recognition rec { get; init; } = new();
    internal HashSet<string> Tags { get; } = new();
    internal bool HasTag(string tag) => Tags.Contains(tag);
}

internal class BatteryInfo : ItemInfo
{
    internal float maxCharge { get; init; }
}

internal sealed class BatteryItem
{
    internal bool hasBattery { get; init; }
    internal float maxAllowedCharge { get; init; } = 50f;
    public static implicit operator bool(BatteryItem? battery) => battery != null;
}

internal sealed class Container;

internal sealed class Body
{
    internal Func<Item, Item, bool> CanCombineItems { get; init; } = (_, _) => false;
    internal bool CanCombine(Item target, Item source) => CanCombineItems(target, source);
}

internal sealed class Recognition
{
    internal bool recognizable { get; init; } = true;
}

internal sealed class WaterContainerItem
{
    internal float Capacity { get; init; }
    internal List<LiquidStack?> stack { get; } = new();
}

internal sealed class LiquidStack
{
    internal string? liquidId { get; init; }
    internal float amount { get; set; }
}

internal static class Liquids
{
    internal sealed class Definition
    {
        internal bool localeFromItem { get; init; }
        internal string localeName { get; init; } = "liquid";
    }

    internal static System.Collections.Concurrent.ConcurrentDictionary<string, Definition> Registry { get; } = new();
}

internal static class Locale
{
    internal static string GetOther(string key) => key;
    internal static string GetItem(string id) => "item:" + id;
}

internal static class PlayerCamera
{
    internal static (string, string) ItemHoverDescription(Item item) =>
        (item.NativeTooltip ?? throw new InvalidOperationException("Native tooltip should not be called for this item."))(item);
}

namespace UnityEngine
{
    internal static class Mathf
    {
        internal static int RoundToInt(float value) => (int)Math.Round(value);
    }
}
