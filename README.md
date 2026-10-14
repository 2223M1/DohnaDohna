# DohnaDohna · 杀戮尖塔 2 四人小队

公开源码仓库：[2223M1/DohnaDohna](https://github.com/2223M1/DohnaDohna)。商业素材、游戏 DLL、原作/历史参考、档案与录像不随源码上传；公开克隆的输入和许可边界见 [PUBLIC-SOURCE.md](Docs/PUBLIC-SOURCE.md)。

2026-10-14 16:48：拖牌预备／待命恢复上一安装版的姿态与保持方式，仅扎帕普通攻击保留六张画稿等节拍抡拳循环；飞碟动态不回退。普通动作停用多娜原作特效（含护盾、枪线、路径斩光），处决保留已有原作特效，STS2原生命中反馈不变。完整受击与自然非阻塞收招不回退。双宿主构建、Fast隔离验收及忍杀1.1.0同载通过，已备份安装本机0.111.0；见[验证／录像／校验值及备份位置](Docs/validation/preparation-rollback-20261014.md)。下方较早报告的全员循环和普通护盾不再是现行规则。

2026-10-11 18:03：一级海报恢复原作画布构图/尺寸与动效；二级改为所选四人的高清战斗服半身组合，取消/排序/重进同步。界面文案统一为“最前方同伴／正前方同伴／相邻同伴”。双宿主编译、Fast后台选人/原生输入链至首战、忍杀同载通过，已备份安装本机0.111.0；见[截图、验证与安装收据](Docs/validation/selection-terminology-20261011.md)。不改玩法、日常存档或设置，未上传。

2026-10-11：十人同源蓄势循环与连续收招、飞碟漂浮／后坐／受击回稳、实际受益者原作护盾、菊千代目标路径斩光和自然音效尾段已完成。Fast主验收、180个三速度前摇对照、反伤／处决／取消／退场及忍杀1.0.23同载通过；真实0.111.0／0.107.1分别编译。16:56已备份安装至本机0.111.0，见[验证／校验值／安装收据](Docs/validation/action-flow-20261011.md)。stable实机、多人、跨平台、物理输入与人工听审未执行。

[最新26秒Fast四人有声剧场](build/theater/four-role-action-flow-fast-20261011/DohnaDohna-four-role-theater.mp4)：19次真实出牌，含拖牌取消、全队护盾、连续飞碟发射、换位及清场。17个高置信音频锚点，导出AAC最大同步残差约23ms；实际捕帧49.90fps，不将编码60fps当成实拍帧率。全员逐项录像入口见上述验证记录。

2026-10-10：修复虎太郎协同攻击首次获得力量时的嵌套能力冲突，恢复原生出牌/弃牌收尾；空牌堆等原生气泡改由当前前排说出。Fast隔离出牌、气泡/普通角色对照及忍杀/东尼/BaseLib同载通过；该修复已随2026-10-11候选安装，原始记录见[验证与安装记录](Docs/validation/native-flow-20261010.md)。前排不动态改换Player身份，取舍见[归属说明](Docs/squad-native-ownership.md#为什么不在换位时改换玩家身份)。

四人仍为一个玩家拥有的原生友军，不伪装额外玩家。能力归属、原生godmode与海克斯/忍杀/东尼定向兼容修正已在Fast隔离验收，2026-10-10 00:01备份并安装至本机0.111.0。见[验证记录](Docs/validation/power-ownership-20261009.md)与[原生归属说明](Docs/squad-native-ownership.md)；未列为通过的可选模组不能从忍杀历史报告继承兼容结论。

当前动作规格已改为十人两档攻击／施放前摇、原生命令等待、非阻塞实际收招，以及忍杀B档式纯视觉处决，见 [squad-spec.md](Docs/squad-spec.md#资源边界)。普通攻击静音人物配音；处决播放原作普通攻击语音，技能／能力共用的施放动画每人每战首次释放播放一次。旧报告里的“处决无配音”“安缇娜固定0.25秒攻击”不再是现行规则。

十人的打击、特殊攻击和施放使用与正式动作同源的原生拖牌预备段：提交续接当前姿态，取消收回；预备不提前消费、换位或触发效果。纯换位为双方跳跃、不蓄力；自然收招与完整一次性动作音不阻塞下一张牌。受击保留完整原作帧序，轻微后退后自然恢复，不跳回。Fast仅沿用原生前摇规则，不再把收招、跳跃和处决统一减半。阿熊枪口闪光与细曳光分开，修正拉伸成大片白光的问题。当前验收与本地安装状态见 [fast-presentation-20261009.md](Docs/validation/fast-presentation-20261009.md)。

本机四人真实出牌剧场：[新版24秒Fast有声录像](build/theater/four-role-fast-20261009-2224/DohnaDohna-four-role-theater.mp4)。四角色18次原生出牌及真实清场，10个高置信音频锚点的AAC同步最大残差15ms；匹配范围、实际捕帧率与未执行项见上述记录。本地录像不随公开源码上传。最终候选已于22:28备份并安装至本机0.111.0。

飞碟独立射口接入六种原生发射样式；普通小炮在头顶悬停点发射，专门光束穿过目标继续延伸，单体攻击选择避开旁观敌人的方向，超能优先上方发射。高度跟随敌人实际显示顶部，头顶空间不足改用斜上方；消退后自然返航，可被下一张牌立即接管。小炮使用折纸飞弹同款原生命中反馈，菊千代范围斩击使用一次原生大横斩；处决采用忍杀B档暗红黑半透明遮罩。见[最终各样式／枪线／换位／处决静音片段](build/smoke/preview/audio-flow-20261009-220518-035/clips/index.html)。不增加穿透伤害、不改变原生结算或音量。

2026-10-09 小队规则与170张原型卡池、真实角色初始遗物/先古升级、原生改费兼容及常驻飞碟的现行验证见 [squad-catalog-20261009.md](Docs/validation/squad-catalog-20261009.md)。飞碟独立选位发射，按样式悬停或接近后返回；原生大小充能球围绕主飞碟上方。可回交编辑板见 [DohnaDohna-card-board.html](Docs/authoring/DohnaDohna-card-board.html)，差异提取入口为 `node tools/card-board.mjs diff <回交HTML>`。

编辑板现已采用忍者杀手式紧凑固定槽位、箭头升级正文和跨区拖拽；使用与安全回交说明见 [authoring/README.md](Docs/authoring/README.md)，本轮验证见 [compact-card-board-20261009.md](Docs/validation/compact-card-board-20261009.md)。

这是从 NinjaSlayer 已发布主分支提取的工程骨架和参考库，目标目录为 ninja5080 的 `C:\Users\theon\Documents\STS2 MOD\DohnaDohna`。
工程身份 **DohnaDohna**，版本 `0.1.0-dev`。目前具备10人选四人的本地可玩候选：四实体独立血量/格挡/能力、共用玩家资源、专属卡与颜色、换位、逐段死亡接替、火堆复活和原生跑局持久化。
Windows 0.111.0 隔离实机已覆盖主要流程；0.107.1 为独立编译验证。当前动作/影子/选人修复、真实鼠标与键盘验收、本地安装收据及未执行项见 `Docs/validation/motion-reconstruction-20261007.md`；小队基础重构见 `Docs/validation/squad-reconstruction-20261007.md`。`squad-first-playable.md`仅是旧实现历史记录，不能继承其PASS或称为全部计划已验收完成。
当前入口为选中小队后按原生确认进入十人二级栏，复用原生左返回/右确认，四槽拖拽排序。人物身体与血条锚点已校正，原作独立受击配音已接入；全部模组音效/配音统一-20 dB（0.1），不改本体音量设置。
最新场景HUD同步、荆棘反杀受击、仅清场处决及镜头边界修复的隔离回归与安装记录见 `Docs/validation/visual-feedback-20261007.md`。原作大幅后跳允许人物暂时出画，镜头本身不越出场景。
后排反伤归属、原生受击反馈、飞碟/光束共同构图与普通射击不移动怪物的修正见 `Docs/validation/hit-routing-20261007.md`，含真实怪物招式、隔离录音、逐帧对齐和双宿主构建记录。
站位、原生风格动态影子与后台原作动作对照的历史记录见 `Docs/validation/idle-alignment-20261007.md`。2026-10-08 改为面向 STS2 的十人短攻击、原地受击及紧凑处决，不再要求完整原作时间线；普通与处决均使用一次原生命中图声，攻击配音关闭，受击/死亡配音保留。实现、隔离验证和安装状态见 `Docs/validation/attack-pacing-20261008.md`，旧 Claude 交接不再是待执行任务或现行限制。

女性特殊死亡海报默认关闭。在游戏内的模组设置 → DohnaDohna → 演出中可开启“女性角色特殊死亡演出”；关闭时与男性一样只播放倒下动作和死亡语音。主菜单或跑局暂停菜单均可进入框架设置，改动从下一次死亡起生效，不改变跑局存档。

缩小甲虫、大蘑菇及固定站位的最新修正见 `Docs/validation/native-scaling-20261007.md`：缩小归中招队员，大蘑菇放大获得上限的四人，体型变化不重新分配站位。

## 开始

在此目录打开终端，使用 PowerShell 7：

```powershell
node tools/verify-project.mjs
python tools/verify-migration.py
pwsh -File tools/Build.ps1 -Channel preview
pwsh -File tools/New-Package.ps1 -Channel preview
# 手动开启可操作窗口，仅使用隔离安装与档案，不加载测试驱动：
pwsh -File tools/Invoke-IsolatedBoot.ps1 -Interactive
```

手动测试默认是居中的1600×900可拖动窗口；Windows下按`Alt+Enter`可切换宿主全屏。

本机宿主路径在忽略提交的 `.local/hosts.json`。换电脑时复制 `eng/hosts.example.json` 并填实际路径；也可给 Build.ps1 传 `-DataDirectory`。
stable 编译要传真实 0.107.1 输入；preview 为 0.111.0。RitsuLib 编译基线 0.5.12 与安装运行版本是不同记录，不要混写。
普通构建与本地打包均不会安装到日常游戏、上传或修改日常存档。打包器会执行 Godot 导入/PCK导出并检查资源边界。隔离启动器另行复制游戏和候选模组至 `build/smoke/preview`，将 APPDATA/LOCALAPPDATA 定位到该目录。

## 找资料

- `Docs/ENGINEERING.md`：完整开发顺序及实际踩坑经验。
- `Docs/REFERENCE-MAP.md`：从功能定位到源代码、测试、工具和教程。
- `Docs/ORIGINAL-GAME-REFERENCE.md`：《多娜多娜》本地逆向资料、素材分类、检索入口与恢复限制；资料位于旁边的 `DohnaDohnaOriginal/`。
- `.agents/skills/dohnadohna-*`：已适配的原作查证、代码质量、模组开发与 STS2 原版查证技能；`tools/Install-DohnaDohnaSkills.ps1` 安装到个人技能目录并验证一致性。
- `Docs/BUILD-RELEASE.md`：双宿主、通用加载器、CI、隔离测试、发布与回收。
- `Docs/MIGRATION.md`：迁移范围、排除项、来源与授权边界。
- `Docs/VALIDATION.md`：本次实测结果与尚未执行项。
- `Reference/NinjaSlayer/`：发布源码的工程部分，保留原始字节和历史名称。
- `Reference/LocalAuthoring/`：未发布制作脚本，仅供研究，完整依赖/素材另存同级 NinjaSlayer 与 Tools，详见根目录迁移收据。

参考脚本、工程、工作流、部署入口带 `.reference` 后缀，防止误执行。参考区已退出 C# 编译、Godot 导入与导出；引用前逐项适配，不能整目录改名接入。
`Reference/LocalAuthoring/` 另含 FMOD 编辑元数据及去掉卡图的 v1.19 编辑板模板。它们是原项目样例，不能复用其事件 GUID、卡牌模型和浏览器存储键作为新项目身份。
所有 NinjaSlayer 部署账号、域名、Workshop ID 只存在于静态参考中；DohnaDohna 未绑定远端仓库、工坊或统计服务。

原版参考见 Docs/VANILLA-SOURCE.md；工具及最终目录见 Docs/TOOLS.md。
