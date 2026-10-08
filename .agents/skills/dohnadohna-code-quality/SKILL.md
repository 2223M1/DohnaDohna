---
name: dohnadohna-code-quality
description: Review or refactor DohnaDohna STS2 mod architecture, state ownership, lifecycle, defensive wrappers, host compatibility, Patch transactions and measured performance. Use for evidence-backed cleanup or architecture review, not routine lore lookup or content registration.
---

# DohnaDohna 代码质量

借鉴 ninjaslayer-code-quality 的证据门槛与删除优先思路；不把忍者杀手的历史重构、测试工程或退休结构清单当作本工程已经发生的事实。

找到包含 `DohnaDohna.csproj` 的工程，读根 `AGENTS.md` 与 `Docs/ENGINEERING.md`。对源代码版本、宿主和路径，以当前文件为准；未提交的工程可按实际工作区审查，不捏造 SHA。

## 先明确请求类型

- 只读审查：追踪当前生产调用和实际问题，报告有据发现；未要求修复时不改文件。
- 已授权修复/重构：先保留玩家可见行为、序列化身份和真实外部契约，再移除不必要层。
- 没有足够证据或没有问题时，“无需修改”是有效结论；不为审查制造清理计划。

## 决策依据

- 区分生产、测试、工具和历史参考调用。名称含 Manager/Service/Registry 不是单独的保留或删除理由。
- 抽象必须承载真实外部边界、可变状态/策略或独立生产调用需求；单纯转发或只为测试方便存在的层应简化。
- `Try*`、catch 和回退用于调用者能作有效选择的预期边界失败；内部不变量坏了应修生产者或显式报错，不吞掉错误伪装成功。
- 状态、后台工作、节点、输入锁、音频和取消机制由产生它们的功能负责。先证明实际并发访问需求，再引入同步；不按“以后可能有用”设计平台。
- 缓存、池化、批处理、加载限制和 GC 干预需要测量指向具体热路径；不未经测量追加全局优化。
- 宿主目标读取 `eng/DohnaDohna.Hosts.props`；已证明的差异放在所属功能的编译分支，不建立全局能力图、方法体指纹、反射平台或猜测版本的兼容门面。
- 历史迁移需要真实生产者版本与样本，不能用任意前缀、best-fit 或假想未来兼容替代证据。

## 修改约束

删除后不以同义 facade/helper/context 重新引入同一层。只处理用户授权的范围，保留无关修改和有意归档。NinjaSlayer 参考示例可提供经验，不是必须复制的实现结构。

Patch 与宿主外部契约由 `dohnadohna-modding` 核对；公共 API 不能确定的精确原版行为由 `dohnadohna-sts2-reference` 查证。需要角色主题事实时用 `dohnadohna-lore`。职责切换不授权委派或扩大修改。

保持逻辑结算的 await、原生随机/选择/死亡顺序、角色身体显示节点与血条根节点的分离、退出/取消清理和 FMOD 流程。发生结构变化时验证具体受影响行为，不用类型数量下降代替正确性。

## 验证与交付

工程基本检查使用 `node tools/verify-project.mjs`；改动迁移参考边界时用 `python tools/verify-migration.py`。按真实改动选择现有逻辑测试与 `pwsh -File tools/Build.ps1 -Channel preview` 或 stable，先确认输入与命令存在。

主机 API、Patch 或编译分支变化需各相关真实宿主验证。单宿主构建不等于双宿主或实机通过。文档/技能修改不自动要求安装游戏、发布或启动实机。

报告必要的删除/修改、被保留结构的理由、玩家可见行为与实际检查；未执行项标明 `NOT RUN`。不要生成永久“结构缺席注册表”或无价值的审计平台。
