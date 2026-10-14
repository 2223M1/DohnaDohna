# 情感图标遗物素材

十名角色各有普通与金色强化两版。普通版保留原画配色，强化版在主体内使用静态金色渐变映射；两版共用构图、缩放、Alpha 和外描边。制作入口为 `tools/Prepare-RelicArt.py`，无生成式模型、运行时滤镜或新增 Patch。

## 本地输入与复现

原作资料根目录由 `.local/original-reference.json` 的 `referenceDirectory` 提供。脚本读取其中 `assets/images/current/界面与图标/情感图标/` 的十张 360×360 PNG，并与 `assets/manifest.csv` 中 `dohnadohnaCG.afa` 对应条目的 SHA256 核对。原图不被覆盖。

描边参照来自本机 `../NinjaSlayer/Slay the Spire 2/Slay the Spire 2 v0.111.0/` 导出中的 `anchor`、`bag_of_preparation`、`burning_blood`、`oddly_smooth_stone`、`meal_ticket`：读取 `images/relics/` 大图，以及 `images/atlases/relic_atlas`、`relic_outline_atlas` 的图集与区域。这里只把宿主图片作为视觉依据，未使用旧项目脚本或发布绑定。

```powershell
& ../Tools/STS2/python-tools/Scripts/python.exe tools/Prepare-RelicArt.py --install-assets
```

`--install-assets` 只写本工程被 Git 排除的 `DohnaDohna/roles/` 素材目录；打包和游戏安装是其他步骤。省略此开关只输出素材包。可用 `--original <原作资料根>`、`--vanilla <宿主导出根>` 指定输入；`--output` 必须是本工程 `build/` 下的独立子目录。`--review-mattes` 只制作主体与蒙版审阅图。

本次环境：Python 3.12.10、Pillow 12.3.0、NumPy 2.5.3、SciPy 1.18.1；审阅图文字使用 Windows `msyh.ttc`。无随机过程。PNG 输出和 ZIP 条目顺序固定，ZIP 条目时间固定；收据中的路径随工作目录变化。

## 分离、描边和金色处理

外缘连通区域识别贴纸白色及透明阴影，白色条件为各通道至少 246、色差不超过 12。白色衣物、眼白、高光及道具填充由外围线稿包围，因此保留。边界约 1.5 原始像素范围根据白色底恢复覆盖率，去除抗锯齿白色污染；内部 RGB 检查为原值。每张保留 `foreground.png`、`keep.png`、`alpha.png`、`removed.png`。

经逐张放大核对，以下额外连通种子位于被线稿包围的空隙，坐标使用原始 360×360 图：

| 角色 ID | 空隙坐标 (x, y) |
| --- | --- |
| kuma | (180, 280) |
| alyce | (117, 227) |
| kikuchiyo | (244, 270) |
| medhico | (253, 163)、(255, 193) |
| zappa | (123, 238) |
| kirakira | (116, 152) |

其余角色使用外缘连通。小丑的白色帽衬及原图道具像素、菊千代和扎帕的文字、各角色独立装饰均保留。修改种子前必须对照原图确认，不能按颜色全局删除白色。

主体按同一个透明包围盒居中，256 画布的主体最长边为 224 像素。所有缩放先使用预乘 Alpha 采样，再导出直通 Alpha。外轮廓使用 8 倍采样与距离扩张，大图外扩半径 10.5 像素，小图 3.0 像素；外描边颜色为黑、Alpha 为 128。半透明平涂和边缘过渡直接校准原版数据，不添加额外颗粒。五个参照的大图描边中位宽度约 10.2–10.6 像素；原版小图轮廓的重建 IoU 为 0.98698–0.99564，详细值在收据中。

金色映射使用原图亮度保留线稿、五官和明暗。七级颜色从深棕 `(13,10,7)` 经过饱和金色 `(192,133,30)` 到浅金 `(255,247,204)`，叠加两条柔和静态反光；金色占 88%，原色占 12%。处理发生在主体上，再共用普通版轮廓合成。导出前检查普通／强化版 Alpha 完全一致，外描边有效像素一致，画布边缘无裁切。

## 导出资源与接入

输出根目录为 `build/relic-art/`。每名角色的运行资源映射如下：

| 文件 | 尺寸 | 用途 |
| --- | --- | --- |
| `relic_icon.png` / `relic_icon_plus.png` | 85×85 | 普通／强化主体，不预合成外描边 |
| `relic_outline.png` | 85×85 | 两版共用的白色 Alpha 轮廓蒙版 |
| `relic_big.png` / `relic_big_plus.png` | 256×256 | 普通／强化详情大图，已合成半透明黑描边 |
| `relic_refinement.png` | 500×380 | 金色升级选择卡图，360 母版居中于 `(70,10)`，底色 `#253044` |

`SquadRoleRelic.AssetProfile` 使用 `res://DohnaDohna/roles/<角色ID>/`，由 `Multiplier == 2` 选择 `_plus` 文件。原生 `NRelic` 为小图轮廓设置黑色、`128/255` Alpha；大图关闭独立轮廓。不得将带描边母版直接代替小图主体，否则会叠加两次。`SquadRelicUpgradeChoice.AssetProfile` 使用对应角色的 `relic_refinement.png`。模型 ID、名称、效果与升级映射保持原有定义。

`masters/` 提供二十张 360×360 透明成品母版，`work/` 保存四十张主体与蒙版，`runtime/` 保存六十张运行素材。交付 ZIP 包含这些文件、对照总览、README 和收据。`receipt.json` 记录生成器、十张原图、五个原版参照、参数和八十张成品输出的 SHA256；`SHA256SUMS` 记录完整素材 ZIP 的 SHA256。

人物与原画来源 Alicesoft，描边参照来源 Mega Crit。商业图片、蒙版、原版参照和成品包保留在本地排除目录；公开源码只保留处理工具与复现说明。实机证据和未执行范围见 `Docs/validation/relic-art-20261009.md`。
