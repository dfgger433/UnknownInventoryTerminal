using UnityEngine;

namespace UnknownInventoryTerminal
{
    // Keep all reserved orders distinct on the default sorting layer.
    internal static class TerminalCanvasOrder
    {
        internal const int Shield = short.MaxValue - 4;
        internal const int Inventory = short.MaxValue - 3;
        internal const int NativeVisuals = short.MaxValue - 2;
        internal const int ItemTooltip = short.MaxValue - 1;
        internal const int ControlHint = short.MaxValue;

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
