using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    /// <summary>Client-only inventory terminal plugin entry point.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("net.cucorelib", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("JustUnknownCharacters", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        /// <summary>Stable BepInEx plugin identifier.</summary>
        public const string PluginGuid = "com.unknown.inventoryterminal";
        /// <summary>Human-readable plugin name.</summary>
        public const string PluginName = "Unknown Inventory Terminal";
        /// <summary>Stage-one plugin version.</summary>
        public const string PluginVersion = "0.1.0";
        internal static Plugin? Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; } = null!;
        internal static bool IsVisible => Instance?._window?.Visible == true;
        private TerminalWindow? _window;
        private InputShield? _shield;
        private Harmony? _harmony;
        private IDisposable? _pinyin;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible;
        private bool _cursorSaved;
        private bool _forceNoMovementSaved;
        private bool _forceNoMovementValue;

        private void Awake()
        {
            Instance = this; Log = Logger;
            SettingsBridge.Initialize(Config);
            _harmony = new Harmony(PluginGuid);
            GamePatches.Apply(_harmony, Logger);
            _shield = new InputShield();
            var adapter = new GameInventoryAdapter(Logger);
            _window = new TerminalWindow(adapter.Capture, adapter.Interact, ex => Logger.LogWarning("Terminal read failed: " + ex.Message));
            _window.Mode = SettingsBridge.ByContainer.Value ? InventoryViewMode.ByContainer : InventoryViewMode.Merged;
            _pinyin = PinyinBridge.Register(() => SettingsBridge.Pinyin.Value, Logger);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded; stage-1 backend and test window ready.");
        }
        private void Update()
        {
            if (SettingsBridge.IsTogglePressed() && CanToggle()) Toggle();
            if (_window == null) return;
            _window.Mode = SettingsBridge.ByContainer.Value ? InventoryViewMode.ByContainer : InventoryViewMode.Merged;
            _window.Update();
            _shield?.SetVisible(_window.Visible);
            if (_window.Visible)
            {
                if (!_cursorSaved) { _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible; _cursorSaved = true; }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
        }
        private void OnGUI() => _window?.Draw();
        private static bool CanToggle()
        {
            if (ConsoleScript.instance && ConsoleScript.instance.input && ConsoleScript.instance.input.isFocused) return false;
            if (PlayerCamera.main && PlayerCamera.main.recipeFilterField && PlayerCamera.main.recipeFilterField.isFocused) return false;
            var tmp = UnityEngine.EventSystems.EventSystem.current;
            return tmp == null || !tmp.IsPointerOverGameObject() || IsVisible;
        }
        private void Toggle()
        {
            if (_window == null) return;
            if (!_window.Visible)
            {
                var body = PlayerCamera.main ? PlayerCamera.main.body : null;
                if (body != null) { _forceNoMovementValue = body.forceNoMovement; _forceNoMovementSaved = true; }
                _window.Open(); _window.Mode = SettingsBridge.ByContainer.Value ? InventoryViewMode.ByContainer : InventoryViewMode.Merged;
            }
            else _window.Close();
            if (!_window.Visible)
            {
                var body = PlayerCamera.main ? PlayerCamera.main.body : null;
                if (body != null && _forceNoMovementSaved) body.forceNoMovement = _forceNoMovementValue;
                _forceNoMovementSaved = false;
                if (_cursorSaved) { Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible; _cursorSaved = false; }
            }
        }
        private void OnDestroy()
        {
            _pinyin?.Dispose(); _shield?.Dispose(); _harmony?.UnpatchSelf();
            var body = PlayerCamera.main ? PlayerCamera.main.body : null;
            if (body != null && _forceNoMovementSaved) body.forceNoMovement = _forceNoMovementValue;
            if (_cursorSaved) { Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible; }
            Instance = null;
        }
    }

    internal static class GamePatches
    {
        internal static void Apply(Harmony harmony, ManualLogSource log)
        {
            Patch(harmony, "PlayerCamera", "HandleInput", nameof(PlayerCameraPrefix), log);
            Patch(harmony, "Together.Chat", "Update", nameof(ChatPrefix), log);
        }
        private static void Patch(Harmony harmony, string typeName, string methodName, string prefixName, ManualLogSource log)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                var method = type == null ? null : AccessTools.Method(type, methodName);
                if (method == null) { log.LogInfo("Optional input target not found: " + typeName + "." + methodName); return; }
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(GamePatches), prefixName));
                log.LogInfo("Input gate patched: " + typeName + "." + methodName);
            }
            catch (Exception ex) { log.LogWarning("Input gate unavailable for " + typeName + ": " + ex.Message); }
        }
        private static bool PlayerCameraPrefix(PlayerCamera __instance)
        {
            if (!Plugin.IsVisible) return true;
            var body = __instance.body;
            if (body != null)
            {
                body.moveDir = Vector2.zero;
                body.crouching = false;
                body.forceNoMovement = true;
            }
            __instance.wantsJumpInput = false;
            return false;
        }
        private static bool ChatPrefix() => !Plugin.IsVisible;
    }

    internal sealed class GameInventoryAdapter
    {
        private readonly ManualLogSource _log;
        private object? _body;
        private IReadOnlyList<InventoryEntry> _last = Array.Empty<InventoryEntry>();
        internal GameInventoryAdapter(ManualLogSource log) { _log = log; }
        internal IReadOnlyList<InventoryEntry> Capture()
        {
            _body = PlayerCamera.main ? (object?)PlayerCamera.main.body : null;
            if (_body == null) return Array.Empty<InventoryEntry>();
            var result = new List<InventoryEntry>();
            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in EnumerateItems(_body))
                {
                    var key = GetInstanceKey(item);
                    if (!seen.Add(key)) continue;
                    var container = GetContainer(item);
                    var id = ReadString(item, "id", "Id");
                    if (string.IsNullOrEmpty(id)) continue;
                    var name = ReadString(item, "fullName", "displayName", "name", "localizedName");
                    if (string.IsNullOrEmpty(name)) name = id;
                    result.Add(new InventoryEntry(key, id, name, GetInstanceKey(container), ContainerName(container), item, GetIcon(item)));
                }
            }
            catch (Exception ex) { _log.LogWarning("Inventory enumeration failed: " + ex.Message); }
            _last = result;
            return result;
        }
        internal void Interact(InventoryEntry entry, NativeInteraction interaction)
        {
            var body = PlayerCamera.main ? (object?)PlayerCamera.main.body : null;
            if (body == null || !Owns(body, entry.NativeItem)) { _log.LogWarning("Rejected stale or non-local item interaction."); return; }
            // The test window never constructs or clones an item. It selects the real Item
            // so stage 2 can route it through PlayerCamera.TryPickupFromUI/HandleReleaseDragging.
            _log.LogInfo("Selected native item " + entry.TypeId + " for original " + interaction + " interaction; stage-2 UI bridge pending.");
        }
        private static bool Owns(object body, object item)
        {
            foreach (var candidate in EnumerateItems(body)) if (ReferenceEquals(candidate, item)) return true;
            return false;
        }
        private static IEnumerable<object> EnumerateItems(object body)
        {
            var method = body.GetType().GetMethod("GetAllItemsThorough", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(method?.Invoke(body, null) is IEnumerable roots)) yield break;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in roots)
            {
                if (root == null) continue;
                var rootKey = GetInstanceKey(root);
                if (visited.Add(rootKey)) yield return root;
                if (root is Component component)
                {
                    foreach (var child in component.GetComponentsInChildren<Component>(true))
                    {
                        if (child == component || child.GetType().Name != "Item") continue;
                        var key = GetInstanceKey(child);
                        if (visited.Add(key)) yield return child;
                    }
                }
            }
        }
        private static object? GetContainer(object item)
        {
            try
            {
                var method = item.GetType().GetMethod("TryGetParentContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    var args = new object?[] { null };
                    var result = method.Invoke(item, args);
                    if (args[0] != null && (!(result is bool) || (bool)result)) return args[0];
                }
                return Read(item, "container", "ParentContainer");
            }
            catch { return null; }
        }
        private static string ContainerName(object? container) => container == null ? "Player inventory" : ReadString(container, "id", "name", "gameObject.name");
        private static object? GetIcon(object item)
        {
            try { return (item as Component)?.GetComponent<SpriteRenderer>()?.sprite; } catch { return null; }
        }
        private static string GetInstanceKey(object? value)
        {
            if (value == null) return "root";
            try { return ((value as Component)?.GetInstanceID() ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value)).ToString(System.Globalization.CultureInfo.InvariantCulture); }
            catch { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString(System.Globalization.CultureInfo.InvariantCulture); }
        }
        private static object? Read(object instance, params string[] paths)
        {
            foreach (var path in paths)
            {
                object? current = instance;
                foreach (var part in path.Split('.'))
                {
                    if (current == null) break;
                    var type = current.GetType();
                    var p = type.GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    var f = type.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    current = p != null ? p.GetValue(current, null) : f?.GetValue(current);
                }
                if (current != null) return current;
            }
            return null;
        }
        private static string ReadString(object instance, params string[] paths) => Convert.ToString(Read(instance, paths), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
