# 骑士异闻录 · 任务计划与进度

最近更新：2026-09-16 17:55（Asia/Shanghai）  
维护者：Codex（当前任务执行者）、ZCode（WEAPON-001 起的 3D 建模任务）  
用途：任何协作者可从此处了解正在做什么、已完成什么以及接下来做什么。

> 本文记录当前协作范围，不代表全项目完成度。历史资产状态依据现有文件和说明整理；未经本轮重新运行的测试会明确标注。

## 当前概览

| 编号 | 任务 | 状态 | 当前进度 / 下一步 |
| --- | --- | --- | --- |
| TRACK-001 | 建立根目录计划、进度与待办入口 | 已完成 | 跟踪文档与协作规则已建立，已核对本地链接与变更范围 |
| CHAR-001 | 筛选可复用的免费可商用人物资产 | 已完成 | 已完成社区来源、许可与本地资产盘点；推荐现有 KayKit Knight |
| CHAR-002 | 中世纪地下城卡通人物造型与预览 | 动作图集已交付待验收 | 2026-09-16 18:15 方案 A 配色经用户确认；走路/跑步/翻滚三状态 8 方向图集已烘焙入库，Unity 显示验证待做 |
| SWORD-001 | 指定概念图的长剑三维重建 | 已交付待验收 | 已有模型、渲染和验证报告；不等同于完全恢复隐藏结构或已完成游戏集成 |
| WEAPON-001 | Blender 建模环境搭建与武器品质阶梯（剑×3） | 已交付待验收 | 普通剑、精良剑已定稿封存；传说剑经多轮 bug 修复后用户未确认验收；经验已沉淀至 skill |
| GIT-001 | 将当前项目工作区上传至既有 GitHub 远程仓库 | 进行中 | 已核对 `main` 与 `origin/main` 同步、远程可达；正在登记并提交当前工作区全部受版本控制范围内的变更，随后推送 |

## TRACK-001 · 协作跟踪机制

负责方：Codex。最近更新：2026-09-16 17:41。  
目标：在执行任务前记录计划，执行过程中更新进度，方便他人随时接手。  
范围：只新增项目协作文档，不修改游戏代码、场景或美术资产。

- [x] 检查项目根目录、现有说明与 Git 工作区状态。
- [x] 建立根目录 `TASK_STATUS.md`，整理当前任务与后续待办。
- [x] 建立根目录 `AGENTS.md`，固化执行前、阶段中及收尾时的更新要求。
- [x] 核对文档链接与状态描述，完成交接。

产物：[任务跟踪](TASK_STATUS.md)、[项目协作规则](AGENTS.md)。  
验证结果：已检查引用的本地文件与目录均存在；Git 状态仅新增这两份文档，无游戏代码或资产变更。本轮未运行 Unity 或 Blender。  
阻塞：无。

## GIT-001 · 当前项目上传

负责方：Codex。最近更新：2026-09-16 20:25（Asia/Shanghai）。  
目标：将用户指定的当前项目工作区状态提交并推送至已有 GitHub 远程仓库。  
范围：当前 Git 工作区的已修改、删除及未跟踪项目文件，包括 Unity 场景/脚本、角色图集、相关资产、文档与工具；不包含已被 `.gitignore` 排除的 Unity `Library`、临时目录或构建缓存。

- [x] 读取协作规则、跟踪记录、Git 状态、当前分支与远程配置。
- [x] 复核 `origin/main` 与本地 `main` 指向相同提交，远程可访问，无待合并远端提交。
- [x] 检查仓库总体积（约 2.4 GB）及本次新增最大文件；本次待纳入文件均低于 GitHub 单文件 100 MB 限制。
- [ ] 将当前工作区变更加入暂存区并创建描述性提交。
- [ ] 推送提交至 `origin/main`，复核本地与远程提交一致。

