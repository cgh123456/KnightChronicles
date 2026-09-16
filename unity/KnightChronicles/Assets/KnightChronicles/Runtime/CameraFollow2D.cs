using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>2D 相机平滑跟随目标（用于小镇等自由行走场景）。</summary>
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private float zOffset = -10f;

        private Vector3 velocity;

        public void SetTarget(Transform value)
        {
            target = value;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var goal = new Vector3(target.position.x, target.position.y, zOffset);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
