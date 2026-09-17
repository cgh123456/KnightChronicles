# 骑士异闻录 Unity 工程

Unity **2022.3.62f1 LTS / URP**，Windows 本机编辑器在 `D:/软件/unity/2022.3.62f1`。采用 2.5D 预渲染角色、正交相机与 SpriteRenderer。

## 运行

从 Git 获取项目后，先完整解压 [Windows 分发包](../../builds/KnightChronicles-Windows.zip)，运行其中的 `KnightChronicles.exe`，保留同目录 Data、DLL 等配套文件。本地构建输出位于根目录 `builds/Windows/`，解压运行目录不纳入 Git。开发时 Unity Hub 添加本目录，打开 `Assets/Scenes/Lobby.unity` 后 Play。

首页进入小镇，到远征门按 E 选择难度出发。首局已配武器、胸挂、背包和药剂。

| 操作 | 默认按键 |
| --- | --- |
| 移动 / 瞄准 | WASD / 鼠标 |
| 小镇跑步 | Shift |
| 普攻 / 翻滚 | 鼠标左键 / 空格 |
| 交互、拾取、上下楼、撤离 | E |
| 搜索 | 靠近搜索点持续按 E 两秒；移动或受伤中断 |
| 胸挂前三格物品 | 1、2、3 |
| 已学习的武器主动技能 | Q、R |
| 背包 / 地图 / 暂停 | I / M / Esc |

仅 L2、L4 有撤离点；L5 可返回 L4。撤离结算金币、解锁与成长，物品入库（溢出仍保留随身）。死亡删除随身及穿戴装备，仓库和永久成长保留。死亡后小镇可重新购买容器。暂停返回首页恢复最近安全点。

小镇连接商店、出售、维护、仓库、打造、公会、小屋、技能树与三章剧情。背包支持单击详情、双击穿戴/转移，以及拖到装备槽或容器标签；仍遵守容量、职业门槛和远征限制。设置支持键位、音量、画质与视野。

## 构建与测试

编辑器菜单「骑士异闻录」提供配置与 Windows 构建。Lobby、Town 两场景参与构建；地下城根据存档在 Town 生成。

根目录 `tools/verify_unity.ps1 -Stage configure|edit|play|build` 异步启动编辑器，日志和 PID 在 `.work/validation`，同一工程只能顺序运行阶段。`tools/verify_core.ps1` 运行独立规则回归；`tools/export_item_catalog.ps1` 导出装备。

最新构建、实际回归与交付边界见根目录 `TASK_STATUS.md`、`docs/PLAYABLE_RELEASE.md` 和 `docs/ART_RELEASE.md`。QA 可用构建参数 `--knight-artcheck <输出目录>` 生成隔离存档的真实相机/GUI画面；仅此模式用离屏容器捕获隐藏窗口中的IMGUI，普通运行不使用该路径。

## 结构与存档

- `Runtime/Core/`：纯 C# 数据、规则、五层生成、成长、打造与校验原子存档。
- `GameSession` / `ProfileSerializer`：真实档案、事务、安全点及保留空引用的序列化。
- `DungeonController` / `GamePanel`：战斗、交互、结算和功能页。
- `Resources/Data/items.txt`：TSV 内容，625 件装备及 29 件辅助物品。
- `Resources/Art/Sprites/`：真正八方向待机/走/跑/翻滚/攻击/受击/施法，供小镇与地下城使用。
- `Resources/Art/World/`、`Enemies/`：三维模型烘焙的建筑/道具/四类商户及四类骷髅动作。
- `DungeonScenery`、`CombatFeedback`、`EnemyPresentation`：五层场景主题、脚点遮挡、道具碰撞、战斗反馈与敌人动作。
- `UiTheme`、`ItemIconAtlas`：原创暗石金边纹理、按类别/档位辨识的物品图标。
- `AudioDirector`：原创建成式合成占位音乐和音效。

玩家存档位于 Unity `Application.persistentDataPath` 下 `knight-profile.save`，包含校验与备份；测试使用独立目录。第三方许可台账位于 `Assets/Art/ThirdParty/THIRD_PARTY_LICENSES.md`。

可编辑 Blender 模型、制作管线及美术 QA 见根目录 `assets/models/world/README.md`。当前为可玩开发版本：本轮场景和动画升级已接入，音频制作、全量装备外观、人工试玩、性能长稳及全部 SRS 验收仍未完成。
