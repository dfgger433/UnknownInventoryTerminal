using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
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
    [BepInDependency("casualtiesunknown.webui", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        /// <summary>Stable BepInEx plugin identifier.</summary>
        public const string PluginGuid = "com.unknown.inventoryterminal";
        /// <summary>Human-readable plugin name.</summary>
        public const string PluginName = "Unknown Inventory Terminal";
        /// <summary>Plugin version.</summary>
        public const string PluginVersion = "0.4.0";
        internal static Plugin? Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; } = null!;
        internal static bool IsVisible => Instance?._window?.Visible == true;
        internal static bool BlocksGameplay => IsVisible || (Instance != null && Instance._closedFrame == Time.frameCount);
        private int _closedFrame = -1;
        private TerminalWindow? _window;
        private NativeFlatInventoryPanel? _nativePanel;
        private InputShield? _shield;
        private Harmony? _harmony;
        private IDisposable? _pinyin;
        private const int MaxSnapshotIconChars = 128 * 1024;
        private WebUiBridge? _webUi;
        private WebUiSpriteIcons? _webUiIcons;
        private JObject? _lastWebUiSnapshot;
        private bool _webUiRequested, _webUiReady, _checkFocusLoss, _ackWebUiChange;
        private float _nextWebUiProbe;
        private bool _inputReady, _sessionActive, _configuredByContainer, _textInputFocused, _clearGuiFocus;
        private PlayerCamera? _sessionCamera;
        private Body? _sessionBody;
        private CursorLockMode _cursorLock;
        private bool _cursorVisible, _forceNoMovementValue, _radialOpen;
        private GameObject? _selectedObject;

        private void Awake()
        {
            Instance = this; Log = Logger;
            _harmony = new Harmony(PluginGuid);
            SettingsBridge.Initialize(Config, _harmony, Logger);
            ContainerPlacement.Initialize(Config);
            _inputReady = GamePatches.Apply(_harmony, Logger);
            _shield = new InputShield();
            _nativePanel = new NativeFlatInventoryPanel(Logger);
            var adapter = new GameInventoryAdapter(Logger);
            _window = new TerminalWindow(adapter.Capture,
                entries => _nativePanel.SetEntries(entries),
                ex => Logger.LogWarning("Terminal read failed: " + ex.Message),
                (bounds, mode) => _nativePanel.SetLayout(bounds, mode));
            _configuredByContainer = SettingsBridge.ByContainer.Value;
            _window.Mode = _configuredByContainer ? InventoryViewMode.ByContainer : InventoryViewMode.Merged;
            _webUi = new WebUiBridge(Logger);
            _webUiIcons = new WebUiSpriteIcons(Logger);
            _webUi.EventReceived += HandleWebUiEvent;
            _webUi.Closed += HandleWebUiClosed;
            _window.StateChanged += () =>
            {
                PublishWebUiState(force: _ackWebUiChange);
                _ackWebUiChange = false;
            };
            _pinyin = PinyinBridge.Register(() => SettingsBridge.Pinyin.Value, Logger);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded; native inventory bridge " + (_inputReady ? "ready." : "disabled: required input hooks unavailable."));
        }
        private void Update()
        {
            if (!_webUiRequested && _webUi != null && Time.unscaledTime >= _nextWebUiProbe)
            {
                _nextWebUiProbe = Time.unscaledTime + 2f;
                if (_webUi.TryCreate())
                {
                    _webUiRequested = true;
                    if (IsVisible)
                    {
                        ShowWebUi();
                    }
                }
            }
            if (_webUi?.Visible == true && !_sessionActive && _window != null && CanToggle()) Open();
            if (_checkFocusLoss && (!IsVisible || !WebUiBridge.IsGameForeground()))
            {
                _checkFocusLoss = false;
                if (IsVisible) Close();
            }
            UpdateTerminal();
            if (_webUi?.Visible == true) _webUiIcons?.Update();
        }

        private void UpdateTerminal()
        {
            if (_window == null) return;
            if (IsVisible && (!SessionValid() || Input.GetKeyDown(KeyBinds.GetBind("pause")))) Close();
            if (SettingsBridge.IsTogglePressed() && CanToggle())
            {
                if (IsVisible)
                {
                    if (_webUi?.IsAvailable == true && !_webUi.Visible) ShowWebUi();
                    else Close();
                }
                else Open();
            }
            if (_configuredByContainer != SettingsBridge.ByContainer.Value)
            {
                _configuredByContainer = SettingsBridge.ByContainer.Value;
                _window.Mode = _configuredByContainer ? InventoryViewMode.ByContainer : InventoryViewMode.Merged;
            }
            _window.Update();
            if (_ackWebUiChange) { PublishWebUiState(force: true); _ackWebUiChange = false; }
            // The close button closes inside TerminalWindow.Update, bypassing Toggle.
            if (!_window.Visible && _sessionActive) Close();
            else if (_window.Visible)
            {
                var byContainer = _window.Mode == InventoryViewMode.ByContainer;
                if (byContainer != _configuredByContainer)
                {
                    _configuredByContainer = byContainer;
                    SettingsBridge.ByContainer.Value = byContainer;
                }
                HoldInput();
            }
            _nativePanel?.SetVisible(_window.Visible);
            _shield?.SetVisible(_window.Visible);
        }
        private void HandleWebUiClosed()
        {
            // The WebUI panel remains registered and may reconnect its host; do not
            // call CreatePanel again with the same stable ID after a browser close.
            if (IsVisible) Close();
        }

        private void HandleWebUiEvent(string name, object? payload)
        {
            var json = payload as JToken;
            switch (name)
            {
                case "webui.ready":
                    _webUiReady = true;
                    PublishWebUiState(force: true);
                    break;
                case "close":
                case "uit.close":
                    if (IsVisible) Close();
                    break;
                case "refresh":
                case "uit.refresh":
                    if (IsVisible) { _window?.RefreshNow(); PublishWebUiState(force: true); }
                    break;
                case "query":
                case "uit.query":
                    if (_webUi?.Visible == true && IsVisible)
                    {
                        var query = ReadWebString(json, "query") ?? "";
                        SearchApi.Query = query.Length > 256 ? query.Substring(0, 256) : query;
                        _ackWebUiChange = true;
                    }
                    break;
                case "mode":
                case "uit.mode":
                    if (_webUi?.Visible != true || !IsVisible) break;
                    var mode = ReadWebString(json, "mode");
                    if (string.Equals(mode, "container", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "byContainer", StringComparison.OrdinalIgnoreCase))
                        SettingsBridge.ByContainer.Value = true;
                    else if (string.Equals(mode, "merged", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
                        SettingsBridge.ByContainer.Value = false;
                    _ackWebUiChange = true;
                    break;
                case "interaction":
                case "uit.interaction":
                    if (json is JObject interaction && ReadWebString(interaction, "action") == "select")
                        SelectWebItem(ReadWebString(interaction, "instanceKey"));
                    break;
                case "uit.native":
                    if (_webUi?.Visible == true && SessionValid()) _webUi.Visible = false;
                    break;
            }
        }

        private static string? ReadWebString(JToken? payload, string field)
        {
            var value = payload is JObject obj ? obj[field] : payload;
            return value?.Type == JTokenType.String ? value.Value<string>() : null;
        }

        private void SelectWebItem(string? instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey) || _webUi?.Visible != true || _window == null || !SessionValid()) return;
            var entry = _window.Rows.FirstOrDefault(row => row.First.InstanceKey == instanceKey)?.First;
            if (entry?.NativeItem is not Item item || !item || item.id != entry.TypeId ||
                !item.TryGetParentContainer(out var parent) || !parent ||
                parent.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture) != entry.ContainerKey ||
                !GameInventoryAdapter.Owns(_sessionBody, item)) return;
            // Leave the WebView visible unless there is a real native item button to hand off to.
            if (_nativePanel?.TryFocusInstance(entry) == true) _webUi.Visible = false;
        }

        private void ShowWebUi()
        {
            if (_webUi?.IsAvailable != true) return;
            _webUi.Visible = true;
            PublishWebUiState(force: true);
        }

        private void PublishWebUiState(bool force = false)
        {
            if (_webUi?.Visible != true || !_webUiReady || _window?.Visible != true) return;
            var iconBudget = MaxSnapshotIconChars;
            var snapshot = _window.Rows.Select(row =>
            {
                var icon = _webUiIcons?.GetOrQueue(row.First.Icon);
                if (icon != null && icon.Length > iconBudget) icon = null;
                iconBudget -= icon?.Length ?? 0;
                return new
                {
                    instanceKey = row.First.InstanceKey,
                    typeId = row.First.TypeId,
                    displayName = row.First.DisplayName,
                    containerKey = row.First.ContainerKey,
                    containerName = row.First.ContainerName,
                    count = row.Count,
                    icon
                };
            }).ToArray();
            var mode = _window.Mode == InventoryViewMode.ByContainer ? "container" : "merged";
            var payload = JObject.FromObject(new { items = snapshot, total = _window.Snapshot.Count, groups = _window.Rows.Count,
                mode, query = _window.ProjectedQuery, status = _window.Status });
            if (!force && JToken.DeepEquals(payload, _lastWebUiSnapshot)) return;
            if (_webUi.SendEvent("uit.snapshot", payload)) _lastWebUiSnapshot = payload;
            _webUi.SendEvent("inventory.snapshot", payload);
            var statusPayload = new { text = _window.Status, count = _window.Snapshot.Count, groups = _window.Rows.Count };
            _webUi.SendEvent("uit.status", statusPayload);
            _webUi.SendEvent("inventory.status", statusPayload);
            var queryPayload = new { query = SearchApi.Query };
            _webUi.SendEvent("uit.query", queryPayload);
            _webUi.SendEvent("inventory.query", queryPayload);
            var modePayload = new { mode };
            _webUi.SendEvent("uit.mode", modePayload);
            _webUi.SendEvent("inventory.mode", modePayload);
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
        internal static bool IsInventorySurface(GameObject target) =>
            Instance?._nativePanel?.Contains(target) == true || Instance?._shield?.IsNativeControl(target) == true;
        internal static bool IsTerminalPoint(Vector2 screenPoint) =>
            Instance?._nativePanel?.ContainsScreenPoint(screenPoint) == true;
        internal static InvButton? ResolveTerminalDrop(PlayerCamera camera, IReadOnlyList<RaycastResult> hits) =>
            Instance?._nativePanel?.ResolveDrop(camera, hits);
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
        private void Open()
        {
            var camera = PlayerCamera.main;
            if (!_inputReady || _window == null || !GameInventoryAdapter.IsLocalCamera(camera) ||
                !EventSystem.current || !camera.body.alive || !camera.body.conscious || camera.dragItem || NativeModalOpen(camera)) return;
            _sessionCamera = camera; _sessionBody = camera.body;
            _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
            _forceNoMovementValue = _sessionBody.forceNoMovement;
            _radialOpen = camera.radialOpen;
            _selectedObject = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(null);
            _sessionActive = true;
            camera.radialOpen = true;
            HoldInput();
            _shield?.SetVisible(true);
            _window.Open();
            ShowWebUi();
            _nativePanel?.SetVisible(true);
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
        private void Close()
        {
            _window?.Close();
            if (_webUi != null) _webUi.Visible = false;
            EndSession();
            _nativePanel?.SetVisible(false);
            _shield?.SetVisible(false);
        }
        private void EndSession()
        {
            if (!_sessionActive) return;
            _sessionActive = false;
            _closedFrame = Time.frameCount;
            _textInputFocused = false; _clearGuiFocus = true;
            if (_sessionCamera)
            {
                CancelDrag(_sessionCamera!);
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
        internal static void SetNativeInventoryInteraction(bool active) => Instance?._shield?.SetNativeInventoryInteraction(active);
        internal static List<RaycastResult> FilterHits(List<RaycastResult> hits)
        {
            if (!IsVisible) return hits;
            var result = new List<RaycastResult>();
            var plugin = Instance!;
            RaycastResult blocker = default;
            bool hasBlocker = false;
            bool? inventorySurface = null;
            var terminalPoint = IsTerminalPoint(Input.mousePosition);
            foreach (var hit in hits)
            {
                if (!hit.gameObject) continue;
                // The full-screen shield is deliberately below native inventory canvases.
                // Unity can still report it first when canvases have different raycasters;
                // defer it until we know no native/terminal surface is under the pointer.
                if (plugin._shield?.IsBlocker(hit.gameObject) == true)
                {
                    if (!hasBlocker) { blocker = hit; hasBlocker = true; }
                    continue;
                }
                var native = plugin._shield?.IsNativeControl(hit.gameObject) == true;
                var terminal = plugin._nativePanel?.Contains(hit.gameObject) == true;
                // The terminal storage area owns the pointer while it is under the cursor;
                // this prevents the hidden radial canvas from winning the release raycast.
                if (terminalPoint && native && !terminal) continue;
                if (!terminalPoint && terminal) continue;
                // Only the foremost inventory surface receives manual raycasts. Unknown UI
                // remains opaque so clicks cannot leak through the modal terminal.
                if (!native && !terminal) break;
                if (inventorySurface.HasValue && inventorySurface.Value != native) continue;
                inventorySurface = native;
                if (ValidHit(hit)) result.Add(hit);
            }
            if (result.Count == 0 && !inventorySurface.HasValue && hasBlocker) result.Add(blocker);
            return result;
        }
        internal static bool ValidHit(RaycastResult hit)
        {
            if (!hit.gameObject || !PlayerCamera.main) return false;
            var camera = PlayerCamera.main;
            var placement = hit.gameObject.GetComponent<ContainerPlacementTarget>() ??
                hit.gameObject.GetComponentInParent<ContainerPlacementTarget>();
            if (placement && !placement.IsValid(camera)) return false;
            if (camera.containerMenu && hit.gameObject.transform.IsChildOf(camera.containerMenu.transform) &&
                (!camera.currentContainer || !GameInventoryAdapter.Owns(camera.body, camera.currentContainer.GetComponent<Item>()))) return false;
            var button = hit.gameObject.GetComponent<InvButton>();
            if (button)
            {
                var item = button.GetItem();
                // Empty body slots remain valid drop targets, but never remote slots.
                if (button.isBody && !GameInventoryAdapter.ButtonUsesLocalBody(button)) return false;
                return item ? GameInventoryAdapter.Owns(PlayerCamera.main.body, item) : button.isBody;
            }
            var label = hit.gameObject.GetComponent<ItemLabel>();
            return !label || GameInventoryAdapter.Owns(PlayerCamera.main.body, label.refItem);
        }
        private void OnApplicationFocus(bool focused)
        {
            _checkFocusLoss = !focused && IsVisible && _webUi?.Visible == true;
            if (!focused && !_checkFocusLoss) Close();
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            _webUi?.Dispose();
            _pinyin?.Dispose(); _shield?.Dispose(); _nativePanel?.Dispose(); _harmony?.UnpatchSelf();
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
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleMouseOver", nameof(MouseOverPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPickupFromWorld", nameof(WorldPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPerformWorldActions", nameof(WorldPrefix), log);
            ready &= Patch(harmony, typeof(PlayerCamera), "TryPickupFromUI", nameof(PickupPrefix), log, nameof(PickupPostfix));
            ready &= Patch(harmony, typeof(PlayerCamera), "HandleReleaseDragging", nameof(ReleasePrefix), log, nameof(ReleasePostfix));
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
        private static bool MouseOverPrefix(PlayerCamera __instance, ref UsableObject usableObject)
        {
            if (!Plugin.BlocksGameplay) return true;
            usableObject = null!;
            if (__instance.hoverSquare) __instance.hoverSquare.enabled = false;
            return false;
        }
        private static bool DraggingPrefix(PlayerCamera __instance, ref List<RaycastResult> uiCasts)
        {
            if (!Plugin.BlocksGameplay) return true;
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
            // Once a terminal item is picked up, expose the original inventory canvas as a drop target.
            Plugin.SetNativeInventoryInteraction(__instance.dragItem);
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
            uiCasts = Plugin.FilterHits(uiCasts);
            // Dropping anywhere in the terminal resolves to a real native container button.
            if (Plugin.IsTerminalPoint(Input.mousePosition))
            {
                var destination = Plugin.ResolveTerminalDrop(__instance, uiCasts);
                if (destination)
                {
                    var nativeHit = uiCasts.Find(hit => hit.gameObject && hit.gameObject.GetComponent<InvButton>() == destination);
                    if (!nativeHit.gameObject)
                    {
                        var module = uiCasts.FirstOrDefault(hit => hit.module != null).module;
                        nativeHit = new RaycastResult
                        {
                            gameObject = destination!.gameObject,
                            module = module,
                            screenPosition = Input.mousePosition,
                            sortingLayer = destination.gameObject.layer,
                            sortingOrder = short.MaxValue - 1,
                            distance = 0
                        };
                    }
                    uiCasts.Insert(0, nativeHit);
                }
            }
            // Check again at release, since the snapshot can outlive a transfer/drop.
            if (!Plugin.CanInteract(__instance) || !GameInventoryAdapter.Owns(__instance.body, __instance.dragItem))
            { Plugin.CancelDrag(__instance); return false; }
            var action = false;
            foreach (var hit in uiCasts)
            {
                if (!Plugin.ValidHit(hit)) continue;
                if (hit.gameObject.GetComponent<InvButton>() || hit.gameObject.CompareTag("RadialCenter") ||
                    (hit.gameObject.CompareTag("ContainerBack") && __instance.currentContainer &&
                     GameInventoryAdapter.Owns(__instance.body, __instance.currentContainer.GetComponent<Item>())))
                { action = true; break; }
            }
            if (action) return true;
            // Releasing over the shield/blank content cancels, rather than unloading
            // into the world underneath the modal window.
            Plugin.CancelDrag(__instance);
            return false;
        }
        private static void ReleasePostfix(PlayerCamera __instance)
        {
            if (Plugin.IsVisible) Plugin.SetNativeInventoryInteraction(false);
        }
        private static bool WorldPrefix() => !Plugin.BlocksGameplay;
        private static bool ChatPrefix() => !Plugin.BlocksGameplay;
    }

    internal sealed class GameInventoryAdapter
    {
        private readonly ManualLogSource _log;
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

        internal GameInventoryAdapter(ManualLogSource log) { _log = log; }
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
        internal IReadOnlyList<InventoryEntry> Capture()
        {
            var camera = PlayerCamera.main;
            if (!IsLocalCamera(camera)) return Array.Empty<InventoryEntry>();
            var result = new List<InventoryEntry>();
            try
            {
                foreach (var (item, container) in EnumerateItems(camera.body))
                {
                    if (!item || !container || string.IsNullOrEmpty(item.id)) continue;
                    var containerItem = container.GetComponent<Item>();
                    if (!containerItem) continue;
                    var sprite = item.GetComponent<SpriteRenderer>();
                    result.Add(new InventoryEntry(Key(item), item.id, item.fullName, Key(container), containerItem.fullName,
                        item, sprite ? sprite.sprite : null));
                }
            }
            catch (Exception ex) { _log.LogWarning("Inventory enumeration failed: " + ex.Message); return Array.Empty<InventoryEntry>(); }
            return result;
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
        private static string Key(UnityEngine.Object value) => value.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
