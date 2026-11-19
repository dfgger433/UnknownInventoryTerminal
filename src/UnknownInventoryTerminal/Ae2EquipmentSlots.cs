using System;
using System.Collections.Generic;

namespace UnknownInventoryTerminal
{
    internal static class Ae2EquipmentSlots
    {
        // Stable, head-to-feet order for the vanilla wearSlotId values. Extension cells follow these.
        internal static IReadOnlyList<string> Vanilla { get; } = Array.AsReadOnly(new[]
        {
            "hat", "eyes", "blindfold", "balaclava", "mouth", "neck",
            "torso", "outertorso", "torsofront", "back", "bandolier", "belt",
            "arms", "wraps", "hands", "thigh", "thighback", "knees",
            "feet"
        });

        internal static string[] Claims(string? slotIds)
        {
            if (string.IsNullOrWhiteSpace(slotIds)) return Array.Empty<string>();
            var claims = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in slotIds!.Split(','))
            {
                var id = part.Trim();
                if (id.Length > 0 && seen.Add(id)) claims.Add(id);
            }
            return claims.ToArray();
        }

        // Do not reorder existing cells when another mod registers a wearable while the terminal is open.
        internal static bool Append(List<string> slots, IEnumerable<string?> definitions)
        {
            var seen = new HashSet<string>(slots, StringComparer.Ordinal);
            var aliases = new HashSet<string>(slots, StringComparer.OrdinalIgnoreCase);
            var changed = false;
            foreach (var definition in definitions)
            {
                var claims = Claims(definition);
                foreach (var id in claims)
                {
                    if ((claims.Length > 1 && aliases.Contains(id)) || !seen.Add(id)) continue;
                    aliases.Add(id);
                    slots.Add(id);
                    changed = true;
                }
            }
            return changed;
        }

        internal static bool ClaimsSlot(string? slotIds, string slot)
        {
            var claims = Claims(slotIds);
            // CCL's multi-slot conflict rule ignores case, but vanilla single-slot matching does not.
            return Array.Exists(claims, id => string.Equals(id, slot,
                claims.Length > 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }
    }
}
