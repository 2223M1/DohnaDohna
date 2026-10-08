# 反伤归属、原生反馈与构图锚点（2026-10-07）

## 变更与依据

- 两宿主 `Core/Models/Powers/ThornsPower.cs::BeforeDamageReceived` 已把反伤目标设为真实 dealer，属性为 `Unpowered | SkipHurtAnim`。删除 Damage 入口按“来源是敌人”把指定队员改成前排的规则；只解析隐藏宿主。
- 两宿主 `AttackCommand.GetPossibleTargets()` 均为无参、返回 `IReadOnlyList<Creature>` 的私有方法。原生 Execute 每段先取得该列表，再调用 HitSfx、自定义 HitVfx、通用 HitVfx、BeforeDamage、Damage。安装 RitsuLib 0.5.12 的 AttackHitHook 只包裹后面的 Damage，晚于特效；删除旧 `IAttackHitHookListener/BeforeAttackHit` 重定向，改用功能附近的精确 Postfix。没有复制原生攻击循环。
- 原生火花、数字、破盾声不在身体 `TriggerAnim` 内；不删除或手工补播这些反馈。通用 VfxCmd 和 NWormyImpactVfx 仅解析隐藏宿主。LeafSlimeM 的原生投射物标记在首个 await 前设置；局部 Postfix 只改该标记，不把加入 Slimed 牌的 Player 目标改成宠物。
- 原作 `PlayerActionView.SetPos`（f33555）把同一个位置交给身体与特效生成器；`EffectViewCollection.SetFrame`（f33841）在生成时捕获位置；`FrameLayerImages.SetPosition`（f33878）只增加原作阵列位移；`EffectView.CreateCg`（f33833）使用 MM 原点。原作没有按每张图片的 X/Y 分别朝目标扭曲构图。
- 导入器为已选动作增加内部 actionAnchorX/anchorBinding：演员构图共用平移；阵列装备/飞碟/充能/光束共用一个变换；明确命中层另取目标锚点。保留图层相对位置、镜像、时序和图像比例。绑定是 STS2 距离/体型适配，不是冒称原作字段。原作表33 SHA256：`f6533f4523960ba6024624be335d6611da9d3527e669a5b14c48319fa89bfa98`。
- 隔离逐帧检查发现第二个错位来源：原生 ScreenShake 移动 SceneContainer，旧 TopLevel 特效留在屏幕坐标。特效现在生成于场景坐标，随场景震屏但不随角色后续动作移动；不新增全局兼容层或公共 API。
- 普通攻击不捕获或写入敌人身体/闪色/震动。处决仍受原有清场预测限制；正常完成且目标存活时，残余目标位移由该次演出拥有的 200ms 视觉回位处理。死亡、取消、退出释放本次修改，尊重宿主重新挂接的身体。
- 没有修改卡牌数值、动作选择、模型/存档身份；模组音频仍统一 −20 dB，原生音量不改。

## 验证

环境为 Windows 隔离 0.111.0（`41cef1ea`），RitsuLib 实际运行 0.6.6，NinjaSlayer 1.0.17 同载；编译基线为 RitsuLib 0.5.12。工程仍是用户未提交工作区，不捏造源码提交 SHA。

- preview 0.111.0、stable 0.107.1 各自真实输入构建：0警告、0错误；stable 不算实机。
- 工程检查、139项纯状态/时间线断言、6项动作资源契约（70个已用动作）、14项原作工具测试通过。1739份静态参考散列未变。
- Godot import/export 与 PCK 白名单通过：5344条目；不含测试驱动、参考资料或宿主 DLL。
- Alignment `command-flow-20261007-130309-094`：12组前/后排换位、strike/special、矮小/高大/浮空目标及三种窗口尺寸请求；实际渲染持续检测飞碟与光束原作相对位置，包含命中震屏；全部通过。窗口请求1600×900、1280×960、1920×810，宿主保持自己的视口缩放/信箱规则，不将截图尺寸冒称窗口尺寸。
- Motion `command-flow-20261007-130524-770`：十人strike/special/cast与受击、原作图层组内坐标、命中次数、普通攻击不取得敌人身体控制、动作后恢复均通过；检查了对应渲染截图。
- Feedback `command-flow-20261007-130834-937`：195个镜头运动帧在场景界内，HUD/队列布局不变；荆棘反杀普通及最后敌人后，外层真实受击继续；房间退出清理通过。
- Finisher `command-flow-20261007-130922-974`：非致死、部分击杀、缓冲、格挡、单体清场、无视格挡、群攻清场、原生结束阻止Hook与活力预览通过。
- Lifecycle `command-flow-20261007-131044-306`：Normal/Fast/Instant暂停恢复、声音、取消、受击插入、回位中取消、战斗退出与外部缩放保持通过。
- Full `command-flow-20261007-132039-259`：完整小队回归得到 `DOHNA_SMOKE_COMBAT_PASS`；敌方群攻使用真实 AttackCommand 验证，未以显式队员损血模拟它。
- Routing `audio-flow-20261007-132554-466`：后排反伤独立格挡/缓冲、群攻、显式归属、SkipHurtAnim、妖精保命7血、反伤死亡、最后敌人互杀均通过，前排不变。7×3为front/next/next，下一人11血；真实TwigSlimeS.TackleMove、Axebot.OneTwoMove与LeafSlimeM.StickyShotMove的原生反馈通过。200ms存活目标视觉回位观察到12个中间位置，最终恢复；该项是视觉专用夹具，不冒称实际卡牌处决资格测试。

