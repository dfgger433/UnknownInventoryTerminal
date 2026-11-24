using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // Screen overlay and inventory raycasts have independent lifetimes. Canvas order is
    // a visual decision, not the mechanism that grants or revokes inventory input.
    internal sealed class TerminalModalInput
    {
        private readonly GameObject _overlay;
        private readonly GraphicRaycaster _inventoryRaycaster;
        private bool _ownsPointer, _inventoryWasEnabled;
        private int _dismissedFrame = -1;

        internal TerminalModalInput(GameObject overlay, GraphicRaycaster inventoryRaycaster)
        {
            _overlay = overlay;
            _inventoryRaycaster = inventoryRaycaster;
        }

        internal bool IsOpen => _overlay && _overlay.activeInHierarchy;
        internal bool BlocksInput(int frame) => IsOpen || _dismissedFrame == frame;

        internal void Open()
        {
            _dismissedFrame = -1;
            if (!_ownsPointer && _inventoryRaycaster)
            {
                _inventoryWasEnabled = _inventoryRaycaster.enabled;
                _inventoryRaycaster.enabled = false;
                _ownsPointer = true;
            }
            _overlay.SetActive(true);
        }

        internal void Dismiss(int frame)
        {
            if (!_overlay || !_overlay.activeSelf) return;
            _overlay.SetActive(false);
            _dismissedFrame = frame;
            ClearSelection();
        }

        internal void Update(int frame)
        {
            if (IsOpen) return;
            // SetSelectedGameObject guards against re-entry. If dismissal happened inside
            // a selection callback, clear only a stale selection once that guard is down.
            ClearSelection();
            if (_dismissedFrame != frame) ReleasePointer();
        }

        internal void EndInputFrame(int frame)
        {
            // Update handlers (including EventSystem) have seen the dismissal shield.
            // Restore raw raycasts now so the next click works regardless of Update order.
            if (IsOpen || _dismissedFrame != frame) return;
            ClearSelection();
            ReleasePointer();
        }

        internal void Reset(int frame)
        {
            Dismiss(frame);
            ClearSelection();
            _dismissedFrame = -1;
            ReleasePointer();
        }

        private void ReleasePointer()
        {
            if (!_ownsPointer) return;
            _ownsPointer = false;
            if (_inventoryRaycaster) _inventoryRaycaster.enabled = _inventoryWasEnabled;
        }

        private void ClearSelection()
        {
            var events = EventSystem.current;
            if (!events || events.alreadySelecting || !_overlay) return;
            var selected = events.currentSelectedGameObject;
            if (selected && selected.transform.IsChildOf(_overlay.transform)) events.SetSelectedGameObject(null);
        }
    }
}
