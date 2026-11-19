# 终端界面与物品 API

`UnknownInventoryTerminal` 命名空间提供两个入口：`TerminalApi` 控制本地窗口、挂载扩展 UI，`TerminalItemsApi` 读取本地库存。**所有调用都须在 Unity 主线程执行**。搜索继续使用原有的 [SearchApi](<SEARCH-API.md>)。

## 引用与加载顺序

编译时引用终端插件和 Core 两个 DLL，以及用到的游戏/Unity 程序集。不要把这些 DLL 再打进自己的 mod；使用游戏安装里的同一份程序集（MSBuild 引用可设置 `Private=false`）。必需集成用 BepInEx 的最低版本依赖，例如：

```csharp
[BepInDependency("com.unknown.inventoryterminal", "0.4.4")]
```

如果终端只是一项可选功能，使用软依赖并通过 `Chainloader.PluginInfos` 检查终端是否安装及版本是否满足要求。不要在必加载类型的字段或签名中使用终端类型；将直接调用隔离在通过检查后才进入的方法中，并禁止内联，或使用反射。

## 界面：开关、定位和挂载

| 入口 | 用途 |
| --- | --- |
| `TerminalApi.IsOpen` | 窗口是否可见。 |
| `TerminalApi.TryOpen()` / `TryClose()` | 通过终端原有的打开检查和关闭清理。返回 `true` 表示本次完成了相应的开关操作。暂停、其他模态界面、文本输入、拖拽或无本地角色时可能拒绝打开。 |
| `TerminalApi.TryGetBounds(out Rect bounds)` | 读取窗口外框，采用 Unity 屏幕像素，**左下角**为原点，与 `Input.mousePosition` 一致。窗口固定居中、不可拖动，随分辨率调整；定位时按需重新读取。 |
| `TerminalApi.ExtensionRoot` | 窗口打开且扩展根可用时返回专用 `RectTransform`。子控件裁剪在原生面板内；外框与挂载区域不是同一坐标空间。 |

终端管理挂载根和父级显隐：关闭时子控件随父级隐藏，重开不必重新注册。不要修改根的布局、组件或 Canvas，也不要查找或移动终端自身子对象。只清理自己创建的子对象。扩展可使用普通 Unity UI 控件，但不会接收原版物品拖放；纯显示控件应关闭射线检测，避免遮挡物品格。

下面演示每个挂载根实例只创建一次 UI，并在扩展 mod 禁用时清理：

```csharp
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using UnknownInventoryTerminal;

[BepInPlugin("example.terminal.overlay", "Terminal Overlay Example", "1.0.0")]
[BepInDependency("com.unknown.inventoryterminal", "0.4.4")]
public sealed class TerminalOverlay : BaseUnityPlugin
{
    private GameObject? _overlay;
    private RectTransform? _host;

    private void Update()
    {
        var root = TerminalApi.ExtensionRoot;
        if (!root || (_overlay && _host == root)) return;
        RemoveOverlay(); // 根被重建时清理旧控件。
        _host = root;
        _overlay = new GameObject("example.terminal.overlay", typeof(RectTransform), typeof(Image));
        var rect = _overlay.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-4, -4);
        rect.sizeDelta = new Vector2(5, 5);
        var image = _overlay.GetComponent<Image>();
        image.color = Color.cyan;
        image.raycastTarget = false;
    }

    private void OnDisable() => RemoveOverlay();

    private void RemoveOverlay()
    {
        if (_overlay) Destroy(_overlay);
        _overlay = null;
        _host = null;
    }
}
```

## 物品：仓储快照与穿戴栏

- `TryGetSnapshot(out entries, out filteredRows)` 返回最近一次仓储刷新数据，不主动扫描。`entries` 包含本地玩家容器内的真实实例（含嵌套容器的内容），不受搜索影响；`filteredRows` 反映最近一次搜索和分组投影。`row.Instances` 保留组内所有实例，`row.First` 只代表其中一件。不要依赖网格的显示顺序。终端关闭或会话无效时返回 `false` 与空列表；`true` 也可能是空库存。
- `TryResolve(instanceKey, out item)` 仅解析当前仓储索引中的实例，重新校验物品仍属于当前本地玩家且仍在快照所记录的直接父容器内。如果物品移动、销毁或角色会话变化，应重新读取快照。`InstanceKey` 只用于当前打开的终端，不是物品类型 ID、存档 ID 或 Together 同步 ID。
- `GetLoadout()` 返回去重的本地手持、主槽位和已穿戴真实实例，不受搜索影响，不包含容器内容。它不是槽位表，不包含空槽；同一件多槽装备只返回一份。会话不可用时返回空列表。

```csharp
if (TerminalItemsApi.TryGetSnapshot(out var entries, out var rows))
{
    foreach (var row in rows)
        foreach (var entry in row.Instances)
            if (TerminalItemsApi.TryResolve(entry.InstanceKey, out var item))
                Logger.LogInfo($"{item.id}: {entry.DisplayName}");
}
```

快照和列表用于只读展示；其中的 `Item`、`InventoryEntry.NativeItem` 和图标是游戏对象引用，不应跨会话缓存或直接修改。API 不生成物品，也不提供移动、穿戴或同步命令：新物品定义继续使用 CUCoreLib 的 `ItemRegistry`，终端的物品交互继续使用游戏原版物品处理和 Together 已有的同步路径。
