# 终端贴图实时预览器

Windows WPF 桌面工具，直接编译链接模组的 `Ae2ScreenStyle` 与 `Ae2TerminalLayout`，用同一份屏幕 JSON 和左上角 `srcRect` 坐标预览终端贴图。它是开发辅助工具，不属于游戏插件或 WebUI。

## 启动

开发运行需要 Windows、.NET 10 SDK。项目根目录运行：

```powershell
.\tools\TexturePreviewer\Run-TexturePreviewer.ps1
```

需要生成 Windows x64 单文件 EXE（自包含 .NET 运行时；WPF 原生组件作为相邻文件发布）时运行：

```powershell
.\tools\TexturePreviewer\Publish-TexturePreviewer.ps1
.\dist\TexturePreviewer\TexturePreviewer.exe
```

发布产物启动后会向上查找 `assets/unknowninventoryterminal/`，并优先监视[仓库内的源资源](../../assets/unknowninventoryterminal/)，不会把 `dist` 下的构建副本当成项目源文件；开发运行时也使用同一目录。也可用“选择资源目录”选取符合新格式的资源根目录（直接包含 `screens` 与 `textures`）：

```text
unknowninventoryterminal/
  screens/terminals/terminal.json
  textures/gui/terminal/terminal_background.png
  textures/gui/terminal/terminal_controls.png
  textures/gui/widgets/text_field.png
  textures/gui/{settings,storage,fluid,widgets}/...
```

屏幕 JSON 只接受 `unknowninventoryterminal:textures/...` 纹理引用，不会退回旧目录或解析无命名空间的引用。程序监视选定目录中的 PNG 和 JSON 变更，短暂合并连续保存后自动重新解析和绘制。若编辑器使用“临时文件替换”保存，监视器也会监听新增、删除与重命名事件。

## 功能

- 用模组现有 `Ae2ScreenStyle` 后端模型解析 `terminal.json`，列出有效图像区域并显示解析/贴图/裁切边界警告。
- 以 2×整数像素缩放显示终端样例（短风格，3 行仓储）：底板由 header、firstRow、row、lastRow、bottom 五段按游戏方式拼出，左侧按钮栏背景、工具栏按钮（含终端风格）、滑块和标签放在与游戏相同的位置；另行展示搜索焦点、按钮普通/悬停下沉/聚焦、滑块启用/禁用、tooltip 及四个终端风格图标。
- 选中任一区域时，同时显示完整图集、裁切框和放大切片。图集空间不足时等比缩小，裁切框随图同步。
- 贴图按原始 Alpha 合成，不铺任何底色；PNG 中透明的区域（如滚动条下方）在预览和游戏中都保持透明。

终端拼装支持调试预览：选择终端、工具栏、搜索、tooltip、仓储或装备组件，使用 X/Y 滑块微调位置，并模拟悬停、聚焦、点击状态；“重置”恢复默认位置。它仍不模拟游戏库存、Unity 字体/物品图标或真实输入，实际游戏渲染以 Unity 运行结果为准。

## 构建

```powershell
dotnet build .\tools\TexturePreviewer\TexturePreviewer.csproj -c Release --nologo
```
