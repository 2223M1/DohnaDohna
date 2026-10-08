# DohnaDohna 工程与验证入口

以含 `DohnaDohna.csproj` 的实际目录为 `<repo>`，本机默认 `%USERPROFILE%/Documents/STS2 MOD/DohnaDohna`。路径迁移时读取当前私有配置，不修改历史参考里的路径字符串来让旧脚本运行。

| 用途 | 工程内入口 |
| --- | --- |
| 现行约定与最小开发路线 | `AGENTS.md`、`Docs/ENGINEERING.md` |
| 内容发现入口与身份 | `Scripts/Entry.cs`、`DohnaDohna.json`、`DohnaDohna.csproj` |
| 主机、包与编译条件 | `eng/DohnaDohna.Hosts.props`、`.local/hosts.json` |
| 三语和卡牌规格 | `DohnaDohna/localization/{zhs,eng,jpn}/`、`Docs/card-catalog.md` |
| 构建与发布边界 | `Docs/BUILD-RELEASE.md`、`tools/Build.ps1`、`tools/New-Package.ps1` |
| 工程参考与适配说明 | `Docs/REFERENCE-MAP.md`、`Reference/NinjaSlayer/` |
| 本机工具 | `Docs/TOOLS.md`、同级 `Tools/STS2/toolchain.json` |
| 原作资料 | `Docs/ORIGINAL-GAME-REFERENCE.md`、`.local/original-reference.json` |

RitsuLib 实际版本和 API 来自当前已解析包/程序集与对应主机输入。构建基线、实际安装运行版本与文档快照分开记录；不要把配置中的数字写成永久最新版。

## 选择教程

RitsuLib 当前教程快照在 `Reference/NinjaSlayer/Docs/sts2-ritsulib.ritsukage.com/guide/`，内容教程在 `Reference/NinjaSlayer/Docs/tutorials.sts2modding.com/docs/04-ritsulib/`。只读需要的章节及其 `index.md`：

- 卡牌：`04-01-add-card`、`04-04-card-properties`。
- 遗物/Power/药水：`04-03-add-relic`、`04-05-add-power`、`04-06-add-potion`。
- 音频：`04-10-add-audio`。
- 角色/卡池/动画：`04-14-add-new-character`、`04-15-1-add-card-pool`、`04-15-2-character-animation`。
- Patch/内容注册：`04-24-patch-system`、`04-27-content-registry`；另查 guide 中 patching-guide。

快照缺失或不匹配实际签名时，核对维护者当前官方教程与已安装 API，报告差异；不为了单次功能悄悄同步整份镜像。参考文件带 `.reference` 后缀的入口不得直接运行。

## 按变化选择验证

```powershell
node tools/verify-project.mjs
python tools/verify-migration.py
pwsh -File tools/Build.ps1 -Channel preview
pwsh -File tools/Build.ps1 -Channel stable
```

源码/技能/文档检查与构建分开。只有改动相关生产代码、主机边界、资源导出或包规则时才执行对应构建/导出；纯技能变更不自动要求构建、安装或启动游戏。

原作整理工具回归测试为 `python -m unittest discover -s tools/original_game -p 'test_*.py'`，使用项目文档指定的独立 Python 环境。检验私人 STS2 导出完整性时用 `python tools/verify-vanilla.py`，不把缺少商业输入的环境当作通过。

缺失的逻辑测试、受保护契约或实机 harness 不能虚构；根据真实行为补最小必要验证，未运行项写明 `NOT RUN`。
