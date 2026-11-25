# Unknown Inventory Terminal v0.5.0 GitHub Releases 说明

> 本文档为 Unknown Inventory Terminal (UIT) v0.5.0 的 GitHub Releases 发行说明草案，提供中英双语版本，可直接作为 GitHub Release 的标题与正文发布。

---

## Release Title / 标题

`Unknown Inventory Terminal v0.5.0 - Unity 原生物品终端更新`
*(Tag: `v0.5.0`)*

---

## Release Body / 正文 (Markdown)

```markdown
# Unknown Inventory Terminal v0.5.0

《Casualties Unknown》客户端原生 Unity 背包物品终端 Mod 0.5.0 版本正式发布！
本次更新重点修复了终端退出后的悬浮框状态释放、带来了固定栏拖动重排、Together 联机倒液同步校验强化、设置与动作栏界面优化，并完成了原生 Unity 终端的整体稳定化。

A native Unity inventory terminal mod for *Casualties Unknown*. Version 0.5.0 brings tooltip canvas lifecycle fixes, pin bar item reordering, improved Together multiplayer fluid emptying sync, and refined settings/action bar UI layouts.

---

### 📦 运行时安装包 / Release Asset

- **文件名 (File Name)**: `UnknownInventoryTerminal-0.5.0.zip`
- **文件大小 (File Size)**: 137,434 字节 (约 134 KB)
- **SHA-256 校验和 (Checksum)**:
  `12A909A1DAF73AC89B4DDE8B052B858C9AE30EF3B827B7BFC68C0DEF62EAB65C`
- **压缩包目录结构 (Archive Layout)**:
  ```text
  UnknownInventoryTerminal/
  ├── UnknownInventoryTerminal.dll        # 模组主程序集
  ├── UnknownInventoryTerminal.Core.dll   # 核心领域逻辑程序集
  ├── Lang/                               # 多语言文件 (zh_cn.json, en_us.json)
  ├── assets/unknowninventoryterminal/    # GUI 屏幕描述与贴图资源
  ├── LICENSE                             # GNU GPL v3.0 许可证
  └── LGPL-3.0.txt                        # 依赖参考许可证
  ```

---

### 🚀 0.5.0 更新内容 / What's Changed

#### 中文更新说明
- **原生悬浮框生命周期与状态释放**：修复终端退出后的原版悬浮框状态释放；终端不再借用或修改原版悬浮框的 Canvas，也不再临时改写菜单状态或射线列表，并保留终端自绘物品提示与 TextMesh Pro 优质排版。
- **固定栏支持拖拽调序**：支持在仓储首行的手绘固定栏中直接拖动物品调整排序；跨页重排后自动切换至物品所在页，且操作纯属界面排序，绝不改变底层实际物品。
- **联机倒液同步增强 (Together)**：客机从背包倒空液体时复用 Together 的既有同步消息，并在首次倒液即清空容器的边界上保持同步校验；主机无需安装本模组，但需运行兼容的 Together。
- **设置页排版与动画优化**：设置页「动作后保留物品」开关移到文字左侧，保留开、关状态各自的悬停滑块动画，依然支持点击整行自由切换。
- **动态动作栏布局**：拖动物品时，左侧动作栏只排列当前可用的动作按钮（如丢弃、固定/解固、倒液、使用）；进入长按确认或等待状态时，所选按钮保持原位不跳动。
- **界面资源完整性恢复**：恢复 `vertical_buttons_bg_2.png` 的 21×26 像素变体，并加入屏幕资源配置与安装清单，提升界面贴图完整度。

#### English Changelog
- **Tooltip Lifecycle & State Cleanup**: Fixed vanilla tooltip state release after closing the terminal; the terminal no longer borrows or mutates the vanilla tooltip Canvas, menu state, or raycast list, while preserving native custom item tooltips with clean TextMesh Pro rendering.
- **Pin Bar Drag Reordering**: Pinned items in the top pinned bar can now be dragged to rearrange their order; cross-page reordering switches to the corresponding page without moving the underlying items.
- **Multiplayer Fluid Emptying Sync (Together)**: Client fluid emptying now reuses Together's existing network synchronization messages with strict boundary checks when emptying containers on first pour; host does not require UIT as long as compatible Together is running.
- **Settings Layout & Animation**: Moved the "Keep item after action" toggle to the left side of text with preserved smooth hover animations; the whole row remains clickable.
- **Contextual Drag Action Bar**: Dragging items now only displays valid action buttons on the left bar; selected buttons stay pinned in place during hold/confirmation states.
- **Asset Integrity**: Restored the 21x26 variant of `vertical_buttons_bg_2.png` and included it in the screen configuration and installation manifests.

---

### 🌟 核心功能概览 / Key Features

1. **纯 Unity 原生终端**：固定居中显示，彻底移除了旧版本对 WebUI 及外部服务的依赖，性能流畅、资源占用极低。
2. **6 列可搜索聚合网格**：同类物品按精确 ID 聚合展示，支持名称/数量多维排序与滚轮平滑滚动；点击聚合格可展开具体实例选择器。
3. **装备栏与主槽位扩展**：原生主手、副手及主槽位完整显示，自动探测并对接 CUCoreLib 注册的扩展装备，超出屏幕自动提供逐行滚轮浏览。
4. **液体精确转移与快捷倒液**：两个液体容器互拖即可唤起毫升精确输入与 Modernity 风格横向滑块转移窗口；左侧动作栏支持长按连续倒空。
5. **快捷动作与固定栏**：支持常用物品置顶固定并自由拖拽排序；可将物品拖出终端窗口在游戏内直接丢弃；左侧快捷使用按钮支持「动作后保持拖拽」。
6. **生态兼容**：无缝对接 CUCoreLib 设置中心与快捷键；可选接入 JustUnknownCharacters (JUC) 拼音首字母/全拼搜索；可选兼容 Casualties Together 联机。

---

### 📥 安装方法 / Installation

1. **确认前置依赖**：
   - 目标游戏：*Casualties Unknown*
   - [BepInEx 5.4.x (x64)](https://github.com/BepInEx/BepInEx)
   - [CUCoreLib v1.1+](https://github.com/jimmyking9999999/CUCoreLib)（必须安装，提供设置与按键注册支持）
2. **下载并解压**：
   - 下载 Releases 附件中的 `UnknownInventoryTerminal-0.5.0.zip`。
   - 将压缩包内的 `UnknownInventoryTerminal` 文件夹解压并放入游戏的 `BepInEx/plugins/` 目录中。
   - 最终目录结构示例：`Casualties Unknown/BepInEx/plugins/UnknownInventoryTerminal/UnknownInventoryTerminal.dll`。
3. **快捷键**：
   - 默认快捷键为 `F7` 打开/关闭终端，可在游戏内「设置 → CUCoreLib」或「输入/控制」中重新绑定。

---

### 📜 许可证与素材说明 / Licenses & Notice

- **代码许可**：本项目自有代码采用 [GNU General Public License v3.0 (GPL-3.0-only)](LICENSE)。
- **贴图素材归属**：
  - 部分终端底板与按钮素材改编自 [Applied Energistics 2](https://github.com/AppliedEnergistics/Applied-Energistics-2)，遵循 **CC BY-NC-SA 3.0** 许可。
  - 流体滑块及滑轨素材追溯自 [Modernity-GTNH-UI](https://github.com/ModernityGTNH/Modernity-GTNH-UI) / [CreativeCore](https://github.com/GTNH-Museum/CreativeCore)，遵循 **CC BY-NC-SA 4.0** 许可。
  - 详细素材署名及各文件许可条款请参阅随包附带的 `assets/unknowninventoryterminal/NOTICE.txt`。
```
