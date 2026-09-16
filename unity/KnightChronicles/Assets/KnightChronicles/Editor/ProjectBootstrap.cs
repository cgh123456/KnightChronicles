#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KnightChronicles.Editor
{
    /// <summary>
    /// 首次打开工程时补齐可由版本库稳定维护的编辑器配置。
    /// 不会在 Play Mode、构建产物或玩家机器上运行；
    /// 也支持命令行批处理调用：-executeMethod KnightChronicles.Editor.ProjectBootstrap.Configure
    /// （delayCall 在 -quit 批处理下不会触发，所以必须提供直接入口）。
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        private const string PipelineAssetPath = "Assets/Settings/KnightChroniclesURP.asset";
        private const string RendererDataPath = "Assets/Settings/KnightChronicles_Renderer.asset";
        private const string LobbyScenePath = "Assets/Scenes/Lobby.unity";

        static ProjectBootstrap()
        {
            EditorApplication.delayCall += Configure;
        }

        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            var changed = false;
            changed |= EnsureRendererData(pipeline);
            changed |= GraphicsSettings.renderPipelineAsset != pipeline;
            if (GraphicsSettings.renderPipelineAsset != pipeline)
            {
                GraphicsSettings.renderPipelineAsset = pipeline;
            }

            changed |= QualitySettings.renderPipeline != pipeline;
            if (QualitySettings.renderPipeline != pipeline)
            {
                QualitySettings.renderPipeline = pipeline;
            }

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length != 1 || buildScenes[0].path != LobbyScenePath || !buildScenes[0].enabled)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LobbyScenePath, true) };
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("KnightChronicles: 已初始化 URP 渲染管线并将 Lobby 设为默认场景。");
            }
        }

        /// <summary>
        /// 裸 CreateInstance 出来的 URP 资产没有渲染器数据（m_RendererDataList 全空），
        /// Play Mode 下 CreatePipeline 会抛 NullReferenceException，画面全黑；这里补建并挂接。
        /// </summary>
        private static bool EnsureRendererData(UniversalRenderPipelineAsset pipeline)
        {
            if (HasValidRenderer(pipeline))
            {
                return false;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "KnightChronicles_Renderer";
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }

            var serializedPipeline = new SerializedObject(pipeline);
            var list = serializedPipeline.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0)
            {
                Debug.LogError("KnightChronicles: URP 资产缺少 m_RendererDataList 字段，请改用 URP 模板重建 Settings 资产。");
                return false;
            }

            list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            serializedPipeline.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("KnightChronicles: 已为 URP 资产补建前向渲染器数据（修复 Play Mode 黑屏）。");
            return true;
        }

        private static bool HasValidRenderer(UniversalRenderPipelineAsset pipeline)
        {
            var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            if (list == null)
            {
                return false;
            }

            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
