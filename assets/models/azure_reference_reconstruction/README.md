# 蓝宝石传说长剑：概念图重建

本目录是根据用户指定的 `exec-12d993e7-b930-4c2a-a2b4-85433bc3446f.png` 单张正面概念图重新制作的三维资产，与旧版 V1/V2/V3 分开保存。

## 使用文件

- `Azure_Reference_Reconstruction.blend`：可编辑 Blender 5.2 场景，含分件模型、材质、已打包参考图、灯光和三个镜头。
- `Azure_Reference_Reconstruction.glb`：仅武器与握柄枢轴；包含 UV、贴图、材质、自发光；不含展示地面、灯光、镜头和悬浮粒子。
- `front_render.png`：模型正面实际渲染。
- `detail_render.png`：剑首、皮革缠带、宝石和护手实际渲染。
- `threequarter_render.png`：25°斜侧面实际渲染。
- `clay_geometry_render.png`：去除纹理后、约42°斜侧面的灰模实际渲染。
- `reference_concept.png`：未经修改的原始参考图。
- `asset_report.json`：建模统计与来源说明。
- `import_validation.json`：对导出的 GLB 重新导入后的独立检查，包括文件哈希。

## 几何与比例

原图按 1024×1536 像素坐标标定。以轴线 x≈510、剑首尖端 y≈14、护手左右尖端 x≈299/720、剑尖 y≈1444 作为造型基准。

为提供可用物理比例，假定全长约 1.60 米，护手宽约 0.473 米，宝石镶座最大总厚度约 0.058 米。这些米制尺寸是制作假设，原图未提供实物尺度。

剑尖朝下，Blender Z 轴向上，正面朝 -Y。`AZURE_SWORD_grip_socket` 为握柄位置的父级枢轴。模型包含刀刃楔形截面、凹入剑脊、剑根双侧肩口、下弯镰形护手、尖冠剑首、分面宝石、连续皮革绕带、浅浮雕与金属镶边。

目前为可编辑的展示精度分件模型，约 8.8 万三角形。每个零件分别闭合；零件间存在正常装配交叠。它不是已合并成单一密闭壳体的打印模型，也没有完成游戏量产所需的 LOD、碰撞和绘制调用合并。

## 还原依据与边界

可见正面的轮廓、分区、主宝石位置和材质分布按原图建立。背面、厚度、内部剑茎和隐藏连接无法从单张正面图唯一确定；本资产使用对称背面与合理结构补全。这些部分不应称为已从原图完整恢复。

细小刻纹、磨损、划痕和部分宝石颜色通过原图的正面 UV 投影保留。实际几何负责主体、厚度、切面、主要镶边、皮革绕带和部分浮雕。并非每一道原图细纹均被雕刻为独立几何。

`REF_COLOR_*` 材质使用原图作为颜色纹理，其中保留原图绘制的光照、高光和阴影，辅以金属度、粗糙度与发光参数。因此当前是以匹配原图外观为目的的材质，不是经过去光照处理的标准 PBR 底色贴图；换光照时不会完全按真实物理材质响应。

场景也保留独立的银钢、黑钢、古金、皮革、宝石等程序化物理材质，供继续制作使用。它们的微表面节点和宝石体积吸收不等同于已烘焙到 GLB 的法线/粗糙度纹理。若继续进行引擎量产，需要另做去光照、材质烘焙和性能整理。

## 验证

导出后使用独立脚本重新读取 GLB 容器、检查嵌入图片与材质引用，并重新导入 Blender 检查尺寸、法线和分件拓扑。glTF 会在 UV/硬法线接缝拆分顶点；拓扑报告另以位置合并副本核实真实开口，不修改交付网格。

灰模渲染仅在保存文件之后临时替换材质，交付 `.blend` 的默认状态保持正常纹理和正面镜头。

## 复现

项目 `tools` 目录内包含：

- `build_azure_reference.py`：基于像素标定建立几何、UV、导出并渲染。
- `azure_reference_materials.py`：物理材质辅助模块。
- `azure_reference_studio.py`：灯光、地面和镜头。
- `validate_azure_reference.py`：独立 GLB 重新导入检查。

在项目根目录执行：

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 2 --python tools/build_azure_reference.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 2 --python tools/validate_azure_reference.py
```

可在第一条命令末尾添加 `-- --quick` 生成较低采样的检查图。
