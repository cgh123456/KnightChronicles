# ART-007 GLB re-import visual check

Reviewed 2026-09-17 using Blender 5.2.1 LTS. This is a current check of the final delivered GLB, not a reuse of the source render.

- GLB: `knight_refined_v4.glb`, SHA256 `094a7d8032eb855d780aa77c3feee4b7575dac1c5e2c92845b9def3466c7d388`.
- Immutable source SHA256: `0457d73d7d32222eef5610d35ab09970740e286ce999040d7c7ef17319154153`.
- Structural and real Blender import validation: **PASS**, 4 meshes, 4 PBR materials, 12 embedded 2048 x 2048 PNG images, 715320 triangles, no triangles at or below area 1e-12 after import.
- The final count equals 715352 source triangles minus 32 explicitly audited export-copy degenerates on the thin hip-pouch lid. The source is unchanged. The cleanup merged 16 identical world-coordinate points with zero vertex displacement; full evidence is in `pbr_export_report.json` and `glb_verification.json`.
- Actual imported-GLB preview: `glb_material_preview.png`, 1000 x 1000, Cycles 64 samples, GPU. The source camera, three lights, world and studio ground were retained. All source knight and alternate geometry was removed before importing the GLB. No source mesh or concept-image projection appears in this preview.

The preview and the final source `hero.png` were both opened and visually inspected. Overall silhouette, armor pieces, glove shapes, relaxed pose, colors, belt and backpack attachments agree with the source. Silver-gray wear, brown leather color/grain, indigo fabric and brass details survive the baked PBR conversion. There are no missing pieces, untextured magenta/white surfaces, obvious UV stretching or newly introduced silhouette changes in this view. The larger source preview shows finer procedural surface detail; the GLB appearance is somewhat softer at this 1000px viewing size and uses the baked 2K atlases.

This confirms broad source-to-delivery consistency for the shown neutral view. It does not certify exact identity with the supplied concept art or every hidden surface. Core glTF PBR carries base color, roughness, metallic and tangent normals; source procedural fiber sheen, anisotropy and leather clearcoat are not separately exported as extensions.
