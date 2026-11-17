using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // IMGUI has no EventSystem surface. Keep a real UI-layer blocker below the
    // terminal tiles and temporarily lift the original inventory controls above it.
    internal sealed class InputShield
    {
        private GameObject? _root;
        private readonly List<CanvasLease> _nativeCanvases = new List<CanvasLease>();
        private bool _inventoryInteraction;

        internal void SetNativeInventoryInteraction(bool active)
        {
            _inventoryInteraction = active;
            if (_root && _root!.activeSelf) SetVisible(true);
        }

        internal void SetVisible(bool visible)
        {
            if (visible && !_root)
            {
                var uiLayer = LayerMask.NameToLayer("UI");
                _root = new GameObject("UIT.InputShield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                if (uiLayer >= 0) _root.layer = uiLayer;
                Object.DontDestroyOnLoad(_root);
                var canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue - 2;
                var blocker = new GameObject("Block clicks", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                if (uiLayer >= 0) blocker.layer = uiLayer;
                blocker.transform.SetParent(_root.transform, false);
                var rect = blocker.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                var image = blocker.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
            }
            if (_root) _root!.SetActive(visible);
            var camera = PlayerCamera.main;
            foreach (var lease in _nativeCanvases) lease.Wanted = false;
            if (visible && camera)
            {
                Lift(camera.radialMenu ? camera.radialMenu.gameObject : null, true);
                if (_inventoryInteraction) Lift(camera.mainCanvas ? camera.mainCanvas.gameObject : null, true);
                // A container opened before the terminal may belong to another body.
                var container = camera.currentContainer;
                if (container && GameInventoryAdapter.Owns(camera.body, container.GetComponent<Item>()))
                    Lift(camera.containerMenu, true);
                Lift(camera.dragImage ? camera.dragImage.gameObject : null, false);
            }
            foreach (var lease in _nativeCanvases) lease.SetActive(lease.Wanted);
        }

        private void Lift(GameObject? root, bool acceptsInput)
        {
            if (!root) return;
            var lease = _nativeCanvases.Find(candidate => candidate.Root == root);
            if (lease == null)
            {
                lease = new CanvasLease(root!, acceptsInput);
                _nativeCanvases.Add(lease);
            }
            lease.Wanted = true;
        }

        internal bool IsBlocker(GameObject target) => Contains(_root, target);
        internal bool IsNativeControl(GameObject target)
        {
            foreach (var lease in _nativeCanvases)
                if (lease.Wanted && lease.AcceptsInput && Contains(lease.Root, target)) return true;
            return false;
        }
        private static bool Contains(GameObject? root, GameObject target) => root && target &&
            (target.transform == root!.transform || target.transform.IsChildOf(root.transform));

        internal void Dispose()
        {
            foreach (var lease in _nativeCanvases) lease.Dispose();
            _nativeCanvases.Clear();
            if (_root) { _root!.SetActive(false); Object.Destroy(_root); }
            _root = null;
        }

        // Restore existing canvas settings; retain added canvases inheriting their
        // original parent's sorting between openings. Disable only added raycasters,
        // since disabling a Canvas can hide the original native menu's graphics.
        private sealed class CanvasLease
        {
            internal readonly GameObject Root;
            internal readonly bool AcceptsInput;
            internal bool Wanted;
            private readonly Canvas _canvas;
            private readonly GraphicRaycaster? _raycaster;
            private readonly bool _addedCanvas, _addedRaycaster;
            private bool _active, _enabled, _overrideSorting, _raycasterEnabled, _imageRaycast;
            private int _order, _layer;
            private readonly Graphic? _visual;

            internal CanvasLease(GameObject root, bool acceptsInput)
            {
                Root = root; AcceptsInput = acceptsInput;
                _canvas = root.GetComponent<Canvas>();
                _addedCanvas = !_canvas;
                if (_addedCanvas) _canvas = root.AddComponent<Canvas>();
                if (acceptsInput)
                {
                    _raycaster = root.GetComponent<GraphicRaycaster>();
                    _addedRaycaster = !_raycaster;
                    if (_addedRaycaster) _raycaster = root.AddComponent<GraphicRaycaster>();
                }
                else _visual = root.GetComponent<Graphic>();
            }
            internal void SetActive(bool active)
            {
                if (!_canvas || _active == active) return;
                if (active)
                {
                    _enabled = _canvas.enabled; _overrideSorting = _canvas.overrideSorting;
                    _order = _canvas.sortingOrder; _layer = _canvas.sortingLayerID;
                    if (_raycaster) _raycasterEnabled = _raycaster!.enabled;
                    if (_visual) { _imageRaycast = _visual!.raycastTarget; _visual.raycastTarget = false; }
                    _canvas.enabled = true;
                    _canvas.overrideSorting = true;
                    _canvas.sortingLayerID = 0;
                    _canvas.sortingOrder = short.MaxValue;
                    if (_raycaster) _raycaster!.enabled = true;
                }
                else
                {
                    _canvas.overrideSorting = _overrideSorting;
                    _canvas.sortingLayerID = _layer; _canvas.sortingOrder = _order;
                    _canvas.enabled = _enabled;
                    if (_raycaster) _raycaster!.enabled = !_addedRaycaster && _raycasterEnabled;
                    if (_visual) _visual!.raycastTarget = _imageRaycast;
                }
                _active = active;
            }
            internal void Dispose()
            {
                SetActive(false);
                if (_addedRaycaster && _raycaster) Object.Destroy(_raycaster);
                if (_addedCanvas && _canvas) Object.Destroy(_canvas);
            }
        }
    }
}
