# 选人海报与队列术语 · 2026-10-11

已完成并于18:03备份安装至本机Steam 0.111.0。不上传、不改日常存档、设置或其他模组。

## 本轮改动

- 一级：删除“全部不透明人物必须塞进安全区”的缩放。背景、人物、圆环共用原作1280×720画布，保留原坐标、入场弹性、视差和旋转，允许原作构图本来就存在的画面裁切。
- 二级：不显示首页六人图。使用十人1481×1935源立绘中的战斗服半身，四列对应所选队列；原色、透明边缘与比例保留，专属色斜面、下缘淡出、短入场。选取/取消/重排同步，返回一级清空。说明和卡牌分置两侧，收窄原生说明组件，避免压住人物。
- 原生按钮、手柄/键盘焦点、确认入口和跑局身份保持；未改变战斗、卡牌数值、演出时序、音量、存档字段。新增展示节点由选人面板持有，没有全局管理器或额外Patch。
- 术语同步卡牌、选人、能力、遗物、编辑板与运行时卡牌导出：队首为“最前方同伴”，紧邻前一人为“正前方同伴”，泛称为“同伴”；相邻范围说明用“相邻同伴”。移动目的地仍为“最前方”、复活位置“队尾”，原生充能球“最右侧”不动。“后方同伴”仍是后方范围，不改成紧邻一位。
- 英语区分frontmost member和member directly in front，日语角色介绍由右端改为先頭。未更改模型ID。
- 编辑板270项身份、槽位、费用和基线保持，49个显示字段更新。原基线散列仍为`52dc1e98395820e8132618ca4a82897311d9d88e3e84a184685e3905797477c2`，文字修改保留为可回交差异。176项运行时导出仅标题/基础文案/升级文案可变，非显示字段深比较一致。

## 原作依据与素材边界

- 外置`DohnaDohnaOriginal/reverse/data/pact/Scene/20_Title/Title/SceneTitle.x`及函数31820/31826/31827，沿用已导入的`title-layout.json`。这是原作场景数据对照，不宣称本轮录制了原作实机。
- 十人立绘源为`立绘／角色／打斗／基本`；菊千代使用`战Ｂ／基本`的黑色战斗服。保留原分辨率，只裁掉透明边界与下半身；爱丽丝裁去伸出头顶很远的枪管上段，头脸保留。不是放大战斗小帧，也不是AI重绘。
- `tools/Prepare-SelectArt.py`校验当前原图SHA256再裁切，原图/裁切框/输出散列见[素材收据](../../build/select-art/receipt.json)，[十人素材联系表](../../build/select-art/battle-busts.jpg)。商业原图与生成PNG不进入公开源码。
- 中文依据包括原作技能表“恢复前方同伴40%HP”和范围字符串“相邻”。三个完整短语是基于原词的模组明确化命名，不冒称全部原作逐字用语。

## 验证

| 项目 | 结果 |
| --- | --- |
| `Test-SquadTerminology.mjs`、`verify-project.mjs` | PASS |
| 纯逻辑/时间线 | PASS，15539断言；不是实机结算证明 |
| 真实0.111.0 / 0.107.1编译 | PASS；全量编译保留已有卡牌空引用警告65/69条，无本轮UI警告，0错误 |
| Godot导入、导出、PCK白名单 | PASS，6792条目 |
| 隔离Selection | PASS；十人三组构图、0至4人、顺序/清空、原作画布变换与实际截图 |
| 隔离MenuFlow | PASS；十人原生输入选择/取消、四人上限、拖拽顺序和海报同步、返回/重进、原生开局、首战出牌/换位/结束回合/药水指针 |
| 术语三语原生卡面和目录 | PASS，先前同轮Catalog独立记录，下方链接 |

最终测试均为Fast，Windows后台隔离宿主0.111.0（`41cef1ea`），RitsuLib运行0.6.7、编译基线0.5.12，NinjaSlayer 1.0.23同载。未抢日常游戏前台。

窗口实际接受1600×900、1280×960、1920×810，但原生逻辑viewport始终1920×1080；截图分别为1600×900、1280×720、1440×810。验证的是宿主原生等比画布在三种窗口下的显示，不是三个任意宽高比的重新排版。

最终证据：

- [Selection收据](../../build/smoke/preview/command-flow-20261011-180059-532/command-proof.json)
- [一级截图](../../build/smoke/preview/command-flow-20261011-180059-532/localappdata/select-primary-1600x900.png)
- [二级四人截图](../../build/smoke/preview/menu-flow-20261011-175903-152/localappdata/level-two-four.png)
- [MenuFlow收据](../../build/smoke/preview/menu-flow-20261011-175903-152/menu-proof.json)，同目录localappdata含十人逐个预览/取消/重排截图。
- [术语Catalog收据](../../build/smoke/preview/command-flow-20261011-173349-559/command-proof.json)，其资源包仅用于术语阶段；最终安装为上述海报候选。

`NOT RUN`：0.107.1实机、多人、跨平台、物理鼠标/手柄输入、三语选人布局全矩阵、战斗动画/音频重新录制。后台Godot输入链不冒称物理输入。本轮不改战斗演出，不重录剧场。

## 本地安装与校验

检查游戏无运行进程且本机为v0.111.0后，按最终Selection/MenuFlow散列校验候选，再调用现有安装器，未传SettingsFile。

- 目标：`C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna`
- 候选：`build/select-art/candidate/packages/preview/DohnaDohna`
- [安装收据](../../build/local-install/20261011-180302-583/receipt.json)
- 旧包备份：`build/local-install/20261011-180302-583/previous-mod`

| 文件 | SHA256 |
| --- | --- |
| 已安装preview DLL | `4310d9925f0d6f357f56b59c6b589bce7d3559d4992b8dd8dd4d96596317705c` |
| 已安装PCK | `b09bb74d31a4203a255b1e6d6490a12e4182cb734b2042dee39e74a976336f81` |
| 已安装manifest | `5a2fc90f03d23e4b07612af8c7db3f3ebaa0eb7cee73095280405a547c145858` |
| stable DLL，仅编译 | `db8f9587e16c85ada8fd5966603f1d9fcc25ae2aa8e5cc0fb9b3360b676ad9f9` |
| 备份旧DLL | `bb8056876c8c8d77511aa17eeaceb1e961d8e3e3c74c3071e3d8d947fb0d2cff` |
| 备份旧PCK | `bd69c8e2a08a11df63ebeeab843510dd509bbdcb7f96d612b6a2663af2394ad2` |

安装后独立读取目标文件再次核对一致。保留原有脏工作树与无关制作工具修改，未提交或发布。
