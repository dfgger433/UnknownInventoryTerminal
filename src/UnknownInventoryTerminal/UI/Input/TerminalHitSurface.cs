// SPDX-License-Identifier: GPL-3.0-only

using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal static class TerminalHitSurface
    {
        // Even a fully transparent cell must participate in GraphicRaycaster hit testing.
        internal static void Enable(Image image)
        {
            image.raycastTarget = true;
            image.alphaHitTestMinimumThreshold = 0;
            image.canvasRenderer.cullTransparentMesh = false;
        }

        internal static void Relay(ref RaycastResult hit, InvButton? target)
        {
            if (target && target!.isActiveAndEnabled) hit.gameObject = target.gameObject;
        }
    }
}