产物：本任务的 Git 提交与 GitHub `origin/main` 远程分支。  
验证计划：推送后检查 `git status --short`、`git rev-parse HEAD` 与 `git ls-remote origin refs/heads/main`；确认工作区干净且本地、远程 SHA 一致。  
待办 / 阻塞：无；若服务器拒绝大文件、认证或非快进推送，将如实记录错误并保留本地提交，不执行强制推送。

## CHAR-001 / CHAR-002 · 人物资产复用与制作

负责方：Codex。最近更新：2026-09-16 17:41。

### 目标与已确认约束

- 用户要求：2.5D、地下城、中世纪、卡通人物；不需要过度精细。
- 用户优先级：先查社区是否有免费、可商用模型能够复用。
- 当前工程路线：按 [Unity 工程说明](unity/KnightChronicles/README.md)，使用三维资产预渲染为二维序列帧，运行时展示 sprite；不是运行时高精度三维角色。

### 已完成的研究

- [x] 盘点本地 KayKit 角色与配套资产。
- [x] 核对已有第三方许可记录和作者资产页面。
- [x] 比较 KayKit 与 Quaternius 的免费卡通奇幻人物资源。
- [x] 提出推荐：复用现有 KayKit Knight，优先保持骨架、动作与画风一致。

本地资源入口：

- [KayKit 冒险者模型目录](unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Adventures/Characters/gltf/)：含 Knight、Barbarian、Mage、Rogue、Rogue_Hooded。
- [本地冒险者许可](unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Adventures/LICENSE.txt)：CC0；本地包版本不应与作者最新发布版本混为一谈。
- [第三方许可台账](unity/KnightChronicles/Assets/Art/ThirdParty/THIRD_PARTY_LICENSES.md)。
- [现有 Knight 序列帧](unity/KnightChronicles/Assets/Resources/Art/Sprites/Knight_sheet.png)。

社区来源：

