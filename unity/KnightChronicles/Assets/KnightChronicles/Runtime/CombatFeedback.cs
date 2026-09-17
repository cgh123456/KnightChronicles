using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Transient world effects use scaled time so inventory pause also freezes combat feedback.</summary>
    public sealed class CombatFeedback : MonoBehaviour
    {
        private float life, elapsed, rise;
        private Vector3 velocity, initialScale;
        private SpriteRenderer sprite;
        private TextMesh text;
        private Color color;
        private static Sprite glow;
        public static Sprite Glow
        {
            get
            {
                if (glow != null) return glow;
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (var y = 0; y < 64; y++) for (var x = 0; x < 64; x++)
                { var d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                  texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2))); }
                texture.Apply(); glow = Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * .5f, 64);
                return glow;
            }
        }
        private static CombatFeedback Effect(GameObject node, float duration)
        {
            var effect = node.AddComponent<CombatFeedback>(); effect.life = duration;
            effect.sprite = node.GetComponent<SpriteRenderer>(); effect.text = node.GetComponent<TextMesh>();
            effect.color = effect.sprite != null ? effect.sprite.color : effect.text.color;
            effect.initialScale = node.transform.localScale; return effect;
        }
        public static void Halo(Vector2 position, Color tint, float diameter, float duration = .4f)
        {
            var node = WorldArt.Shape("MagicBloom", null, position, Vector2.one * diameter, tint, 10000);
            node.GetComponent<SpriteRenderer>().sprite = Glow; Effect(node, duration);
        }
        public static void Ghost(SpriteRenderer source)
        {
            var node=new GameObject("DodgeAfterimage",typeof(SpriteRenderer));node.transform.position=source.transform.position;
            node.transform.localScale=source.transform.lossyScale;var sr=node.GetComponent<SpriteRenderer>();sr.sprite=source.sprite;
            sr.sortingOrder=source.sortingOrder-1;sr.color=new Color(.4f,.75f,1,.28f);Effect(node,.18f);
        }
        public static void Sparks(Vector2 position, Color tint, int count = 9)
        {
            Halo(position, new Color(tint.r, tint.g, tint.b, .55f), 1.5f, .2f);
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2 / count + Random.value * .25f;
                var node = WorldArt.Shape("ImpactSpark", null, position, new Vector2(.08f, .22f), tint, 10002);
                node.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90);
                var effect = Effect(node, .2f + Random.value * .18f);
                effect.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Random.Range(2.5f, 5f);
            }
        }
        public static void Number(Vector2 position, float amount, bool healing = false)
        {
            var node = new GameObject("DamageNumber", typeof(TextMesh)); node.transform.position = position + Vector2.up * .6f;
            var mesh = node.GetComponent<TextMesh>(); mesh.font = GameFont.World; mesh.fontSize = 48;
            mesh.characterSize = .075f; mesh.anchor = TextAnchor.MiddleCenter; mesh.fontStyle = FontStyle.Bold;
            mesh.text = (healing ? "+" : "") + Mathf.CeilToInt(amount);
            mesh.color = healing ? new Color(.4f, 1, .62f) : amount >= 80 ? new Color(1, .78f, .28f) : new Color(1, .94f, .8f);
            var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mesh.font.material; renderer.sortingOrder = 10010;
            Effect(node, .85f).rise = 1.2f;
        }
        public static void Slash(Vector2 position, Vector2 direction, float reach)
        {
            var node = new GameObject("WeaponArc", typeof(LineRenderer)); node.transform.position = position;
            var line = node.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 13;
            line.material = new Material(Shader.Find("Sprites/Default")); line.startColor = new Color(1, .94f, .66f, .9f);
            line.endColor = new Color(.75f, .87f, 1, .1f); line.startWidth = .13f; line.endWidth = .025f;
            line.sortingOrder = 10001; var angle = Mathf.Atan2(direction.y, direction.x);
            for (var i = 0; i < 13; i++) { var a = angle - .95f + i / 12f * 1.9f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * Mathf.Min(reach, 2.6f)); }
            var effect = node.AddComponent<CombatFeedback>(); effect.life = .18f; effect.initialScale = Vector3.one;
        }
        private void Update()
        {
            var dt = Time.deltaTime; elapsed += dt; var t = Mathf.Clamp01(elapsed / life);
            transform.position += (velocity + Vector3.up * rise) * dt; velocity *= Mathf.Exp(-dt * 5);
            if (sprite != null) { sprite.color = new Color(color.r, color.g, color.b, color.a * (1 - t));
                transform.localScale = initialScale * (1 + t * .3f); }
            if (text != null) text.color = new Color(color.r, color.g, color.b, Mathf.Clamp01((1 - t) * 3));
            var line = GetComponent<LineRenderer>(); if (line != null) { var c = line.startColor; c.a = (1 - t) * .85f; line.startColor = c; }
            if (elapsed >= life) Destroy(gameObject);
        }
        private void OnDestroy() { var line = GetComponent<LineRenderer>(); if (line != null) Destroy(line.sharedMaterial); }
    }

    public sealed class ProjectileTrail : MonoBehaviour
    {
        private Material material;
        public void Initialize(Color color)
        {
            var trail=gameObject.AddComponent<TrailRenderer>();trail.time=.15f;trail.minVertexDistance=.08f;
            trail.startWidth=.24f;trail.endWidth=.015f;trail.startColor=color;trail.endColor=new Color(color.r,color.g,color.b,0);
            material=new Material(Shader.Find("Sprites/Default"));trail.material=material;trail.sortingOrder=9998;
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }

    public sealed class WorldPulse : MonoBehaviour
    {
        public float Speed = 2, Strength = .12f;
        private SpriteRenderer sprite;
        private Color color;
        private Vector3 scale;
        private void Awake() { sprite = GetComponent<SpriteRenderer>(); color = sprite.color; scale = transform.localScale; }
        private void Update() { var t = Mathf.Sin(Time.time * Speed + transform.position.x * .7f);
            sprite.color = new Color(color.r, color.g, color.b, color.a * (1 - Strength + Strength * t));
            transform.localScale = scale * (1 + t * Strength * .15f); }
    }

    public sealed class GroundShadow : MonoBehaviour
    {
        public Transform Target;
        private void LateUpdate() { if (Target == null) { Destroy(gameObject); return; }
            transform.position = Target.position + new Vector3(0, -.45f, .01f); }
        public static void Attach(Transform target, Vector2 size)
        {
            var node = WorldArt.Shape("ContactShadow", null, target.position, size, new Color(0, 0, 0, .45f), 500);
            node.GetComponent<SpriteRenderer>().sprite = CombatFeedback.Glow; node.AddComponent<GroundShadow>().Target = target;
        }
    }
}
