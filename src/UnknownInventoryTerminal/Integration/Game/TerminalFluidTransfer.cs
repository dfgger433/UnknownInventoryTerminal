// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    // The dialog never mutates a liquid stack. Finish is the native transaction and Together sync hook.
    internal sealed class TerminalFluidTransfer(PlayerCamera camera, Item source, Item destination)
    {
        // Invoke the patched entry point explicitly even on Unity/Mono builds that inline small direct calls.
        internal static readonly MethodInfo NativeFinish = AccessTools.Method(typeof(LiquidTransfer), nameof(LiquidTransfer.Finish));

        internal PlayerCamera Camera { get; } = camera;
        internal Body Body { get; } = camera.body;
        internal Item Source { get; } = source;
        internal Item Destination { get; } = destination;

        internal static bool IsPair(Item source, Item destination) => source && destination && source != destination &&
            source.GetComponent<WaterContainerItem>() && destination.GetComponent<WaterContainerItem>();

        internal int MaximumMl() => TryGetContainers(out var from, out var to)
            ? TerminalFluidAmount.MaximumMl(from.CurrentTotal, to.SpaceLeft) : 0;

        private bool TryGetContainers(out WaterContainerItem from, out WaterContainerItem to)
        {
            from = to = null!;
            // The native drag start already performed the one-shot use permission check.
            // Re-reading allowUseItem here can run its mood-dependent random gate again.
            return Plugin.CanInteract(Camera) && Camera.body == Body &&
                Source && Destination && Source != Destination && Destination.id != "craftingbottle" &&
                GameInventoryAdapter.Owns(Body, Source) && GameInventoryAdapter.Owns(Body, Destination) &&
                Source.TryGetComponent(out from) && from && Destination.TryGetComponent(out to) && to &&
                Body.CanCombine(Destination, Source);
        }

        internal bool Finish(int ml)
        {
            if (!TryGetContainers(out var from, out var to) || !Body.DoPickupCheck(Source, noAlerts: true) ||
                !TerminalFluidAmount.CanTransfer(ml, TerminalFluidAmount.MaximumMl(from.CurrentTotal, to.SpaceLeft)) ||
                !GameInventoryAdapter.CanSyncFluidTransfer(Source, Destination)) return false;

            // The native selector only needs its endpoint fields and mL to complete. Attach it
            // to an inactive object so Awake/Update never open or poll a second UI; its Finish
            // still runs through Harmony (Together's postfix reads these exact fields).
            var native = new GameObject("UIT.NativeLiquidTransfer");
            native.SetActive(false);
            try
            {
                var transfer = native.AddComponent<LiquidTransfer>();
                transfer.transferFrom = from;
                transfer.transferTo = to;
                transfer.ml = ml;
                NativeFinish.Invoke(transfer, null);
                return true;
            }
            finally { Object.Destroy(native); }
        }
    }
}
