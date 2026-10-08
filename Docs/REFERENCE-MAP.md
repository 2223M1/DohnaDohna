# 参考定位表

《多娜多娜》原作资料与此处 NinjaSlayer 工程参考分开。原作本地逆向入口、素材分类与恢复限制见 `ORIGINAL-GAME-REFERENCE.md`；实际路径绑定在 `.local/original-reference.json`，不将整份原作导出加入编译或发布范围。

以下路径相对 `Reference/NinjaSlayer/`。`.reference` 是迁移时追加的安全后缀，该发布源码快照内容字节未改；原始路径和 SHA-256 见 migration-manifest.json。LocalAuthoring 中去卡图的编辑板属于显式转换的例外，同时记录转换前后散列。

| 需求 | 源码、工具、文档 |
| --- | --- |
| RitsuLib 入门、注册、生命周期 | `Docs/sts2-ritsulib.ritsukage.com/guide/`、`Docs/tutorials.sts2modding.com/docs/04-ritsulib/`、`Scripts/Entry.cs` |
| 卡牌、Power、遗物、药水、充能球、事件、角色、遭遇 | `Cards/`、`Powers/`、`Relics/`、`Potions/`、`Orbs/`、`Events/`、`Content/`、`Encounters/`、`Ancients/` |
| 动态数字、三语、卡图与规格导出 | `NinjaSlayer/localization/`、`Docs/card-catalog.md`、`Tests/NinjaSlayer.OrbContractTests/card-metadata.json`、`tools/release/import-website-catalog.mjs.reference`、`Website/content/versions/1.0.12/catalog.json` |
| 战斗命令与时序 | `Code/Commands/`、`Code/Combat/`、`Code/Patches/`、`Docs/combat-action-timing.md` |
| 处决、取消、人物入场/复活、UI 固定 | `Code/ExternalAnimations/`、`Code/Nodes/`、`Docs/form-finisher-validation.md` |
| 原生宠物友军、意图、联机布局 | `Monsters/`、`Code/Patches/`、`Docs/heavy-layout-validation.md` |
| 事件、安全继续、问候、动态背景 | `Events/`、`Monsters/`、`Docs/boss-greetings.md`、`Docs/sawatari-completion-validation-2026-09-22.md` |
| 音频、FMOD、响度、事件语义 | `Content/NinjaSlayerAudio.cs`、`Content/NinjaSlayerCombatAudio.cs`、`tools/fmod/`、`NinjaSlayer/audio/fmod/*GUIDs.txt`、`Docs/fixed-event-music.md` |
| Godot 场景、shader、材质 | `NinjaSlayer/scenes/`、`NinjaSlayer/shaders/`、`NinjaSlayer/materials/`、`NinjaSlayer/themes/`（未复制其引用的成品图像/音频/Spine 数据） |
| 构建、依赖、通用加载器 | `eng/`、`NinjaSlayer.csproj.reference`、`tools/loader/`、`tools/release/`、`tools/artifact-contract/` |
| CI、候选证明、Smoke、包边界 | `.github/`、`tools/private-contract/`、`tools/test-build-boundaries.mjs.reference`、`Tests/`、`tools/package-contract.mjs.reference` |
| 实机隔离、后台桌面、音频/视频、剧场 JSON | `tools/smoke-harness/README.md`、`tools/smoke-harness/theater/README.md`、其余 harness 源码与编排文件 |
| 读档探针、过渡性能、GPU 调试 | `tools/save-probe/`、`tools/transition-perf/`、`tools/gpu-sentinel/` |
| Steam 多语言、元数据与校验 | `tools/workshop-metadata/`、`Workshop/`、`.github/scripts/`（只作参考，不操作旧条目） |
| 可选程序集与九模组兼容 | `Integrations/AutoAnthony/`、`Docs/compatibility-nine-mods.md`、`Code/Patches/` |
| 反馈/附件、作者回应、后台、战报、快照、网站文案 | `Infrastructure/telemetry-worker/` 的 `src/`、`dashboard/`、`scripts/`、`test/`、README 与隐私说明 |
| 经验、旧验证、模型改名边界 | `Docs/validation/`、`Docs/model-ids-0.3.8.md`、`Docs/maintenance-baseline.md`、`Docs/dependency-security.md` |
| 美术抠图、海报、动画、FMOD 编辑 | `tools/assets/`、`tools/architect-spine/`、`tools/character-select/`、`tools/naraku-event/`；外置制作脚本见 `Reference/LocalAuthoring/` |
| 原作资料索引工程 | `.agents/skills/ninjaslayer-lore/scripts/` 和其技能文档，仅可借鉴组织与检索方法；未迁移小说、语料、原作剧情索引 |

## 将参考变成 DohnaDohna 功能

先确定设计与原生接口，选择最小相关实现和行为测试；将 ID、命名空间、路径、注册、三语及宿主签名改成新项目的实际内容；重新验证再接入。
构建、发布、Smoke、网站脚本引用原项目特定目录和模型，不是可直接运行的 SDK。`.reference` 后缀应只在受审查的适配副本中去掉；不要整库去后缀或全局替换名称。
Reference 中 README/AGENTS/SKILL 是历史资料，现行规则以根 AGENTS.md 与本项目技能为准。

卡牌编辑板生成脚本来自本地临时工作，脚本仍引用历史 v1.18 HTML 路径；另附 `Reference/LocalAuthoring/card-board/v1.19-without-art.html.reference` 供提取当前交互、合并基础/升级文案、保存机制与设计备注。移植时替换模型数据、图片、标题、localStorage 键和文件名，不能直接沿用忍者杀手数据作为 DohnaDohna 内容。
`Reference/LocalAuthoring/FMOD/` 保留本机当前编辑工程的 README、fspro 和 Metadata，包括原项目事件与混音。它不是未修改的通用模板；不含 Assets/Build/.user。新项目保留宿主 Master 关系，为自己的 bank/event 重新创建唯一身份，不将忍者杀手 GUID 同时加载进游戏。
弹窗制作脚本依赖外置源帧/分割环境。这里保留算法与参数，未宣称开箱即用，也未复制图片、视频或 Python/GPU 环境。
