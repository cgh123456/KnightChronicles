using System.Collections.Generic;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 俯视 sprite 图集动画器：预烘焙图集（每行一个方向 S/SE/E/NE/N/NW/W/SW，每列一帧）
    /// 的多动作运行时播放器。行动作由 <see cref="Configure"/> 注入，方向由 <see cref="Row"/> 驱动，
    /// 切换动作用 <see cref="Play"/>。由环境层/玩家控制器在 Awake 里注入参数。
    /// </summary>
    public sealed class SpriteAnimator : MonoBehaviour
    {
        /// <summary>一个行动作 = 一张图集 + 帧率；行数固定为 8 方向。</summary>
        public sealed class ActionSet
        {
            public string Name;
            public Texture2D Sheet;
            public int Columns = 12;
            public float Fps = 12f;
            public float PixelsPerUnit = 100f;
        }

        public const int Rows = 8;

        private readonly List<ActionSet> actions = new List<ActionSet>();
        private readonly Dictionary<string, Sprite[][]> framesByAction = new Dictionary<string, Sprite[][]>();
        private readonly Dictionary<string, float> fpsByAction = new Dictionary<string, float>();

        private SpriteRenderer spriteRenderer;
        private string currentAction;
        private int row;
        private int column;
        private float timer;
        private float oneShotUntil;

        /// <summary>方向行号：0=S，顺时针每 45° 一行（与图集烘焙行序一致）。</summary>
        public int Row
        {
            set
            {
                var next = Mathf.Clamp(value, 0, Rows - 1);
                if (row == next)
                {
                    return;
                }

                row = next;
                // 方向切换必须在当帧就替换 Sprite。此前仅更新 row，若动画列号没有变化，
                // SpriteRenderer 会继续显示旧朝向，造成“面朝镜头平移”的错觉。
                RefreshCurrentFrame();
            }
        }

        public string CurrentAction => currentAction;
        public int Direction => row;
        public bool HasAction(string name) { return framesByAction.ContainsKey(name); }
        public void PlayOneShot(string name, float duration)
        {
            if (!HasAction(name)) return;
            oneShotUntil = 0; Play(name); column = 0; timer = 0; oneShotUntil = Time.time + duration;
        }

        /// <summary>由环境层注入动作清单并预切帧（8 方向 × N 列）。</summary>
        public void Configure(params ActionSet[] sets)
        {
            ReleaseFrames();
            currentAction = null;
            spriteRenderer = GetComponent<SpriteRenderer>();
            foreach (var set in sets)
            {
                if (set?.Sheet == null)
                {
                    Debug.LogWarning($"SpriteAnimator({name}): 动作 {set?.Name} 缺少图集，已跳过。");
                    continue;
                }

                actions.Add(set);
                var cellW = set.Sheet.width / set.Columns;
                var cellH = set.Sheet.height / Rows;
                var grid = new Sprite[Rows][];
                for (var r = 0; r < Rows; r++)
                {
                    grid[r] = new Sprite[set.Columns];
                    for (var c = 0; c < set.Columns; c++)
                    {
                        var rect = new Rect(c * cellW, set.Sheet.height - (r + 1) * cellH, cellW, cellH);
                        grid[r][c] = Sprite.Create(set.Sheet, rect, new Vector2(0.5f, 0.5f), set.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                        grid[r][c].name = $"{set.Sheet.name}_{r}_{c:00}";
                    }
                }

                framesByAction[set.Name] = grid;
                fpsByAction[set.Name] = set.Fps;
            }
        }
        private void OnDestroy() { ReleaseFrames(); }
        private void ReleaseFrames()
        {
            foreach (var grid in framesByAction.Values) foreach (var rowFrames in grid) foreach (var sprite in rowFrames) if (sprite != null) Destroy(sprite);
            framesByAction.Clear(); fpsByAction.Clear(); actions.Clear();
        }

        /// <summary>切换动作；同名调用为幂等。动作缺失时保持原动作并告警一次。</summary>
        public void Play(string actionName)
        {
            if (Time.time < oneShotUntil && actionName != currentAction) return;
            if (currentAction == actionName)
            {
                return;
            }

            if (!framesByAction.ContainsKey(actionName))
            {
                Debug.LogWarning($"SpriteAnimator({name}): 未知动作 {actionName}。");
                return;
            }

            currentAction = actionName;
            column = 0;
            timer = 0f;
            RefreshCurrentFrame();
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(currentAction) || !framesByAction.ContainsKey(currentAction))
            {
                // Domain reload / 热更新期间，运行中组件可能暂时保留动作名而丢失运行时字典。
                // 安静地停止旧动作，避免每帧抛异常造成角色视觉抽动。
                currentAction = null;
                return;
            }

            timer += Time.deltaTime;
            var columns = framesByAction[currentAction][row].Length;
            var frame = Time.time < oneShotUntil ? Mathf.Min(columns - 1, Mathf.FloorToInt(timer * fpsByAction[currentAction]))
                : Mathf.FloorToInt(timer * fpsByAction[currentAction]) % columns;
            if (frame != column)
            {
                column = frame;
                RefreshCurrentFrame();
            }
        }

        private void RefreshCurrentFrame()
        {
            if (spriteRenderer == null || string.IsNullOrEmpty(currentAction))
            {
                return;
            }

            var frames = framesByAction[currentAction][row];
            column = Mathf.Clamp(column, 0, frames.Length - 1);
            spriteRenderer.sprite = frames[column];
        }
    }
}
