using System;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 俯视角人物控制：WASD / 方向键移动，输入方向会驱动角色朝向；
    /// 新的完整四方向角色使用 DirectionalKnightAnimator，旧八方向图集仍兼容 SpriteAnimator。
    /// 物理碰撞由 Rigidbody2D 承担。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.2f;

        private SpriteAnimator spriteAnimator;
        private DirectionalKnightAnimator directionalAnimator;
        private Rigidbody2D body;
        private Vector2 input;

        /// <summary>测试/脚本注入输入的钩子；非空时替代真实键盘中读数。</summary>
        public Func<Vector2> InputProvider;

        private void Awake()
        {
            spriteAnimator = GetComponent<SpriteAnimator>();
            directionalAnimator = GetComponent<DirectionalKnightAnimator>();
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (spriteAnimator != null)
            {
                spriteAnimator.Play("idle");
            }
        }

        private void Update()
        {
            input = InputProvider != null ? InputProvider() : ReadKeyboardInput();
            var moving = input.sqrMagnitude > 0.001f;
            if (moving)
            {
                if (spriteAnimator != null)
                {
                    spriteAnimator.Row = DirectionRow(input);
                }
            }

            if (spriteAnimator != null)
            {
                spriteAnimator.Play(moving ? "walk" : "idle");
            }

            if (directionalAnimator != null)
            {
                directionalAnimator.SetMovement(input, moving);
            }

        }

        private void FixedUpdate()
        {
            // 只在物理步进中写入刚体速度。此前在 Update 中直接改 velocity，渲染帧率与
            // 物理帧率不同步时会产生细小来回抽动；插值后的刚体由物理系统平滑呈现。
            var moving = input.sqrMagnitude > 0.001f;
            body.velocity = moving ? input.normalized * moveSpeed : Vector2.zero;
        }

        private static Vector2 ReadKeyboardInput()
        {
            var x = 0f;
            var y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            return new Vector2(x, y);
        }

        /// <summary>速度向量 → 图集方向行号（0=S，顺时针 45°/行，与烘焙行序一致）。</summary>
        public static int DirectionRow(Vector2 velocity)
        {
            var degrees = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90f;
            var row = Mathf.RoundToInt(degrees / 45f);
            return ((row % Rows8) + Rows8) % Rows8;
        }

        private const int Rows8 = 8;
    }
}
