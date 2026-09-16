# 第三方美术资产许可台账

本文件记录工程内 `Assets/Art/ThirdParty/` 下所有第三方资产的来源与许可。
**每次新增第三方资产必须同步在此登记**（来源 URL、作者、许可、下载日期、核验方式）。

## 许可速查

| 目录 | 包名 | 作者 | 许可 | 商用 | 署名 | 下载日期 |
| --- | --- | --- | --- | --- | --- | --- |
| `KayKit/Characters-Adventures/` | KayKit Character Pack: Adventures (1.0) | Kay Lousberg | CC0 1.0 | ✅ 免费 | 非强制（欢迎） | 2026-09-15 |
| `KayKit/Characters-Skeletons/` | KayKit Character Pack: Skeletons (1.0) | Kay Lousberg | CC0 1.0 | ✅ 免费 | 非强制（欢迎） | 2026-09-15 |
| `KayKit/Dungeon-Remastered/` | KayKit: Dungeon Remastered (1.0) | Kay Lousberg | CC0 1.0 | ✅ 免费 | 非强制（欢迎） | 2026-09-15 |
| `KayKit/Furniture-Bits/` | KayKit: Furniture Bits (1.0) | Kay Lousberg | CC0 1.0 | ✅ 免费 | 非强制（欢迎） | 2026-09-15 |
| `KayKit/Medieval-Hexagon/` | KayKit: Medieval Hexagon Pack (1.0) | Kay Lousberg | CC0 1.0 | ✅ 免费 | 非强制（欢迎） | 2026-09-15 |

## 核验记录（2026-09-15）

- **核验方式**：从作者官方 GitHub（`KayKit-Game-Assets`）下载源码包，逐包打开随包
  `LICENSE.txt` 原文确认。五份 LICENSE 均声明：
  > License: (Creative Commons Zero, CC0) — http://creativecommons.org/publicdomain/zero/1.0/
  > This content is free to use in personal, educational and **commercial** projects.
- **CC0 含义**：进入公有领域，可商用、可修改、可再分发，**无任何强制署名要求**。
- 作者请求（非义务）：如条件允许，在制作人员名单致谢 "Kay Lousberg, www.kaylousberg.com"。

## 来源 URL

| 包 | 官方仓库 | 分发页 |
| --- | --- | --- |
| Adventures | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 | https://kaylousberg.itch.io/kaykit-adventurers |
| Skeletons | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 | https://kaylousberg.itch.io/kaykit-skeletons |
| Dungeon Remastered | https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 | https://kaylousberg.itch.io/kaykit-dungeon-remastered |
| Furniture Bits | https://github.com/KayKit-Game-Assets/KayKit-Furniture-Bits-1.0 | https://kaylousberg.itch.io/kaykit-furniture-bits |
| Medieval Hexagon | https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0 | https://kaylousberg.itch.io/kaykit-medieval-hexagon-pack |

## 格式说明

- 官方包为 glTF/GLB 格式（面向 Godot）。glTFast 导入在批量验证中不可用，已改用
  **Blender 5.2.1 无头模式批量转换为 FBX**（Unity 原生支持，骨骼与动画完整保留，
  Knight.fbx 含 152 个动画片段），转换脚本与清单见 `/tmp/kaykit-stage/gltf2fbx.py`。
- 已从 Assets 中移除原始 .glb/.gltf/.bin（保留 .fbx / 贴图 / LICENSE.txt），
  并从 `Packages/manifest.json` 移除了 glTFast 包。
- 角色（Knight、Barbarian、Mage、Rogue、Skeleton × 4）均带骨骼与动画 clip。

## 引入其他资产的规则

1. **仅接受** CC0 / CC-BY / MIT 等明确允许商业使用的许可；CC-BY 必须能履行署名。
2. 下载时**必须**保留随包许可文件到资产目录内。
3. 在本文件登记一行，并在「核验记录」补充核验方式与日期。
4. 禁止引入：「仅限个人使用」「仅限编辑器使用」「禁止 AI 训练用途未声明且来源不明」的资产。
