// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using CUCoreLib.Registries;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal sealed class LoadoutCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal Body? Body;
        internal int MainIndex = -1;
        internal string? EquipmentId;
        internal Image Face = null!, Highlight = null!;
        internal Image? UnavailableBadge;
        internal TextMeshProUGUI Caption = null!;
        internal InvButton? Button;
        internal NativeLoadoutPanel Owner = null!;
        internal bool DragEligible;
        internal bool BaseUnavailable;

        public void OnPointerEnter(PointerEventData e) => Owner.Hover(this);
        public void OnPointerExit(PointerEventData e) => Owner.Leave(this);
        private void OnDisable()
        {
            DragEligible = false;
            Owner?.Leave(this);
        }

        internal bool IsCurrent(PlayerCamera camera) => isActiveAndEnabled && Body && Body == camera.body;

        // Hover/drag feedback is type-based: a matching wearable highlights even when
        // the target limb is currently unavailable. The release path uses CanEquip below.
        internal bool MatchesEquipmentType(PlayerCamera camera, Item source) => IsCurrent(camera) && EquipmentId != null &&
            source && source.Stats.wearable && Ae2EquipmentSlots.ClaimsSlot(source.Stats.wearSlotId, EquipmentId);

        internal bool CanEquip(PlayerCamera camera, Item source) => MatchesEquipmentType(camera, source) &&
            !string.IsNullOrEmpty(source.Stats.desiredWearLimb) &&
            Body!.LimbByName(source.Stats.desiredWearLimb) is { } limb && !limb.dismembered;
    }

    internal sealed class LoadoutWheel : MonoBehaviour, IScrollHandler
    {
        internal NativeLoadoutPanel Owner = null!;
        internal bool Equipment;
        public void OnScroll(PointerEventData e)
        {
            if (Owner.Scroll(Equipment, e.scrollDelta.y)) e.Use();
        }
    }

    // Only visible rows need native buttons. Empty equipment cells are real drop targets, not fake items.
    internal sealed class NativeLoadoutPanel
    {
        private readonly ManualLogSource _log;
        private readonly Ae2TerminalSkin _skin;
        private readonly List<string> _equipment = new List<string>(Ae2EquipmentSlots.Vanilla);
        private readonly List<LoadoutCell> _mainCells = new List<LoadoutCell>(), _equipmentCells = new List<LoadoutCell>();
        private RectTransform _main = null!, _worn = null!;
        private TextMeshProUGUI _heading = null!;
        private LoadoutCell? _hover;
        private Ae2TerminalLayout _layout;
        private Body? _body;
        private int _mainRow, _equipmentRow;
        private float _nextDiscovery;
        private bool _warned;

        internal NativeLoadoutPanel(ManualLogSource log, Ae2TerminalSkin skin) { _log = log; _skin = skin; }
        internal int EquipmentCount => _equipment.Count;
        internal string Detail => _hover && (_hover!.EquipmentId != null || _hover.MainIndex >= 0)
            ? (_hover.EquipmentId is string id ? SlotLabel(id) : MainLabel(_hover.MainIndex)) +
                (_hover.Button && _hover.Button!.gameObject.activeInHierarchy && _hover.Button.GetItem() is Item item && item ? " / " + item.fullName : "")
            : "";

        internal void Discover()
        {
            if (Time.unscaledTime < _nextDiscovery) return;
            _nextDiscovery = Time.unscaledTime + .5f;
            // CUCoreLib injects registered definitions into GlobalItems, but registrations can precede injection.
            Ae2EquipmentSlots.Append(_equipment, ItemRegistry.GetRegisteredItemIds().Select(id =>
                ItemRegistry.TryGetCustomInfo(id, out var info) && info.wearable ? info.wearSlotId : null));
            if (Item.GlobalItems != null)
                Ae2EquipmentSlots.Append(_equipment, Item.GlobalItems.Values.Where(info => info.wearable).Select(info => info.wearSlotId));
        }

        internal void Attach(Transform parent)
        {
            _main = AddSection(parent, "Main inventory", false);
            _heading = NativeFlatInventoryPanel.CreateText(parent, "Equipment heading", 12, TextAlignmentOptions.MidlineRight);
            _worn = AddSection(parent, "Equipment slots", true);
        }

        private RectTransform AddSection(Transform parent, string name, bool equipment)
        {
            var root = NativeFlatInventoryPanel.Add(parent, name, typeof(Image), typeof(LoadoutWheel));
            root.GetComponent<Image>().color = Color.clear;
            var wheel = root.GetComponent<LoadoutWheel>();
            wheel.Owner = this;
            wheel.Equipment = equipment;
            return root.GetComponent<RectTransform>();
        }

        internal void SetLayout(Ae2TerminalLayout layout)
        {
            if (_layout.Scale != layout.Scale)
            {
                foreach (var cell in _mainCells) Ae2TerminalSkin.SizeSlotHighlight(cell.Highlight, layout.Scale);
                foreach (var cell in _equipmentCells) Ae2TerminalSkin.SizeSlotHighlight(cell.Highlight, layout.Scale);
            }
            _layout = layout;
            var scale = layout.Scale;
            var pitch = Ae2TerminalLayout.Pitch * scale;
            var left = Ae2TerminalLayout.Inset * scale;
            var width = Ae2TerminalLayout.GridWidth * scale;
            // The parent starts at the top of the board's bottom part; sections sit on the grids drawn there.
            _heading.fontSize = 6 * scale;
            // The status line uses the left part of the same gap.
            NativeFlatInventoryPanel.Position(_heading.rectTransform, new Rect(left + 64 * scale, 0, width - 64 * scale,
                Ae2TerminalLayout.StatusHeight * scale));
            NativeFlatInventoryPanel.Position(_worn, new Rect(left, Ae2TerminalLayout.EquipmentOffset * scale,
                width, layout.EquipmentRows * pitch));
            NativeFlatInventoryPanel.Position(_main, new Rect(left, Ae2TerminalLayout.MainOffset * scale,
                width, layout.MainRows * pitch));
        }

        internal void Reset()
        {
            _body = null;
            if (_hover) Leave(_hover!);
            ClearDragHighlight();
            _mainRow = _equipmentRow = 0;
        }

        internal void UpdateDragHighlight(PlayerCamera camera, Item? source)
        {
            foreach (var cell in _equipmentCells)
            {
                cell.DragEligible = source is Item item && item && cell.MatchesEquipmentType(camera, item);
                UpdateHighlight(cell);
            }
        }

        internal void ClearDragHighlight()
        {
            foreach (var cell in _equipmentCells)
            {
                cell.DragEligible = false;
                UpdateHighlight(cell);
                UpdateUnavailableBadge(cell, cell.BaseUnavailable);
            }
        }

        internal bool Scroll(bool equipment, float delta)
        {
            if (!_body || delta == 0 || !PlayerCamera.main || PlayerCamera.main.body != _body) return false;
            var rows = equipment ? Ae2TerminalLayout.ContentRows(_equipment.Count) : Ae2TerminalLayout.ContentRows(_body!.slots.Length);
            var visible = equipment ? _layout.EquipmentRows : _layout.MainRows;
            if (rows <= visible) return false;
            ref var row = ref (equipment ? ref _equipmentRow : ref _mainRow);
            row = Mathf.Clamp(row - (int)Mathf.Sign(delta), 0, rows - visible);
            Refresh(_body!);
            return true;
        }

        internal void Hover(LoadoutCell cell)
        {
            if (_hover && _hover != cell) UpdateHighlight(_hover!);
            _hover = cell;
            UpdateHighlight(cell);
        }

        internal void Leave(LoadoutCell cell)
        {
            if (_hover != cell) return;
            _hover = null;
            UpdateHighlight(cell);
        }

        private static void UpdateHighlight(LoadoutCell cell)
        {
            var active = cell.DragEligible || cell == cell.Owner._hover &&
                (cell.MainIndex >= 0 || cell.EquipmentId != null);
            cell.Highlight.gameObject.SetActive(active);
            if (!active) return;

            // Keep the ordinary cyan hover/drag outline visible above a static red
            // unavailable marker. Slot labels remain the topmost text when present.
            cell.Highlight.transform.SetAsLastSibling();
            if (cell.Caption && cell.Caption.gameObject.activeSelf) cell.Caption.transform.SetAsLastSibling();
        }

        internal void Refresh(Body body)
        {
            if (_body != body) { Reset(); _body = body; }
            var mainCount = body.slots.Length;
            var columns = Ae2TerminalLayout.Columns;
            _mainRow = Mathf.Clamp(_mainRow, 0, Math.Max(0, Ae2TerminalLayout.ContentRows(mainCount) - _layout.MainRows));
            _equipmentRow = Mathf.Clamp(_equipmentRow, 0, Math.Max(0, Ae2TerminalLayout.ContentRows(_equipment.Count) - _layout.EquipmentRows));
            var equipmentRows = Ae2TerminalLayout.ContentRows(_equipment.Count);
            _heading.text = equipmentRows > _layout.EquipmentRows
                ? TerminalText.Format("inventory.equipmentRows", _equipmentRow + 1, _equipmentRow + _layout.EquipmentRows, equipmentRows)
                : TerminalText.Get("inventory.equipment");
            var exact = new Dictionary<string, Item>(StringComparer.Ordinal);
            var multi = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in body.GetAllWearables())
            {
                if (!item) continue;
                var claims = Ae2EquipmentSlots.Claims(item.Stats.wearSlotId);
                foreach (var claim in claims)
                {
                    exact.TryAdd(claim, item);
                    if (claims.Length > 1) multi.TryAdd(claim, item);
                }
            }
            for (var i = 0; i < _layout.MainRows * columns; ++i)
            {
                var index = _mainRow * columns + i;
                var slot = index < mainCount ? body.slots[index] : null;
                var cell = Cell(_mainCells, _main, i);
                Bind(cell, body, slot ? index : -1, null, slot ? body.GetItem(index) : null,
                    slot ? index.ToString(CultureInfo.InvariantCulture) : "", slot && slot!.isHand && body.handSlot == index,
                    unavailable: slot && !slot!.canPickUp);
            }
            for (var i = 0; i < _layout.EquipmentRows * columns; ++i)
            {
                var index = _equipmentRow * columns + i;
                var id = index < _equipment.Count ? _equipment[index] : null;
                var cell = Cell(_equipmentCells, _worn, i);
                Item? item = null;
                if (id != null && !exact.TryGetValue(id, out item)) multi.TryGetValue(id, out item);
                Bind(cell, body, -1, id, item, item ? "" : id == null ? "" : SlotLabel(id), false,
                    unavailable: EquipmentUnavailable(body, id, item));
            }
            HideExtra(_mainCells, _layout.MainRows * columns);
            HideExtra(_equipmentCells, _layout.EquipmentRows * columns);
        }

        private static bool EquipmentUnavailable(Body body, string? slotId, Item? item)
        {
            if (slotId == null) return false;

            // A populated equipment cell uses the actual worn item's target limb.
            if (item)
            {
                var stats = item!.Stats;
                var limb = string.IsNullOrEmpty(stats.desiredWearLimb) ? null : body.LimbByName(stats.desiredWearLimb);
                return !stats.wearable || !Ae2EquipmentSlots.ClaimsSlot(stats.wearSlotId, slotId) ||
                    !limb || limb!.dismembered;
            }

            // Empty cells have no Item instance. Use every registered wearable definition
            // that can claim this cell; only mark it when all possible target limbs are gone.
            var targetLimbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Item.GlobalItems != null)
                foreach (var info in Item.GlobalItems.Values)
                    if (info.wearable && Ae2EquipmentSlots.ClaimsSlot(info.wearSlotId, slotId) &&
                        !string.IsNullOrEmpty(info.desiredWearLimb)) targetLimbs.Add(info.desiredWearLimb);
            foreach (var id in ItemRegistry.GetRegisteredItemIds())
                if (ItemRegistry.TryGetCustomInfo(id, out var info) && info.wearable &&
                    Ae2EquipmentSlots.ClaimsSlot(info.wearSlotId, slotId) && !string.IsNullOrEmpty(info.desiredWearLimb))
                    targetLimbs.Add(info.desiredWearLimb);

            return targetLimbs.Count > 0 && targetLimbs.All(name =>
            {
                var limb = body.LimbByName(name);
                return !limb || limb.dismembered;
            });
        }

        private LoadoutCell Cell(List<LoadoutCell> cells, Transform parent, int i)
        {
            if (i == cells.Count)
            {
                var root = NativeFlatInventoryPanel.Add(parent, "AE2 loadout cell", typeof(Image), typeof(LoadoutCell));
                var cell = root.GetComponent<LoadoutCell>();
                cell.Owner = this;
                // The slot art is part of terminal.png; this face is only a hit area and the active-hand tint.
                cell.Face = root.GetComponent<Image>();
                cell.Face.color = Color.clear;
                TerminalHitSurface.Enable(cell.Face);
                cell.Highlight = Ae2TerminalSkin.CreateSlotHighlight(root.transform);
                Ae2TerminalSkin.SizeSlotHighlight(cell.Highlight, _layout.Scale);
                cell.Caption = NativeFlatInventoryPanel.CreateText(root.transform, "Slot label", 5, TextAlignmentOptions.Center);
                cells.Add(cell);
            }
            var result = cells[i];
            var pitch = Ae2TerminalLayout.Pitch * _layout.Scale;
            NativeFlatInventoryPanel.Position((RectTransform)result.transform,
                new Rect(i % Ae2TerminalLayout.Columns * pitch, i / Ae2TerminalLayout.Columns * pitch, pitch, pitch));
            result.gameObject.SetActive(true);
            return result;
        }

        private void Bind(LoadoutCell cell, Body body, int index, string? id, Item? item, string caption, bool activeHand, bool unavailable = false)
        {
            cell.Body = body;
            cell.MainIndex = index;
            cell.EquipmentId = id;
            cell.DragEligible = false;
            cell.BaseUnavailable = unavailable;
            UpdateHighlight(cell);
            var scale = _layout.Scale;
            // The slot art underneath comes from terminal.png; only the active hand gets a translucent highlight.
            var focus = Ae2TerminalSkin.Focus;
            cell.Face.color = activeHand ? new Color(focus.r, focus.g, focus.b, .55f) : Color.clear;
            var pitch = Ae2TerminalLayout.Pitch * scale;
            // A refItem button would destroy itself on update when empty. Keep an empty equipment cell as an Image only.
            if (index >= 0 || item)
            {
                if (!cell.Button) cell.Button = CreateButton(cell);
                if (cell.Button)
                {
                    var button = cell.Button!;
                    button.slot = index;
                    button.refItem = index >= 0 ? null! : item!;
                    button.maxImageSize = pitch - 6 * scale;
                    NativeFlatInventoryPanel.Stretch((RectTransform)button.transform, Vector2.one * scale, -Vector2.one * scale);
                    button.gameObject.SetActive(true);
                }
            }
            else if (cell.Button) cell.Button!.gameObject.SetActive(false);
            cell.Caption.transform.SetAsLastSibling();
            UpdateUnavailableBadge(cell, unavailable);
            cell.Caption.text = caption;
            cell.Caption.fontSize = (index >= 0 ? 6 : 5) * scale;
            cell.Caption.alignment = index >= 0 ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
            NativeFlatInventoryPanel.Stretch(cell.Caption.rectTransform, Vector2.one * scale, -Vector2.one * 2 * scale);
        }

        private void UpdateUnavailableBadge(LoadoutCell cell, bool unavailable)
        {
            // The number in the lower-right corner is the slot label, not part of the
            // unavailable state; hide it while the full-slot marker is shown.
            cell.Caption.gameObject.SetActive(!unavailable);
            if (!unavailable)
            {
                if (cell.UnavailableBadge) cell.UnavailableBadge!.gameObject.SetActive(false);
                return;
            }

            if (!cell.UnavailableBadge)
            {
                var badge = NativeFlatInventoryPanel.Add(cell.transform, "Unavailable slot", typeof(Image)).GetComponent<Image>();
                badge.raycastTarget = false;
                cell.UnavailableBadge = badge;
            }
            var overlay = cell.UnavailableBadge!;
            overlay.sprite = _skin.GetVisibleSprite(Ae2Image.UnavailableSlotBadge);
            overlay.type = Image.Type.Simple;
            overlay.preserveAspect = true;
            overlay.color = overlay.sprite ? Color.white : Color.clear;
            var visible = _skin.VisibleBounds(Ae2Image.UnavailableSlotBadge);
            if (!overlay.sprite || visible is not { } visibleBounds)
            {
                overlay.gameObject.SetActive(false);
                return;
            }
            // Keep the red prohibition icon at its native aspect ratio while centering
            // it in the slot. This child never receives raycasts or drag/drop hits.
            overlay.transform.SetAsLastSibling();
            var size = Ae2UnavailableSlotBadge.Bounds((visibleBounds.Width, visibleBounds.Height), _layout.Scale);
            NativeFlatInventoryPanel.Position(overlay.rectTransform, new Rect(size.X, size.Y, size.Width, size.Height));
            overlay.gameObject.SetActive(true);
        }

        private InvButton? CreateButton(LoadoutCell cell)
        {
            var prefab = Resources.Load<GameObject>("ContainerItem");
            if (!prefab || !prefab.GetComponent<InvButton>())
            {
                if (!_warned) { _warned = true; _log.LogWarning("Native loadout slots require the ContainerItem prefab."); }
                return null;
            }
            var root = UnityEngine.Object.Instantiate(prefab, cell.transform);
            var button = root.GetComponent<InvButton>();
            button.centerButtons = false;
            button.woundView = false;
            var image = root.GetComponent<Image>();
            _skin.ApplyButtonFace(image);
            // Body buttons overwrite image.sprite every native UpdateGraphic(). Keep both faces transparent.
            button.normalSprite = button.mutedSprite = image.sprite;
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = cell.gameObject.layer;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            return button;
        }

        private static void HideExtra(List<LoadoutCell> cells, int count)
        {
            for (var index = count; index < cells.Count; ++index) cells[index].gameObject.SetActive(false);
        }

        private static string MainLabel(int index) => index == 0 ? TerminalText.Get("inventory.mainHand") :
            index == 1 ? TerminalText.Get("inventory.offHand") : TerminalText.Format("inventory.mainSlot", index);

        private static string SlotLabel(string id) => TerminalText.Get("slot." + id, id);

    }
}
