# 骑士 · 概念精修 v4

本次根据概念图继续精修独立骑士模型，并重点处理用户最新提出的「材质不同」「握剑异常、收剑手指过长」。状态：已交付待视觉验收；不是用户已确认完全还原。

## 打开与查看

- [整套预览](review.html)：正、侧、背、持剑、游戏俯视、材质近景、双手近景及真实旋转视频。
- [收剑可编辑源](knight_refined_v4.blend)：默认佩剑入鞘、双手自然内弯。
- [持剑可编辑源](knight_refined_v4_drawn.blend)：默认显示沿剑柄轴构造的握持手。
- [带贴图GLB](knight_refined_v4.glb)：中立收剑姿态；4个材质族网格，12张内嵌2048×2048 PBR图，可直接导入。
- [原始参考图](reference.png)：用户指定概念图，建模对象为上排骑士。

Blender 文件中的两张原始颜色图均已打包，移动源文件不依赖绝对贴图路径。主要零件在 `KNIGHT | editable components` 集合；独立持剑配件在 `ALTERNATE | drawn sword presentation`。

## 本次实际变更

- 重新构建完整短肩壳、宽银边与短下层搭接，前臂增加侧面覆盖、铆点、边缘搭接；膝甲与胫甲有独立折面和踝部结构。
- 胸布加入受腰带约束的宽褶，围巾改偏心V形斜向叠压，披风与前摆采用较宽的垂坠褶和贴面织边。
- 背包有厚包盖、带压痕、角折与侧缝；卷毯为2.65圈有实际厚度的布卷。
- 钢甲采用银灰锻钢底色、克制划痕与粗糙度变化；布料减弱泛白纤维反光并调整深靛蓝；皮革保留棕色旧化和细纹，手套单独调为较柔和的表面。
- 放松手重做短粗手指与连续掌部，指尖自然内弯，单手最低约z=0.9766（腕位约z=1.273）。
- 持剑手按真实倾斜柄轴构造四指与拇指，并保留握柄通道；补合剑根与护手的连接。默认姿态和持剑姿态分别保留。
- 扩展内眼罩的遮光覆盖，消除斜视时眼孔透背景；预览取自模型实际Cycles渲染。

## 材质来源

[皮革底色及完整提示词](material_sources/leather_albedo.prompt.md)、[钢材底色及完整提示词](material_sources/steel_albedo.prompt.md) 由内置 image_gen 生成，用于实际3D表面颜色贴图。原始两图为1254×1254，原样保留；然后与程序化粗糙度、细纹与曲率磨损混合，烘焙成2K交付图集。未将概念图投影在角色上。

## 验证与技术信息

- 中立源：472 个可编辑网格，715,352 个求值三角形。
- 独立备用持剑部件：16 个网格，55,850 个三角形；持剑展示会隐藏中立右手和入鞘剑柄。
- 使用18个源材质。GLB合并为钢、黄铜、布、皮革4个材质族；UV、法线、切线与12张内嵌2K图片保留。导出副本对腰包盖32个因世界浮点合并而退化的零面积三角做了精确局部清理，GLB为715,320三角；位置变化为0，源模型不变，差数在导出/回读报告中逐项核对。
- [最终源验证](validation.json)：只读复开、有限坐标、面索引、严格面积阈值1e-12、材质、隐藏状态及打包资源校验通过；源SHA在验证前后不变。
- [PBR导出](pbr_export_report.json)、[GLB真实回读](glb_verification.json)、[独立看图记录](visual_review.md)、[交付完整性](delivery_report.json)。
- 九张静态图为1600×1600、Cycles128采样；旋转视频为720×720、48帧、12fps、4秒。

这是用于造型与材质审阅的高精度静态模型，尚无骨骼/动画、运行时LOD或本轮Unity接入。GLB保留核心金属/粗糙度PBR；Blender源保留完整材质网络，轻微sheen、各向异性与coat扩展在GLB中未单独导出。图像和文件校验通过不等同概念图逐像素一致，最终审美验收仍由用户确认。

## 复现

归档同时包含项目相对路径下的 `tools/build_knight_refined_v4.py`、部件模块、材质、只读渲染、导出及校验脚本。使用Blender5.2.1运行：

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --python tools/build_knight_refined_v4.py -- /absolute/project/root --geometry-only
/Applications/Blender.app/Contents/MacOS/Blender -b --python tools/render_knight_refined_views.py -- /absolute/project/root
/Applications/Blender.app/Contents/MacOS/Blender -b --python tools/export_knight_refined_pbr.py -- /absolute/project/root --resolution 2048
```

当前交付为v4。已废弃的骑士版本、旧交付包及专用脚本已按ART-008清理。
