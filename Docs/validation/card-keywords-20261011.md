# 突前与换位固有

2026-10-11，Windows，隔离实机 Fast。实现只改变关键词与说明，不改变队列插队时点、代打规则、卡牌费用和伤害。

## 实现

- 33张近战牌使用“突前 / Advance / 前進”。通过 RitsuLib `RegisterOwnedCardKeyword` 注册，原生式正文与悬停提示共用 `card_keywords` 三语表；原有消耗、固有等词条不被覆盖。
- 原型的 `IsMelee` 仍是既定队列规则的来源，词条展示不另触发一次移动。代打保留关键词，提示中明确不触发突前。
- `SquadSwap` 增加原生 `CardKeyword.Innate`，0费与升级获得Retain不变。删除三语升级正文手写的重复“保留”，由原版词条排版生成。
- 当前生产代码没有按近战区分标题颜色的分支。两宿主的 `NCard.UpdateTitleLabel` 使用普通奶白／升级绿色文字，`GetTitleLabelOutlineColor` 使用稀有度描边；罕见牌的青蓝描边不是近战专色。保留这些原版显示规则以及角色卡框色，不增加标题Patch。
- 同步规格和176项运行时导出；导出将模组关键词反查为稳定字符串ID，而非枚举数值。编辑板将原近战开关显示为“突前”并提供说明，保留原设计数据与基线，不覆盖已交付编辑板的正文快照或用户修改。

## 验证

以下目录均相对 `build/smoke/preview/`。后台宿主/模型测试不冒称物理鼠标验收。

| 项目 | 结果／证据 |
| --- | --- |
| 真实0.111.0／0.107.1编译 | PASS；65／69条已有警告，0错误 |
| Godot导入、PCK导出、白名单 | PASS；6769条目，驱动不进入包 |
| Catalog + 新增KeywordScenarios | PASS；`command-flow-20261011-144401-548` |
| Catalog同载忍杀／东尼／BaseLib | PASS；`command-flow-20261011-144621-420` |
| NativeFlow同载回归 | PASS；`command-flow-20261011-144854-071`，虎太郎出牌弃牌、前排气泡与普通玩家对照 |
| 工程／静态参考 | PASS；1739个迁移文件 |
| 纯逻辑 | PASS；11864断言，不作为实机证明 |
| 编辑板后台Chrome | PASS；`build/authoring-proof/compact-board/result.json`，270条目、拖拽／编辑／保存重开／安全差异提取；基线未改 |

关键词定向测试覆盖全170种卡牌的canonical、mutable、复制、升级及原生序列化往返；33张近战牌与词条一一对应。检查背刺同时具有突前／固有／消耗，狂宴、狩猎、巨镰保留消耗。三语检查词条正文各出现一次、注册悬停说明、普通与升级标题的原生颜色。三组阵容真实开战均由原版将换位抽入首回合手牌；Catalog原有近战插队、阵亡代打、80种候选池和队列持久化检查继续通过。

已查看同载运行的 `localappdata/keywords-{zhs,eng,jpn}.png`，四张实际NCard分别为上勾拳、背刺+、换位、换位+；截图底部文字是测试层显示的实际HoverTip内容，不称作物理悬停操作。运行时导出与 `Docs/card-metadata.json` 逐项数据相等。

## 安装

运行宿主0.111.0（`41cef1ea`），RitsuLib编译0.5.12／运行0.6.7；同载NinjaSlayer 1.0.19、AutoAnthony 0.3.143、BaseLib v3.4.7，仅表示上述定向回归通过。

验证候选：`build/keywords/candidate/packages/preview/DohnaDohna`。

| 文件 | SHA256 |
| --- | --- |
| preview DLL | `0811acb46559b313efd258b62b608c26e490007c6e0f95942a3eb8e4008044bd` |
| PCK | `c65c8ae56038e60932468e600002fd5f5515de2015b789e4768b90d0a13ee55b` |
| stable DLL | `4650c3ec844679dcabca7ea97a4188b5fe38230e39b95d3dbe7f8bce54f59da0` |

重新核对日常宿主和所有游戏进程退出后，已备份并安装至本机Steam `mods/DohnaDohna`，安装器逐文件校验。备份／收据：`build/local-install/20261011-145044-913/`。本包同时包含上一轮已验证但未安装的虎太郎出牌与前排气泡修复。本轮没有修改正常存档、偏好、其他模组或上传。

0.107.1实机、多人、跨平台、物理键鼠：**NOT RUN**。原有Godot退出资源日志仍存在，不宣称全部日志零警告。
