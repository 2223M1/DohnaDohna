---
name: dohnadohna-sts2-reference
description: Resolve an unanswered STS2 behavior or private contract from the exact target host export for DohnaDohna. Use when public APIs and tutorials are insufficient; not for Dohna Dohna lore.
---

# DohnaDohna 的 STS2 原版查证

工程路径相对含 `DohnaDohna.csproj` 的根目录；遵守根 `AGENTS.md`，已读内容不重复加载。

先明确公共 API/教程未解答的行为或私有契约，以及它影响的玩家行为。常规接入用 `dohnadohna-modding`，原作人物/语音用 `dohnadohna-lore`。

## 精确查证

1. 从 `eng/DohnaDohna.Hosts.props` 确定相关宿主，按 [host-map.md](references/host-map.md) 找对应导出。
2. 按类型/成员检索；符号未知时从相关生产调用定位。读取最小必要方法、状态和调用链，确认先决条件、命令/Hook 顺序及错误、取消、清理。
3. 导出可能失真；涉及私有访问或签名时核对真实程序集。缺少对应输入就报告缺口，不以相邻版本替代。
4. 修改 Patch、私有签名或宿主分支时核对各相关版本；有差异才在功能附近做编译分支。

## 交付边界

给出结论、宿主版本、源码路径、类型/成员及剩余缺口。需要接入时返回最小行为约束，按 `dohnadohna-modding` 选择接口；不照搬原版管理器、继承树、缓存或兼容层。

源码查证、真实宿主编译、实机验证分别记录，旧报告不算本次通过。导出是私人反编译资料，不进入公共 Git、编译或运行包。

历史适配来源：`sts2-original-code-reference`。
