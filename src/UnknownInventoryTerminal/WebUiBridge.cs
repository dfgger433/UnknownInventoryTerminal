using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;

namespace UnknownInventoryTerminal
{
    /// <summary>Optional reflection adapter for the Casualties Unknown WebUI host.</summary>
    internal sealed class WebUiBridge : IDisposable
    {
        private readonly ManualLogSource _log;
        private readonly List<(EventInfo Event, Delegate Handler)> _subscriptions = new List<(EventInfo, Delegate)>();
        private object? _panel;
        private PropertyInfo? _visible;
        private MethodInfo? _sendEvent;
        private bool _disposed;

        internal WebUiBridge(ManualLogSource log) { _log = log; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        // WebUI is an HWND child of the game. Unity can lose keyboard focus while
        // its top-level window remains foreground (e.g. while typing into WebView2).
        internal static bool IsGameForeground()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            var window = GetForegroundWindow();
            return window != IntPtr.Zero && GetWindowThreadProcessId(window, out var processId) != 0 &&
                processId == GetCurrentProcessId();
        }

        internal bool IsAvailable => _panel != null;
        internal bool Visible
        {
            get => _panel != null && _visible?.GetValue(_panel) is bool value && value;
            set
            {
                if (_panel == null || _visible == null) return;
                try { _visible.SetValue(_panel, value); }
                catch (Exception ex) { _log.LogWarning("WebUI visibility update failed: " + ex.Message); }
            }
        }

        internal event Action<string, object?>? EventReceived;
        internal event Action? Closed;

        internal bool TryCreate()
        {
            if (_disposed || _panel != null) return _panel != null;
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "WebUI.Plugin", StringComparison.OrdinalIgnoreCase));
                var apiType = assembly?.GetType("WebUI.Plugin.WebUiApi") ?? Type.GetType("WebUI.Plugin.WebUiApi, WebUI.Plugin", false);
                var optionsType = assembly?.GetType("WebUI.Plugin.WebUiPanelOptions") ?? Type.GetType("WebUI.Plugin.WebUiPanelOptions, WebUI.Plugin", false);
                var create = apiType?.GetMethod("CreatePanel", BindingFlags.Public | BindingFlags.Static);
                if (optionsType == null || create == null) return false;

                var root = Path.GetFullPath(Path.Combine(Paths.PluginPath, "UnknownInventoryTerminal", "Web"));
                var options = Activator.CreateInstance(optionsType)!;
                optionsType.GetProperty("RootPath")?.SetValue(options, root);
                optionsType.GetProperty("EntryRelativeUrl")?.SetValue(options, "index.html");
                optionsType.GetProperty("Id")?.SetValue(options, "unknown-inventory-terminal");
                optionsType.GetProperty("StartVisible")?.SetValue(options, false);
                _panel = create.Invoke(null, new[] { options });
                if (_panel == null) return false;

                var panelType = _panel.GetType();
                _visible = panelType.GetProperty("Visible");
                _sendEvent = panelType.GetMethod("SendEvent", new[] { typeof(string), typeof(object) });
                Subscribe(panelType.GetEvent("EventReceived"), nameof(HandleEvent));
                Subscribe(panelType.GetEvent("Closed"), nameof(HandleClosed));
                Subscribe(panelType.GetEvent("Error"), nameof(HandleError));
                _log.LogInfo("WebUI inventory panel created: " + root);
                return true;
            }
            catch (Exception ex)
            {
                _panel = null;
                _log.LogInfo("WebUI host unavailable; using native inventory terminal: " + ex.GetBaseException().Message);
                return false;
            }
        }

        internal bool SendEvent(string name, object payload)
        {
            if (_panel == null || _sendEvent == null || string.IsNullOrEmpty(name)) return false;
            try { return _sendEvent.Invoke(_panel, new[] { name, payload }) is bool sent && sent; }
            catch (Exception ex) { _log.LogWarning("WebUI event failed (" + name + "): " + ex.GetBaseException().Message); return false; }
        }

        private void Subscribe(EventInfo? eventInfo, string methodName)
        {
            if (eventInfo?.EventHandlerType == null) return;
            var invoke = eventInfo.EventHandlerType.GetMethod("Invoke");
            if (invoke == null) return;
            var parameters = invoke.GetParameters();
            var args = parameters.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var method = typeof(WebUiBridge).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            var callArgs = method.GetParameters().Length == 0
                ? Array.Empty<Expression>()
                : new[] { Expression.Convert(args[0], typeof(object)) };
            var body = Expression.Call(Expression.Constant(this), method, callArgs);
            var handler = Expression.Lambda(eventInfo.EventHandlerType, body, args).Compile();
            eventInfo.AddEventHandler(_panel, handler);
            _subscriptions.Add((eventInfo, handler));
        }

        private void HandleEvent(object value)
        {
            if (value == null) return;
            var type = value.GetType();
            var name = type.GetProperty("Name")?.GetValue(value) as string;
            var payload = type.GetProperty("Payload")?.GetValue(value);
            if (!string.IsNullOrEmpty(name)) EventReceived?.Invoke(name!, payload);
        }

        private void HandleClosed() => Closed?.Invoke();
        private void HandleError(object value) => _log.LogWarning("WebUI host error: " + (value?.ToString() ?? "unknown error"));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_panel != null)
            {
                foreach (var subscription in _subscriptions)
                    try { subscription.Event.RemoveEventHandler(_panel, subscription.Handler); } catch { }
                try { (_panel as IDisposable)?.Dispose(); } catch (Exception ex) { _log.LogWarning("WebUI panel dispose failed: " + ex.Message); }
            }
            _subscriptions.Clear();
            _panel = null;
            _visible = null;
            _sendEvent = null;
        }
    }
}
