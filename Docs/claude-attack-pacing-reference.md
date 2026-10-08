# 攻击节奏交接：按需查阅的代码与证据

配套任务：`claude-handoff-attack-pacing-20261007.md`。本文件是2026-10-07工作区的静态定位，不是新节奏已实现/已实测的报告。先读任务，不必一次读完所有历史报告或原作库。

## 1. 当前慢在哪里

生产链：`Cards/SquadCards.cs::SquadAttackCard` → `Code/Squad/SquadActions.cs::Attack` → `RoleVisuals.PlayAttack/Play` → 唯一正伤害节点 `ResolveHit` → 原生 `AttackCommand.Execute` → 附加效果 → 原作余下帧/停顿/回位 → 返回卡牌。

- `SquadActions` 使用 `WithNoAttackerAnim()`，真实攻击者设为该队员；普通攻击由 `ConfigureNativeImpact` 配好原生命中图声，处决预测由 `SquadFinisher.CanFinish` 负责。不要在视觉重构时改变这些结算归属。
- `Code/Visuals/RoleVisuals.cs::Play` 按原作累计时间跑完全部帧；命中回调被 await；`Damage.EnableHitStop` 另等；末帧非零 `OffsetX` 还进入 `PlayReturn`。仅删最后一个等待不足以对齐宿主。
- `PlayReturn` 真正播放 `return` 跳回素材，持续时间为 `(400 + .8 × clamp(abs(offset),0,1000)) / 1000` 秒，不是一个简单位移Tween。
- `PlayHurt` 是角色持有的可替换视觉轨道，不阻塞原生扣血；`_Process` 在 `hit` 结束且有偏移时进入 `BeginHurtReturn`。原地受击需同时处理源帧内的身体位移和此回跳入口。
- `PlayFrameAudio` 已关闭普通 `strike/special` 及其回位的人物语音；独立受击/死亡配音走 `PlayReactionVoice`，受击是严格 `last + 300 < now` 毫秒，死亡覆盖它。

以下秒数直接由当前 `DohnaDohna/roles/<id>/motions.json` 的60Hz帧累计而来。格式为 **命中点 / 主体时长**；不含原生命令耗时、额外hit-stop、回位，也不等于实测出牌耗时。

| 角色 ID | 打击 | 特殊 | 受击主体 |
| --- | --- | --- | --- |
| kuma 阿熊 | 1.233 / 1.700 | 1.267 / 2.517 | 0.850 |
| alyce 爱丽丝 | 0.483 / 1.367 | 3.217 / 5.850 | 0.867 |
| antena 安缇娜 | 2.167 / 3.600 | 2.217 / 3.650 | 0.800 |
| tora 虎太郎 | 0.617 / 1.283 | 1.233 / 2.150 | 0.900 |
| kikuchiyo 菊千代 | 1.367 / 2.167 | 1.817 / 2.667 | 0.850 |
| medhico 梅蒂可 | 1.533 / 3.133 | 1.533 / 3.000 | 0.933 |
| joker 小丑 | 1.167 / 1.800 | 1.733 / 2.700 | 0.817 |
| zappa 扎帕 | 1.050 / 3.017 | 0.650 / 1.950 | 0.833 |
| kirakira 绮菈绮菈 | 0.433 / 1.200 | 0.683 / 1.083 | 0.867 |
| porno 珀尔诺 | 1.500 / 2.617 | 1.733 / 3.150 | 1.183 |

## 2. 原生时序的最小查证链

`Reference/Vanilla/{0.111.0,0.107.1}/src/Core/` 为对应真实宿主导出，不是可编译模板。

| 问题 | 文件/成员与已查事实 |
| --- | --- |
| 普通攻击 | `Models/Cards/StrikeIronclad.cs::OnPlay` 使用原生攻击命令，不等待整段人物素材完播 |
| 起手至命中 | `Models/Characters/Ironclad.cs::AttackAnimDelay` 两宿主均0.15秒；0.111.0五个正式原生角色均为0.15秒 |
| 快速模式 | `Commands/CreatureCmd.cs::TriggerAnim` 经 `Cmd.CustomScaledWait(min(waitTime*.5,.25),waitTime)`；0.15对应Fast 0.075，而非所有等待统一倍速 |
| 命中顺序 | `Commands/Builders/AttackCommand.cs::Execute`：BeforeAttack → 每段确定有效目标 → 攻击者动画 → HitSfx/指定等待/HitVfx → await Damage → AfterAttack。不要复制循环或提前执行伤害 |
| 整张牌节奏 | `GameActions/PlayCardAction.cs` → `Models/CardModel.cs::OnPlayWrapper`：资源、Hook、重复出牌和牌堆处理仍由宿主持有。0.111.0卡牌尾部有 `CustomScaledWait(.15-elapsed,.3-elapsed)`，不是再固定加.3秒；自动出牌另有等待 |
| Instant | `Commands/Cmd.cs::Wait/CustomScaledWait` 跳过相应等待，仍不能跳过或重复逻辑与必要死亡处理 |

0.15秒只是起手动画等待；0.3秒也不是所有出牌的总时间。用同一真实宿主、相同速度模式和可比效果测量：开始执行→实际扣血、开始执行→整张牌逻辑完成、下一张开始执行、视觉结束；分别报告，不以手牌动画开始/能够排队冒称下一张已结算。截图/磁盘写入不能放进计时区间。

## 3. 坐标和状态的易错点

