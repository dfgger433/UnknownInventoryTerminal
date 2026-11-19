# 终端 GUI 资源

终端按照 [AE2 上游的资源结构](https://github.com/AppliedEnergistics/Applied-Energistics-2/tree/f0ee8bf4bf35431b9f43788401aa609ca2adbcef/src/main/resources/assets/ae2) 分开存放 PNG 与界面描述。它使用 AE2 的 `texture` / `srcRect` 方式声明图像，但不是 Minecraft 资源包加载器；不支持 AE2 的 `includes`、`.mcmeta` 或完整屏幕 schema。

| 源文件 | 用途 |
|---|---|
| [terminal.json](screens/terminals/terminal.json) | `terminalStyle` 指定窗框、仓储行、滚动槽和配装背景；`images` 指定搜索、按钮状态、图标和滑块。 |
| [terminal.png](textures/guis/terminal.png) | 终端底板、仓储网格与主槽/装备槽图集。 |
| [states.png](textures/guis/states.png) | 按钮底图（普通/悬停）与关闭、排序、刷新等图标图集。 |
| [text_field.png](textures/guis/text_field.png) | 搜索框普通/聚焦状态和工具提示背景。 |
| [big_scroller.png](textures/gui/sprites/big_scroller.png) / [big_scroller_disabled.png](textures/gui/sprites/big_scroller_disabled.png) | 滑块可用/禁用状态。 |
| [transparent.png](textures/gui/sprites/transparent.png) | 隐藏原版 `InvButton` 的外框；物品图标、悬停和拖放仍由游戏控件处理。请保持全透明。 |
| [NOTICE.txt](NOTICE.txt) | AE2 原图来源和授权条件。 |

## 替换或拆分图集

1. 想保留现有裁切坐标时，直接替换对应 PNG，保持同样的尺寸和图集排列。如果只替换某个区域或要换用独立 PNG，改写屏幕描述中该区域的 `texture` 和 `srcRect`。例如 `"thumb": { "texture": "gui/sprites/big_scroller.png", "srcRect": [0, 0, 12, 15] }`。
2. `texture` 相对于 `textures/`，用 `/` 分隔；路径必须位于 `guis/` 或 `gui/sprites/`，用小写字母、数字、下划线或连字符命名，并以 `.png` 结尾。`srcRect` 是从 PNG **左上角**起的 `[x, y, 宽, 高]`，必须在图片内。它只控制图片取样，不改变窗口尺寸、控件位置或点击区域。`mainSlot` 和 `equipmentSlot` 各应是一条能等分为六格的贴图；`backButton` 和 `backButtonHover` 的 `border` 是以源像素为单位的九宫格边宽，两者应匹配。
3. 从项目根目录运行[构建脚本](../../scripts/Build.ps1)。输出保持 `dist/Assets/AE2/` 的目录层级；退出游戏后运行[安装脚本](../../scripts/Install.ps1)。安装脚本只复制内置资源清单。如果 JSON 引用了新文件名，须手工复制额外 PNG 到已安装插件的相同相对路径，或将其加入安装清单。运行时从插件 DLL 所在目录读取资源，在本次会话内缓存；修改后需重启游戏。

不会读取旧版平铺 PNG 或 `AE2_New` 目录。缺失文件或错误裁切会写入 BepInEx 日志，受影响控件用纯色、字符或透明外框继续显示，不会切回旧资源。构建测试会检查项目自带屏幕的区域声明及 PNG 边界；实机请另看搜索焦点、按钮悬停、两个滑块状态，以及聚合页返回和原版拖放。
