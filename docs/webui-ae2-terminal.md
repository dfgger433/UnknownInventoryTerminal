# WebUI / OreUI / AE2 风格终端设计记录

> 下文是早期原生窗口和 WebUI 的历史记录，不代表当前布局或资源。当前原生终端使用 AE2 贴图，操作、构建和贴图授权见 [项目 README](../README.md)；WebUI 前端不再随本项目提供。

## 1. 目标、范围与非目标

### 1.1 目标

- 提供一个可搜索、可刷新、可按容器查看的客户端库存终端。
- 采用 AE2 风格的“网格 + 数量聚合”信息密度：聚合行只代表同类实例的显示汇总，点击后进入真实实例选择页。
- 采用偏 OreUI / WebUI release 的分层结构：顶部搜索与操作区、中部可滚动内容区、底部状态/操作提示区；内容区由 Unity 原生 UGUI 控件承载，避免只做一个不可交互的截图式面板。
- 保留原版物品按钮、右键/上下文菜单和拖放路径；终端只负责索引、筛选、推荐和呈现，不自建物品同步协议。
- 在单机和 Together 场景下只展示本地玩家真实拥有的 `Item` 实例，并在拾取、拖拽、释放前重新校验归属。

### 1.2 本次范围

- IMGUI 外壳：窗口、搜索框、清空/刷新/关闭按钮、视图模式切换和状态文本。
- UGUI 内容：聚合网格、实例选择页、原生 `ContainerItem`/`InvButton` 控件、容器投放候选区。
- 输入门：窗口可见时阻止移动、世界交互和聊天快捷键泄漏；透明 blocker 只承担 UI 射线遮挡，不能替代 `PlayerCamera.HandleInput` 的 Harmony gate。
- 依赖、构建、安装、单机/联机验收以及许可证和风险边界的记录。

### 1.3 非目标

- 不实现独立于游戏原版的物品复制、重建、父子关系修改或网络消息。
- 不承诺把外部 WebUI/OreUI/AE2 项目的资产、主题或代码直接移植到游戏中。
- 不把聚合行当作真实物品动作目标：聚合行点击只打开实例选择；实际操作必须落到某一个原始 `Item`。
- 不以自动化 core 测试替代 Unity、原版 Canvas、Together 主客机和实际输入的游戏内验收。

## 2. 设计说明

### 2.1 信息架构

```text
TerminalWindow（IMGUI chrome）
├─ Search / Clear / Refresh / Close
├─ All items (merged) | By container
├─ NativeFlatInventoryPanel（UGUI 内容区）
│  ├─ 聚合网格：数量、名称、容器名
│  ├─ 实例选择：一个 tile 对应一个真实 Item
│  └─ ContainerPlacementPanel：原生容器投放候选
└─ 状态栏：匹配实例数 / 快照总数 / 聚合组数 + 操作提示
```

- **WebUI release 方向**：把查询、状态和操作分区，按钮标签明确，错误时给出可执行的“查看 BepInEx 日志 / 刷新”提示；不依赖装饰性动画来表达成功。
- **OreUI 方向**：使用清晰的层次、紧凑的卡片/网格、滚动视口和显式返回入口；颜色只用于区分面板、按钮和数量，不作为唯一状态信息。
- **AE2 方向**：按精确物品 ID（或“物品 ID + 容器”）聚合，数量只表示匹配实例数；点击聚合项后再选取具体实例，以维持原版交互语义。

### 2.2 原生交互与数据安全

1. `GameInventoryAdapter.Capture()` 生成当前本地玩家的库存快照；嵌套遍历去重，实例 key 保持稳定，类型 ID 使用游戏的精确大小写。
2. `InventoryProjection.Build()` 先按查询过滤，再按视图模式分组。外部 matcher（例如可选拼音 provider）只扩展匹配，不修改游戏对象。
3. `NativeFlatInventoryPanel` 从 `Resources.Load<GameObject>("ContainerItem")` 实例化原版按钮，并把 `InvButton.refItem` 绑定到真实 `Item`。聚合 tile 不是物品动作目标；实例页 tile 才能进入原版菜单/拖拽路径。
4. 容器投放区只显示推荐目标（BestFit、WeightReduction 或 Manual），并在射线命中和释放时再次执行容量、距离、嵌套、标签和本地归属检查。
5. `Plugin` 的 Harmony patch 将终端输入门接到 `PlayerCamera`、`UIUtil` 和可用的 Together 聊天入口；实际转移仍由原版 `PlayerCamera`/`InvButton`/`Container` 路径处理，由 Together 既有补丁负责其同步。

## 3. 代码与依赖路径

### 3.1 本仓库路径

