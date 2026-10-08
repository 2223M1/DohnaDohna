# 原生体型效果接入与固定站位 — 2026-10-07

## 需求与原因

- 缩小甲虫此前将 `ShrinkPower` 施加到隐藏的玩家宿主：既无法看见缩小，真正出牌的成员也没得到该状态的30%减伤。
- 大蘑菇 `Grow` 同样只对隐藏宿主调用 `NCreature.ScaleTo(1.5,0)`。
- 用户明确“大蘑菇加生命上限的角色放大”。现有分配规则将+20变成四人各+5，四名队员均属于获得者；保留既有HP和少抽牌规则。
- 用户随后明确：放大或缩小不能改变原先排布。队列基准宽度不再乘 `Visuals.Scale.X`，体型变化和原生重新排布回调均不得改变队列槽位/血条位置。没有保留收尾过程中尝试的动态边距调整。
- 用户补充：角色死亡后仍允许重新排布。固定站位仅约束体型变化；`ConfirmDeath` 仍调用 `Layout()`，按存活成员重算排布并接替前排，没有冻结死亡后的队列。

## 最小实现与宿主依据

- `SquadPowerTargetPatch` 的成员能力路由加入原生 `ShrinkPower`，不重写其结算、动画或移除条件。
- `SquadBigMushroomPatch` 精确注册原生 `BigMushroom.Grow()`，原生调用后为可见成员调用同一 `ScaleTo(1.5f,0)`。只影响已注册的小队，不接管全局 `NCreature.ScaleTo`。
- 原生 `CombatRoom.StartCombat` 的 `AfterRoomEntered` 早于 `BeforeCombatStart`。后者才创建四个队员，因此成员创建完成时按已有、未熔毁的大蘑菇补齐外观；不再次发放HP、抽牌或调用获得遗物逻辑。
- `SquadFormation` 按未缩放的人物基准宽度与HUD宽度排布。缩放由原生身体节点持有，影子自然继承；没有新的保存字段、全局尺寸状态或自制Tween。
- 精确参考：`Reference/Vanilla/{0.111.0,0.107.1}/src/Core/Models/Powers/ShrinkPower.cs`、`Models/Relics/BigMushroom.cs`、`Nodes/Combat/NCreature.cs::ScaleTo`；0.111.0 `Models/Monsters/ShrinkerBeetle.cs::ShrinkMove`、`Rooms/CombatRoom.cs::StartCombat`。
- 两宿主都是临时倍率替换：大蘑菇1.5 → 缩小0.5 → 解除缩小1；下一房间再应用大蘑菇1.5。本模组不改成1.5×0.5，也不在解除时另造大蘑菇叠乘算法。
- 不改卡牌数值、模型/存档身份、动作选取、普通/处决分流或音量；原生文本沿宿主，规格已同步。

## 验证

初次定向运行 `command-flow-20261007-175153-459`：真实甲虫第一回合、前排缩小、6点打击变4点、健康队员6点、后排专属9点变6点、人工制品、甲虫死亡解除、大蘑菇上限与少抽牌等 PASS。截图显示1.5倍时旧排布算法会挤压后排，故该候选没有安装；最终按用户新确认改为体型不参与排布，并增加坐标不变断言。

最终固定站位候选：Windows 0.111.0 `41cef1ea`；RitsuLib 编译0.5.12、运行0.6.6；NinjaSlayer 1.0.17 同载。

- preview DLL：`f14906309269bb9fa148412fad3448bb42f9d7e9b25245e5ec7784ed3af6bc37`。
- PCK：`4711bfb962eb22b093cd973ebdce369bae5d8712d3e236bf2a29307e370b17fd`。
- manifest：`5a2fc90f03d23e4b07612af8c7db3f3ebaa0eb7cee73095280405a547c145858`。
- 真实0.107.1编译DLL：`d9833fc45fc95b827bad8e3ccfa5bdfae85226679b8c5668b9a62787b9f0972f`。
- 双宿主各自编译0警告0错误；Godot import/export、6449条PCK白名单、工程检查、28项Python工具测试及139项纯逻辑断言 PASS。没有已提交源码基线，不虚构源码SHA。

`command-flow-20261007-175944-473` Scaling **PASS**：

- 真正结束玩家回合，让原生缩小甲虫行动，确认仅当时前排持有 `ShrinkPower`，隐藏宿主/其余三人无该状态。
- 实际出牌：缩小者6点打击→4，换位后健康者仍6，后排缩小者9点专属→6。缩放不被攻击/回位重置。
- 原生甲虫招式的人工制品拦截、施加者死亡后移除缩小和原生0.75秒回位过渡 PASS；人工制品子项直接调用原生招式，不冒称另一完整敌方回合。
- 大蘑菇战斗外获得后下一战四人1.5倍、各+5上限、原生首回合5→3抽牌修正；战斗中获得同样生效。没有替换原生获得/抽牌算法。
- 三批队伍覆盖全部十人的1.5倍与0.5倍身体/地面影子变换；体型与血条缩放分离；大体型下换位保持缩放。
- 强制重新排布后，缩小及解除前后四人槽位完全不变；战斗中获得大蘑菇前后，四人根节点及血条坐标逐一相等。
- `localappdata/scale-after-beetle.png`、`scale-big-mushroom.png`、`scale-group-*.png`、`scale-midcombat-obtain-swap.png` 为实际渲染截图。放大截图已人工查看，四条血条完整位于画面内；人物比例变化而非扩大队列间距。

`command-flow-20261007-180124-920` Lifecycle **PASS**：原生/快速/瞬间速度下暂停、取消、退出与身体/影子图层清理，外部身体缩放未被动作恢复逻辑覆盖。

`command-flow-20261007-180232-961` Full **PASS**（`DOHNA_SMOKE_COMBAT_PASS`）：30次换位、独立药水/能力、原生7×3死亡接替（下一人11血、无溢出）、死者卡禁用、缓冲/保命、复活、十人动作与两种死亡设置、商店、古人0血复活治疗、读档、群攻/异常/毒及全灭回归通过。死亡仍走存活队列的重排路径；本轮没有新增死亡排布算法。

测试只使用隔离游戏/档案及后台桌面，不使用 computer use、不抢前台。原生实际命令/怪物回合与驱动直接调用招式的子检查在测试注释中区分；不是物理鼠标验收。

stable实机、多人、跨平台、物理输入、人工音频听审：`NOT RUN`。

## 本机安装

- 最终验收后重新检查：Steam宿主为0.111.0 `41cef1ea`，游戏进程数为0。
- 已运行 `tools/Install-LocalMod.ps1`，未传 `SettingsFile`。目标为 `C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna`。
- 收据：`build/local-install/20261007-180854-232/receipt.json`；旧模组完整备份在同目录 `previous-mod/`。
- 安装后DLL/PCK散列与上文最终隔离候选一致。备份旧DLL为 `8140fe313a93781162d6fd066419346fd4e85b9fba75cea4576e62c1e3feef00`，旧PCK为 `fd92bf1340696c9ca3bfc81c4c8cb47b86b077b32fb7620e5e54d94b85d48695`。
- 未修改日常存档、设置、其他模组或全局音量，未发布上传。安装后工程检查再次PASS；未将安装复制本身声称为日常档案实测。
