# 本地原作资料定位

以下路径相对外置资料根 `<original>`：读取工程 `.local/original-reference.json` 的 `referenceDirectory`；失效时检查工程同级 `DohnaDohnaOriginal/`，仍缺失则报告，不扫整个磁盘。

## 按问题选入口

| 问题 | 最小入口 |
| --- | --- |
| 来源、版本、完整性 | `reverse/source_manifest.json`、`reverse/reports/summary.json` |
| 角色、技能、物品数值 | `reverse/data/current/data.x` 及拆分 `.x` 表 |
| 函数、条件、流程 | `reverse/scripts/function_index.csv` → `reverse/scripts/functions/fNNNNN.jam` |
| 类型与接口 | `reverse/scripts/` 下的 `metadata.json`、`structures.txt`、`globals.txt`、`libraries.txt` |
| 剧情与消息 | `reverse/scripts/` 下的 `text.txt`、`messages.txt`，结合相关函数上下文 |
| UI/场景定义 | `reverse/data/pact/`；不等于完整叙事 |
| 语音角色与用途 | `reverse/data/current/11_ボイス情報.x`、`assets/audio/voice/角色用途索引.csv` |
| 语音文件与缺口 | `assets/audio/voice/` 下的 `按角色/`、`未关联/`、`缺失引用.csv` |
| 图像、动作帧 | `assets/manifest.csv`、`assets/images/current/`、`assets/images/variants/` |
| 音乐/音效 | `reverse/data/current/` 下的 `50_音乐模式情報.x`、`51_音乐設定.x`；`reverse/reports/audio_assets.json` |

从索引取原始引用、输出路径和源行号，目录显示名不是模型 ID。编号仅在所属索引内查询，避免全库数字搜索混入其他命名空间。

## 语音与版本

- 表 11 证明角色/角色组与用途；角色用途索引连接引用和现有录音。剧情关联须沿实际播放调用与脚本上下文追踪；引用名中的日语片段不是完整转写。
- 未关联不等于漏提取，表内缺失也不是可补号空位。核对索引和原资源包目录；覆盖率取 `reverse/reports/voice_table_association.json`，不固定历史数量。
- 使用表 21 或其他资料时独立注明来源，不归功于表 11。别名合并先查证，保留原始名称与编号。
- `reverse/data/variants/` 是修改版本，只在比较差异时读取并记录对应原表。AIN/JAM 是恢复结果，入口桩不代表完整引擎；恢复限制和提取工具见工程 `Docs/ORIGINAL-GAME-REFERENCE.md`。

## 窄检索示例

先将 `$original` 设为已解析的资料根，按需选择查询：

```powershell
rg -n -F '爱丽丝' "$original/assets/audio/voice/角色用途索引.csv"
rg -n -F 'VoiceRouter@Play' "$original/reverse/scripts/function_index.csv"
```

找到相关记录后只读必要上下文；STS2 宿主资料另由 `dohnadohna-sts2-reference` 定位。
