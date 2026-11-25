// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnknownInventoryTerminal.Core;

namespace UnknownInventoryTerminal
{
    internal static class PinyinBridge
    {
        internal static IDisposable? Register(ManualLogSource log)
        {
            if (!Chainloader.PluginInfos.TryGetValue("JustUnknownCharacters", out var plugin)) return null;

            Func<string, string, bool> contains;
            try
            {
                var method = plugin.Instance?.GetType().Assembly.GetType("JustUnknownCharacters.PinyinMatcher")?
                    .GetMethod("Contains", BindingFlags.Public | BindingFlags.Static | BindingFlags.ExactBinding,
                        binder: null, types: new[] { typeof(string), typeof(string) }, modifiers: null);
                if (method == null || method.ReturnType != typeof(bool))
                {
                    log.LogWarning("PinyinMatcher.Contains(string,string) is unavailable; built-in search remains active.");
                    return null;
                }
                contains = (Func<string, string, bool>)Delegate.CreateDelegate(typeof(Func<string, string, bool>), method);
            }
            catch (Exception ex)
            {
                log.LogWarning("Could not connect JustUnknownCharacters pinyin search; built-in search remains active: " + ex.Message);
                return null;
            }

            bool failed = false;
            log.LogInfo("JustUnknownCharacters pinyin search connected.");
            return SearchApi.Matchers.RegisterMatcher((entry, term) =>
            {
                if (failed) return false;
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
