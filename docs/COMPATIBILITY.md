# UnknownInventoryTerminal 阶段 1 兼容性依据

本文件记录本机静态核对结果；没有调用 REA MCP，也没有进行运行时联机实测。反编译结果来自本机 `ilspycmd 10.1.1.8388`（普通工具结果），以游戏目录 DLL 为准。

## 目标 DLL 身份

| 文件 | AssemblyVersion | 长度 | SHA-256 |
|---|---:|---:|---|
| `BepInEx/plugins/CUCoreLib.dll` | `1.1.0.0` | `1,222,144` | `C0F064260B2671D02BF802C460AA346C6B858EC9269F2FF93439EBABAA940AA3` |
| `BepInEx/plugins/Together/CasualtiesMP.dll` | `5.5.4.0` | `2,697,216` | `606A7A9B3467CFBB916B41B86697EDB97C82508EE7FFE15432CCB7B08549CF41` |

哈希来自 `Get-FileHash -Algorithm SHA256`；版本来自 `AssemblyName.GetAssemblyName`。Together 上游仓库可供概念参考，但本表和下文均以本机 DLL / 本机反编译为证据。

## CCL 设置与按键 API

本机 `CUCoreLib` 确认以下可编译签名：

```csharp
namespace CUCoreLib.Data;
public sealed class ModOptionDefinition
{
    public static ModOptionDefinition Bool(
        string id, string label, string description, string category,
        bool defaultValue, Action<bool> apply = null);

    public static ModOptionDefinition Keybind(
        string id, string label, string description, string category,
        UnityEngine.KeyCode defaultValue,
        Action<UnityEngine.KeyCode> apply = null);
}

namespace CUCoreLib.Registries;
public static class ModOptionsRegistry
{
    public static bool Register(ModOptionDefinition option);
}

namespace CUCoreLib.Helpers;
public static class CUCoreUtils
{
    public static CUCoreUtils.FriendlyKeybind GetFriendlyKeyBind(string friendlyKeybindName);
    public static bool AllowKeybindRebind(
        string friendlyKeybindName,
        string descriptionToShowInSettingsKeybindMenu);
}
public sealed class CUCoreUtils.FriendlyKeybind
{
    public UnityEngine.KeyCode KeyCode { get; }
    public bool disableInInputFields { get; set; }
    public bool disableInMainMenu { get; set; }
    public bool disableInHealthPanel { get; set; }
    public bool disableInInventory { get; set; }
    public bool IsPressed();
}
```

`FriendlyKeybind.IsPressed()` 先调用 `Input.GetKeyDown(KeyCode)`，再应用 CCL 阻断条件。已确认的阻断检查包括：主菜单、伤口/医疗面板、`PlayerCamera.main.craftingPanel.activeSelf`、原版控制台输入框、原版配方过滤输入框、Together 的 `Together.Chat.CHAT_textbox_input_focused`，以及当前 EventSystem 选中的 `TMP_InputField.isFocused`。

阶段 1 应将 `ConfigEntry<KeyCode>` / `ConfigEntry<bool>` 作为 BepInEx 配置存储，再用独立且稳定的 option id 调用 `ModOptionDefinition.Keybind/Bool`。`ModOptionsRegistry.Register` 会拒绝重复 option id；不要同时为同一按键注册 CCL FriendlyKeybind option 和另一个相同语义的显式 option。当前 DLL 中 `BepInExConfigAutoRegistrar.RunOnce()` 反编译为空，不能把自动注册当作可靠行为；显式注册是本版本更可审计的路径。

## 输入门边界

### 已核对的原版入口

UEI 的既有兼容实现使用：

```csharp
[HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
internal static class PlayerCameraInputPatch
{
    private static bool Prefix() => !TerminalVisible;
}
```

本项目的 Prefix 在返回 `false` 前还会把本地 `Body.moveDir` 清零、关闭蹲伏/跳跃输入并保持 `forceNoMovement`，关闭终端时恢复原值；否则跳过原方法会绕过原版先行清零逻辑。仅在 `Event.current.Use()` 上调用不足以阻止这些每帧逻辑。透明全屏 UGUI `Image` + `GraphicRaycaster` 可阻止 EventSystem UI 点击，但不能阻止 `Update()` 中的 `Input.GetKey*`。

