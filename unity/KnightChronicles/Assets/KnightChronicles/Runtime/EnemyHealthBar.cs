using KnightChronicles.Runtime.Core;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        private Transform target;
        private EnemyState data;
        private SpriteRenderer background, fill;
        private float maximum, reveal;
        public void Initialize(Transform actor,EnemyState state)
        {
            target=actor;data=state;
            maximum=(state.Type==3?180:24+state.Type*12)*DungeonGenerator.DifficultyMultiplier(GameSession.State.ActiveRun.Difficulty);
            background=WorldArt.Shape("HealthBorder",transform,Vector2.zero,new Vector2(state.Type==3?1.65f:1.15f,.13f),new Color(.06f,.05f,.04f,.9f),10005).GetComponent<SpriteRenderer>();
            fill=WorldArt.Shape("HealthFill",transform,Vector2.zero,new Vector2(state.Type==3?1.55f:1.05f,.065f),new Color(.8f,.19f,.14f),10006).GetComponent<SpriteRenderer>();
            background.transform.localPosition=fill.transform.localPosition=Vector3.zero;
        }
        public void Reveal(){reveal=3;}
        private void LateUpdate()
        {
            if(target==null||data.Dead){Destroy(gameObject);return;}
            reveal=Mathf.Max(0,reveal-Time.deltaTime);
            var visible=reveal>0||data.Type==3;
            background.enabled=fill.enabled=visible;
            transform.position=target.position+Vector3.up*(data.Type==3?1.65f:1.25f);
            var width=(data.Type==3?1.55f:1.05f)*Mathf.Clamp01(data.Hp/maximum);
            fill.transform.localScale=new Vector3(width,.065f,1);
            fill.transform.localPosition=Vector3.left*((data.Type==3?1.55f:1.05f)-width)*.5f;
        }
    }
}