- `MotionTimeline`：累计源时间、逐个事件边界；原作 `Quad` 是四次方，`Jump` 是阶跃，不是抛物线。不要为普通版剪辑篡改处决所用的原数据或通用原作语义。
- `RoleVisuals.DrawFrame/OriginalToGlobal/DrawShadows` 与 `MotionPlacement`：图片原点、身体/装备共同构图、阵列全局位置、目标锚点分开处理。`OffsetX` 是动作末端回位元数据，不是每帧平移量。原地化不能只清零该字段；也不能将每张图分别归零或按每帧包围盒居中，造成枪口/手/飞碟/光束散架。
- `RoleMotionEffects.AddFrame/Advance/PlaceEmissions`：附着构图与生成后固定的发射特效不是同一归属。连发可以复用机器人/武器姿态，但每张牌的独立发射/命中特效仍各生成一次并各自结束。
- 目前 `_action`/`_primaryEffects` 是单次主动作，`_hurt` 是独立反应轨道。连续动作复用是**待新增的视觉行为**，不能把现状声称为已支持；由角色持有最小必要状态，不做全局动画调度平台，不保存进跑局。用户已确认按单次自然收势，不额外设驻留计时或连击等待窗口。
- 同角色新动作可替换已完成逻辑的视觉尾段；不能取消上一张仍await中的伤害/抽牌/治疗等。旧尾段结束也不能把新动作的位置、图层、缩放、声音清掉。
- `Code/Patches/SquadAnimationPatches.cs::SquadHitCastPatch` 只替换身体Hit/Cast；原生VFX/SFX保留。`SkipHurtAnim` 的荆棘不额外身体受击，真正死亡另走 `PlayDeath`。
- `SquadFormation` 只依存活队列与未缩放宽度排布。角色缩放不得改槽位，换位/死亡仍可改；演出位置不是逻辑前排和攻击距离。

## 4. 现有素材与测试入口

原作根取 `.local/original-reference.json`；当前同级 `../DohnaDohnaOriginal/`。先用导入资源和现有证据，只有具体片段/控制器疑问才按lore技能查源；不要重提取整个商业游戏。

- `Content/RoleDefinition.cs`：稳定ID、已批准的打击/特殊原动作。
- `tools/Import-OriginalRoles.py`：原动作、身体/装备/impact分类、影子及锚点导入。
- `Tests/DohnaDohna.SmokeDriver/VisualMotionScenarios.cs`：Motion/Alignment、构图、实际viewport帧marker。其中有**直接播放器调用**，不能替代真实卡牌时序测试。
- `ImpactScenarios.cs`：普通版真实出牌与处决播放器图声对照；真实处决资格另由Finisher检查。
- `VisualLifecycleScenarios.cs`：暂停、速度、取消、退出、外部变换。`ScalingScenarios.cs`：原生缩小/大蘑菇与固定站位。
- 旧测试的“普通攻击等于完整原作每一帧/全部发射次数”若与新剪辑冲突，要改成已批准的普通版契约，并继续保留处决原版契约。不能仅删除失败断言或放宽到无实际意义。

后台隔离运行，不用computer use、不切前台、不并发启动游戏。先读 `Docs/BUILD-RELEASE.md`；常用命令（PowerShell 7，项目根）：

```powershell
node tools/verify-project.mjs
dotnet run --project Tests/DohnaDohna.LogicTests
& ../Tools/STS2/python-tools/Scripts/python.exe -m unittest discover -s tools -p 'test_*.py'
pwsh -File tools/Build.ps1 -Channel preview
pwsh -File tools/Build.ps1 -Channel stable
pwsh -File tools/New-Package.ps1 -Channel preview
pwsh -File tools/Invoke-IsolatedBoot.ps1 -CommandScenario Motion -CaptureMovie -Seconds 600
pwsh -File tools/Invoke-IsolatedBoot.ps1 -CommandScenario Impacts -CaptureAudio -Seconds 600
pwsh -File tools/Invoke-IsolatedBoot.ps1 -Probe -Rendered -Seconds 600 -AdditionalModDirectory 'C:/Program Files/steam/steamapps/workshop/content/2868840/3776911445'
```

最后一条为与本机NinjaSlayer同载的Full。其他定向用 `-CommandScenario Alignment/Routing/Finisher/Feedback/Lifecycle/Scaling`，每次传一个真实值，串行运行。新增时序/连击场景需先实现和注册，不假装已有 `Pacing` 选项。

`-CaptureMovie` 是真实宿主MovieWriter固定60Hz模拟录屏；**不能用来证明墙钟出牌速度或FMOD听感**。真实录音另用 `-CaptureAudio`。切片工具 `tools/Export-MotionClips.py`、对照工具 `tools/Build-MotionComparison.py`；参数先查各自入口。FFmpeg/Python/Godot路径见 `../Tools/STS2/toolchain.json` 与 `.local/hosts.json`，不新装整套工具。

## 5. 最新已安装基线（不是新任务PASS）

- Windows STS2 0.111.0 `41cef1ea`；0.107.1仅编译通过。
- RitsuLib编译0.5.12、运行0.6.6；NinjaSlayer同载1.0.17。
- 收据 `build/local-install/20261007-180854-232/receipt.json`；备份同目录 `previous-mod/`。
- DLL `f14906309269bb9fa148412fad3448bb42f9d7e9b25245e5ec7784ed3af6bc37`。
- PCK `4711bfb962eb22b093cd973ebdce369bae5d8712d3e236bf2a29307e370b17fd`。
- 最新报告 `Docs/validation/native-scaling-20261007.md`。旧定位/图层问题才读 `idle-alignment-20261007.md`；命中政策读 `impact-policy-20261007.md`；反伤归属读 `hit-routing-20261007.md`，不要先把全部历史报告塞入上下文。
- 本工程有大量未跟踪的用户源码与资源，无已提交源码基线；不要git clean/reset，不虚构提交SHA。
