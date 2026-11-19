using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    internal enum ContainerPlacementMode { BestFit, WeightReduction, Manual }

    // Select a destination only. The native release path performs the actual transfer.
    internal static class ContainerPlacement
    {
        private static ConfigEntry<ContainerPlacementMode> _mode = null!;
        internal static ContainerPlacementMode Mode
        {
            get => Enum.IsDefined(typeof(ContainerPlacementMode), _mode.Value) ? _mode.Value : ContainerPlacementMode.BestFit;
            set => _mode.Value = value;
        }

        internal static void Initialize(ConfigFile config)
        {
            _mode = config.Bind(SettingsBridge.ConfigSection, "ContainerPlacement", ContainerPlacementMode.BestFit,
                "Default destination for a drop on the storage grid. A drop on a loadout slot targets that slot.");
            const string name = "ContainerPlacement";
            ModOptionsRegistry.Register(ModOptionDefinition.Dropdown(SettingsBridge.ConfigSection + "." + name,
                SettingsBridge.Label(name), SettingsBridge.Description(name), SettingsBridge.MenuCategory, (int)Mode, new[] {
                    SettingsBridge.Choice(name, nameof(ContainerPlacementMode.BestFit)),
                    SettingsBridge.Choice(name, nameof(ContainerPlacementMode.WeightReduction)),
                    SettingsBridge.Choice(name, nameof(ContainerPlacementMode.Manual))
                }, value => { if (value >= 0 && value <= 2) Mode = (ContainerPlacementMode)value; }));
        }

        internal sealed class Candidate
        {
            internal readonly Item Item;
            internal readonly float RemainingCapacity, WeightReduction;
            internal Candidate(Item item, float remainingCapacity, float weightReduction)
            { Item = item; RemainingCapacity = remainingCapacity; WeightReduction = weightReduction; }
        }

        internal static IReadOnlyList<Item> Containers(Body body) => body.GetSurfaceInventoryItems()
            .Where(item => item && item.container && !item.TryGetParentContainer(out _))
            .GroupBy(item => item.GetInstanceID()).Select(group => group.First())
            .OrderBy(item => item.fullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.GetInstanceID()).ToArray();

        internal static IReadOnlyList<Candidate> Candidates(Body body, Item source)
        {
            if (!body || !source || !GameInventoryAdapter.Owns(body, source)) return Array.Empty<Candidate>();
            var sourceMultiplier = source.TryGetParentContainer(out var parent) ? parent.encumberanceMult : 1f;
            var candidates = new List<Candidate>();
            foreach (var item in Containers(body))
            {
                if (!CanPlace(body, source, item)) continue;
                var container = item.container;
                candidates.Add(new Candidate(item,
                    container.maxWeight - container.GetHoldingWeight() - source.totalWeight,
                    source.totalWeight * (sourceMultiplier - container.encumberanceMult)));
            }

            var ordered = Mode == ContainerPlacementMode.WeightReduction
                ? candidates.OrderByDescending(candidate => candidate.WeightReduction).ThenBy(candidate => candidate.RemainingCapacity)
                : Mode == ContainerPlacementMode.Manual
                    ? candidates.OrderBy(candidate => candidate.Item.fullName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidate => candidate.RemainingCapacity)
                    : candidates.OrderBy(candidate => candidate.RemainingCapacity).ThenByDescending(candidate => candidate.WeightReduction);
            return ordered.ThenBy(candidate => candidate.Item.GetInstanceID()).ToArray();
        }

        internal static bool CanPlace(Body body, Item source, Item destination)
        {
            if (!body || !source || !destination || source == destination || !destination.container ||
                !GameInventoryAdapter.Owns(body, source) || !GameInventoryAdapter.Owns(body, destination)) return false;
            if (destination.TryGetParentContainer(out _) || destination.transform.IsChildOf(source.transform) ||
                source.transform.parent == destination.transform) return false;
            if (source.container && source.container.GetHoldingWeight() > 0f) return false;
            if (Vector2.Distance(source.transform.position, destination.transform.position) >= 10f) return false;
            if (destination.battery && (source.Stats.HasTag("tool") || source.Stats.HasTag("battery"))) return false;
            return destination.container.CanHoldItem(source);
        }
    }
}
