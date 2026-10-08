# 构建、验证与发布路线

## 本工程可直接执行

`tools/Build.ps1` 只 restore/build，真实宿主目录通过参数或 `.local/hosts.json` 提供。两个 channel 的 bin/obj 分离。未选择游戏 DLL 时明确失败，不回退到近似 RefLib。
`tools/New-Package.ps1` 将本次 DLL、Godot PCK、带 channel 最低游戏版本的 manifest 和 SHA256SUMS 放入 `build/packages/<channel>/DohnaDohna`。先导入/导出并拒绝导出错误及非法PCK条目；不复制 sts2、Harmony、RitsuLib 或 GodotSharp到模组包，不安装到日常游戏、不上传。
`node tools/verify-project.mjs` 检查身份、编译/导出隔离和空发布绑定。`python tools/verify-migration.py` 检查静态参考的原始字节。
根 CI 只执行不需要商业游戏输入的源码检查及 Git 索引公开边界检查，无发布权限；历史参考迁移校验只在保有私人输入的本机执行。实际候选验证要另配置 DohnaDohna 自己的受保护输入。公开仓库边界见 `PUBLIC-SOURCE.md`。

## 引入 Godot 资源时

安装 Godot 4.5.1 .NET 编辑器与对应导出模板，在 .local/hosts.json 填 godotConsole。资源仅放 DohnaDohna/；先 import，再导出 PCK，导出后检查条目白名单并记录资源散列。参考 NinjaSlayer.Packaging.targets 和 package-contract.mjs 的实际实现，移除其 Box2D、Spine、角色专属校验与原项目安装路径。
不能直接使用 Windows Spine 编辑器扩展作为跨平台随包依赖。Export preset 是隔离起点，未经过 DohnaDohna 资源实机导出前不得声称可发布。

## 双宿主通用包

参考 `Reference/NinjaSlayer/tools/loader/` 的 schema 2：原生 ReleaseInfoManager.SemVer 精确选择已声明实现，MVID 仅记录证据；检查相对路径、重复清单、DLL 散列及混装。运行时 MVID 白名单曾误拒绝同版本 macOS，不应继承。
空骨架尚无通用加载器，两个本地包分别针对 stable 0.107.1 / preview 0.111.0。需要同时发布时再抽取该 loader，并以 DohnaDohna 的身份和包布局验证；不要直接上传单宿主包给所有玩家。
源码参考仍包含旧构建期 MVID 约束和旧命令示例，属于历史上下文，不覆盖上述运行时原则。

## 必要验证层次

- 纯逻辑：玩家可见的数值、顺序、取消、选择和随机一致性。
- 仓库：资源/三语/目录/模型注册一致，工作区与发布范围干净。
- 双宿主：真实程序集编译，精确 Patch/API 契约、动态卡牌与关键词导出、候选包文件/散列。
- 隔离实机：根加载器启动、角色选择与首战、重启、读档、死亡/复活、快速模式、暂停/取消；多人分别记录各客户端结果。
- 跨平台：Windows build 不等于 macOS/Linux 实机；未测试就写 NOT RUN。

当前候选可用 `tools/Invoke-IsolatedBoot.ps1 -Probe -Rendered -Seconds 210` 在不活动的 Windows 桌面运行生产代码的原生命令场景。驱动仅在隔离副本中加载，不进入 DLL/PCK成品包。`-Interactive` 由用户手动运行，开启隔离可操作窗口而不加载驱动；不要与 `-Probe/-Rendered` 混用。

手动与后台渲染启动统一使用原生窗口模式、1600×900、可调整尺寸，并以 `window_position=(-1,-1)` 让宿主重新居中，不恢复旧的偏移位置。Godot 参数为 `--windowed --resolution 1600x900`（分开传参，不使用 `--resolution=1600x900`）。窗口有标题栏；Windows 下可用宿主原生 `Alt+Enter` 切换全屏。
`-CommandScenario Display -Seconds 35` 只检查真实渲染宿主的启动窗口、原生全屏、返回窗口三个状态，核对模式、边框标志、尺寸和屏幕范围，输出 `localappdata/display-proof.json`。不进入跑局，也不等于日常 Steam 启动或物理拖动验收。

`-MenuFlow -Seconds 180` 是独立的原生界面输入验收：点击网关角色、十人选取、原生返回/确认、地图到首战、出牌/结束回合和换位。主菜单、地图与出牌使用宿主键盘输入；角色栏和原生确认使用鼠标事件，不直接调用开局函数或按钮回调。隔离桌面不提供有效 OS 鼠标位置，不能把它称为物理鼠标/手动全流程验收。
新档案的原生首次教学标记及兼容模组遥测拒绝只写入测试档案。`-MenuRoster` 指定四名队员，`-AdditionalModDirectory` 提供待验证的本机模组目录。运行前重新编译测试驱动，避免使用编译失败后的旧 DLL；隔离副本只保留本次指定的模组，多余旧测试模组可恢复地移出。每次在独立 `menu-flow-*` 目录保存日志、截图与 `menu-proof.json`，没有完整 PASS 就返回失败。

