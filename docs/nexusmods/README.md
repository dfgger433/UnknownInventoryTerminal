# NexusMods 发布文案（Unknown Inventory Terminal）

本文件夹单独存放 [NexusMods](https://www.nexusmods.com/) 发布所需的文案与字段内容，与 GitHub 仓库文档（[DESCRIPTION.md](../DESCRIPTION.md)、[RELEASE-0.5.0.md](../RELEASE-0.5.0.md)）分开维护，避免两套平台的格式互相污染。所有内容依据 [README](../../README.md)、[CHANGELOG](../CHANGELOG.md)、[COMPATIBILITY](../COMPATIBILITY.md)、[LICENSE-AUDIT](../LICENSE-AUDIT.md) 与 [NOTICE](../../assets/unknowninventoryterminal/NOTICE.txt) 编写，未新增任何代码或游戏内功能。

## 文件清单

| 文件 | 用途 | 粘贴到 NexusMods 的位置 |
| --- | --- | --- |
| [short-description.txt](short-description.txt) | 一句话简介（英文 / 简体中文，含字符数） | 「Short description」字段（上限 255 字符） |
| [description.bbcode.txt](description.bbcode.txt) | 完整模组描述，BBCode 格式，中英双语 | 「Description」字段（BBCode 编辑器） |
| [description.md](description.md) | 同一份描述的可读 Markdown 版本 | 仓库留档；Nexus 若切到 Markdown 编辑器可直接粘贴 |
| [permissions-and-credits.md](permissions-and-credits.md) | 授权与鸣谢，按 Nexus 权限问卷口径整理 | 「Permissions and credits」区块 |
| [changelog-0.5.0.bbcode.txt](changelog-0.5.0.bbcode.txt) | 0.5.0 版本更新说明 | 「Changelog」字段（每次上传版本时填写） |

## NexusMods 字段对照

| Nexus 字段 | 本文件夹中的内容 |
| --- | --- |
| Mod name | `Unknown Inventory Terminal`（缩写 UIT） |
| Game | Casualties: Unknown（Steam App ID `4576490`） |
| Version | `0.5.0`，与 [版本配置](../../src/Directory.Build.props) 和 `UnknownInventoryTerminal-0.5.0.zip` 保持一致 |
| Category | 建议放在「User Interface」或最接近的原生 UI 分类；若不适用则选「Miscellaneous」。这只是分类建议，不是模组功能。 |
| Tags | `BepInEx`、`Inventory`、`User Interface`、`Utility`、`Client-side`、`Multiplayer (Together)` |
| Short description | [short-description.txt](short-description.txt) |
| Description | [description.bbcode.txt](description.bbcode.txt) |
| Dependencies | BepInEx 5.4.x（x64）与 CUCoreLib 1.1+ 为必装；Casualties Together、Just Unknown Characters 为可选 |
| Permissions and credits | [permissions-and-credits.md](permissions-and-credits.md) |
| Changelog | [changelog-0.5.0.bbcode.txt](changelog-0.5.0.bbcode.txt) |

## BBCode 约定

`description.bbcode.txt` 与 `changelog-0.5.0.bbcode.txt` 只使用 Nexus 描述编辑器支持的 BBCode 子集：`[b]`、`[i]`、`[list]`/`[*]`、`[url=]`、`[center]`、`[line]`、`[size=2]`–`[size=5]`。不使用表格、Markdown 标题或行内 HTML，以免粘贴后显示原始标记。修改后建议在 Nexus 编辑器的预览里检查一次列表缩进和空行。

## 发布前置检查（重要）

1. **贴图授权未清前不要上传完整包。** [LICENSE-AUDIT 第 50 行](../LICENSE-AUDIT.md#L50) 与 [NOTICE 第 74–79 行](../../assets/unknowninventoryterminal/NOTICE.txt#L74-L79) 记录：Modernity / CreativeCore 流体滑轨与滑块的底图再分发权利尚未确认，免费公开发布同样受限。请先取得覆盖再分发的授权或替换为原创素材，再上传含贴图的整包。
2. **代码与贴图许可分离。** 自有代码为 `GPL-3.0-only`；AE2 贴图按 CC BY-NC-SA 3.0、Modernity 贴图按 CC BY-NC-SA 4.0 记录，均不可用主许可证覆盖、不可商用。上传时须随包保留 `LICENSE`、`assets/unknowninventoryterminal/NOTICE.txt` 及其中的署名与许可链接。
3. **版本号一致。** 上传文件名、页面版本号、`src/Directory.Build.props` 的 `<Version>` 三处必须同为 `0.5.0`；本文件夹内的描述不写死版本号之外的未来功能。
4. **语言选项。** 本模组界面内置 English / 简体中文；Nexus 页面语言建议选 English，并在描述里保留简体中文段落，方便中文玩家阅读。
5. **截图与视频。** 本文件夹不包含截图。需要上传媒体素材时请在游戏内自行截图，不要使用仓库中与界面无关的贴图源文件。

## 宣传图素材（本地草稿）

`../../artwork/nexusmods/` 中的 1300×372 头图与 1920×1080 主图是基于离线界面预览 `../../TestResults/terminal-namespace-preview.png` 制作的产品视觉稿，均明确标注「非实机截图」，不能替代当前版本的游戏内截图。公开上传前，建议另拍 0.5.0 当前构建的实机画面，并核对 Nexus 上传器的裁切预览。两张图复用了 Applied Energistics 2 的 GUI 素材：Ridanisaurus Rid、AlgorithmX2 等；素材许可为 CC BY-NC-SA 3.0。改动包括从离线预览裁切、最近邻放大并编排至原创宣传构图。若公开分发，请一并注明来源及上述作者、将改编图按 CC BY-NC-SA 3.0 共享，并遵守署名、非商业及相同方式共享条件；商用须另获许可。详见 `../../assets/unknowninventoryterminal/NOTICE.txt`。

## 维护约定

- 模组功能变化时，先更新 [README](../../README.md) 与 [CHANGELOG](../CHANGELOG.md)，再同步本文件夹的 `description.*` 与 `changelog-*.bbcode.txt`，避免 Nexus 页面与实际功能不一致。
- 本文件夹只放发布文案，不放 DLL、贴图、存档或游戏文件。
