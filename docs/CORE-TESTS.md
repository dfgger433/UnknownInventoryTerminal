# Core 自动化测试

测试项目位于 `tests/UnknownInventoryTerminal.Tests`，目标框架为 .NET 10，并通过项目引用测试 `netstandard2.1` 的 `UnknownInventoryTerminal.Core`。测试框架为 MSTest，断言使用 FluentAssertions；没有引入 Moq，因为 core 的扩展点可用真实委托直接验证。

覆盖范围包括：

- 重复实例 key 去重，以及合并行保留全部原始 `InventoryEntry` / native item 引用。
- 按容器分组时使用容器 key 区分显示名相同的容器；type ID 大小写保持区分。
- 查询过滤先于分组；null/空白查询、大小写不敏感、多词 AND，以及外部 matcher 对各词的扩展匹配。
- 外部 matcher 注册、释放、异常隔离及失败后禁用。
- 必需构造参数、投影输入、搜索条目及 matcher 的 null 参数验证。
- [终端屏幕描述](../assets/unknowninventoryterminal/screens/terminals/terminal.json)的命名区域、PNG 裁切边界，以及无效 JSON、路径和九宫格边宽的处理。

在项目根目录运行：

```powershell
dotnet test .\tests\UnknownInventoryTerminal.Tests\UnknownInventoryTerminal.Tests.csproj
```

当前在 .NET SDK 10 上的结果：281 项测试全部通过（覆盖核心库存投影、搜索匹配、AE2 屏幕样式与 JSON 边界、拖拽手势、工具提示排版、设置面板布局及按键控制等），0 失败，0 跳过。测试会校验 PNG 的尺寸和裁切边界，不替代 Unity 实机渲染验收。Core 目标仍为 `netstandard2.1`。
