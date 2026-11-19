using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UnknownInventoryTerminal
{
    // The blocker sits below the terminal. Only the drag cursor and tooltip are lifted above it.
    internal sealed class InputShield
    {
        internal const int BackOrder = short.MaxValue - 3;
        private GameObject? _root, _blocker;
        private LeaseCleanup? _cleanup;
        private readonly List<Lease> _leases = new List<Lease>();

        internal void SetVisible(bool visible)
        {
            if (visible)
            {
                EnsureRoot();
                if (!_blocker!.activeSelf) _blocker.SetActive(true);
            }
            else if (_blocker && _blocker!.activeSelf) _blocker.SetActive(false);

            foreach (var lease in _leases) lease.Wanted = false;
            if (visible)
            {
                var camera = PlayerCamera.main;
                if (camera)
                {
                    Want<MenuLease>(camera.radialMenu ? camera.radialMenu.gameObject : null, root => new MenuLease(root));
                    Want<MenuLease>(camera.containerMenu, root => new MenuLease(root));
                    Want<VisualLease>(camera.dragImage ? camera.dragImage.gameObject : null, root => new VisualLease(root));
                }
                var hoverBox = GlobalDark.main ? GlobalDark.main.tooltipRect : null;
                Want<VisualLease>(hoverBox ? hoverBox!.gameObject : null, root => new VisualLease(root));
            }

            bool unused = false;
            for (int i = _leases.Count - 1; i >= 0; --i)
            {
                var lease = _leases[i];
                if (!lease.Root) _leases.RemoveAt(i);
                else
                {
                    lease.SetActive(lease.Wanted);
                    unused |= !lease.Wanted;
                }
            }

            // Destroy is deferred in Unity. Wait until after Update/LateUpdate so a
            // close and reopen in those callbacks can reuse our Canvas/CanvasGroup.
            if (unused)
            {
                if (_cleanup) _cleanup!.Queue(DiscardUnusedLeases);
                else DiscardUnusedLeases();
            }
        }

        private void EnsureRoot()
        {
            if (_root && _blocker && _cleanup) return;
            if (_root)
            {
                _root!.SetActive(false);
                Object.Destroy(_root);
            }
            var uiLayer = LayerMask.NameToLayer("UI");
            _root = new GameObject("UIT.InputShield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            if (uiLayer >= 0) _root.layer = uiLayer;
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = BackOrder;
            _cleanup = _root.AddComponent<LeaseCleanup>();
            _blocker = new GameObject("Block clicks", typeof(RectTransform), typeof(Image));
            _blocker.layer = _root.layer;
            _blocker.transform.SetParent(_root.transform, false);
            var rect = _blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            _blocker.GetComponent<Image>().color = Color.clear;
        }

        private void Want<T>(GameObject? root, Func<GameObject, T> create) where T : Lease
        {
            if (!root) return;
            var lease = _leases.Find(candidate => candidate is T && candidate.Root == root);
            if (lease == null) _leases.Add(lease = create(root!));
            lease.Wanted = true;
        }

        internal bool IsBlocker(GameObject target) => _blocker && _blocker!.activeSelf && _root && target &&
            (target.transform == _root!.transform || target.transform.IsChildOf(_root.transform));

        private void DiscardUnusedLeases()
        {
            for (int i = _leases.Count - 1; i >= 0; --i)
            {
                var lease = _leases[i];
                if (lease.Wanted && lease.Root) continue;
                lease.Dispose();
                _leases.RemoveAt(i);
            }
        }

        internal void Dispose()
        {
            SetVisible(false);
            DiscardUnusedLeases();
            if (_root) Object.Destroy(_root);
            _root = _blocker = null;
            _cleanup = null;
        }

        private sealed class LeaseCleanup : MonoBehaviour
        {
            private Action? _pending;

            internal void Queue(Action cleanup)
            {
                if (_pending != null) return;
                _pending = cleanup;
                StartCoroutine(EndOfFrame());
            }

            private IEnumerator EndOfFrame()
            {
                yield return new WaitForEndOfFrame();
                RunPending();
            }

            private void OnDestroy() => RunPending();

            private void RunPending()
            {
                var pending = _pending;
                _pending = null;
                pending?.Invoke();
            }
        }

        private abstract class Lease
        {
            protected Lease(GameObject root) { Root = root; }
            internal GameObject Root { get; }
            internal bool Wanted { get; set; }
            internal abstract void SetActive(bool active);
            internal abstract void Dispose();
        }

        private sealed class MenuLease : Lease
        {
            private CanvasGroup? _group;
            private bool _added, _active, _enabled, _interactable, _blocksRaycasts;
            private float _alpha;

            internal MenuLease(GameObject root) : base(root) { }

            internal override void SetActive(bool active)
            {
                if (!Root) return;
                if (active)
                {
                    if (!_group)
                    {
                        _group = Root.GetComponent<CanvasGroup>();
                        _added = !_group;
                        if (_added) _group = Root.AddComponent<CanvasGroup>();
                        _active = false;
                    }
                    var group = _group!;
                    if (!_active)
                    {
                        _enabled = group.enabled;
                        _alpha = group.alpha;
                        _interactable = group.interactable;
                        _blocksRaycasts = group.blocksRaycasts;
                        _active = true;
                    }
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    group.enabled = true;
                }
                else if (_active)
                {
                    _active = false;
                    if (!_group) return;
                    _group!.enabled = _enabled;
                    _group.alpha = _alpha;
                    _group.interactable = _interactable;
                    _group.blocksRaycasts = _blocksRaycasts;
                }
            }

            internal override void Dispose()
            {
                SetActive(false);
                if (_added && _group) Object.Destroy(_group);
            }
        }

        private sealed class VisualLease : Lease
        {
            private Canvas? _canvas;
            private GraphicRaycaster? _raycaster;
            private Graphic? _visual;
            private bool _addedCanvas, _active, _enabled, _overrideSorting, _raycasterEnabled, _imageRaycast;
            private int _order, _layer;

            internal VisualLease(GameObject root) : base(root) { }

            internal override void SetActive(bool active)
            {
                if (!Root) return;
                if (active && _active && !_canvas) SetActive(false);
                if (_active == active) return;
                if (active)
                {
                    if (!_canvas)
                    {
                        _canvas = Root.GetComponent<Canvas>();
                        _addedCanvas = !_canvas;
                        if (_addedCanvas) _canvas = Root.AddComponent<Canvas>();
                    }
                    var canvas = _canvas!;
                    _enabled = canvas.enabled; _overrideSorting = canvas.overrideSorting;
                    _order = canvas.sortingOrder; _layer = canvas.sortingLayerID;
                    _active = true;
                    _raycaster = Root.GetComponent<GraphicRaycaster>();
                    if (_raycaster) { _raycasterEnabled = _raycaster!.enabled; _raycaster.enabled = false; }
                    _visual = Root.GetComponent<Graphic>();
                    if (_visual) { _imageRaycast = _visual!.raycastTarget; _visual.raycastTarget = false; }
                    canvas.enabled = true;
                    canvas.overrideSorting = true;
                    canvas.sortingLayerID = 0;
                    canvas.sortingOrder = short.MaxValue;
                }
                else
                {
                    _active = false;
                    if (_canvas)
                    {
                        _canvas!.sortingLayerID = _layer; _canvas.sortingOrder = _order;
                        _canvas.overrideSorting = _overrideSorting;
                        _canvas.enabled = _enabled;
                    }
                    if (_raycaster) _raycaster!.enabled = _raycasterEnabled;
                    if (_visual) _visual!.raycastTarget = _imageRaycast;
                }
            }

            internal override void Dispose()
            {
                SetActive(false);
                if (_addedCanvas && _canvas) Object.Destroy(_canvas);
            }
        }
    }
}
