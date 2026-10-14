# 工程与验证入口

工程路径相对 `DohnaDohna.csproj` 所在目录；只读取与任务相关的入口。

| 需要定位 | 当前入口 |
| --- | --- |
| 注册与工程身份 | `Scripts/Entry.cs`、`DohnaDohna.json`、`DohnaDohna.csproj` |
| 宿主与依赖 | `eng/DohnaDohna.Hosts.props`、`.local/hosts.json`、当前解析的包/程序集 |
| 卡牌规则、规格与导出 | `Content/card-pool.json`、`Docs/card-catalog.md`、`Docs/squad-spec.md`、`Docs/card-metadata.json` |
| 三语 | `DohnaDohna/localization/{zhs,eng,jpn}/` |
| 编辑板回交 | `Docs/authoring/README.md`、`tools/card-board.mjs` |
| 构建、PCK、隔离实机 | `Docs/BUILD-RELEASE.md` |
| 历史参考适配 | `Docs/REFERENCE-MAP.md` |
| 本机工具 | `Docs/TOOLS.md`、同级 `Tools/STS2/toolchain.json` |

RitsuLib 编译基线、实际运行版本与教程快照分别核对，不把配置数字当作永久最新版。参考脚本的 `.reference` 后缀不得直接去除执行。

## 教程按需读取

内容教程根为 `Reference/NinjaSlayer/Docs/tutorials.sts2modding.com/docs/04-ritsulib/`，只读对应章节的 `index.md`：

| 任务 | 章节 |
| --- | --- |
| 卡牌 | `04-01-add-card`、`04-04-card-properties` |
| 遗物、Power、药水 | `04-03-add-relic`、`04-05-add-power`、`04-06-add-potion` |
| 音频 | `04-10-add-audio` |
| 角色、卡池、动画 | `04-14-add-new-character`、`04-15-1-add-card-pool`、`04-15-2-character-animation` |
| Patch、注册 | `04-24-patch-system`、`04-27-content-registry` |

框架补充见 `Reference/NinjaSlayer/Docs/sts2-ritsulib.ritsukage.com/guide/` 对应章节，如 `patching-guide/index.md`。快照缺失或签名不符时核对维护者官方文档与已安装 API，不自动同步整份镜像。

## 按改动选择验证

| 改动 | 检查入口 |
| --- | --- |
| 工程基本检查 | `node tools/verify-project.mjs` |
| 静态参考边界 | `python tools/verify-migration.py` |
| 玩法逻辑 | `Tests/DohnaDohna.LogicTests/` 中相关行为测试 |
| 生产代码/宿主契约 | `pwsh -File tools/Build.ps1 -Channel preview` 或 `stable`；契约变化核对每个相关真实宿主 |
| 资源/包规则 | `tools/New-Package.ps1` 及 BUILD-RELEASE 的导入、导出、包检查 |
| 实机行为 | BUILD-RELEASE 中相关隔离场景，复用生产实现 |

技能/文档修改不触发游戏构建或启动；测试驱动不进入运行包。缺少输入或未运行的检查写 `NOT RUN`，不以编译通过代替实机、联机或跨平台验证。
