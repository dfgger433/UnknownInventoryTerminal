# Unknown Inventory Terminal

Casualties Unknown 的客户端背包物品终端。阶段 1 聚焦后端、兼容性边界和可验证的测试窗口。

## 阶段 2 已交付

- 只读取本地玩家的真实物品实例，按实例身份去重；不复制物品、不修改真实容器层级。
- AE2 风格平铺网格按精确物品 ID（或容器 + ID）聚合数量，并保留一个真实实例作为原生交互代表。
- 每个聚合格使用游戏原版 `ContainerItem` / `InvButton` 外观和引用，点击、拖动、电池、容器、组合与装备操作继续进入原版处理管线。
- 搜索、按容器显示、拼音 provider 和 CCL 独立设置继续可用；交互前后都会校验本地物品归属。
- 输入遮罩会阻止终端下方的世界拾取与释放回退，并在关闭、失焦、死亡或角色切换时恢复移动、鼠标和原生 Canvas 状态。
- 客机侧软集成 Together，不添加自定义网络消息，不要求主机安装本 mod。

原版 Canvas、`ContainerItem` prefab、Harmony 输入钩子以及 Together 主客机链路仍需在目标游戏版本中实机验收；自动化测试只覆盖核心投影和搜索逻辑，见 [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)。

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
