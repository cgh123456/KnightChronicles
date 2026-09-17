using System.Collections.Generic;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Walkable town assembled from the same camera-baked models as the dungeon.</summary>
    public sealed class TownBootstrap : MonoBehaviour
    {
        public const float WorldWidth = 46f;
        public const float WorldHeight = 38f;
        private readonly List<Interactable> interactables = new List<Interactable>();
        private Transform world, player;
        private TextMesh interactPrompt;
        private Interactable nearest;
        private TownAtmosphere atmosphere;

        private void Awake()
        {
            AudioDirector.Ensure();
            if (GameSession.State.ActiveRun != null)
            {
                gameObject.AddComponent<DungeonController>();
                enabled = false;
                return;
            }
            world = new GameObject("TownWorld").transform;
            world.SetParent(transform);
            atmosphere = gameObject.AddComponent<TownAtmosphere>();
            CreateCamera();
            CreateGround();
            CreateStreets();
            CreateBuildings();
            CreateGardens();
            CreateResidents();
            CreatePlayer();
            CreatePrompt();
            gameObject.AddComponent<GamePanel>();
            GameSession.Save();
        }

        private static void CreateCamera()
        {
            var node = new GameObject("TownCamera", typeof(Camera), typeof(AudioListener));
            var camera = node.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.3f * GameSession.State.Zoom;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.19f, .26f, .21f);
            node.transform.position = new Vector3(0, -3.8f, -10);
            node.tag = "MainCamera";
            node.AddComponent<CameraFollow2D>().SetBounds(new Rect(-22.5f, -18.5f, 45, 37));
        }

        private void CreateGround()
        {
            WorldArt.Shape("MeadowUnderlay", world, Vector2.zero, new Vector2(WorldWidth, WorldHeight), new Color(.31f, .43f, .29f), -1000);
            var tiles = new GameObject("MeadowTiles").transform; tiles.SetParent(world);
            for (var y = -18f; y <= 18; y += 3f)
                for (var x = -22f; x <= 22; x += 3f)
                {
                    var tile = ModelArt.Place("grass_tile", tiles, new Vector2(x, y), new Vector2(3.03f, 3.03f), -990);
                    var renderer = tile.GetComponent<SpriteRenderer>();
                    var variation = Mathf.PerlinNoise(x * .14f + 12, y * .14f + 5);
                    renderer.color = Color.Lerp(new Color(.98f, .99f, .98f), Color.white, variation);
                }
            Boundary("WestTownBoundary", new Vector2(-23, 0), new Vector2(1, 39));
            Boundary("EastTownBoundary", new Vector2(23, 0), new Vector2(1, 39));
            Boundary("NorthTownBoundary", new Vector2(0, 19), new Vector2(47, 1));
            Boundary("SouthTownBoundary", new Vector2(0, -19), new Vector2(47, 1));
        }

        private void Boundary(string name, Vector2 position, Vector2 size)
        {
            var node = new GameObject(name, typeof(BoxCollider2D)); node.transform.SetParent(world); node.transform.position = position;
            node.GetComponent<BoxCollider2D>().size = size;
        }

        private void CreateStreets()
        {
            var roads = new GameObject("StoneStreets").transform; roads.SetParent(world);
            // The fountain has a generous walkable ring, with clear avenues to every shop.
            for (var y = -16.8f; y <= 14; y += 1.4f)
                for (var x = -19.6f; x <= 19.6f; x += 1.4f)
                {
                    var central = Mathf.Abs(x) < 2.2f;
                    var square = Mathf.Abs(x) < 5.3f && Mathf.Abs(y) < 4.4f;
                    var north = Mathf.Abs(y - 4.3f) < 1.75f && Mathf.Abs(x) < 17;
                    var south = Mathf.Abs(y + 7.2f) < 1.5f && Mathf.Abs(x) < 17;
                    var ring = Mathf.Abs(Mathf.Abs(x) - 16.5f) < 1.6f && y > -7.5f && y < 5;
                    var hutLane = Mathf.Abs(y + 15.4f) < .9f && x > -11.8f && x < 1.5f;
                    if (central || square || north || south || ring || hutLane)
                        ModelArt.Place("cobble_tile", roads, new Vector2(x, y), new Vector2(1.43f, 1.43f), -900);
                }
            var fountainPosition = new Vector2(0, .85f);
            Prop("fountain", fountainPosition, new Vector2(3.1f, 3.5f), true, new Vector2(2.1f, 1.2f), -.65f);
            atmosphere.AddFountain(fountainPosition + new Vector2(0, .48f));
            Prop("bench", new Vector2(-4, -.2f), new Vector2(1.8f, 1.15f), true, new Vector2(1.55f, .42f), -.28f);
            Prop("bench", new Vector2(4, -.2f), new Vector2(1.8f, 1.15f), true, new Vector2(1.55f, .42f), -.28f);
            Prop("signpost", new Vector2(2.7f, -3.1f), new Vector2(1.1f, 1.9f), false);
            SmallSign("北街 · 商店", new Vector2(2.7f, -2.6f));
            Prop("signpost", new Vector2(-2.8f, -8.7f), new Vector2(1.1f, 1.9f), false);
            SmallSign("远征之门 ↓", new Vector2(-2.8f, -8.1f));
            for (var x = -8; x <= 8; x += 16)
            {
                Prop("cart", new Vector2(x, 3.9f), new Vector2(2.2f, 1.9f), true, new Vector2(1.55f, .70f), -.42f);
                Prop("barrel", new Vector2(x + 1.35f, 4.05f), new Vector2(.86f, 1.10f), true, new Vector2(.50f, .42f), -.23f);
            }
            foreach (var position in new[] { new Vector2(-5.9f, 3.3f), new Vector2(5.9f, 3.3f), new Vector2(-5.9f, -3.7f), new Vector2(5.9f, -3.7f), new Vector2(-2.5f, -11.3f), new Vector2(2.5f, -11.3f) })
            {
                Prop("lantern", position, new Vector2(.85f, 2.1f), false);
                atmosphere.AddLantern(position + Vector2.up * .5f);
            }
        }

        private void CreateBuildings()
        {
            Building("铁匠铺", "smith", "smith", "武器 · 锻造 · 修复", new Vector2(-13.3f, 8.4f), new Vector2(7.3f, 6.3f));
            Building("防具店", "armor_shop", "armor", "防具 · 套装 · 维护", new Vector2(-4.7f, 9.5f), new Vector2(6.5f, 6.1f));
            Building("药剂店", "alchemy", "potions", "药剂 · 补给", new Vector2(4.9f, 9.5f), new Vector2(6.6f, 6.1f));
            Building("杂货铺", "general_shop", "general", "背包 · 胸挂 · 钥匙", new Vector2(13.4f, 8.4f), new Vector2(6.7f, 6.0f));
            Building("卷轴屋", "scroll_shop", "scrolls", "卷轴 · 奥术用品", new Vector2(-13.2f, -3.8f), new Vector2(6.6f, 5.9f));
            Building("冒险者公会", "guild", "guild", "委托 · 晋升 · 故事", new Vector2(13.2f, -3.8f), new Vector2(7.8f, 6.6f));
            Building("冒险者小屋", "hut", "hut", "技能 · 仓储 · 收藏", new Vector2(-9.8f, -12.0f), new Vector2(6.5f, 5.9f));
            Building("远征之门", "gate", "expedition", "准备装备，选择远征难度", new Vector2(0, -12.4f), new Vector2(7.7f, 6.3f));
        }

        private void Building(string displayName, string key, string route, string hint, Vector2 position, Vector2 size)
        {
            // The baked shop doorstep is at image y=.10; the stone gate is shorter.
            var footY = position.y - size.y * (key == "gate" ? .30f : .39f);
            var root = ModelArt.Place(key, world, position, size, TownAtmosphere.Order(footY));
            root.name = displayName + "_Model";
            var collider = root.AddComponent<BoxCollider2D>();
            // World size is converted because Place scales the render object.
            collider.size = new Vector2(size.x * .73f / root.transform.lossyScale.x, 1.85f / root.transform.lossyScale.y);
            collider.offset = new Vector2(0, (footY + .88f - position.y) / root.transform.lossyScale.y);
            // Town approaches its southern exit from the inside (the north side).
            var doorway = new Vector2(position.x, key == "gate" ? position.y + .80f : footY - .98f);
            for (var y = doorway.y - 1.0f; y < footY + .1f; y += .8f)
                ModelArt.Place("cobble_tile", world, new Vector2(position.x, y), new Vector2(2.0f, 1.25f), -870);
            var node = new GameObject(displayName + "_Interact", typeof(Interactable), typeof(BoxCollider2D));
            node.transform.SetParent(world); node.transform.position = doorway;
            var interaction = node.GetComponent<Interactable>();
            interaction.DisplayName = displayName; interaction.Route = route; interaction.Hint = hint;
            var trigger = node.GetComponent<BoxCollider2D>(); trigger.isTrigger = true; trigger.size = new Vector2(3.5f, 2.3f);
            interaction.Visual = root.GetComponent<SpriteRenderer>();
            interaction.Highlight = TownAtmosphere.MakeDoorHighlight(world, new Vector2(position.x, key == "gate" ? doorway.y + .85f : footY - .35f), new Vector2(2.1f, .55f));
            var labelPosition = key == "gate" ? new Vector2(position.x, position.y + size.y * .43f)
                : new Vector2(position.x - size.x * .22f, footY - .73f);
            interaction.LabelPlate = WorldArt.Shape(displayName + "_ApproachCaption", world, labelPosition,
                new Vector2(displayName.Length * .075f * 3.2f + .28f, .43f), Color.clear, 19998).GetComponent<SpriteRenderer>();
            interaction.Label = MakeText(displayName + "_ApproachLabel", displayName, labelPosition, .075f, new Color(1, .93f, .74f));
            interaction.Label.text = "";
            interactables.Add(interaction);
            if (key == "smith") atmosphere.AddSmoke(position + new Vector2(-size.x * .23f, size.y * .31f));
            if (key == "alchemy") atmosphere.AddSmoke(position + new Vector2(size.x * .27f, size.y * .32f), new Color(.72f, .82f, .69f, .3f));
            if (key == "gate") atmosphere.AddPortal(new Vector2(position.x, footY + 1.2f));
            var merchant = key == "smith" ? "npc_smith" : key == "alchemy" ? "npc_alchemist" : key == "guild" ? "npc_guild" : key == "general_shop" ? "npc_trader" : null;
            if (merchant != null) atmosphere.AddMerchant(merchant, new Vector2(position.x + 1.62f, footY + .24f), footY - .44f);
            var side = position.x >= 0 ? 1f : -1f;
            Prop(key == "alchemy" || key == "hut" ? "bush" : "barrel", new Vector2(position.x + side * size.x * .41f, footY + .03f), new Vector2(1.1f, 1.1f), false);
            Prop("crate", new Vector2(position.x - side * size.x * .42f, footY - .12f), new Vector2(1.1f, .96f), false);
            Prop("lantern", new Vector2(position.x + size.x * .39f, footY - .25f), new Vector2(.7f, 1.8f), false);
            atmosphere.AddLantern(new Vector2(position.x + size.x * .39f, footY + .14f));
        }

        private void CreateGardens()
        {
            var trees = new[] { new Vector2(-20.0f, 13.6f), new Vector2(-18.8f, 9.0f), new Vector2(-20.5f, 3.5f), new Vector2(-20.2f, -3.4f),
                new Vector2(-19.5f, -10.0f), new Vector2(-18.0f, -15.5f), new Vector2(-12.9f, 15.8f), new Vector2(-5.8f, 16),
                new Vector2(4.5f, 16), new Vector2(12.5f, 15.8f), new Vector2(19.8f, 13.6f), new Vector2(20.0f, 7),
                new Vector2(20.0f, .2f), new Vector2(20.6f, -6.8f), new Vector2(18.9f, -13.4f), new Vector2(13.9f, -15.5f),
                new Vector2(7.7f, -12.7f), new Vector2(8.8f, -16.2f) };
            for (var i = 0; i < trees.Length; i++)
            {
                var size = new Vector2(3.5f, 4.8f) * (i % 3 == 0 ? 1.13f : .97f);
                var tree = Prop("tree", trees[i], size, true, new Vector2(.6f, .6f), -size.y * .40f + .25f);
                atmosphere.AddBreeze(tree.transform, .7f + i * .31f);
                Prop("bush", trees[i] + new Vector2(i % 2 == 0 ? 1.1f : -1.15f, -1.4f), new Vector2(1.4f, 1.15f), false);
            }
            foreach (var p in new[] { new Vector2(-7.2f, .2f), new Vector2(7.2f, .2f), new Vector2(-7.1f, -3.3f), new Vector2(7.1f, -3.3f), new Vector2(9.0f, -9.6f) })
            {
                Prop("bush", p, new Vector2(1.6f, 1.3f), false);
                Prop("rocks", p + new Vector2(.7f, -.6f), new Vector2(1.15f, .75f), false);
                atmosphere.AddPetals(p);
            }
            for (var side = -1; side <= 1; side += 2)
            {
                for (var y = -14f; y < 15; y += 3.1f) Prop("fence", new Vector2(side * 22f, y), new Vector2(2.9f, 1.45f), false);
                for (var x = -18f; x < 19; x += 3.1f) if (Mathf.Abs(x) > 3.5f) Prop("fence", new Vector2(x, side * 17.3f), new Vector2(2.9f, 1.45f), false);
            }
        }

        private GameObject Prop(string key, Vector2 position, Vector2 size, bool collision, Vector2 colliderSize = default(Vector2), float footOffset = 0)
        {
            var node = ModelArt.Place(key, world, position, size, TownAtmosphere.Order(position.y + (footOffset == 0 ? -size.y * .25f : footOffset)));
            if (collision)
            {
                var collider = node.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(colliderSize.x / node.transform.lossyScale.x, colliderSize.y / node.transform.lossyScale.y);
                collider.offset = new Vector2(0, footOffset / node.transform.lossyScale.y);
            }
            return node;
        }

        private void CreatePlayer()
        {
            var node = new GameObject("Player", typeof(SpriteRenderer));
            node.tag = "Player"; node.transform.position = new Vector3(0, -3.8f, 0); node.transform.localScale = Vector3.one * 1.05f;
            WorldArt.AddKnight(node); node.AddComponent<TopDownPlayerController>();
            var collider = node.GetComponent<BoxCollider2D>(); collider.size = new Vector2(.50f, .32f); collider.offset = new Vector2(0, -.63f);
            player = node.transform;
            TownAtmosphere.MakePlayerShadow(node.transform, new Vector2(0, -.63f));
            Camera.main.GetComponent<CameraFollow2D>().SetTarget(player);
        }

        private void CreateResidents()
        {
            // Quiet foot traffic gives the hub life; paths stay clear of shop doorways.
            TownResident.Create(world, "NorthStreetCourier", new[] { new Vector2(-10.3f, 3.1f), new Vector2(10.3f, 3.1f), new Vector2(10.3f, 2.2f), new Vector2(-10.3f, 2.2f) }, new Color(.89f, .78f, .63f), 1.4f);
            TownResident.Create(world, "ReturningAdventurer", new[] { new Vector2(7.5f, -8.3f), new Vector2(-6.2f, -8.3f), new Vector2(-6.2f, -9.4f), new Vector2(7.5f, -9.4f) }, new Color(.69f, .78f, .71f), 1.2f);
            TownResident.Create(world, "WestGateGuard", new[] { new Vector2(-3.8f, -11.3f) }, new Color(.82f, .84f, .87f), 0);
            TownResident.Create(world, "EastGateGuard", new[] { new Vector2(3.8f, -11.3f) }, new Color(.82f, .84f, .87f), 0);
        }

        private void SmallSign(string text, Vector2 position) { MakeText("StreetSign", text, position, .071f, new Color(.22f, .15f, .08f)); }
        private TextMesh MakeText(string name, string text, Vector2 position, float size, Color color)
        {
            var node = new GameObject(name, typeof(TextMesh)); node.transform.SetParent(world); node.transform.position = position;
            var mesh = node.GetComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 44; mesh.characterSize = size; mesh.color = color;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.font = GameFont.World;
            var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mesh.font.material; renderer.sortingOrder = 20000;
            return mesh;
        }
        private void CreatePrompt() { interactPrompt = MakeText("InteractPrompt", "", Vector2.zero, .08f, new Color(1, .95f, .79f)); }

        private void Update()
        {
            if (player == null) return;
            var previous = nearest; nearest = null; var best = float.MaxValue;
            foreach (var interaction in interactables)
            {
                if (!interaction.IsPlayerInside || GamePanel.Blocked) continue;
                var distance = ((Vector2)interaction.transform.position - (Vector2)player.position).sqrMagnitude;
                if (distance < best) { best = distance; nearest = interaction; }
            }
            if (previous != nearest && nearest != null) AudioDirector.Cue(10);
            foreach (var interaction in interactables) interaction.SetHighlighted(interaction == nearest);
            var camera = Camera.main; var anchor = camera.ViewportToWorldPoint(new Vector3(.5f, .12f, 10)); anchor.z = 0;
            interactPrompt.transform.position = anchor;
            interactPrompt.text = nearest == null ? "" : "[" + GameInput.Key("interact") + "] " + nearest.DisplayName + "    ·    " + nearest.Hint;
            if (!GamePanel.Blocked && nearest != null && GameInput.Down("interact")) GamePanel.OpenPage(nearest.Route);
        }
    }

    /// <summary>Only the nearest service shows a label and doorway glow.</summary>
    public sealed class Interactable : MonoBehaviour
    {
        public string DisplayName, Route, Hint;
        public SpriteRenderer Visual, Highlight, LabelPlate;
        public TextMesh Label;
        public bool IsPlayerInside { get; private set; }
        private float emphasis;
        public void SetHighlighted(bool selected)
        {
            emphasis = Mathf.MoveTowards(emphasis, selected ? 1 : 0, Time.unscaledDeltaTime * 5);
            if (Visual != null) Visual.color = Color.Lerp(Color.white, new Color(1.07f, 1.04f, .95f), emphasis);
            if (Highlight != null) Highlight.color = new Color(1, .79f, .33f, emphasis * (.40f + .07f * Mathf.Sin(Time.unscaledTime * 3)));
            if (LabelPlate != null) LabelPlate.color = new Color(.055f, .075f, .068f, emphasis * .83f);
            if (Label != null) { Label.text = emphasis > .01f ? DisplayName : ""; Label.color = new Color(1, .94f, .78f, emphasis); }
        }
        private void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player")) IsPlayerInside = true; }
        private void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player")) IsPlayerInside = false; }
    }
}
