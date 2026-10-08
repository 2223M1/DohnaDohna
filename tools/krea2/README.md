# DohnaDohna Krea2 角色训练

十个独立角色 LoRA，沿用 ModelScope Raw → Musubi native → Turbo 验收。原作素材、训练包、权重、日志全部留在 `D:/AI/ComfyUI-aki/training/dohnadohna-krea2-v1`；本目录只存工具。运行时不改 Mod，不替换卡图。

当前是数据与工具准备状态，不能称为十个模型交付。真实提交状态以外置 `configs/<id>.json` 为准；模型可加载、训练结束和视觉验收是三个不同结论。

**2026-10-09 用户已批准全部选图并执行训练，随后明确要求头像裁片改为完整原图。** 当前有效运行目录是外置 `revisions/official-gyokai-v2/training-all-v2/`，不要再用根目录v1配置判断当前任务。137张图全部进入训练，原28张验证图不再是独立留出集；3张珀尔诺头像已替换为对应完整原图，旧裁片仅在archive留证。图片、Caption、ZIP及来源散列均已核验。

阿熊任务 **183325**（13图）、爱丽丝任务 **183327**（15图）已真实提交，各6000步、24魔粒；提交前余额386，提交后338。实际参数与未开放参数记于当前运行目录的`configs/*.json`。两项任务尚未交付权重或通过本机视觉验收，其余八人包已准备并等待先导验收。心跳自动跟进ID为`krea2`，每30分钟继续本任务，不重复提交已有任务。

v1保持冻结：阿熊旧任务183237已取消，最后观察到30/6000步，没有可用检查点，扣费/退款未确认。旧取消任务仍计一次，因此183325已是阿熊第二次云端任务，不可擅自提交第三次。

## 2026-10-08 文档核对与参数决定

