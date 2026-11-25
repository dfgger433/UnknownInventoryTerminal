# 终端 GUI 资源

终端按 NeoForge 的命名空间目录约定，将 PNG 与界面描述放在 `assets/unknowninventoryterminal/` 下。图像声明保留 [AE2 的 `texture` / `srcRect` 结构](https://github.com/AppliedEnergistics/Applied-Energistics-2/tree/f0ee8bf4bf35431b9f43788401aa609ca2adbcef/src/main/resources/assets/ae2)，但不加载 Minecraft 资源包，也不支持 AE2 的 `includes`、`.mcmeta` 或完整屏幕 schema。

| 源文件 | 用途 |
|---|---|
| [terminal.json](screens/terminals/terminal.json) | `terminalStyle` 按 AE2 `base_terminal.json` 的方式把底板分为 `header`、`firstRow`、`row`、`lastRow`、`bottom` 五段；`images` 指定搜索、按钮状态、图标、滑块和左侧按钮栏背景。 |
| [terminal_background.png](textures/gui/terminal/terminal_background.png) | 终端底板（3 行时为 141×170）：窗框、仓储格、滚动槽、3 行装备格和 1 行主物品栏。 |
| [terminal_controls.png](textures/gui/terminal/terminal_controls.png) | 手绘图集：按钮底图（普通、聚焦、悬停）及关闭、排序、设置（x=32/48，y=48）、返回、终端风格（短/中等/长/最长，y=112）等图标。 |
| [vertical_button_bar.png](textures/gui/widgets/vertical_button_bar.png) | AE2 原版左侧按钮栏背景（21×26），用于工具栏和未贴齐面板底边的拖拽动作栏。 |
| [vertical_button_bar_variant.png](textures/gui/widgets/vertical_button_bar_variant.png) | 恢复的按钮栏变体（21×26）。原 PNG 字节不可得，但像素表已核验；通过 `verticalButtonsBgVariant` 保留独立替换入口。 |
| [vertical_button_bar_bottom.png](textures/gui/widgets/vertical_button_bar_bottom.png) | 当前布局使用的手绘接缝变体（21×26），拖拽动作栏贴齐三行面板底边时连接侧栏与面板。 |
| [text_field.png](textures/gui/widgets/text_field.png) | 搜索框普通/聚焦状态和工具提示背景。 |
| [settings_background.png](textures/gui/settings/settings_background.png) | 终端内设置面板的浅色背景。 |
| [toggle.png](textures/gui/settings/toggle.png) | 设置页「使用后保持拖动」的滑动开关图集。 |
| [drop_down_list.png](textures/gui/widgets/drop_down_list.png) | 语言和容器摆放共用的下拉图集，以 AE2 按钮底图为基础裁切、重排并加上箭头，按 CC BY-NC-SA 3.0 保留归属；两列裁切均保留完整 112 px。 |
| [scroll_thumb.png](textures/gui/storage/scroll_thumb.png) / [scroll_thumb_disabled.png](textures/gui/storage/scroll_thumb_disabled.png) | 原版 AE2 仓储终端的独立滑块（各 12×15），分别绑定 `thumb` 和 `thumbDisabled`。没有可滚动行时显示禁用贴图。 |
| [fluid_slider_rail.png](textures/gui/fluid/fluid_slider_rail.png) | Modernity-GTNH-UI 的 82×18 流体数量滑轨，左/中/右分别裁切 `[1,7,1,4]`、`[2,7,1,4]`、`[80,7,1,4]`。 |
| [fluid_slider_thumb.png](textures/gui/fluid/fluid_slider_thumb.png) | Modernity-GTNH-UI 的 5×18 流体数量滑块，完整裁切 `[0,0,5,18]`。 |
| [Modernity license](licenses/modernity/LICENSE.txt) | Modernity 素材按 CC BY-NC-SA 4.0 记录，上游许可正文原样保留；CreativeCore 底图的再分发权未获确认，先确认授权或替换滑轨和滑块再公开分发。 |
| [transparent_button.png](textures/gui/widgets/transparent_button.png) | 隐藏原版 `InvButton` 的外框；物品图标、悬停和拖放仍由游戏控件处理。请保持全透明。 |
| [NOTICE.txt](NOTICE.txt) | AE2 与 Modernity 原图来源、本地修改和已核实的许可。待确认项见[第三方许可审计与发布清单](../../docs/LICENSE-AUDIT.md)。 |

项目根目录 `贴图/` 仅在本地保留并已加入 `.gitignore`，其 Paint.NET 工程不会提交或上传。已恢复的 `states.pdn` 是 SHA256 已核验的 2026-11-20 较早版本，并非 11 月 22 日被删除的稿件；`drop_down_list.pdn` 原件未找回。

## 设置面板手绘尺寸

| 部件 | PNG 源尺寸或裁切 `[x, y, 宽, 高]` | 游戏内 1× 尺寸 |
|---|---|---|
| 设置页背景 | [settings_background.png](textures/gui/settings/settings_background.png) 的 `[0, 0, 124, 82]`，四边各保留 4 px | 收起 124×82，展开时每行增加 19 px，四角和边框不缩放变形 |
| 下拉字段及列表项 | [drop_down_list.png](textures/gui/widgets/drop_down_list.png) 提供带箭头的字段帧和列表项贴图；展开后的选项按下时复用 [terminal_controls.png](textures/gui/terminal/terminal_controls.png) 的按压帧 | 同宽 112 px；字段命中区高 20 px，文字和箭头随底图位移并使用同一状态颜色；列表行高 19 px，悬停和按下时底图与文字分别比常态下移 1 px 和 2 px，命中范围不变；超出四行可滚轮翻动 |
| 右上返回按钮 | [terminal_controls.png](textures/gui/terminal/terminal_controls.png)，底图源尺寸 18×20；悬停为 18×19 | 原尺寸绘制，悬停下移 1 px |
| 左侧设置图标 | [terminal_controls.png](textures/gui/terminal/terminal_controls.png)，`[32, 48, 16, 16]` | 16×16 裁切，按可见像素居中 |
| 使用后保持拖动 | [toggle.png](textures/gui/settings/toggle.png) 下半部：关闭 `[0,28,22,12]`、开启 `[0,40,22,12]`，对应的悬停态分别为 `[22,28,22,12]`、`[22,40,22,12]` | 原尺寸 22×12，靠左居中，文字在右，整行可点击 |

制作背景时，把角和边框画在最外侧 4 px 内。九宫格保留四角，沿边长方向缩放边条，并缩放中间填充。显示尺寸随终端倍率整体放大。

## 替换或拆分图集

1. 贴图只做整数倍像素放大，不缩放。底板按 AE2 `MEStorageScreen.drawBG` 的顺序上下堆叠：`header`（141×17）、`firstRow`、`row` 重复若干次、`lastRow`（各 141×18），最后是 `bottom`（141×99）。仓储为 3 行时正好拼出 `terminal_background.png` 的原图，因此 `bottom` 必须从第 3 行仓储之后开始裁切。`row` 会重复，所以它的滚动槽必须贯穿整段高度。`bottom` 内的位置固定：状态行在其顶部，装备格在其下 14 像素，主物品栏在其下 72 像素；滚动槽 x=124。PNG 中透明的像素在游戏里保持透明，不会补底色。按钮和图标按 `srcRect` 的原尺寸绘制；悬停时先画下移 1 像素的按钮底图，再画同样下移的图标。启用/禁用滑块建议使用相同尺寸，滑块以自身尺寸居中，沿槽的上下内边缘移动。搜索框按控件宽度横向缩放；终端所有悬浮提示共用 `tooltip` 区域，默认取搜索框普通态的原图，按 3 px 九宫格保留边角与描边。左侧按钮栏背景按 AE2 sprite 元数据分段：上 2 行和下 4 行保持原样，中间按可见按钮数量纵向拉伸，宽度必须是 21。
2. 关闭按钮位于终端右上角。左侧工具栏依次是排序方式、排序方向和终端风格；打开分组后，下方还会显示返回按钮。图标按裁切区内的非透明像素居中，透明留白不影响对齐。终端风格按钮与 AE2 一样，在短、中等、长、最长之间循环（左键下一档，右键上一档），仓储行数分别取屏幕可容纳行数的 1/4、2/4、3/4 和全部，但不会少于 `terminal_background.png` 画好的 3 行。选择保存在 BepInEx 配置的 `TerminalStyle` 项中。
3. 改动图集排列后，按新位置改写屏幕描述中对应区域的 `texture` 和 `srcRect`。`texture` 必须使用 `unknowninventoryterminal:textures/` 前缀，如 `unknowninventoryterminal:textures/gui/terminal/terminal_background.png`，对应本目录下的 [terminal_background.png](textures/gui/terminal/terminal_background.png)。`textures/` 下只读取 `gui/` 子目录内的小写 `.png` 路径，各段仅使用小写字母、数字、下划线或连字符；按用途分别放入 `gui/terminal/`、`gui/settings/`、`gui/storage/`、`gui/fluid/` 与 `gui/widgets/`。`srcRect` 是从 PNG **左上角**起的 `[x, y, 宽, 高]`，必须在图片内。
4. 从项目根目录运行[构建脚本](../../scripts/Build.ps1)。输出保持 `dist/assets/unknowninventoryterminal/` 的目录层级；退出游戏后运行[安装脚本](../../scripts/Install.ps1)。安装脚本只复制内置资源清单。如果 JSON 引用了新文件名，须手工复制额外 PNG 到已安装插件的相同相对路径，或将其加入安装清单。运行时从插件 DLL 所在目录读取资源，在本次会话内缓存；修改后需重启游戏。

旧的 `Assets/AE2`、`AE2_New`、平铺 PNG 和无命名空间引用不再读取。如果已有自定义贴图，请将旧 `textures/guis/` 和嵌套 `textures/creativecore/textures/gui/` 中的 PNG 移到对应的 `textures/gui/` 用途目录，并按上表更新 `texture`；从旧 `gui/sprites/` 迁移时，也要更新已改名的控件和滑块路径。安装脚本不会删除旧文件。缺失文件或错误裁切会写入 BepInEx 日志，受影响控件用纯色、字符或透明外框继续显示，不会切回旧资源。构建测试会检查项目自带屏幕的区域声明、PNG 边界、`terminal_background.png` 中各网格的位置、`row` 的滚动槽，以及按钮栏背景的分段；实机请另看搜索焦点、按钮悬停、滑块、四档终端风格，以及左侧返回按钮和原版拖放。
