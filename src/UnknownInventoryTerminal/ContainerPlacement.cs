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
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    internal enum ContainerPlacementMode { BestFit, WeightReduction, Manual }

    // Recommendations only: no item parenting, LoadItem calls, or network state is changed here.
    internal static class ContainerPlacement
    {
        private const string Category = "UnknownInventoryTerminal";
        private static ConfigEntry<ContainerPlacementMode> _mode = null!;
        internal static ContainerPlacementMode Mode
        {
            get => Enum.IsDefined(typeof(ContainerPlacementMode), _mode.Value) ? _mode.Value : ContainerPlacementMode.BestFit;
            set => _mode.Value = value;
        }
        internal static string ModeLabel => Mode == ContainerPlacementMode.BestFit ? "Place: smallest remaining capacity" :
            Mode == ContainerPlacementMode.WeightReduction ? "Place: highest weight reduction" : "Place: choose container manually";

        internal static void Initialize(ConfigFile config)
        {
            _mode = config.Bind(Category, "ContainerPlacement", ContainerPlacementMode.BestFit,
                "Terminal container targets: BestFit (least free capacity after insertion), WeightReduction (largest encumbrance decrease), or Manual. Drop on the displayed native container icon to confirm.");
            ModOptionsRegistry.Register(ModOptionDefinition.Dropdown(Category + ".ContainerPlacement", "Container placement",
                "Choose how the terminal recommends a container. All transfers use the original inventory controls.", Category,
                (int)Mode, new[] {
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

        internal static IReadOnlyList<Candidate> Candidates(Body body, Item source)
        {
            if (!body || !source || !GameInventoryAdapter.Owns(body, source)) return Array.Empty<Candidate>();
            var sourceMultiplier = source.TryGetParentContainer(out var parent) ? parent.encumberanceMult : 1f;
            var seen = new HashSet<int>();
            var candidates = new List<Candidate>();
            // Native LoadItem disallows inserting into a container which is itself contained.
            foreach (var item in body.GetSurfaceInventoryItems())
            {
                if (!item || !seen.Add(item.GetInstanceID()) || !CanPlace(body, source, item)) continue;
                var container = item.container;
                candidates.Add(new Candidate(item, container.maxWeight - container.GetHoldingWeight() - source.totalWeight,
                    source.totalWeight * (sourceMultiplier - container.encumberanceMult)));
            }
            // Stable tie-breakers avoid moving the recommended target when the inventory refreshes.
            return (Mode == ContainerPlacementMode.WeightReduction
                ? candidates.OrderByDescending(c => c.WeightReduction).ThenBy(c => c.RemainingCapacity)
                : Mode == ContainerPlacementMode.Manual
                    ? candidates.OrderBy(c => c.Item.Stats.fullName, StringComparer.Ordinal).ThenBy(c => c.RemainingCapacity)
                    : candidates.OrderBy(c => c.RemainingCapacity).ThenByDescending(c => c.WeightReduction))
                .ThenBy(c => c.Item.GetInstanceID()).ToArray();
        }

        internal static bool CanPlace(Body body, Item source, Item destination)
        {
            if (!body || !source || !destination || source == destination || !destination.container ||
                !GameInventoryAdapter.Owns(body, source) || !GameInventoryAdapter.Owns(body, destination)) return false;
            if (destination.TryGetParentContainer(out _) || destination.transform.IsChildOf(source.transform) ||
                source.transform.parent == destination.transform) return false;
            // Match Container.LoadItem's stacking/range rules in addition to CanHoldItem's
            // total weight, per-item weight, and tag restrictions. No hypothetical mutations.
            if (source.container && source.container.GetHoldingWeight() > 0f) return false;
            if (Vector2.Distance(source.transform.position, destination.transform.position) >= 10f) return false;
            // The battery branch precedes the container branch in the native action handler.
            if (destination.battery && (source.Stats.HasTag("tool") || source.Stats.HasTag("battery"))) return false;
            return destination.container.CanHoldItem(source);
        }
    }

    // Attached to the exact native InvButton hit object. Rechecked at raycast/release time,
    // because a displayed recommendation can outlive a capacity or ownership change.
    internal sealed class ContainerPlacementTarget : MonoBehaviour
    {
        internal Item Source = null!;
        internal bool IsValid(PlayerCamera camera)
        {
            var button = GetComponent<InvButton>();
            return camera && camera.body && camera.dragItem == Source && button &&
                !Input.GetKey(KeyBinds.GetBind("expanddesc")) &&
                ContainerPlacement.CanPlace(camera.body, Source, button.GetItem());
        }
    }

    internal sealed class ContainerPlacementPanel : IDisposable
    {
        private sealed class Target
        {
            internal readonly GameObject Root;
            internal readonly InvButton Button;
            internal readonly ContainerPlacementTarget Marker;
            internal readonly TextMeshProUGUI Label;
            internal Target(GameObject root, InvButton button, ContainerPlacementTarget marker, TextMeshProUGUI label)
            { Root = root; Button = button; Marker = marker; Label = label; }
        }
        private readonly ManualLogSource _log;
        private readonly List<Target> _targets = new List<Target>();
        private GameObject? _root;
        private RectTransform? _content;
        private TextMeshProUGUI? _modeLabel, _hint;
        private Button? _modeButton;
        private ScrollRect? _scroll;
        private bool _warned;

        internal ContainerPlacementPanel(ManualLogSource log) { _log = log; }

        internal void Attach(Transform parent)
        {
            if (_root) return;
            _root = new GameObject("Container placement targets", typeof(RectTransform));
            _root.layer = parent.gameObject.layer;
            _root.transform.SetParent(parent, false);
            var rect = _root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(-16, 108);
            rect.anchoredPosition = new Vector2(0, -8);

            var mode = new GameObject("Choose container rule", typeof(RectTransform), typeof(Image), typeof(Button));
            mode.layer = _root.layer; mode.transform.SetParent(_root.transform, false);
            var modeRect = mode.GetComponent<RectTransform>();
            modeRect.anchorMin = new Vector2(0, 1); modeRect.anchorMax = Vector2.one;
            modeRect.pivot = new Vector2(0.5f, 1);
            modeRect.offsetMin = new Vector2(270, -30); modeRect.offsetMax = Vector2.zero;
            mode.GetComponent<Image>().color = new Color(0.24f, 0.29f, 0.34f, 1);
            _modeButton = mode.GetComponent<Button>();
            _modeButton.onClick.AddListener(() => ContainerPlacement.Mode = (ContainerPlacementMode)(((int)ContainerPlacement.Mode + 1) % 3));
            _modeLabel = Text(mode.transform, "Placement rule", 13);
            Stretch(_modeLabel.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));

            var viewport = new GameObject("Container targets viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.layer = _root.layer; viewport.transform.SetParent(_root.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, Vector2.zero, new Vector2(0, -36));
            viewport.GetComponent<Image>().color = new Color(0.15f, 0.20f, 0.24f, 1);
            var content = new GameObject("Native container targets", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.layer = _root.layer; content.transform.SetParent(viewport.transform, false);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 1); _content.sizeDelta = new Vector2(0, 72);
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.spacing = 8;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = viewport.GetComponent<ScrollRect>();
            _scroll.viewport = viewportRect; _scroll.content = _content;
            _scroll.horizontal = true; _scroll.vertical = false; _scroll.inertia = false;
            _scroll.scrollSensitivity = 30; _scroll.movementType = ScrollRect.MovementType.Clamped;
            _hint = Text(viewport.transform, "Placement instructions", 13);
            Stretch(_hint.rectTransform, new Vector2(8, 2), new Vector2(-8, -2));
        }

        internal void Refresh()
        {
            if (!_root || !_hint || !_modeLabel || !PlayerCamera.main || !PlayerCamera.main.body) return;
            _modeLabel!.text = ContainerPlacement.ModeLabel + "  >";
            var camera = PlayerCamera.main;
            var source = camera.dragItem;
            try
            {
                var candidates = source ? ContainerPlacement.Candidates(camera.body, source) : Array.Empty<ContainerPlacement.Candidate>();
                var count = ContainerPlacement.Mode == ContainerPlacementMode.Manual ? candidates.Count : Math.Min(1, candidates.Count);
                _hint!.text = !source ? "Drag an item onto a container icon here. Manual mode lists every compatible container (scroll horizontally)." :
                    Input.GetKey(KeyBinds.GetBind("expanddesc")) ? "Release the expanded-description key to place a single item." :
                    count == 0 ? "No compatible container: check capacity, item tags, and container nesting." : "";
                _hint.gameObject.SetActive(_hint.text.Length > 0);
                if (Input.GetKey(KeyBinds.GetBind("expanddesc"))) count = 0;
                for (var index = 0; index < count; index++)
                {
                    var candidate = candidates[index];
                    var target = index < _targets.Count ? _targets[index] : CreateTarget();
                    // Source remains a real reference; count/merged groups never become item actions.
                    target.Marker.Source = source;
                    target.Button.refItem = candidate.Item;
                    target.Root.SetActive(true);
                    target.Button.UpdateGraphic();
                    target.Label.text = candidate.Item.fullName + "\nFree after: " + Number(candidate.RemainingCapacity) +
                        "  |  Reduction: " + Number(candidate.WeightReduction);
                }
                for (var index = count; index < _targets.Count; index++) _targets[index].Root.SetActive(false);
            }
            catch (Exception ex)
            {
                foreach (var target in _targets) if (target.Root) target.Root.SetActive(false);
                _hint!.text = "Container targets are unavailable. Check the BepInEx log.";
                _hint.gameObject.SetActive(true);
                if (!_warned) _log.LogWarning("Container target creation failed: " + ex.Message);
                _warned = true;
            }
        }

        private Target CreateTarget()
        {
            var prefab = Resources.Load<GameObject>("ContainerItem");
            if (!prefab) throw new InvalidOperationException("Native ContainerItem prefab is unavailable.");
            var root = new GameObject("Container drop target", typeof(RectTransform));
            root.layer = _root!.layer; root.transform.SetParent(_content, false);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 72);
            var native = UnityEngine.Object.Instantiate(prefab, root.transform);
            var button = native.GetComponent<InvButton>();
            button.slot = -1; button.centerButtons = false; button.woundView = false; button.maxImageSize = 50;
            var nativeRect = native.GetComponent<RectTransform>();
            nativeRect.anchorMin = nativeRect.anchorMax = new Vector2(0, 0.5f);
            nativeRect.pivot = new Vector2(0, 0.5f); nativeRect.anchoredPosition = new Vector2(4, 0);
            nativeRect.sizeDelta = new Vector2(64, 64);
            foreach (var child in native.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
            foreach (var graphic in native.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = graphic.gameObject == native;
            var marker = native.AddComponent<ContainerPlacementTarget>();
            var label = Text(root.transform, "Container capacity and reduction", 12);
            label.richText = true;
            Stretch(label.rectTransform, new Vector2(76, 2), new Vector2(-4, -2));
            var target = new Target(root, button, marker, label);
            _targets.Add(target);
            return target;
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
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        public void Dispose()
        {
            if (_root) { _root!.SetActive(false); UnityEngine.Object.Destroy(_root); }
            _root = null; _content = null; _modeLabel = null; _hint = null;
            _modeButton = null; _scroll = null; _targets.Clear();
        }
    }
}