| 责任 | 路径 | 备注 |
|---|---|---|
| 插件入口、会话生命周期、Harmony 输入门、本地归属检查 | [`src/UnknownInventoryTerminal/Plugin.cs`](../src/UnknownInventoryTerminal/Plugin.cs) | 客户端插件；稳定 GUID 为 `com.unknown.inventoryterminal` |
| IMGUI 窗口、搜索和工具栏 | [`src/UnknownInventoryTerminal/TerminalWindow.cs`](../src/UnknownInventoryTerminal/TerminalWindow.cs) | 只拥有 chrome；内容区交给 UGUI |
| 原生库存网格、实例选择、滚动视口 | [`src/UnknownInventoryTerminal/NativeFlatInventoryPanel.cs`](../src/UnknownInventoryTerminal/NativeFlatInventoryPanel.cs) | 使用原版 `ContainerItem`/`InvButton` |
| 容器候选与投放校验 | [`src/UnknownInventoryTerminal/ContainerPlacement.cs`](../src/UnknownInventoryTerminal/ContainerPlacement.cs) | 推荐逻辑不直接调用 `LoadItem`，不改变网络状态 |
| 搜索/投影 core | [`src/UnknownInventoryTerminal.Core/`](../src/UnknownInventoryTerminal.Core/) | `netstandard2.1`；可被外部搜索 provider 引用 |
| 设置、快捷键、可选拼音桥接 | [`src/UnknownInventoryTerminal/SettingsBridge.cs`](../src/UnknownInventoryTerminal/SettingsBridge.cs)、[`src/UnknownInventoryTerminal/PinyinBridge.cs`](../src/UnknownInventoryTerminal/PinyinBridge.cs) | CCL 为硬依赖；拼音 mod 为软依赖 |
| 构建 | [`scripts/Build.ps1`](../scripts/Build.ps1) | Release build + core tests |
| 安装 | [`scripts/Install.ps1`](../scripts/Install.ps1) | 只写入本插件专用目录 |
| 兼容性证据与未实测风险 | [`docs/COMPATIBILITY.md`](COMPATIBILITY.md) | 本机 DLL 静态核对，不等于运行时联机验收 |
| core 测试说明 | [`docs/CORE-TESTS.md`](CORE-TESTS.md) | 当前记录为 13 项通过 |
| 搜索扩展接口 | [`docs/SEARCH-API.md`](SEARCH-API.md) | provider 注册/释放和匹配约束 |

### 3.2 构建时依赖

`src/UnknownInventoryTerminal/UnknownInventoryTerminal.csproj` 目标为 `netstandard2.1`，通过 `GameRoot` 定位：

- `$(GameRoot)\CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll`
- `$(GameRoot)\CasualtiesUnknown_Data\Managed\UnityEngine*.dll`
- `$(GameRoot)\CasualtiesUnknown_Data\Managed\Unity.TextMeshPro.dll`
- `$(GameRoot)\BepInEx\core\BepInEx.dll`
- `$(GameRoot)\BepInEx\core\0Harmony.dll`
- `$(GameRoot)\BepInEx\plugins\CUCoreLib.dll`

`UnknownInventoryTerminal.Core` 同样目标为 `netstandard2.1`，不强引用插件入口或 Together。测试项目 [`tests/UnknownInventoryTerminal.Tests/UnknownInventoryTerminal.Tests.csproj`](../tests/UnknownInventoryTerminal.Tests/UnknownInventoryTerminal.Tests.csproj) 目标为 `net10.0`，使用 MSTest 和 FluentAssertions；这些 NuGet 包只用于测试，不会随插件部署。

### 3.3 运行时依赖与可选依赖

- **必须**：匹配当前游戏版本的 `Assembly-CSharp`、Unity UI/EventSystem、BepInEx 5、`CUCoreLib.dll`。插件声明 `net.cucorelib` 为硬依赖。
- **可选**：`JustUnknownCharacters` 的 `PinyinMatcher.Contains(string,string)`；不存在或接口变化时应回退到普通搜索。`casualtiesunknown.webui` 同样是软依赖；存在并且 `Web/` 资源可用时显示 WebUI 面板，否则继续使用原生 IMGUI/UGUI 窗口。
- **兼容性探测**：Together 类型通过反射探测，插件不新增网络消息；实际主客机动作必须仍进入 Together 的原版 inventory-action/`ItemSync` 路径。
- **资源前提**：游戏资源中需要可加载名为 `ContainerItem` 的 prefab，且该 prefab 含有可用 `InvButton`。缺失时终端可显示快照，但原生物品交互应禁用并记录警告。
- **线程约束**：库存读取、Unity 对象访问、搜索 provider 注册/注销均在 Unity 主线程执行；外部 matcher 不得修改物品或启动 Unity 异步操作。

## 4. 构建、安装与验收步骤

### 4.1 可重复构建

在仓库根目录 `D:\stam\steamapps\common\Casualties Unknown\mods\UnknownInventoryTerminal` 执行：

