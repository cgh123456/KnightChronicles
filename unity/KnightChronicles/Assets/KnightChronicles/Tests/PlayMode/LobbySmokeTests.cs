using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KnightChronicles.Tests
{
    /// <summary>
    /// B 路线（预渲染 2.5D）首页冒烟测试：验证 2D 大厅（正交相机、俯视背景、
    /// 帧动画骑士）与 UGUI 菜单在 Play Mode 下正确创建，并保存截图供人工复核。
    /// </summary>
    public sealed class LobbySmokeTests
    {
        [UnityTest]
        public IEnumerator Lobby_RendersKnightSpriteAndMenu()
        {
            KnightChronicles.Runtime.GameSession.BeginIsolatedTestSession(Path.Combine(Path.GetTempPath(), "KnightChronicles-PlayTests"));
            var load = SceneManager.LoadSceneAsync("Assets/Scenes/Lobby.unity");
            while (!load.isDone)
            {
                yield return null;
            }

            for (var i = 0; i < 60; i++)
            {
                yield return null;  // 等待动画与 UI 稳定
            }

            var camera = Camera.main;
            Assert.IsNotNull(camera, "首页应有 2D 正交相机");
            Assert.IsTrue(camera.orthographic, "首页相机应为正交投影（B 路线）");

            var knight = GameObject.Find("LobbyKnight");
            Assert.IsNotNull(knight, "首页应实例化 2D 帧动画骑士");
            var spriteRenderer = knight.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(spriteRenderer, "骑士应有 SpriteRenderer");
            Assert.IsNotNull(spriteRenderer.sprite, "骑士应已获得起始帧 sprite");
            Assert.IsNotNull(knight.GetComponent<KnightChronicles.Runtime.DirectionalKnightAnimator>(), "骑士应有完整四方向动画器");

            var canvas = Object.FindObjectOfType<Canvas>();
            Assert.IsNotNull(canvas, "首页 UGUI 应被创建");

            // 把 UGUI 临时切到 ScreenSpaceCamera，让截图同时包含背景、角色与菜单。
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            for (var i = 0; i < 3; i++)
            {
                yield return null;
            }

            yield return Capture(camera, "lobby-smoke.png");

            var shot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "lobby-smoke.png");
            Assert.IsTrue(File.Exists(shot), "截图应已写盘");
        }

        private static IEnumerator Capture(Camera camera, string fileName)
        {
            const int width = 1600;
            const int height = 900;
            var renderTexture = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            frame.Apply();
            RenderTexture.active = null;
            camera.targetTexture = previousTarget;
            File.WriteAllBytes(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, fileName),
                frame.EncodeToPNG());
            yield return null;
        }
    }
}
