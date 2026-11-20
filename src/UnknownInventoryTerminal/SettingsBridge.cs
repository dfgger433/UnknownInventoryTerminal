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
        // Keep general terminal options in the stock Game tab. Keybinds use the stock
        // Controls/Input tab so all actions can be rebound in one place.
        internal const Setting.SettingCategory MenuCategory = Setting.SettingCategory.Game;
        internal const Setting.SettingCategory KeybindCategory = Setting.SettingCategory.Input;

        internal static ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;
        // Changed by the terminal-style toolbar button, like AE2's client-side TERMINAL_STYLE setting.
        private static ConfigEntry<TerminalHeight>? _terminalHeight;
        internal static TerminalHeight TerminalHeight
        {
            get => _terminalHeight is { } entry && System.Enum.IsDefined(typeof(TerminalHeight), entry.Value) ? entry.Value : TerminalHeight.Small;
            set { if (_terminalHeight != null) _terminalHeight.Value = value; }
        }

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
                ConfigSection + ".ToggleKey", Label("ToggleKey"), Description("ToggleKey"), KeybindCategory, ToggleKey.Value,
                value => ToggleKey.Value = value));

            _terminalHeight = config.Bind(ConfigSection, "TerminalStyle", TerminalHeight.Small,
                "Storage height: Small, Medium, Tall or Full share of the rows that fit on the screen (never below 3 rows). " +
                "The terminal-style button in the left toolbar cycles it.");
        }

        internal static bool IsTogglePressed()
        {
            // The stock rebinder accepts mouse buttons, but using them here would toggle
            // the terminal on every inventory click. Keep the setting visible/rebindable
            // while ignoring pointer buttons in the modal's global toggle poll.
            var key = ToggleKey.Value;
            return key != KeyCode.None && key != KeyCode.Mouse0 && key != KeyCode.Mouse1 &&
                key != KeyCode.Mouse2 && key != KeyCode.Mouse3 && key != KeyCode.Mouse4 &&
                key != KeyCode.Mouse5 && key != KeyCode.Mouse6 && Input.GetKeyDown(key);
        }
    }
}
