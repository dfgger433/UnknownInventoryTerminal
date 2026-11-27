# Unknown Inventory Terminal

Casualties Unknown 的客户端背包物品终端。本项目提供 Unity 原生终端，不再包含 WebUI 网页前端。

## 项目声明

**早期版本：** 本模组仍处于早期开发阶段，功能和接口可能调整。自动化测试不能替代游戏内实测；单机交互和 Together 联机的验证范围见[兼容性说明](docs/COMPATIBILITY.md)。

**AI 使用：** 本项目在开发过程中使用了 AI 辅助；AI 辅助不属于模组的游戏内功能。

## 终端 0.5.1

- 只读取本地玩家的真实物品实例，按实例身份去重；不复制物品、不修改真实容器层级。
- 终端窗口固定在屏幕中央，不可拖动。打开时不显示原版径向物品栏或容器面板。上部是六列可搜索仓储网格，按精确物品 ID 聚合同类；下部显示双手、主槽位和装备，不受搜索过滤。
- 主槽位从左到右按游戏槽位索引排列：主手 `0`、副手 `1`，随后是其余槽位；数字是槽号，不是新增快捷键。装备格绑定游戏的 `wearSlotId`，空槽也显示。CUCoreLib 新注册的可穿戴物品会自动补进装备栏，先填满本行，再增加完整的六格行；屏幕放不下时可在装备栏用滚轮逐行查看。
- 使用设置中的快捷键打开终端（默认 `F7`）。右键清空搜索框，`Enter` 退出编辑。右上角关闭终端；左侧可切换名称/数量排序、升/降序及终端高度。用滚轮或右侧滑块逐行滚动。
- 点击聚合格进入实例选择页；实际物品继续使用原版 `ContainerItem` / `InvButton` 交互。悬停时由原版生成物品说明，终端以 Unity 默认 IMGUI 字体显示，保留灰蓝外框、原版图标、CUCoreLib 物品和展开描述按键。聚合格标注当前只展示一件物品的属性。
- 拖到相应的装备格可穿戴，拖到手持或穿戴的容器格可直接存入。拖到上部仓储网格时，自动模式按最小剩余容量或最高减重选取容器；手动模式请直接拖到目标容器格。
- 将工具拖到装有电池的设备上并松开拖拽键，可卸下电池；也可以将合适的电池拖到空设备上装入。其他可组合物品也沿用原版规则。请拖到具体物品：聚合格要先展开，固定栏内重排仍优先。
- 将装有液体的具体容器拖到另一个有空间的液体容器上，再松开拖拽键。弹窗中可输入整数毫升或拖动横向滑块，点击「转移」才会倒入；点「取消」不改变两件物品。转移上限取来源存量和目标剩余容量中较小的一项。
- 将终端中的具体物品拖出窗口，在游戏画面内松开原拖拽键可把它丢到地上。仅拖出但不松手不会丢弃；拖回窗口内可照常操作。
- 将物品拖到左侧对应按钮。在「倒空容器」上按住鼠标左键可持续倒液，松开即停；在「使用物品」上单击左键使用。若左键也是拖拽键，先松开，再按一次左键；若拖拽用的是其他键，可以拖住时直接按左键。移开鼠标或右键可取消等待中的操作。倒液仍调用原版逻辑，并复用 Together 原有同步消息；客机使用自己的背包物品时，主机无需安装本模组，但必须运行兼容的 Together。物品转移交互前校验本地归属，不直接调用 `LoadItem`。
- 拖动物品到左侧的锁定按钮并松开，即可将这件物品固定到仓储首行；把已固定的物品拖到同一按钮即可解锁。拖动固定栏中的物品可调整顺序；固定栏不受搜索或排序影响，超过六件时可滚动固定栏或点击页码翻页。关掉终端再打开仍保留当前角色持有物品的固定状态；不写入存档。
- 点击终端左侧的「设置」按钮，可切换语言、选择容器摆放规则或关闭默认开启的「使用后保持拖动」。开启时，使用后仍可用的同一物品会继续随按住的拖拽键移动；如果已松开拖拽键，可在使用按钮上连续单击。物品用完、鼠标离开等待中的按钮或右键取消时，不会继续使用。点击右上角返回库存；终端快捷键位于游戏的「设置 → 输入/控制」。安装兼容的 JustUnknownCharacters（JUC）时自动接入拼音匹配，未安装时仍可普通搜索。

自动化测试覆盖库存投影、搜索、六列布局与装备扩展补行，以及屏幕 JSON、图集裁切和资源边界；Unity 实际渲染、原版物品交互和 Together 主客机链路仍需在目标游戏版本中实机验收，见[兼容性说明](docs/COMPATIBILITY.md)。

## 构建

在本项目目录运行：

```powershell
.\scripts\Build.ps1 -GameRoot 'D:\stam\steamapps\common\Casualties Unknown'
```

脚本构建插件、复制 GUI 资源，并运行单元测试和输入门测试，不需要 Node.js/npm。需要 .NET SDK、本机游戏 Managed DLL、BepInEx 5 和 `BepInEx/plugins/CUCoreLib.dll`。

每交付一次兼容更新，按语义版本规则更新[版本配置](src/Directory.Build.props)并同步[更新记录](docs/CHANGELOG.md)；日常修复通常递增补丁位（例如 `0.4.1` → `0.4.2`），功能或兼容性边界发生阶段性变化时再递增次版本号。同一版本的重复测试构建不加版；插件加载日志与两个 DLL 使用同一版本源。

## 安装

关闭游戏后运行：

