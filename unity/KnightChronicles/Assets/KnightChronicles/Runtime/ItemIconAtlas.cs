using System.Collections.Generic;
using KnightChronicles.Runtime.Core;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Original rasterized silhouettes, metal bevels and alchemy glass; shared by UI and world drops.</summary>
    public static class ItemIconAtlas
    {
        private const int Size = 96;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        public static Texture2D Get(ItemDefinition definition)
        {
            var effect = definition.Effect ?? "";
            var visualEffect = definition.Kind == ItemKind.Potion || definition.Kind == ItemKind.Scroll ? effect : "";
            var key = definition.Kind + ":" + definition.Branch + ":" + definition.Slot + ":" + visualEffect + ":" + definition.Tier;
            Texture2D result; if (Cache.TryGetValue(key, out result) && result != null) return result;
            var canvas = new Canvas(); var tier = WorldArt.Tier(definition.Tier);
            var metal = Color.Lerp(new Color(.69f, .75f, .77f), tier, .32f);
            var leather = new Color(.36f, .21f, .12f); var light = new Color(.92f, .93f, .86f);
            if (definition.Kind == ItemKind.Armor)
            { if(definition.Branch=="robe")metal=Color.Lerp(new Color(.25f,.37f,.58f),tier,.2f);else if(definition.Branch=="leather")metal=Color.Lerp(new Color(.48f,.31f,.17f),tier,.22f);else if(definition.Branch=="heavy")metal=Color.Lerp(new Color(.43f,.49f,.53f),tier,.26f);else if(definition.Branch=="light")metal=Color.Lerp(new Color(.38f,.57f,.59f),tier,.27f); }
            switch (definition.Kind)
            {
                case ItemKind.Weapon:
                    if (definition.Branch == "staff") { canvas.Line(24, 16, 67, 71, 7, leather); canvas.Line(26, 17, 66, 70, 2, new Color(.7f, .47f, .24f)); canvas.Polygon(new[]{new Vector2(56,69),new Vector2(59,84),new Vector2(75,86),new Vector2(83,72),new Vector2(71,61)}, metal); canvas.Circle(69, 76, 8, tier); canvas.Circle(67, 79, 3, light); }
                    else if (definition.Branch == "spear") { canvas.Line(19, 14, 70, 76, 5, leather); canvas.Line(19, 15, 68, 73, 1.7f, new Color(.67f,.46f,.25f)); canvas.Polygon(new[]{new Vector2(60,70),new Vector2(77,91),new Vector2(83,64),new Vector2(71,58)}, metal); canvas.Line(70,65,77,87,2,light); canvas.Line(61,62,74,53,4,tier); }
                    else { var dagger = definition.Branch == "dagger"; var blade = definition.Branch == "blade"; canvas.Line(22, 20, 39, 38, 9, leather); canvas.Line(18,18,27,26,4,metal); canvas.Polygon(blade ? new[]{new Vector2(34,42),new Vector2(71,80),new Vector2(85,87),new Vector2(81,67),new Vector2(43,31)} : new[]{new Vector2(34,42),new Vector2(dagger?62:76,dagger?68:83),new Vector2(dagger?76:86,dagger?79:92),new Vector2(dagger?73:83,dagger?61:71),new Vector2(44,32)},metal); canvas.Line(41,40,dagger?69:80,dagger?71:84,2.5f,light); canvas.Line(24,48,49,25,5,tier); canvas.Circle(37,37,4,new Color(.94f,.74f,.35f)); }
                    break;
                case ItemKind.Armor:
                    if (definition.Slot == Slot.Head) { canvas.Polygon(new[]{new Vector2(22,27),new Vector2(20,54),new Vector2(29,77),new Vector2(48,85),new Vector2(68,75),new Vector2(77,53),new Vector2(72,27),new Vector2(49,15)},metal); canvas.Polygon(new[]{new Vector2(26,52),new Vector2(69,52),new Vector2(67,44),new Vector2(51,42),new Vector2(48,26),new Vector2(44,43),new Vector2(28,43)},new Color(.04f,.065f,.08f)); canvas.Line(49,80,49,55,3,light); canvas.Line(29,65,39,76,3,light); canvas.Line(24,27,47,18,3,tier); }
                    else if(definition.Slot==Slot.Legs) { canvas.Polygon(new[]{new Vector2(24,79),new Vector2(72,79),new Vector2(72,54),new Vector2(67,18),new Vector2(48,15),new Vector2(45,53),new Vector2(41,18),new Vector2(21,17)},metal); canvas.Line(28,74,65,74,4,leather); canvas.Line(34,58,30,26,3,light); canvas.Line(57,59,57,26,3,light); canvas.Line(22,25,39,24,5,tier); canvas.Line(49,24,68,24,5,tier); }
                    else { canvas.Polygon(new[]{new Vector2(19,73),new Vector2(34,82),new Vector2(42,72),new Vector2(54,72),new Vector2(63,82),new Vector2(77,73),new Vector2(84,54),new Vector2(67,49),new Vector2(64,17),new Vector2(33,17),new Vector2(28,49),new Vector2(12,55)},metal); canvas.Line(33,64,46,54,3,light); canvas.Line(62,64,50,54,3,light); canvas.Line(34,27,63,27,5,leather); canvas.Circle(48,48,7,tier); canvas.Circle(48,48,3,light); }
                    break;
                case ItemKind.Rig: canvas.Rectangle(21,26,73,74,leather); canvas.Line(29,73,33,89,7,leather);canvas.Line(65,73,61,89,7,leather); canvas.Rectangle(26,30,44,59,new Color(.5f,.35f,.19f));canvas.Rectangle(50,30,68,59,new Color(.5f,.35f,.19f));canvas.Line(28,55,42,55,3,tier);canvas.Line(52,55,66,55,3,tier);canvas.Rectangle(42,63,54,72,metal); break;
                case ItemKind.Pack: canvas.Line(21,27,23,70,7,new Color(.22f,.13f,.09f));canvas.Line(73,27,73,70,7,new Color(.22f,.13f,.09f));canvas.Polygon(new[]{new Vector2(26,15),new Vector2(70,15),new Vector2(76,68),new Vector2(65,84),new Vector2(30,84),new Vector2(20,68)},new Color(.43f,.31f,.19f));canvas.Rectangle(27,29,69,57,leather);canvas.Line(29,77,67,77,5,tier);canvas.Line(35,26,35,79,4,new Color(.65f,.5f,.27f));canvas.Line(59,26,59,79,4,new Color(.65f,.5f,.27f));canvas.Rectangle(42,50,53,62,metal);break;
                case ItemKind.Potion:
                    var liquid = effect.StartsWith("hp") ? new Color(.85f,.15f,.17f) : effect.StartsWith("mp") ? new Color(.14f,.44f,.95f) : effect=="attack" ? new Color(.94f,.34f,.13f) : effect=="speed" ? new Color(.39f,.75f,.2f) : new Color(.94f,.66f,.12f);
                    canvas.Circle(48,37,26,new Color(.56f,.72f,.74f));canvas.Rectangle(37,53,59,78,new Color(.56f,.72f,.74f));canvas.Circle(48,36,21,liquid);canvas.Rectangle(36,72,60,83,leather);canvas.Line(30,47,30,31,4,new Color(.91f,.96f,.89f));canvas.Circle(40,50,4,light);canvas.Rectangle(43,23,53,43,new Color(.94f,.83f,.62f));canvas.Rectangle(38,28,58,38,new Color(.94f,.83f,.62f));break;
                case ItemKind.Scroll:
                    canvas.Polygon(new[]{new Vector2(23,23),new Vector2(70,23),new Vector2(77,77),new Vector2(33,77)},new Color(.79f,.68f,.43f));canvas.Line(23,23,69,23,12,leather);canvas.Line(30,78,78,78,12,new Color(.92f,.81f,.57f));canvas.Line(27,24,65,24,5,new Color(.94f,.84f,.6f));canvas.Line(40,60,65,60,2,new Color(.27f,.2f,.12f));canvas.Line(38,53,62,53,2,new Color(.27f,.2f,.12f));
                    if(effect=="return"){var c=new Color(.12f,.42f,.38f);canvas.Line(40,35,57,35,3,c);canvas.Line(57,35,57,46,3,c);canvas.Line(57,46,41,46,3,c);canvas.Line(41,46,47,52,3,c);canvas.Line(41,46,47,40,3,c);}
                    else if(effect=="fireball"){canvas.Circle(49,40,9,new Color(.76f,.19f,.08f));canvas.Polygon(new[]{new Vector2(42,42),new Vector2(49,57),new Vector2(55,42)},new Color(.93f,.4f,.1f));canvas.Circle(49,39,4,new Color(1,.77f,.21f));}
                    else if(effect=="detect"){canvas.Polygon(new[]{new Vector2(36,41),new Vector2(49,49),new Vector2(63,41),new Vector2(49,33)},new Color(.28f,.33f,.32f));canvas.Circle(49,41,5,new Color(.93f,.73f,.2f));canvas.Circle(49,41,2,new Color(.14f,.21f,.23f));}
                    else if(effect=="shield"){canvas.Polygon(new[]{new Vector2(39,49),new Vector2(59,49),new Vector2(57,37),new Vector2(49,29),new Vector2(41,37)},new Color(.16f,.36f,.57f));canvas.Line(49,32,49,46,2,light);}
                    else if(effect=="strength"){canvas.Line(43,32,55,48,4,new Color(.57f,.12f,.13f));canvas.Line(39,39,49,31,3,new Color(.57f,.12f,.13f));canvas.Line(51,47,57,53,2,light);}
                    else{canvas.Polygon(new[]{new Vector2(49,54),new Vector2(53,44),new Vector2(62,40),new Vector2(53,36),new Vector2(49,26),new Vector2(45,36),new Vector2(36,40),new Vector2(45,44)},new Color(.91f,.85f,.69f));}
                    break;
                case ItemKind.Key: canvas.Circle(65,69,17,new Color(.73f,.55f,.24f));canvas.Circle(65,69,9,Color.clear,true);canvas.Line(53,57,21,23,8,new Color(.73f,.55f,.24f));canvas.Line(28,31,39,21,7,new Color(.73f,.55f,.24f));canvas.Line(20,24,30,14,7,new Color(.73f,.55f,.24f));canvas.Line(53,59,24,28,2,light);break;
                case ItemKind.Material: canvas.Polygon(new[]{new Vector2(17,29),new Vector2(29,59),new Vector2(61,80),new Vector2(80,48),new Vector2(68,18),new Vector2(37,14)},Color.Lerp(new Color(.41f,.45f,.48f),tier,.55f));canvas.Polygon(new[]{new Vector2(29,59),new Vector2(61,80),new Vector2(55,42),new Vector2(37,14)},Color.Lerp(metal,tier,.5f));canvas.Polygon(new[]{new Vector2(61,80),new Vector2(80,48),new Vector2(55,42)},light*.7f);canvas.Line(29,59,55,42,2,light);break;
                default: canvas.Polygon(new[]{new Vector2(20,35),new Vector2(28,69),new Vector2(69,76),new Vector2(80,45),new Vector2(65,20),new Vector2(32,20)},tier);canvas.Polygon(new[]{new Vector2(28,69),new Vector2(48,47),new Vector2(69,76)},light*.85f);canvas.Line(48,47,32,20,3,light);canvas.Line(48,47,80,45,3,light);break;
            }
            if (definition.Tier >= 2) { canvas.Circle(79,17,5,tier);canvas.Line(79,9,79,25,1.7f,light);canvas.Line(71,17,87,17,1.7f,light); }
            result = canvas.Texture("Icon_" + key); Cache[key] = result; return result;
        }
        private sealed class Canvas
        {
            private readonly Color[] pixels = new Color[Size*Size];
            public void Rectangle(int x0,int y0,int x1,int y1,Color color) { Polygon(new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)},color); }
            public void Circle(float x,float y,float radius,Color color,bool replace=false)
            { for(var yy=Mathf.Max(0,Mathf.FloorToInt(y-radius-1));yy<Mathf.Min(Size,Mathf.CeilToInt(y+radius+1));yy++)for(var xx=Mathf.Max(0,Mathf.FloorToInt(x-radius-1));xx<Mathf.Min(Size,Mathf.CeilToInt(x+radius+1));xx++) {var a=Mathf.Clamp01(radius+.5f-Vector2.Distance(new Vector2(xx+.5f,yy+.5f),new Vector2(x,y))); if(a>0)Put(xx,yy,color,a,replace);} }
            public void Line(float x0,float y0,float x1,float y1,float width,Color color)
            {var a=new Vector2(x0,y0);var b=new Vector2(x1,y1);var d=b-a;for(var y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(y0,y1)-width));y<Mathf.Min(Size,Mathf.CeilToInt(Mathf.Max(y0,y1)+width));y++)for(var x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(x0,x1)-width));x<Mathf.Min(Size,Mathf.CeilToInt(Mathf.Max(x0,x1)+width));x++){var p=new Vector2(x+.5f,y+.5f);var t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);var alpha=Mathf.Clamp01(width*.5f+.5f-Vector2.Distance(p,a+d*t));if(alpha>0)Put(x,y,color,alpha);} }
            public void Polygon(Vector2[] vertices,Color color)
            {for(var y=0;y<Size;y++)for(var x=0;x<Size;x++){var inside=false;for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++){var a=vertices[i];var b=vertices[j];if((a.y>y+.5f)!=(b.y>y+.5f)&&(x+.5f<(b.x-a.x)*(y+.5f-a.y)/(b.y-a.y)+a.x))inside=!inside;}if(inside)Put(x,y,color,1);} }
            private void Put(int x,int y,Color color,float coverage,bool replace=false)
            { var i=y*Size+x; if(replace){pixels[i]=Color.Lerp(pixels[i],color,coverage);return;}var alpha=color.a*coverage;var previous=pixels[i];var total=alpha+previous.a*(1-alpha);var mixed=total>0?(color*alpha+previous*previous.a*(1-alpha))/total:Color.clear;mixed.a=total;pixels[i]=mixed; }
            public Texture2D Texture(string name){var result=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};result.SetPixels(pixels);result.Apply();return result;}
        }
    }
}
