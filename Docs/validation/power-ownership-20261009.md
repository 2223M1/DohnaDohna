# 原生归属、godmode 与可选模组定向验证

日期：2026-10-09～10，本机 Windows。生产结构说明见[小队原生归属](../squad-native-ownership.md)。本轮不改卡牌数值、动作剪辑、音量或存档ID，不上传。

## 基线与修正

旧包隔离基线 `command-flow-20261009-230820-784` 复现：战斗中开启原生godmode仅前排获得三项能力；关闭从隐藏玩家删除，前排残留。连续两次泛型 `PowerCmd.Apply<BufferPower>` 实際叠至队员，但第二次返回另一个未施加的0层实例。**基线中前排图标可见，未复现“四人图标全部缺失”**；另外确认原共享能力栏被能量球/手牌遮住。

落实内容：

1. 初始四实体提前到 `Player.PopulateCombatState`，早于原生开场遗物；删除大蘑菇事后补播。保留原生宠物归属和管理器的一次注册。
2. 开场格挡沿用原生玩家首回合例外；后续清除与能力Hook不变。
3. 原生施加与叠层查找共用同一明确接收者，返回真实能力实例。
4. godmode沿用原生开关/订阅，三项原生能力分别作用于存活队员，关闭后全部清理。普通角色及普通宠物不改。
5. 光环跟踪实际Power实例，外部整组移除后换位不会扣出负力量；共享原生能力栏移至队伍上方。
6. 未知外部扣血拦截/玩法模型下保守停用预测特写，不改实际伤害、保命或死亡。忍杀1.0.19两条已核实的非参与前缀不因仅同载禁用处决。

两个宿主逐项核对 `GodModeConsoleCmd.EnableGodMode/DisableGodMode`、`PowerCmd.FindExistingInstanceForStacking`、`Player.PopulateCombatState`、`Creature.AfterTurnStart`。实际编译版本见下表；不是把0.111.0 DLL标为双版本。

## 构建与候选

| 项目 | 结果 |
|---|---|
| 工程检查 | PASS：`node tools/verify-project.mjs` |
| 静态参考边界 | PASS：1739个迁移文件 |
| 纯逻辑 | PASS：11864断言；非宿主实机 |
| 真实0.111.0编译 | PASS：65条既有警告，0错误 |
| 真实0.107.1编译 | PASS：69条既有警告，0错误 |
| Godot导入/PCK白名单 | PASS：6764条；测试、私有参考、第三方包不进入产品 |
| RitsuLib | 编译0.5.12；实际运行0.6.7 |

候选：`build/power-ownership/revision2/packages/preview/DohnaDohna`。

源码基线 `ea6a098faef04b13ba0be17a8fb50e10b534acd9`，包含本轮及用户既有未提交修改；不是干净发布构建。真实输入MVID：0.111.0 `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e`；0.107.1 `97f10687-c306-4798-ab75-8b9f23f34dfb`。

- preview DLL：`e3b2d1ccbb5730e837ba970e22defab2e3f749143532d67870e3df26850bc7b3`
- PCK：`1e7480de08c34d68a7dad390f0430b50d4b7ac36068d4cbdbf1f6ebcb47b03f4`
- stable DLL：`24c4b79805d898dabc1983f3a8e7f0cf2cc33e412b47513a8dcb63ba91e31665`

日志：`build/power-ownership/package-revision2-preview.log`、`build-revision2-stable.log`。

## Windows 0.111.0 实机（Fast）

全部在不活动桌面与隔离档案内运行。使用原生命令/真实渲染，不是物理鼠标验收，不把函数调用等同于人工操作。

| 场景 | 证据目录（均位于 `build/smoke/preview/`） | 结果 |
|---|---|---|
| PowerOwnership，忍杀+东尼同载 | `command-flow-20261009-234420-875` | PASS：开场金刚杵/光滑石/船锚，真实叠层实例、移除、集中、共享能力、四人godmode图标和缓冲、换位后关闭、下战重启/关闭、光环清除后重排；另起原版铁甲战士+奥斯提对照通过 |
| Finisher，忍杀+东尼同载 | `command-flow-20261009-234525-756` | PASS：非致死、部分击杀、缓冲、格挡、单体与群体清场、转阶段阻止；普通同载未禁用处决 |
| Scaling，最终候选 | `command-flow-20261009-234616-132` | PASS：真实缩小甲虫回合，减伤、换位、人工制品、解除，大蘑菇开场/中途、十人缩放和固定站位 |
| Hextech，海克斯+忍杀+东尼同载 | `command-flow-20261009-235521-276` | PASS：下述限定案例 |

早期候选额外通过 `Routing`（`232302-039`）、`CatalogRules`（`233853-349`）、`Core`（`234017-211`）；最终候选重跑记录在本文件后续“最终回归”项中，不把早期证据冒作最终产物。

原生反馈截图：[四人godmode及共享栏](../../build/smoke/preview/command-flow-20261009-234420-875/localappdata/godmode-four-native-bars.png)。普通角色与宠物对照见同目录日志。原生godmode有意移除整组力量/缓冲/再生，开关前的普通来源不作恢复备份。

## 海克斯实际边界

