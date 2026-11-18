using BepInEx.Configuration;
using BepInEx.Logging;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    /// <summary>Stage-1 settings bridge. Call Initialize from the plugin's Awake on the Unity main thread.</summary>
    internal static class SettingsBridge
    {
        internal const string Category = "UnknownInventoryTerminal";
        internal const string ToggleKeyId = Category + ".ToggleKey";
        internal const string ByContainerId = Category + ".ByContainer";
        internal const string PinyinId = Category + ".Pinyin";
        internal const string ToggleKeyLabel = "Open inventory terminal";
        internal const string ByContainerLabel = "ByContainer";
        internal const string PinyinLabel = "Pinyin";

        internal static ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;
        internal static ConfigEntry<bool> ByContainer { get; private set; } = null!;
        internal static ConfigEntry<bool> Pinyin { get; private set; } = null!;

        internal static void Initialize(ConfigFile config, Harmony harmony, ManualLogSource log)
        {
            ToggleKey = config.Bind(Category, "ToggleKey", KeyCode.F7,
                "Key used to open or close the inventory terminal.");
            ByContainer = config.Bind(Category, "ByContainer", false,
                "Show inventory rows grouped by container.");
            Pinyin = config.Bind(Category, "Pinyin", true,
                "Use the optional JustUnknownCharacters pinyin matcher.");

            SettingsUiPatch.Install(harmony, log);
            // Unique action ID and one source of truth for menu changes and config files.
            ModOptionsRegistry.Register(ModOptionDefinition.Keybind(
                ToggleKeyId, ToggleKeyLabel,
                "Open or close the inventory terminal (default F7).", Category, ToggleKey.Value,
                value => ToggleKey.Value = value));

            ModOptionsRegistry.Register(ModOptionDefinition.Bool(
                ByContainerId, ByContainerLabel,
                "Show inventory rows grouped by container.", Category, ByContainer.Value,
                value => ByContainer.Value = value));
            ModOptionsRegistry.Register(ModOptionDefinition.Bool(
                PinyinId, PinyinLabel,
                "Use the optional JustUnknownCharacters pinyin matcher.", Category, Pinyin.Value,
                value => Pinyin.Value = value));
        }

        internal static bool IsTogglePressed()
        {
            return ToggleKey.Value != KeyCode.None && Input.GetKeyDown(ToggleKey.Value);
        }
    }
}
