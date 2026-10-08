# DohnaDohna 验证

当前四人小队、170张卡池、真实初始遗物和飞碟的验证见 [2026-10-09验证](validation/squad-catalog-20261009.md)。`squad-first-playable.md`及以下内容均为历史基线，不代表当前候选状态，不继承旧PASS或旧依赖版本。

在最终 STS2 MOD/DohnaDohna 路径执行：

- stable 0.107.1 与 preview 0.111.0 编译及 DLL 开发包：均通过，0 警告、0 错误。
- verify-project.mjs：通过；编译仅包含空 Entry，不编译参考源码。
- verify-migration.py：1,739 份参考文件校验通过。
- verify-vanilla.py：16,676 份原版源码／文本资源校验通过。
- Godot 4.5.1 Mono headless 导入：通过。
- 技能：18 份原文件逐字节核验通过。
- Python 图像／音频库 import：通过。

编译 RitsuLib 基线固定 0.5.12；目标机 Workshop 已安装版本为 0.6.5，不能将二者混称。

尚未执行：DohnaDohna 实机玩法、PCK 成品包、多人及跨平台运行（目前是空注册骨架）；FMOD 图形界面手动检查；新项目发布和账号认证。NinjaSlayer 的构建和素材结果请看同级迁移证据，不用旧候选替代。

源电脑临时 staging 目录的递归清理曾被自动审批拒绝，仍保留，未绕过；来源源码和资料也保留。
