// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnknownInventoryTerminal.Tests;

[TestClass]
public sealed class TerminalTextTests
{
    [TestMethod]
    public void InvalidSelectedTemplateFallsBackToValidEnglishOverrideThenBuiltin()
    {
        SetField("_fallback", Dict(("count", "Builtin {0}/{1}")));
        SetField("_english", Dict(("count", "English override {0}/{1}")));
        SetTranslations(("en", Dict(("count", "English override {0}/{1}"))),
            ("xx", Dict(("count", "Broken {7}"))));
        SetLanguages("en", "xx");
        Select("xx");

        TerminalText.Format("count", 3, 9).Should().Be("English override 3/9");

        SetField("_english", Dict());
        SetTranslations(("en", Dict()), ("xx", Dict(("count", "Only {0}"))));
        Select("xx");
        TerminalText.Format("count", 3, 9).Should().Be("Builtin 3/9");
    }

    [TestMethod]
    public void SelectedLanguageCanReorderAllRequiredArguments()
    {
        SetField("_fallback", Dict(("count", "{0}/{1}")));
        SetField("_english", Dict(("count", "{0}/{1}")));
        SetTranslations(("en", Dict(("count", "{0}/{1}"))), ("xx", Dict(("count", "{1} then {0}"))));
        SetLanguages("en", "xx");
        Select("xx");

        TerminalText.Format("count", "first", "second").Should().Be("second then first");
    }

    [TestMethod]
    public void MissingSelectedTranslationUsesEnglishAndUnknownKeysHonorExplicitFallback()
    {
        SetField("_fallback", Dict(("known", "Builtin")));
        SetField("_english", Dict(("known", "English")));
        SetTranslations(("en", Dict(("known", "English"))), ("xx", Dict()));
        SetLanguages("en", "xx");
        Select("xx");

        TerminalText.Get("known").Should().Be("English");
        TerminalText.Get("slot.custom", "Custom slot").Should().Be("Custom slot");
        TerminalText.Get("missing.key").Should().Be("missing.key");
        ResolveLanguage("XX").Should().Be("xx");
        ResolveLanguage("unknown").Should().Be("en_us");
    }

    [TestMethod]
    public void LegacyLanguageIdsResolveToCurrentFileNames()
    {
        SetLanguages("en_us", "zh_cn");

        ResolveLanguage("en").Should().Be("en_us");
        ResolveLanguage("zh-CN").Should().Be("zh_cn");
    }

    [TestMethod]
    [DataRow("en_us", "Terminal language", "Container placement")]
    [DataRow("zh_cn", "终端语言", "容器放置规则")]
    public void SettingsReadPlainFieldLabelsEvenWithLegacyFormatKeys(string language, string languageTitle, string placementTitle)
    {
        var shipped = TerminalText.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Lang", language + ".json")));
        shipped["settings.Language"].Should().Be(languageTitle).And.NotContain("{0}");
        shipped["settings.ContainerPlacement"].Should().Be(placementTitle).And.NotContain("{0}");
        shipped["language.name"].Should().NotBeNullOrWhiteSpace();
        shipped["settings.ContainerPlacement.BestFit"].Should().NotContain("{0}");
        shipped["settings.ContainerPlacement.WeightReduction"].Should().NotContain("{0}");
        shipped["settings.ContainerPlacement.Manual"].Should().NotContain("{0}");
    }

    [TestMethod]
    [DataRow("en_us", "Keep item after action")]
    [DataRow("zh_cn", "动作后保留物品")]
    public void KeepDraggingToggleHasALocalizedLabelAndActionDescription(string language, string label)
    {
        var shipped = TerminalText.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Lang", language + ".json")));
        shipped["settings.KeepDraggingAfterUse"].Should().Be(label);
        var description = shipped["settings.KeepDraggingAfterUse.description"];
        (description.Contains("Drain", StringComparison.Ordinal) || description.Contains("倒液", StringComparison.Ordinal))
            .Should().BeTrue();
        description.Should().Contain(language == "en_us" ? "once" : "一次");
    }

    private static Dictionary<string, string> Dict(params (string Key, string Value)[] entries)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries) result.Add(entry.Key, entry.Value);
        return result;
    }

    private static void SetTranslations(params (string Key, Dictionary<string, string> Value)[] entries)
    {
        var translations = (Dictionary<string, IReadOnlyDictionary<string, string>>)typeof(TerminalText)
            .GetField("Translations", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        translations.Clear();
        foreach (var entry in entries) translations.Add(entry.Key, entry.Value);
    }

    private static void SetLanguages(params string[] languages) => SetField("_languages", languages);

    private static void SetField(string name, object value) =>
        typeof(TerminalText).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);

    private static void Select(string value) =>
        typeof(TerminalText).GetMethod("Select", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object?[] { value });

    private static string ResolveLanguage(string value) =>
        (string)typeof(TerminalText).GetMethod("ResolveLanguage", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object?[] { value })!;
}