本机无工坊海克斯包。使用公开MIT源码 [s1f102500012/HextechRunes](https://github.com/s1f102500012/HextechRunes)，固定提交 `e0529bf5f5148a56f3f7857a858c499c82d1f8fd`，manifest 0.9.7，实际针对0.111.0编译。

来源保留在忽略提交的 `.local/HextechRunes-reference`；不修改游戏代码。资源使用其原素材/打包流程，构建副本的打包脚本仅补 `Variant` 显式类型以适配本机Godot。没有运行其自动部署脚本，没有安装/修改用户的海克斯。

- 海克斯测试DLL：`bf8095b4053b28840d250f2e37e32801b6cc11749fe5a1d2ffac5b1ac417f95a`
- 海克斯测试PCK：`2c6f1f0886c50dc1958f2787e2bb6e5e8e0746edb4cd5135cbd0165536f53b20`
- 实测启用：DohnaDohna、RitsuLib0.6.7、NinjaSlayer1.0.19、AutoAnthony0.3.143、上述海克斯及测试驱动。
- 测试档案明确关闭海克斯遥测；正常档案和全局配置不动。

`BrutalForceRune` 开场1力量归前排、额外2张手牌只发一次；`ArcanePunchRune` 两张真实攻击牌获得1共享能量，不按四实体重复；换位不迁移已获得力量。所选四人的80种原型牌池保持。

敌方 `NearDeathFeast` 经海克斯自身调试入口启用：真实卡牌6伤害把3血敌人打入-3债务，内部1血存活、获得3力量，战斗不提前结束、不触发清场特写；下一张真实卡跨过债务上限后正常死亡。证据：[hextech.json](../../build/smoke/preview/command-flow-20261009-235521-276/localappdata/hextech.json)、同目录日志及 `hextech-near-death-survival.png`。

玩家版濒死狂宴仅允许原版铁甲战士，已核实小队自然不可获得。没有伪造职业、复制四份负血债务或强制解锁原职业符文。**所有符文、完整选符文/古人路线、工坊二进制等同性：NOT RUN**。在这类未知外部能力存在时，特写预测可保守停用，实际卡牌伤害照常。

## 忍杀九模组清单的本轮结论

| 模组 | 本轮结论 |
|---|---|
| 海克斯0.9.7 | 上述源码构建定向实测PASS，不代表所有符文 |
| 东尼0.3.143 | 同载/原归属/处决PASS。四人生成牌桥接未设计，保持170张卡池；生成模式适配 NOT RUN |
| Loadout 2 | 未安装，NOT RUN。未知扣血拦截仅用合成契约夹具验证，不冒充Loadout |
| CookieCursor | 未安装，NOT RUN；未借用忍杀光标身份 |
| MintySpire2 | 未安装，NOT RUN；多实体伤害预测未宣称适配 |
| Better Menu | 未安装，NOT RUN；小队自身原生读档另行回归 |
| RemovalCostViewer | 未安装，NOT RUN；未添加猜测的删卡计价补丁 |
| MaxHpSizeMod | 未安装，NOT RUN；只认玩家身体的第三方体型算法不自动扩展为四人 |
| ThrowPotionsAtArchitect | 未安装，NOT RUN；未复用忍杀专属建筑师收尾 |

## 失败记录与夹具校正

不删除失败证据：`231807-537` 暴露开场格挡被原生宠物首回合清除，已修正生产入口。缩放夹具原先依赖已删除的开场自动轮换、旧9伤害专属牌，现改为明确阵容与7×0.7原生截断=4；`233618-653`、`234203-103` 的末项还采到了原生血条入场补间的亚像素位置，改为等待入场结束后作缩放前后比较，不放宽站位断言。

海克斯 `234822-086` 停在正常开局选择面板而超时；定向夹具用该模组自身配置关闭随机发放，原生获得明确测试符文，没有禁用模组或伪造PASS。`235258-560` 被爱丽丝本回合追击加成改变测试伤害，改用无力量扎帕的精确6伤害边界后通过。测试调用不计作UI验收。

## 最终回归与本地安装

最终候选全部复测通过（Fast）：

- `command-flow-20261009-235633-211`：CatalogRules，包括真实原生磁盘保存/卸载/重载、队列/HP/初始遗物及强化ID；光环和被动回归。
- `command-flow-20261009-235750-361`：Core，包括复活、逐段接替和全灭后的牌堆/可打状态。
- `command-flow-20261009-235837-016`：Routing，包括后排荆棘的个人格挡/缓冲/妖精药水/死亡/最后敌人同死、原生命中特效目标、多段与群攻去重。

2026-10-10 00:01检查无运行游戏后，将上述逐文件校验的preview候选安装至 `C:/Program Files/steam/steamapps/common/Slay the Spire 2/mods/DohnaDohna`。实际宿主0.111.0，commit `41cef1ea`。安装后的DLL/PCK校验与上文相同。

备份：`build/local-install/20261010-000143-149/previous-mod`；[安装收据](../../build/local-install/20261010-000143-149/receipt.json)。只替换DohnaDohna，未安装海克斯测试包、未修改日常存档/设置/其他模组，未上传。

本轮未重新录制动作剧场（动作未改）；旧录像不充作本轮兼容证据。0.107.1实机、多人、跨平台和物理鼠标：**NOT RUN**。
