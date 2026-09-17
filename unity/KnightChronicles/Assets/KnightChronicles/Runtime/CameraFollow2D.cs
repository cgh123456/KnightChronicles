using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>2D 相机平滑跟随目标（用于小镇等自由行走场景）。</summary>
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.16f;
        [SerializeField] private float zOffset = -10f;

        private Vector3 velocity;
        private Rect bounds;
        private bool hasBounds;
        private Camera view;
        private Rigidbody2D targetBody;

        public void SetTarget(Transform value)
        {
            target = value;
            targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
            if (target != null) transform.position = new Vector3(target.position.x, target.position.y, zOffset);
        }

        public void SetBounds(Rect value) { bounds = value; hasBounds = true; view = GetComponent<Camera>(); }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (view != null) view.orthographicSize = Mathf.Lerp(view.orthographicSize, 7.3f * GameSession.State.Zoom, Time.unscaledDeltaTime * 5);
            var lead = targetBody == null ? Vector2.zero : Vector2.ClampMagnitude(targetBody.velocity * .13f, .85f);
            var goal = new Vector3(target.position.x + lead.x, target.position.y + lead.y + .35f, zOffset);
            if (hasBounds && view != null)
            {
                var halfHeight = view.orthographicSize; var halfWidth = halfHeight * view.aspect;
                goal.x = halfWidth * 2 >= bounds.width ? bounds.center.x : Mathf.Clamp(goal.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
                goal.y = halfHeight * 2 >= bounds.height ? bounds.center.y : Mathf.Clamp(goal.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
            }
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