| 证据 | 能支持的结论 | 不能推出的结论 |
| --- | --- | --- |
| [ModelScope 训练页](https://www.modelscope.cn/aigc/model-training/)：选 Krea 2 后推荐档位 6000，步数输入 1000–10000、步进 1000 | 6000 是平台推荐实验配置；提示说明 Raw 训练、Turbo 推理 | 每个小数据集的最佳检查点都是第 6000 步 |
| [ModelScope 公共预设](https://www.modelscope.cn/api/v1/muse/train/queryPreset?modelType=IMAGE)，KREA_2_TURBO / 854890 | rank/alpha 32，AdamW 1e-4，batch 2，repeat 10，1024 分桶64，cosine restarts 3 | 预设不是实际任务参数；其中 steps 为 null，epoch20、saveEveryNEpochs5、shuffleCaption=true 与本地要求有差异 |
| [Krea 官方开放模型仓库](https://github.com/krea-ai/krea-2) | Raw 训练、Turbo 推理；Turbo 8步、关闭CFG、mu1.15 | 没有统一6000步角色训练配方 |
| [Krea 官方训练说明](https://www.krea.ai/blog/krea-2-lora-training) | 单一主题，准确 caption，多角度/表情/背景，从默认配置起试 | 托管 Medium/Large 与 ModelScope 开放 Raw 不能直接等同 |
| [Krea 技术报告](https://www.krea.ai/blog/krea-2-technical-report) | 去重复、去伪影、精确描述有价值 | 基础模型预训练实验不是小型 LoRA 的最优超参数证明 |
| [Musubi Krea2 文档](https://github.com/kohya-ss/musubi-tuner/blob/main/docs/krea2.md) | Raw、rank/alpha32、全264 Linear；1e-4、16epoch示例；每epoch保存；明确说最优设置尚未建立 | 不能把16epoch直接解释成固定步数，也不能把 attention-only 配方套到当前转换器 |
| [社区角色训练复现](https://huggingface.co/JahJedi/krea2-character-lora-recipe) | 1e-4、rank32、逐epoch筛选；127图/2032步与822图/13152步两种规模；caption只写可见特征 | 大型VRChat数据和正则化集不证明4–12张原作图需要同样步数 |

已保存官方 README、公共预设与本机环境散列到外置 `research/`、`validation/`。社区报告是经验，不是受控对照试验；不引用搜索引擎 AI 摘要为证据。

本轮取消旧的 `8 × ceil(10 × N / 2)` 步数公式。两名先导的候选上限改为6000，期望参数保持32/32、AdamW 1e-4、batch2、repeat10、1024分桶、seed42；实际接受情况单独记录。10月8日其余八人按4000步预留额度；10月9日余额增至386后，当前包把其余八人的6000步列为候选预算，仍须先导验收通过后决定实际配置。6000不是验收结论或固定最终检查点。

先导初筛目标是接近1000、3000、6000的三个实际保存点；如果后期过拟合，向更早的保存点回查。批大小2时，6000步相当于每图约 `12000/N` 次呈现，当前小数据集存在明显过拟合风险。epoch数是按repeat估算的派生值，分桶会影响实际epoch步数；实际训练日志优先。已在历史MS2000任务页面确认c1-st1000、c1-st2000，证明平台提供中途保存点；新任务的实际频率尚未暴露，不能保证每epoch保存。新任务需记录实际可下载步数；少于三个则明确为验收缺口，不伪造检查点。

界面可见的参数与隐藏预设分开记录。当前 UI 显示可编辑步数、1024分辨率、rank、AdamW、学习率、随机种子与保存精度；batch、alpha、shuffle、scheduler、保存频率尚未在专业参数窗看到。要求关闭caption改写与打乱，但不能把本地JSON当作平台已接受的证据。caption导入需逐图检查。

页面出现“开始免费训练”，但实际消耗社区魔粒；高阶训练关闭。账户按钮图标名为Magic Cube，点开明确显示“我的魔粒200”，因此先前称200魔方不准确。阿熊10图、1024、rank32实际报价：6000步24魔粒，4000步16魔粒。按当前报价推算，两人6000加八人4000为176魔粒，余24；十人6000需240，超出现有余额。各任务仍需重新报价。不绑定阿里云付费服务，不充值，优先保障首轮；历史2000步16魔方不作当前报价。

## 素材决定

- 使用可追溯官方图及经核实的原画师鱼介作品，包括官网海报、宣传图、授权周边和画师公开作品。用户接受不足20张；不加入无关同人、生成图、连续帧或重复网站立绘来补数。卡牌继续追溯完整原画；用户已允许独立包装新绘保留文字纳入审阅，不将其冒称无字原稿。
- 肢体完整度优先。完整人物不为去字而裁成头像；原画只有半身时明确标注。Tamatoys 五张斜拍盒面按厂家 H210×W145 mm 的实际比例校正四角透视及横向压缩，输出638×924；绮菈绮菈采用鱼介715×1000平面图，维持原比例。全部保留整个正面，原图、四角、参数、父文件散列和前后对照可追溯；没有生成补画或去字重绘。
- 原始Alpha正确合成；保留原始分辨率，不预先缩到960再放大。透明图留小安全边；按姿势组分配三种中性底色，每个源图仅输出一张。原有场景背景保留，不加人为边框。分桶由平台完成。
- 爱丽丝两张同构图、五官变形的生气差分移出。三种表情上限仍按姿势/服装约束；同身体、换装和裁切共享泄漏组。低分辨率动作图≤25%。
- caption记录实际可见服装、动作、表情、道具、背景；不对脸被遮挡、闭眼或低分辨率图标注想象中的眼睛细节。
- 成年素材的非露骨裁切单独审查。已检查四张梅蒂可面部裁切，因强烈表情偏差、局部遮挡、脸部不完整或有效分辨率低，首轮不采用；原始露骨全图不进训练包。是否采用依据裁切后的训练价值，不以数量为目标。
- v1安缇娜、绮菈绮菈、珀尔诺仅覆盖日常服装，这是旧筛选范围，不是用户要求。v2逐图复核服装和构图。多数角色缺少独立侧面/背面高分辨率图；验证素材独立但数量少。这些缺口必须随模型交付披露。

## 历史审阅提案与当前运行入口

批准前的本地 v2 审阅提案：共134张拟选文件（106训练、28验证），另有3张头部备选，不能当作134幅独立原画。已补入4张用户提供的鱼介X图，另外核实保存4幅X作品（2拟训练、2备选），以及6幅独立包装绘。包装无字原文件、2张卡牌对应原图仍未取得；X历史作品检索不保证穷尽。

137张审阅输出均有绑定输出SHA256的手足覆盖目视记录。拟训练中52张为全身构图；数量包含同姿势表情和低分辨率动作图，不能据此推断手指细节足够。珀尔诺只有1个全身来源组、没有独立全身验证图；包装六图均无足部。审阅页按角色列训练/验证覆盖，并可筛选全身、自身手部、足/鞋；旁人手部不算角色自己的手。

历史审阅页位于外置 `revisions/official-gyokai-v2/review/index.html`，其134拟选/3备选和覆盖统计是批准前快照。实际137张输入与任务状态见同目录`training-status.html`，由`training_status.py`读取当前运行收据重建。旧审阅manifest已复制到运行目录留证；批准及“不要头像裁片”修正见`approval.json`。当前revision上传/提交开关已依据用户明确批准开启，但其余八人仍受先导验收条件约束。

当前执行阶段只刷新进度页、核验训练包，不重建已提交数据。PowerShell，仓库根目录执行：

```powershell
$kreaPython = 'D:/AI/ComfyUI-aki/training/tools/musubi-tuner/.venv/Scripts/python.exe'
$approvedRun = 'D:/AI/ComfyUI-aki/training/dohnadohna-krea2-v1/revisions/official-gyokai-v2/training-all-v2'
& $kreaPython -X utf8 tools/krea2/dataset.py verify --root $approvedRun
& $kreaPython -X utf8 tools/krea2/training_status.py
& $kreaPython -X utf8 -m unittest discover -s tools/krea2 -p test_pipeline.py
```

`import_user_art.py`仅用于归档已经指定的4个本地附件；`Collect-Packaging.ps1`采集已核实厂家页面和盒面。勿为普通重建重复采集。`limb-coverage-audit.json`保存在外置v2目录，裁切或源像素改变后必须重做对应条目的目视核对；builder拒绝沿用散列不匹配的肢体结论。

`curate_v1.py`为历史选择记录，不再针对已冻结v1运行。`inventory`会重建候选清单，不可对v2直接运行并覆盖作者来源记录。当前训练包已使用实际服装覆盖及官方/鱼介来源策略。prepare_approved_v2.py为已执行的批准迁移入口，不得重复运行；其后“不要头像裁片”的更改已记入approval.json。后续上传仅用training-all-v2对应角色manifest及当前train目录，不得上传archive、review或旧packages。

`convert_lora.py --help` 提供严格转换入口。传入实际本机底模，计算真实SHA256；未知键、缺失模块、形状/秩错误、非有限数值直接失败。保留MS原文件与转换收据；文件名带角色、版本和真实步数。已通过MS2000历史参考转换检验，不代表新角色已训练。

`evaluate.py prepare` 只准备预设。填入三份真实检查点及转换收据后，先执行 `evaluate.py run --role kuma --stage screen`（以 `--help` 的实际选项为准）。人工逐图记录身份与可用性；选定后再跑18张正式图与6张对照。Turbo 1024²、8steps、CFG1、mu1.15，保持已有显存卸载。至少15/18身份可辨，动作与风格各2/3可用，无系统性串人或大面积复刻。失败图与原因保留；STS2风格叠加尚未实现/测试。

完成状态应同时具备十人独立权重、推荐权重/提示词、转换收据、验收图与运行记录；目前仅工具与训练包不能满足该标准。