### 实际录音证据

最新Routing档案 `localappdata/audio` 包含隔离游戏进程loopback录音、事件时钟及两份证明JSON。48kHz，零采集不连续，不是整个桌面录音。

- `Verify-CapturedAudio.py`：13次配音波形匹配通过，最低相关度0.3549；探针读取模组事件实际gain=0.1（−20 dB）。本次配音样本覆盖阿熊、爱丽丝、安缇娜、虎太郎，不冒称这份录音独自覆盖全部十人。
- `Verify-NativeHitAudio.py`：真实Tackle录音里原生攻击声与破盾声各调用一次，原生volume=1；原生攻击独立起音窗口0.227秒，RMS=0.0002058、peak=0.0028108，之前0.18秒录音为静音。没有用角色配音替代原生攻击输出证明。
- 同一原生破盾命令的音源隔离正对照：仅允许原生block_break，无模组声音，录得RMS=0.0027061、peak=0.0391255；关闭该one-shot的负对照RMS/peak均为0。门控仅存在隔离测试驱动中，不在成品DLL/PCK内、不改玩家或全局音量设置。
- 正常Tackle混合录音RMS=0.0102755；真实Axebot两段斩击录音RMS=0.0059532，原生招式攻击声一次（不是每段补播）。原生事件计数与宿主招式一致。
- 音频验证工具另通过7项正/负测试，确认静音、带噪负对照、重复声音、错误原生音量、模组声音混入隔离段及全桌面录音不能冒充通过。

上述是隔离游戏真实渲染和原生命令驱动，不是物理鼠标验收。原作依据为表与控制器，不声称新增了原作实机完整录像。

## 失败记录的处理

- `audio-flow-20261007-124848-137`：测试构造器在 FromMonster 后又指定 Targeting，触发原生拒绝；修正测试构造，不改生产攻击循环。
- `command-flow-20261007-125246-298` / `125619-666`：持续连接断言抓到原生震屏时几像素漂移，促成上述场景归属修复；不能算最终通过。
- `command-flow-20261007-131151-867`：旧Full把显式队员 Damage 列表当作敌方AOE，与新批准归属规则冲突。改为真实敌方 AttackCommand 重测，不恢复错误的全局重定向。
- `audio-flow-20261007-125051-144`：基础Routing命令通过；8次角色配音波形匹配通过，但单一破盾基准片段相关度未达门槛，不能单凭调用或这份录音宣称破盾声音通过。后续完整音频结果另列。
- `audio-flow-20261007-131756-861`：完整Routing及13次配音通过，原生随机音色的固定模板/变速模板仍未通过。没有降低相关度门槛或将失败改写成PASS；后续改用上述可隔离音源的实际输出正/负对照，并保留这次失败记录。

## 候选文件

| 文件 | SHA256 |
| --- | --- |
| preview DLL | `f5b70005ed5c874be397b9a3abdd69d57e9ed51c67fba4d39330a8d6bc862cbb` |
| preview PCK | `55a5ce7eb7a6558decefb93c03f96935ba88d2cc6e817742282d79a307d68019` |
| preview manifest | `5a2fc90f03d23e4b07612af8c7db3f3ebaa0eb7cee73095280405a547c145858` |
| stable DLL（仅编译） | `ef9458f199686fa112864b4a25f66b51857271f5abaf63f61a95b5c99875edfa` |

## 本地安装

最终Routing、录音证明和Full回归通过后，重新确认本机Steam宿主为0.111.0（commit `41cef1ea`），无SlayTheSpire2运行进程，再执行 `tools/Install-LocalMod.ps1`，未传SettingsFile。

- 安装目录：`C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna`。
- 旧模组备份：`build/local-install/20261007-133440-742/previous-mod`，可恢复。
- 收据：`build/local-install/20261007-133440-742/receipt.json`。
- 安装后DLL/PCK与上述已验收preview候选SHA256一致。测试驱动与音频门控不进入运行包。
- 未改日常存档、设置、其他模组或全局音量；未上传发布。安装后没有启动日常档案来冒充隔离验收。

NOT RUN：stable实机、本轮物理输入、多人、macOS/Linux、原作完整录像对照、人工听感评审。仍存在宿主退出时的Godot资源诊断，不宣称整个进程零泄漏。
