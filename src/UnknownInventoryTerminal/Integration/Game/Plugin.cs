// SPDX-License-Identifier: GPL-3.0-only

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
        private bool _inputReady, _sessionActive, _closing;
        private PlayerCamera? _sessionCamera;
        private Body? _sessionBody;
        private Item? _terminalDragItem;
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
            var fluidDialog = _nativePanel?.FluidDialog;
            fluidDialog?.AdvanceInputFrame();
            if (IsVisible)
            {
                if (!SessionValid()) Close();
                else if (fluidDialog?.BlocksInput == true)
                {
                    if (Input.GetKeyDown(KeyBinds.GetBind("pause"))) fluidDialog.Hide();
                    else fluidDialog.Tick();
                }
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
            if (fluidDialog?.BlocksInput != true && SettingsBridge.IsTogglePressed())
            {
                if (IsVisible) TryCloseTerminal();
                else TryOpenTerminal();
            }
            _window.Update(fluidDialog?.BlocksInput == true);
            if (_window.Visible)
            {
                HoldInput();
                _shield?.SetVisible(true);
                if (_window.SettingsOpen && _sessionCamera is { } settingsCamera) CancelDrag(settingsCamera);
                _nativePanel?.SetVisible(true, _window.SettingsOpen);
                _nativePanel?.BlockPointerInput(_window.BlocksInventoryInput);
            }
        }

        private void LateUpdate()
        {
            _nativePanel?.FluidDialog?.EndInputFrame();
            if (IsVisible)
            {
                if (!SessionValid()) Close();
                else
                {
                    HoldInput();
                    _shield?.SetVisible(true);
                    UpdateDragActions(_sessionCamera!);
                    if (_nativePanel?.Chrome is { } chrome) _window!.DrawInventoryChrome(chrome);
                    _nativePanel?.UpdateTooltips(_sessionCamera!, FluidBlocksInput || _window!.BlocksInventoryInput || _window.CloseRequested);
                }
            }
        }
        private void OnGUI()
        {
            _window?.DrawSettings();
            if (IsVisible && !FluidBlocksInput && !_window!.BlocksInventoryInput && !_window.CloseRequested)
                _nativePanel?.DrawItemTooltip();
        }
        internal static bool SearchFocused => IsVisible && Instance?._nativePanel?.Chrome?.SearchFocused == true;
        internal static bool IsInventorySurface(GameObject target) => Instance?._nativePanel?.Contains(target) == true;
        internal static bool TryGetDragAction(GameObject target, out TerminalDragAction action)
        {
            action = default;
            return Instance?._nativePanel?.TryGetDragAction(target, out action) == true;
        }
        internal static bool FluidBlocksInput => Instance?._nativePanel?.FluidDialog?.BlocksInput == true;
        internal static bool CanInteractWithDragActions(PlayerCamera camera) => CanInteract(camera) &&
            !Instance!._closing && !SearchFocused && !FluidBlocksInput &&
            Instance._window is { BlocksInventoryInput: false, CloseRequested: false };

        internal static void OpenFluidTransfer(TerminalFluidTransfer transfer)
        {
            var maximumMl = transfer.MaximumMl();
            if (maximumMl <= 0)
            {
                transfer.Camera.DoAlert(TerminalText.Get("fluidTransfer.unavailable"));
                return;
            }
            Instance?._nativePanel?.ShowFluidTransfer(maximumMl, ml =>
            {
                var camera = transfer.Camera;
                if (!Application.isFocused || !CanInteract(camera)) return;
                try
                {
                    if (transfer.Finish(ml)) return;
                }
                catch (Exception ex) { Log.LogWarning("Native fluid transfer failed: " + ex); }
                camera.DoAlert(TerminalText.Get("fluidTransfer.unavailable"));
            });
        }

        internal static TerminalDragAction? DragActionAtPointer(PlayerCamera camera, Item item, out TerminalDragAction actionLayout,
            TerminalDragAction armed = TerminalDragAction.None, TerminalDragAction originalLayout = TerminalDragAction.None)
        {
            actionLayout = TerminalDragAction.None;
            return CanInteractWithDragActions(camera) && GameInventoryAdapter.Owns(camera.body, item)
                ? Instance!._nativePanel?.DragActionAtPointer(camera, item, out actionLayout, armed, originalLayout) : null;
        }

        internal static void TogglePin(PlayerCamera camera, Item item) => Instance?._nativePanel?.TogglePin(camera.body, item);

        internal static bool ReorderPinnedItem(PlayerCamera camera, Item item, Vector2 pointer)
        {
            var panel = Instance?._nativePanel;
            if (!CanInteract(camera) || !IsTerminalDrag(camera, item) || !GameInventoryAdapter.Owns(camera.body, item) ||
                panel?.TryReorderPinnedItem(item, pointer) != true) return false;
            CancelDrag(camera);
            panel.RefreshPinnedItems();
            return true;
        }

        internal static void UpdateDragActions(PlayerCamera camera)
        {
            var plugin = Instance;
            if (plugin == null || camera != plugin._sessionCamera) return;
            // Equipment highlights follow the live native drag only. A pending action has already
            // left the original drag path and must not make wearable slots look actionable.
            var dragItem = CanInteract(camera) && !plugin._closing && plugin._window is { BlocksInventoryInput: false, CloseRequested: false }
                ? camera.dragItem : null;
            if (!GameInventoryAdapter.Owns(camera.body, dragItem)) dragItem = null;
            plugin._nativePanel?.UpdateLoadoutDragHighlight(camera, dragItem);
            if (CanInteractWithDragActions(camera))
            {
                var item = GamePatches.PendingDragItem(camera, out var armed, out var originalLayout) ?? camera.dragItem;
                if (GameInventoryAdapter.Owns(camera.body, item))
                {
                    plugin._nativePanel?.UpdateDragActions(camera, item, armed, originalLayout);
                    return;
                }
            }
            plugin._nativePanel?.HideDragActions();
        }
        internal static bool IsNativeItemTooltip(GameObject target) => IsVisible && !Instance!._closing &&
            Instance._nativePanel?.IsNativeItemTooltip(target) == true;
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

        internal static void SetTerminalDrag(PlayerCamera camera, Item? item)
        {
            if (Instance is { } plugin && camera == plugin._sessionCamera) plugin._terminalDragItem = item;
        }

        internal static bool IsTerminalDrag(PlayerCamera camera, Item item) => Instance is { } plugin &&
            camera == plugin._sessionCamera && ReferenceEquals(plugin._terminalDragItem, item);

        internal static bool CanDiscardAtPointer(PlayerCamera camera, Item source, Vector2 pointer) =>
            CanInteractWithDragActions(camera) && IsTerminalDrag(camera, source) &&
            Application.isFocused && !SettingsBridge.IsTogglePressed() && !Input.GetKeyDown(KeyBinds.GetBind("pause")) &&
            TerminalDragGesture.CanDiscard(Input.GetKeyUp(KeyBinds.GetBind("iteminteract")), Input.GetMouseButtonDown(1),
                IsTerminalPoint(pointer), pointer.x, pointer.y, Screen.width, Screen.height, (pointer - camera.clickPos).sqrMagnitude);

        internal static InvButton? ResolveTerminalDrop(PlayerCamera camera)
        {
            var panel = Instance?._nativePanel;
            return panel?.ContainsScreenPoint(Input.mousePosition) == true ? panel.ResolveDrop(camera) : null;
        }
        internal static void ReleaseSearchFocus() => Instance?._nativePanel?.Chrome?.ReleaseSearchFocus();
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
                try
                {
                    if (!ReferenceEquals(_sessionCamera, null)) GamePatches.CancelPendingAction(_sessionCamera!);
                }
                finally
                {
                    try { _nativePanel?.SetVisible(false); }
                    finally { _shield?.SetVisible(false); }
                }
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
            if (_sessionCamera)
            {
                if (!handOff) CancelDrag(_sessionCamera!);
                _sessionCamera!.radialOpen = _radialOpen;
            }
            _terminalDragItem = null;
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
            GamePatches.CancelPendingAction(camera);
            SetTerminalDrag(camera, null);
            camera.dragItem = null;
            if (camera.dragImage) camera.dragImage.enabled = false;
            UpdateDragActions(camera);
        }
        internal static bool CanInteract(PlayerCamera camera) => IsVisible && Instance!.SessionValid() && camera == Instance._sessionCamera;
        internal static List<RaycastResult> FilterHits(List<RaycastResult> hits)
        {
            if (!IsVisible) return hits;
            var result = new List<RaycastResult>();
            var plugin = Instance!;
            if (plugin._nativePanel?.FluidDialog is { BlocksInput: true } dialog)
            {
                foreach (var hit in hits)
                {
                    if (!hit.gameObject) continue;
                    if (dialog.Contains(hit.gameObject) || plugin._shield?.IsBlocker(hit.gameObject) == true)
                    {
                        result.Add(hit);
                        break;
                    }
                }
                return result;
            }
            RaycastResult blocker = default;
            bool hasBlocker = false;
            var terminalPoint = IsTerminalPoint(Input.mousePosition);
            foreach (var rawHit in hits)
            {
                var hit = rawHit;
                if (!hit.gameObject) continue;
                // Keep the marker through repeated filtering. Only a confirmed drain call uses
                // the native drain object; a drag-action hit must not fall through to item slots.
                if (plugin._window?.BlocksInventoryInput != true && TryGetDragAction(hit.gameObject, out _))
                    return new List<RaycastResult> { hit };
                if (plugin._shield?.IsBlocker(hit.gameObject) == true)
                {
                    if (!hasBlocker) { blocker = hit; hasBlocker = true; }
                    continue;
                }
                // Keep the blocker hit, but exclude all item targets while settings own the pointer.
                if (plugin._window?.BlocksInventoryInput == true) continue;
                // Extension controls receive ordinary UGUI events, not native item actions.
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
            // Install this independently of the input gate: it only replaces terminal item hover text.
            Patch(harmony, typeof(UIUtil), nameof(UIUtil.GetUITooltip), nameof(NativeItemTooltipPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleInput", nameof(PlayerCameraPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleDragging", nameof(DraggingPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleWhileDragging", nameof(WhileDraggingPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleWorldHover", nameof(WorldHoverPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPickupFromWorld", nameof(WorldPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPerformWorldActions", nameof(WorldPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPickupFromUI", nameof(PickupPrefix), log, nameof(PickupPostfix));
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleReleaseDragging", nameof(ReleasePrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPerformUIActions", nameof(InventoryActionPrefix), log);
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
        private static bool NativeItemTooltipPrefix(List<RaycastResult> __0, ref (string, string) __result)
        {
            if (!Plugin.IsVisible) return true;
            foreach (var hit in __0)
            {
                if (!hit.gameObject || !hit.gameObject.GetComponent<UITooltip>()) continue;
                // Only replace a tooltip that vanilla would have picked from a terminal item cell.
                // A native button still needs its UITooltip component for UpdateGraphic().
                if (!Plugin.IsNativeItemTooltip(hit.gameObject)) return true;
                __result = ("", "");
                return false;
            }
            return true;
        }

        private static bool WorldHoverPrefix(ref UsableObject usableObject)
        {
            if (!Plugin.BlocksGameplay) return true;
            // HandleMouseOver still resets the native world outline. GlobalDark owns UI
            // tooltips independently; neither path needs a borrowed radialOpen or cast list.
            usableObject = null!;
            return false;
        }
        private static bool DraggingPrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            if (!Plugin.BlocksGameplay) return true;
            // Keep the closing input blocked without clearing the drag handed back to the game.
            if (!Plugin.IsVisible) return false;
            if (!Plugin.CanInteract(__instance)) { Plugin.CancelDrag(__instance); return false; }
            if (Plugin.FluidBlocksInput) return false;
            if (!__instance.dragItem) Plugin.SetTerminalDrag(__instance, null);
            uiCasts = Plugin.FilterHits(uiCasts);
            if (_pendingAction is { } pending && pending.Camera == __instance)
            {
                // A new native drag supersedes a pending action, never clears the new item.
                // While an action is armed, the synthetic native call owns the frame; otherwise
                // vanilla/Together can run a second HandleWhileDragging without the captured item.
                if (__instance.dragItem) CancelPendingAction(__instance);
                else if (HandlePendingAction(__instance, pending)) return false;
            }
            if (__instance.dragItem is { } source && Plugin.DragActionAtPointer(__instance, source, out var actionLayout) is { } action)
            {
                var dragKey = KeyBinds.GetBind("iteminteract");
                var release = Input.GetKeyUp(dragKey);
                if (action == TerminalDragAction.Pin)
                {
                    // Pins are a display-only drop, not a native item move or a pending Use/Drain.
                    if (release || Input.GetMouseButtonDown(1))
                    {
                        if (release && !Input.GetMouseButtonDown(1)) Plugin.TogglePin(__instance, source);
                        Plugin.CancelDrag(__instance);
                        return false;
                    }
                }
                else
                {
                    var gesture = TerminalDragGesture.Begin(release, Input.GetMouseButtonDown(0), Input.GetMouseButtonDown(1));
                    if (gesture != TerminalDragStart.None)
                    {
                        // Capture before vanilla treats the confirm click as a new pickup or release.
                        var fromTerminal = Plugin.IsTerminalDrag(__instance, source);
                        Plugin.CancelDrag(__instance);
                        if (gesture != TerminalDragStart.Cancel)
                        {
                            var pendingAction = new PendingAction(__instance, source, action, actionLayout, fromTerminal);
                            _pendingAction = pendingAction;
                            if (gesture == TerminalDragStart.Confirm)
                                HandlePendingAction(__instance, pendingAction, resumeDrag: !release && Input.GetKey(dragKey));
                            Plugin.UpdateDragActions(__instance);
                        }
                        return false;
                    }
                }
            }
            if (Plugin.SearchFocused)
            {
                // Key-up still ends an existing drag while search owns the pointer. Don't hand
                // it to vanilla: the pointer may now be over a container or a drag action.
                if (Input.GetKeyUp(KeyBinds.GetBind("iteminteract")))
                {
                    if (__instance.dragItem) Plugin.CancelDrag(__instance);
                    return false;
                }
                // A fixed inventory cell is not necessarily selectable; release search on its native click path too.
                var pointerDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
                if (!pointerDown || !uiCasts.Exists(hit => Plugin.IsInventorySurface(hit.gameObject))) return false;
                Plugin.ReleaseSearchFocus();
            }
            if (__instance.dragItem && !GameInventoryAdapter.Owns(__instance.body, __instance.dragItem)) Plugin.CancelDrag(__instance);
            // Keep Together's world-hover branch from acquiring another body.
            __instance.radialOpen = true;
            return true;
        }
        private sealed class PendingAction(PlayerCamera camera, Item item, TerminalDragAction action,
            TerminalDragAction actionLayout, bool fromTerminal)
        {
            internal PlayerCamera Camera { get; } = camera;
            internal Body Body { get; } = camera.body;
            internal Item Item { get; } = item;
            internal TerminalDragAction Action { get; } = action;
            internal TerminalDragAction ActionLayout { get; } = actionLayout;
            internal bool FromTerminal { get; } = fromTerminal;
            internal bool Draining { get; set; }
            internal bool MultiplayerDrain { get; set; }
            internal bool ResumeDragAfterAction { get; set; }
        }

        private static readonly MethodInfo WhileDragging = AccessTools.Method(typeof(PlayerCamera), "HandleWhileDragging");
        private static PendingAction? _pendingAction;
        private static PlayerCamera? _nativeDrainCall;

        internal static Item? PendingDragItem(PlayerCamera camera, out TerminalDragAction action, out TerminalDragAction actionLayout)
        {
            if (_pendingAction is { } pending && pending.Camera == camera)
            {
                action = pending.Action;
                actionLayout = pending.ActionLayout;
                return pending.Item;
            }
            action = actionLayout = TerminalDragAction.None;
            return null;
        }

        private static bool IsOverPendingAction(PlayerCamera camera, PendingAction pending) => camera && camera.body == pending.Body &&
            Plugin.DragActionAtPointer(camera, pending.Item, out _, pending.Action, pending.ActionLayout) == pending.Action;

        private static bool DragKeyHeld(PlayerCamera camera)
        {
            var key = KeyBinds.GetBind("iteminteract");
            return key != KeyCode.None && key != KeyCode.Mouse0 && key != KeyCode.Mouse1 && key != KeyCode.Mouse2 &&
                key != KeyCode.Mouse3 && key != KeyCode.Mouse4 && key != KeyCode.Mouse5 && key != KeyCode.Mouse6 && Input.GetKey(key);
        }

        private static bool HandlePendingAction(PlayerCamera camera, PendingAction pending, bool resumeDrag = false)
        {
            var overTarget = IsOverPendingAction(camera, pending);
            var step = TerminalDragGesture.Step(pending.Action, overTarget, Input.GetMouseButtonDown(1),
                Input.GetMouseButtonDown(0), Input.GetMouseButton(0), pending.Draining);
            switch (step)
            {
                case TerminalDragStep.Cancel:
                    CancelPendingAction(camera);
                    // Moving off the action and starting a new drag in the same frame should work.
                    return overTarget;
                case TerminalDragStep.Wait:
                    return true;
                case TerminalDragStep.StopDrain:
                    var canFinishDrain = !pending.MultiplayerDrain || camera && camera.body == pending.Body &&
                        GameInventoryAdapter.Owns(pending.Body, pending.Item) && GameInventoryAdapter.IsTogetherClient &&
                        GameInventoryAdapter.CanSyncFluidDrain(pending.Item);
                    var stopped = pending.Draining && canFinishDrain && RunNativeDrain(pending, new List<RaycastResult>());
                    pending.Draining = false;
                    if (!stopped && pending.MultiplayerDrain) GameInventoryAdapter.ResetFluidDrainSync();
                    var keepAfterDrain = stopped && SettingsBridge.KeepDraggingAfterUse && IsOverPendingAction(camera, pending);
                    if (keepAfterDrain)
                    {
                        _pendingAction = null;
                        CompleteAction(camera, pending, pending.ResumeDragAfterAction && DragKeyHeld(camera));
                    }
                    else CancelPendingAction(camera);
                    return true;
                case TerminalDragStep.Use:
                    // Clear first: use may consume the item, open a modal or run another mod's hooks.
                    CancelPendingAction(camera);
                    if (!pending.Body.DoPickupCheck(pending.Item)) return true;
                    pending.Body.UseItem(pending.Item); // Includes Together's native use hook.
                    CompleteAction(camera, pending, resumeDrag);
                    return true;
                case TerminalDragStep.Drain:
                    if (!GameInventoryAdapter.CanStartFluidDrain(pending.Item))
                    {
                        CancelPendingAction(camera);
                        camera.DoAlert(TerminalText.Get("fluidDrain.unavailable"));
                        return true;
                    }
                    pending.MultiplayerDrain = GameInventoryAdapter.IsTogetherClient;
                    if (!GameInventoryAdapter.PrepareFluidDrainSync(pending.Item))
                    {
                        GameInventoryAdapter.ResetFluidDrainSync();
                        CancelPendingAction(camera);
                        camera.DoAlert(TerminalText.Get("fluidDrain.unavailable"));
                        return true;
                    }
                    pending.ResumeDragAfterAction = resumeDrag || DragKeyHeld(camera);
                    pending.Draining = true;
                    var drainHit = new RaycastResult { gameObject = camera.liquidDrainObject, screenPosition = Input.mousePosition };
                    if (!RunNativeDrain(pending, new List<RaycastResult> { drainHit }) ||
                        Plugin.DragActionAtPointer(camera, pending.Item, out _, pending.Action, pending.ActionLayout) != pending.Action)
                        CancelPendingAction(camera);
                    return true;
            }
            return false;
        }

        private static void CompleteAction(PlayerCamera camera, PendingAction pending, bool resumeDrag)
        {
            var keepDragging = SettingsBridge.KeepDraggingAfterUse;
            var sameTarget = keepDragging && !camera.dragItem && _pendingAction == null && IsOverPendingAction(camera, pending);
            switch (TerminalDragGesture.AfterAction(keepDragging, sameTarget, resumeDrag))
            {
                case TerminalDragAfterAction.KeepArmed:
                    _pendingAction = pending;
                    break;
                case TerminalDragAfterAction.ResumeDrag:
                    camera.dragItem = pending.Item;
                    Plugin.SetTerminalDrag(camera, pending.FromTerminal ? pending.Item : null);
                    if (camera.dragImage)
                    {
                        camera.dragImage.rectTransform.position = Input.mousePosition;
                        camera.dragImage.enabled = true;
                    }
                    break;
            }
            Plugin.UpdateDragActions(camera);
        }

        internal static void CancelPendingAction(PlayerCamera camera)
        {
            if (_pendingAction is not { } pending || !ReferenceEquals(pending.Camera, camera)) return;
            _pendingAction = null;
            // Together reports its final liquid amount when the native drain hover ends.
            // Keep the captured item and body until that existing hook has run.
            if (pending.Draining && pending.MultiplayerDrain)
            {
                if (camera && camera.body == pending.Body && GameInventoryAdapter.Owns(pending.Body, pending.Item) &&
                    GameInventoryAdapter.IsTogetherClient && GameInventoryAdapter.CanSyncFluidDrain(pending.Item))
                {
                    if (!RunNativeDrain(pending, new List<RaycastResult>()))
                    {
                        GameInventoryAdapter.ResetFluidDrainSync();
                        Plugin.Log.LogWarning("Could not finish Together liquid drain synchronization.");
                    }
                }
                else GameInventoryAdapter.ResetFluidDrainSync();
            }
            Plugin.UpdateDragActions(camera);
        }

        private static bool RunNativeDrain(PendingAction pending, List<RaycastResult> hits)
        {
            var camera = pending.Camera;
            var previous = camera.dragItem;
            var radialOpen = camera.radialOpen;
            camera.dragItem = pending.Item;
            camera.radialOpen = true; // Do not enter Together's world-hover branch on confirmation.
            _nativeDrainCall = camera;
            try { WhileDragging.Invoke(camera, new object[] { hits }); return true; }
            catch (TargetInvocationException ex) { Plugin.Log.LogWarning("Liquid drain failed: " + ex.InnerException); return false; }
            finally
            {
                _nativeDrainCall = null;
                camera.radialOpen = radialOpen;
                if (ReferenceEquals(camera.dragItem, pending.Item)) camera.dragItem = previous;
                if (!previous && camera.dragImage) camera.dragImage.enabled = false;
            }
        }

        private static bool WhileDraggingPrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            // The scoped call already has the native target. Re-filtering it would discard that hit.
            if (ReferenceEquals(_nativeDrainCall, __instance) || !Plugin.BlocksGameplay) return true;
            if (!Plugin.CanInteract(__instance) || Plugin.SearchFocused || Plugin.FluidBlocksInput) return false;
            uiCasts = Plugin.FilterHits(uiCasts);
            return true;
        }
        private static bool PickupPrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts, out bool __state)
        {
            __state = false;
            if (!Plugin.BlocksGameplay) return true;
            uiCasts = Plugin.FilterHits(uiCasts);
            if (!Plugin.CanInteract(__instance) || Plugin.FluidBlocksInput) return false;
            if (!__instance.dragItem)
            {
                Plugin.SetTerminalDrag(__instance, null);
                __state = uiCasts.Exists(hit => Plugin.IsInventorySurface(hit.gameObject) &&
                    hit.gameObject.GetComponent<InvButton>());
            }
            return true;
        }
        private static void PickupPostfix(PlayerCamera __instance, bool __state)
        {
            if (!Plugin.IsVisible || !__instance.dragItem) return;
            if (!GameInventoryAdapter.Owns(__instance.body, __instance.dragItem)) Plugin.CancelDrag(__instance);
            else if (__state) Plugin.SetTerminalDrag(__instance, __instance.dragItem);
        }

        // The empty list belongs to this specific native release only. Never interpret an
        // ordinary empty raycast (e.g. a blank cell or the input shield) as a discard.
        private sealed class DiscardRelease(PlayerCamera camera, Item item) : List<RaycastResult>
        {
            internal PlayerCamera Camera { get; } = camera;
            internal Item Item { get; } = item;
        }

        // Keep native release handling (including Together's drag cleanup), but give this
        // specific liquid pair to the terminal before swaps or automatic bag placement.
        private sealed class FluidRelease(TerminalFluidTransfer transfer) : List<RaycastResult>
        {
            internal TerminalFluidTransfer Transfer { get; } = transfer;
        }

        private static bool ReleasePrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            if (!Plugin.BlocksGameplay) return true;
            if (!Plugin.IsVisible || Plugin.FluidBlocksInput) return false;
            uiCasts = Plugin.FilterHits(uiCasts);
            // Revalidate before passing any target to the game's release path.
            var source = __instance.dragItem;
            if (!Plugin.CanInteract(__instance) || !GameInventoryAdapter.Owns(__instance.body, source))
            { Plugin.CancelDrag(__instance); return false; }
            // DraggingPrefix owns action clicks/releases. A stale visual raycast must not fall
            // through to the inventory drop path if the item is no longer eligible.
            if (uiCasts.Count == 1 && Plugin.TryGetDragAction(uiCasts[0].gameObject, out _))
            { Plugin.CancelDrag(__instance); return false; }
            var pointer = (Vector2)Input.mousePosition;
            if (Vector2.Distance(pointer, __instance.clickPos) >= 10f && !Input.GetMouseButtonDown(1) &&
                Plugin.ReorderPinnedItem(__instance, source, pointer)) return false;
            if (Plugin.CanDiscardAtPointer(__instance, source, pointer))
            {
                uiCasts = new DiscardRelease(__instance, source);
                return true;
            }
            if (Plugin.IsTerminalPoint(pointer))
            {
                var moved = Vector2.Distance(pointer, __instance.clickPos) >= 10f;
                var top = uiCasts.FirstOrDefault().gameObject;
                if (moved && !Input.GetMouseButtonDown(1) && Plugin.CanInteractWithDragActions(__instance) &&
                    Plugin.IsTerminalDrag(__instance, source) && top && Plugin.IsInventorySurface(top) &&
                    top.TryGetComponent<InvButton>(out var fluidTarget) &&
                    fluidTarget.GetItem() is { } destination && TerminalFluidTransfer.IsPair(source, destination))
                {
                    // Together's release hook also expects a registered drag item. Never hand it a
                    // locally valid liquid pair that the active network cannot synchronize.
                    if (!GameInventoryAdapter.CanSyncFluidTransfer(source, destination))
                    {
                        Plugin.CancelDrag(__instance);
                        __instance.DoAlert(TerminalText.Get("fluidTransfer.unavailable"));
                        return false;
                    }
                    // Even if the bottle is full or the source is empty, this is not a bag drop or slot swap.
                    uiCasts = new FluidRelease(new TerminalFluidTransfer(__instance, source, destination));
                    return true;
                }
                var slotHit = uiCasts.Find(hit => hit.gameObject && hit.gameObject.GetComponentInParent<LoadoutCell>());
                if (slotHit.gameObject)
                {
                    var slot = slotHit.gameObject.GetComponentInParent<LoadoutCell>();
                    var target = slot.Button;
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
            if (Plugin.IsTerminalPoint(pointer))
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
            // Blank content and non-discard releases over the shield do not change inventory.
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

        private static bool InventoryActionPrefix(PlayerCamera __instance, List<RaycastResult> uiCasts, ref bool __result)
        {
            if (uiCasts is FluidRelease fluid)
            {
                __result = true;
                var transfer = fluid.Transfer;
                if (Application.isFocused && Plugin.CanInteractWithDragActions(__instance) &&
                    transfer.Camera == __instance && ReferenceEquals(__instance.dragItem, transfer.Source))
                {
                    try { Plugin.OpenFluidTransfer(transfer); }
                    catch (Exception ex) { Plugin.Log.LogWarning("Could not open fluid transfer: " + ex); }
                }
                return false;
            }
            if (uiCasts is DiscardRelease discard)
            {
                // Run only after vanilla's DoPickupCheck and Together's release prefix have run.
                // Use native Drop/Unload hooks, not the world path (which can fill a world container).
                __result = true;
                var item = discard.Item;
                if (__instance == discard.Camera && ReferenceEquals(__instance.dragItem, item) &&
                    Plugin.CanDiscardAtPointer(__instance, item, Input.mousePosition) && GameInventoryAdapter.Owns(__instance.body, item))
                {
                    Plugin.SetTerminalDrag(__instance, null);
                    if (item.TryGetParentContainer(out var container))
                    {
                        container.UnloadItem(item);
                        __instance.PlayBackpackSound();
                    }
                    else if (__instance.body.HoldingItem(item)) __instance.body.DropItem(item);
                    else if (__instance.body.HasWearable(item)) __instance.body.DropWearable(item);
                }
                return false;
            }
            if (!Plugin.CanInteract(__instance) || uiCasts.Count == 0 || !uiCasts[0].gameObject) return true;
            var source = __instance.dragItem;
            var slot = uiCasts[0].gameObject.GetComponentInParent<LoadoutCell>();
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
        private static readonly PropertyInfo? NetworkClient = NetType == null ? null : AccessTools.Property(NetType, "IsClient");
        private static readonly Type? ItemSyncType = AccessTools.TypeByName("Together.ItemSync");
        private static readonly Type? SyncInfoType = AccessTools.TypeByName("Together.SyncInfo");
        private static readonly MethodInfo? TryGetSyncInfo = ItemSyncType == null || SyncInfoType == null ? null :
            AccessTools.Method(ItemSyncType, "TryGetSyncInfo", new[] { typeof(Item), SyncInfoType.MakeByRefType() });
        private static readonly MethodInfo? IgnoreForNetwork = ItemSyncType == null ? null :
            AccessTools.Method(ItemSyncType, "ItemCanBeIgnoredForNetwork", new[] { typeof(Item) });
        private static readonly MethodInfo? TransferPostfix = AccessTools.TypeByName("Together.LiquidTransfer_Finish_MultiplayerPatch") is { } patch ?
            AccessTools.Method(patch, "Postfix", new[] { typeof(LiquidTransfer) }) : null;
        private static readonly Type? DrainPatchType = AccessTools.TypeByName("Together.PlayerCamera_HandleWhileDragging_MultiplayerPatch");
        private static readonly MethodInfo? DrainPostfix = DrainPatchType == null ? null : AccessTools.Method(DrainPatchType, "Postfix");
        private static readonly FieldInfo? DrainingLiquid = DrainPatchType == null ? null : AccessTools.Field(DrainPatchType, "draining_liquid");
        private static readonly MethodInfo? NetworkWorldReady = AccessTools.TypeByName("Together.Multiplayer") is { } multiplayer ?
            AccessTools.Method(multiplayer, "IsNetworkActiveAndIsWorldGenerated", Type.EmptyTypes) : null;

        internal static bool CanSyncFluidTransfer(Item source, Item destination)
        {
            if (NetType == null) return true;
            try
            {
                if (NetworkRunning?.GetValue(null) is not bool running) return false;
                if (!running) return true;
                // In multiplayer, don't mutate local liquid if Together cannot announce it.
                return NetworkWorldReady?.Invoke(null, null) is true && TransferPostfix != null &&
                    Harmony.GetPatchInfo(TerminalFluidTransfer.NativeFinish)?.Postfixes.Any(patch => patch.PatchMethod == TransferPostfix) == true &&
                    IsRegistered(source) && IsRegistered(destination);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Together fluid transfer preflight failed: " + ex.Message);
                return false;
            }

            static bool IsRegistered(Item item) => IgnoreForNetwork?.Invoke(null, new object[] { item }) is false &&
                TryGetSyncInfo?.Invoke(null, new object?[] { item, null }) is true;
        }

        internal static bool CanStartFluidDrain(Item item) => item && item.TryGetComponent<WaterContainerItem>(out var liquid) &&
            liquid && liquid.CurrentTotal > 0f && CanSyncFluidDrain(item);

        internal static bool CanSyncFluidDrain(Item item)
        {
            if (!item || !item.TryGetComponent<WaterContainerItem>(out var liquid) || !liquid) return false;
            if (NetType == null) return true;
            try
            {
                if (NetworkRunning?.GetValue(null) is not bool running) return false;
                if (!running) return true;
                if (NetworkClient?.GetValue(null) is not bool isClient) return false;
                if (!isClient) return true;
                return NetworkWorldReady?.Invoke(null, null) is true && DrainPostfix != null && DrainingLiquid != null &&
                    Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerCamera), "HandleWhileDragging"))?.Postfixes
                        .Any(patch => patch.PatchMethod == DrainPostfix) == true &&
                    IgnoreForNetwork?.Invoke(null, new object[] { item }) is false &&
                    TryGetSyncInfo?.Invoke(null, new object?[] { item, null }) is true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Together fluid drain preflight failed: " + ex.Message);
                return false;
            }
        }

        internal static bool PrepareFluidDrainSync(Item item)
        {
            if (!IsTogetherClient) return true;
            if (!CanSyncFluidDrain(item) || DrainingLiquid == null) return false;
            try
            {
                // The Together postfix reports only a true-to-false drain transition. Prime its
                // existing latch so even a first-frame drain to zero emits the vanilla MP message.
                if (DrainingLiquid.GetValue(null) is false) DrainingLiquid.SetValue(null, true);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not prepare Together liquid drain synchronization: " + ex.Message);
                return false;
            }
        }

        internal static void ResetFluidDrainSync()
        {
            if (!IsTogetherClient) return;
            try { DrainingLiquid?.SetValue(null, false); }
            catch (Exception ex) { Plugin.Log.LogWarning("Could not reset Together liquid drain state: " + ex.Message); }
        }

        internal static bool IsTogetherClient
        {
            get
            {
                try { return NetworkRunning?.GetValue(null) is true && NetworkClient?.GetValue(null) is true; }
                catch { return false; }
            }
        }
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