### Together 独立输入

本机 `Together.Chat.Update()` 每帧直接读取：

```csharp
Input.GetKeyDown(KeyBinds.GetBind("casualtiestogether_chat"))
```

所以终端显示期间，即使跳过 `PlayerCamera.HandleInput`，Together 聊天快捷键仍可能被读取。若阶段 1 需要“终端期间完全不抢按键”，需另加可选的 `Together.Chat.Update` Harmony Prefix gate；不要依赖某个 postfix 来阻止原方法已经执行的输入副作用。此 gate 只应暂停聊天快捷键/预览，不应触碰网络状态。

## 原版物品 UI 与 Together 同步路径

终端只应展示快照，并把实际菜单/拖拽交还原版对象；不要在终端中直接复制网络写入协议。

本机 Together 反编译确认：

1. `Together.PlayerCamera_TryPerformInventoryAction_MultiplayerPatch` 补丁挂在 `PlayerCamera.TryPerformInventoryAction(RaycastResult, List<RaycastResult>)`。它验证 `InvButton` 与 UI overlap，再处理电池操作、容器装入/倒入、组合、装备槽交换等路径。
2. `Container.LoadItem(Item)` 的 Together postfix 在 `PlayerCamera_TryPerformInventoryAction_MultiplayerPatch.is_inside_TryPerformInventoryAction` 为真且有 `ItemSync` 信息时加入 `ItemSync.ItemContainerChanged`。
3. `Container.UnloadItem(Item, Body)` 的 Together prefix 同样在上述 inventory-action 作用域内登记 `ItemSync.ItemContainerChanged`。
4. `ItemSync.Client_AnnounceContainerChange(SyncInfo)` 批量发送容器变更；`ItemSync.ItemGetContainerInfo(Item)` 将物品映射为身体槽、穿戴、表面库存或普通 `Container`，并携带 `syncId` / container version 等信息。
5. Together 的 `ItemSync.SafeUnloadItem` 在卸载后若当前打开的容器相同，会调用 `PlayerCamera.main.RepopulateContainer()`；原版 `Container.LoadItem/UnloadItem` 和 `PlayerCamera.RepopulateContainer` 是 UI 状态的安全边界。

因此“Original item menu”和“Drag with original controls”必须最终调用原版 `PlayerCamera`/`InvButton`/`Container` 路径（或在主线程模拟同一原版调用），不得从终端直接修改 transform、parent 或仅修改本地列表来宣称已同步。主机无需安装额外 mod 的前提是：客户端动作仍进入 Together 已有的原版 inventory-action patch 与 `ItemSync` 路径；本终端本身不新增网络消息。

### 拖放到另一件物品

将实际物品拖到另一件具体物品上时，电池拆装及原版 `Body.CanCombine` 认可的组合优先于自动入包和槽位移动。终端只转交目标 `InvButton`，仍由 `HandleReleaseDragging` 执行拾取检查，再由 `TryPerformInventoryAction` 完成操作。聚合格不是物品目标；液体转移、固定栏重排和普通容器放置沿用终端规则。

已静态核对本机 Together **5.5.5.0**（SHA-256 `42DB2CB04FF204EBC83F1B3FB81B02BF977CD37088458429C4094C6660B040EC`）。电池拆装和物品组合走 Together 原有 Harmony 钩子，不新增消息。联机中如果世界未就绪、任一物品未注册或缺少相应钩子，终端取消这次交互，不回退到槽位移动或仅本地修改。

实机验收：用带 `tool` 标签的工具分别拖放到仓储、手持槽和固定栏中装有电池的设备，确认设备失去电池、工具不被移动；再插回尺寸合适的电池。验证尺寸不符、电池仓已占用及对同一实例松手不会触发拆装或槽位移动。联机时需确认主客机的电池和库存状态一致。

### 拖出终端丢弃

