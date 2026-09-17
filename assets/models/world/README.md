# 世界模型与方向动画制作管线

本目录保存可编辑 Blender 源文件；游戏运行时读取透明 PNG，保持现有 Unity 2.5D 路线。这里的模型不是截图里的临时色块：每个资产包含独立网格部件、倒角与实际材质，建筑含屋面瓦片、木骨架、砌石、门窗、招牌和差异化店铺陈设。

## 产物

- `world_collection.blend`：35 个 `ASSET_<key>` 集合，按 7 列网格排列供检查。石木金属材质、灯光和烘焙相机保留。
- `world_manifest.json`：模型集合、网格数量、资源 key、相机角度与图片中心 pivot。侧墙变体额外登记。
- `stone_wall_side.blend`：原砌石墙绕 Z 轴旋转 90 度的独立烘焙源，避免把正面墙拉成长侧墙。
- `KnightVariant_animation_source.blend`：用户已确认的秘银蓝·金徽骑士，完整动作库、材质图片、方向父节点和烘焙相机。
- `Skeleton_{Minion,Rogue,Mage,Warrior}_animation_source.blend`：本地 KayKit CC0 骷髅、完整骨骼动作库、主要武器和打包贴图。
- `qa/`：PNG 方向与透明边缘检查报告、源文件检查报告，以及世界、方向、攻击帧联系预览；用于人工检查，不能替代实际游戏截图。

运行时资源位于 `unity/KnightChronicles/Assets/Resources/Art/World`、`Enemies` 与 `Sprites`。小镇 NPC 是小镇代理另行制作的资源，不属于 `world_collection.blend`，许可来源仍按下方记录。

## 重现

本轮实际采用官方 Blender 5.2.1 LTS Windows 便携版。官方下载目录：<https://download.blender.org/release/Blender5.2/>；ZIP 的 SHA256 为 `0e631dad7d0cad6d5d18abdd2e2550f6c0213215334eda00ddbd3d22b96ecb2c`，已与同目录官方 `.sha256` 清单匹配。仓库 `.work/art-tools` 下的便携包不作为发布资产。

```powershell
$blender = '.work/art-tools/blender-5.2.1-windows-x64/blender.exe'
& $blender --background --python tools/build_world_art.py -- (Get-Location).Path
& $blender --background --python tools/render_world_variant.py -- (Get-Location).Path
& $blender --background --python tools/render_character_art.py -- (Get-Location).Path all
& $blender --background --python tools/inspect_art_sources.py -- (Get-Location).Path
py tools/verify_art.py
```

角色可将 `all` 换成 `knight`、`minion`、`rogue`、`mage` 或 `warrior`，只重做指定角色。追加 `--sources-only` 仅更新可编辑源，不重渲染已有 PNG。生成 `world_collection.blend` 后再运行侧墙变体，后者会更新 manifest。

## 机位与接口

世界建筑/摆件和角色统一约 51.34 度正交机位，世界模型使用 Eevee、AgX 与柔和三点光；角色使用 Workbench 纹理/腔体/阴影，便于保留现有 KayKit 手绘风格。草/石/木地砖使用 90 度正俯视、矩形满幅，方便现有平面地图直接拼接。

`ModelArt.Sprite(key)` 加载整幅图；`ModelArt.Place(key,parent,pos,size,order)` 返回带 SpriteRenderer 的 GameObject，`pos` 是图像中心，`size` 是完整图片世界宽高。建筑图的前门并非统一物理碰撞范围，场景应按地面足迹设置碰撞。所有图片用 `.5,.5` 中心 pivot。

图集行序为 `S, SE, E, NE, N, NW, W, SW`。骑士帧为 256px：Idle/Walking_A/Running_A 各 12 列，Dodge_Forward 10 列，Attack/Hit/Cast 各 8 列。四类骷髅帧为 192px，idle/walk/attack 各 6 列。JSON 是列数、帧尺寸与 `orthoScale` 的真实元数据。`ModelArt.MotionPixelsPerUnit(path)` 返回 `frameWidth / orthoScale`，让不同镜头包围盒恢复一致模型世界尺寸；引擎接入需把它传给 SpriteAnimator.ActionSet。

方向由未参与动画的 `DirectionRoot` 父节点承担。直接改 `Rig.rotation_euler` 会在 render 重新求值时被动作覆盖，本轮已发现并修复此前所有八行同向的问题。相机对每个角色的所有帧/方向求固定包围盒，避免动作之间变大变小和武器裁边；打包所有使用的材质图像，并为动作设 Fake User，避免 Blender 保存时丢弃未激活动作。

## 验证范围与限制

`verify_art.py` 检查 RGBA、非空可见像素、图片/JSON 尺寸、每幅 8 个独特方向、所有帧 alpha 大于 8 的边界留白，并输出联系预览。它不证明引擎遮挡、碰撞、负重/攻击规则、贴图实际导入尺寸或最终构建视觉正确；这些由主代理的 Unity 测试和真实游戏截图验证。

角色方向骨骼渲染是真实帧采样，但未为 625 件装备逐件制作换装模型。敌人用四个可辨识 archetype，尚非 15 套独立原创敌人。世界建筑背面/内部没有为了自由三维相机补全，当前用途是固定机位烘焙；没有宣称商用美术验收完成。

## 许可与派生记录

世界建筑、地砖和摆件为本项目本轮原创网格与材质，许可依项目自身约定；没有把项目原创资产擅自改为 CC0，也没有下载不明许可美术。

骑士基础角色/骨骼/动作来自本仓库 **KayKit Adventurers Character Pack (1.0)**；骷髅基础角色/骨骼/动作来自 **KayKit Character Pack: Skeletons (1.0)**。作者 Kay Lousberg，原许可为 CC0，可个人、教育和商业使用，署名可选。本轮派生增加本地已确认的骑士配色、主要武器、方向采样、透明烘焙和游戏接入。保留原许可证：

- [Adventurers 许可证](../../../unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Adventures/LICENSE.txt)
- [Skeletons 许可证](../../../unity/KnightChronicles/Assets/Art/ThirdParty/KayKit/Characters-Skeletons/LICENSE.txt)

来源与署名：Kay Lousberg，<https://www.kaylousberg.com/>；CC0 说明：<https://creativecommons.org/publicdomain/zero/1.0/>。上述记录使用仓库现有许可文本，未重新下载人物素材。
