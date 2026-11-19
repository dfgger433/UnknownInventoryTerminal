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
        internal Image Face = null!;
        internal TextMeshProUGUI Caption = null!;
        internal InvButton? Button;
        internal NativeLoadoutPanel Owner = null!;

        public void OnPointerEnter(PointerEventData e) => Owner.Hover(this);
        public void OnPointerExit(PointerEventData e) => Owner.Leave(this);
        private void OnDisable() => Owner?.Leave(this);

        internal bool IsCurrent(PlayerCamera camera) => isActiveAndEnabled && Body && Body == camera.body;

        internal bool CanEquip(PlayerCamera camera, Item source) => IsCurrent(camera) && EquipmentId != null &&
            source && source.Stats.wearable && Ae2EquipmentSlots.ClaimsSlot(source.Stats.wearSlotId, EquipmentId) &&
            !string.IsNullOrEmpty(source.Stats.desiredWearLimb) && Body!.LimbByName(source.Stats.desiredWearLimb);
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
            _heading = NativeFlatInventoryPanel.CreateText(parent, "Equipment heading", 12, TextAlignmentOptions.MidlineLeft);
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
            _layout = layout;
            var scale = layout.Scale;
            var pitch = Ae2TerminalLayout.Pitch * scale;
            var left = Ae2TerminalLayout.Inset * scale;
            var width = Ae2TerminalLayout.GridWidth * scale;
            var top = Ae2TerminalLayout.DetailHeight * scale;
            NativeFlatInventoryPanel.Position(_main, new Rect(left, top, width, layout.MainRows * pitch));
            top += layout.MainRows * pitch;
            _heading.fontSize = 7 * scale;
            NativeFlatInventoryPanel.Position(_heading.rectTransform, new Rect(left, top, width, Ae2TerminalLayout.EquipmentHeader * scale));
            top += Ae2TerminalLayout.EquipmentHeader * scale;
            NativeFlatInventoryPanel.Position(_worn, new Rect(left, top, width, layout.EquipmentRows * pitch));
        }

        internal void Reset()
        {
            _body = null;
            _hover = null;
            _mainRow = _equipmentRow = 0;
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

        internal void Hover(LoadoutCell cell) => _hover = cell;
        internal void Leave(LoadoutCell cell) { if (_hover == cell) _hover = null; }

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
                var valid = index < mainCount && body.slots[index];
                var cell = Cell(_mainCells, _main, i, Ae2Image.MainSlot);
                Bind(cell, body, valid ? index : -1, null, valid ? body.GetItem(index) : null,
                    valid ? index.ToString(CultureInfo.InvariantCulture) : "", valid && body.slots[index].isHand && body.handSlot == index);
            }
            for (var i = 0; i < _layout.EquipmentRows * columns; ++i)
            {
                var index = _equipmentRow * columns + i;
                var id = index < _equipment.Count ? _equipment[index] : null;
                var cell = Cell(_equipmentCells, _worn, i, Ae2Image.EquipmentSlot);
                Item? item = null;
                if (id != null && !exact.TryGetValue(id, out item)) multi.TryGetValue(id, out item);
                Bind(cell, body, -1, id, item, item ? "" : id == null ? "" : SlotLabel(id), false);
            }
            HideExtra(_mainCells, _layout.MainRows * columns);
            HideExtra(_equipmentCells, _layout.EquipmentRows * columns);
        }

        private LoadoutCell Cell(List<LoadoutCell> cells, Transform parent, int i, Ae2Image slotArt)
        {
            if (i == cells.Count)
            {
                var root = NativeFlatInventoryPanel.Add(parent, "AE2 loadout cell", typeof(Image), typeof(LoadoutCell));
                var cell = root.GetComponent<LoadoutCell>();
                cell.Owner = this;
                cell.Face = root.GetComponent<Image>();
                _skin.Apply(cell.Face, slotArt, Ae2TerminalSkin.Slot, i % Ae2TerminalLayout.Columns);
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

        private void Bind(LoadoutCell cell, Body body, int index, string? id, Item? item, string caption, bool activeHand)
        {
            cell.Body = body;
            cell.MainIndex = index;
            cell.EquipmentId = id;
            cell.Face.color = activeHand ? Ae2TerminalSkin.Focus : index < 0 && id == null
                ? new Color32(164, 167, 180, 255) : cell.Face.sprite ? Color.white : Ae2TerminalSkin.Slot;
            var scale = _layout.Scale;
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
            cell.Caption.text = caption;
            cell.Caption.fontSize = (index >= 0 ? 6 : 5) * scale;
            cell.Caption.alignment = index >= 0 ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
            NativeFlatInventoryPanel.Stretch(cell.Caption.rectTransform, Vector2.one * scale, -Vector2.one * 2 * scale);
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
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = graphic == image;
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
