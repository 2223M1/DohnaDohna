# 女性特殊死亡演出设置 — 2026-10-07

用户追加要求：作为游戏设置里的可选项，默认关闭；关闭时与男性使用相同死亡流程。

## 实现

- `Code/Visuals/PresentationSettings.cs` 使用已安装 RitsuLib 0.5.12 的公开 `ModDataStore`、`ModSettingsValueBinding` 和 `RegisterModSettings`。复用框架设置入口/开关/自动保存，不新增设置面板或 Patch。
- 页面：模组设置 → DohnaDohna → 演出 → 女性角色特殊死亡演出。中英日 `settings_ui.json` 同步。
- `FemaleDeathCutInEnabled=false`。偏好按 `SaveScope.Global` 存到本模组 `presentation.json`，不加入小队存档、角色模型或战斗状态。
- `RoleVisuals.PlayDeath` 在死亡入口读取一次：关闭时与男性一样，只 await 各自原作倒下动作，保留死亡语音；开启时有原作海报的六人额外 await `DeathCutIn.Show`。当前正在播放的死亡演出不因设置改变中途切断，改动从下一次死亡生效。
- 战斗外事件损血经 `SquadWorldSuccessionPatch.Present` 的独立入口也读取同一开关；关闭时不创建海报，不改变原生损血和前排接替。此入口在收尾审查中补齐，下面 e84 候选是补齐前的历史验证，不是最终安装包。
- 战斗外死亡语音使用现有 FMOD 事件及统一 −20dB，经 RitsuLib `AudioLifecycleScope.Room` 持有，到原生房间退出时清理；不在无海报的同步返回点立即 Dispose。没有新增原始音频回退或改变普通攻击/处决音频。
- 不修改死亡判定、原生保命、卡牌禁用、无溢出接替、男性效果或任何音量。与本轮影子修正一起安装。

## 验证记录

战斗内设置候选：0.111.0 / 0.107.1 实际输入分别编译，0警告0错误；Godot/PCK白名单6448条 PASS。DLL `e84de509de05c882eeb88c9e67a3bea44e0e9c18c9a93dce4e748435dc9e25b1`，PCK `fd92bf1340696c9ca3bfc81c4c8cb47b86b077b32fb7620e5e54d94b85d48695`。

测试驱动扩展 `Presentation`：使用真实框架设置页，经 Godot 鼠标输入链切换并检查自动落盘，不直接调用设置回调/Save 来伪造 UI 保存；再对十人各运行关闭与开启两种死亡情形，保留逐段必要等待断言。输入注入不是物理鼠标验收。

测试端两次失败有保留：`audio-flow-20261007-165757-426` 缺少导航 pageId；`audio-flow-20261007-165938-297` 已完成开关和自动保存，但错误弹出空的主菜单栈。分别改为使用注册页实际 ID、原生返回键关闭框架宿主；没有为测试改生产设置逻辑。

`audio-flow-20261007-170159-599` 实机 PASS：设置页面默认关闭，Godot 鼠标输入开启/关闭并自动保存，返回键正确关闭框架宿主。十人 × 两种设置全部死亡检查通过；关闭时六名女性均无海报（约754–769ms完成原生伤害/倒下流程），开启后六人均有海报（约1776–1804ms），男性始终无海报。`Verify-CapturedAudio.py` 识别20次实际死亡语音，十人均覆盖，最低波形相关性0.2756（阈值0.15）；不能改称人工听审。

同一 e84 候选追加回归：`command-flow-20261007-170437-558` 的十人 × 开关双状态 MovieWriter 录制 PASS，20段导出已加入 `build/motion-comparison-final-20261007/index.html`；页面的海报关键帧明确标为开启状态。`command-flow-20261007-170549-782` 影子像素测试 PASS，修复层与隐藏影子差异0像素，旧升层正对照96像素；600帧墙钟中位16.6661ms、P95 16.6844ms、最大17.0617ms，不是影子增量GPU耗时。`command-flow-20261007-170640-680` Full PASS。

