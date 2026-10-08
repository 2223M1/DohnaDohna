# DohnaDohna
Read and follow `AGENTS.md`, then `Docs/ENGINEERING.md` and `Docs/REFERENCE-MAP.md`.
The active project is DohnaDohna. `Reference/` contains inert historical examples, not runtime content or live deployment instructions.
Do not add AI co-author trailers to commits. No GitHub remote or Workshop item is configured for this project.

## DohnaDohna skills — read the actual files

本工程当前技能正文唯一来源是 `.agents/skills/`。`.claude/skills/dohnadohna-*/SKILL.md` 是 Claude Code 的发现入口，不是完整技能正文。触发后必须用文件读取工具读完对应正文，再按其路由读取本任务需要的参考；不得只读入口或名称就声称使用了技能。

| 任务 | 必须读取的技能正文 |
| --- | --- |
| 原作动作、坐标、素材、声音依据 | `.agents/skills/dohnadohna-lore/SKILL.md` |
| 状态归属、视觉生命周期、重构 | `.agents/skills/dohnadohna-code-quality/SKILL.md` |
| RitsuLib、Godot、FMOD、构建接入 | `.agents/skills/dohnadohna-modding/SKILL.md` |
| 公开API不足以解释的精确宿主时序 | `.agents/skills/dohnadohna-sts2-reference/SKILL.md` |

上述技能正文内的 `references/`、`scripts/` 路径相对于其 `.agents/skills/<技能名>/`，不是 `.claude/skills/`。NinjaSlayer/旧 STS2 名称入口仅保留作历史参考，不代替 DohnaDohna 的现行技能。

## 当前演出与后续开发

2026-10-08 用户已改为要求 Codex 直接实施，范围包括普通攻击、处决攻击、原地受击和连续出牌衔接。现行规则见 `Docs/squad-spec.md`，本次实现/验证/安装状态见 `Docs/validation/attack-pacing-20261008.md`。旧 Claude 交接只供定位历史代码，不沿用其中“处决不可压缩”等旧限制。

普通攻击和处决都采用短动作，命中逻辑 await 原生命令，纯视觉收势由各角色持有，可替换并在退出时清理。命中图声统一由原生命令生成；本版不调用攻击人物语音，保留受击/死亡语音，模组声音仍 −20 dB。不要恢复普通攻击的原作命中特效叠加或长往返。

保持四实体归属、真实攻击者反伤、逐段前排死亡接替、存档身份、女性死亡海报默认关闭、影子地面层级、体型变化不改站位和外部缩放。后续变更先读取相关技能正文，不能仅凭入口存在就声称已使用。不得将命令驱动、固定帧录屏或波形检测冒称物理输入、墙钟性能或人工听审。
