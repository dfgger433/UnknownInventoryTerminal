using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // Keep all reserved orders distinct on the default sorting layer.
    internal static class TerminalCanvasOrder
    {
        internal const int Shield = short.MaxValue - 5;
        internal const int Inventory = short.MaxValue - 4;
        internal const int NativeVisuals = short.MaxValue - 3;
        internal const int ItemTooltip = short.MaxValue - 2;
        internal const int ControlHint = short.MaxValue - 1;
        internal const int Modal = short.MaxValue;

        internal static GameObject CreateModalLayer(int layer)
        {
            // A root overlay has its own raw EventSystem raycast priority. Do not nest it
            // in the canvas whose hit surface may be re-ordered to block inventory input.
            var root = new GameObject("UIT.FluidTransferDialog", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster))
                { layer = layer };
            root.SetActive(false);
            Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingLayerID = 0;
            canvas.sortingOrder = Modal;
            canvas.pixelPerfect = true;
            return root;
        }

        internal static RectTransform AddTooltipLayer(Transform parent, string name, int order)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup))
                { layer = parent.gameObject.layer };
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = node.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerID = 0;
            canvas.sortingOrder = order;
            // No GraphicRaycaster: a hint can cover a control without taking its input.
            var group = node.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return rect;
        }
    }
}
