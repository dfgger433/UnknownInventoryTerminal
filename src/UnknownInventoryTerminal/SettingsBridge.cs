using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;
using System;

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
        private static ConfigEntry<string> _language = null!;
        internal static event Action? LanguageChanged;

        internal static string Language => TerminalText.Language;
        internal static string LanguageLabel() => TerminalText.LanguageName(Language);
        internal static void SetLanguage(string language) => _language.Value = TerminalText.ResolveLanguage(language);

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

        internal static void Initialize(ConfigFile config)
        {
            _language = config.Bind(ConfigSection, LanguageSetting, TerminalText.English,
                "Display language for the inventory terminal (en or zh-CN).");
            TerminalText.Select(_language.Value);
            _language.SettingChanged += (_, _) =>
            {
                TerminalText.Select(_language.Value);
                LanguageChanged?.Invoke();
            };

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
