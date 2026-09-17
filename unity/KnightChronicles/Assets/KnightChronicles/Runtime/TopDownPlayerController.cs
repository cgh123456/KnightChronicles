using System;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Eight-direction animated town movement with foot collisions and short rolls.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class TopDownPlayerController : MonoBehaviour
    {
        private SpriteAnimator animator;
        private SpriteRenderer spriteView;
        private Rigidbody2D body;
        private Vector2 input, facing = Vector2.down, rollDirection;
        private float rollTime, rollCooldown, stepTimer;
        private bool running;
        public Func<Vector2> InputProvider;
        public bool ControlsBlocked;
        public bool IsMoving { get { return input.sqrMagnitude > .001f || rollTime > 0; } }
        public Vector2 Facing { get { return facing; } }
        private void Awake()
        {
            animator = GetComponent<SpriteAnimator>(); spriteView = GetComponent<SpriteRenderer>(); body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0; body.freezeRotation = true; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (animator != null) animator.Play("idle");
        }
        private void Update()
        {
            var blocked = ControlsBlocked || GamePanel.Blocked;
            input = blocked ? Vector2.zero : InputProvider != null ? InputProvider() : ReadKeyboardInput();
            running = !blocked && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            rollCooldown = Mathf.Max(0, rollCooldown - Time.deltaTime);
            rollTime = blocked ? 0 : Mathf.Max(0, rollTime - Time.deltaTime);
            if (input.sqrMagnitude > .001f) facing = input.normalized;
            if (!blocked && GameInput.Down("dodge") && rollCooldown <= 0)
            {
                rollDirection = facing; rollTime = .34f; rollCooldown = 1.3f; AudioDirector.Cue(15);
                TownAtmosphere.Footstep((Vector2)transform.position + Vector2.down * .66f, true);
            }
            if (animator != null)
            {
                animator.Row = DirectionRow(rollTime > 0 ? rollDirection : facing);
                animator.Play(rollTime > 0 ? "dodge" : input.sqrMagnitude > .001f ? running ? "run" : "walk" : "idle");
            }
            if (spriteView != null) spriteView.sortingOrder = TownAtmosphere.Order(transform.position.y - .66f) + 1;
            if (input.sqrMagnitude > .001f && rollTime <= 0)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0) { stepTimer = running ? .25f : .34f; TownAtmosphere.Footstep((Vector2)transform.position + Vector2.down * .66f, running); }
            }
            else stepTimer = .10f;
        }
        private void FixedUpdate()
        {
            if (ControlsBlocked || GamePanel.Blocked) { body.velocity = Vector2.zero; return; }
            body.velocity = rollTime > 0 ? rollDirection * 8.3f : input.normalized * GameSession.Rules.MoveSpeed * (running ? 1.32f : 1);
        }
        private static Vector2 ReadKeyboardInput()
        {
            return new Vector2((GameInput.Held("right") || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (GameInput.Held("left") || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (GameInput.Held("up") || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (GameInput.Held("down") || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
        }
        /// <summary>Sheet rows: S, SE, E, NE, N, NW, W, SW.</summary>
        public static int DirectionRow(Vector2 velocity)
        {
            var row = Mathf.RoundToInt((Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90) / 45);
            return (row % 8 + 8) % 8;
        }
    }
}
