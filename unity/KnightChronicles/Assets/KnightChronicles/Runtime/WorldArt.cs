using UnityEngine;

namespace KnightChronicles.Runtime
{
    public static class WorldArt
    {
        private static Sprite square, circle;
        public static Sprite Square
        {
            get { if (square == null) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, Color.white); t.Apply();
                square = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1); } return square; }
        }
        public static Sprite Circle
        {
            get { if (circle == null) { var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++) t.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) < 15 ? Color.white : Color.clear);
                t.Apply(); circle = Sprite.Create(t, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32); } return circle; }
        }
        public static GameObject Shape(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order, bool round = false)
        {
            var node = new GameObject(name, typeof(SpriteRenderer)); node.transform.SetParent(parent);
            node.transform.position = position; node.transform.localScale = size;
            var sr = node.GetComponent<SpriteRenderer>(); sr.sprite = round ? Circle : Square; sr.color = color; sr.sortingOrder = order; return node;
        }
        public static void Label(Transform parent, string text, Vector2 position, Color color, float size = 0.12f)
        {
            var node = new GameObject("Label", typeof(TextMesh)); node.transform.SetParent(parent); node.transform.position = position;
            var mesh = node.GetComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 40; mesh.characterSize = size; mesh.color = color; mesh.anchor = TextAnchor.MiddleCenter;
            mesh.font = GameFont.World; if (mesh.font != null) node.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            node.GetComponent<MeshRenderer>().sortingOrder = 30;
        }
        public static Color Tier(int tier) { return new[] { new Color(0.9f, 0.9f, 0.8f), new Color(0.45f, 0.85f, 0.5f), new Color(0.3f, 0.7f, 1), new Color(0.8f, 0.45f, 1), new Color(1, 0.6f, 0.1f) }[tier]; }
        public static SpriteAnimator AddKnight(GameObject node)
        {
            var a = node.AddComponent<SpriteAnimator>();
            var sets=new System.Collections.Generic.List<SpriteAnimator.ActionSet>{Set("idle", "Idle", 12), Set("walk", "Walking_A", 12), Set("run", "Running_A", 12), Set("dodge", "Dodge_Forward", 10)};
            foreach(var action in new[]{"Attack","Hit","Cast"})
            {var path="Art/Sprites/KnightVariant_"+action+"_sheet";var texture=Resources.Load<Texture2D>(path);if(texture!=null)sets.Add(new SpriteAnimator.ActionSet{Name=action.ToLowerInvariant(),Sheet=texture,Columns=8,Fps=22,PixelsPerUnit=ModelArt.MotionPixelsPerUnit(path)});}
            a.Configure(sets.ToArray());
            a.Play("idle"); return a;
        }
        public static void Skeleton(GameObject node, int type, int difficulty)
        {
            var p = (Vector2)node.transform.position; var bone = new Color(.85f, .83f, .68f);
            var robe = type >= 2 ? new Color(.35f, .22f, .55f) : new Color(.35f + difficulty * .05f, .30f, .25f);
            node.GetComponent<SpriteRenderer>().sprite = Square;
            node.GetComponent<SpriteRenderer>().color = robe;
            Shape("Skull", node.transform, p + Vector2.up * .48f, new Vector2(.6f, .54f), bone, 13, true);
            Shape("Eye", node.transform, p + new Vector2(-.13f, .53f), Vector2.one * .1f, new Color(.1f, .1f, .13f), 14, true);
            Shape("Eye", node.transform, p + new Vector2(.13f, .53f), Vector2.one * .1f, new Color(.1f, .1f, .13f), 14, true);
            Shape("Jaw", node.transform, p + Vector2.up * .28f, new Vector2(.36f, .15f), bone, 13);
            Shape("Spine", node.transform, p, new Vector2(.12f, .62f), bone, 13);
            for (var i = 0; i < 3; i++) Shape("Ribs", node.transform, p + Vector2.up * (i * .15f - .06f), new Vector2(.42f, .07f), bone, 13);
            Shape("LeftLeg", node.transform, p + new Vector2(-.2f, -.58f), new Vector2(.12f, .5f), bone, 13);
            Shape("RightLeg", node.transform, p + new Vector2(.2f, -.58f), new Vector2(.12f, .5f), bone, 13);
            Shape("Arm", node.transform, p + new Vector2(-.45f, .1f), new Vector2(.13f, .48f), bone, 13);
            Shape("Arm", node.transform, p + new Vector2(.45f, .1f), new Vector2(.13f, .48f), bone, 13);
            if (type >= 2) { Shape("Staff", node.transform, p + new Vector2(.63f, .1f), new Vector2(.08f, 1.5f), new Color(.55f, .35f, .18f), 12);
                Shape("Orb", node.transform, p + new Vector2(.63f, .9f), Vector2.one * .22f, Color.magenta, 14, true); }
            else Shape("Blade", node.transform, p + new Vector2(.65f, .2f), new Vector2(.16f, 1), new Color(.7f, .8f, .9f), 14);
        }
        private static SpriteAnimator.ActionSet Set(string name, string action, int columns)
        {
            var path="Art/Sprites/KnightVariant_"+action+"_sheet";
            return new SpriteAnimator.ActionSet { Name = name, Sheet = Resources.Load<Texture2D>(path), Columns = columns, Fps = 12,PixelsPerUnit=ModelArt.MotionPixelsPerUnit(path) };
        }
    }
    public static class GameFont
    {
        private static Font world;
        public static Font World { get { if (world == null) world = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 32); return world; } }
    }
}
