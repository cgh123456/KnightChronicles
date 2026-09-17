using System.Collections.Generic;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Eight directional baked CC0 skeleton animation with hit and death response.</summary>
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private SpriteAnimator animator;
        private SpriteRenderer bodyRenderer;
        private float flash, death;
        private bool dying;
        private Color baseColor = Color.white;
        private int kind;
        public void Configure(int type, int difficulty)
        {
            kind = type; bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null) bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            animator = GetComponent<SpriteAnimator>(); if (animator == null) animator = gameObject.AddComponent<SpriteAnimator>();
            var character = type == 0 ? "Minion" : type == 1 ? "Rogue" : type == 2 ? "Mage" : "Warrior";
            var sets = new List<SpriteAnimator.ActionSet>();
            foreach (var action in new[] { "idle", "walk", "attack" })
            {
                var path = "Art/Enemies/Skeleton_" + character + "_" + action + "_sheet";
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null) sets.Add(new SpriteAnimator.ActionSet { Name = action, Sheet = texture, Columns = 6, Fps = action == "attack" ? 13 : 8, PixelsPerUnit = ModelArt.MotionPixelsPerUnit(path) });
            }
            animator.Configure(sets.ToArray());
            if (sets.Count > 0) animator.Play("idle");
            else Debug.LogWarning("EnemyPresentation: missing baked skeleton " + character);
            transform.localScale = Vector3.one * (type == 3 ? 1.10f : .83f);
            baseColor = Color.Lerp(Color.white, new Color(.91f, .83f, .77f), difficulty * .06f);bodyRenderer.color = baseColor;
            bodyRenderer.sortingOrder = 5000 - Mathf.RoundToInt(transform.position.y * 10);
        }
        public void Tick(Vector2 direction, bool walking, bool telegraph)
        {
            if (dying || animator == null) return;
            if (direction.sqrMagnitude > .001f)
            {
                var angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
                animator.Row = ((Mathf.RoundToInt(angle / 45) % 8) + 8) % 8;
            }
            animator.Play(telegraph ? "attack" : walking ? "walk" : "idle");
            bodyRenderer.sortingOrder = 5000 - Mathf.RoundToInt(transform.position.y * 10);
        }
        public void Hit() { flash = .13f; }
        public void Die() { if (dying) return; dying = true; death = 0; if (animator != null) animator.enabled = false; }
        private void Update()
        {
            if (bodyRenderer == null) return;
            flash = Mathf.Max(0, flash - Time.deltaTime);
            if (dying)
            {
                death += Time.deltaTime; var alpha = Mathf.Clamp01(1 - death / .45f);
                bodyRenderer.color = new Color(.52f, .46f, .42f, alpha);
                transform.rotation = Quaternion.Euler(0, 0, death * 75);
                if (death > .45f) Destroy(gameObject);
            }
            else bodyRenderer.color = flash > 0 ? new Color(1, .36f, .24f) : baseColor;
        }
    }
}

