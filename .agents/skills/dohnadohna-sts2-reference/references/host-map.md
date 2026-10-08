# 精确宿主导出定位

以 `DohnaDohna.csproj` 工程为根。当前主机配置来自 `eng/DohnaDohna.Hosts.props`，真实程序集输入来自 `.local/hosts.json`；不要使用忍者杀手旧 `eng/compatibility.json` 作为本工程配置。

私人导出位于 `Reference/Vanilla/<gameApiVersion>/`，其代码通常在 `src/`。先按任务需要读取 `Docs/VANILLA-SOURCE.md` 与对应源清单；不是公开源码包，也不进入公共 Git、编译或 PCK。

| 精确行为 | 对应导出内的窄入口 |
| --- | --- |
| 卡牌/Power/遗物/药水/角色/卡池 | `src/Core/Models/` 对应子目录 |
| 伤害、抽弃牌、召唤、结束战斗顺序 | `src/Core/Commands/`、相关调用者 |
| 战斗历史/最终实例数值 | `src/Core/Combat/History/`、`src/Core/ValueProps/` |
| 节点、动画与视觉反馈 | `src/Core/Nodes/` 与精确命名类型 |
| 音频 | `src/Core/Audio/` 与 `src/gdscript/audio_manager_proxy.gd` |
| 序列化 | 本次请求所指的模型、转换器、上下文或存档类型 |

这些是定位提示，不是读取整目录的要求。导出布局变化时以实际匹配符号为准，不伪造路径或代码。

## 查询示例

先从 XML 配置取得目标版本，再针对已知标识检索：

```powershell
[xml]$hosts = Get-Content -LiteralPath eng/DohnaDohna.Hosts.props -Raw
$hosts.Project.PropertyGroup | Select-Object Condition,DohnaDohnaGameApiVersion
# 将实际目标版本和已确定符号代入，不用这些示例字符作字面查询。
rg -n -F '已确定的类型或成员' 'Reference/Vanilla/目标版本/src' -g '*.cs'
```

原版导出完整性由 `python tools/verify-vanilla.py` 校验。更换导出后更新证据定位与源散列；相邻版本、忍者杀手历史参考和同级旧包不能无声替代当前目标输入。

多娜多娜资料根来自 `.local/original-reference.json`，两者版本命名与编号体系不同。不要将 AIN 函数、语音编号或原作 shader 当作 STS2 成员或稳定接口。
