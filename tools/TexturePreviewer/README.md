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

发布产物启动后会向上查找仓库内的 `Assets\AE2`，因此可直接用于本项目；开发运行时也使用同一目录。也可用“选择资源目录”选取任意包含下列结构的目录：

```text
AE2/
  screens/terminals/terminal.json
  textures/guis/terminal.png
  textures/guis/states.png
  textures/guis/text_field.png
  textures/gui/sprites/...
```

程序监视选定目录中的 PNG 和 JSON 变更，短暂合并连续保存后自动重新解析和绘制。若编辑器使用“临时文件替换”保存，监视器也会监听新增、删除与重命名事件。

## 功能

- 用模组现有 `Ae2ScreenStyle` 后端模型解析 `terminal.json`，列出有效图像区域并显示解析/贴图/裁切边界警告。
- 以 2×整数像素缩放拼装六列终端样例，并单独展示工具栏图标、搜索焦点、按钮悬停、滑块启用/禁用、返回按钮普通/悬停九宫格及 tooltip 样片。
- 选中任一区域时，同时显示完整图集、裁切框和放大切片。图集空间不足时等比缩小，裁切框随图同步；六格槽位条按六等分读取。
- 贴图按原始 Alpha 合成；拼装面板只在游戏实际使用 Shell 底色的区域铺底色。透明 PNG 切片不会被误判为缺失资源。

终端拼装是静态资源检查样例，不模拟游戏库存、Unity 字体/物品图标或交互；实际游戏渲染仍以 Unity 运行结果为准。

## 构建

```powershell
dotnet build .\tools\TexturePreviewer\TexturePreviewer.csproj -c Release --nologo
```
