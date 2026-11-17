# 搜索扩展接口（阶段1）

引用 `UnknownInventoryTerminal.Core.dll` 即可，不需要强引用插件入口或 Together。
所有调用（含注销）都在 Unity 主线程执行。

```csharp
using UnknownInventoryTerminal.Core;

// 外部修改终端搜索框内容。不会强制打开窗口。
SearchApi.Query = "绷带";

// 外部 provider 按词进行扩展匹配，和内置字段匹配为 OR。
// 不同词之间为 AND，例如“背包 绷带”要求两个词都匹配。
IDisposable token = SearchApi.Matchers.RegisterMatcher((entry, term) =>
    MyPinyin.Contains(entry.DisplayName, term));

// 在 provider 卸载时注销：
token.Dispose();
```

内置匹配字段：当前显示名称、原始物品 ID、所在容器路径；不区分搜索文本大小写。
物品分组的 ID 按游戏的精确大小写比较，不用名称作为身份。
出错的 provider 会被禁用，其他内置搜索继续使用。返回的 `InventoryEntry.NativeItem` 仅供识别；
不要在 matcher 内更改游戏物品或调用 Unity 异步操作。

本插件还会可选探测 `JustUnknownCharacters.PinyinMatcher.Contains(string,string)`，建立委托，
不改动拼音 mod 的源码或 DLL。没有安装、关闭拼音设置或接口缺失时普通搜索仍可使用。
