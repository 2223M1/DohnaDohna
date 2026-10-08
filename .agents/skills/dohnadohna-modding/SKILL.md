---
name: dohnadohna-modding
description: Implement or debug DohnaDohna Slay the Spire 2 mod features using installed RitsuLib public APIs, content registration, commands, Godot resources, localization, FMOD and project build/export contracts. Not another game's modding workflow or broad architecture audit.
---

# DohnaDohna STS2 模组开发

适配源技能 sts2-ritsulib-modding，但目标工程、命名空间、资源、宿主配置和验证入口均使用 DohnaDohna。不要照搬 NinjaSlayer 工程名、发布身份或不存在的命令。

找到 `DohnaDohna.csproj`，读根 `AGENTS.md`；按 [project-map.md](references/project-map.md) 选择接口、教程、资源和验证入口。先看当前生产代码及已安装 RitsuLib API，教程与实际签名冲突时以实际宿主/依赖为准。

## 需求与外部接口

明确玩家可见行为和最小外部边界。主题、人物、命名或对白需要原作事实时先用 `dohnadohna-lore`；只涉及内部结构审查时用 `dohnadohna-code-quality`。公共 API 与教程没有回答某个精确宿主行为时，才用 `dohnadohna-sts2-reference`。

优先现有 RitsuLib 的内容、生命周期、命令、注册与持久化接口。程序集发现从 `Scripts/Entry.cs` 追踪。只引入实际需要的内容类、节点、Patch、资源或 FMOD，不自动追加完整忍者杀手玩法和工具平台。

数值、关键词、费用、升级、名称或行为发生变更时同步 `Docs/card-catalog.md`、相关运行规格和 `DohnaDohna/localization/{zhs,eng,jpn}/`。显示名称与模型/存档 ID 分离；设计推论不冒充原作事实。

## 注册、Patch 与结算

- 先核对已安装 API 的实际注册和签名。普通静态 Patch 采用对应公开 IPatchMethod/ModPatchTarget 模式，集中在 `Scripts/Entry.cs` 注册；不要只为包装一个 Patch 建分组。
- 必需 Patch 不能部分安装后继续初始化；失败须验证回滚。可选部分只有在所拥有的静态/动态目标均已释放后才能降级，不用总计数替代逐目标证据。
- 多段攻击、抽弃牌、随机流、目标选择、死亡/召唤和联机选择沿原生命令顺序；逻辑必须 await，显示/预览不消耗随机或结算状态。
- 状态与后台视觉归属单次功能，具备退出/取消清理；优先移动身体显示节点，不能连血条/状态 UI 一起移动，也不能覆盖外部朝向、缩放和位置。
- 宿主配置来自 `eng/DohnaDohna.Hosts.props`，差异仅在所属功能附近编译分支；不猜测版本或引入全局兼容平台。

## 资源与原作参考

运行资源只在 `res://DohnaDohna/...`。外置 `DohnaDohnaOriginal/`、`Reference/Vanilla/` 与 `Reference/NinjaSlayer/` 是资料，不是自动运行依赖或可直接发布的内容。

采用已明确选定和适配的图像、动画与声音；使用索引中的确切输出路径及原始编号，不从全库数字或文件排序猜角色。音频接入 FMOD bank/event，不增加原始 WAV/OGG 直播放回退；新事件与 bank 使用自己的身份，不复用忍者杀手 GUID。

引入 Godot 运行资源前，先按 `Docs/BUILD-RELEASE.md` 建立实际 import/export 和包条目验证。空骨架目前的打包方式不自动等于具备 PCK 发布能力，设置 has_pck 前先核对现状。

## 收尾

选用确实存在且相关的检查与构建。精确区分源码检查、preview/stable 编译、隔离实机、联机与跨平台结果；不把参考项目旧报告或一次普通构建当作全部通过。

构建、打包、安装、上传是不同操作；只完成本次授权的阶段。未经要求不配置仓库、Workshop、后台或统计服务，不沿用旧项目账号、域名与凭据。