战斗外追加首次验证 `audio-flow-20261007-171741-319`（DLL `51f86180a3f0051c3e57f14912b449ecde17621907b48c9473292994ad0953f1`）：设置 UI、20次战斗内及20次战斗外的海报开关/死亡接替均 PASS，**声音验证未通过**。40个死亡语音中，战斗内20个均匹配；战斗外仅海报开启的6名女性匹配，关闭海报的10人及开启时的4名男性未匹配。查已安装 `AudioHandleBase.Dispose` 确认会停止播放，原世界入口的 `using var voice` 在无海报时立即结束。最终改为房间生命周期持有；测试每次世界死亡后额外留2秒录音分隔（只在驱动中），并断言真实换房后20个句柄全部释放。该失败包没有安装。

## 最终候选

- 实际宿主 Windows 0.111.0 `41cef1ea`；RitsuLib 编译0.5.12、运行0.6.6；NinjaSlayer 1.0.17 同载。
- preview DLL：`8140fe313a93781162d6fd066419346fd4e85b9fba75cea4576e62c1e3feef00`。
- preview PCK：`fd92bf1340696c9ca3bfc81c4c8cb47b86b077b32fb7620e5e54d94b85d48695`。
- manifest：`5a2fc90f03d23e4b07612af8c7db3f3ebaa0eb7cee73095280405a547c145858`。
- 真实0.107.1编译 DLL：`ef0b62ac41c03c35529970071411d4869a7c6e30e573d1e1154004f2a01e16f3`；未将preview包冒称双宿主通用包。
- 两次独立宿主编译均0警告0错误；Godot/PCK白名单6448条 PASS；工程检查、28项Python工具测试、139项纯状态/时间线断言、1739个静态参考散列检查 PASS。没有已提交源码基线，不虚构源码 SHA。

`audio-flow-20261007-172317-201` 使用上述最终包：

- 真实设置页默认关闭，Godot GUI输入切换开启/关闭并自动保存，返回键退出 PASS。截图 `localappdata/settings-death-default-off.png` 已检查，标签和说明完整。这不是物理鼠标验收。
- 十人 × 开/关 × 战斗内/外，共40次真实原生损血死亡，海报资格/死亡等待/前排接替 PASS；世界损血用原生 `CreatureCmd.Damage`，没有直接调用海报函数替代全流程。
- 世界死亡之后存活接替者均25血；原生换房后20个世界死亡声音句柄全部释放 PASS。
- 同进程loopback录音 `Verify-CapturedAudio.py` 的40个死亡语音全部匹配，覆盖十人，最低相关性0.2752（阈值0.15）。这证明输出存在，不是人工听审。

最终包 Full：`command-flow-20261007-172628-087` PASS，含30次换位、药水归属、7×3逐段无溢出（接替者11血）、死者卡/复活恢复、原生缓冲和保命、十人动作/死亡、火堆原生选择、古人零血复活治疗、原生存读档、群攻/异常/毒及全灭收尾。与前述设置/录音定向检查分别记证，不将驱动命令当作物理输入验收。

## 本地安装

2026-10-07 17:31（本机时区）重新核对日常宿主仍为0.111.0 `41cef1ea`，日常和隔离游戏进程均已关闭。执行现有 `Install-LocalMod.ps1`，没有传 `SettingsFile`，只安装本模组三个包文件及 `SHA256SUMS`，安装后散列与最终候选一致。

- 目标：`C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna`。
- 收据：`build/local-install/20261007-173139-918/receipt.json`。
- 可恢复旧版：`build/local-install/20261007-173139-918/previous-mod/`（旧DLL `f5b70005ed5c874be397b9a3abdd69d57e9ed51c67fba4d39330a8d6bc862cbb`，旧PCK `55a5ce7eb7a6558decefb93c03f96935ba88d2cc6e817742282d79a307d68019`）。
- 没有写日常档案或偏好、其他模组、全局音量，没有上传发布或强制结束日常游戏。未向日常目录复制测试驱动或测试设置。

stable实机、多人、跨平台、物理鼠标、设置跨进程重启及人工听审 `NOT RUN`。安装后未自行启动日常游戏；上面实机结果来自同散列的隔离安装。
