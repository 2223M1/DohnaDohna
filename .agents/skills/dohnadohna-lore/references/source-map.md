# 本地原作资料定位

所有路径先基于实际 `DohnaDohna.csproj` 工程定位。外置资料根记为 `<original>`，读取工程 `.local/original-reference.json` 的 `referenceDirectory`；失效时只检查同级 `DohnaDohnaOriginal/`，不要假装找到资料或扫整个磁盘。

| 问题 | 最小入口 |
| --- | --- |
| 来源、版本与提取完整性 | `<original>/reverse/source_manifest.json`、`reverse/reports/summary.json` |
| 技能、角色、物品与配置数值 | `reverse/data/current/data.x` 与其拆分 `.x` 表 |
| 原作函数与流程 | `reverse/scripts/function_index.csv`、`functions/fNNNNN.jam` |
| 类型、变量、库接口 | `reverse/scripts/metadata.json`、`structures.txt`、`globals.txt`、`libraries.txt` |
| 消息与故事文本 | `reverse/scripts/text.txt`、`messages.txt`；结合相关函数上下文，不按同号音频直接配对 |
| UI 与场景定义 | `reverse/data/pact/`；场景定义不等同完整叙事 |
| 角色语音用途 | `reverse/data/current/11_ボイス情報.x`、`assets/audio/voice/角色用途索引.csv` |
| 可选录音文件 | `assets/audio/voice/按角色/`；表外音频在 `未关联/`，缺失引用见 `缺失引用.csv` |
| 图像、动作帧及不同版本 | `assets/manifest.csv`、`assets/images/current/`、`variants/` |
| 音乐/音效映射 | `reverse/data/current/50_音乐模式情報.x`、`51_音乐設定.x`、`reverse/reports/audio_assets.json` |

使用索引中的原始引用、输出路径和源行号，不把目录的显示名当作模型 ID。语音关联报告 `reverse/reports/voice_table_association.json` 给出当前覆盖率；不要把初始 375 个匹配或其他数量硬编码成永久事实。

研究修改器差异时才读取 `reverse/data/variants/`，同时记录对应原始表。语音表 21 以及其他表是额外资料，不得声称表 11 本身提供了其中的信息。

原作与宿主是两份不同资料：多娜多娜原作在外置 `<original>`；STS2 原版在工程 `Reference/Vanilla/<版本>/`，后者由 `dohnadohna-sts2-reference` 按精确行为问题使用。

## 窄检索示例

在 PowerShell 中先将 `$original` 设置为解析后的资料根，再执行按任务选择的查询：

```powershell
rg -n -F '爱丽丝' "$original/assets/audio/voice/角色用途索引.csv"
rg -n -F 'VoiceRouter@Play' "$original/reverse/scripts/function_index.csv"
rg -n -F '已确认的表项或字段' "$original/reverse/data/current" -g '*.x'
```

编号只在所属索引中查证；全库搜某个数字会混入方法编号、常量、消息编号和无关数值。找到相关函数后读取它及必要调用者，停止无目的扩散检索。
