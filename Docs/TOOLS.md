# 本机工具

全部迁入的工具保存在 `../Tools/STS2/`。Godot 4.5.1 Mono 使用自包含模式，Windows 导出模板位于其 editor_data/export_templates 内，官方 SHA512 已核对。FMOD Studio 为 2.03.06；完整界面尚未手动验证。

Python 图像／音频工具使用 `../Tools/STS2/python-tools/Scripts/python.exe`，已包含 Pillow、numpy、scipy、OpenCV、soundfile、imageio。rg 15.2.0 在 ripgrep 子目录。Spine 编辑扩展可选，DohnaDohna 未启用它。

.NET 9、Node、Git、GitHub CLI、PowerShell 7、系统 Python、FFmpeg 使用目标机已有安装。工具精确路径见 ../Tools/STS2/toolchain.json。

两个宿主编译输入已在 ../Tools/STS2/hosts/stable 和 preview；DohnaDohna 的 .local/hosts.json 已配置新路径。它们只作私有编译引用，不是完整可运行游戏，不进入 Git 和模组运行包。

原项目四项技能按原字节复制；保留 NinjaSlayer 名称和内部路径是用户要求，未来由用户自行修改，不能视为已自动适配。
