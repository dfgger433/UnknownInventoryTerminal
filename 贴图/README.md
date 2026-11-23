# 贴图源文件恢复记录

本目录此前含有 `states.pdn` 和 `drop_down_list.pdn`。NTFS 与 PowerShell 历史显示，2026-11-22 20:38，工具任务将本目录称作“临时设计源目录”，并执行 `Remove-Item -Recurse -Force`。这是直接删除，不是 Git 分支切换；该目录是未跟踪文件，Git reflog 没有目录变更记录。该操作不创建可用的回收站副本。

## 可恢复文件

- `states.pdn` 来自回收站中同名较早文件的副本，原路径为 `Assets/AE2/textures/guis/states.pdn`。SHA256 `8FDB1B9D2CC72A0F3EB26D732FC267400D64BCC2AA2CC55CF6C08D8BD30AAE6D`，11787 字节；最后修改于 2026-11-20 00:37。它**不是** 11 月 22 日 `贴图/states.pdn` 被删除的精确版本。
- `drop_down_list.pdn` 的原件、回收站副本及 Git 副本均未找到；不以其他 `.pdn` 假冒。

`vertical_buttons_bg_2.png` 原 PNG 字节也未找到。项目中的同名 PNG 是从留存的 21×26 像素表重建，测试验证其像素与表一致。重建文件不是被删的原始 PNG 字节流。