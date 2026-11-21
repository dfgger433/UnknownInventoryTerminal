using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

// Minimal Unity/game/CCL surface consumed by the linked formatter. Tests do not load game binaries.
internal sealed class Item : UnityEngine.Object
{
    private readonly Dictionary<Type, UnityEngine.Object> _components = new();
    internal string id = "";
    internal ItemInfo Stats = new();
    internal string label = "";
    internal float condition = 1f;
    internal float totalWeight;
    internal bool favourited;
    internal bool isWet;
    internal Container container => TryGetComponent<Container>(out var value) ? value : null!;
    internal TimeSpan DecayRemaining;
    internal TimeSpan TimeUntilDecayed() => DecayRemaining;
    internal void Attach<T>(T component) where T : UnityEngine.Object => _components[typeof(T)] = component;

    internal bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : UnityEngine.Object
    {
        component = _components.TryGetValue(typeof(T), out var result) ? (T)result : null;
        return component != null;
    }
}

internal sealed class ItemInfo
{
    [Flags]
    internal enum DecayType : byte
    {
        NoDecayWithoutContainerItem = 1,
        NoDecayWhenStill = 2,
        NoDecayWhenNotWorn = 4
    }

    internal string fullName = "";
    internal string description = "";
    internal string category = "";
    internal Recognition rec = new();
    internal float rotSpeed;
    internal byte decayInfo;
    internal bool onlyHoldInHands;
    internal bool wearable;
    internal string wearSlotId = "";
    internal float wearableArmor;
    internal float wearableIsolation;
    internal bool usable;
    internal bool usableWithLMB;
    internal bool UsableOnLimb;
    internal int Value;
    internal List<CraftingQuality> qualities = new();
    internal bool ActuallyUsableOnLimb(Item item) => UsableOnLimb;
    internal int GetValue(Item item) => Value;
}

internal sealed class Recognition
{
    internal bool recognizable = true;
    internal int min;
}

internal sealed class Container : UnityEngine.Object
{
    internal float HoldingWeight;
    internal float maxWeight;
    internal float maxWeightPerItem;
    internal float encumberanceMult = 1f;
    internal float GetHoldingWeight() => HoldingWeight;
}

internal sealed class BatteryItem : UnityEngine.Object
{
    internal string Info = "";
    internal string GetBatteryInfo() => Info;
}

internal sealed class WaterContainerItem : UnityEngine.Object
{
    internal float CurrentTotal;
    internal float Capacity;
    internal List<LiquidStack> stack = new();
    internal Color AverageColor() => Color.white;
}

internal sealed class AmmoScript : UnityEngine.Object
{
    internal enum AmmoItemType { Magazine }
    internal AmmoItemType itemType = AmmoItemType.Magazine;
    internal int rounds;
    internal int maxRounds;
}

internal sealed class BoughtItem : UnityEngine.Object
{
    internal float time;
}

internal sealed class CraftingQuality
{
    internal string LocaleName = "";
    internal float amount;
}

internal sealed class LiquidStack
{
    internal string liquidId = "";
    internal float amount;
}

internal static class Liquids
{
    internal sealed class Definition
    {
        internal bool localeFromItem;
        internal string localeName = "";
        internal Color color = Color.white;
        internal List<CraftingQuality> qualities = new();
        internal IEnumerable<CraftingQuality> GetScaledQualities(float amount) => qualities;
    }

    internal static ConcurrentDictionary<string, Definition> Registry { get; } = new();
}

internal static class Locale
{
    internal static string GetOther(string key) => key is "unknownobjectwarning" or "decaysin" ? key + " <>" : key;
    internal static string GetItem(string id) => "item:" + id;
}

internal static class PlayerCamera
{
    internal static string ConditionToColorCode(float condition) => "<color=#FFFFFF>";
    internal static string EndColor() => "</color>";
    internal static string TimeToString(TimeSpan remaining) => remaining.ToString();
}

namespace CUCoreLib.Registries
{
    internal static class ItemRegistry
    {
        internal static ConcurrentDictionary<string, ItemInfo> Definitions { get; } = new();
        internal static bool TryGetItemInfo(string id, [NotNullWhen(true)] out ItemInfo? info) => Definitions.TryGetValue(id, out info);
    }
}

namespace UnityEngine
{
    internal class Object
    {
        public static implicit operator bool(Object? value) => value is not null;
    }

    internal readonly struct Color
    {
        internal static Color white => default;
        internal static Color Lerp(Color first, Color second, float fraction) => first;
    }

    internal static class ColorUtility
    {
        internal static string ToHtmlStringRGB(Color color) => "FFFFFF";
    }

    internal static class Mathf
    {
        internal static int FloorToInt(float value) => (int)Math.Floor(value);
        internal static int RoundToInt(float value) => (int)Math.Round(value);
    }
}
