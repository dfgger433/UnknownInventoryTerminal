using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal enum ContainerPlacementMode { BestFit, WeightReduction, Manual }

    // Select a destination only. The native release path performs the actual transfer.
    internal static class ContainerPlacement
    {
        private const string Category = "UnknownInventoryTerminal";
        private static ConfigEntry<ContainerPlacementMode> _mode = null!;
        internal static ContainerPlacementMode Mode
        {
            get => Enum.IsDefined(typeof(ContainerPlacementMode), _mode.Value) ? _mode.Value : ContainerPlacementMode.BestFit;
            set => _mode.Value = value;
        }

        internal static void Initialize(ConfigFile config)
        {
            _mode = config.Bind(Category, "ContainerPlacement", ContainerPlacementMode.BestFit,
                "Default destination for an item dropped on the terminal. A drop on a container card targets that container.");
            ModOptionsRegistry.Register(ModOptionDefinition.Dropdown(Category + ".ContainerPlacement", "Container placement",
                "Choose the default destination for a drop on the terminal.", Category, (int)Mode, new[] {
                    new ModDropdownChoice("BestFit", "Smallest remaining capacity"),
                    new ModDropdownChoice("WeightReduction", "Highest weight reduction"),
                    new ModDropdownChoice("Manual", "Choose container manually")
                }, value => { if (value >= 0 && value <= 2) Mode = (ContainerPlacementMode)value; }));
        }

        internal sealed class Candidate
        {
            internal readonly Item Item;
            internal readonly float RemainingCapacity, WeightReduction;
            internal Candidate(Item item, float remainingCapacity, float weightReduction)
            { Item = item; RemainingCapacity = remainingCapacity; WeightReduction = weightReduction; }
        }

        internal static IReadOnlyList<Item> Containers(Body body) => body.GetSurfaceInventoryItems()
            .Where(item => item && item.container && !item.TryGetParentContainer(out _))
            .GroupBy(item => item.GetInstanceID()).Select(group => group.First())
            .OrderBy(item => item.fullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.GetInstanceID()).ToArray();

        internal static IReadOnlyList<Candidate> Candidates(Body body, Item source)
        {
            if (!body || !source || !GameInventoryAdapter.Owns(body, source)) return Array.Empty<Candidate>();
            var sourceMultiplier = source.TryGetParentContainer(out var parent) ? parent.encumberanceMult : 1f;
            var candidates = new List<Candidate>();
            foreach (var item in Containers(body))
            {
                if (!CanPlace(body, source, item)) continue;
                var container = item.container;
                candidates.Add(new Candidate(item,
                    container.maxWeight - container.GetHoldingWeight() - source.totalWeight,
                    source.totalWeight * (sourceMultiplier - container.encumberanceMult)));
            }

            var ordered = Mode == ContainerPlacementMode.WeightReduction
                ? candidates.OrderByDescending(candidate => candidate.WeightReduction).ThenBy(candidate => candidate.RemainingCapacity)
                : Mode == ContainerPlacementMode.Manual
                    ? candidates.OrderBy(candidate => candidate.Item.fullName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidate => candidate.RemainingCapacity)
                    : candidates.OrderBy(candidate => candidate.RemainingCapacity).ThenByDescending(candidate => candidate.WeightReduction);
            return ordered.ThenBy(candidate => candidate.Item.GetInstanceID()).ToArray();
        }

        internal static bool CanPlace(Body body, Item source, Item destination)
        {
            if (!body || !source || !destination || source == destination || !destination.container ||
                !GameInventoryAdapter.Owns(body, source) || !GameInventoryAdapter.Owns(body, destination)) return false;
            if (destination.TryGetParentContainer(out _) || destination.transform.IsChildOf(source.transform) ||
                source.transform.parent == destination.transform) return false;
            if (source.container && source.container.GetHoldingWeight() > 0f) return false;
            if (Vector2.Distance(source.transform.position, destination.transform.position) >= 10f) return false;
            if (destination.battery && (source.Stats.HasTag("tool") || source.Stats.HasTag("battery"))) return false;
            return destination.container.CanHoldItem(source);
        }
    }

    // Revalidated every frame and again at release; each hit remains a native InvButton.
    internal sealed class ContainerPlacementTarget : MonoBehaviour
    {
        internal Item Source = null!;
        internal bool IsValid(PlayerCamera camera)
        {
            var button = GetComponent<InvButton>();
            return camera && camera.body && Source && camera.dragItem == Source && button &&
                !Input.GetKey(KeyBinds.GetBind("expanddesc")) &&
                ContainerPlacement.CanPlace(camera.body, Source, button.GetItem());
        }
    }

    internal sealed class ContainerPlacementCard : MonoBehaviour, IPointerClickHandler
    {
        internal ContainerPlacementPanel Owner = null!;
        internal Item Item = null!;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && PlayerCamera.main && !PlayerCamera.main.dragItem)
                Owner.Select(Item);
        }
    }

    internal sealed class ContainerPlacementPanel : IDisposable
    {
        internal const float Height = 108;
        internal const float BottomGap = 8;

        private sealed class Target
        {
            internal readonly GameObject Root;
            internal readonly Button CardButton;
            internal readonly InvButton ItemButton;
            internal readonly ContainerPlacementTarget Marker;
            internal readonly TextMeshProUGUI Label;
            internal Target(GameObject root, Button cardButton, InvButton itemButton,
                ContainerPlacementTarget marker, TextMeshProUGUI label)
            { Root = root; CardButton = cardButton; ItemButton = itemButton; Marker = marker; Label = label; }
        }

        private readonly ManualLogSource _log;
        private readonly List<Target> _targets = new List<Target>();
        private GameObject? _root;
        private RectTransform? _content;
        private TextMeshProUGUI? _hint;
        private Item? _selected;
        private bool _warned;

        internal ContainerPlacementPanel(ManualLogSource log) { _log = log; }
        internal void ClearSelection() => _selected = null;

        internal void Attach(Transform parent)
        {
            if (_root) return;
            _root = new GameObject("Container storage targets", typeof(RectTransform));
            _root.layer = parent.gameObject.layer;
            _root.transform.SetParent(parent, false);
            var rect = _root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(-16, Height);
            rect.anchoredPosition = new Vector2(0, -8);

            var heading = Text(_root.transform, "Containers heading", 13);
            heading.text = "CONTAINERS  ·  DROP AN ITEM ANYWHERE IN STORAGE";
            heading.rectTransform.anchorMin = new Vector2(0, 1);
            heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.offsetMin = new Vector2(0, -24);
            heading.rectTransform.offsetMax = Vector2.zero;

            var viewport = new GameObject("Container cards viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.layer = _root.layer; viewport.transform.SetParent(_root.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, Vector2.zero, new Vector2(0, -28));
            viewport.GetComponent<Image>().color = new Color(0.12f, 0.17f, 0.19f, 0.8f);
            var content = new GameObject("Container cards", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.layer = _root.layer; content.transform.SetParent(viewport.transform, false);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 1); _content.sizeDelta = new Vector2(0, Height - 30);
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.spacing = 6; layout.padding = new RectOffset(2, 2, 2, 2);
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect; scroll.content = _content;
            scroll.horizontal = true; scroll.vertical = false; scroll.inertia = false;
            scroll.scrollSensitivity = 30; scroll.movementType = ScrollRect.MovementType.Clamped;

            _hint = Text(_root.transform, "Container status", 11);
            _hint.rectTransform.anchorMin = new Vector2(0, 0);
            _hint.rectTransform.anchorMax = new Vector2(1, 0);
            _hint.rectTransform.pivot = new Vector2(0.5f, 0);
            _hint.rectTransform.offsetMin = new Vector2(4, 0);
            _hint.rectTransform.offsetMax = new Vector2(-4, 16);
        }

        internal InvButton? ResolveDrop(PlayerCamera camera, IReadOnlyList<RaycastResult> hits)
        {
            var source = camera.dragItem;
            if (!source || Input.GetKey(KeyBinds.GetBind("expanddesc"))) return null;
            try
            {
                foreach (var hit in hits)
                {
                    if (!hit.gameObject) continue;
                    var target = _targets.FirstOrDefault(candidate => hit.gameObject.transform == candidate.Root.transform ||
                        hit.gameObject.transform.IsChildOf(candidate.Root.transform));
                    if (target != null && ContainerPlacement.CanPlace(camera.body, source, target.ItemButton.refItem))
                        return target.ItemButton;
                }

                var recommended = Recommended(camera.body, source);
                var chosen = _targets.FirstOrDefault(target => target.Root.activeInHierarchy && target.ItemButton.refItem == recommended);
                return chosen != null && chosen.Marker.IsValid(camera) ? chosen.ItemButton : null;
            }
            catch (Exception ex)
            {
                Warn(ex);
                return null;
            }
        }

        private Item? Recommended(Body body, Item source)
        {
            if (ContainerPlacement.Mode == ContainerPlacementMode.Manual && _selected &&
                ContainerPlacement.CanPlace(body, source, _selected!)) return _selected;
            return ContainerPlacement.Candidates(body, source).FirstOrDefault()?.Item;
        }

        internal void Refresh()
        {
            if (!_root || !_hint || !PlayerCamera.main || !PlayerCamera.main.body) return;
            var camera = PlayerCamera.main;
            var source = camera.dragItem;
            try
            {
                var containers = ContainerPlacement.Containers(camera.body);
                if (_selected && !containers.Contains(_selected!)) _selected = null;
                var blocked = source && Input.GetKey(KeyBinds.GetBind("expanddesc"));
                var recommended = source && !blocked ? Recommended(camera.body, source) : null;
                _hint!.text = containers.Count == 0 ? "No carried containers" : blocked ? "Release expanded-description key to place one item" :
                    source && !recommended ? "No compatible container for this item" :
                    ContainerPlacement.Mode == ContainerPlacementMode.Manual && !source ? "Select a container, then drop items anywhere in storage" : "";
                _hint.gameObject.SetActive(!string.IsNullOrEmpty(_hint.text));

                for (var index = 0; index < containers.Count; index++)
                {
                    var item = containers[index];
                    var target = index < _targets.Count ? _targets[index] : CreateTarget();
                    target.CardButton.GetComponent<ContainerPlacementCard>().Item = item;
                    target.ItemButton.refItem = item;
                    target.Marker.Source = source;
                    target.CardButton.interactable = !source;
                    target.Root.SetActive(true);
                    target.ItemButton.UpdateGraphic();
                    var container = item.container;
                    target.Label.text = item.fullName + "\n" + Number(container.GetHoldingWeight()) + "/" +
                        Number(container.maxWeight) + "u   ·   Weight x" + Number(container.encumberanceMult);
                    var highlighted = source ? recommended == item : ContainerPlacement.Mode == ContainerPlacementMode.Manual && _selected == item;
                    target.Root.GetComponent<Image>().color = highlighted
                        ? new Color(0.26f, 0.47f, 0.44f, 1) : new Color(0.20f, 0.25f, 0.29f, 1);
                    target.Marker.enabled = source && !blocked && ContainerPlacement.CanPlace(camera.body, source, item);
                }
                for (var index = containers.Count; index < _targets.Count; index++) _targets[index].Root.SetActive(false);
            }
            catch (Exception ex)
            {
                foreach (var target in _targets) if (target.Root) target.Root.SetActive(false);
                _hint!.text = "Containers unavailable; check the BepInEx log.";
                _hint.gameObject.SetActive(true);
                Warn(ex);
            }
        }

        internal void Select(Item item)
        {
            if (item && item.container) _selected = item;
            Refresh();
        }

        private Target CreateTarget()
        {
            var prefab = Resources.Load<GameObject>("ContainerItem");
            if (!prefab) throw new InvalidOperationException("Native ContainerItem prefab is unavailable.");
            var root = new GameObject("Container card", typeof(RectTransform), typeof(Image), typeof(Button));
            root.layer = _root!.layer; root.transform.SetParent(_content, false);
            root.GetComponent<Image>().color = new Color(0.20f, 0.25f, 0.29f, 1);
            var rootRect = root.GetComponent<RectTransform>(); rootRect.sizeDelta = new Vector2(210, Height - 34);
            var cardButton = root.GetComponent<Button>();
            cardButton.onClick.AddListener(() =>
            {
                var card = root.GetComponent<ContainerPlacementCard>();
                if (card) Select(card.Item);
            });
            var card = root.AddComponent<ContainerPlacementCard>(); card.Owner = this;

            var native = UnityEngine.Object.Instantiate(prefab, root.transform);
            var button = native.GetComponent<InvButton>();
            if (!button) throw new InvalidOperationException("ContainerItem has no InvButton.");
            button.slot = -1; button.centerButtons = false; button.woundView = false; button.maxImageSize = 46;
            var nativeRect = native.GetComponent<RectTransform>();
            nativeRect.anchorMin = nativeRect.anchorMax = new Vector2(0, 0.5f);
            nativeRect.pivot = new Vector2(0, 0.5f); nativeRect.anchoredPosition = new Vector2(5, 0);
            nativeRect.sizeDelta = new Vector2(58, 58);
            foreach (var child in native.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
            foreach (var graphic in native.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = graphic.gameObject == native;
            var marker = native.AddComponent<ContainerPlacementTarget>();
            var label = Text(root.transform, "Container name and capacity", 11);
            label.raycastTarget = false;
            Stretch(label.rectTransform, new Vector2(68, 3), new Vector2(-5, -3));
            return AddTarget(new Target(root, cardButton, button, marker, label));
        }

        private Target AddTarget(Target target) { _targets.Add(target); return target; }
        private void Warn(Exception ex)
        {
            if (_warned) return;
            _warned = true;
            _log.LogWarning("Container card creation or refresh failed: " + ex.Message);
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max; }
        private static TextMeshProUGUI Text(Transform parent, string name, float size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.layer = parent.gameObject.layer; root.transform.SetParent(parent, false);
            var text = root.GetComponent<TextMeshProUGUI>();
            text.font = PlayerCamera.main && PlayerCamera.main.containerText ? PlayerCamera.main.containerText.font : TMP_Settings.defaultFontAsset;
            text.fontSize = size; text.color = new Color(0.90f, 0.93f, 0.95f, 1);
            text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false; text.richText = false;
            text.overflowMode = TextOverflowModes.Ellipsis; text.enableWordWrapping = false;
            return text;
        }

        public void Dispose()
        {
            if (_root) { _root!.SetActive(false); UnityEngine.Object.Destroy(_root); }
            _root = null; _content = null; _hint = null; _targets.Clear(); _selected = null;
        }
    }
}
