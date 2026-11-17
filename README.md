# Unknown Inventory Terminal

Casualties Unknown 的客户端背包物品终端。阶段 1 聚焦后端、兼容性边界和可验证的测试窗口。

## 阶段 1 已交付

- 只读取本地玩家的 `GetAllItemsThorough()` 物品实例，按实例身份去重。
- `All items (merged)` 按精确物品 ID 汇总数量；`By container` 按容器身份分组。
- 保留原生物品引用、当前名称和 Sprite，搜索扩展接口可被拼音 mod 注册。
- CCL 独立设置分类：终端按键（默认 F7）、按容器显示、拼音 provider 开关。
- 游戏内 IMGUI 测试窗口、`PlayerCamera.HandleInput` 输入门、Together 聊天输入可选门。
- 客机侧软集成 Together，不添加自定义网络消息，不要求主机安装本 mod。

阶段 1 的测试窗口只选中真实物品实例并做本地归属二次校验；“Original item menu”和拖拽按钮目前是原版交互桥接占位，完整原版 UI 交互在阶段 2 完成，见 [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)。

## 构建

在本项目目录运行：

```powershell
.\scripts\Build.ps1 -GameRoot 'D:\stam\steamapps\common\Casualties Unknown'
```

脚本会构建插件和运行 13 个后端测试。需要本机游戏 Managed DLL、BepInEx 5 和 `BepInEx/plugins/CUCoreLib.dll`。

## 安装

关闭游戏后运行：

```powershell
.\scripts\Install.ps1 -GameRoot 'D:\stam\steamapps\common\Casualties Unknown'
```

安装脚本只写入 `BepInEx/plugins/UnknownInventoryTerminal/`，不修改游戏 DLL、Together 或存档。

## 文档

- [本地计划](docs/PLAN.md)
- [兼容性依据和未实测风险](docs/COMPATIBILITY.md)
- [外部搜索 API](docs/SEARCH-API.md)
