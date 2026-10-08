# 窗口模式修复 · 2026-10-07

## 问题与边界

用户截图是日常Steam启动，不是后台隔离测试。最近日志没有启动参数，宿主为0.111.0（commit `41cef1ea`），引擎为MegaDot 4.5.1-m.14，单屏3840×2160、DPI 192。原设置为`fullscreen=true`、窗口尺寸3840×2019、位置(-1,-1)。日志在全屏初始化后记录1918×1080、1944×1151、3840×2160三次尺寸变化，截图实际窗口偏移/裁切、没有可拖动的标题栏。

这些证据不能单独确定是宿主、缩放还是其他模组造成偏移。本次修复是恢复可用的原生窗口设置，不声称修复引擎根因，不加入模组全局显示Patch。

原生依据：`Reference/Vanilla/0.111.0/src/Core/Nodes/NGame.cs`的`ApplyDisplaySettings`、`ToggleFullscreen`及`NFullscreenTickbox.SetFullscreen`。位置(-1,-1)由宿主居中；Windows的Alt+Enter调用同一原生切换入口。实际游戏`--help`确认`--windowed`及`--resolution <W>x<H>`参数形式。

## 改动

- 按用户请求，仅将日常Steam的`settings.save`中`fullscreen`改为false、`window_size`改为1600×900。位置仍为(-1,-1)，resize仍为true；其他所有配置键逐项JSON比对未变。没有修改游戏安装、日常玩法存档、键位、音量、模组开关或Steam启动选项。
- 修正`Invoke-IsolatedBoot.ps1`：手动/后台渲染启动共用`--windowed --resolution 1600x900`；重置隔离窗口位置为(-1,-1)、允许调整尺寸。之前的手动分支没有窗口参数，后台分支使用不符合help形式的`--resolution=1600x900`。
- 添加仅测试驱动的`Display`场景：不进入跑局，检查启动窗口、原生全屏、返回窗口的实际模式/边框/尺寸/屏幕范围。测试代码不进入运行包；生产DLL/PCK未改、未重新安装或发布。

日常配置备份与收据：`build/window-repair/20261007-162456-482/`。修复时确认日常游戏未运行；已有后台音频测试完成后才启动本次验证，没有中断已有进程或抢前台。

| 配置 | SHA256 |
| --- | --- |
| 修改前，可恢复的`settings.save.before` | `c68ddd9d0ff66164aa19fe310ee4c6fe9dd561aa42a3de821c8b1eaec7bf52bf` |
| 修改后`settings.save` | `73e00fb2dd4b6133dc008a584a3ce6dc7b5c05d09f3d391e46aecac390cd5a44` |

## 验证

PASS：`node tools/verify-project.mjs`；PowerShell语法解析；`python tools/verify-migration.py`（1739份静态参考散列未变）；preview测试驱动与后台helper编译（0警告、0错误）。

两轮真实游戏均在不活动桌面、隔离档案运行，游戏EXE与日常安装EXE的SHA256相同：`8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231`。

1. `pwsh -File tools/Invoke-IsolatedBoot.ps1 -CommandScenario Display -Seconds 35`：`build/smoke/preview/command-flow-20261007-162628-590/localappdata/display-proof.json`，`DOHNA_SMOKE_DISPLAY_PASS`。
2. 同一隔离安装，不传`--windowed`或`--resolution`，只使用原生已修复的窗口设置：`build/window-repair/20261007-162456-482/no-window-args/localappdata/display-proof.json`，`DOHNA_SMOKE_DISPLAY_PASS`。

两轮结果相同：

| 状态 | 原生模式 | 实际位置 | 实际尺寸 | 边框/调整尺寸 |
| --- | --- | --- | --- | --- |
| 启动窗口 | Windowed | (1120,630) | 1600×900 | Borderless=false、Unresizable=false |
| 原生全屏 | Fullscreen | (0,0) | 3840×2160 | 与整个屏幕位置/尺寸一致 |
| 返回窗口 | Windowed | (1120,630) | 1600×900 | Borderless=false、Unresizable=false |

本轮验证只保留DohnaDohna、RitsuLib及测试驱动。前一次隔离测试额外加载的NinjaSlayer被启动器可恢复地移到`build/smoke/preview/disabled-test-mods-20261007-162627-478/`，日常模组目录未动。

NOT RUN：日常Steam前台启动、真实鼠标拖动/Alt+Enter、日常全部模组组合、多屏、其他DPI、stable实机和macOS/Linux。隔离的模式/几何断言通过不冒充这些验收；请用户重新打开日常游戏确认。
