# 第三方许可审计与发布清单

范围：当前 Unity 原生终端的源码、随包资源、构建和安装清单；用本机输出作交叉核对。历史网页版本和独立贴图预览器不算当前模组包。本记录区分已核实的许可与待确认的权利，不代替权利人的授权。

## 自有源码可以选什么许可证？

当前根目录没有主许可证。确认作者及贡献者有权授权后，建议将**已有权许可的原创源码**用 [MIT](https://opensource.org/license/mit) 发布；如果需要明确的专利授权和专利诉讼终止条款，可以改选 [Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0)。两者都允许商业使用；若决定采用，请由权利人添加正式许可证、版权声明和适用范围，明确排除第三方资产及另行许可的文件。本文不是授权声明。

代码与贴图可分开授权。**CC BY-NC-SA 的非商业限制不符合 [OSI 的开源定义](https://opensource.org/education/what-is-open-source)**，不要把包含这些贴图的整包标为 MIT、Apache-2.0 或称其全部开源。署名不能取代授权。想发行可商用的整包，应替换受限或来源未明的资产，或取得覆盖所需用途的明确许可。

## CC BY-NC-SA 3.0 和 4.0 有什么区别？

两版都允许在遵守条件时**公开分享**，都要求署名（BY）、限制用于主要追求商业利益或金钱报酬的用途（NC），并要求公开分享改编时采用适用的相同方式共享条款（SA）。免费提供下载不等于用途必然非商业；4.0 也没有放开商用。对这里的贴图，差别主要在下面几项：

| 项目 | [3.0 Unported](https://creativecommons.org/licenses/by-nc-sa/3.0/legalcode.en) | [4.0 International](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en) |
| --- | --- | --- |
| 署名和修改 | 如原作者提供作品标题，署名时保留；发布改编时表明改过。 | 不再明确要求作品标题；署名可合理地链接到集中声明，应标注自己的修改并保留已有修改说明。 |
| 违规后恢复 | 违规时授权自动终止，没有 30 天内补救即可自动恢复的条款。 | 自发现违规起 30 天内纠正，可从纠正之日起自动恢复许可；不免除此前违规的责任。 |
| 适用范围 | 有 Unported 版，也有不同司法辖区的移植版。 | 同一套 International 条款供全球使用，并明确涵盖适用的特殊数据库权利。 |

这不等于可以给原件换许可证。3.0 第 4(b) 节允许把**确有改编**的作品按同元素的后续版本（如 BY-NC-SA 4.0）发布，但原始素材的授权仍按原来源记载；单纯复制、改文件名或与别的文件打包，不会把原图升级成 4.0。本模组对已确认的 AE2 贴图继续保留 3.0 声明；Modernity 的素材按其许可正文记录为 4.0；这不等于 CreativeCore 的原图也获得了同样授权。参见 [Creative Commons 官方 4.0 变更说明](https://creativecommons.org/version4/)和[相同方式共享兼容列表](https://creativecommons.org/compatible-licenses/)。

## 随包文件与发布前待办

| 文件或范围 | 审计结果 | 发布前怎么做 |
| --- | --- | --- |
| [AE2 原图和本地改编](../assets/unknowninventoryterminal/NOTICE.txt) | 固定版本的 [AE2 README](https://github.com/AppliedEnergistics/Applied-Energistics-2/blob/f0ee8bf4bf35431b9f43788401aa609ca2adbcef/README.md#license) 将贴图与模型指定为 [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/legalcode)，不是 AE2 API 的 MIT 或 Java 实现的 LGPL。核实了原样复制的开关等 8 张图，以及终端底板、按钮图集、设置底板和按钮栏两种变体的 AE2 来源。 | 非商业分发时随包保留作者、来源及许可链接，标明本地修改；改编素材继续按适用的相同方式共享条款发布，不用主许可证覆盖。商业用途须另获许可或替换。 |
| [流体滑轨和滑块](../assets/unknowninventoryterminal/textures/gui/fluid/) | 两张 PNG 与 [Modernity 固定提交](https://github.com/ModernityGTNH/Modernity-GTNH-UI/tree/2ab52b96da3d81419fbd7b99ed941e301fda9298/assets/creativecore/textures/gui)字节相同，Modernity 部分按[随包许可正文](../assets/unknowninventoryterminal/licenses/modernity/LICENSE.txt)记录为 **CC BY-NC-SA 4.0**。Ranzu [最初导入](https://github.com/ModernityGTNH/Modernity-GTNH-UI/commit/a7effc62ce7152a3c6d68eeca10a8e82206761d1)的两张图与 N247S 的 [CreativeCore 原图](https://github.com/GTNH-Museum/CreativeCore/commit/ae74070df07f486387c6b20b32e2616bff346902)字节相同。含同一原图的 CreativeCore 固定快照附 [GPLv3 正文](https://github.com/GTNH-Museum/CreativeCore/blob/1f850a3fdcca0f0e589d940d6432ed8740caa95a/LICENSE)，未找到这两张原图单独采用 CC 4.0 的声明；[WanQueen 的修订](https://github.com/ModernityGTNH/Modernity-GTNH-UI/commit/12f56bb47098643843f2b62560892957745dbf33)本身不能证明已获底图授权。 | **在确认底图权利前不要随包公开分发，免费也一样。** 取得涵盖保留底图和修订图的再分发授权，或替换为独立原创素材。若继续使用，保留各方署名、来源、修改说明和适用许可；遵守非商业及改编相同方式共享条件。 |
| [下拉图集](../assets/unknowninventoryterminal/textures/gui/widgets/drop_down_list.png) | 本项目在 `9d2d186` 引入项目作者提供的图集，早于同一作者的移植项目收录。像素比对显示，AE2 的 [`button.png`](https://github.com/AppliedEnergistics/Applied-Energistics-2/blob/f0ee8bf4bf35431b9f43788401aa609ca2adbcef/src/main/resources/assets/ae2/textures/gui/sprites/button.png)从中间去掉列、缩至 112 像素宽后，普通态仅差 9 个箭头像素，去顶边后与普通选项逐像素一致；高亮态和禁用态也匹配对应 AE2 底图的裁切。未发现其使用 GTNH 上游图集的证据。 | 按 AE2 按钮的 **CC BY-NC-SA 3.0 改编**处理，随包署名并标明裁切、重排和加箭头；不能只因图集由项目作者提供就改标 MIT。 |
| [工具提示富文本处理](../src/UnknownInventoryTerminal/Presentation/Tooltip/TerminalTooltipText.cs#L9) | 标签解析与作者自有的 Casualties: Unknown 移植版重合。核对了 [GTNewHorizons/NotEnoughItems 上游许可说明](https://github.com/GTNewHorizons/NotEnoughItems/blob/bdadcc37a6e9661f135ec5d953c574e955cb2328/README.md#L85-L93)（GTNH 修改为 LGPL-3.0-or-later，另有 MIT 代码）及对应的 Java 实现：[工具提示](https://github.com/GTNewHorizons/NotEnoughItems/blob/bdadcc37a6e9661f135ec5d953c574e955cb2328/src/main/java/codechicken/nei/guihook/GuiContainerManager.java#L634-L694)使用 Minecraft 格式，[文本格式处理](https://github.com/GTNewHorizons/NotEnoughItems/blob/bdadcc37a6e9661f135ec5d953c574e955cb2328/src/main/java/codechicken/nei/FormattedTextField.java#L295-L326)处理 `§` 颜色码；在已核对的上游版本中未发现终端复用 GTNH 的具体实现。 | 不将作者自有移植版列为第三方依赖，不给这些文件套用 GTNH 许可证。若未来实际引入 GTNH 或其原有 MIT 片段，须按对应文件的上游条款保留声明及许可证。 |
| [透明占位图](../assets/unknowninventoryterminal/textures/gui/widgets/transparent_button.png) | 本项目在 `0606fc5` 加入的全透明占位图，当时的声明将其记为本地文件，非 AE2 素材。 | 无已识别的第三方素材义务；和 AE2 素材分开标记。 |
| [屏幕描述](../assets/unknowninventoryterminal/screens/terminals/terminal.json) | 沿用 AE2 的 `texture` / `srcRect` 约定和 [base_terminal.json](https://github.com/AppliedEnergistics/Applied-Energistics-2/blob/f0ee8bf4bf35431b9f43788401aa609ca2adbcef/src/main/resources/assets/ae2/screens/terminals/base_terminal.json) 五段布局，适配本地六列及资源路径。AE2 对贴图的声明不自动覆盖 JSON。 | 保留来源；如有保留来自上游的实质内容，单独确认该文件的适用许可，不用源码许可证覆盖。 |

## 不随模组打包的依赖和参考

生产[项目引用](../src/UnknownInventoryTerminal/UnknownInventoryTerminal.csproj)将外部 DLL 标为 `Private=false`；[必装清单](../scripts/Install.ps1#L11-L36)只列本项目的两个 DLL、语言与界面 JSON、PNG 和素材许可声明。安装脚本还会复制[可选语言包](../scripts/Install.ps1#L46-L55)：构建输出里额外的语言 JSON 若未在目标目录存在便会复制，公开打包时应另查授权。以下是本地引用、可选集成或开发链路，不是当前模组分发的第三方二进制：

| 项目 | 角色和许可证据 |
| --- | --- |
| Casualties: Unknown、Unity / Unity UI、TextMesh Pro | 从用户自己的游戏安装引用程序集；物品控件、图标等在运行时由游戏提供。不要打包游戏、Unity、TMP 的 DLL、资源或本机提取件；需遵守各自的适用条款。 |
| BepInEx 5.4.23.5、HarmonyX 2.9.0、Json.NET | 分别是加载器、补丁框架（本机 `0Harmony.dll` 为 HarmonyX）、JSON 解析；核对了 [BepInEx 的 MIT](https://github.com/BepInEx/BepInEx/blob/v5.4.23.5/LICENSE)、[HarmonyX 的 MIT](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/LICENSE) 和[游戏所带 Json.NET 13.0.2 的 MIT](https://github.com/JamesNK/Newtonsoft.Json/blob/13.0.2/LICENSE.md)。 |
| CUCoreLib 1.1 | 必装，提供设置、按键和内容注册。已核对[上游 v1.1 授权](https://github.com/jimmyking9999999/CUCoreLib/blob/v1.1/LICENSE)为 LGPL-3.0，本机 DLL SHA-256 与该[发布页](https://github.com/jimmyking9999999/CUCoreLib/releases/tag/v1.1)的摘要一致。保持可替换的独立依赖，由用户从上游安装。无需仅因调用库就把自有源码改成 LGPL；分发链接后的插件时应核对 [LGPLv3 第 4 节](https://www.gnu.org/licenses/lgpl-3.0.html#section4)对组合体的要求，包括随包显著标注依赖和授权、附 LGPLv3/GPLv3 文本、保持库可替换，不限制为调试库的修改而逆向工程。 |
| Together、Just Unknown Characters | 可选联机兼容、可选拼音匹配；分别参考 [Together 上游的 LGPL-2.1 正文](https://github.com/creaturefeaturelarry/casualties-together/blob/main/LICENSE)（不视为已经与本机 5.5.4 二进制逐版对上）和 [JUC 自有源码的 MIT](https://github.com/XeronOwO/Just-Unknown-Characters/blob/master/LICENSE)。JUC 的拼音字典另有 [PinIn](https://github.com/Towdium/PinIn) 来源；本模组通过接口调用，不复制字典。 |
| Unknown Enough Items（UEI）和 NeoForge | [UEI](https://github.com/MAakber/Unknown-Enough-Items)是输入拦截的参考，上游为 [MIT](https://github.com/MAakber/Unknown-Enough-Items/blob/main/LICENSE)，与本项目的工具提示实现无关。[兼容性记录](COMPATIBILITY.md)有同一补丁入口的简化示例；当前实现没有引用 UEI DLL。如确认发布的代码或文档重用了实质性源码，随相应内容保留原版权与 MIT 全文。[NeoForge](https://docs.neoforged.net/docs/1.21.1/misc/resourcelocation/)仅用于参考目录和资源命名约定。 |
| MSTest、Microsoft.NET.Test.Sdk、FluentAssertions、开发用 Json.NET | [测试项目](../tests/UnknownInventoryTerminal.Tests/UnknownInventoryTerminal.Tests.csproj#L9-L15)使用的 MSTest、Test SDK、Json.NET 13.0.3 包声明 MIT。**[FluentAssertions 8.6.0](https://www.nuget.org/packages/FluentAssertions/8.6.0) 不是 MIT**：该 NuGet 包随附 Xceed 非商业社区许可。商业开发或 CI 不要默认沿用社区许可；取得适用商业许可或改用 MSTest 自带断言等替代品。不要把测试输出打进模组。 |
| TexturePreviewer、历史 OreUI / WebUI | [贴图预览器](../tools/TexturePreviewer/TexturePreviewer.csproj)是独立本地 WPF 工具；如单独发布其[自包含程序](../tools/TexturePreviewer/Publish-TexturePreviewer.ps1)，需另外检查 .NET/WPF 运行时、原生库、Json.NET 和随包声明。[早期网页前端](webui-ae2-terminal.md)曾含 [MPL-2.0 的 OreUI](https://github.com/katorlys/OreUI/blob/main/LICENSE) 及字体；当前原生包不含这些文件。公开包含旧提交的完整 Git 历史或再发旧网页制品时，仍须另查 MPL 的通知及相应源码要求，并核实字体和其他素材的单独权利。 |

## 发布前检查

1. **先处理流体滑轨和滑块的 CreativeCore 底图权利**：取得覆盖再分发的授权，或替换为独立原创素材；免费公开发布也不能跳过。下拉图集按 AE2 改编发布，随包保留署名与修改说明，不是独立待确认项。确认自有源码的授权范围，并按上表核对屏幕描述中可能保留的上游实质内容。
2. 用[安装清单](../scripts/Install.ps1#L11-L36)选择要打包的文件，使用干净的暂存目录，不压缩整个构建输出目录。已有旧版 ZIP、预览器及截图不是当前发布包；也不应打入其他模组 DLL、测试产物、研究用文件或游戏自带文件。确需另行分发依赖时，必须再按该版本核对并满足其条款（如保留 MIT 通知，或按 LGPL 提供适用的许可、源代码和替换/重新链接所需材料）。
3. 从当前源码重新生成发布件，核对随包的[素材声明](../assets/unknowninventoryterminal/NOTICE.txt)及每项保留资源所需的原许可，并随发布件附上选定的项目许可证及范围说明。抽查 ZIP 内实际文件，而不是只检查 README 或构建是否成功。
