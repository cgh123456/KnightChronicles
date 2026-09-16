# 骑士异闻录 Unity 工程

这是游戏的正式 Unity 工程根目录，目标编辑器版本为 **Unity 2022.3.62f1 LTS（Apple Silicon）**，渲染管线为 URP。

## 打开项目

1. 在 Unity Hub 的 **Installs → Install Editor → Archive** 中选择 `2022.3.62f1`、`Apple Silicon`。
2. 在 Hub 的 **Projects → Add** 中添加本目录。
3. 首次打开时让 Unity 解析 `Packages/manifest.json`；它会生成 `Library/`、`Packages/packages-lock.json`、`.meta` 文件和默认 ProjectSettings。
4. 首次打开后的编辑器引导会自动创建 `Assets/Settings/KnightChroniclesURP.asset`、绑定 URP，并将 `Lobby` 写为默认构建场景。
5. 打开 `Assets/Scenes/Lobby.unity`，进入 Play Mode，即可查看首页运行时实现。

## 当前首页范围（FR-1102，B 路线：预渲染 2.5D）

> 2026-09-16 决策：放弃运行时 3D，改为 **2.5D 预渲染 sprite** 路线（老暗黑/幸存者类做法）。
> 3D 建模量为零——全部角色/场景来自 KayKit CC0 资产，用 Blender 烘焙成 2D 序列帧，
> 运行时是纯 2D sprite 游戏，彻底绕开蒙皮/骨骼/导入管线问题。SRS 的俯视角玩法不变。

首页按「数据 → 清单 → UI」三层实现：

- **数据层 `HomeProfileService`**：宝石、档案统计、最近战绩、未完成远征快照的单一来源（R-1102-6），事件驱动。
- **清单层 `HomeMenuModel`**：一级入口清单、可见性与默认焦点推导（R-1102-1 / R-1102-9），纯 C# 可测。
- **UI 层 `HomeMenuController`**：UGUI 首页，五个页面状态（Normal/Loading/SaveFailed/Modal/Transitioning）：
  骨架屏（R-1102-5）、空战绩态（TC-1102-3）、键盘导航（R-1102-3）、放弃远征二次确认（R-1102-10）、
  存档失败警告条（TC-1102-10）、宝石滚动动画、连点锁定（P-5）、红点与帮助徽标。
- **环境层 `HomeLobby2D`**：正交 2D 相机 + 卡通俯视大厅背景图 + 帧动画骑士 sprite（含极缓慢镜头浮动）。
- **帧动画 `SpriteSheetAnimator`**：运行时从图集按行切帧循环播放，行序即 8 方向（S/SE/E/NE/N/NW/W/SW）。

## B 路线资产管线

1. **烘焙**：`Blender --background --python /tmp/kaykit-stage/render_spritesheet.py -- <角色.glb> <动作名> <输出目录>`
   —— 俯视 50° 正交机位、透明背景，8 方向 × 12 帧拼成图集 + JSON 元数据（Workbench 引擎、TEXTURE 颜色模式）。
2. **入库**：图集与背景图放 `Assets/Resources/Art/Sprites/`（普通 Texture 导入即可，运行时 `Sprite.Create` 切帧）。
3. **扩展新角色/敌人**：对 `Characters-Skeletons`、`Dungeon-Remastered` 里的模型重复步骤 1-2 即可，无需任何建模。

角色动画问题排查备忘：KayKit 模型为 Q 版大头比例（约 1.2m），蒙皮 FBX 直转 Unity 有塌陷问题，
B 路线下不再使用 FBX/骨骼，全部动作经 Blender 烘焙为帧动画。
机位备注：ortho_scale=2.2（正交框必须大于人物 32° 仰角投影高度约 1.95 单位，否则 S/N 朝向会平切头盔顶）；图集内 boots 在上、头盔在下是俯视投影的正确形态。
动作挂载备注（2026-09-16 修复）：烘焙脚本必须把目标动作赋给 armature.animation_data.action 并清空 NLA 轨道，
否则 frame_set 驱动的是 glTF 导入后默认激活的动作——曾导致四套图集全部渲染成攻击循环（游戏内待机表现为反复挥剑）；
另在每帧采样后强制回写骨架朝向旋转，防动作曲线覆盖八方向朝向。修复后已逐帧验证：待机两帧姿势一致无挥剑、行走 S/E/N 三行朝向各不相同。

## 冒险者小镇（占位版，2026-09-16）

- 场景 `Assets/Scenes/Town.unity`（构建列表第 2 位），由 `TownBootstrap` 在 Play Mode 程序化搭建：
  草地/十字路径/广场色块、5 座功能建筑占位（铁匠铺、炼金商店、旅馆、杂货铺、远征之门，
  色块 + TextMesh 标签 + BoxCollider2D 碰撞 + 触发区交互点）。
- `TopDownPlayerController`：WASD/方向键移动（Rigidbody2D 速度驱动，碰撞由物理解决），
  位移向量自动映射八方向朝向（SpriteAnimator 行号），行走/待机动作自动切换；
  提供 `InputProvider` 钩子供测试注入输入。
- 交互：走近建筑触发区显示「按 E 进入××」，按 E 弹占位提示；Esc 返回首页。
- 首页「开始远征」与「更换角色」当前直接进入小镇（FR-1103 选角页接入前的占位流程）。
- 已知占位项：建筑均为色块（小镇正式模型待输出后替换）；交互后仅提示，未接功能页。
- 测试：`TownSmokeTests` 验证移动/朝向/动作切换（脚本化物理步进保证批处理确定性）。

## 验收与测试

- EditMode 测试位于 `Assets/KnightChronicles/Tests/HomeMenuModelTests.cs`，覆盖默认焦点、继续游戏入口、放弃远征、红点、帮助徽标、存档失败重试等 FR-1102 用例。
- 运行方式：编辑器打开工程后 **Window → General → Test Runner → EditMode → Run All**。

## 编辑器版本（已解决）

- 工程使用 **Unity 2022.3.62f1（Apple Silicon）**，已通过 Unity Hub CLI 安装在 `/Applications/Unity/Hub/Editor/2022.3.62f1`。
- 注意：**不要升级到 2022.3.63f1 及以上**。该系列自 63f1 起进入 Extended LTS，只接受 Industry / Enterprise 许可证，Personal 许可无法激活（本机曾误装 64f1 导致无法启动，现已删除）。
- 工程已完成首次导入，URP 资产（`Assets/Settings/KnightChroniclesURP.asset`）与构建场景（`Assets/Scenes/Lobby.unity`）配置就绪；EditMode 测试 12/12 通过。

## 目录约定

```text
Assets/
  Art/Home/                  # 原始可编辑美术导入区
  Resources/Home/            # MVP 首页运行时背景
  Scenes/                    # Unity 场景
  KnightChronicles/Runtime/  # 运行时代码
  KnightChronicles/Editor/   # 编辑器工具
  KnightChronicles/Tests/    # 自动化测试
  Settings/                  # URP、输入和游戏配置资产
```
