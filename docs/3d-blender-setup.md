# 3D 建模环境说明（Blender + cc-blender-skill + blender-mcp）

> 2026-09-15 由 ZCode 安装配置并验证通过。

## 装了什么

| 组件 | 位置 | 说明 |
|---|---|---|
| Blender 5.2.1 LTS | `/Applications/Blender.app` | 从清华 TUNA 镜像下载安装 |
| cc-blender-skill v1.3.0 | `~/.zcode/repos/cc-blender-skill/` | 30 个建模 skill 本体 |
| skill 软链接 | `~/.zcode/skills/blender-*` 等 30 个 | ZCode 每次会话自动加载 |
| blender-mcp MCP 服务器 | `~/.local/bin/blender-mcp`（uv tool 安装） | ZCode 与 Blender 通信的桥 |
| blendermcp 插件 | `~/Library/Application Support/Blender/5.2/scripts/addons/blendermcp.py` | 已启用并持久化 |
| MCP 配置 | `~/.zcode/cli/config.json` → `mcp.servers.blender` | stdio，指向已安装的可执行文件 |

## 核心技能

- `text-to-blender`：总编排器，自然语言 → Blender，自动链式调用子技能
- `blender-modeling` / `blender-materials` / `blender-lighting` / `blender-cameras` / `blender-rendering` / `blender-animation` / `blender-export`
- `reference-to-3d` / `wireframe-to-3d`：参考图 / 线稿转 3D
- `blender-pro-workflow`：完整产品级流程（草模→相机→灯光→材质→渲染→导出）

## 使用方法

1. **打开 Blender**（插件会在启动时自动开 9876 端口服务器）
2. **重启 ZCode 会话**（MCP 服务器在会话启动时连接，改配置后需重启才生效）
3. 直接用自然语言提需求，例如：
   - 「用 Blender 建一把骑士长剑，三点布光渲染」
   - 「把这个场景导出成 GLB 给 Unity 用」

## 验证记录

- socket 直连 9876 端口模拟 MCP 调用：ping / execute_code / get_scene_info 全部成功
- 按 `blender-modeling` skill 配方生成骑士剑（长 78cm、宽 4.5cm、剑尖收尖、部件重叠消缝、GEO- 命名）
- 产物：`assets/models/GEO-sword.glb`（50KB，glTF 2.0）+ `GEO-sword_preview.png`
- 验证脚本保留在 `tools/test_sword.py`，可重复运行

## 已知限制

- 插件服务器**必须在 Blender GUI 模式下运行**（`blender -b` 无头模式会被插件拒绝）
- 程序化建模适合武器/道具/场景摆件；高审美要求的角色仍需人工雕刻打磨
- 真实感 PBR 材质进阶（Programmatically 高级材质、HDRI）走 `blender-materials` / `blender-lighting` 的完整配方

## 武器品质分级与资产清单

品质阶梯：**普通 → 精良 → 稀有 → 史诗 → 传说**（命名前缀对应 GEO-knight_ / GEO-fine_ / …）

| 品质 | 资产 | 文件 | 特征 | 状态 |
|---|---|---|---|---|
| 普通 | 骑士剑 | `GEO-knight_sword.glb` / `.blend` | 抛光钢 + 黄铜 + 皮革，无特效 | ✅ 定稿封存 |
| 精良 | 精良长剑 | `GEO-fine_sword.glb` / `.blend` | 大马士革暗钢 + 符文刻纹（发光）+ 鎏金翼形护手 + 漆器绯红握柄 + 红宝石配重 | ✅ 定稿封存 |
| 传说 | 传说长剑「金焰」 | `GEO-mythic_sword.glb` / `.blend` | 玄黑镜钢金流纹 + 刃口流光 + 符文环阵列 + 血槽菱形宝石阵 + 三层展翼护手龙珠端头 + 黑檀金丝握柄 + 三段式焰橙宝石配重，42 部件 | ✅ 定稿封存 |
| 稀有/史诗 | 待做 | — | 介于精良与传说之间 | ⬜ |

约定：定稿资产不再修改；新武器新开部件与文件名。GLB 为 Y-up 含基础材质（程序化纹理不导出，引擎内需重制或烘焙贴图）。

## 事故复盘（传说剑护手区 8 轮返工）

部件悬空/穿模/埋地/比例减半的根因与防御规则，已沉淀为 skill `blender-mcp-pitfalls`（`~/.zcode/skills/blender-mcp-pitfalls/`），核心四条：

1. `size=1` 立方体的 scale 值就是目标全长（不是半长）
2. 本环境下 `transform_apply` 会把 location 烘进顶点并归零 origin，之后改 location 全是叠加——部件会悄悄漂移
3. 多部件衔接必须链式布局（从相邻部件实测 bbox 推导，重叠 4-6mm），禁止手填坐标
4. 位置改动后渲染前先打印 evaluated bbox 核对；位置 bug 两轮修不好就整体重建
