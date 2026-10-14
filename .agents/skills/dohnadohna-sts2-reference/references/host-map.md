# 精确宿主导出定位

以下路径相对 `DohnaDohna.csproj` 所在工程：

- 目标版本与编译条件：`eng/DohnaDohna.Hosts.props`。
- 真实程序集输入：`.local/hosts.json`。
- 私人导出：`Reference/Vanilla/<gameApiVersion>/src/`。
- 来源与范围：`Docs/VANILLA-SOURCE.md`、`Reference/Vanilla/source-manifest.json`。

## 按行为定位

下表路径相对目标版本的 `src/`，是检索入口，不是整目录阅读要求。

| 行为 | 入口 |
| --- | --- |
| 卡牌、Power、遗物、角色等模型 | `Core/Models/` 对应子目录 |
| 伤害、抽弃牌、召唤、结束战斗 | `Core/Commands/` 及相关调用者 |
| 战斗历史、最终实例数值 | `Core/Combat/History/`、`Core/ValueProps/` |
| 节点、动画、视觉反馈 | `Core/Nodes/` 对应类型 |
| 音频 | `Core/Audio/`、`gdscript/audio_manager_proxy.gd` |
| 序列化 | 当前模型及实际引用的转换器、上下文、存档类型 |

布局变化时从实际调用定位符号；不要猜路径或用相邻版本补缺口。

## 查询与完整性

```powershell
[xml]$hostConfig = Get-Content -LiteralPath eng/DohnaDohna.Hosts.props -Raw
$hostConfig.Project.PropertyGroup | Select-Object Condition,DohnaDohnaGameApiVersion
```

确定目标版本和符号后，在对应目录用 `rg -n -F` 限定 `*.cs` 检索。导出新增、更换或完整性存疑时运行 `python tools/verify-vanilla.py`，更新证据定位与源散列。

完整性校验不等于行为验证；导出不能替代真实程序集签名。多娜多娜原作资料由 `dohnadohna-lore` 定位，两者的编号体系互不通用。
