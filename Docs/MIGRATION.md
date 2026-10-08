# 迁移说明

用户最终要求完整迁移，并统一放入 ninja5080 的 C:\Users\theon\Documents\STS2 MOD。DohnaDohna 是独立空模组，NinjaSlayer/NinjaSlayer 是最新发布源码，二者不会互相覆盖。

DohnaDohna 的 1,739 份工程参考保持迁移清单中的原字节；脚本入口有 .reference 后缀。双版本原版源码 16,676 份已校验。项目技能共 18 个文件按原样复制，最初的适配草稿只留 .local-reference。

NinjaSlayer 已迁入完整 Git 历史、分支、标签和引用；主工程检出 main@20d73608d4e851b95d4af5550b287041a42f69e3（1.0.12 发布源码）。10 份有未提交修改的旧工作树在 NinjaSlayer/migration-worktrees 独立恢复，staged/unstaged 补丁和原路径在 migration-evidence/worktree-state；没有把旧工作树覆盖到 main。

FMOD 编辑项目、原始图片／音频／视频、小说语料、素材库、制作成果及有意归档均纳入迁移。大型素材分批传输并逐文件 SHA256 校验；迁移完成前不能将目录存在视作数据完整，最终以 NinjaSlayer/migration-evidence 下的收据为准。

未迁移账号令牌、私钥、浏览器认证、系统用户配置；可重建的依赖环境、编译缓存、重复隔离游戏安装不作为源资料搬迁。没有修改正常游戏或存档，没有推送、发布、上传遥测，来源电脑原件保留。

历史文档里的旧绝对路径属于出处，不整体改写；使用制作工具时按根目录 SOURCE-PATHS.md 定位新输入。技能原稿保持不变。原项目与第三方的现有许可均保留。
