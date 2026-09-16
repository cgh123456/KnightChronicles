using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 冒险者小镇（占位版）：地面、路径、功能建筑（色块 + 标签 + 碰撞 + 交互点）与玩家出生点
    /// 均为程序化占位，待小镇正式模型输出后逐项替换（美术规格见 docs，占位色板与建筑尺寸可复用）。
    /// </summary>
    public sealed class TownBootstrap : MonoBehaviour
    {
        private static readonly Color GrassColor = new Color(0.32f, 0.52f, 0.33f, 1f);
        private static readonly Color PathColor = new Color(0.62f, 0.55f, 0.44f, 1f);
        private static readonly Color WallColor = new Color(0.55f, 0.42f, 0.35f, 1f);
        private static readonly Color RoofColor = new Color(0.72f, 0.36f, 0.28f, 1f);
        private static readonly Color PromptColor = new Color(1f, 0.92f, 0.7f, 1f);

        private const float WorldWidth = 44f;
        private const float WorldHeight = 32f;

        private TextMesh interactPrompt;
        private TextMesh toast;
        private Interactable nearest;
        private Interactable[] interactables;
        private Transform player;

        private void Awake()
        {
            CreateCamera();
            CreateGround();
            CreatePaths();
            CreateBuildings();
            CreatePlayer();
            CreateHud();
            interactables = FindObjectsOfType<Interactable>();
        }

        private void Update()
        {
            UpdateHudAnchors();
            UpdateNearestInteractable();
            UpdatePrompt();
            HandleInput();
        }

        private void UpdateHudAnchors()
        {
            interactPrompt.transform.position = ViewportToWorld(new Vector2(0.5f, 0.1f));
            toast.transform.position = ViewportToWorld(new Vector2(0.5f, 0.88f));
        }

        // ---------- 场景搭建 ----------

        private static void CreateCamera()
        {
            var cameraNode = new GameObject("TownCamera", typeof(Camera));
            var camera = cameraNode.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.14f, 0.10f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.tag = "MainCamera";
            cameraNode.AddComponent<CameraFollow2D>();
        }

        private static Sprite squareSprite;

        private static Sprite Square()
        {
            if (squareSprite == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                tex.SetPixels32(pixels);
                tex.Apply();
                squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                squareSprite.name = "PlaceholderSquare";
            }

            return squareSprite;
        }

        private static void CreateBlock(string name, Vector2 position, Vector2 size, Color color, int order, bool withCollider = false)
        {
            var node = new GameObject(name, typeof(SpriteRenderer));
            node.transform.position = new Vector3(position.x, position.y, 0f);
            var renderer = node.GetComponent<SpriteRenderer>();
            renderer.sprite = Square();
            renderer.color = color;
            renderer.sortingOrder = order;
            node.transform.localScale = size;
            if (withCollider)
            {
                node.AddComponent<BoxCollider2D>();
            }
        }

        private static void CreateGround()
        {
            CreateBlock("Ground", Vector2.zero, new Vector2(WorldWidth, WorldHeight), GrassColor, -20);
        }

        private static void CreatePaths()
        {
            // 十字主路 + 广场圆盘占位（圆形用叠块近似，正式模型到位后替换）。
            CreateBlock("PathHorizontal", new Vector2(0f, -2f), new Vector2(WorldWidth - 4f, 3.2f), PathColor, -10);
            CreateBlock("PathVertical", new Vector2(0f, 2f), new Vector2(3.2f, WorldHeight - 6f), PathColor, -10);
            CreateBlock("TownSquare", new Vector2(0f, -2f), new Vector2(9f, 9f), PathColor, -9);
        }

        private void CreateBuildings()
        {
            // (名称, 位置, 尺寸)——占位布局：北排功能建筑，南侧地牢入口。
            Building("铁匠铺", new Vector2(-13f, 8f), new Vector2(7f, 5.5f));
            Building("炼金商店", new Vector2(-4f, 9.5f), new Vector2(6f, 5.5f));
            Building("旅馆", new Vector2(5f, 9.5f), new Vector2(7f, 5.5f));
            Building("杂货铺", new Vector2(13.5f, 8f), new Vector2(6f, 5.5f));
            Building("远征之门", new Vector2(0f, -11.5f), new Vector2(8f, 4.5f));
        }

        private static void Building(string name, Vector2 position, Vector2 size)
        {
            // 墙体（带碰撞）+ 屋顶色块 + 标签 + 交互点（建筑正下方）。
            CreateBlock(name + "_Wall", position, size, WallColor, 0, withCollider: true);
            CreateBlock(name + "_Roof", position + new Vector2(0f, size.y * 0.42f), new Vector2(size.x, size.y * 0.35f), RoofColor, 1);

            var label = new GameObject(name + "_Label", typeof(TextMesh));
            label.transform.position = new Vector3(position.x, position.y + size.y * 0.72f, 0f);
            var text = label.GetComponent<TextMesh>();
            text.text = name;
            text.fontSize = 48;
            text.characterSize = 0.22f;
            text.color = Color.white;
            text.anchor = TextAnchor.MiddleCenter;

            var trigger = new GameObject(name + "_Interact", typeof(Interactable));
            trigger.transform.position = position + new Vector2(0f, -size.y * 0.5f - 1.2f);
            var interactable = trigger.GetComponent<Interactable>();
            interactable.DisplayName = name;
            var collider = trigger.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(size.x + 1.5f, 2.6f);
        }

        private void CreatePlayer()
        {
            var node = new GameObject("Player", typeof(SpriteRenderer), typeof(DirectionalKnightAnimator), typeof(TopDownPlayerController));
            node.tag = "Player";
            node.transform.position = new Vector3(0f, -4.5f, 0f);
            node.transform.localScale = Vector3.one * 1.42f;
            var renderer = node.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            node.GetComponent<BoxCollider2D>().size = new Vector2(0.72f, 0.52f);
            node.GetComponent<BoxCollider2D>().offset = new Vector2(0f, -0.1f);
            player = node.transform;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.GetComponent<CameraFollow2D>().SetTarget(player);
            }
        }

        private void CreateHud()
        {
            interactPrompt = CreateHudText("InteractPrompt", new Vector2(0f, -0.65f), 30, PromptColor);
            toast = CreateHudText("Toast", new Vector2(0f, 0.62f), 34, Color.white);
        }

        private static TextMesh CreateHudText(string name, Vector2 viewportAnchor, int fontSize, Color color)
        {
            var node = new GameObject(name, typeof(TextMesh));
            var text = node.GetComponent<TextMesh>();
            text.fontSize = fontSize;
            text.characterSize = 0.14f;
            text.color = color;
            text.anchor = TextAnchor.MiddleCenter;
            text.text = string.Empty;
            return text;
        }

        private static Vector3 ViewportToWorld(Vector2 viewport)
        {
            var camera = Camera.main;
            var world = camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 10f));
            return new Vector3(world.x, world.y, 0f);
        }

        // ---------- 交互 ----------

        private void UpdateNearestInteractable()
        {
            if (player == null)
            {
                return;
            }

            nearest = null;
            var best = float.MaxValue;
            foreach (var interactable in interactables)
            {
                if (interactable == null || !interactable.IsPlayerInside)
                {
                    continue;
                }

                var distance = ((Vector2)interactable.transform.position - (Vector2)player.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = interactable;
                }
            }
        }

        private void UpdatePrompt()
        {
            interactPrompt.text = nearest != null ? $"按 E 进入{nearest.DisplayName}" : string.Empty;
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.E) && nearest != null)
            {
                toast.text = $"已进入{nearest.DisplayName}（占位，功能页待接入）";
                CancelInvoke(nameof(ClearToast));
                Invoke(nameof(ClearToast), 2.5f);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            }
        }

        private void ClearToast()
        {
            toast.text = string.Empty;
        }
    }

    /// <summary>建筑交互触发区：玩家进入范围即可按 E 交互。</summary>
    public sealed class Interactable : MonoBehaviour
    {
        public string DisplayName;

        public bool IsPlayerInside { get; private set; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                IsPlayerInside = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                IsPlayerInside = false;
            }
        }
    }
}
