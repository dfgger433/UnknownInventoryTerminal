# Unknown Inventory Terminal

Casualties Unknown 的客户端背包物品终端。阶段 1 聚焦后端、兼容性边界和可验证的测试窗口。

## 阶段 3 已交付

- 只读取本地玩家的真实物品实例，按实例身份去重；不复制物品、不修改真实容器层级。
- AE2 风格网格按精确物品 ID（或容器 + ID）聚合；主槽位普通物品与容器内物品显示，主槽位/装备槽位中的容器外壳隐藏。
- 聚合格点击后可进入实例选择页；每个实际物品仍使用原版 `ContainerItem` / `InvButton` 交互。
- 拖动终端物品可投放到原版背包容器；终端候选容器支持最小剩余容量、最高减重和手动选择三种规则。
- 所有转移继续通过原版物品处理管线，交互前校验本地归属；不直接调用 `LoadItem`，Together 同步由原有补丁处理。
- 搜索、容器分组、拼音 provider、CCL 设置和关闭/失焦状态恢复继续可用。

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
