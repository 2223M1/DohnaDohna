# 本机直接安装

已按用户2026-10-06的明确请求安装到日常STS2模组目录；不是上传或工坊发布。

2026-10-06 14:20更新：底部二级人物栏/原生左右按钮、身体与血条锚点、独立受击配音及统一-20 dB已安装。最终隔离界面输入回归通过后才更新；DLL/PCK与已验收候选逐文件SHA256一致。最新备份/收据：`build/local-install/20261006-142054-033/`，旧09:28安装保留为历史记录。本次没有改写settings、跑局或进度存档，成品不含测试驱动；验收范围和NOT RUN见`validation/squad-first-playable.md`。

本机`release_info.json`为`v0.111.0`，使用preview候选。
位置：`C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna/`。
只复制本项目DLL、PCK、manifest和SHA256SUMS，不复制测试驱动。

RitsuLib0.6.5沿用已安装且启用的工坊版本，不再安装重复副本。
Steam账号settings只新增/启用`DohnaDohna`（`mods_directory`）；其余模组设置保持不变，不修改任何跑局或进度存档。
备份及安装收据位于忽略提交的`build/local-install/20261006-092807-566/`。
安装后逐文件SHA256与preview候选一致；原有模组条目及所有非模组settings字段比较一致。
使用已安装目录作为`Invoke-IsolatedBoot.ps1 -ModPackageDirectory`输入，在隔离档案核验加载；不以启动日常档案作为测试手段。

正常从Steam启动STS2，选“多娜多娜小队”进入二层选人即可。当前仍为0.1.0-dev候选，未完成项仍以`validation/squad-first-playable.md`为准。
旧隔离启动器曾遗留测试驱动文件夹：普通启动时它仍被宿主加载，自动测试可能启动后退出。现已修正为非Probe启动前把该目录可恢复地移出mods，不依赖单纯遗漏启用列表来禁用驱动；日常安装包从未包含驱动。

今后更新可在关闭游戏后运行工程的`tools/Install-LocalMod.ps1`。必须使用实际宿主对应的已校验包；脚本对旧同名文件与settings做备份，不支持的宿主直接拒绝安装。

用户提供的`D:/DNDN/`是指向`D:/Games/Standalone/DNDN`的junction。exe、AIN、EX的SHA256均与已有`DohnaDohnaOriginal/reverse/source_manifest.json`一致，因此资料根不用迁移，也未改动原作安装或存档。
