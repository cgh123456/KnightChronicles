#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KnightChronicles.Editor
{
    public static class BuildTools
    {
        [MenuItem("KnightChronicles/配置可玩版本")]
        public static void Configure()
        {
            ProjectBootstrap.Configure();
            PlayerSettings.companyName = "KnightChronicles";
            PlayerSettings.productName = "KnightChronicles";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            // Legacy keyboard queries and UGUI coexist with the installed Input System package.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler"); if (input != null) { input.intValue = 2; settings.ApplyModifiedPropertiesWithoutUndo(); }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art/Sprites", "Assets/Resources/Art/Enemies", "Assets/Resources/Art/World" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("KnightVariant_") && !path.Contains("/Enemies/") && !path.Contains("/World/")) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var changed = importer.maxTextureSize != 4096 || importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None
                    || importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency;
                if (!changed) continue;
                importer.maxTextureSize = 4096; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("KnightChronicles/构建 Windows 可玩版本")]
        public static void Windows()
        {
            Configure();
            var destination = Environment.GetEnvironmentVariable("KNIGHT_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(destination)) destination = Path.GetFullPath("Builds/Windows/KnightChronicles.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/Town.unity" }, locationPathName = destination,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows 构建失败：" + report.summary.result);
            Debug.Log("Windows build: " + destination + " / " + report.summary.totalSize + " bytes");
        }
    }
}
#endif
