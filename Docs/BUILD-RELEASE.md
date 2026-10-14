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

自动验收及剧场默认 `-TestSpeed Fast`；`Normal`仅用于对照，`Instant`用于跳过／清理。驱动仅写隔离档案，stdout与command/menu收据均记录实际选择；逐速度矩阵还在样本中记录当时模式。视觉收招与跳跃不随Fast统一减半，只有原生动画等待采用其Fast规则。

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

`-CommandScenario Impacts -CaptureAudio -Seconds 600`检查十人20次真实普通出牌的原生反馈与20段短处决视觉；后者不执行伤害，不自行补播原生命中声，播放一次该角色原作普通攻击语音。实际处决清场和一次原生命中反馈由`Finisher`真实出牌验证。`Verify-ImpactAudio.py <该次localappdata/audio>`匹配同进程原生基准录音；出牌完成后额外录0.6秒声音尾段，不因短动作已解除等待而截断录音证据。普通攻击继续停用原作混合命中声及人物攻击配音。命令、视觉和波形通过都不冒称人工听审。

`-CommandScenario Preparation -CaptureMovie -Seconds 700`通过原生手牌键盘事件验证十人三种动作的预备、取消、提交及辅助目标；另用生产方法检查早期/中段交接。它是后台宿主输入链测试，不是物理鼠标或物理手柄验收。帧标记与逐角色片段由`Export-MotionClips.py`导出，实际源AVI保留。

`-CommandScenario PreparationEdges`另以生产方法探针检查预备中受击、原生队列撤销、收回途中重新准备和房间退出。它不替代上面的原生输入链。

`-CommandScenario MotionFlow -CaptureRealtime`检查实际冰川／集结的原生格挡与受益者不变，且不再派发原作护盾和专属护盾声；纯生球／抽牌／集中同样无原作护盾。检查飞碟实际射口／球槽锚点、漂浮不移动真实根、暂停、死亡和退出清理。菊千代后排为明示的视觉探针，换位后的单体／群体为真实卡牌命令；普通动作无原作路径斩光，处决视觉探针保留斩光。真实清场资格另由Finisher验证。`Preparation`持牌覆盖扎帕多个等节拍抡拳循环、其余角色上一版的末姿态保持，检查不产生新声音及完整取消／提交；纯时间线另检查六张真实不同画稿及跨圈曝光时长。

`-CommandScenario Emission`自动录制同进程音画，覆盖六种飞碟发射显示、随机目标延迟绑定、真实原生卡连续出牌、近战插队、双人跳跃换位、安缇娜B档遮罩清场与扎帕处决跳回。另测基础／公共／专属／代打小炮命中、反伤不串色、菊千代单体／一次范围横斩，以及阿熊对三个目标正常命中且不再派发原作枪线。样式矩阵临时覆盖单张测试打击的内部显示属性，实际走生产选角、发射、原生命令和扣血；退出释放测试Patch，不增添生产模型或公开API。随机目标绑定另用视觉探针，不冒称随机卡规则全覆盖。`Alignment`另检查三种体型／宽高比、前后排、射口连接及命中截图。普通小炮应留在当前头顶悬停点，不能继续用“所有攻击必须离开悬停点”的旧断言。

发射矩阵现为14次真实卡：两种炮弹各一次、四种光束分别攻击左／中／右三个目标。检查原生几何端点越过敌人、旁观敌人扩张显示边界不被射线穿过、旁观者HP不变，以及消退前飞碟局部位置不返航。世界坐标可随原生整场震动改变，不能用“绝对世界位置静止”误判返航。Alignment扩为18次出牌，增加单体超能、高个怪物1.4倍显示夹具、浮空及顶部净空，群攻按最远目标检查射程。单独看断言不足以证明画面自然，仍须检查实际命中截图／录像。