```powershell
.\scripts\Install.ps1 -GameRoot 'D:\stam\steamapps\common\Casualties Unknown'
```

安装脚本只向模组专用目录复制插件 DLL、Core DLL、GUI 屏幕描述、贴图、授权声明和语言文件，不修改游戏 DLL、Together 或存档。终端只使用原生窗口，旧的 `UseLegacyWebUi` 配置不再生效。

## 源码结构

源码按功能目录组织，物理移动不改变现有 C# 命名空间或程序集边界：

- [`src/UnknownInventoryTerminal.Core/Domain/`](src/UnknownInventoryTerminal.Core/Domain/)：与 Unity 无关的库存投影、搜索及容器数据模型。
- [`src/UnknownInventoryTerminal/Api/`](src/UnknownInventoryTerminal/Api/)：对外的 `TerminalApi` / `TerminalItemsApi`。
- [`src/UnknownInventoryTerminal/Presentation/`](src/UnknownInventoryTerminal/Presentation/)：布局、交互状态、贴图 schema、装备和本地化等逻辑。
- [`src/UnknownInventoryTerminal/UI/`](src/UnknownInventoryTerminal/UI/)：原生窗口、设置、输入、工具提示和弹窗。
- [`src/UnknownInventoryTerminal/Integration/`](src/UnknownInventoryTerminal/Integration/)：游戏补丁、设置绑定、可选拼音接入。

## GUI 资源

终端只从插件 DLL 同级的 `assets/unknowninventoryterminal/` 读取资源。[贴图](assets/unknowninventoryterminal/textures/)和[终端屏幕描述](assets/unknowninventoryterminal/screens/terminals/terminal.json)共用这一命名空间。旧目录及旧的无命名空间纹理引用不再读取；升级时请按[贴图替换说明](assets/unknowninventoryterminal/README.md)迁移自定义资源。

资源沿用 NeoForge 的命名空间目录约定，贴图按 `terminal`、`settings`、`storage`、`fluid`、`widgets` 用途归类。底板和按钮图集包含本地修改，界面在运行时裁切和重组。现有贴图不能随代码统一改授权；逐项来源及限制见[贴图授权说明](assets/unknowninventoryterminal/NOTICE.txt)。

## 鸣谢

- **界面素材与设计**：感谢 [Applied Energistics 2](https://github.com/AppliedEnergistics/Applied-Energistics-2) 的 Ridanisaurus Rid、AlgorithmX2 等创作者提供 GUI 贴图和终端布局参考，以及 [Modernity-GTNH-UI](https://github.com/ModernityGTNH/Modernity-GTNH-UI) 的 Ranzu、WanQueen 等贡献者和 [CreativeCore](https://github.com/GTNH-Museum/CreativeCore) 的 N247S，流体滑轨和滑块的素材可追溯到两个项目。感谢 [NeoForge](https://docs.neoforged.net/docs/1.21.1/misc/resourcelocation/) 的资源组织文档；本模组不依赖 Minecraft、AE2 或 NeoForge 运行时。
- **运行与兼容**：感谢 [Casualties: Unknown](https://store.steampowered.com/app/4576490/) 的原生物品界面，以及 [Unity](https://unity.com/)、[BepInEx](https://github.com/BepInEx/BepInEx)、[HarmonyX](https://github.com/BepInEx/HarmonyX)、[CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib)、[Json.NET](https://www.newtonsoft.com/json) 和 [TextMesh Pro](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html)。[Together](https://github.com/creaturefeaturelarry/casualties-together) 提供可选的联机兼容，[Just Unknown Characters](https://github.com/XeronOwO/Just-Unknown-Characters) 提供可选拼音搜索。这些依赖不与本模组一起打包。
- **参考项目**：感谢 Aakber 的 [Unknown Enough Items（UEI）](https://github.com/MAakber/Unknown-Enough-Items) 提供输入拦截参考；UEI 不是安装前置。
- **开发与测试**：感谢 [.NET](https://dotnet.microsoft.com/)、[MSTest](https://github.com/microsoft/testfx) 和 [Fluent Assertions](https://fluentassertions.com/)；测试依赖不随模组分发，Fluent Assertions 8 的非商业社区许可见下方审计。

## 许可证与发布

**代码：** 本项目自有代码（含插件、Core、测试、工具和构建脚本）采用 [GNU GPL v3.0](LICENSE)，SPDX 标识为 `GPL-3.0-only`。发布插件 DLL 时，须随附许可证并按 GPLv3 第 6 节提供同版本的对应源码；第三方依赖保留各自许可证。

**贴图：** 贴图不纳入代码的 GPLv3 授权。AE2 贴图及本地改编保留 **CC BY-NC-SA 3.0**；Modernity 贴图按 **CC BY-NC-SA 4.0** 记录。两种非商业条款只适用于相应素材，不限制 GPL 代码的商业使用。具体归属见[贴图授权说明](assets/unknowninventoryterminal/NOTICE.txt)。

**发布：** 当前整包即使免费也暂不宜公开分发。先确认 Modernity 流体滑轨、滑块所用 CreativeCore 底图的再分发权限，或换成原创素材，再按[发布清单](docs/LICENSE-AUDIT.md)核对代码和贴图是否可作为独立材料分发。

## 文档

- [兼容性依据和未实测风险](docs/COMPATIBILITY.md)
- [终端界面与物品 API（含接入示例）](docs/TERMINAL-API.md)
- [外部搜索 API](docs/SEARCH-API.md)
- [NexusMods 发布文案（描述、权限与更新记录）](docs/nexusmods/README.md)
