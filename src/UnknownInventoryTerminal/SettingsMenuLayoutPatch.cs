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
    internal static class SettingsMenuLayoutPatch
    {
        [HarmonyPatch(typeof(SettingsMenu), "Start")]
        private static class MenuStart
        {
            [HarmonyPostfix, HarmonyAfter("net.cucorelib"), HarmonyPriority(Priority.Last)]
            private static void Postfix(SettingsMenu __instance) => ConfigureScrollbars(__instance);
        }

        [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.SelectTab), typeof(Setting.SettingCategory))]
        private static class TabSelection
        {
            [HarmonyPostfix, HarmonyAfter("net.cucorelib"), HarmonyBefore("org.bepinex.plugins.qol_unknown"), HarmonyPriority(Priority.Last)]
            private static void Postfix(SettingsMenu __instance, Setting.SettingCategory category, List<GameObject> ___spawnedSettings)
            {
                ConfigureScrollbars(__instance);
                if (category != SettingsBridge.MenuCategory || !__instance.content) return;

                // Native rows come first; CCL defers string settings and appends them afterward.
                var terminalRows = new List<RectTransform>(5);
                var updateText = new List<Action>();
                TMP_Dropdown? languageDropdown = null;
                int index = 0;
                foreach (var setting in Settings.GetAllSettings())
                {
                    if (setting == null || setting.category != category || !(setting is SettingBool || setting is SettingKeybind ||
                        setting is SettingDropdown || setting is SettingFloat || setting is SettingInt)) continue;
                    if (index >= ___spawnedSettings.Count) break;
                    var row = ___spawnedSettings[index++];
                    if (!row || row.transform.parent != __instance.content || row.transform.childCount < 2 ||
                        !(setting.name?.StartsWith(SettingsBridge.ConfigSection + ".", StringComparison.Ordinal) ?? false)) continue;
                    if (!(row.transform is RectTransform rect)) continue;
                    terminalRows.Add(rect);
                    var label = row.transform.GetChild(0).GetComponent<TMP_Text>();
                    var control = row.transform.GetChild(1);
                    if (!label || !(control is RectTransform field)) continue;
                    FitRow(rect, label.rectTransform, field, setting is SettingBool);
                    FitText(label);
                    var option = setting.name.Substring(SettingsBridge.ConfigSection.Length + 1);
                    var tooltip = label.GetComponent<UITooltip>();
                    updateText.Add(() =>
                    {
                        if (!label) return;
                        label.text = SettingsBridge.Label(option);
                        if (!tooltip) return;
                        tooltip.tipName = label.text;
                        tooltip.tipDesc = SettingsBridge.Description(option);
                    });
                    if (setting is SettingDropdown dropdownSetting && control.TryGetComponent<TMP_Dropdown>(out var dropdown))
                    {
                        FitText(dropdown.captionText);
                        FitText(dropdown.itemText);
                        if (__instance.content.parent is RectTransform viewport) FitPopup(dropdown, viewport);
                        if (option == SettingsBridge.LanguageSetting) languageDropdown = dropdown;
                        updateText.Add(() =>
                        {
                            if (!dropdown || dropdown.options.Count != dropdownSetting.choices.Length) return;
                            for (var i = 0; i < dropdown.options.Count; i++)
                                dropdown.options[i].text = SettingsBridge.ChoiceLabel(option, dropdownSetting.choices[i]);
                            dropdown.RefreshShownValue();
                        });
                    }
                    else if (setting is SettingKeybind) FitText(control.GetComponentInChildren<TMP_Text>(true));
                }
                void RefreshText() { foreach (var update in updateText) update(); }
                RefreshText();
                // The stock listener applies and persists the option before this listener refreshes the text.
                if (languageDropdown) languageDropdown!.onValueChanged.AddListener(_ => RefreshText());
                PinTerminalRows(__instance.content, ___spawnedSettings, terminalRows);
            }
        }

        private static void PinTerminalRows(RectTransform content, List<GameObject> rows, List<RectTransform> terminalRows)
        {
            if (terminalRows.Count == 0) return;
            // Keep CCL's row-to-setting indices intact; only move the rendered rows.
            float pinnedHeight = 0f;
            foreach (var terminal in terminalRows) pinnedHeight += terminal.rect.height;
            float above = 0f;
            foreach (var row in rows)
            {
                if (!row || !(row.transform is RectTransform rect) || rect.parent != content) continue;
                var position = rect.anchoredPosition;
                if (terminalRows.Contains(rect))
                {
                    position.y = -above - rect.rect.height * 0.5f;
                    above += rect.rect.height;
                }
                else position.y -= pinnedHeight - above;
                rect.anchoredPosition = position;
            }
        }

        internal static void Install(Harmony harmony, ManualLogSource log)
        {
            try
            {
                harmony.CreateClassProcessor(typeof(MenuStart)).Patch();
                harmony.CreateClassProcessor(typeof(TabSelection)).Patch();
                harmony.CreateClassProcessor(typeof(OpenDropdown)).Patch();
            }
            catch (Exception ex) { log.LogWarning("Settings layout patch unavailable: " + ex.GetBaseException().Message); }
        }

        private static void ConfigureScrollbars(SettingsMenu menu)
        {
            if (!menu.content) return;
            var form = menu.content.GetComponentInParent<ScrollRect>();
            if (form && form.content == menu.content) AutoHide(form);
            // CCL replaces its rail on Start and each tab switch (old rails survive until end of frame).
            foreach (var scroll in menu.GetComponentsInChildren<ScrollRect>(true))
                if (scroll.name == "CUCoreLibModOptionTabs" && scroll.transform.parent &&
                    scroll.transform.parent.name == "CUCoreLibModOptionTabFrame") AutoHide(scroll);
        }

        private static void AutoHide(ScrollRect scroll)
        {
            if (scroll.verticalScrollbar && scroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent)
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static void FitRow(RectTransform row, RectTransform label, RectTransform field, bool checkbox)
        {
            // Only convert the stock fixed-width prefab. Do not overwrite an existing responsive layout.
            if (row.anchorMin.x != row.anchorMax.x || row.rect.width <= 0 || field.rect.width <= 0) return;
            var columns = new SettingsRowColumns(row.rect.width, field.rect.width, checkbox);
            SetHorizontal(row, 0f, 1f, 0f, 0f);
            SetHorizontal(label, 0f, columns.SplitAnchor, SettingsRowColumns.Inset, columns.LabelRightOffset);
            SetHorizontal(field, columns.SplitAnchor, 1f, columns.ControlLeftOffset, -SettingsRowColumns.Inset);
        }

        private static void SetHorizontal(RectTransform rect, float leftAnchor, float rightAnchor, float left, float right)
        {
            rect.anchorMin = new Vector2(leftAnchor, rect.anchorMin.y);
            rect.anchorMax = new Vector2(rightAnchor, rect.anchorMax.y);
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            rect.offsetMax = new Vector2(right, rect.offsetMax.y);
        }

        private static void FitText(TMP_Text text)
        {
            if (!text) return;
            if (!text.enableAutoSizing) text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(text.fontSizeMin, text.fontSizeMax, 12f);
            text.enableAutoSizing = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private sealed class PopupBounds
        {
            internal readonly RectTransform Viewport;
            internal readonly float ItemHeight, TemplateLimit;
            internal PopupBounds(RectTransform viewport, float itemHeight, float templateLimit)
            { Viewport = viewport; ItemHeight = itemHeight; TemplateLimit = templateLimit; }
        }

        private static readonly ConditionalWeakTable<TMP_Dropdown, PopupBounds> Popups = new();

        [HarmonyPatch(typeof(TMP_Dropdown), nameof(TMP_Dropdown.Show))]
        private static class OpenDropdown
        {
            [HarmonyPrefix]
            private static void Prefix(TMP_Dropdown __instance)
            {
                if (!Popups.TryGetValue(__instance, out var bounds) || !bounds.Viewport || !__instance.template) return;
                // Measure at open time: the player can scroll or resize after selecting the tab.
                var field = (RectTransform)__instance.transform;
                var corners = new Vector3[4];
                bounds.Viewport.GetWorldCorners(corners);
                float below = field.rect.yMin - field.InverseTransformPoint(corners[0]).y;
                float above = field.InverseTransformPoint(corners[1]).y - field.rect.yMax;
                var layout = new SettingsPopupLayout(bounds.ItemHeight, __instance.options.Count, bounds.TemplateLimit, below, above);
                var template = __instance.template;
                float edge = layout.OpenAbove ? 1f : 0f;
                template.anchorMin = new Vector2(template.anchorMin.x, edge);
                template.anchorMax = new Vector2(template.anchorMax.x, edge);
                template.pivot = new Vector2(template.pivot.x, 1f - edge);
                template.anchoredPosition = new Vector2(template.anchoredPosition.x, 0f);
                template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.Height);
            }
        }

        private static void FitPopup(TMP_Dropdown dropdown, RectTransform formViewport)
        {
            var template = dropdown.template;
            if (!template || !template.TryGetComponent<ScrollRect>(out var scroll)) return;
            var viewport = scroll.viewport;
            var content = scroll.content;
            if (!viewport || !content || content.parent != viewport || !dropdown.itemText) return;
            var toggle = dropdown.itemText.GetComponentInParent<Toggle>(true);
            if (!toggle || !(toggle.transform is RectTransform item) || item.parent != content) return;

            // CCL assigns +200 to a vertically stretched viewport. Match the popup edges instead.
            if (viewport.anchorMin.y != 0f || viewport.anchorMax.y != 1f || content.anchorMin.y != 1f ||
                content.anchorMax.y != 1f || item.anchorMin.y != 1f || item.anchorMax.y != 1f) return;
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, 0f);
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, 0f);
            // TMP treats space below the prototype as list padding on every Show().
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, item.rect.height);
            item.anchoredPosition = new Vector2(item.anchoredPosition.x, -item.rect.height * (1f - item.pivot.y));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            AutoHide(scroll);
            if (!Popups.TryGetValue(dropdown, out _))
                Popups.Add(dropdown, new PopupBounds(formViewport, item.rect.height, template.rect.height));
        }
    }
}