`-CaptureRealtime`给Pacing、Preparation等场景启用同进程音频及QPC实际时间录像，使用Vulkan异步读回，不固定模拟步长、不与CaptureMovie混用。`Export-MotionClips.py`需要完整video-proof才导出逐角色静音片段与联系表；原录音另存，不能把这些静音片段说成音画已同步。`Verify-ActionAudio.py <audio目录>`用独立起音与450ms之后的源波形片段，检查至少三种长动作音未被旧统一截止截断；它不是全事件覆盖或人工听审。

实时录制时不要并行执行ffmpeg切片导出；编码队列溢出仍判失败，不能扩大队列或掩盖丢帧来换取PASS。若`Verify-CapturedAudio.py`的首500ms语音模板被同期音效掩蔽，可用`Verify-VoiceSegments.py <audio目录>`提供补充证据：两个互不重叠100ms源片段各自相关性≥0.7、延迟一致到2ms。原`audio-proof.json`失败记录保持不变，另存`voice-segment-proof.json`；不能说原模板验证全通过，亦不代表整句人工听感通过。

`-CommandScenario Theater -Seconds 240`录制四角色真实出牌剧场：自动使用同一次游戏的进程录音和Vulkan异步读回，不可与`-CaptureMovie`混用。脚本只在隔离驱动中准备阵容、手牌、能量和初始敌人HP，后续每张牌通过原生手牌输入/行动队列执行，最后一击必须真实扣血清场。音画原件、QPC帧标记、事件和实际HP轨迹保存在该档案`localappdata/audio/`。

```powershell
pwsh -File tools/Invoke-IsolatedBoot.ps1 -CommandScenario Theater -Seconds 240
& ../Tools/STS2/python-tools/Scripts/python.exe tools/Export-Theater.py <该次localappdata/audio> build/theater/<新目录>
```

导出采用实际FMOD源波形的两段独立100ms片段匹配，至少三个高置信锚点且跨越5秒；按该次实测延迟合成，不使用固定猜测偏移。输出再次核对AAC中的声音位置，报告最大残差、实际捕帧率与重复帧。迟帧保留真实时长，不加速游戏或录像。门槛未过则报错，不能把有文件当作同步通过。剧场是后台脚本化原生输入，不冒称人工打牌或物理鼠标实测。

`-CommandScenario Pacing`覆盖十人40次真实自动出牌、完整原作受击帧序配轻微击退、短处决及三种速度，另直接检查及时连续动作的兼容续接、换目标、独立队员和取消。`full-hurt`片段保留完整反应与回到待机，`hurt-replace`另查阿熊／安缇娜的连续受击、取消和新动作接管；不仅检查队列坐标，还须看片并运行`test_motion_contract.py`验证原图内嵌位移的离线注册。`-CaptureMovie`得到实际宿主画面与帧标记，`tools/Export-MotionClips.py`导出逐人片段；MovieWriter固定模拟时间，不能用其墙钟作性能结论。墙钟基准须另跑不录Movie的Pacing，区分纯动画命中门槛、AutoPlay原生展示等待与技能的原生附加效果等待。

`-CommandScenario PacingFocus`仅复测珀尔诺的4次真实出牌和18个门槛，用于定位耗时异常，不等于全员矩阵。Pacing两个入口都在失败时保留`pacing.json`，区分同步准备、原生计时等待、测试观察延迟与每帧engine/wall时间；诊断Patch仅存在于测试驱动。

`-CommandScenario Scaling -Seconds 600`使用缩小甲虫真实第一回合、原生卡牌/Power/遗物命令，检查缩小归属、30%减伤、换位、人工制品及施加者死亡解除；大蘑菇战斗外获得、进战补齐、战斗中获得、十人身体/影子和固定队列槽位另有断言及截图。测试事件/队伍是隔离驱动夹具，不冒称物理输入或完整古人事件路线。

`-CommandScenario PowerOwnership`验证初始实体时机、开场遗物、原生能力叠层返回值、共享/个人归属、四人godmode开关与下次开战、原生图标和普通铁甲战士/奥斯提对照。可传`-AdditionalModDirectory`检查实际同载。合成原始扣血拦截只是契约夹具，不等于对应第三方模组通过。

