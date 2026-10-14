---
name: dohnadohna-lore
description: Research Dohna Dohna canon, naming, voice and asset evidence for this mod. Use for lore-dependent design and writing; not STS2 host behavior or API integration.
---

# 多娜多娜原作查证

工程路径相对含 `DohnaDohna.csproj` 的根目录；遵守根 `AGENTS.md`，已读内容不重复加载。

按 [source-map.md](references/source-map.md) 定位外置资料和本次问题的最小入口；人物口吻、命名或剧情创作再读 [voice-and-naming.md](references/voice-and-naming.md)。

## 查证与判断

- 将问题收窄到人物、表项、函数或资源引用，先查索引，再读记录及必要上下文。文件名、函数名和图片外观不能独立证明剧情。
- 重要推论核对调用者或独立场景；同一段脚本的重复匹配不算多份证据。缺少说话人、阶段或上下文时标为不确定。
- 区分 `原作事实`、`设计推论`、`创作补全`、`不确定`；原作数值和主题不直接决定 STS2 平衡。
- 证据保留路径、表项/函数/消息定位及原作版本或源散列；生成行号须注明所属导出。
- 语音、消息、字符串、函数编号分属不同索引，同号不能配对。用途表不证明逐字台词，中文文本不等于日语录音转写；具体关联规则见 source-map。

## 交付

事实查询给结论、短证据和缺口；设计说明事实如何支持改编；创作优先交付文本，不强加研究报告。完整语料与商业资源留在外置资料包，只积累必要、可追溯的设计笔记。

需要实现时用 `dohnadohna-modding`；查 STS2 宿主行为用 `dohnadohna-sts2-reference`。

历史适配来源：`ninjaslayer-lore`。
