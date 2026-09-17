using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// B 路线（预渲染 2.5D）首页环境层：正交相机 + 俯视大厅背景 + 帧动画角色。
    /// 3D 实时大厅已由 sprite 方案替代；UGUI 首页（HomeMenuController）不受影响。
    /// </summary>
    public sealed class HomeLobby2D : MonoBehaviour
    {
        private static readonly Color NightTint = new Color(0.72f, 0.78f, 0.92f, 1f);

        // 布局常量（世界单位；相机正交高度 10.8、宽度 19.2）。
        // 刻意不用序列化字段：场景里残留的旧值会覆盖代码默认值，导致布局漂移。
        private static readonly Vector2 KnightPosition = new Vector2(-4.6f, -2.95f);
        private const float KnightScale = 1.5f;

        private void Awake()
        {
            AudioDirector.Ensure();
            CreateCamera();
            CreateBackground();
            CreateKnight();
        }

        private static void CreateCamera()
        {
            var cameraNode = new GameObject("LobbyCamera2D", typeof(Camera), typeof(AudioListener));
            var camera = cameraNode.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;  // 背景图 941px/100PPU 高度的一半
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.04f, 0.085f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.tag = "MainCamera";
        }

        private void CreateBackground()
        {
            var background = new GameObject("LobbyBackground", typeof(SpriteRenderer));
            var texture = Resources.Load<Texture2D>("Art/Sprites/LobbyBackground");
            var renderer = background.GetComponent<SpriteRenderer>();
            if (texture != null)
            {
                // 背景 1672×941px，以宽高比适配 19.2×10.8 的视野（高度铺满，宽度按比例）。
                renderer.sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                renderer.color = NightTint;  // 压暗背景，保证菜单文字可读性
                background.transform.position = new Vector3(0f, 0f, 1f);
                var viewHeight = 10.8f;
                var scale = viewHeight * 100f / texture.height;
                background.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                Debug.LogWarning("HomeLobby2D: 未找到 Art/Sprites/LobbyBackground，使用纯色背景。");
            }

            renderer.sortingOrder = -10;
        }

        private void CreateKnight()
        {
            var node = new GameObject("LobbyKnight", typeof(SpriteRenderer), typeof(DirectionalKnightAnimator));
            node.transform.position = new Vector3(KnightPosition.x, KnightPosition.y, 0f);
            node.transform.localScale = Vector3.one * KnightScale;
            node.GetComponent<SpriteRenderer>().sortingOrder = 0;
        }
    }
}
