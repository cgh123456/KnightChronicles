using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 4 方向完整骑士精灵播放器。图集布局：左上=S、右上=E、左下=N、右下=W。
    /// 步态只修改视觉对象的缩放，永不修改刚体位置，因此不会产生物理抽动。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DirectionalKnightAnimator : MonoBehaviour
    {
        public enum FacingDirection { South, East, North, West }

        private const string SpriteSheetPath = "Art/Sprites/Knight_Directional_4";
        private const float PixelsPerUnit = 320f;
        private readonly Sprite[] sprites = new Sprite[4];
        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale;
        private float gaitTime;

        public FacingDirection Facing { get; private set; } = FacingDirection.South;
        public bool IsMoving { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadSprites();
            ApplySprite();
        }

        private void Start()
        {
            // TownBootstrap / HomeLobby2D 会在创建组件后设置展示比例；在 Start 读取，
            // 能确保步态缩放以最终比例为基准，而不是意外回退到 (1,1,1)。
            baseScale = transform.localScale;
        }

        public void SetMovement(Vector2 direction, bool moving)
        {
            IsMoving = moving;
            if (moving && direction.sqrMagnitude > 0.001f)
            {
                Facing = ToFacingDirection(direction);
                ApplySprite();
            }

            if (!moving)
            {
                gaitTime = 0f;
                transform.localScale = baseScale;
            }
        }

        private void Update()
        {
            if (!IsMoving)
            {
                return;
            }

            gaitTime += Time.deltaTime * 11f;
            transform.localScale = baseScale * (1f + Mathf.Sin(gaitTime) * 0.018f);
        }

        private void LoadSprites()
        {
            var sheet = Resources.Load<Texture2D>(SpriteSheetPath);
            if (sheet == null)
            {
                Debug.LogError("DirectionalKnightAnimator: 未找到完整四方向骑士图集。");
                return;
            }

            var width = sheet.width / 2;
            var height = sheet.height / 2;
            sprites[(int)FacingDirection.South] = CreateSprite(sheet, 0, height, width, height, "Knight_South");
            sprites[(int)FacingDirection.East] = CreateSprite(sheet, width, height, width, height, "Knight_East");
            sprites[(int)FacingDirection.North] = CreateSprite(sheet, 0, 0, width, height, "Knight_North");
            sprites[(int)FacingDirection.West] = CreateSprite(sheet, width, 0, width, height, "Knight_West");
        }

        private static Sprite CreateSprite(Texture2D sheet, int x, int y, int width, int height, string name)
        {
            var sprite = Sprite.Create(sheet, new Rect(x, y, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private void ApplySprite()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprites[(int)Facing];
            }
        }

        private static FacingDirection ToFacingDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x > 0f ? FacingDirection.East : FacingDirection.West;
            }

            return direction.y > 0f ? FacingDirection.North : FacingDirection.South;
        }
    }
}
