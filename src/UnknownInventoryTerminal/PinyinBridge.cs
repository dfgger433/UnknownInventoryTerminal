using System;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    internal static class PinyinBridge
    {
        internal static IDisposable? Register(Func<bool> enabled, ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.TryGetValue("JustUnknownCharacters", out var plugin)) return null;
            var method = plugin.Instance.GetType().Assembly.GetType("JustUnknownCharacters.PinyinMatcher")?
                .GetMethod("Contains", new[] { typeof(string), typeof(string) });
            if (method == null || method.ReturnType != typeof(bool) || !method.IsStatic)
            {
                log.LogWarning("PinyinMatcher.Contains(string,string) is unavailable; built-in search remains active.");
                return null;
            }
            var contains = (Func<string, string, bool>)Delegate.CreateDelegate(typeof(Func<string, string, bool>), method);
            bool failed = false;
            log.LogInfo("JustUnknownCharacters pinyin search connected.");
            return SearchApi.Matchers.RegisterMatcher((entry, term) =>
            {
                if (!enabled() || failed) return false;
                try { return contains(entry.DisplayName, term) || contains(entry.ContainerName, term); }
                catch (Exception ex)
                {
                    failed = true;
                    log.LogWarning("Pinyin provider disabled after an error: " + ex.Message);
                    return false;
                }
            });
        }
    }
}
