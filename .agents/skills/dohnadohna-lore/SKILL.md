---
name: dohnadohna-lore
description: Establish evidence-backed Dohna Dohna character, story, terminology, mechanic, asset and voice facts for the DohnaDohna STS2 mod. Use for original-game research, character voice, naming, flavor and canon-sensitive design; not generic writing or host API implementation.
---

# 多娜多娜原作查证

借鉴 ninjaslayer-lore 的证据与改编边界，用本机原作数据、脚本和资源索引代替小说 EPUB 语料。不会要求忍者杀手小说、茶道术语或不存在的写作技能。

## 定位与职责

找到包含 `DohnaDohna.csproj` 的工程；本机默认是 `%USERPROFILE%/Documents/STS2 MOD/DohnaDohna`。先读根 `AGENTS.md` 和 `Docs/ORIGINAL-GAME-REFERENCE.md`，通过 `.local/original-reference.json` 定位外置资料，不沿用旧 DNDN 地址。

查证前按 [source-map.md](references/source-map.md) 选择最小资料入口。只有涉及人物口吻、事件、风味或命名时，再读 [voice-and-naming.md](references/voice-and-naming.md)。不默认加载整个资料库。

本技能负责原作事实和创作依据；RitsuLib 接入交给 `dohnadohna-modding`，内部结构审查交给 `dohnadohna-code-quality`，宿主原版精确行为交给 `dohnadohna-sts2-reference`。这里的“交给”是职责切换，不是自动创建子代理或新任务。

## 查证方式

- 把请求变成明确的人物、原作表项、函数、资源名或语音引用，先查现有索引再打开相关记录。
- 数值与配置优先查当前 EX 表；流程与条件查对应字节码函数；人物和剧情要读能确定说话人、前后关系与事件阶段的上下文。函数名、文件名和图片外观不是完整剧情证据。
- 重要推论核对调用者或第二个独立场景；同一段脚本的多处匹配不算多份佐证。缺少上下文就标为不确定，不从类型名或排序猜结论。
- 引用记录包含本地路径、表名/键或函数编号/地址/消息编号、原作版本或源散列；引用生成的行号时说明其所属导出。
- 区分四类结论：`原作事实`、`设计推论`、`创作补全`、`不确定`。原作事实不会自动决定 STS2 的平衡、数值或玩法。

## 语音、版本与恢复边界

- `11_ボイス情報.x` 证明角色/角色组与用途关联；角色用途索引证明引用与现有音频的对应。不把战斗受伤叫声当作完整人物口吻。
- 音频编号、消息编号、字符串编号、函数编号是不同命名空间。同号不能直接关联台词；剧情关联必须沿实际播放调用和脚本上下文追踪。
- 未关联语音不是自动漏提取；表内缺失引用也不是可随意补号的空位。检查索引与原资源包目录后报告差别。
- 当前中文脚本文本不是日语录音的逐字转写；没有实际转写证据就不提供“原日语台词”。
- `reverse/data/variants/` 是修改版本，不能当作未修改的原作设定。AIN/JAM 与伪代码是导出/恢复结果，不是官方完整源码；主程序入口桩不能代表整个引擎。
- 保留原始名称与编号；别名合并先查证，不因翻译相似就改模型 ID 或重命名素材。

## 交付与积累

事实查询返回必要证据和缺口；设计请求说明事实到设计的桥梁；创作请求优先交付用户要的文本，不强行附长篇研究报告。

可维护小型、可追溯的设计笔记，但不要为一次查询建立新语料平台。完整台词、音频、图片、程序和商业游戏导出留在外置本地资料中，不复制进技能包或公共 Git。
