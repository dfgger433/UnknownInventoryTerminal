# Core 自动化测试

测试项目位于 `tests/UnknownInventoryTerminal.Tests`，目标框架为 .NET 10，并通过项目引用测试 `netstandard2.1` 的 `UnknownInventoryTerminal.Core`。测试框架为 MSTest，断言使用 FluentAssertions；没有引入 Moq，因为 core 的扩展点可用真实委托直接验证。

覆盖范围包括：

- 重复实例 key 去重，以及合并行保留全部原始 `InventoryEntry` / native item 引用。
- 按容器分组时使用容器 key 区分显示名相同的容器；type ID 大小写保持区分。
- 查询过滤先于分组；null/空白查询、大小写不敏感、多词 AND，以及外部 matcher 对各词的扩展匹配。
- 外部 matcher 注册、释放、异常隔离及失败后禁用。
- 必需构造参数、投影输入、搜索条目及 matcher 的 null 参数验证。

在项目根目录运行：

```powershell
dotnet test .\tests\UnknownInventoryTerminal.Tests\UnknownInventoryTerminal.Tests.csproj
```

本次已在 .NET SDK 10.0.401 上运行上述命令并成功还原 NuGet 包（网络可用）：13 项通过，0 失败，0 跳过。Core 目标仍为 `netstandard2.1`。
