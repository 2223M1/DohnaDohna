# 原作本地逆向参考

《多娜多娜》的素材与逆向结果位于模组工程旁边的 `DohnaDohnaOriginal/`，而不是 `Reference/NinjaSlayer/` 或模组运行资源目录。实际本机路径见忽略提交的 `.local/original-reference.json`。

## 检索入口

| 需要研究的内容 | 本地参考包中的入口 |
| --- | --- |
| 游戏逻辑、函数与参数 | `reverse/scripts/function_index.csv`、`function_index.json`、`functions/` |
| 类型、全局状态、外部库接口 | `reverse/scripts/metadata.json`、`structures.txt`、`globals.txt`、`libraries.txt` |
| 技能、角色、道具及其他原作数据 | `reverse/data/current/data.x` 与其拆分表 |
| 不同数据版本 | `reverse/data/variants/`；不能将修改器版本误当作原始设计 |
| 界面与场景定义 | `reverse/data/pact/` |
| 图片、动画帧与差异版本 | `assets/images/current/`、`variants/`、`assets/manifest.csv` |
| 音频与映射数据 | `assets/audio/`、`reverse/data/sound/`、`voice/` |
| 原作角色语音表关联 | `assets/audio/voice/按角色/`、`角色用途索引.csv`、`缺失引用.csv`；依据当前版 `11_ボイス情報.x`，未覆盖的音频保留在 `未关联/` |
| Shader 行为 | `reverse/data/shaders/index.json`、`bytecode/`、`assembly/` |
| 本机引擎和运行库 | `reverse/native/`，先看各模块的覆盖率和限制 |
| 来源及完整性 | `reverse/source_manifest.json`、`archive_indices/`、`reports/summary.json` |

## 开发约定

- 原作参考用于确认行为、数值、素材和演出；不是 STS2 模组架构模板。实际接入以本工程根 AGENTS、当前宿主和 RitsuLib 公共 API 为准。
- 明确区分原作事实、修改器版本差异与模组设计适配。保留用于判断的源包、表项或函数编号，不凭名称猜逻辑。
- AIN 交付的是全量字节码与结构信息，不是原始 JAF/C++ 源码；原脚本没有 FNAM 源文件名表，禁止把猜测文件名当成来源事实。
- 当前与原版 EXE 的主要代码区呈加壳特征，现有静态输出主要是入口桩，不表示引擎已完整恢复。Shader 输出是 DXBC 与汇编，不是 HLSL 源码。
- 原作源码导出、对话、商业程序、完整素材和工具依赖留在本地外置包，不复制进版本库或直接发布。只引入经明确选用和适配的运行资源。
- 本轮没有增加角色、卡牌或玩法，没有安装、上传或启动游戏。资料导出与实际模组运行验证是两个独立阶段。

## 工具与验证

提取和验证入口为 `tools/original_game/extract_reference.py`，静态程序分析入口为 `run-native.ps1` 和 `ExportOriginalGame.java`。在参考包的隔离 Python 环境中运行，先执行 `inventory` 确认输入散列，再执行需要的阶段；不要覆盖来自不同原作版本的现有输出。

工具的单元测试覆盖分卷接缝、目录边界、路径安全、防覆盖、混合编码和图像分类。根工程验证与原有 1,739 份 NinjaSlayer 参考的散列验证仍独立执行；没有执行的宿主编译、实机或发布检查不得写为通过。