用户授权前台实测时，`-Interactive -FreshProfile`建立新的手工隔离档案，不加载驱动。跨进程读档使用`-Interactive -InteractiveProfile <已存在的manual-flow目录>`；只接受build/smoke/preview直属、非链接的手工档案，不接受日常存档。运行中的隔离游戏必须先正常关闭，不能覆盖其包或同时启动测试。

`tools/Build-Audio.ps1` 重建选定素材、自己的Studio事件、Desktop bank/GUID；不复制Master bank、不安装。`Invoke-IsolatedBoot.ps1 -CaptureAudio -Seconds 240`通过Windows进程loopback只录制隔离游戏的声音；测试档案的BGM/环境音静音以识别低响度配音，不改日常设置。之后使用`../Tools/STS2/python-tools/Scripts/python.exe tools/Verify-CapturedAudio.py <音频目录>`匹配实际输出波形；FMOD实际音量由测试探针读取。注册、开始播放、波形匹配、人工听感是不同证据层级。

`-CommandScenario Recovery/Boundaries/Presentation/Lifecycle`分别选择复活、战斗边界、十人死亡、动作暂停/取消/退场的定向场景。每次传一个值，各自有独立PASS标记，不等于Full通过。最新Full覆盖更多结算，可用`-CaptureAudio -Seconds 600`；不并发启动隔离游戏。

`-CommandScenario Feedback`定向检查特写镜头的共同绘制归属、原生血条/状态/意图及目标布局不变、镜头边界与结束恢复，以及原生荆棘反杀普通/最后敌人时的受击和房间退出清理。边界使用0.001像素绝对容差；不是物理输入验收。
`-CommandScenario Finisher`通过真实出牌检查非致死、部分击杀、缓冲、格挡、单体清场、无视格挡、活力消费和群攻清场的特写开关；额外直接验证原生阶段/死亡召唤阻止Hook会抑制预测。两种证据在日志中分开记录。

`-CommandScenario Routing -CaptureAudio`检查后排反伤的独立格挡/缓冲、群攻、死亡，7×3逐段VFX目标，真实怪物招式、破盾与原生投射物。录音在该次档案的`localappdata/audio`；`tools/Verify-NativeHitAudio.py`核对破盾音源隔离正/负对照与真实怪物攻击的原生独立起音、事件次数和音量，`Verify-CapturedAudio.py`核对模组配音输出。音源门控只在测试驱动的对照段启用，不进入成品、不改变音量设置。原生随机音色不适合只用单一波形模板判定；调用日志与录音证明分开，不能将前者单独算作可听到。验证工具本身用独立Python环境运行`python -m unittest discover -s tools -p test_native_hit_audio.py`，覆盖无输出、串入配音、重复音效、错误音量及非隔离录音拒绝。
`-CommandScenario Alignment`检查安缇娜普通/特殊攻击在三种体型及宽高比、前后排换位后的持续发射连接；`Motion`另逐帧检查十人的同组身体/装备构图与普通攻击不接管怪物身体。均为真实渲染的命令驱动，不冒称物理鼠标输入。

`-CommandScenario Impacts -CaptureAudio -Seconds 600`检查十人20次真实普通出牌的原生反馈与20段短处决视觉；后者不执行伤害，不能自行补播命中声或攻击配音。实际处决清场和一次原生命中反馈由`Finisher`真实出牌验证。`Verify-ImpactAudio.py <该次localappdata/audio>`匹配同进程原生基准录音；出牌完成后额外录0.6秒声音尾段，不因短动作已解除等待而截断录音证据。普通与短处决都停用原作混合命中声及人物攻击配音。命令、视觉和波形通过都不冒称人工听审。

`-CommandScenario Pacing`覆盖十人40次真实自动出牌、原地受击、短处决及三种速度，另直接检查及时连续动作的兼容续接、换目标、独立队员和取消。`-CaptureMovie`得到实际宿主画面与帧标记，`tools/Export-MotionClips.py`导出逐人片段；MovieWriter固定模拟时间，不能用其墙钟作性能结论。墙钟基准须另跑不录Movie的Pacing，区分纯动画命中门槛、AutoPlay原生展示等待与技能的原生附加效果等待。

`-CommandScenario Scaling -Seconds 600`使用缩小甲虫真实第一回合、原生卡牌/Power/遗物命令，检查缩小归属、30%减伤、换位、人工制品及施加者死亡解除；大蘑菇战斗外获得、进战补齐、战斗中获得、十人身体/影子和固定队列槽位另有断言及截图。测试事件/队伍是隔离驱动夹具，不冒称物理输入或完整古人事件路线。

## 发布时才配置

仓库、Workshop ID、可见性、语言、依赖、图片、官网/Worker/KV/R2 全部使用新项目自己的值。参考源里 NinjaSlayer 的 repo、3776911445 和 telemetry.feixingwawa.cn 永远不能作为 DohnaDohna 默认目的地。
从干净提交构建、使用用户 Git 署名、显式检查 squash 正文，发布后重新下载核对文件、语言描述和依赖。详细更新文案由本项目未来的用户要求决定，不自动套用忍者杀手的固定句政策。
在确认回收范围且没有运行进程后移除重复构建/上传缓存，保留源 SHA、宿主 MVID、RitsuLib 版本和文件散列。不要每版保存完整游戏或完整发布包。
