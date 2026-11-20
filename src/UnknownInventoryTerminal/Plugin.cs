using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        /// <summary>Plugin version.</summary>
        public const string PluginVersion = BuildVersion.Value;
        internal static Plugin? Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; } = null!;
        internal static bool IsVisible => Instance?._window?.Visible == true;
        internal static bool BlocksGameplay => IsVisible || (Instance != null && Instance._closedFrame == Time.frameCount);
        private int _closedFrame = -1;
        private TerminalWindow? _window;
        private Ae2TerminalSkin? _terminalSkin;
        private NativeFlatInventoryPanel? _nativePanel;
        private InputShield? _shield;
        private Harmony? _harmony;
        private IDisposable? _pinyin;
        private bool _inputReady, _sessionActive, _closing, _textInputFocused, _clearGuiFocus;
        private PlayerCamera? _sessionCamera;
        private Body? _sessionBody;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible, _forceNoMovementValue, _radialOpen;
        private GameObject? _selectedObject;

        private void Awake()
        {
            Instance = this; Log = Logger;
            _harmony = new Harmony(PluginGuid);
            TerminalText.Initialize(Info.Location, message => Logger.LogWarning(message));
            SettingsBridge.Initialize(Config);
            ContainerPlacement.Initialize(Config);
            SettingsMenuLayoutPatch.Install(_harmony, Logger);
            _inputReady = GamePatches.Apply(_harmony, Logger);
            _shield = new InputShield();
            _terminalSkin = new Ae2TerminalSkin(Logger);
            _nativePanel = new NativeFlatInventoryPanel(Logger, _terminalSkin);
            _window = new TerminalWindow(_terminalSkin, GameInventoryAdapter.Capture, _nativePanel.EquipmentCount,
                entries => _nativePanel.SetEntries(entries),
                ex => Logger.LogWarning("Terminal read failed: " + ex.Message),
                _nativePanel.SetLayout, () => _nativePanel.CanReturnToGroups, _nativePanel.ReturnToGroups);
            _pinyin = PinyinBridge.Register(Logger);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded; native inventory bridge " + (_inputReady ? "ready." : "disabled: required input hooks unavailable."));
        }
        private void Update()
        {
            if (_window == null) return;
            if (IsVisible)
            {
                if (!SessionValid()) Close();
                else if (_window.CloseRequested || Input.GetKeyDown(KeyBinds.GetBind("pause"))) Close(preserveDrag: true);
                else if (!SearchFocused && Input.GetKeyDown(KeyBinds.GetBind("woundview")) &&
                    _sessionCamera is { } camera && HasHeldLocalDrag(camera))
                {
                    Close(preserveDrag: true);
                    // Native ToggleWoundView dismisses an open radial instead of opening health.
                    camera.radialOpen = false;
                    camera.ToggleWoundView();
                }
            }
            if (SettingsBridge.IsTogglePressed())
            {
                if (IsVisible) TryCloseTerminal();
                else TryOpenTerminal();
            }
            _window.Update();
            if (_window.Visible)
            {
                HoldInput();
                _shield?.SetVisible(true);
                _nativePanel?.SetVisible(true);
            }
        }

        private void LateUpdate()
        {
            if (IsVisible)
            {
                if (!SessionValid()) Close();
                else { HoldInput(); _shield?.SetVisible(true); }
            }
        }
        private void OnGUI()
        {
            if (_clearGuiFocus) { GUI.FocusControl(null); _clearGuiFocus = false; }
            _window?.Draw();
            _textInputFocused = IsVisible && GUI.GetNameOfFocusedControl() == "UIT.Search";
        }
        internal static bool SearchFocused => Instance?._textInputFocused == true;
        internal static bool IsInventorySurface(GameObject target) => Instance?._nativePanel?.Contains(target) == true;
        internal RectTransform? ExtensionRoot => IsVisible && !_closing && SessionValid() ? _nativePanel?.ExtensionRoot : null;

        internal bool TryGetTerminalBounds(out Rect screenBounds)
        {
            screenBounds = default;
            if (_closing || _window?.Visible != true || !SessionValid()) return false;
            screenBounds = _window.ScreenBounds;
            return true;
        }

        internal bool TryGetTerminalSnapshot(out IReadOnlyList<InventoryEntry> entries, out IReadOnlyList<InventoryRow> filteredRows)
        {
            entries = Array.Empty<InventoryEntry>();
            filteredRows = Array.Empty<InventoryRow>();
            if (_closing || _window?.Visible != true || !_window.HasSnapshot || !SessionValid()) return false;
            entries = _window.Snapshot;
            filteredRows = _window.Rows;
            return true;
        }

        internal bool TryResolveTerminalItem(string instanceKey, out Item? item)
        {
            item = null;
            if (string.IsNullOrEmpty(instanceKey) || !TryGetTerminalSnapshot(out var entries, out _)) return false;
            var entry = entries.FirstOrDefault(candidate => candidate.InstanceKey == instanceKey);
            if (entry?.NativeItem is not Item native || !native || native.id != entry.TypeId ||
                !GameInventoryAdapter.Owns(_sessionBody, native) || !native.TryGetParentContainer(out var parent) ||
                !parent || GameInventoryAdapter.Key(parent) != entry.ContainerKey) return false;
            item = native;
            return true;
        }

        internal IReadOnlyList<Item> GetTerminalLoadout() => IsVisible && !_closing && SessionValid()
            ? Array.AsReadOnly(_sessionBody!.GetSurfaceInventoryItems().Where(item => item).Distinct().ToArray())
            : Array.Empty<Item>();
        internal static bool IsTerminalPoint(Vector2 screenPoint) =>
            Instance?._window?.ContainsScreenPoint(screenPoint) == true;
        internal static InvButton? ResolveTerminalDrop(PlayerCamera camera)
        {
            var panel = Instance?._nativePanel;
            return panel?.ContainsScreenPoint(Input.mousePosition) == true ? panel.ResolveDrop(camera) : null;
        }
        internal static void ReleaseSearchFocus()
        {
            if (Instance == null) return;
            Instance._textInputFocused = false;
            Instance._clearGuiFocus = true;
        }
        private static bool CanToggle()
        {
            if (Instance != null && Instance._closedFrame == Time.frameCount) return false;
            if (IsVisible) return true;
            if (ConsoleScript.instance && ConsoleScript.instance.active) return false;
            var events = EventSystem.current;
            var selected = events ? events.currentSelectedGameObject : null;
            if (selected)
            {
                var input = selected!.GetComponentInParent<InputField>();
                var tmp = selected.GetComponentInParent<TMP_InputField>();
                if ((input && input.isFocused) || (tmp && tmp.isFocused)) return false;
            }
            if (PlayerCamera.main && PlayerCamera.main.recipeFilterField && PlayerCamera.main.recipeFilterField.isFocused) return false;
            return !GameInventoryAdapter.ChatFocused;
        }
        internal bool TryOpenTerminal()
        {
            if (!isActiveAndEnabled || _closing || _sessionActive || !_inputReady || _window == null || !CanToggle()) return false;
            var camera = PlayerCamera.main;
            if (!GameInventoryAdapter.IsLocalCamera(camera) || !EventSystem.current ||
                !camera.body.alive || !camera.body.conscious || camera.dragItem || NativeModalOpen(camera)) return false;
            _sessionCamera = camera; _sessionBody = camera.body;
            _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
            _forceNoMovementValue = _sessionBody.forceNoMovement;
            _radialOpen = camera.radialOpen;
            _selectedObject = EventSystem.current.currentSelectedGameObject;
            _sessionActive = true;
            try
            {
                EventSystem.current.SetSelectedGameObject(null);
                HoldInput();
                _window.Open();
                _shield?.SetVisible(true);
                _nativePanel?.SetVisible(true);
                return IsVisible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Terminal could not open; releasing inventory input. " + ex);
                Close();
                return false;
            }
        }
        private static bool NativeModalOpen(PlayerCamera camera) => PauseHandler.paused ||
            (camera.craftingPanel && camera.craftingPanel.activeSelf) ||
            (camera.itemCustomizer && camera.itemCustomizer.activeSelf) ||
            (camera.tradeMenu && camera.tradeMenu.activeSelf) ||
            (camera.woundView && camera.woundView.activeSelf) ||
            (MinigameBase.main && MinigameBase.main.currentMinigame) || LiquidTransfer.instance;
        private bool SessionValid() => _sessionActive && _sessionCamera && _sessionBody &&
            PlayerCamera.main == _sessionCamera && _sessionCamera!.body == _sessionBody &&
            GameInventoryAdapter.IsLocalCamera(_sessionCamera) && _sessionBody!.alive && _sessionBody.conscious &&
            !NativeModalOpen(_sessionCamera);
        private void HoldInput()
        {
            if (!_sessionBody || !_sessionCamera) return;
            _sessionBody!.moveDir = Vector2.zero; _sessionBody.crouching = false;
            _sessionBody.forceNoMovement = true; _sessionBody.endedJump = true;
            _sessionCamera!.wantsJumpInput = false;
            _sessionCamera.radialOpen = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private static bool HasHeldLocalDrag(PlayerCamera camera) =>
            Input.GetKey(KeyBinds.GetBind("iteminteract")) && GameInventoryAdapter.Owns(camera.body, camera.dragItem);
        internal bool TryCloseTerminal()
        {
            if (_closing || _window?.Visible != true) return false;
            Close(preserveDrag: true);
            return true;
        }

        private void Close(bool preserveDrag = false)
        {
            if (_closing) return;
            _closing = true;
            try
            {
                try { _nativePanel?.SetVisible(false); }
                finally { _shield?.SetVisible(false); }
            }
            finally
            {
                try
                {
                    if (IsVisible) _window!.Close();
                }
                finally
                {
                    try { EndSession(preserveDrag); }
                    finally { _closing = false; }
                }
            }
        }
        private void EndSession(bool preserveDrag)
        {
            if (!_sessionActive) return;
            // Validate the local camera, body and item before releasing session ownership.
            var handOff = preserveDrag && SessionValid() && HasHeldLocalDrag(_sessionCamera!);
            _sessionActive = false;
            _closedFrame = Time.frameCount;
            _textInputFocused = false; _clearGuiFocus = true;
            if (_sessionCamera)
            {
                if (!handOff) CancelDrag(_sessionCamera!);
                _sessionCamera!.radialOpen = _radialOpen;
            }
            // Restore the exact body saved at open, even if main.body has changed.
            if (_sessionBody) _sessionBody!.forceNoMovement = _forceNoMovementValue;
            Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible;
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (_selectedObject && _selectedObject!.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(_selectedObject);
            }
            _sessionBody = null; _sessionCamera = null; _selectedObject = null;
        }
        internal static void CancelDrag(PlayerCamera camera)
        {
            camera.dragItem = null;
            if (camera.dragImage) camera.dragImage.enabled = false;
        }
        internal static bool CanInteract(PlayerCamera camera) => IsVisible && Instance!.SessionValid() && camera == Instance._sessionCamera;
        internal static List<RaycastResult> FilterHits(List<RaycastResult> hits)
        {
            if (!IsVisible) return hits;
            var result = new List<RaycastResult>();
            var plugin = Instance!;
            RaycastResult blocker = default;
            bool hasBlocker = false;
            var terminalPoint = IsTerminalPoint(Input.mousePosition);
            foreach (var rawHit in hits)
            {
                var hit = rawHit;
                if (!hit.gameObject) continue;
                if (plugin._shield?.IsBlocker(hit.gameObject) == true)
                {
                    if (!hasBlocker) { blocker = hit; hasBlocker = true; }
                    continue;
                }
                // Extension controls receive ordinary UGUI events, not native item actions.
                // The settings popup is IMGUI, so its screen rectangle is consumed by the
                // terminal input gate rather than leaking clicks into inventory cells.
                if (terminalPoint && plugin._window?.SettingsOpen == true) return result;
                if (terminalPoint && plugin._nativePanel?.ContainsExtension(hit.gameObject) == true) return result;
                if (!terminalPoint || plugin._nativePanel?.Contains(hit.gameObject) != true) continue;
                plugin._nativePanel.ResolveItemHit(ref hit);
                if (ValidHit(hit)) result.Add(hit);
            }
            if (result.Count == 0 && hasBlocker) result.Add(blocker);
            return result;
        }
        internal static bool ValidHit(RaycastResult hit)
        {
            if (!hit.gameObject || !PlayerCamera.main || !PlayerCamera.main.body) return false;
            var camera = PlayerCamera.main;
            if (camera.containerMenu && hit.gameObject.transform.IsChildOf(camera.containerMenu.transform) &&
                (!camera.currentContainer || !GameInventoryAdapter.Owns(camera.body, camera.currentContainer.GetComponent<Item>()))) return false;
            var button = hit.gameObject.GetComponent<InvButton>();
            if (button)
            {
                // Check the local body and index before InvButton dereferences its body slot.
                if (button.isBody && (!GameInventoryAdapter.ButtonUsesLocalBody(button) ||
                    button.slot >= camera.body.slots.Length || !camera.body.slots[button.slot])) return false;
                var item = button.GetItem();
                return item ? GameInventoryAdapter.Owns(PlayerCamera.main.body, item) : button.isBody;
            }
            var label = hit.gameObject.GetComponent<ItemLabel>();
            return !label || GameInventoryAdapter.Owns(PlayerCamera.main.body, label.refItem);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Close();
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            _pinyin?.Dispose(); _shield?.Dispose(); _nativePanel?.Dispose(); _terminalSkin?.Dispose(); _harmony?.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }

    internal static class GamePatches
    {
        internal static bool Apply(Harmony harmony, ManualLogSource log)
        {
            var ready = true;
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleInput", nameof(PlayerCameraPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleDragging", nameof(DraggingPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleWhileDragging", nameof(WhileDraggingPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleMouseOver", nameof(MouseOverPrefix), log, nameof(MouseOverPostfix));
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPickupFromWorld", nameof(WorldPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPerformWorldActions", nameof(WorldPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPickupFromUI", nameof(PickupPrefix), log, nameof(PickupPostfix));
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleReleaseDragging", nameof(ReleasePrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPerformUIActions", nameof(LoadoutActionPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), nameof(PlayerCamera.OpenContainer), nameof(OpenContainerPrefix), log);
            ready &= Patch(harmony, typeof(UIUtil), "GetEventSystemRaycastResults", null, log, nameof(RaycastPostfix));
            Patch(harmony, AccessTools.TypeByName("Together.Chat"), "Update", nameof(ChatPrefix), log);
            return ready;
        }
        private static bool Patch(Harmony harmony, Type? type, string methodName, string? prefixName, ManualLogSource log, string? postfixName = null)
        {
            try
            {
                var method = type == null ? null : AccessTools.Method(type, methodName);
                if (method == null) { log.LogInfo("Input target not found: " + type?.FullName + "." + methodName); return false; }
                var prefix = prefixName == null ? null : new HarmonyMethod(typeof(GamePatches), prefixName) { priority = Priority.First };
                var postfix = postfixName == null ? null : new HarmonyMethod(typeof(GamePatches), postfixName) { priority = Priority.Last };
                harmony.Patch(method, prefix: prefix, postfix: postfix);
                log.LogInfo("Input gate patched: " + type!.FullName + "." + methodName);
                return true;
            }
            catch (Exception ex) { log.LogWarning("Input gate unavailable for " + type?.FullName + "." + methodName + ": " + ex.Message); return false; }
        }
        private static bool PlayerCameraPrefix(PlayerCamera __instance)
        {
            if (!Plugin.BlocksGameplay) return true;
            var body = __instance.body;
            if (body)
            {
                body.moveDir = Vector2.zero; body.crouching = false; body.endedJump = true;
            }
            __instance.wantsJumpInput = false;
            return false;
        }
        private static void RaycastPostfix(ref List<RaycastResult> __result)
        {
            if (Plugin.IsVisible) __result = Plugin.FilterHits(__result);
        }
        private static bool MouseOverPrefix(PlayerCamera __instance, List<RaycastResult> uiCasts,
            ref UsableObject usableObject, out bool __state)
        {
            __state = false;
            if (!Plugin.BlocksGameplay) return true;
            usableObject = null!;
            if (__instance.hoverSquare) __instance.hoverSquare.enabled = false;
            // Keep the game's UI-hover/UITooltip path alive while suppressing its world-hover
            // branch. This matters after Together has focused a local body: returning false
            // skips the native tooltip update and leaves the last tooltip hidden or stale.
            __state = __instance.radialOpen;
            // Keep HandleMouseOver on its UI-only branch. It treats an empty cast list as
            // permission to probe the world, so add a non-hit sentinel for blank/shielded
            // terminal areas instead of allowing world hover through the modal.
            __instance.radialOpen = false;
            if (uiCasts.Count == 0) uiCasts.Add(default);
            return true;
        }

        private static void MouseOverPostfix(PlayerCamera __instance, bool __state)
        {
            if (Plugin.BlocksGameplay) __instance.radialOpen = __state;
        }
        private static bool DraggingPrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            if (!Plugin.BlocksGameplay) return true;
            // Keep the closing input blocked without clearing the drag handed back to the game.
            if (!Plugin.IsVisible) return false;
            if (!Plugin.CanInteract(__instance)) { Plugin.CancelDrag(__instance); return false; }
            uiCasts = Plugin.FilterHits(uiCasts);
            if (Plugin.SearchFocused)
            {
                // IMGUI focus survives UGUI clicks unless explicitly released.
                var pointerDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
                if (!pointerDown || !uiCasts.Exists(hit => Plugin.IsInventorySurface(hit.gameObject))) return false;
                Plugin.ReleaseSearchFocus();
            }
            if (__instance.dragItem && !GameInventoryAdapter.Owns(__instance.body, __instance.dragItem)) Plugin.CancelDrag(__instance);
            // Keep Together's world-hover branch from acquiring another body.
            __instance.radialOpen = true;
            return true;
        }
        private static bool WhileDraggingPrefix(PlayerCamera __instance)
        {
            if (!Plugin.BlocksGameplay) return true;
            return Plugin.CanInteract(__instance) && !Plugin.SearchFocused;
        }
        private static bool PickupPrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            if (!Plugin.BlocksGameplay) return true;
            uiCasts = Plugin.FilterHits(uiCasts);
            return Plugin.CanInteract(__instance);
        }
        private static void PickupPostfix(PlayerCamera __instance)
        {
            if (Plugin.IsVisible && __instance.dragItem && !GameInventoryAdapter.Owns(__instance.body, __instance.dragItem))
                Plugin.CancelDrag(__instance);
        }
        private static bool ReleasePrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            if (!Plugin.BlocksGameplay) return true;
            if (!Plugin.IsVisible) return false;
            uiCasts = Plugin.FilterHits(uiCasts);
            // Revalidate before taking either the equipment or original inventory path.
            if (!Plugin.CanInteract(__instance) || !GameInventoryAdapter.Owns(__instance.body, __instance.dragItem))
            { Plugin.CancelDrag(__instance); return false; }
            if (Plugin.IsTerminalPoint(Input.mousePosition))
            {
                var slotHit = uiCasts.Find(hit => hit.gameObject && hit.gameObject.GetComponentInParent<LoadoutCell>());
                if (slotHit.gameObject)
                {
                    var slot = slotHit.gameObject.GetComponentInParent<LoadoutCell>();
                    var target = slot.Button;
                    var source = __instance.dragItem;
                    var moved = Vector2.Distance(Input.mousePosition, __instance.clickPos) >= 10f;
                    var equip = moved && slot.CanEquip(__instance, source);
                    if (slot.IsCurrent(__instance) && (equip ||
                        (target && target!.gameObject.activeInHierarchy && Plugin.ValidHit(ButtonHit(slotHit, target)) &&
                            (moved ? CanDropOnLoadout(__instance, slot, target, source) : target.GetItem() == source))))
                    {
                        uiCasts = new List<RaycastResult> { equip ? slotHit : ButtonHit(slotHit, target!) };
                        return true;
                    }
                    Plugin.CancelDrag(__instance);
                    return false;
                }
            }
            // Only the searchable storage grid uses the configured automatic container destination.
            if (Plugin.IsTerminalPoint(Input.mousePosition))
            {
                var destination = uiCasts.Exists(hit => Plugin.IsInventorySurface(hit.gameObject))
                    ? Plugin.ResolveTerminalDrop(__instance) : null;
                if (destination)
                {
                    var hit = uiCasts.FirstOrDefault(result => result.module != null);
                    hit.screenPosition = Input.mousePosition;
                    uiCasts = new List<RaycastResult> { ButtonHit(hit, destination!) };
                    return true;
                }
                // A click on its own storage tile can complete through the native release path.
                if (Vector2.Distance(Input.mousePosition, __instance.clickPos) < 10f &&
                    uiCasts.Exists(hit => hit.gameObject && hit.gameObject.TryGetComponent<InvButton>(out var button) &&
                        button.GetItem() == __instance.dragItem)) return true;
            }
            // Releasing over the shield/blank content cancels, rather than unloading
            // into the world underneath the modal window.
            Plugin.CancelDrag(__instance);
            return false;
        }
        private static RaycastResult ButtonHit(RaycastResult hit, InvButton button)
        {
            hit.gameObject = button.gameObject;
            return hit;
        }

        private static bool CanDropOnLoadout(PlayerCamera camera, LoadoutCell cell, InvButton button, Item source)
        {
            var target = button.GetItem();
            if (target == source) return true;
            if (target && target.container)
                return !Input.GetKey(KeyBinds.GetBind("expanddesc")) && ContainerPlacement.CanPlace(camera.body, source, target);
            if (cell.MainIndex < 0) return false;
            var body = camera.body;
            var slot = body.slots[cell.MainIndex];
            if (!slot.canPickUp || (source.Stats.onlyHoldInHands && !slot.isHand)) return false;
            // The native swap detaches both items before placing them. Reject it if the
            // displaced item cannot go back to the source slot.
            return !target || !body.HoldingItem(source) || !target.Stats.onlyHoldInHands || body.slots[body.SlotOf(source)].isHand;
        }

        private static bool LoadoutActionPrefix(PlayerCamera __instance, List<RaycastResult> uiCasts, ref bool __result)
        {
            if (!Plugin.CanInteract(__instance) || uiCasts.Count == 0 || !uiCasts[0].gameObject) return true;
            var slot = uiCasts[0].gameObject.GetComponentInParent<LoadoutCell>();
            var source = __instance.dragItem;
            if (!slot || Vector2.Distance(Input.mousePosition, __instance.clickPos) < 10f ||
                !slot.CanEquip(__instance, source)) return true;
            // Call the native wear method inside HandleReleaseDragging so CCL's conflict
            // checks and Together's native wear/release hooks still execute.
            if (!__instance.body.HasWearable(source)) __instance.body.WearWearable(source);
            __result = true;
            return false;
        }

        private static bool OpenContainerPrefix() => !Plugin.IsVisible;
        private static bool WorldPrefix() => !Plugin.BlocksGameplay;
        private static bool ChatPrefix() => !Plugin.BlocksGameplay;
    }

    internal static class GameInventoryAdapter
    {
        private static readonly Type? NetType = AccessTools.TypeByName("Together.Net");
        private static readonly PropertyInfo? NetworkRunning = NetType == null ? null : AccessTools.Property(NetType, "IsRunning");
        private static readonly Type? PlayerType = AccessTools.TypeByName("Together.ScavPlayer");
        private static readonly FieldInfo? LocalPlayer = PlayerType == null ? null : AccessTools.Field(PlayerType, "LocalPlayer");
        private static readonly FieldInfo? PlayerBody = PlayerType == null ? null : AccessTools.Field(PlayerType, "body");
        private static readonly Type? ChatType = AccessTools.TypeByName("Together.Chat");
        private static readonly FieldInfo? ChatFocus = ChatType == null ? null : AccessTools.Field(ChatType, "CHAT_textbox_input_focused");
        private static readonly MethodInfo? ButtonBody = AccessTools.PropertyGetter(typeof(InvButton), "body");
        private static readonly Type? ButtonPatchType = AccessTools.TypeByName("Together.InvButton_get_body_MultiplayerPatch");
        private static readonly FieldInfo? FocusedBody = ButtonPatchType == null ? null : AccessTools.Field(ButtonPatchType, "focused_body");
        internal static bool ChatFocused => ChatFocus?.GetValue(null) is bool focused && focused;
        internal static bool IsLocalCamera(PlayerCamera? camera)
        {
            if (!camera || !camera!.body) return false;
            try
            {
                if (NetType == null) return true;
                if (!(NetworkRunning?.GetValue(null) is bool running)) return false;
                if (!running) return true;
                var local = LocalPlayer?.GetValue(null);
                var focused = FocusedBody?.GetValue(null) as Body;
                // Together can retarget InvButton.body independently of main.body.
                return local != null && PlayerBody?.GetValue(local) is Body body && body && body == camera.body &&
                    (!focused || focused == body);
            }
            catch { return false; }
        }
        internal static bool ButtonUsesLocalBody(InvButton button)
        {
            try { return ButtonBody?.Invoke(button, null) is Body body && PlayerCamera.main && body == PlayerCamera.main.body; }
            catch { return false; }
        }
        internal static IReadOnlyList<InventoryEntry> Capture()
        {
            var camera = PlayerCamera.main;
            if (!IsLocalCamera(camera)) return Array.Empty<InventoryEntry>();
            var result = new List<InventoryEntry>();
            foreach (var (item, container) in EnumerateItems(camera.body))
            {
                if (!item || !container || string.IsNullOrEmpty(item.id)) continue;
                var containerItem = container.GetComponent<Item>();
                if (!containerItem) continue;
                var sprite = item.GetComponent<SpriteRenderer>();
                result.Add(new InventoryEntry(Key(item), item.id, item.fullName, Key(container), containerItem.fullName,
                    item, sprite ? sprite.sprite : null));
            }
            return result.AsReadOnly();
        }
        // Membership follows actual direct Container parents to a currently held or
        // worn root. Arbitrary Item components under a transform are not inventory.
        internal static bool Owns(Body? body, Item? item)
        {
            if (!body || !item) return false;
            try
            {
                var visited = new HashSet<int>();
                var root = item!;
                while (root.TryGetParentContainer(out var parent))
                {
                    if (!parent || !visited.Add(root.GetInstanceID())) return false;
                    root = parent.GetComponent<Item>();
                    if (!root) return false;
                }
                foreach (var candidate in body!.GetSurfaceInventoryItems())
                    if (candidate && candidate == root) return true;
            }
            catch { return false; }
            return false;
        }
        private static IEnumerable<(Item Item, Container Container)> EnumerateItems(Body body)
        {
            // Surface items establish ownership, but only actual container contents are indexed.
            var roots = body.GetSurfaceInventoryItems().Where(item => item && item.container).Select(item => item.container);
            return ContainerContents.Enumerate(roots, DirectItems,
                item => item.TryGetParentContainer(out var parent) && parent ? parent : null,
                item => item.container ? item.container : null);
        }

        private static IEnumerable<Item> DirectItems(Container container)
        {
            if (!container) yield break;
            foreach (Transform child in container.transform)
                if (child && child.TryGetComponent<Item>(out var item) && item) yield return item;
        }
        internal static string Key(UnityEngine.Object value) => value.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
