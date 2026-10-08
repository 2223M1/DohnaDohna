# 公开源码边界

本仓库公开 DohnaDohna 模组源码、三语、开发工具、技能入口、卡牌编辑板及精简验证记录。它不是直接安装包，也不是商业游戏源码或资源分发包。

以下只保留在本机，不上传：原作图片、角色动作数据、音频及 FMOD bank；私人宿主导出和商业 DLL；NinjaSlayer 历史参考副本；原作语料、素材库与迁移文件清单；本地工具/宿主路径、凭据、存档、录像、隔离安装和构建产物。忽略文件不会从本机删除。

公开克隆可运行 `node tools/verify-project.mjs` 和 `node tools/verify-public-source.mjs`。构建需要自行合法准备对应宿主、RitsuLib、Godot .NET 和选定本地资源，并填写不提交的 `.local/hosts.json`。缺少这些输入时不能导出可玩 PCK。详见 `BUILD-RELEASE.md` 和 `ORIGINAL-GAME-REFERENCE.md`。

`Reference/` 教程和研究定位是作者本地入口，不随公开仓库附送。`tools/verify-migration.py` 仍用于本机静态参考核对，不在公共 CI 中伪装运行；公共 CI 不访问商业输入、不构建游戏包、不上传或发布工坊。

上传前对 Git 索引运行 `node tools/verify-public-source.mjs`，检查文件边界与常见凭据格式，并人工核对文件清单。自动扫描不替代来源与许可审查。

## 来源与权利

- 《多娜多娜》人物、原画、动画和语音属于其原权利人 Alicesoft；《杀戮尖塔2》素材、文本、游戏程序及原生卡牌原型属于 Mega Crit。本仓库不授予这些内容的再分发权。
- 模组工程骨架与部分适配经验来自作者自己的 NinjaSlayer 工程；原型卡牌来源在 `Content/card-pool.json` 与编辑板中保留，模组模型与宿主模型分离。
- RitsuLib 由 OLC / BAKAOLC 提供（NuGet STS2.RitsuLib 0.5.12 声明 MIT，见其上游项目）；Godot 与 Harmony 等由依赖管理或游戏宿主提供，本仓库不捆绑其 DLL，也不替换各自许可证。
- 本次仅按作者要求公开仓库，没有替作者选择新的统一开源许可证。公开可读不代表商业资源或第三方内容获得额外授权。
