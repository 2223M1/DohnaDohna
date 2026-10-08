# DohnaDohna · 杀戮尖塔 2 四人小队

公开源码仓库：[2223M1/DohnaDohna](https://github.com/2223M1/DohnaDohna)。商业素材、游戏 DLL、原作/历史参考、档案与录像不随源码上传；公开克隆的输入和许可边界见 [PUBLIC-SOURCE.md](Docs/PUBLIC-SOURCE.md)。

2026-10-09 小队规则与170张原型卡池、真实角色初始遗物/先古升级、原生改费兼容及常驻飞碟的现行验证见 [squad-catalog-20261009.md](Docs/validation/squad-catalog-20261009.md)。飞碟攻击会独立飞至目标附近，发射后返回；原生大小充能球围绕主飞碟上方。可回交编辑板见 [DohnaDohna-card-board.html](Docs/authoring/DohnaDohna-card-board.html)，差异提取入口为 `node tools/card-board.mjs diff <回交HTML>`。

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
