# NexusMods 权限与鸣谢（Permissions and credits）

> 对应 NexusMods 发布页的「Permissions and credits」区块。本文件同时给出可复制文本和各条权限的依据，依据来自 [LICENSE-AUDIT.md](../LICENSE-AUDIT.md)、[NOTICE.txt](../../assets/unknowninventoryterminal/NOTICE.txt) 与 [README.md](../../README.md#鸣谢)。未做任何超出以上文件的权利声明。

## 可直接粘贴的段落

```text
Permissions

Code: my own code is licensed GNU GPL v3.0 only. You may reuse, modify and redistribute it under the GPL terms, including providing the corresponding source for any binary distribution.
Textures: the GUI artwork is NOT covered by the GPL. Applied Energistics 2 textures are CC BY-NC-SA 3.0 (Ridanisaurus Rid, AlgorithmX2 et al.); the Modernity-GTNH-UI / CreativeCore fluid slider art is recorded as CC BY-NC-SA 4.0. Non-commercial, attribution and ShareAlike conditions apply. Do not reupload the texture files as your own asset, and do not sell them.
Redistribution of the full archive: please do not mirror the package on other sites; link to this page instead. Ask first if you need to bundle it.
Modpacks: contact me before including it.

Credits

- Applied Energistics 2 — GUI textures and terminal layout reference. Ridanisaurus Rid, AlgorithmX2 et al. CC BY-NC-SA 3.0. https://github.com/AppliedEnergistics/Applied-Energistics-2
- Modernity-GTNH-UI — fluid slider and rail art. Ranzu, WanQueen and contributors; CreativeCore originals by N247S. Recorded as CC BY-NC-SA 4.0. https://github.com/ModernityGTNH/Modernity-GTNH-UI
- CreativeCore — original SliderRail.png / Slider.png. N247S. https://github.com/GTNH-Museum/CreativeCore
- NeoForge documentation — namespace/directory convention reference only. No Minecraft or NeoForge runtime is required.
- Casualties: Unknown — vanilla item interface. Unity, BepInEx, HarmonyX, CUCoreLib, Json.NET, TextMesh Pro — runtime dependencies, not bundled.
- Together — optional multiplayer compatibility. Just Unknown Characters — optional pinyin search. Unknown Enough Items — input-gating reference only.
- AI-assisted tooling was used during development; it is not an in-game feature.
```

## 权限逐项对照

| Nexus 权限项 | 建议填写 | 依据 |
| --- | --- | --- |
| Upload to other sites / 转载整包 | 不允许；仅允许链接回本页面 | 贴图授权未清前整包不具备公开再分发条件，见下方「发布阻断项」；代码部分按 GPLv3 可再分发，但须随附源码 |
| Modify and release / 修改后发布 | 代码：允许，须按 GPLv3 发布修改后的源码；贴图：按 CC BY-NC-SA 允许改编，须署名、标明修改并以相同方式共享 | [LICENSE-AUDIT 第 7 行](../LICENSE-AUDIT.md#L7)、[NOTICE 第 44–48 行](../../assets/unknowninventoryterminal/NOTICE.txt#L44-L48) |
| Use assets / 使用素材 | 不允许直接挪用贴图文件；改编须署名并遵守 NC-SA | [NOTICE 第 23–48 行](../../assets/unknowninventoryterminal/NOTICE.txt#L23-L48) |
| Sell / 售卖 | 不允许售卖整包或贴图；代码本身按 GPLv3 允许商业使用，但受贴图非商业条款限制 | [LICENSE-AUDIT 第 7 行](../LICENSE-AUDIT.md#L7)、[NOTICE 第 1–6 行](../../assets/unknowninventoryterminal/NOTICE.txt#L1-L6) |
| Include in modpacks / 整合包 | 先联系作者；整合包若含贴图同样受 NC-SA 约束 | 同上 |
| Convert / 移植到其他游戏 | 仅代码可按 GPLv3 移植；贴图不可随移植包分发，除非遵守 NC-SA 条件 | 同上 |

## 发布阻断项（上传前必须确认）

[LICENSE-AUDIT 第 50 行](../LICENSE-AUDIT.md#L50)与 [NOTICE 第 74–79 行](../../assets/unknowninventoryterminal/NOTICE.txt#L74-L79)记录：流体滑轨与滑块所用的 CreativeCore 底图，其再分发权利尚未确认，免费公开发布同样受限。**在取得涵盖底图的授权或替换为原创素材之前，不要上传含贴图的整包。** 若要先发布，可考虑只发布权利已清的代码部分，并在页面明确说明贴图不在包内。

## 署名清单（与压缩包内 NOTICE 一致）

| 材料 | 权利人 / 来源 | 许可 |
| --- | --- | --- |
| AE2 原样与改编贴图（8 张原样图、终端底板、按钮图集、设置底板、按钮栏变体、下拉图集） | Ridanisaurus Rid、AlgorithmX2 等，[Applied Energistics 2](https://github.com/AppliedEnergistics/Applied-Energistics-2) | CC BY-NC-SA 3.0 |
| 流体滑轨与滑块 | Ranzu、WanQueen 等，[Modernity-GTNH-UI](https://github.com/ModernityGTNH/Modernity-GTNH-UI)；底图来自 N247S 的 [CreativeCore](https://github.com/GTNH-Museum/CreativeCore) | Modernity 部分记录为 CC BY-NC-SA 4.0；CreativeCore 底图权利未确认 |
| 目录与资源命名约定 | [NeoForge 文档](https://docs.neoforged.net/docs/1.21.1/misc/resourcelocation/) | 仅作参考 |
| 输入拦截参考 | Aakber，[Unknown Enough Items](https://github.com/MAakber/Unknown-Enough-Items) | MIT；未引用其 DLL |
| 运行依赖与兼容 | Casualties: Unknown、Unity、BepInEx、HarmonyX、CUCoreLib、Json.NET、TextMesh Pro；可选 Together、Just Unknown Characters | 各自许可，均不随包分发 |

## 备注

- 本文件不改变任何素材的原许可，也不代表权利人已授权；Nexus 页面上的权限选项应以上表为准填写。
- 项目在开发过程中使用了 AI 辅助，这属于开发方式披露，不是游戏内功能。
