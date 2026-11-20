# 终端 GUI 资源

终端按照 [AE2 上游的资源结构](https://github.com/AppliedEnergistics/Applied-Energistics-2/tree/f0ee8bf4bf35431b9f43788401aa609ca2adbcef/src/main/resources/assets/ae2) 分开存放 PNG 与界面描述。它使用 AE2 的 `texture` / `srcRect` 方式声明图像，但不是 Minecraft 资源包加载器；不支持 AE2 的 `includes`、`.mcmeta` 或完整屏幕 schema。

| 源文件 | 用途 |
|---|---|
| [terminal.json](screens/terminals/terminal.json) | `terminalStyle` 按 AE2 `base_terminal.json` 的方式把底板分为 `header`、`firstRow`、`row`、`lastRow`、`bottom` 五段；`images` 指定搜索、按钮状态、图标、滑块和左侧按钮栏背景。 |
| [terminal.png](textures/guis/terminal.png) | 终端底板（3 行时为 141×170）：窗框、仓储格、滚动槽、3 行装备格和 1 行主物品栏。 |
| [states.png](textures/guis/states.png) | 手绘图集：按钮底图（普通、聚焦、悬停）及关闭、排序、设置（x=32/48，y=48）、返回、终端风格（短/中等/长/最长，y=112）等图标。 |
| [vertical_buttons_bg.png](textures/gui/sprites/vertical_buttons_bg.png) | AE2 原版左侧按钮栏背景（21×26），垫在左侧工具栏按钮下方。 |
| [text_field.png](textures/guis/text_field.png) | 搜索框普通/聚焦状态和工具提示背景。 |
| [background.png](textures/guis/background.png) | 终端内设置面板的浅色背景。 |
| [checkbox.png](textures/guis/checkbox.png) | 设置面板的加速/使用状态贴图。 |
| [big_scroller.png](textures/gui/sprites/big_scroller.png) / [big_scroller_disabled.png](textures/gui/sprites/big_scroller_disabled.png) | 原版 AE2 仓储终端的独立滑块（各 12×15），分别绑定 `thumb` 和 `thumbDisabled`。没有可滚动行时显示禁用贴图。 |
| [transparent.png](textures/gui/sprites/transparent.png) | 隐藏原版 `InvButton` 的外框；物品图标、悬停和拖放仍由游戏控件处理。请保持全透明。 |
| [NOTICE.txt](NOTICE.txt) | AE2 原图来源和授权条件。 |

## 替换或拆分图集

1. 贴图只做整数倍像素放大，不缩放。底板按 AE2 `MEStorageScreen.drawBG` 的顺序上下堆叠：`header`（141×17）、`firstRow`、`row` 重复若干次、`lastRow`（各 141×18），最后是 `bottom`（141×99）。仓储为 3 行时正好拼出 terminal.png 的原图，因此 `bottom` 必须从第 3 行仓储之后开始裁切。`row` 会重复，所以它的滚动槽必须贯穿整段高度。`bottom` 内的位置固定：状态行在其顶部，装备格在其下 14 像素，主物品栏在其下 72 像素；滚动槽 x=124。PNG 中透明的像素在游戏里保持透明，不会补底色。按钮和图标按 `srcRect` 的原尺寸绘制；悬停时先画下移 1 像素的按钮底图，再画同样下移的图标。启用/禁用滑块建议使用相同尺寸，滑块以自身尺寸居中，沿槽的上下内边缘移动。搜索框和工具提示按控件宽度横向缩放。左侧按钮栏背景按 AE2 sprite 元数据分段：上 2 行和下 4 行保持原样，中间按可见按钮数量纵向拉伸，宽度必须是 21。
2. 关闭按钮位于终端右上角。左侧工具栏依次是排序方式、排序方向和终端风格；打开分组后，下方还会显示返回按钮。图标按裁切区内的非透明像素居中，透明留白不影响对齐。终端风格按钮与 AE2 一样，在短、中等、长、最长之间循环（左键下一档，右键上一档），仓储行数分别取屏幕可容纳行数的 1/4、2/4、3/4 和全部，但不会少于 terminal.png 画好的 3 行。选择保存在 BepInEx 配置的 `TerminalStyle` 项中。
3. 改动图集排列后，按新位置改写屏幕描述中对应区域的 `texture` 和 `srcRect`。`texture` 相对于 `textures/`，用 `/` 分隔；路径必须位于 `guis/` 或 `gui/sprites/`，用小写字母、数字、下划线或连字符命名，并以 `.png` 结尾。`srcRect` 是从 PNG **左上角**起的 `[x, y, 宽, 高]`，必须在图片内。
4. 从项目根目录运行[构建脚本](../../scripts/Build.ps1)。输出保持 `dist/Assets/AE2/` 的目录层级；退出游戏后运行[安装脚本](../../scripts/Install.ps1)。安装脚本只复制内置资源清单。如果 JSON 引用了新文件名，须手工复制额外 PNG 到已安装插件的相同相对路径，或将其加入安装清单。运行时从插件 DLL 所在目录读取资源，在本次会话内缓存；修改后需重启游戏。

不会读取旧版平铺 PNG 或 `AE2_New` 目录。缺失文件或错误裁切会写入 BepInEx 日志，受影响控件用纯色、字符或透明外框继续显示，不会切回旧资源。构建测试会检查项目自带屏幕的区域声明、PNG 边界、terminal.png 中各网格的位置、`row` 的滚动槽，以及按钮栏背景的分段；实机请另看搜索焦点、按钮悬停、滑块、四档终端风格，以及左侧返回按钮和原版拖放。
