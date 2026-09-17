using System.Collections.Generic;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Bounded, lightweight atmosphere: water, chimney smoke, leaves and warm lamps.</summary>
    public sealed class TownAtmosphere : MonoBehaviour
    {
        private sealed class Emitter
        {
            public int Kind;
            public Vector2 Position;
            public Color Color;
            public float Timer;
        }
        private sealed class Particle
        {
            public SpriteRenderer Render;
            public Vector2 Velocity;
            public Color Color;
            public float Life, Duration, Size, Growth, Wobble;
        }
        private sealed class Breeze
        {
            public Transform Transform;
            public float Phase;
        }
        private sealed class Glow
        {
            public SpriteRenderer Renderer;
            public Color Color;
            public Vector3 Scale;
            public float Phase;
        }
        private static TownAtmosphere instance;
        private static Sprite softDisc, ring;
        private readonly List<Emitter> emitters = new List<Emitter>();
        private readonly List<Breeze> breezes = new List<Breeze>();
        private readonly List<Glow> glows = new List<Glow>();
        private readonly List<Breeze> merchants = new List<Breeze>();
        private readonly Particle[] particles = new Particle[64];
        private int cursor;

        public static int Order(float feetY) { return 10000 - Mathf.RoundToInt(feetY * 100); }

        private void Awake()
        {
            instance = this;
            for (var i = 0; i < particles.Length; i++)
            {
                var node = new GameObject("TownAtmosphereParticle", typeof(SpriteRenderer)); node.transform.SetParent(transform);
                var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = SoftDisc; renderer.enabled = false;
                particles[i] = new Particle { Render = renderer };
            }
        }
        private void OnDestroy() { if (instance == this) instance = null; }

        private static Sprite SoftDisc
        {
            get
            {
                if (softDisc == null) softDisc = MakeDisc(false);
                return softDisc;
            }
        }
        private static Sprite Ring
        {
            get
            {
                if (ring == null) ring = MakeDisc(true);
                return ring;
            }
        }
        private static Sprite MakeDisc(bool outline)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = outline ? "SoftGroundRing" : "TownSoftParticle", filterMode = FilterMode.Bilinear };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    var alpha = outline ? Mathf.Clamp01(1 - Mathf.Abs(radius - .73f) * 7) : Mathf.Pow(Mathf.Clamp01(1 - radius), 2);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
        }

        public static SpriteRenderer MakeDoorHighlight(Transform parent, Vector2 position, Vector2 size)
        {
            var node = new GameObject("DoorwayApproachGlow", typeof(SpriteRenderer)); node.transform.SetParent(parent); node.transform.position = position; node.transform.localScale = size;
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = Ring; renderer.sortingOrder = -650; renderer.color = Color.clear;
            return renderer;
        }

        public static void MakePlayerShadow(Transform parent, Vector2 localFeet)
        {
            var node = new GameObject("KnightGroundShadow", typeof(SpriteRenderer)); node.transform.SetParent(parent, false);
            node.transform.localPosition = localFeet; node.transform.localScale = new Vector3(1.02f, .42f, 1);
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = SoftDisc; renderer.color = new Color(.06f, .08f, .08f, .62f); renderer.sortingOrder = -700;
        }

        public void AddBreeze(Transform target, float phase) { breezes.Add(new Breeze { Transform = target, Phase = phase }); }
        public void AddFountain(Vector2 position) { emitters.Add(new Emitter { Kind = 2, Position = position, Timer = .1f, Color = new Color(.60f, .92f, 1, .7f) }); }
        public void AddPetals(Vector2 position) { emitters.Add(new Emitter { Kind = 3, Position = position, Timer = .9f, Color = new Color(.94f, .71f, .45f, .58f) }); }
        public void AddSmoke(Vector2 position, Color? color = null) { emitters.Add(new Emitter { Kind = 1, Position = position, Timer = .2f, Color = color ?? new Color(.82f, .81f, .76f, .23f) }); }
        public void AddLantern(Vector2 position)
        {
            AddGlow(position, new Vector2(1.75f, 1.7f), new Color(1, .65f, .22f, .22f), Order(position.y) + 1);
            AddGlow(position, new Vector2(.35f, .4f), new Color(1, .87f, .47f, .69f), Order(position.y) + 2);
        }
        public void AddPortal(Vector2 position)
        {
            AddGlow(position, new Vector2(2.8f, 2.75f), new Color(.35f, .73f, 1, .42f), Order(position.y - 1.2f) + 1);
            emitters.Add(new Emitter { Kind = 4, Position = position, Timer = .2f, Color = new Color(.54f, .86f, 1, .66f) });
        }
        public void AddMerchant(string key, Vector2 imageCenter, float feetY)
        {
            var node = ModelArt.Place(key, transform, imageCenter, new Vector2(2.4f, 2.4f), Order(feetY) + 2);
            merchants.Add(new Breeze { Transform = node.transform, Phase = merchants.Count * 1.7f });
            var shadow = new GameObject("MerchantGroundShadow", typeof(SpriteRenderer)); shadow.transform.SetParent(transform); shadow.transform.position = new Vector2(imageCenter.x, feetY); shadow.transform.localScale = new Vector3(.83f, .34f, 1);
            var renderer = shadow.GetComponent<SpriteRenderer>(); renderer.sprite = SoftDisc; renderer.color = new Color(.07f, .09f, .08f, .65f); renderer.sortingOrder = -690;
        }
        private void AddGlow(Vector2 position, Vector2 size, Color color, int order)
        {
            var node = new GameObject("TownWarmGlow", typeof(SpriteRenderer)); node.transform.SetParent(transform); node.transform.position = position; node.transform.localScale = size;
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = SoftDisc; renderer.sortingOrder = order; renderer.color = color;
            glows.Add(new Glow { Renderer = renderer, Color = color, Scale = node.transform.localScale, Phase = glows.Count * 2.1f });
        }

        public static void Footstep(Vector2 position, bool running)
        {
            if (instance == null) return;
            instance.Spawn(position + new Vector2(Random.Range(-.13f, .13f), 0), new Vector2(Random.Range(-.11f, .11f), .14f),
                new Color(.74f, .68f, .51f, running ? .32f : .20f), running ? .35f : .25f, running ? .65f : .4f, .38f, -640);
        }

        private void Spawn(Vector2 position, Vector2 velocity, Color color, float size, float duration, float growth, int order)
        {
            var particle = particles[cursor++ % particles.Length];
            particle.Render.enabled = true; particle.Render.sortingOrder = order; particle.Render.transform.position = position;
            particle.Velocity = velocity; particle.Color = color; particle.Size = size; particle.Life = particle.Duration = duration; particle.Growth = growth; particle.Wobble = Random.Range(0, 6.28f);
            particle.Render.color = color; particle.Render.transform.localScale = Vector3.one * size;
        }

        private void Update()
        {
            var delta = Time.deltaTime; var time = Time.time;
            foreach (var breeze in breezes)
                if (breeze.Transform != null) breeze.Transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * .65f + breeze.Phase) * .55f);
            foreach (var merchant in merchants)
                if (merchant.Transform != null) merchant.Transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 1.7f + merchant.Phase) * .35f);
            foreach (var glow in glows)
            {
                var flicker = .91f + .06f * Mathf.Sin(time * 3.7f + glow.Phase) + .03f * Mathf.Sin(time * 7.1f + glow.Phase);
                var color = glow.Color; color.a *= flicker; glow.Renderer.color = color;
                glow.Renderer.transform.localScale = glow.Scale * (1 + .03f * Mathf.Sin(time * 1.4f + glow.Phase));
            }
            foreach (var emitter in emitters)
            {
                emitter.Timer -= delta; if (emitter.Timer > 0) continue;
                if (emitter.Kind == 1)
                {
                    emitter.Timer = .32f;
                    Spawn(emitter.Position, new Vector2(.20f, .72f), emitter.Color, .43f, 2.2f, .52f, 17000);
                }
                else if (emitter.Kind == 2)
                {
                    emitter.Timer = .09f;
                    var offset = new Vector2(Random.Range(-.18f, .18f), Random.Range(-.06f, .12f));
                    Spawn(emitter.Position + offset, new Vector2(offset.x * 1.4f, -.3f), emitter.Color, .055f, .48f, .025f, Order(emitter.Position.y - 1.25f) + 2);
                }
                else if (emitter.Kind == 3)
                {
                    emitter.Timer = 1.2f + Random.value;
                    Spawn(emitter.Position + new Vector2(Random.Range(-.45f, .45f), .4f), new Vector2(.4f, -.17f), emitter.Color, .07f, 2.8f, 0, Order(emitter.Position.y) + 3);
                }
                else
                {
                    emitter.Timer = .17f;
                    Spawn(emitter.Position + new Vector2(Random.Range(-.6f, .6f), -.9f), new Vector2(.03f, .58f), emitter.Color, .075f, 1.4f, .05f, Order(emitter.Position.y - 1.2f) + 3);
                }
            }
            foreach (var particle in particles)
            {
                if (particle.Life <= 0) continue;
                particle.Life -= delta;
                if (particle.Life <= 0) { particle.Render.enabled = false; continue; }
                var age = particle.Duration - particle.Life;
                var drift = particle.Velocity + Vector2.right * (Mathf.Sin(time * 2 + particle.Wobble) * .045f);
                particle.Render.transform.position += (Vector3)(drift * delta);
                particle.Render.transform.localScale = Vector3.one * (particle.Size + age * particle.Growth);
                var color = particle.Color; color.a *= Mathf.Clamp01(particle.Life / particle.Duration * 1.5f); particle.Render.color = color;
            }
        }
    }

    /// <summary>Ambient residents use real baked idle/walk frames without controlling the player.</summary>
    public sealed class TownResident : MonoBehaviour
    {
        private SpriteAnimator animator;
        private SpriteRenderer spriteView;
        private Vector2[] route;
        private int waypoint;
        private float speed, rest;

        public static void Create(Transform parent, string name, Vector2[] points, Color tint, float speed)
        {
            var node = new GameObject(name, typeof(SpriteRenderer)); node.transform.SetParent(parent); node.transform.position = points[0]; node.transform.localScale = Vector3.one * .82f;
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.color = tint;
            var animation = node.AddComponent<SpriteAnimator>();
            animation.Configure(new SpriteAnimator.ActionSet { Name = "idle", Sheet = Resources.Load<Texture2D>("Art/Sprites/KnightVariant_Idle_sheet"), Columns = 12, Fps = 12 },
                new SpriteAnimator.ActionSet { Name = "walk", Sheet = Resources.Load<Texture2D>("Art/Sprites/KnightVariant_Walking_A_sheet"), Columns = 12, Fps = 12 });
            animation.Play("idle");
            var resident = node.AddComponent<TownResident>(); resident.route = points; resident.animator = animation; resident.spriteView = renderer; resident.speed = speed; resident.waypoint = points.Length > 1 ? 1 : 0;
            TownAtmosphere.MakePlayerShadow(node.transform, new Vector2(0, -.63f));
        }

        private void Update()
        {
            if (route == null || route.Length == 0) return;
            spriteView.sortingOrder = TownAtmosphere.Order(transform.position.y - .52f);
            if (route.Length == 1 || speed <= 0) return;
            rest -= Time.deltaTime;
            var direction = route[waypoint] - (Vector2)transform.position;
            if (rest > 0 || direction.sqrMagnitude < .035f)
            {
                animator.Play("idle");
                if (rest <= 0) { waypoint = (waypoint + 1) % route.Length; rest = 1.5f; }
                return;
            }
            animator.Row = TopDownPlayerController.DirectionRow(direction);
            animator.Play("walk");
            transform.position = Vector2.MoveTowards(transform.position, route[waypoint], speed * Time.deltaTime);
        }
    }
}
