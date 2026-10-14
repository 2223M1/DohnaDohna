# 预备回退、扎帕连续抡拳与普通特效停用 — 2026-10-14

## 改动范围和依据

- 以实际旧安装备份 `build/local-install/20261011-165628-577/previous-mod/DohnaDohna.dll` 核对，而非较早的 Git HEAD。29组预备的画稿、位置、影子与曝光恢复旧版：进入姿态后保持，不再全员做新增的呼吸／往复循环。
- 仅扎帕普通攻击的拖牌预备例外：采用原作前六张不同抡拳画稿，每张3/60秒，一圈0.30秒，正向循环，不在圈尾停顿。此前循环拼入原作第二圈加速及末帧延长，现已移除。
- 未拖牌的站立 idle 没有改过：十人原始动作文件与旧PCK逐项散列一致。此次回退的是预备／待命；不回退完整受击帧序、轻微击退、自然收招及原生命中前摇。提交后保留到释放点之间的连续画稿，不跳过中间姿态。
- 保留飞碟漂浮、轻微倾斜、发射后坐与受击回稳。普通动作不派发原作特效，包括菊千代路径斩光、阿熊原作枪线及安缇娜护盾；人物、武器、飞碟和STS2原生投射物／命中反馈保留。
- 处决继续使用已有原作发射／路径特效，不恢复重复命中图声。许可归本次处决及真实执行者，命中后的旧镜头尾段不能授权下一张普通牌。删除原作护盾的视觉格挡钩子与专属显示实现，不改变真实格挡。

按项目原作查证技能核对旧包及源画稿，按模组接入／代码质量技能将许可留在所属动作与处决内；没有新增公共API、存档字段或全局兼容层。

## 构建和静态检查

最终候选：`build/animation-revert/final/packages/preview/DohnaDohna`。

| 检查 | 结果 |
| --- | --- |
| 工程身份、资源和三语边界 | PASS |
| 静态参考散列 | PASS，1739项 |
| 源动作契约测试 | PASS，10项 |
| 纯状态／时间线 | PASS，17520条断言；包括六张不同画稿、等曝光及100圈边界 |
| 真实0.111.0编译 | PASS，65条nullable警告，0错误 |
| 真实0.107.1编译 | PASS，69条nullable警告，0错误 |
| PCK白名单 | PASS，6791项 |

最终文件SHA256：

```text
preview DLL  f8aef624923d0e7ed1de547fb487d2c8f7863cc6e4aa7d8d0a2695caaebed66d
stable DLL   c116bc9681de6e43d70781ee205a51e1adb350d86c1e5446119b80238e2252e2
PCK          bce4f7527f26f56e871ea26d2caea0fc89ca9ba7e6cde115a36654a32d7cd082
manifest     5a2fc90f03d23e4b07612af8c7db3f3ebaa0eb7cee73095280405a547c145858
```

编译RitsuLib基线0.5.12；隔离Windows宿主0.111.0（`41cef1ea`），运行RitsuLib0.6.7，并载本机NinjaSlayer1.1.0。最终验收与安装记录见下文，不能由编译通过推断实机通过。

## 中间候选证据（不冒充最终包）

以下路径均在本机 `build/smoke/preview/` 下，录像不随源码上传。

- `audio-flow-20261014-161209-118`：第一候选 `093698b2…` 的十人三类预备，原生手牌键盘事件进入／取消／提交，60段录像；六张抡拳循环与其余旧姿态保持已经查看。`clips/zappa-prepare-strike-commit.mp4`、`clips/zappa-prepare-strike-cancel.mp4` 可直接播放。真实捕帧50.42fps，编码60fps，不将重复编码帧当实拍。
- `audio-flow-20261014-161016-275`：第一候选的普通无原作护盾／斩光、处决仍有路径斩光；实际截图已查看。
- `audio-flow-20261014-161937-034`：第二候选 `6c55f241…` 的Pacing与9种长音效波形尾段匹配。

前两版到最终包未改变29组预备画稿和扎帕循环；最终包另补了预备到释放点的连续过渡，以及处决特效许可边界，因此仍须单独复测最终包，不能用中间候选代替。

本地旧包反射对照：`build/animation-revert/previous-preparations.json`（29项PASS）；源时间线散列：`unchanged-timelines.json`。实录扎帕逐50ms联系表：`zappa-loop-recorded.jpg`。编码帧的粗图像分类存在采样抖动，不作为“每帧精确50ms”的实测证明；等曝光是动作时间线契约，实录用于检查是否还存在圈尾刻意停顿。

