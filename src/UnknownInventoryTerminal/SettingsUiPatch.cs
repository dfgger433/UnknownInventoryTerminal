using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnknownInventoryTerminal
{
    // Repair only this mod's rows after the game has built them and CCL has adjusted the templates.
    internal static class SettingsUiPatch
    {
        private const string PlacementId = SettingsBridge.Category + ".ContainerPlacement";
        private const string PlacementLabel = "Container placement";
        private const string RailFrameName = "CUCoreLibModOptionTabFrame";
        private const string RailScrollName = "CUCoreLibModOptionTabs";

        private sealed class ScrollbarState
        {
            internal ScrollRect.ScrollbarVisibility Original;
        }

        private static readonly ConditionalWeakTable<ScrollRect, ScrollbarState> OriginalScrollbarVisibility = new();
        private static readonly (string Key, string Label)[] PlacementChoices =
        {
            ("BestFit", "Smallest remaining capacity"),
            ("WeightReduction", "Highest weight reduction"),
            ("Manual", "Choose container manually")
        };

        private static ManualLogSource? _log;
        private static bool _warned;

        internal static void Install(Harmony harmony, ManualLogSource log)
        {
            _log = log;
            _warned = false;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab),
                    new[] { typeof(Setting.SettingCategory) });
                if (target == null || AccessTools.DeclaredField(typeof(SettingsMenu), "spawnedSettings") == null)
                {
                    log.LogWarning("Settings UI compatibility patch skipped: unsupported native settings menu.");
                    return;
                }
                var postfix = new HarmonyMethod(typeof(SettingsUiPatch), nameof(AfterSelectTab))
                {
                    after = new[] { "net.cucorelib" },
                    priority = Priority.Last
                };
                harmony.Patch(target, postfix: postfix);
            }
            catch (Exception ex)
            {
                log.LogWarning("Settings UI compatibility patch unavailable: " + ex.GetBaseException().Message);
            }
        }

        private static void AfterSelectTab(SettingsMenu __instance, Setting.SettingCategory category,
            List<GameObject> ___spawnedSettings)
        {
            if (!__instance) return;
            try
            {
                UpdateScrollbarVisibility(__instance, category);
                if (!__instance.content || ___spawnedSettings == null) return;
                int rowIndex = 0;
                foreach (var setting in Settings.GetAllSettings())
                {
                    // CCL handles text rows separately, appending them after the native rows.
                    if (setting == null || setting.category != category ||
                        !(setting is SettingBool || setting is SettingKeybind || setting is SettingDropdown ||
                          setting is SettingFloat || setting is SettingInt)) continue;
                    if (rowIndex >= ___spawnedSettings.Count) break;
                    var row = ___spawnedSettings[rowIndex++];
                    string? fallback = setting.name switch
                    {
                        SettingsBridge.ToggleKeyId when setting is SettingKeybind => SettingsBridge.ToggleKeyLabel,
                        SettingsBridge.ByContainerId when setting is SettingBool => SettingsBridge.ByContainerLabel,
                        SettingsBridge.PinyinId when setting is SettingBool => SettingsBridge.PinyinLabel,
                        PlacementId when setting is SettingDropdown => PlacementLabel,
                        _ => null
                    };
                    if (fallback == null || !row || row.transform.parent != __instance.content ||
                        row.transform.childCount < 2) continue;

                    var label = row.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                    if (!label) continue;
                    var control = row.transform.GetChild(1);
                    if (setting is SettingDropdown && !control.GetComponent<TMP_Dropdown>() ||
                        setting is SettingKeybind && !control.GetComponent<Button>() ||
                        setting is SettingBool && !control.GetComponent<Toggle>()) continue;
                    RepairRowSurface(row.GetComponent<Image>());
                    EnsureText(label, "gameset" + setting.name, fallback);

                    if (setting is SettingKeybind)
                    {
                        var caption = control.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (caption) EnsureText(caption, null, SettingsBridge.ToggleKey.Value.ToString());
                    }
                    else if (setting is SettingDropdown)
                    {
                        RepairPlacementDropdown(control.GetComponent<TMP_Dropdown>());
                    }
                }
            }
            catch (Exception ex)
            {
                if (_warned) return;
                _warned = true;
                _log?.LogWarning("Could not normalize the terminal settings rows: " + ex.GetBaseException().Message);
            }
        }

        private static void SetAutoHide(ScrollRect scroll)
        {
            if (!OriginalScrollbarVisibility.TryGetValue(scroll, out _))
                OriginalScrollbarVisibility.Add(scroll, new ScrollbarState { Original = scroll.verticalScrollbarVisibility });
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static void UpdateScrollbarVisibility(SettingsMenu menu, Setting.SettingCategory category)
        {
            var content = menu.content;
            if (!content) return;
            var categoryOwned = false;
            foreach (var setting in Settings.GetAllSettings())
            {
                if (setting != null && setting.category == category &&
                    (setting.name == SettingsBridge.ToggleKeyId || setting.name == SettingsBridge.ByContainerId ||
                     setting.name == SettingsBridge.PinyinId || setting.name == PlacementId))
                {
                    categoryOwned = true;
                    break;
                }
            }

            var contentScroll = content.GetComponentInParent<ScrollRect>();
            if (contentScroll && contentScroll.content == content)
            {
                if (categoryOwned)
                {
                    SetAutoHide(contentScroll);
                }
                else if (OriginalScrollbarVisibility.TryGetValue(contentScroll, out var state))
                {
                    contentScroll.verticalScrollbarVisibility = state.Original;
                }
            }

            // CCL's custom category rail is rebuilt with Permanent visibility. It is
            // independent from the form scroll and should only occupy space when needed.
            foreach (var scroll in menu.GetComponentsInChildren<ScrollRect>(true))
            {
                var parent = scroll.transform.parent;
                if (scroll.name == RailScrollName && parent && parent.name == RailFrameName)
                    SetAutoHide(scroll);
            }
        }

        private static void RepairRowSurface(Image image)
        {
            if (!image) return;
            var color = image.color;
            // Leave visible stock/theme surfaces alone; don't restyle the game's dimmer or other tabs.
            if (color.a >= 0.5f && Mathf.Max(color.r, Mathf.Max(color.g, color.b)) >= 0.12f) return;
            image.color = new Color32(38, 45, 48, 255);
        }

        private static void RepairPlacementDropdown(TMP_Dropdown dropdown)
        {
            if (!dropdown) return;
            if (dropdown.template)
            {
                var scroll = dropdown.template.GetComponent<ScrollRect>();
                var viewport = scroll?.viewport;
                if (viewport && Mathf.Approximately(viewport!.anchorMin.y, 0f) &&
                    Mathf.Approximately(viewport.anchorMax.y, 1f) &&
                    (Mathf.Approximately(viewport.sizeDelta.y, 200f) || Mathf.Approximately(viewport.sizeDelta.y, 0f)))
                {
                    // CCL puts 200 on a stretched child, adding 200 to the popup instead of capping it.
                    viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, 0f);
                    RepairDropdownContentHeight(scroll!.content);
                }
            }

            if (dropdown.options.Count == PlacementChoices.Length)
            {
                for (int i = 0; i < PlacementChoices.Length; i++)
                {
                    var (key, fallback) = PlacementChoices[i];
                    var option = dropdown.options[i];
                    if (option != null && IsEmptyLabel(option.text, "gameset" + PlacementId + key))
                        option.text = fallback;
                }
            }
            dropdown.RefreshShownValue();
            if (dropdown.captionText) EnsureText(dropdown.captionText, null, string.Empty);
            if (dropdown.itemText) EnsureText(dropdown.itemText, null, string.Empty);
        }

        private static void RepairDropdownContentHeight(RectTransform? content)
        {
            if (!content || content!.childCount == 0) return;
            var item = content.GetChild(0) as RectTransform;
            if (!item) return;

            // The stock template is top anchored with one 50px item centered at -25.
            // Reduce the serialized 710px content to the actual item bounds so TMP's
            // runtime clone can size the popup to its three options without empty scroll space.
            var itemRect = item!.rect;
            var topInset = Mathf.Max(0f, item.anchoredPosition.y + itemRect.height * (1f - item.pivot.y));
            var bottomInset = Mathf.Max(0f, -item.anchoredPosition.y + itemRect.height * item.pivot.y);
            var height = Mathf.Max(itemRect.height, topInset + bottomInset);
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        private static bool IsEmptyLabel(string? text, string? key) =>
            string.IsNullOrWhiteSpace(text) || (key != null && string.Equals(text, key, StringComparison.Ordinal));

        private static void EnsureText(TMP_Text label, string? key, string fallback)
        {
            if (IsEmptyLabel(label.text, key) && !string.IsNullOrEmpty(fallback)) label.text = fallback;
            if (!label.font && TMP_Settings.defaultFontAsset) label.font = TMP_Settings.defaultFontAsset;
            label.color = Color.white;
            var maximum = Mathf.Max(1f, label.enableAutoSizing ? label.fontSizeMax : label.fontSize);
            label.fontSizeMin = Mathf.Min(12f, maximum);
            label.fontSizeMax = maximum;
            label.enableAutoSizing = true;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}