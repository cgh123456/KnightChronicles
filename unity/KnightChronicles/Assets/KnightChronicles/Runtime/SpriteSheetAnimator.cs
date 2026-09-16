using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 俯视 sprite 图集的运行时帧动画：从图集纹理按行切帧并循环播放。
    /// 行序与图集烘焙约定一致（每行一个方向）：S, SE, E, NE, N, NW, W, SW。
    /// </summary>
    public sealed class SpriteSheetAnimator : MonoBehaviour
    {
        [SerializeField] private Texture2D sheet;
        [SerializeField] private int row;
        [SerializeField] private int columns = 12;
        [SerializeField] private float fps = 12f;
        [SerializeField] private float pixelsPerUnit = 100f;

        /// <summary>图集纹理（运行时由环境层注入）。</summary>
        public Texture2D Sheet { set => sheet = value; }

        /// <summary>方向行号（0=S，顺时针每 45° 一行）。</summary>
        public int Row { set => row = value; }

        private Sprite[] frames;
        private SpriteRenderer spriteRenderer;
        private float timer;
        private int index;

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (sheet == null)
            {
                Debug.LogWarning($"SpriteSheetAnimator({name}): 未设置图集纹理。");
                enabled = false;
                return;
            }

            var cellW = sheet.width / columns;
            var cellH = sheet.height / rows_from_meta();
            frames = new Sprite[columns];
            for (var col = 0; col < columns; col++)
            {
                var rect = new Rect(
                    col * cellW,
                    sheet.height - (row + 1) * cellH,
                    cellW,
                    cellH);
                frames[col] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
                frames[col].name = $"{sheet.name}_{row}_{col:00}";
            }

            spriteRenderer.sprite = frames[0];
        }

        private int rows_from_meta()
        {
            // 图集固定 8 方向（见烘焙脚本 render_spritesheet.py 的 DIRS 表）。
            return 8;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            var frame = Mathf.FloorToInt(timer * fps) % columns;
            if (frame != index)
            {
                index = frame;
                spriteRenderer.sprite = frames[index];
            }
        }
    }
}
