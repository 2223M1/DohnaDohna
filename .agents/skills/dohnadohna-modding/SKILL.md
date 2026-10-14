---
name: dohnadohna-modding
description: Implement or debug DohnaDohna STS2 features with installed RitsuLib APIs and project resources. Use for integration work; architecture-only reviews belong to dohnadohna-code-quality.
---

# DohnaDohna STS2 模组开发

工程路径相对含 `DohnaDohna.csproj` 的根目录；遵守根 `AGENTS.md`，已读内容不重复加载。按 [project-map.md](references/project-map.md) 选择当前任务需要的入口。

## 实现与契约

- 先明确玩家可见行为，追踪当前生产代码及已安装 RitsuLib API；教程与签名冲突时以实际宿主/依赖为准。
- 优先公开内容注册、生命周期、命令和持久化接口，从 `Scripts/Entry.cs` 追踪程序集发现。接口不足时才用功能内精确 Patch，再考虑局部反射。
- 普通静态 Patch 使用实际 `IPatchMethod`/`ModPatchTarget`，在 Entry 注册。必需 Patch 失败须回滚，不能部分安装后继续；可选功能降级前须释放所拥有的全部静态/动态目标，逐目标验证。
- 逻辑结算必须 await，保持原生多段攻击、抽弃牌、随机、选择与死亡/召唤顺序；预览不消耗结算状态。纯视觉尾段须有归属和取消清理，身体演出与血条布局分离，恢复外部变换。
- 卡牌名称、数值、费用、关键词、升级或行为变化，同步三语、`Docs/card-catalog.md`、相关规格和运行时导出 `Docs/card-metadata.json`；显示名变更不改模型/存档 ID。

需要原作事实时用 `dohnadohna-lore`；公共 API 与教程无法解释的精确宿主行为用 `dohnadohna-sts2-reference`；纯结构审查用 `dohnadohna-code-quality`。

## 资源与验证

运行资源使用 `res://DohnaDohna/...`；素材按已查证的引用选用。音频走自己的 FMOD bank/event/GUID，不增加原始 WAV/OGG 回退。

Godot import/export 与 PCK 条目按 `Docs/BUILD-RELEASE.md` 检查，配置声明不能代替产物验证。按 project-map 选择相关检查，分别报告源码、各宿主编译与隔离实机结果；未执行项写 `NOT RUN`。

构建、打包、安装、上传分别执行，只完成本次授权阶段。

历史适配来源：`sts2-ritsulib-modding`。
