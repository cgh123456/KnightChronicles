using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Original stone, parchment and tarnished brass textures for the game UI.</summary>
    public static class UiTheme
    {
        public static readonly Color Gold = new Color(.83f, .68f, .39f);
        public static readonly Color Ink = new Color(.055f, .062f, .069f);
        public static readonly Color Text = new Color(.91f, .87f, .76f);
        public static readonly Color Muted = new Color(.64f, .66f, .63f);
        private static Texture2D stone, inset, brass, hover, shade;
        public static Texture2D Stone { get { return stone == null ? stone = Make(128, false, false) : stone; } }
        public static Texture2D Inset { get { return inset == null ? inset = Make(64, true, false) : inset; } }
        public static Texture2D Brass { get { return brass == null ? brass = Make(64, false, true) : brass; } }
        public static Texture2D Hover { get { return hover == null ? hover = Make(64, false, true, true) : hover; } }
        public static Texture2D Shade
        {
            get { if (shade == null) { shade = new Texture2D(1, 1, TextureFormat.RGBA32, false); shade.SetPixel(0, 0, new Color(.015f, .018f, .023f, .86f)); shade.Apply(); } return shade; }
        }
        private static Texture2D Make(int size, bool recessed, bool gold, bool bright = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "KnightUI_" + (gold ? "Brass" : "Stone"), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var edge = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
                var noise = Mathf.PerlinNoise(x * .23f, y * .23f) * .025f + Mathf.PerlinNoise(x * .04f, y * .04f) * .018f;
                var c = gold ? new Color(.18f + noise, .16f + noise, .12f + noise) : new Color(.055f + noise, .065f + noise, .072f + noise);
                if (recessed) c *= .68f;
                if (bright) c += new Color(.08f, .066f, .03f);
                if (edge == 0) c = new Color(.023f, .026f, .027f);
                else if (edge == 1) c = Gold * (gold ? .82f : .58f);
                else if (edge == 2) c = new Color(.023f, .026f, .027f);
                else if (edge == 3) c = Gold * (gold ? .42f : .24f);
                if (!recessed && !gold && edge >= 5 && (x < 12 || x >= size - 12) && (y < 12 || y >= size - 12))
                    c = edge == 5 || x == 8 || y == 8 || x == size - 9 || y == size - 9 ? Gold * .6f : c;
                c.a = .985f; pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels); tex.Apply(); return tex;
        }
        public static GUIStyle Panel(int padding = 20)
        {
            var style = new GUIStyle(GUI.skin.box) { padding = new RectOffset(padding, padding, padding, padding), border = new RectOffset(14, 14, 14, 14) };
            style.normal.background = Stone; return style;
        }
        public static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        public static void Frame(Rect rect, Color color, float thickness = 1)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color); Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color); Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
