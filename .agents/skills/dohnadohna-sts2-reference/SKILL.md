---
name: dohnadohna-sts2-reference
description: Resolve precise vanilla Slay the Spire 2 behavior for DohnaDohna when installed RitsuLib public APIs and tutorials are insufficient. Use exact-version local source for command order, hooks, serialization or private targets; not Dohna Dohna lore or a mod architecture template.
---

# DohnaDohna 的 STS2 原版行为参考

适配 sts2-original-code-reference，目标是宿主 STS2，不是多娜多娜原作。原作人物/剧情/语音事实由 `dohnadohna-lore` 查证。

## 进入条件

先明确一个实际缺口：哪个当前宿主、哪个类型/成员/签名/序列化字段或命令顺序、会影响什么玩家行为，以及为什么已安装公共 API 与教程不能回答。

没有明确问题就回到 `dohnadohna-modding` 或 `dohnadohna-code-quality`，不因为原版附近有个方便的类就浏览或复制整套架构。

## 查证流程

1. 定位 `DohnaDohna.csproj` 工程，读取 `eng/DohnaDohna.Hosts.props` 中任务涉及的主机版本；按 [host-map.md](references/host-map.md) 找对应的私人导出。
2. 按精确类型、成员或已确认签名查询。没有符号或对应导出就报告缺口，不能换成相邻版本或宽泛类名猜测。
3. 读取最小必要方法、状态与调用链，记录状态归属、先决条件、命令/Hook 顺序、取消、错误、清理与玩家可见结果。
4. 只抽取影响本次行为的事实；记录主机版本、路径、类型和成员。导出不是官方可重建源码，必要时与真实程序集签名核对。
5. 差异存在才在所属功能内做编译分支；修改 Patch、私有签名或主机分支时核对各相关当前版本。

## 接入边界

优先公共 RitsuLib API；确实不够时再考虑所属功能的精确 Harmony Patch，然后才是局部反射。私有访问不扩散成共享反射/兼容平台。

原版类继承树、管理器、缓存和防御层不是模组必须遵从的结构。行为证据也不是卡牌数值平衡或多娜多娜原作设定。

精确签名定位可以使用；不以方法体指纹、IL 散列、token 匹配、运行时猜版本或静默降级伪造兼容性。历史迁移要对应生产者版本与真实样本。

交付回答问题所需的证据、最小接入方式与剩余缺口；明确区分检查过的导出、主机编译与实机结果，不把参考旧报告算作本工程验证。