只有从终端实际物品格开始的拖拽，在游戏画面内、终端完整边界外松开原版拖拽键，才进入丢弃流程。仍由 `PlayerCamera.HandleReleaseDragging` 执行原版 `DoPickupCheck` 和收尾；在其 UI 处理阶段分别调用 `Container.UnloadItem`、`Body.DropItem` 或 `Body.DropWearable`。原版世界点击仍被屏蔽，因此不会向光标下的场景容器存入物品。Together 的原版释放钩子可对比释放前后的物品归属，无需新增网络消息。

实机验收：分别从仓储实例、固定栏、手持槽和穿戴格尝试丢弃；确认只丢当前实例，且拖回终端、仅按住不松、失焦或窗口内空白不会丢弃。有 Together 时从客户端重复，并在主机侧确认物品落地且双方库存一致。

### 容器间液体转移

终端只识别本地库存中的两个具体液体容器；聚合格需先展开选实例。原版 `HandleReleaseDragging` 仍处理拾取检查和拖拽收尾。确认时重新核对两个实例的归属、液体存量及目标容量，再将所选 mL 交给原版 `LiquidTransfer.Finish`；不直接写液体栈。Together 5.5.4.0 的本机 DLL 已静态核对：`Finish` 的 postfix 使用 `transferFrom`、`transferTo` 和 `ml` 发送原有转移消息。联机时如果未装载该钩子或库存对象未注册，终端拒绝提交，不走仅本地修改的回退路径。

### 客机倒空背包液体

当前安装的 Together 5.5.4.0 `PlayerCamera_HandleWhileDragging_MultiplayerPatch` 在客机以原有消息 75 报告液体容器的剩余量，主机接收器按物品同步 ID、客户端角色、可达性和当前容量验证后执行剩余差额。`ItemSync` 的本机实现支持通过 `Carried` / 背包关系定位同一角色携带的容器，所以物品不需要先拿在手上；主机不需要安装 UnknownInventoryTerminal，但必须运行兼容的 Together。终端在客机开始倒液前检查该 postfix 实际已安装、世界已就绪且物品已注册；否则拒绝本地修改。倒液开始时预置 Together 的现有状态标记，覆盖第一次原版倒液就将容器清空的情况；同步仍使用 Together 消息，不新增协议。

这只是针对本机已安装 DLL 的静态与代码路径核对，不保证其他 Together 版本或主机接收校验结果，也不表示远端已确认。未实测小于 0.5 mL 的残量、浸水状态、网络中断、主机拒绝/回弹、其他物品操作与倒液中途交错；这些场景需在双机游戏中确认。

实机验收：用具体实例、固定栏和手持槽互拖，分别验证取消、转移指定 mL、混合液体比例、转移上限和失去物品归属。输入框获焦后点窗口空白，再分别点击输入框、滑块和两个按钮；确认第一次点击就有响应。按 Esc 或点击「取消」后，确认该次点击不落到库存，下次点击可以立即操作库存，再次打开转移框也能编辑。最后在 Together 客户端复测双方体积一致，确认没有打开第二层原版转移窗口，也没有移动物品本身（原版超重掉落规则除外）。

## 本次未实测 / 风险

- 未启动游戏、未进入主机/客户端会话，未验证不同 Together 规则、容器损坏/休眠袋、距离限制和物品权限。
- 未验证 `PlayerCamera.HandleInput` 在当前游戏版本是否被其他 mod 改写；Harmony patch 顺序可能影响最终输入门。
- 未验证透明 UGUI blocker 与 IMGUI `GUI.Window` 在所有分辨率、EventSystem 配置和暂停状态下的交互；它只能作为 UI 点击层，不能替代 `HandleInput` Prefix。
- 未验证 Together `Chat.Update` gate 与聊天框已聚焦时的恢复体验；关闭终端前后应确保不清除或伪造 Together 的网络聊天状态。
- 未验证 CCL 设置菜单的旧配置迁移；option id 一旦发布应保持稳定，配置 key 与 CCL option id 不应在小版本中无意改名。
