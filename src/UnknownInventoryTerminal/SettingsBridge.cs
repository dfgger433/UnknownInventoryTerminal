using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;

namespace UnknownInventoryTerminal
{
    internal static class SettingsBridge
    {
        internal const string ConfigSection = "UnknownInventoryTerminal";
        internal const string LanguageSetting = "Language";
        // CCL's string overload adds a separate rail tab; the enum overload uses the stock menu.
        internal const Setting.SettingCategory MenuCategory = Setting.SettingCategory.Game;

        internal static ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;

        internal static string Label(string name) => TerminalText.Get("settings." + name);
        internal static string Description(string name) => Label(name + ".description");
        internal static string ChoiceLabel(string name, string key) => Label(name + "." + key);
        internal static ModDropdownChoice Choice(string name, string key) => new ModDropdownChoice(key, ChoiceLabel(name, key));

        internal static void Initialize(ConfigFile config)
        {
            var language = config.Bind(ConfigSection, LanguageSetting, TerminalText.English,
                "Display language for the inventory terminal (en or zh-CN).");
            TerminalText.Select(language.Value);
            language.SettingChanged += (_, _) => TerminalText.Select(language.Value);
            var languages = new[] { TerminalText.English, TerminalText.Chinese };
            ModOptionsRegistry.Register(ModOptionDefinition.Dropdown(ConfigSection + "." + LanguageSetting,
                Label(LanguageSetting), Description(LanguageSetting), MenuCategory,
                TerminalText.Language == TerminalText.Chinese ? 1 : 0, new[] {
                    Choice(LanguageSetting, languages[0]), Choice(LanguageSetting, languages[1])
                }, index => { if (index >= 0 && index < languages.Length) language.Value = languages[index]; }));

            ToggleKey = config.Bind(ConfigSection, "ToggleKey", KeyCode.F7,
                "Key used to open or close the inventory terminal.");

            ModOptionsRegistry.Register(ModOptionDefinition.Keybind(
                ConfigSection + ".ToggleKey", Label("ToggleKey"), Description("ToggleKey"), MenuCategory, ToggleKey.Value,
                value => ToggleKey.Value = value));
        }

        internal static bool IsTogglePressed()
        {
            return ToggleKey.Value != KeyCode.None && Input.GetKeyDown(ToggleKey.Value);
        }
    }
}
