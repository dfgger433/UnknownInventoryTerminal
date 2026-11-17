using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // IMGUI does not participate in EventSystem raycasting. A transparent UGUI shield
    // prevents the existing inventory and world UI from also receiving window clicks.
    internal sealed class InputShield
    {
        private GameObject? _root;
        internal void SetVisible(bool visible)
        {
            if (visible && !_root)
            {
                _root = new GameObject("UIT.InputShield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                Object.DontDestroyOnLoad(_root);
                var canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var blocker = new GameObject("Block clicks", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                blocker.transform.SetParent(_root.transform, false);
                var rect = blocker.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                var image = blocker.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
            }
            if (_root) _root!.SetActive(visible);
        }
        internal void Dispose() { if (_root) Object.Destroy(_root); _root = null; }
    }
}
