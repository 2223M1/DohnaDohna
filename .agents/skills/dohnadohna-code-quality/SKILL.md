---
name: dohnadohna-code-quality
description: Review or simplify DohnaDohna architecture, state ownership and lifecycle using production evidence. Use for structural review or refactoring, not routine content registration.
---

# DohnaDohna 代码质量

工程路径相对含 `DohnaDohna.csproj` 的根目录；遵守根 `AGENTS.md`，已读内容不重复加载。审查以当前工作区为准，历史报告不代表当前状态。

只读审查只报告发现；授权修复时才改文件，保留无关修改。除用户要求的行为变化外，保持玩家行为、序列化身份和外部契约；证据不足时允许“无需修改”。

## 保留与删除的依据

- 追踪生产调用和状态所有者，区分测试、工具与历史参考。类名和类型数量不是质量证据。
- 抽象须承载真实边界、状态/策略或独立生产调用需求。简化纯转发和只为测试存在的层，不换个 facade/helper 名称重新包装。
- `Try*`、catch、回退只处理调用者能有效应对的边界失败；内部不变量坏了应修生产者或报错。
- 状态、任务、节点、输入锁和音频由产生它们的功能负责；核对完成、取消、重载、退出后的清理及旧任务回写。
- 同步须有实际并发访问需求；缓存、池化、批处理等优化须有热路径测量。遵守工程对全局兼容平台和 GC 控制的禁令。
- 宿主差异须有目标版本证据，迁移须有生产者版本与真实样本；不为假想兼容增加结构。

## 验证与交付

Patch/API 契约用 `dohnadohna-modding` 核对；公共接口无法解释的宿主行为才用 `dohnadohna-sts2-reference` 查证。

运行 `node tools/verify-project.mjs`，按改动选择现有行为测试；宿主/API/编译分支变化按 `Docs/BUILD-RELEASE.md` 验证相关真实宿主。只改技能或文档无需构建、打包或实机。

报告发现位置、行为后果、必要修改和实际验证；未执行项标明 `NOT RUN`，不以结构变少或旧报告代替正确性。

历史适配来源：`ninjaslayer-code-quality`。
