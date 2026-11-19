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
空白分隔 AND 条件，`|` 分隔 OR 条件，`*` 前缀仅匹配物品 ID。例如，`medical *bandage | water` 匹配医疗绷带或水。仅查 ID 的词不会交给扩展 provider。游戏没有对应的 AE2 模组、标签和 tooltip 索引，不启用 `@`、`#` 和 `$` 前缀。
物品分组的 ID 按游戏的精确大小写比较，不用名称作为身份。
出错的 provider 会被禁用，其他内置搜索继续使用。返回的 `InventoryEntry.NativeItem` 仅供识别；
不要在 matcher 内更改游戏物品或调用 Unity 异步操作。

若安装了兼容版本的 JustUnknownCharacters（JUC），终端自动调用其 `PinyinMatcher.Contains(string,string)` 匹配物品名和容器名，不需要打开设置。没有安装 JUC、接口不兼容或匹配器出错时，普通搜索仍可使用。
