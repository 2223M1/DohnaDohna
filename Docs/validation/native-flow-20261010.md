# 虎太郎出牌收尾与原生气泡

日期：2026-10-10，Windows，Fast。不改卡牌数值、动作、音量、玩家/卡牌存档ID或原生出牌循环。结构取舍见[原生归属说明](../squad-native-ownership.md)。

## 原因与最小修正

- 日常日志中的协同攻击确实在原生 `Creature.ApplyPowerInternal` 抛出 `Trying to add multiple instances of a non-instanced power to a creature.`，随后 `PlayCardAction` 失败。不是弃牌动画太慢。
- 两个宿主的 `TemporaryStrengthPower.BeforeApplied` 都先施加Strength；旧虎太郎被动在这个Hook中再次施加同一种 `SquadTemporaryStrength`，内层先插入、外层再插入时冲突。
- 被动改用独立来源的 `ToraSupportStrength`，来源提示引用其初始遗物，图标/描述沿用原生临时力量。协同攻击保持旧模型。两者仍由原生命令叠层与回合末扣回；没有屏蔽异常、强制弃牌或自建临时属性引擎。
- `NThoughtBubbleVfx.Create(string, Creature, double?)` 与 `NSpeechBubbleVfx.Create(string, Creature, double, VfxColor)` 的功能内Patch仅把隐藏小队入口解析为当前存活前排。原生工厂继续读取TalkPosition并负责气泡外观、文字、显示时间与释放。后排/敌人/普通玩家/普通宠物及绝对位置重载不改。

原生证据为 `Reference/Vanilla/{0.111.0,0.107.1}/src/Core/` 下的 `Commands/PowerCmd.cs`、`Models/Powers/TemporaryStrengthPower.cs`、`Models/CardModel.cs`、`Nodes/Vfx/{NThoughtBubbleVfx,NSpeechBubbleVfx}.cs`、`Nodes/Combat/NCombatCardPile.cs`。只读身份见 `Entities/Players/Player.cs`、`Entities/Creatures/Creature.cs`；实体事件订阅见 `Combat/CombatStateTracker.cs`。商业导出不随源码交付。

## 验证

证据目录均在 `build/smoke/preview/`，没有占用前台或修改日常档案。

| 项目 | 证据 | 结果 |
| --- | --- | --- |
| 旧安装包复现 | `command-flow-20261010-003812-840` | **预期失败**：后台原生手牌输入触发同一异常，协同攻击留在Play堆/中央；保留失败截图和日志 |
| 修复版 NativeFlow | `command-flow-20261010-004035-361` | PASS：零力量首张协同攻击、升级版、重复叠层、下一张打击、回合结束和次回合重新触发 |
| NativeFlow + 忍杀/东尼/BaseLib | `command-flow-20261010-004307-486` | PASS：同样出牌流程、Play堆与中央卡节点均清空；气泡和普通玩家/宠物对照 |
| CatalogRules | `command-flow-20261010-004459-456` | PASS：各角色被动、强化虎太郎每种增益+2、队列、原生斩杀收益、磁盘保存/卸载/重载与旧初始标记迁移 |
| 工程与静态参考 | `verify-project.mjs` / `verify-migration.py` | PASS，1739个迁移文件 |
| 纯逻辑 | `DohnaDohna.LogicTests` | PASS，11864断言；不是实机证据 |
| 0.111.0 / 0.107.1独立编译 | 真实对应宿主DLL | PASS，65 / 69条原有警告、0错误 |
| Godot导入/导出/包白名单 | `build/native-flow/candidate/` | PASS，PCK v3、6765条目；不含测试驱动 |

NativeFlow手牌使用宿主键盘输入事件与原生行动队列，空弃牌堆使用原生UI热键；这是后台注入输入，不是物理键鼠实测。直接后排/敌人/普通角色说话是原生工厂对照探针，明确与UI热键测试分开。

气泡覆盖初始虎太郎前排、爱丽丝换至前排、前排死亡后接替；没有把生成后已存在的气泡强制改成另一人说话。以下真实截图已查看，位置在说话者头部上方，不在队伍脚下：

- [虎太郎前排](../../build/smoke/preview/command-flow-20261010-004035-361/localappdata/native-flow-bubble-tora.png)
- [爱丽丝换至前排](../../build/smoke/preview/command-flow-20261010-004035-361/localappdata/native-flow-bubble-alyce-after-swap.png)
- [前排死亡后](../../build/smoke/preview/command-flow-20261010-004035-361/localappdata/native-flow-bubble-after-front-death.png)

没有新增卡牌规则或文本；临时力量使用原生三语描述、已有遗物标题，卡池/运行时卡牌导出内容不变。测试副本仍有旧基线已存在的Godot退出资源/脱树节点日志，不宣称全日志零警告。

## 产物与范围

运行宿主0.111.0（`41cef1ea`）；RitsuLib编译0.5.12、运行0.6.7。定向同载：NinjaSlayer 1.0.19、AutoAnthony 0.3.143、BaseLib v3.4.7。只证明上述测试，不等于这些模组所有功能通过。

候选：`build/native-flow/candidate/packages/preview/DohnaDohna`。

| 文件 | SHA256 |
| --- | --- |
| preview DLL | `854a8f4f7ae8d44e0daf90cd21be5e111a34ade569b057b16ca4640fe7d09553` |
| PCK | `87901dfbe0921495cc3d568a78d272f485a64789f3ada1be7e99821627e8f29b` |
| stable DLL | `2be5eedad017647f82e96303f5a396fc6414a697a8cec2dc1493edfd4efcdbc1` |

0.107.1实机、多人、跨平台、物理鼠标、完整卡池穷举与本轮音频/动画重录：**NOT RUN**。没有上传。

## 安装状态

安装前重新检查发现仍有活跃 `SlayTheSpire2` 进程，安全检查在复制前中止；没有强制关闭游戏。**修复版尚未安装**，本地仍为上一轮的 `e3b2d1ccbb5730e837ba970e22defab2e3f749143532d67870e3df26850bc7b3` DLL；上一份安装收据仍为 `build/local-install/20261010-000143-149/receipt.json`，不是本轮收据。

待所有游戏/其他后台测试正常退出后，重新核对宿主和进程，再从上文已验证的候选备份安装；不修改日常存档/设置/其他模组。
