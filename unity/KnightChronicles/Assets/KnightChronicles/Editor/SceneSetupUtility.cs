using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KnightChronicles.Editor
{
    /// <summary>
    /// Lobby 场景结构维护命令。
    /// 命令行：-executeMethod KnightChronicles.Editor.SceneSetupUtility.ConvertLobbyTo2D
    /// </summary>
    public static class SceneSetupUtility
    {
        public static void ConvertLobbyTo2D()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity", OpenSceneMode.Single);
            var menu = Object.FindObjectOfType<KnightChronicles.Runtime.HomeMenuController>(true);
            if (menu == null)
            {
                Debug.LogError("[场景转换] 找不到 LobbyBootstrap（HomeMenuController）。");
                EditorApplication.Exit(1);
                return;
            }

            var root = menu.gameObject;

            // HomeLobby3D 已删除，场景里的旧组件表现为 Missing Script，直接清掉。
            var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            if (removed > 0)
            {
                Debug.Log($"[场景转换] 已清理 {removed} 个失效组件。");
            }

            if (root.GetComponent<KnightChronicles.Runtime.HomeLobby2D>() == null)
            {
                root.AddComponent<KnightChronicles.Runtime.HomeLobby2D>();
                Debug.Log("[场景转换] 已挂载 HomeLobby2D。");
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[场景转换] Lobby.unity 已保存。");
            EditorApplication.Exit(0);
        }
    }
}