`-CommandScenario NativeFlow`通过后台原生手牌输入/行动队列复测虎太郎零力量起手的协同攻击、升级版、连续出牌、真实弃牌与中央卡面清理、回合末临时力量清除。使用原生弃牌堆热键生成空堆提示，检查初始前排、换位、前排死亡后的气泡锚点和真实截图；指定后排/敌人与原版铁甲战士/奥斯提另作对照。不是物理键鼠验收；不以自动出牌或直接按钮回调代替这个卡牌UI回归。

`-CommandScenario Catalog`包含关键词定向检查：全卡池的“突前”标记、复制／升级／原生序列化往返、三语正文与HoverTip、原生标题颜色和换位固有首回合入手。三语NCard截图与176项 `runtime-cards.json` 存在当次隔离目录；编辑板既有设计基线不因运行时导出刷新而重建。

`-CommandScenario Selection -Seconds 180`只在原生选人场景验证一级原作画布变换、二级十人高清半身构图、1至4人/空槽、返回重进清空，截图保存到隔离档案。请求三种窗口尺寸，日志记录宿主实际窗口/viewport尺寸；宿主锁定宽高比时不冒称已通过任意宽高比。此为显示夹具，另用`-MenuFlow`验收原生输入链、每人说明不覆盖人物、取消/重排与开局。高清立绘准备入口：`../Tools/STS2/python-tools/Scripts/python.exe tools/Prepare-SelectArt.py`，校验原图散列并只生成已选十人半身PNG，原点/裁切与输出散列在`build/select-art/receipt.json`；不需重新导入战斗动作或音频。

`-CommandScenario Hextech -AdditionalModDirectory <海克斯包目录>`使用真实海克斯0.9.7模型与命令，检查开场力量/抽牌、两张攻击回能、固定能力归属及敌方濒死保命。启动器只在隔离档案关闭遥测、把随机发放数量设为0；驱动经原生命令获得明确测试符文，经海克斯调试入口指定敌方词条。不是完整选符文UI验收，不进入产品包，也不等于海克斯所有功能通过。

## 遗物素材定向验收

情感图标遗物的定向验收使用 `-CommandScenario Relics`：二十种普通／强化原生遗物栏控件、详情页、十张原生强化预览卡，以及四件初始遗物的真实强化选择与替换。截图与资源绑定记录在该次档案的 `localappdata/`，详见 `Docs/validation/relic-art-20261009.md`。

需要为本次工作保留独立候选包时，使用 `tools/New-Package.ps1 -Channel preview -OutputRoot build/relic-art/candidate`。`OutputRoot` 只接受本工程 `build/` 下的独立目录；DLL 在该次编译后立即留存，PCK 与最终包写入该目录。编译中间目录和 Godot 导入日志仍共用，不能据此并发导入或编译同一宿主。

`tools/Invoke-IsolatedBoot.ps1 -SessionName relic-art -ModPackageDirectory build/relic-art/candidate/packages/preview/DohnaDohna -CommandScenario Relics -Seconds 240` 使用 `build/smoke/preview/relic-art/` 下的独立游戏副本和测试档案，不占用默认隔离目录。`SessionName` 仅接受字母、数字、下划线及短横线；省略参数保留原有目录。单个 Session 仍拒绝覆盖运行中的游戏。测试驱动对 RitsuLib 自身遥测的拒绝只写入该次测试档案，以保证截图不被授权提示遮挡。

## 发布时才配置

仓库、Workshop ID、可见性、语言、依赖、图片、官网/Worker/KV/R2 全部使用新项目自己的值。参考源里 NinjaSlayer 的 repo、3776911445 和 telemetry.feixingwawa.cn 永远不能作为 DohnaDohna 默认目的地。
从干净提交构建、使用用户 Git 署名、显式检查 squash 正文，发布后重新下载核对文件、语言描述和依赖。详细更新文案由本项目未来的用户要求决定，不自动套用忍者杀手的固定句政策。
在确认回收范围且没有运行进程后移除重复构建/上传缓存，保留源 SHA、宿主 MVID、RitsuLib 版本和文件散列。不要每版保存完整游戏或完整发布包。
