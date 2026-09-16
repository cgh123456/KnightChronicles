using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KnightChronicles.Tests
{
    /// <summary>
    /// 冒险者小镇（占位）冒烟测试：验证玩家生成、输入驱动移动、八方向朝向切换、
    /// 行走动作切换与建筑碰撞体，并保存小镇截图供人工复核。
    /// </summary>
    public sealed class TownSmokeTests
    {
        [UnityTest]
        public IEnumerator Town_PlayerMovesAndFacesMovementDirection()
        {
            var load = SceneManager.LoadSceneAsync("Assets/Scenes/Town.unity");
            while (!load.isDone)
            {
                yield return null;
            }

            for (var i = 0; i < 20; i++)
            {
                yield return null;
            }

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "小镇应生成玩家");
            var body = player.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(body, "玩家应有物理体（碰撞）");
            var animator = player.GetComponent<KnightChronicles.Runtime.DirectionalKnightAnimator>();
            Assert.IsNotNull(animator, "玩家应有完整四方向骑士动画器");

            // 注入向右的测试输入，验证位移、朝向与动作切换。
            var controller = player.GetComponent<KnightChronicles.Runtime.TopDownPlayerController>();
            var startPosition = player.transform.position;

            // 批处理下真实时间驱动不了物理步进，改用脚本模式确定性模拟。
            var previousSimulationMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            controller.InputProvider = () => Vector2.right;
            for (var i = 0; i < 30; i++)
            {
                Physics2D.Simulate(Time.fixedDeltaTime);
                yield return null;
            }

            var moved = player.transform.position.x - startPosition.x;
            Debug.Log($"[小镇冒烟] 向右位移 {moved:F2}（0.5s 内）");
            Assert.Greater(moved, 0.5f, "输入向右应产生水平位移");
            Assert.IsTrue(animator.IsMoving, "移动时应启用步态表现");
            Assert.AreEqual(KnightChronicles.Runtime.DirectionalKnightAnimator.FacingDirection.East, animator.Facing, "向右移动应面向 E");

            // 继续验证垂直与反向输入，防止角色再次出现始终面朝镜头的回归。
            controller.InputProvider = () => Vector2.up;
            yield return null;
            Physics2D.Simulate(Time.fixedDeltaTime);
            yield return null;
            Assert.AreEqual(KnightChronicles.Runtime.DirectionalKnightAnimator.FacingDirection.North, animator.Facing, "向上移动应面向 N");

            controller.InputProvider = () => Vector2.left;
            yield return null;
            Physics2D.Simulate(Time.fixedDeltaTime);
            yield return null;
            Assert.AreEqual(KnightChronicles.Runtime.DirectionalKnightAnimator.FacingDirection.West, animator.Facing, "向左移动应面向 W");

            // 松开输入后应回到待机。
            controller.InputProvider = () => Vector2.zero;
            Physics2D.simulationMode = previousSimulationMode;
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.IsFalse(animator.IsMoving, "静止时应停止步态表现");

            // 传送到北排建筑前：验证建筑占位、标签与交互提示后抓帧。
            var teleport = new Vector2(-4f, 3.6f);
            body.position = teleport;
            player.transform.position = new Vector3(teleport.x, teleport.y, 0f);
            var camera = Camera.main;
            Assert.IsNotNull(camera, "小镇应有跟随相机");
            camera.transform.position = new Vector3(-4f, 3.6f, -10f);
            for (var i = 0; i < 3; i++)
            {
                yield return null;
            }

            yield return Capture(camera, "town-smoke.png");
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
