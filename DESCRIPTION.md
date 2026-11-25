# GitHub 仓库描述 / Repository Description

> 适用于 GitHub 仓库首页「About」栏目及发布说明，贴合 Unknown Inventory Terminal (UIT) 0.5.0 实际功能特性。

---

## 1. GitHub 仓库「About」短描述（建议直接复制）

### 中文（推荐）
《Casualties Unknown》的 Unity 原生客户端物品终端 Mod。提供 6 列可搜索聚合仓储、装备栏扩展、液体精确转移与快捷动作栏；支持固定栏调序、拖出丢弃、CUCoreLib 设置与 Together 联机兼容。

### 中文（极简版，单行）
Casualties Unknown 的 Unity 原生物品终端，支持可搜索聚合网格、装备栏扩展、液体转移、快捷动作与 Together 联机兼容。

### English
A native Unity inventory terminal mod for Casualties Unknown. Features a 6-column searchable aggregated storage grid, wearable slots expansion, precise fluid transfer dialog, quick drag actions, pin ordering, and CUCoreLib / Together multiplayer compatibility.

---

## 2. 建议 GitHub Topics 标签

`casualties-unknown` `unity` `bepinex` `mod` `inventory-management` `inventory-terminal` `csharp` `cucorelib` `together`

---

## 3. 0.5.0 核心特性概况（简明摘要）

- **Unity 原生终端**：纯原生 Unity UI 渲染，固定居中，不含 WebUI 依赖。
- **6 列可搜索网格**：同类物品按精确 ID 聚合，支持名称/数量排序与滚动翻页；点击查看具体实例。
- **独立物品悬浮框 (0.5.0)**：悬浮框完全由终端自绘并释放状态，不借用或修改原版 Canvas/射线列表，保留原版图标、展开属性与 TextMesh Pro 排版。
- **装备与槽位扩展**：原生主手/副手/主槽与装备槽展示，自动对接 CUCoreLib 扩展装备行与滚轮浏览，支持不可用槽位禁止标识与拖拽穿戴校验。
- **容器与流体交互**：拖动物品自动/手动存入容器；液体容器互拖弹出毫升/滑块精确转移窗口；左侧长按倒空，客机倒液原生复用 Together 同步消息。
- **快捷动作与固定栏**：拖出窗口丢弃物品、左侧按钮使用物品（可选使用后保持拖动）；支持将常用物品固定到仓储首行并拖动调序。
- **生态兼容**：无缝对接 CUCoreLib 快捷键与设置中心；可选接入 JustUnknownCharacters 拼音搜索，可选兼容 Together 联机。
