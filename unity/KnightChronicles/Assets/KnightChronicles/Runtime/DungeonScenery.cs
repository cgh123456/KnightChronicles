using System;
using System.Collections.Generic;
using KnightChronicles.Runtime.Core;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Art follows the generator's walkable geometry. Doorways are cut only for open connections.</summary>
    public static class DungeonScenery
    {
        private static readonly List<Rect> obstacles=new List<Rect>();
        private static readonly int[] avoidanceTurns={45,90,135,-45,-90,-135};
        public static bool ClearPoint(float x,float y,float radius=.18f)
        { foreach(var rect in obstacles) if(x>rect.xMin-radius&&x<rect.xMax+radius&&y>rect.yMin-radius&&y<rect.yMax+radius)return false;return true; }
        public static Vector2 StepToward(FloorData floor,Vector2 position,Vector2 target,float step,int side=1)
        {
            var delta=Vector2.ClampMagnitude(target-position,step);var next=position+delta;
            if(WalkPoint(floor,next))return next;
            var meaningful=delta.magnitude*.45f;
            if(Mathf.Abs(delta.x)>meaningful&&WalkPoint(floor,new Vector2(next.x,position.y)))return new Vector2(next.x,position.y);
            if(Mathf.Abs(delta.y)>meaningful&&WalkPoint(floor,new Vector2(position.x,next.y)))return new Vector2(position.x,next.y);
            // A zero component is no movement. Take a consistent tangent around collidable props.
            foreach(var turn in avoidanceTurns)
            {
                var angle=turn*side*Mathf.Deg2Rad;
                next=position+new Vector2(delta.x*Mathf.Cos(angle)-delta.y*Mathf.Sin(angle),delta.x*Mathf.Sin(angle)+delta.y*Mathf.Cos(angle));
                if(WalkPoint(floor,next))return next;
            }
            return position;
        }
        private static bool WalkPoint(FloorData floor,Vector2 p)
        {return DungeonGenerator.Walkable(floor,p.x,p.y)&&ClearPoint(p.x,p.y);}
        public static readonly string[] Themes = { "遗忘地窖", "苔石墓道", "失落藏书室", "余烬监牢", "古王秘库" };
        private static readonly Color[] FloorColors = {
            new Color(.8f,.83f,.91f), new Color(.67f,.84f,.75f), new Color(.85f,.74f,.9f),
            new Color(.94f,.69f,.56f), new Color(.94f,.86f,.64f) };
        private static readonly Color[] Lights = { new Color(1,.6f,.23f,.38f), new Color(.3f,.85f,.6f,.28f),
            new Color(.65f,.48f,1,.35f), new Color(1,.32f,.12f,.4f), new Color(1,.77f,.32f,.4f) };
        public static int FootOrder(float y) { return 5000 - Mathf.RoundToInt(y * 10); }
        public static GameObject Prop(string key, Transform parent, Vector2 foot, Vector2 size)
        {
            if(key=="pillar"||key=="bookshelf"||key=="chest"||key=="crate"||key=="urn"||key=="barrel")
                obstacles.Add(new Rect(foot.x-size.x*.33f,foot.y-.18f,size.x*.66f,Mathf.Min(.5f,size.y*.3f)));
            return ModelArt.Place(key, parent, foot + Vector2.up * size.y * .28f, size, FootOrder(foot.y));
        }
        public static void Render(FloorData floor, Transform parent, int seed)
        {
            obstacles.Clear();
            var random = new System.Random(seed ^ floor.Number * 7919); var tint = FloorColors[floor.Number - 1];
            foreach (var room in floor.Rooms)
            {
                if (!room.Opened) continue;
                var c = new Vector2(room.X * 16, room.Y * 16);
                WorldArt.Shape("StoneFoundation", parent, c, Vector2.one * 12.2f, new Color(.16f,.16f,.17f), -1002);
                for (var x = -5; x <= 5; x += 2) for (var y = -5; y <= 5; y += 2)
                {
                    var tile = ModelArt.Place("stone_floor", parent, c + new Vector2(x, y), Vector2.one * 2.03f, -1000);
                    var sr = tile.GetComponent<SpriteRenderer>(); sr.color = tint * (float)(.91 + random.NextDouble() * .13); sr.color = new Color(sr.color.r,sr.color.g,sr.color.b,1);
                }
                // Uneven wear breaks the visual repetition without changing the generated walkable layout.
                for(var patch=0;patch<4;patch++)
                {
                    var pos=c+new Vector2((float)(random.NextDouble()*9-4.5),(float)(random.NextDouble()*9-4.5));
                    var stain=WorldArt.Shape("FloorWear",parent,pos,new Vector2(1.7f,1.2f),
                        floor.Number==2?new Color(.1f,.28f,.17f,.18f):new Color(.06f,.04f,.035f,.15f),-999);
                    stain.GetComponent<SpriteRenderer>().sprite=CombatFeedback.Glow;
                }
                if (room.Parent >= 0 && floor.Rooms[room.Parent].Opened)
                {
                    var p = floor.Rooms[room.Parent]; var a = new Vector2(p.X * 16,p.Y * 16);
                    var delta = c-a; var middle=(c+a)*.5f;
                    WorldArt.Shape("PassageFoundation",parent,middle,new Vector2(Mathf.Abs(delta.x)>0?4.8f:3.2f,Mathf.Abs(delta.y)>0?4.8f:3.2f),new Color(.19f,.20f,.22f),-1002);
                    for(var i=-1;i<=1;i++) for(var j=-1;j<=1;j++)
                    { var horizontal=Mathf.Abs(delta.x)>0;var step=new Vector2(horizontal?1.6f:1.06f,horizontal?1.06f:1.6f);
                      var tile=ModelArt.Place("stone_floor",parent,middle+new Vector2(i*step.x,j*step.y),step+Vector2.one*.025f,-1001); tile.GetComponent<SpriteRenderer>().color=tint; }
                }
                for (var side=0;side<4;side++)
                {
                    var direction=side==0?Vector2.up:side==1?Vector2.right:side==2?Vector2.down:Vector2.left;
                    var open=floor.Rooms.Exists(r=>r.Opened && r.X==room.X+(int)direction.x && r.Y==room.Y+(int)direction.y
                        && (r.Parent==floor.Rooms.IndexOf(room) || room.Parent==floor.Rooms.IndexOf(r)));
                    for(var offset=-5;offset<=5;offset+=2)
                    {
                        if(open&&Mathf.Abs(offset)<=1)continue;
                        var foot=c+direction*5.95f+(side%2==0?Vector2.right:Vector2.up)*offset;
                        var wall=Prop(side%2==0?"stone_wall":"stone_wall_side",parent,foot,new Vector2(side%2==0?2.05f:.95f,side%2==0?1.65f:2.2f));
                        wall.name="MasonryWall"; wall.GetComponent<SpriteRenderer>().color=tint;
                        wall.AddComponent<WallOcclusion>().Foot=foot;
                    }
                    if(open)
                    { Prop("pillar",parent,c+direction*5.8f+(side%2==0?Vector2.right:Vector2.up)*2,new Vector2(.85f,2.2f));
                      Prop("pillar",parent,c+direction*5.8f-(side%2==0?Vector2.right:Vector2.up)*2,new Vector2(.85f,2.2f)); }
                }
                foreach(var x in new[]{-4f,4f})
                {
                    var foot=c+new Vector2(x,4.7f); Prop("torch",parent,foot,new Vector2(.55f,1.4f));
                    Light(parent,foot+Vector2.up*.65f,3.8f,Lights[floor.Number-1]);
                }
                // Set dressing remains at edges, outside the route to stairs and the searchable containers.
                for(var i=0;i<3;i++)
                { var p=c+new Vector2((i%2==0?-1:1)*4.5f,-4.4f+i*.6f);
                  var key=floor.Number==3?"bookshelf":floor.Number==5?"urn":i==0?"barrel":"rocks";
                  Prop(key,parent,p,new Vector2(1.05f,1.2f)); }
                if(room.Hidden) { var rug=WorldArt.Shape("HiddenRoomRug",parent,c,new Vector2(5,4),new Color(.3f,.18f,.35f,.6f),-998);
                    Prop("chest",parent,c+new Vector2(0,4.4f),new Vector2(1.4f,1.2f)); }
                else if(floor.Number==3&&floor.Rooms.IndexOf(room)%3==1)
                {var carpet=WorldArt.Shape("LibraryRunner",parent,c,new Vector2(3,7),new Color(.28f,.14f,.2f,.48f),-998);
                 foreach(var x in new[]{-3.8f,3.8f})Prop("bookshelf",parent,c+new Vector2(x,1.5f),new Vector2(1.3f,1.8f));}
            }
            foreach(var room in floor.Rooms)
            {
                if(room.Opened||room.Parent<0)continue;
                var p=floor.Rooms[room.Parent];var door=new Vector2((room.X+p.X)*8,(room.Y+p.Y)*8);
                Prop("door",parent,door,new Vector2(2.5f,2.8f));Light(parent,door,2.4f,new Color(.65f,.4f,.9f,.28f));
            }
        }
        public static GameObject Light(Transform parent,Vector2 pos,float size,Color color)
        {
            var node=WorldArt.Shape("AmbientLight",parent,pos,Vector2.one*size,color,8000);
            node.GetComponent<SpriteRenderer>().sprite=CombatFeedback.Glow;node.AddComponent<WorldPulse>();return node;
        }
    }
    public sealed class WallOcclusion : MonoBehaviour
    {
        public Vector2 Foot;
        private SpriteRenderer sprite;
        private Color tint;
        private Transform player;
        private void Start() { sprite=GetComponent<SpriteRenderer>();tint=sprite.color;var p=GameObject.FindGameObjectWithTag("Player");if(p!=null)player=p.transform; }
        private void LateUpdate()
        {
            if(player==null)return; var delta=(Vector2)player.position-Foot;
            var target=Mathf.Abs(delta.x)<1.5f&&delta.y>.1f&&delta.y<1.8f?.35f:1f;
            tint.a=Mathf.Lerp(sprite.color.a,target,1-Mathf.Exp(-Time.deltaTime*12));sprite.color=tint;
        }
    }
}