- [KayKit Adventurers](https://kaylousberg.itch.io/kaykit-adventurers)：首选人物来源。
- [KayKit Character Animations](https://kaylousberg.itch.io/kaykit-character-animations)：配套动作来源。
- [KayKit Skeletons](https://kaylousberg.itch.io/kaykit-skeletons)：同风格敌人来源。
- [Quaternius RPG Characters](https://quaternius.com/packs/rpgcharacters.html)：备选奇幻人物。
- [Quaternius Animated Knight](https://quaternius.itch.io/lowpoly-animated-knight)：备选单体骑士。

研究结论已反馈；没有因该次调研新增下载、替换模型或修改场景。推荐复用不等同于用户已选定最终人物造型。新增资产时仍须核对所下载文件的具体许可，不能将付费扩展内容计入免费范围。

### 后续计划与待办（尚未执行）

- [x] 明确本次人物的外形差异：头盔、肩甲、胸徽和主辅色；保持低复杂度卡通风格。
- [x] 以推荐的现有 Knight 为候选底模制作轻量造型预览，记录实际采用的方案。
- [ ] （待用户反馈配色方向后）确认最终方案，检查改动对骨架、武器挂点与动作的影响。
- [ ] 按当前俯视镜头检查轮廓、缩小后的辨识度和场景色彩适配。（预览阶段已完成 128px 缩图初检，最终确认待方案定稿）
- [ ] 需要入库时，沿现有预渲染路线输出方向与动作图集，核对尺寸、透明边缘、帧序与元数据。
- [ ] 如引入新第三方文件，更新许可台账并保留许可文本。（本次仅修改现有 CC0 资产的贴图副本，无新增第三方文件）
- [ ] 接入范围确定后，再验证 Unity 中的显示与动画；记录实际测试结果。

### 方案 A「秘银蓝 · 金徽」改版记录（2026-09-16 17:35，ZCode）

实现方式：部件级贴图变体（numpy 对 `knight_texture.png` 做色相替换生成副本，Cape/Body 挂各自变体材质，几何零改动，骨架/挂点/动作不受影响）。

- 披风：红 → 幽蓝紫（hue 0.68）
- 胸口红徽 → 金徽；腰带棕 → 金（红色系 mask 联动，视觉统一）
- 甲胄银灰 → 蓝调冷银
- 隐藏腰侧灰色挂盾（Badge_Shield）与多余武器挂件，仅保留右手 1H_Sword

产物：[改版目录](assets/char/knight_variant/)——`knight_variant.blend`、`preview_front/topdown50/angled35.png`、128px 缩图 `mini_128_*.png`、变体贴图 `TEX-knight_*.png`、原版对比 `_compare_base_topdown50.png`。烘焙脚本 `/tmp/kaykit-stage/` 实测仍在（临时目录，正式复现前建议纳入项目）。

验证：正视/俯视50°/斜35° 三机位渲染 + 128px 缩图初检（金徽金腰带可辨、与原版红披风明显区分）。**配色方向待用户确认后才进入序列帧烘焙。**

### 动作图集烘焙记录（2026-09-16 18:15，ZCode）

用户确认方案 A 配色，并要求增加走路、跑步、翻滚三状态。KayKit Knight 内嵌动作映射：走路=`Walking_A`、跑步=`Running_A`、翻滚=`Dodge_Forward`（10 帧）。

- [x] 烘焙脚本纳入项目：[tools/render_spritesheet_variant.py](tools/render_spritesheet_variant.py)（原 `/tmp/kaykit-stage/` 脚本 + 变体贴图应用 + 挂件隐藏 + 自定义输出前缀；解决临时目录依赖）。
- [x] 三状态 8 方向图集烘焙入库至 [Unity Sprites 目录](unity/KnightChronicles/Assets/Resources/Art/Sprites/)：`KnightVariant_Walking_A_sheet`（12 帧）、`KnightVariant_Running_A_sheet`（12 帧）、`KnightVariant_Dodge_Forward_sheet`（10 帧），均 256px/帧、fps 12、透明背景，附同名 JSON 元数据。
- [x] 抽帧验证：S/SE 方向帧完整，变体贴图（蓝紫披风/金徽）在图集中生效。
- [ ] Unity 内显示与 `SpriteSheetAnimator` 行序验证（打开工程后进行）。
- [ ] idle 状态图集（`Idle` 动作）未烘焙——原版 `Knight_sheet.png` 已含 idle 时按需补。

验证现状：本轮只复核工程说明，未重新运行角色渲染或 Unity 测试。  
待定信息：最终人物配色、装备外形与首批动作范围。它们是后续设计事项，不是本轮文档任务的阻塞。  
交接提示：工程说明中的烘焙脚本路径位于 `/tmp/kaykit-stage/`；正式复现前需检查脚本是否仍存在，并考虑将可复现脚本纳入项目，不能假定临时目录长期可用。

## SWORD-001 · 已有长剑资产交接

负责方：此前的 Codex 制作任务。最近更新：2026-09-16 17:41（状态整理）。

- [x] 已生成参考图重建模型与展示渲染。
- [x] 已有独立导入验证报告与复现脚本。
- [ ] 用户最终验收尚未记录。

产物与说明：[长剑资产目录及 README](assets/models/azure_reference_reconstruction/README.md)。

边界：单张正面图无法唯一确定背面与厚度；部分细刻纹和磨损来自参考图投影。该资产为展示精度分件模型，尚未完成游戏量产需要的 LOD、碰撞、绘制调用优化和引擎集成。以上后续工作不属于本轮任务，不自动启动。

验证现状：已有报告位于资产目录，本轮未重跑建模、渲染或导入验证。不把文件存在表述为本轮重新测试通过。

## WEAPON-001 · Blender 建模环境与武器品质阶梯

负责方：ZCode。最近更新：2026-09-16 17:55。

### 目标与已完成

- [x] 安装 Blender 5.2.1 LTS（清华 TUNA 镜像；GitHub 直连不稳定，仓库文件经 jsDelivr CDN 拉取）。
- [x] 安装 cc-blender-skill（30 个 skill 软链至 `~/.zcode/skills/`）与 blender-mcp（uv tool；MCP 配置于 `~/.zcode/cli/config.json`）。
- [x] 启用 BlenderMCP 插件（Blender 5.x 用户目录已迁移至 `~/Library/Application Support/Blender/5.2/`）。
- [x] 端到端验证：socket 直连 9876 模拟 MCP 调用，建模→渲染→导出全通。
- [x] 普通：`GEO-knight_sword`（抛光钢+黄铜+皮革）。已定稿封存。
- [x] 精良：`GEO-fine_sword`（大马士革暗钢+发光符文+鎏金翼形护手+红宝石配重）。已定稿封存。
- [x] 传说：`GEO-mythic_sword`「金焰」（玄黑镜钢金流纹+刃口流光+符文环+菱形宝石阵+展翼护手+三段式宝石配重；33 部件 / 15,582 三角面）。经用户指出多轮 bug（部件悬空、穿模、比例错误、构图偏移）后逐轮修复，当前版本衔接已全部经 bbox 实测验证。

### 产物

- [环境与资产说明](docs/3d-blender-setup.md)（安装位置、使用方法、品质阶梯、事故复盘摘要）
- 资产三件套：`assets/models/GEO-{knight,fine,mythic}_sword.{glb,blend}` + `_preview.png`
- 验证脚本：`tools/test_sword.py`（headless 可复跑）
- 经验沉淀：`~/.zcode/skills/blender-mcp-pitfalls/SKILL.md`（transform_apply 位置漂移、size=1 scale 语义、链式布局、bbox 验证纪律、depsgraph 缓存等 7 条铁律）

### 验证现状与边界

- 渲染为 Cycles 离线图；GLB 为 Y-up 含基础材质，**程序化纹理（大马士革纹、发光）不随 GLB 导出**，引擎内需重制或烘焙。
- 传说剑多轮返工后的当前版本用户**尚未确认验收**；再次反馈需按 skill 修复纪律处理。
- 尚未完成：LOD、碰撞体、纹理烘焙、Unity 集成——均未启动，不自动视为已授权。

### 后续待办（候选，未经授权不自动执行）

- [ ] 稀有/史诗两档补齐（当前跳档：普通→精良→传说）
- [ ] 传说剑符文发光烘焙为贴图导出（引擎可用）
- [ ] 武器类型扩充（斧/锤/匕首/法杖）
- [ ] Unity 导入验证

## 更新记录

| 时间（Asia/Shanghai） | 任务 | 记录 |
| --- | --- | --- |
| 2026-09-16 17:41 | TRACK-001 | 按用户要求建立根目录跟踪入口与协作规则；整理人物调研、后续制作待办及已有长剑交接状态。 |
| 2026-09-16 17:41 | TRACK-001 | 完成本地引用与变更范围检查，跟踪机制任务标记为已完成；人物制作仍为未开始。 |
| 2026-09-16 17:55 | WEAPON-001 | ZCode 接入登记：Blender 环境与三把品质剑（普通/精良/传说）已交付待验收；沉淀建模防御 skill；后续候选待办已列入条目。 |
| 2026-09-16 17:35 | CHAR-002 | ZCode 推进人物改版预览：Knight 部件级贴图变体（秘银蓝·金徽），三机位预览 + 128px 辨识度初检完成，配色方向待用户确认。 |
| 2026-09-16 18:15 | CHAR-002 | 用户确认配色并追加走路/跑步/翻滚要求：烘焙脚本纳入项目（支持变体贴图），三状态 8 方向图集已入库 Unity Sprites 并抽帧验证；Unity 显示验证待做。 |
| 2026-09-16 20:25 | GIT-001 | 用户要求上传当前项目：已确认 `origin` 为 GitHub 仓库 `cgh123456/KnightChronicles`，本地 `main` 与远程同基线，开始登记、提交和推送流程。 |

## 接手顺序

1. 阅读 [AGENTS.md](AGENTS.md)，查看本页的当前概览。
2. 对照实际文件核实准备接手的任务；先登记目标、步骤、负责方和验证计划，再开始实质性操作。
3. 每个阶段结束同步勾选进度，暂停时留下明确下一步；收尾记录产物与实际验证结果。