## 最终候选验收

各目录仍相对 `build/smoke/preview/`。均加载上列最终DLL/PCK与忍杀1.1.0。

| 场景／目录 | 结果与边界 |
| --- | --- |
| Pacing — `audio-flow-20261014-163037-940` | PASS；40次Fast实际自动出牌、180个三速度／两档门槛、十人完整受击与连续接管；真实捕帧52.65fps，编码60fps |
| MotionFlow — `audio-flow-20261014-163558-910` | PASS；真实格挡不变、普通原作护盾／斩光停用、处决路径斩光保留；射口／球槽跟随、漂浮、暂停、死亡和退场。处决特效许可的角色、出牌作用域和已命中阶段正／负对照通过；截图已查看 |
| Finisher — `audio-flow-20261014-163703-105` | PASS；真实非致死／部分击杀／缓冲／格挡不处决、最后敌人和群攻清场处决；原生转阶段阻止Hook单独作方法探针 |
| PreparationEdges — `command-flow-20261014-163751-886` | PASS；预备中受击、队列撤销、取消途中重入、换位和退出清理；生产方法探针，不冒称物理输入 |
| Lifecycle — `command-flow-20261014-163823-248` | PASS；Normal／Fast暂停、Instant跳过、取消、外部变换恢复、声音及房间退出归属 |
| Emission — `audio-flow-20261014-163858-637` | PASS；六种原生发射样式、飞碟连续接管／球／换位、处决返回与正常原生命中反馈；普通阿熊三目标均无原作枪线，菊千代群攻不重复横斩 |
| Preparation — `audio-flow-20261014-164119-036` | PASS；十人三类动作的原生手牌键盘事件预备／取消／提交，另有早期／中段接管探针。60段静音切片与联系表在 `clips/index.html`；真实捕帧51.67fps，编码60fps |

最终Pacing进程录音的10种独立动作声同时匹配起音和450ms之后的尾段（每段相关性至少0.7，两个匹配延迟相差不超过2ms）；证据在该次 `localappdata/audio/action-audio-proof.json`，不声称所有音效或人工听审通过。

最终扎帕录像：`audio-flow-20261014-164119-036/clips/zappa-prepare-strike-commit.mp4`；取消片同目录 `zappa-prepare-strike-cancel.mp4`。已检查扎帕／阿熊／安缇娜关键联系表，最终扎帕逐50ms实录裁图另存 `build/animation-revert/zappa-final-recorded.jpg`。粗图像分类的曝光33–100ms包含重复编码帧与取样误差，不作为精确渲染帧耗证明。Pacing的连续动作／处决20段切片另在其 `clips/index.html`；切片没有音轨，真实进程录音单独保留。

日志不是零告警：仍有Godot空OrbManager Tween和退出RID／资源诊断（此前记录已有），本次Pacing另记录启动阶段 `Invalid Task ID`。保留原始日志，未完成整进程归因，不把上述功能清理断言解释为整个宿主零泄漏或零日志错误。

## 安装和公开源码

16:48重新确认日常游戏已退出、宿主仍为0.111.0后完成备份安装。目标：`C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna`；安装后的DLL/PCK/manifest逐项与最终候选SHA256一致。

备份与收据：`build/local-install/20261014-164825-693/previous-mod/`、同目录 `receipt.json`。旧安装DLL为 `4310d9925f0d6f357f56b59c6b589bce7d3559d4992b8dd8dd4d96596317705c`，旧PCK为 `b09bb74d31a4203a255b1e6d6490a12e4182cb734b2042dee39e74a976336f81`。未传入SettingsFile，不改日常存档、设置或其他模组。

本次授权的源码同步目标为公开仓库 `2223M1/DohnaDohna` 的 `main`，包含此前已完成但未同步的相关演出、原生归属、编辑板、选人界面与测试依赖。不是Release／工坊发布；排除商业素材、DLL/PCK、音频、录像、正常存档及无关 `tools/krea2` 修改。Git索引公开边界扫描和暂存差异检查是上传前的独立步骤，构建／安装本身不代表已推送。

## 未执行项目

stable实机、多人、跨平台、物理键鼠操作、人工听审：**NOT RUN**。后台原生输入事件不冒称物理鼠标；实际录音波形匹配不冒称人工听感验收。