```powershell
.\scripts\Build.ps1 -GameRoot 'D:\stam\steamapps\common\Casualties Unknown'
```

脚本等价于：

```powershell
dotnet build .\src\UnknownInventoryTerminal\UnknownInventoryTerminal.csproj `
  -c Release -p:GameRoot='D:\stam\steamapps\common\Casualties Unknown' --nologo

dotnet test .\tests\UnknownInventoryTerminal.Tests\UnknownInventoryTerminal.Tests.csproj `
  -c Release --nologo
```

构建门槛：插件和 core 成功编译、`TreatWarningsAsErrors` 不产生警告错误；测试应全部通过。当前 [`docs/CORE-TESTS.md`](CORE-TESTS.md) 记录的基线是 13 项通过、0 失败、0 跳过，但在新环境仍应重新运行而不是直接引用旧结果。

### 4.2 安装与回滚边界

关闭游戏后执行：

```powershell
.\scripts\Install.ps1 -GameRoot 'D:\stam\steamapps\common\Casualties Unknown'
```

安装脚本会验证游戏目录、`CUCoreLib.dll`、游戏进程关闭状态以及 `dist/UnknownInventoryTerminal.dll` 和 `dist/UnknownInventoryTerminal.Core.dll`，然后只写入：

```text
<GameRoot>\BepInEx\plugins\UnknownInventoryTerminal\
```

卸载/回滚应手工移除该专用目录中的本插件文件；不要删除或替换游戏 DLL、Together、CUCoreLib、其他 mod、存档或备份。安装前后应保留 BepInEx 日志，便于核对 patch 是否缺失。

### 4.3 静态/自动化验收

- [ ] Release build 使用目标游戏目录成功完成，无缺失 Managed DLL 或 CUCoreLib 报错。
- [ ] core tests 成功；覆盖实例 key 去重、容器分组、大小写、多词 AND、matcher 异常隔离和参数校验。
- [ ] `dist/` 至少包含 `UnknownInventoryTerminal.dll` 与 `UnknownInventoryTerminal.Core.dll`；不要把测试 NuGet DLL 安装到插件目录。
- [ ] BepInEx 日志显示插件版本 `0.4.2`，并明确记录必需输入 patch 是否 ready；缺失入口时不能把“窗口可见”当作完整可用。
- [ ] 安装 WebUI release 后，F7 能打开外部终端窗口；点击网页物品会隐藏 WebUI 并把焦点交回原生 `InvButton` 选择页；没有 WebUI 时 F7 仍能打开原生窗口。

### 4.4 单机游戏内验收

使用本机游戏版本、默认分辨率和至少一个不同 UI 缩放/宽高比各执行一次：

1. 在没有打开控制台、聊天框、配方过滤、制作/交易/伤口/暂停等原生 modal 的情况下，用设置中的快捷键打开终端；确认光标解锁、玩家不移动，关闭后恢复原光标、焦点、移动锁和 radial 状态。
2. 核对顶部搜索、Clear、Refresh、Close；空查询、多词查询、大小写变化和不存在的词都应给出正确数量和状态文案。
3. 切换 `All items (merged)` 与 `By container`；确认相同显示名但不同容器不会错误合并，类型 ID 大小写按游戏规则区分。
4. 点击聚合 tile 进入实例选择页，再返回；确认计数是实例数，实例页每个 tile 对应一个真实物品，不会生成复制品或改变容器层级。
5. 使用原版物品上下文菜单；拖拽单个实例到原版背包/容器；确认空白处释放会取消而不是把物品丢入世界，非法目标会被拒绝。
6. 逐项切换容器推荐规则：BestFit、WeightReduction、Manual；核对容量、距离、标签、嵌套和重叠拖拽的禁用提示。推荐区只提供候选，不绕过原版 `Container.LoadItem/UnloadItem`。
7. 在库存变化、物品转移、容器失效、玩家昏迷/死亡、切换角色或打开原生 modal 后确认终端关闭或刷新，旧快照不能继续操作。
8. 删除/暂时移走 `ContainerItem` 资源或模拟相关错误时，确认终端给出可理解的错误并写入日志，而不是静默修改物品。

### 4.5 Together 主客机验收

该部分不能由 core tests 替代，必须使用实际 Together 版本和至少一台主机/一台客户端：

- [ ] 终端只列出当前客户端本地角色拥有的实例；不能读到其他玩家或远端角色的库存。
- [ ] 客户端执行原版菜单、容器转移和拖放后，主机/其他客户端能通过 Together 现有同步看到结果；不出现重复、回滚或幽灵物品。
- [ ] 终端可见期间聊天快捷键、世界拾取、移动/跳跃和原生 modal 不发生输入泄漏；聊天框焦点恢复正常。
- [ ] 主机未安装本插件时（若当前 Together 部署规则允许）仍能完成原版动作；若规则要求双方安装，应记录实际规则而不是宣称无条件兼容。
- [ ] 断线、重新连接、容器距离变化、权限变化和容器被其他玩家改变时，旧 tile 释放被重新校验并安全取消。

### 4.6 验收证据

每次发布候选至少保存：构建命令和输出摘要、测试结果、游戏版本、BepInEx/CUCoreLib/Together 版本或 DLL hash、单机截图/日志、主客机动作结果以及未执行项目。未启动游戏或未进入联机时，必须在报告中标注“待实测”。

## 5. 许可证与归属说明

- 当前检查到的仓库树中没有 `LICENSE`/`LICENSE.txt` 等项目许可证文件；因此不能替项目源代码或文档推断 MIT、GPL、Apache 等许可证。对外发布前应由项目作者补充明确许可证或书面授权。
- 早期网页前端曾随附 OreUI 源码、字体和图标，并按 MPL-2.0 保留相应许可证。这些资源已随网页前端从当前工作区移除。参考项目的名称、截图、文档和商标仍归各自权利人所有，若未来加入其他实际资产，必须单独核对其许可证、归属和再分发条件。
- 游戏、Unity/Unity UI、TextMeshPro、BepInEx、Harmony、CUCoreLib、Together、JustUnknownCharacters 及其 DLL 均不因本插件而改变许可证。插件只按其公开/本机 API 进行引用、反射或 Harmony patch；不修改、重新打包或分发这些上游 DLL。
- MSTest、FluentAssertions 及其他测试 NuGet 包只属于开发/测试链路；应按各自包许可证保留归属，不能把测试输出当作游戏插件依赖。
- `dist/` 中的插件 DLL 是构建产物，不代表第三方依赖的许可证被转授；发布压缩包应附上本项目许可证（待补）和必要的第三方归属清单。

## 6. 风险边界与应对

| 风险 | 影响 | 当前边界/应对 |
|---|---|---|
| 游戏更新改变 `PlayerCamera`、`InvButton`、`Container`、`UIUtil` 签名或字段 | 编译失败、patch 失效或物品动作异常 | 构建时锁定本机 Managed DLL；启动日志检查每个输入 patch；版本升级必须重新做静态核对和实机验收 |
| `ContainerItem` prefab 缺失或层级改变 | 网格可显示但原生菜单/拖放不可用 | 发现资源缺失时禁用原生交互、显示提示并记录 warning；不伪造可用状态 |
| IMGUI 与 UGUI Canvas/EventSystem 的排序、分辨率、DPI 或其他 mod 冲突 | 点击穿透、按钮错位、搜索焦点残留 | blocker 仅做射线遮挡，另有 `HandleInput` gate；至少覆盖不同分辨率，并记录其他 UI mod 共存结果 |
| 快照在刷新间隔内过期 | 释放到已转移、已删除或已不属于本地的物品 | 交互开始、射线命中、释放时都重新验证；无效实例取消拖拽，不直接写列表或 transform |
| Together patch 顺序、聊天实现或同步规则变化 | 联机重复/丢失物品、聊天输入泄漏 | 不新增网络协议；沿用原版 inventory-action/`ItemSync`；每个 Together 版本都要做主客机验收 |
| 嵌套容器、同名容器、大小写 ID 或特殊物品 | 错误合并、错误推荐或物品无法放入 | 实例 key 与容器 key 分离；精确大小写比较；候选计算拒绝嵌套/距离/容量/标签不满足者 |
| 大库存导致 UI 重建和 GameObject 数量过多 | 卡顿、GC 或输入延迟 | 当前设计以刷新和可视化正确性优先；发布前应使用最大实际库存测量帧时间，必要时增加复用/分页，而不是放宽安全检查 |
| 本项目许可证缺失或参考资产误用 | 再分发和合规风险 | OreUI 许可证随源码和 Web 构建产物保留；发布前仍需补本项目许可证与第三方清单；无法确认的素材不进入仓库 |
| 未运行游戏/联机实测 | 自动化通过但真实交互仍可能失败 | 报告中明确区分静态/自动化通过与“待实测”；不能以构建成功替代验收结论 |

## 7. 发布前结论模板

```text
构建：通过 / 失败（命令：...）
Core 测试：通过 X，失败 Y，跳过 Z
游戏版本与 DLL hash：...
CUCoreLib / Together：...
单机 UI 与原版交互：通过 / 待实测 / 失败
Together 主客机同步：通过 / 待实测 / 失败
输入与 modal 隔离：通过 / 待实测 / 失败
许可证：项目许可证已确认 / 待作者补充
已知风险与回滚方式：...
```

本记录只描述设计、依赖和验收边界；不扩大“已验证”范围。WebUI release 是可选运行时宿主，OreUI 与 AE2 仍只是设计参考，不把参考项目资产或代码表述为本项目内容。
