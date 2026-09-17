using System;
using System.Collections.Generic;
using KnightChronicles.Runtime.Core;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    public sealed class DungeonController : MonoBehaviour
    {
        private sealed class EnemyVisual { public EnemyState Data; public GameObject Node; public EnemyPresentation Presentation; public EnemyHealthBar Bar; public float Cooldown, Telegraph, PoisonTime, PoisonTick; public int PoisonStacks; }
        private sealed class Bolt { public Vector2 Position, Direction; public float Damage, Life; public bool Hostile, Blast; public GameObject Node; }
        private readonly List<EnemyVisual> enemies = new List<EnemyVisual>();
        private readonly List<Bolt> bolts = new List<Bolt>();
        private readonly Dictionary<GroundItem,GameObject> dropNodes = new Dictionary<GroundItem,GameObject>();
        private readonly Dictionary<SearchPoint,GameObject> searchNodes = new Dictionary<SearchPoint,GameObject>();
        private readonly Dictionary<string,Sprite> lootSprites = new Dictionary<string,Sprite>();
        private Transform world;
        private GameObject player;
        private SpriteAnimator animator;
        private Camera cameraView;
        private Vector2 facing = Vector2.down, move, dodgeDirection;
        private float dodgeTime, dodgeCooldown, immunity, attackCooldown, charge, potionCooldown, searchTime;
        private float attackBuff, speedBuff, defenseBuff, shield, strengthBuff;
        private float skill1Cooldown, skill2Cooldown, guardTime, reflectTime, executionTime, poisonBuff, streamTime, streamTick;
        private float sinceDamage = 100, rollSpeedTime, undyingCooldown, bloodTime;
        private int bloodStacks;
        private float cameraShake, arrivalTime, ghostCooldown;
        public float ArrivalTime { get { return arrivalTime; } }
        public float QuickCooldown { get { return Mathf.Max(0,potionCooldown); } }
        public float Skill1Cooldown { get { return skill1Cooldown; } }
        public float Skill2Cooldown { get { return skill2Cooldown; } }
        private SearchPoint searchTarget;
        public string Prompt { get; private set; }
        public float SearchProgress { get { return searchTime / SearchDuration; } }
        public float DodgeCooldown { get { return dodgeCooldown; } }
        public float Charge { get { return charge; } }
        private GameRules Rules { get { return GameSession.Rules; } }
        private RunData Run { get { return GameSession.State.ActiveRun; } }
        public FloorData Floor { get { return Run.Floors[Run.Floor - 1]; } }
        private float SearchDuration { get { return Rules.State.Skill("explore_search") > 0 ? 1.6f : 2f; } }
        public Vector2 Position { get { return player == null ? Vector2.zero : (Vector2)player.transform.position; } }
        private void Awake()
        {
            var cameraNode = new GameObject("DungeonCamera", typeof(Camera), typeof(AudioListener)); cameraView = cameraNode.GetComponent<Camera>();
            cameraView.orthographic = true; cameraView.orthographicSize = 7 * GameSession.State.Zoom;
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(0.025f, 0.035f, 0.06f); cameraView.tag = "MainCamera";
            cameraView.transform.position = new Vector3(Run.X, Run.Y, -10);
            player = new GameObject("Player", typeof(SpriteRenderer)); player.tag = "Player";
            player.GetComponent<SpriteRenderer>().sortingOrder = 20; animator = WorldArt.AddKnight(player);
            player.transform.localScale = Vector3.one * 0.8f;
            GroundShadow.Attach(player.transform,new Vector2(1.25f,.55f));
            BuildFloor(); gameObject.AddComponent<GamePanel>().Dungeon = this;
        }
        public void BuildFloor()
        {
            AudioDirector.FloorChanged();
            if (world != null) Destroy(world.gameObject);
            foreach (var b in bolts) Destroy(b.Node); bolts.Clear(); enemies.Clear(); dropNodes.Clear();searchNodes.Clear();
            world = new GameObject("DungeonFloor").transform;
            var f = Floor;
            DungeonScenery.Render(f,world,Run.Seed);
            foreach (var s in f.Searches)
            {
                if (!f.Rooms[s.Room].Opened) continue;
                var node=DungeonScenery.Prop(new[]{"crate","bookshelf","bones","chest","urn","supplies"}[s.Type],world,new Vector2(s.X,s.Y),new Vector2(1.25f,1.3f));
                node.name="SearchContainer";if(s.Searched)node.GetComponent<SpriteRenderer>().color=new Color(.55f,.55f,.55f,.7f);searchNodes[s]=node;
            }
            MarkStair(0, "↑ 返回上一层", new Vector2(-3, -3), new Color(0.4f, 0.6f, 0.8f));
            if (Run.Floor < 5) MarkStair(f.DownRoom, "↓ 前往下一层", new Vector2(3, 3), new Color(0.4f, 0.6f, 0.8f));
            if (f.ExtractRoom >= 0) MarkStair(f.ExtractRoom, "✦ 撤离点", Vector2.zero, new Color(0.3f, 1, 0.7f));
            foreach (var e in f.Enemies)
            {
                if (e.Dead) { DungeonScenery.Prop("bones",world,new Vector2(e.X,e.Y),new Vector2(1.1f,.55f));continue; }
                // Generated actor positions predate collidable art. Keep saved actors out of containers.
                var safe = NearestClearPosition(new Vector2(e.X,e.Y)); e.X = safe.x; e.Y = safe.y;
                var node=new GameObject("Enemy",typeof(SpriteRenderer));node.transform.SetParent(world);node.transform.position=new Vector2(e.X,e.Y);
                var presentation=node.AddComponent<EnemyPresentation>();presentation.Configure(e.Type,Run.Difficulty);
                GroundShadow.Attach(node.transform,new Vector2(e.Type==3?1.8f:1,.45f));
                var bar=new GameObject("EnemyHealth",typeof(EnemyHealthBar)).GetComponent<EnemyHealthBar>();bar.Initialize(node.transform,e);
                enemies.Add(new EnemyVisual { Data = e, Node = node,Presentation=presentation,Bar=bar });
            }
            foreach (var d in f.Drops) DrawDrop(d);
            var entry = NearestClearPosition(new Vector2(Run.X,Run.Y)); Run.X = entry.x; Run.Y = entry.y;
            player.transform.position = entry; searchTarget = null; searchTime = 0;
            cameraView.transform.position = new Vector3(Run.X, Run.Y, -10);
            arrivalTime=3.2f;
        }
        private Vector2 Marker(int room, Vector2 offset) { var r = Floor.Rooms[room]; return new Vector2(r.X * 16, r.Y * 16) + offset; }
        private void MarkStair(int room, string label, Vector2 offset, Color color)
        {
            if (Run.Floor == 1 && room == 0 && offset.x < 0) label = "入口 · 请在 L2/L4 撤离";
            var pos = Marker(room, offset);var portal=offset==Vector2.zero;
            var node=DungeonScenery.Prop(portal?"portal":offset.x<0?"stair_up":"stair_down",world,pos,new Vector2(portal?2.6f:2.4f,portal?3.2f:2.1f));
            node.name=portal?"ExtractionPortal":"Stairway";
            if(portal)DungeonScenery.Light(world,pos+Vector2.up*.4f,4,new Color(.25f,1,.65f,.32f));
            WorldArt.Label(world,label,pos+Vector2.up*(portal?2.4f:1.65f),color,.08f);
            var text=world.GetChild(world.childCount-1).GetComponent<MeshRenderer>();if(text!=null)text.sortingOrder=9000;
        }
        private void DrawDrop(GroundItem d)
        {
            var definition=Rules.Catalog.Get(d.Item.Id);Sprite icon;
            if(!lootSprites.TryGetValue(definition.Id,out icon)) { var texture=ItemIconAtlas.Get(definition);icon=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,texture.width);lootSprites[definition.Id]=icon; }
            var node=WorldArt.Shape("Loot",world,new Vector2(d.X,d.Y),Vector2.one*.68f,Color.white,9001);node.GetComponent<SpriteRenderer>().sprite=icon;
            var glow=DungeonScenery.Light(node.transform,new Vector2(d.X,d.Y),1.8f,WorldArt.Tier(definition.Tier)*new Color(1,1,1,.28f));glow.GetComponent<SpriteRenderer>().sortingOrder=8999;
            dropNodes[d]=node;
        }
        private void Update()
        {
            if (Run == null || GamePanel.Blocked || Time.timeScale == 0) return;
            var dt = Time.deltaTime;
            arrivalTime=Mathf.Max(0,arrivalTime-dt);cameraShake=Mathf.Max(0,cameraShake-dt*2);ghostCooldown-=dt;
            dodgeCooldown = Mathf.Max(0, dodgeCooldown - dt); immunity -= dt; attackCooldown -= dt; potionCooldown -= dt;
            attackBuff -= dt; speedBuff -= dt; defenseBuff -= dt; strengthBuff -= dt;
            var input = GameInput.Move; move = input.normalized;
            var aim = (Vector2)cameraView.ScreenToWorldPoint(Input.mousePosition) - Position; if (aim.sqrMagnitude > 0.01f) facing = aim.normalized;
            if (GameInput.Down("dodge") && dodgeCooldown <= 0 && Rules.Weight <= Rules.CarryLimit)
            { dodgeTime = 0.3f + (Rules.State.Skill("survival_speed_large") > 0 ? .1f : 0); dodgeCooldown = Rules.RollCooldown;
              dodgeDirection = move.sqrMagnitude > 0 ? move : facing; immunity = dodgeTime; searchTime = 0;
              if (Rules.State.Skill("survival_speed_medium") > 0) rollSpeedTime = 2; }
            var dodging = dodgeTime > 0; if (dodging) dodgeTime -= dt;
            var displacement = (dodging ? dodgeDirection * 13 : move * Rules.MoveSpeed * (speedBuff > 0 ? 1.2f : 1)
                * (rollSpeedTime > 0 ? Rules.State.Skill("survival_speed_large") > 0 ? 1.25f : 1.15f : 1)) * dt;
            MovePlayer(displacement);
            animator.Row = TopDownPlayerController.DirectionRow(dodging ? dodgeDirection : move.sqrMagnitude > 0 ? move : facing);
            animator.Play(dodging ? "dodge" : move.sqrMagnitude > 0 ? "walk" : "idle");
            player.GetComponent<SpriteRenderer>().sortingOrder=DungeonScenery.FootOrder(Position.y);
            player.GetComponent<SpriteRenderer>().color=immunity>0&&!dodging?new Color(1,.68f,.64f,Mathf.Sin(Time.time*40)>0?.6f:1):Color.white;
            if(dodging&&ghostCooldown<=0){CombatFeedback.Ghost(player.GetComponent<SpriteRenderer>());ghostCooldown=.055f;}
            Run.X = Position.x; Run.Y = Position.y;
            var roomIndex = Floor.Rooms.FindIndex(r => r.Opened && Mathf.Abs(Position.x - r.X * 16) < 6 && Mathf.Abs(Position.y - r.Y * 16) < 6);
            if (roomIndex >= 0) Floor.Rooms[roomIndex].Visited = true;
            UpdatePassives(dt);
            HandleAttack(dt, dodging); HandleInteraction(dt); UpdateEnemies(dt); UpdateBolts(dt);
            if (GameInput.Down("skill1")) UseSkill(1); if (GameInput.Down("skill2")) UseSkill(2);
            for (var i = 0; i < 3; i++) if (GameInput.Down("quick" + (i + 1))) UseQuick(i);
            if (Run == null) return;
            cameraView.transform.position = Vector3.Lerp(cameraView.transform.position, new Vector3(Position.x, Position.y, -10), 1 - Mathf.Exp(-dt * 12));
            if(cameraShake>0)cameraView.transform.position+=new Vector3(Mathf.Sin(Time.time*97),Mathf.Cos(Time.time*83),0)*cameraShake*.11f;
            if (Run.Hp <= 0) End(false);
        }
        private bool CanWalk(float x,float y) {return DungeonGenerator.Walkable(Floor,x,y)&&DungeonScenery.ClearPoint(x,y);}
        private Vector2 NearestClearPosition(Vector2 origin)
        {
            if (CanWalk(origin.x,origin.y)) return origin;
            for (var ring=1;ring<=32;ring++)
                for (var step=0;step<32;step++)
                {
                    var angle=step*Mathf.PI/16;
                    var p=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*ring*.25f;
                    if(CanWalk(p.x,p.y)) return p;
                }
            foreach(var room in Floor.Rooms)
                if(room.Opened)
                    for(var x=-4;x<=4;x++)for(var y=-4;y<=4;y++)
                    {var p=new Vector2(room.X*16+x,room.Y*16+y);if(CanWalk(p.x,p.y))return p;}
            throw new InvalidOperationException("Dungeon has no clear actor spawn.");
        }
        private void MovePlayer(Vector2 delta)
        {
            // Substeps prevent rolling/tunnelling across walls during low frame rates.
            var steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.15f)); delta /= steps; var p = Position;
            for (var i = 0; i < steps; i++) { if (CanWalk( p.x + delta.x, p.y)) p.x += delta.x;
                if (CanWalk( p.x, p.y + delta.y)) p.y += delta.y; }
            player.transform.position = p;
        }
        private void UpdatePassives(float dt)
        {
            sinceDamage += dt; rollSpeedTime -= dt; undyingCooldown -= dt; bloodTime -= dt;
            if (bloodTime <= 0) bloodStacks = 0;
            skill1Cooldown -= dt; skill2Cooldown -= dt; guardTime -= dt; reflectTime -= dt; executionTime -= dt; poisonBuff -= dt;
            var state = Rules.State;
            if (state.Skill("survival_defense_medium") > 0 && sinceDamage >= (state.Skill("survival_defense_large") > 0 ? 3 : 5))
                Run.Hp = Mathf.Min(Rules.MaximumHp, Run.Hp + Rules.MaximumHp * dt * (state.Skill("survival_defense_large") > 0 ? .02f : .01f));
            if (state.Skill("survival_mp_medium") > 0 && sinceDamage >= 5)
                Run.Mp = Mathf.Min(Rules.MaximumMp, Run.Mp + Rules.MaximumMp * dt * (state.Skill("survival_mp_large") > 0 ? .04f : .02f));
            if (streamTime > 0)
            { streamTime -= dt; streamTick -= dt;
              if (streamTick <= 0) { Shoot(Position, facing, Rules.AttackPower * .6f * (state.Skill("staff_enhance1") > 0 ? 1.3f : 1), false); streamTick = .12f; } }
            foreach (var e in enemies) if (!e.Data.Dead && e.PoisonTime > 0)
            { e.PoisonTime -= dt; e.PoisonTick -= dt; if (e.PoisonTick <= 0) { HurtEnemy(e, 8 * e.PoisonStacks, false); e.PoisonTick = 1; } }
            Run.Hp = Mathf.Min(Run.Hp, Rules.MaximumHp); Run.Mp = Mathf.Min(Run.Mp, Rules.MaximumMp);
        }
        public void UseSkill(int number)
        {
            var w = Rules.Weapon;
            if (w == null || Rules.State.Skill(w.Branch + "_skill" + number) == 0) { GamePanel.Toast("请在小屋解锁对应武器技能"); return; }
            var index = Array.IndexOf(new[] { "blade", "sword", "spear", "dagger", "staff" }, w.Branch);
            var cost = number == 1 ? new[] { 20, 15, 20, 15, 30 }[index] : new[] { 25, 25, 25, 20, 40 }[index];
            var cooldown = number == 1 ? new[] { 12, 15, 10, 8, 18 }[index] : new[] { 20, 18, 14, 16, 20 }[index];
            if (Run.Mp < cost || (number == 1 ? skill1Cooldown : skill2Cooldown) > 0) { GamePanel.Toast("蓝量不足或技能冷却中"); return; }
            Run.Mp -= cost; var enhanced = Rules.State.Skill(w.Branch + "_enhance" + number) > 0;
            animator.PlayOneShot(w.Branch=="staff"?"cast":"attack",.4f);
            if (number == 1) skill1Cooldown = cooldown * (enhanced ? .85f : 1); else skill2Cooldown = cooldown * (enhanced ? .85f : 1);
            var power = Rules.AttackPower * (enhanced ? 1.3f : 1);
            if (w.Branch == "blade") { if (number == 1) { Strike(3.5f, power * 1.8f, -1); Pulse(Position, Color.yellow, 6); } else executionTime = 6; }
            if (w.Branch == "sword") { if (number == 1) guardTime = 2; else reflectTime = 3; Pulse(Position, Color.cyan, 2); }
            if (w.Branch == "spear")
            {
                if (number == 1)
                { var multiplier = 1f; foreach (var e in enemies) { var delta = (Vector2)e.Node.transform.position - Position;
                    if (!e.Data.Dead && delta.magnitude <= 6 && Vector2.Dot(delta.normalized, facing) >= .95f)
                    { HurtEnemy(e, power * 2.2f * multiplier); if (Rules.State.Skill("spear_ultimate") > 0) multiplier += .15f; } } }
                else { Strike(4, power * 2.5f, 0); foreach (var e in enemies) { var delta = (Vector2)e.Node.transform.position - Position;
                    if (!e.Data.Dead && delta.magnitude < 4 && Vector2.Dot(delta.normalized, facing) >= 0)
                    { var next = (Vector2)e.Node.transform.position + delta.normalized * 1.5f; if (CanWalk( next.x, next.y)) { e.Node.transform.position = next; e.Data.X = next.x; e.Data.Y = next.y; } } } }
                Pulse(Position + facing * 2, Color.yellow, 3);
            }
            if (w.Branch == "dagger")
            {
                if (number == 1)
                { EnemyVisual nearest = null; var distance = 4f; foreach (var e in enemies) { var delta = (Vector2)e.Node.transform.position - Position;
                    if (!e.Data.Dead && delta.magnitude <= distance && Vector2.Dot(delta.normalized, facing) > .6f) { nearest = e; distance = delta.magnitude; } }
                    if (nearest != null) { var next = (Vector2)nearest.Node.transform.position + facing * .7f;
                        if (CanWalk( next.x, next.y)) { player.transform.position = next; Run.X = next.x; Run.Y = next.y; }
                        HurtEnemy(nearest, power * 2 * 1.5f); immunity = .3f; } }
                else poisonBuff = 8;
            }
            if (w.Branch == "staff")
            {
                if (number == 1) { streamTime = 2; streamTick = 0; }
                else { var target = (Vector2)cameraView.ScreenToWorldPoint(Input.mousePosition); var delta = target - Position;
                    if (delta.magnitude > 8) target = Position + delta.normalized * 8;
                    foreach (var e in enemies) if (!e.Data.Dead && Vector2.Distance(target, e.Node.transform.position) <= 3) HurtEnemy(e, power * 3);
                    Pulse(target, Color.magenta, 6); }
            }
        }
        private void HandleAttack(float dt, bool dodging)
        {
            var w = Rules.Weapon; if (dodging) return;
            if (w != null && w.Branch == "staff")
            {
                if (GameInput.Held("fire")) charge = Mathf.Min(2, charge + dt * (1 + Rules.State.Skill("staff_mastery") * 0.06f));
                if (GameInput.Up("fire") && attackCooldown <= 0)
                { var powers = w.Effect.Split(','); var basePower = charge < 0.5f ? int.Parse(powers[0]) : charge < 1.5f ? w.Power : int.Parse(powers[1]);
                    animator.PlayOneShot("cast",.4f);
                  Shoot(Position, facing, basePower * Rules.AttackPower / w.Power, false, charge >= 1.5f && Rules.State.Skill("staff_ultimate") > 0); Rules.WearWeapon(); attackCooldown = Rules.AttackInterval; charge = 0; }
            }
            else if (GameInput.Held("fire") && attackCooldown <= 0)
            {
                var range = w == null ? 1.4f : w.Range; var power = Rules.AttackPower * (attackBuff > 0 ? 1.2f : 1) * (strengthBuff > 0 ? 1.5f : 1);
                attackCooldown = Rules.AttackInterval;
                animator.Row=TopDownPlayerController.DirectionRow(facing);animator.PlayOneShot("attack",Mathf.Min(.4f,attackCooldown));
                CombatFeedback.Slash(Position,facing,range);
                if (executionTime > 0) { power *= 3.5f * 2 * (Rules.State.Skill("blade_enhance2") > 0 ? 1.3f : 1); executionTime = 0; }
                Strike(range, power, w != null && (w.Branch == "spear" || w.Branch == "dagger") ? 0.88f : 0.5f);
                Rules.WearWeapon(); Pulse(Position + facing * range * 0.55f, WorldArt.Tier(w == null ? 0 : w.Tier), range * 0.7f);
            }
        }
        public void Strike(float range, float power, float dot)
        {
            foreach (var e in enemies) { if (e.Data.Dead) continue; var delta = (Vector2)e.Node.transform.position - Position;
                if (delta.magnitude <= range && (delta.sqrMagnitude < 0.01f || Vector2.Dot(delta.normalized, facing) >= dot)) HurtEnemy(e, power); }
        }
        private void HurtEnemy(EnemyVisual e, float damage, bool weaponHit = true)
        {
            if (e.Data.Dead) return;
            AudioDirector.Cue(weaponHit ? 11 : 12);
            if (weaponHit)
            {
                var strength = Rules.EnchantStrength; var w = Rules.Weapon;
                if (Rules.HasEnchant("slaughter")) damage *= 1 + .1f * strength;
                if (Rules.HasEnchant("fire")) damage += 5 * strength;
                var critical = (Rules.HasEnchant("critical") ? .06f * strength : 0) + (w != null && w.Branch == "dagger" ? Rules.State.Skill("dagger_mastery") * .03f : 0);
                if (UnityEngine.Random.value < critical) damage *= 2;
                if (w != null && w.Branch == "blade" && Rules.State.Skill("blade_ultimate") > 0) { damage *= 1 + bloodStacks * .02f; bloodStacks = Mathf.Min(10, bloodStacks + 1); bloodTime = 4; }
                if (w != null && w.Branch == "dagger" && Rules.State.Skill("dagger_ultimate") > 0
                    && e.Data.Hp < (e.Data.Type == 3 ? 180 : 24 + e.Data.Type * 12) * DungeonGenerator.DifficultyMultiplier(Run.Difficulty) * .2f) damage *= 1.4f;
                if (Rules.HasEnchant("drain")) Run.Hp = Mathf.Min(Rules.MaximumHp, Run.Hp + Mathf.Min(e.Data.Hp, damage) * .03f * strength);
                if (poisonBuff > 0) { e.PoisonTime = 8; e.PoisonStacks = Mathf.Min(3, e.PoisonStacks + 1); }
            }
            var armor = e.Data.Type * .04f; if (Rules.HasEnchant("pierce")) armor *= 1 - .1f * Rules.EnchantStrength;
            e.Data.Hp -= damage * (1 - armor);
            e.Presentation.Hit();CombatFeedback.Number(e.Node.transform.position,damage*(1-armor));
            e.Bar.Reveal();
            CombatFeedback.Sparks(e.Node.transform.position,weaponHit?new Color(1,.82f,.44f):new Color(.55f,.9f,1));
            cameraShake=Mathf.Max(cameraShake,.32f);
            if (e.Data.Hp > 0) return;
            e.Data.Dead = true; e.Presentation.Die(); Run.Kills++; Rules.State.Kills++; Rules.QuestEvent("kill", 1, false);
            DungeonScenery.Prop("bones",world,new Vector2(e.Data.X,e.Data.Y),new Vector2(1.1f,.55f));
            var drop = new GroundItem { X = e.Data.X, Y = e.Data.Y, Item = Rules.RollLoot(e.Data.Type == 3, e.Data.Type) }; Floor.Drops.Add(drop); DrawDrop(drop);
            Rules.State.PocketCoins += Mathf.CeilToInt((8 + Run.Difficulty * 5) * (Rules.HasEnchant("greed") ? 1 + .1f * Rules.EnchantStrength : 1));
            if (Rules.HasEnchant("mana")) Run.Mp = Mathf.Min(Rules.MaximumMp, Run.Mp + 3 * Rules.EnchantStrength);
            if (Rules.State.Skill("survival_mp_large") > 0) Run.Mp = Mathf.Min(Rules.MaximumMp, Run.Mp + 5);
            // Only a cleared room is a safe checkpoint.
            if (!Floor.Enemies.Exists(x => x.Room == e.Data.Room && !x.Dead)) GameSession.Save();
        }
        private void UpdateEnemies(float dt)
        {
            AudioDirector.Threat(enemies.Exists(e=>!e.Data.Dead&&e.Data.Type==3&&Vector2.Distance(Position,e.Node.transform.position)<9));
            foreach (var e in enemies)
            {
                if (e.Data.Dead) continue; e.Cooldown -= dt;
                var p = (Vector2)e.Node.transform.position; var delta = Position - p; var distance = delta.magnitude;
                if (e.Telegraph > 0)
                {
                    e.Telegraph -= dt;
                    if (e.Telegraph <= 0) { if (e.Data.Type == 2 || e.Data.Type == 3)
                        { Shoot(p, delta.normalized, (e.Data.Type == 3 ? 25 : 12) * DungeonGenerator.DifficultyMultiplier(Run.Difficulty), true);
                          if (e.Data.Type == 3) for (var angle = 0; angle < 8; angle++) Shoot(p, new Vector2(Mathf.Cos(angle * Mathf.PI / 4), Mathf.Sin(angle * Mathf.PI / 4)), 18 * DungeonGenerator.DifficultyMultiplier(Run.Difficulty), true); }
                        else if (distance < 1.8f) DamagePlayer((10 + e.Data.Type * 4) * DungeonGenerator.DifficultyMultiplier(Run.Difficulty), p); e.Cooldown = 1.4f; }
                }
                else if (distance < 9)
                {
                    var attackRange = e.Data.Type >= 2 ? 6 : 1.2f;
                    if (distance <= attackRange && e.Cooldown <= 0) {e.Telegraph = 0.45f;CombatFeedback.Halo(p,new Color(1,.28f,.06f,.45f),e.Data.Type>=2?2.5f:3,.45f);}
                    else if (distance > attackRange)
                    {
                        p=DungeonScenery.StepToward(Floor,p,Position,dt*(e.Data.Type==1?2.8f:2.1f),(e.Data.Room+e.Data.Type)%2==0?1:-1);
                    }
                }
                else { var patrol = new Vector2(Mathf.Sin(Time.time + e.Data.Room), Mathf.Cos(Time.time + e.Data.Room)) * dt * 0.4f;
                    if (CanWalk( p.x + patrol.x, p.y + patrol.y)) p += patrol; }
                e.Node.transform.position = p; e.Data.X = p.x; e.Data.Y = p.y;
                e.Presentation.Tick(delta.normalized,distance>1.2f&&distance<9,e.Telegraph>0);
                e.Node.GetComponent<SpriteRenderer>().sortingOrder=DungeonScenery.FootOrder(p.y);
            }
        }
        private void DamagePlayer(float damage, Vector2 source = default(Vector2))
        {
            if (immunity > 0) return; searchTarget = null; searchTime = 0;
            var frontal = source == Vector2.zero || Vector2.Dot((source - Position).normalized, facing) > .3f;
            if (guardTime > 0 && frontal) { Strike(2.5f, Rules.AttackPower * 1.5f * (Rules.State.Skill("sword_enhance1") > 0 ? 1.3f : 1), .3f); guardTime = 0; return; }
            if (Rules.State.Skill("survival_hp_medium") > 0 && sinceDamage < (Rules.State.Skill("survival_hp_large") > 0 ? 5 : 3)) damage *= Rules.State.Skill("survival_hp_large") > 0 ? .8f : .9f;
            if (shield > 0) { var absorbed = Mathf.Min(shield, damage); shield -= absorbed; damage -= absorbed; }
            if (damage > 0) Rules.ReceiveDamage(damage * (defenseBuff > 0 ? 0.8f : 1));
            sinceDamage = 0;
            var w = Rules.Weapon;
            if (Run.Hp <= 0 && undyingCooldown <= 0 && w != null && w.Branch == "sword" && Rules.State.Skill("sword_ultimate") > 0) { Run.Hp = 1; undyingCooldown = 60; }
            immunity = 0.5f; Pulse(Position, new Color(1, 0.2f, 0.2f), 1.4f);
            cameraShake=.9f;CombatFeedback.Number(Position,damage);animator.PlayOneShot("hit",.22f);
            AudioDirector.Cue(21);
        }
        private void Shoot(Vector2 p, Vector2 direction, float damage, bool hostile, bool blast = false)
        {
            var node = WorldArt.Shape("Projectile", world, p, Vector2.one * 0.3f, hostile ? new Color(1, 0.4f, 0.2f) : Color.cyan, 25, true);
            node.GetComponent<SpriteRenderer>().sortingOrder=10000;node.AddComponent<ProjectileTrail>().Initialize(hostile?new Color(1,.45f,.2f):new Color(.3f,.85f,1));
            bolts.Add(new Bolt { Node = node, Position = p, Direction = direction, Damage = damage, Life = hostile ? 1.8f : 8f / 12, Hostile = hostile, Blast = blast });
        }
        private void UpdateBolts(float dt)
        {
            for (var i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i]; b.Life -= dt; var steps = Mathf.Max(1, Mathf.CeilToInt(dt * 12 / 0.15f));
                for (var s = 0; s < steps && b.Life > 0; s++)
                {
                    b.Position += b.Direction * dt * 12 / steps;
                    if (!CanWalk( b.Position.x, b.Position.y)) b.Life = 0;
                    if (b.Hostile && Vector2.Distance(b.Position, Position) < 0.55f)
                    { if (reflectTime > 0 && Vector2.Dot(-b.Direction, facing) > .3f) { b.Hostile = false; b.Direction = -b.Direction;
                          b.Damage *= .8f * (Rules.State.Skill("sword_enhance2") > 0 ? 1.3f : 1); b.Position = Position + b.Direction * .7f; }
                      else { DamagePlayer(b.Damage, b.Position - b.Direction); b.Life = 0; } }
                    if (!b.Hostile) foreach (var e in enemies) if (!e.Data.Dead && Vector2.Distance(b.Position, e.Node.transform.position) < 0.7f)
                    { HurtEnemy(e, b.Damage); if (b.Blast) { foreach (var nearby in enemies) if (nearby != e && !nearby.Data.Dead && Vector2.Distance(b.Position, nearby.Node.transform.position) < 2) HurtEnemy(nearby, b.Damage * .6f);
                        Pulse(b.Position, Color.cyan, 4); } b.Life = 0; break; }
                }
                b.Node.transform.position = b.Position; if (b.Life <= 0) { Destroy(b.Node); bolts.RemoveAt(i); }
            }
        }
        private void HandleInteraction(float dt)
        {
            Prompt = "";
            var drop = Floor.Drops.Find(d => Vector2.Distance(Position, new Vector2(d.X, d.Y)) < 1.5f);
            if (drop != null) { Prompt = "E 拾取 " + Rules.Catalog.Get(drop.Item.Id).Name; if (GameInput.Down("interact")) { string error;
                if (Rules.Pickup(drop.Item, out error)) { Floor.Drops.Remove(drop);GameObject node;if(dropNodes.TryGetValue(drop,out node)){Destroy(node);dropNodes.Remove(drop);}AudioDirector.Cue(25);GamePanel.Toast("已拾取 "+Rules.Catalog.Get(drop.Item.Id).Name); } else GamePanel.Toast(error); } return; }
            foreach (var room in Floor.Rooms)
            {
                if (!room.Hidden || room.Opened) continue; var p = Floor.Rooms[room.Parent]; var door = new Vector2((room.X + p.X) * 8, (room.Y + p.Y) * 8);
                if (Vector2.Distance(Position, door) < 3.3f) { Prompt = "E 使用钥匙开启密室"; if (GameInput.Down("interact")) { string error; if (Rules.Unlock(room, out error)) BuildFloor(); else GamePanel.Toast(error); } return; }
            }
            if (Floor.ExtractRoom >= 0 && Vector2.Distance(Position, Marker(Floor.ExtractRoom, Vector2.zero)) < 2)
            { Prompt = "E 撤离 · 带出所有物资并永久解锁装备"; if (GameInput.Down("interact")) GamePanel.Confirm("立即撤离？", "确认后带出物资，获得职业等级与技能点。", () => End(true)); return; }
            if (Run.Floor < 5 && Vector2.Distance(Position, Marker(Floor.DownRoom, new Vector2(3, 3))) < 1.8f)
            { Prompt = "E 前往下一层 · 收益更高，风险更大"; if (GameInput.Down("interact")) Travel(1); return; }
            if (Run.Floor > 1 && Vector2.Distance(Position, Marker(0, new Vector2(-3, -3))) < 1.8f)
            { Prompt = "E 返回上一层"; if (GameInput.Down("interact")) Travel(-1); return; }
            var point = Floor.Searches.Find(s => !s.Searched && Floor.Rooms[s.Room].Opened && Vector2.Distance(Position, new Vector2(s.X, s.Y)) < 1.5f);
            if (point == null || move.sqrMagnitude > 0 || !GameInput.Held("interact")) { searchTime = 0; searchTarget = null; if (point != null) Prompt = "按住 E 搜索"+new[]{"木箱","书架","尸骸","宝箱","壁龛","补给袋"}[point.Type]+" · 移动或受击打断"; return; }
            if (searchTarget != point) { searchTarget = point; searchTime = 0; }
            searchTime += dt; Prompt = "正在搜索…";
            if (searchTime >= SearchDuration) { point.Searched = true; var loot = Rules.SearchLoot(point);
                if (loot != null) {var foundDrop=new GroundItem { X = point.X, Y = point.Y, Item = loot };Floor.Drops.Add(foundDrop);DrawDrop(foundDrop);}
                GameObject node;if(searchNodes.TryGetValue(point,out node))node.GetComponent<SpriteRenderer>().color=new Color(.55f,.55f,.55f,.7f);
                AudioDirector.Cue(26);CombatFeedback.Sparks(new Vector2(point.X,point.Y),new Color(1,.82f,.4f),6);
                GamePanel.Toast(loot == null ? "这里已经空了" : "发现 " + Rules.Catalog.Get(loot.Id).Name);searchTime=0;searchTarget=null; }
        }
        public void Travel(int direction)
        {
            string error = null;
            if (GameSession.Commit(r => r.ChangeFloor(direction, out error), true)) { dodgeTime = 0; immunity = 0.5f; BuildFloor(); }
            else { BuildFloor(); GamePanel.Toast(error ?? GameSession.SaveError); }
        }
        public void End(bool extract)
        {
            if (!GameSession.Finish(extract)) { BuildFloor(); GamePanel.Toast(GameSession.SaveError); GamePanel.OpenPage("pause"); return; }
            AudioDirector.Cue(extract ? 31 : 32);
            Time.timeScale = 1; UnityEngine.SceneManagement.SceneManager.LoadScene("Town");
        }
        public void UseQuick(int index)
        {
            if (potionCooldown > 0) { GamePanel.Toast("药剂冷却中"); return; }
            var quick = Rules.State.RigItems.FindAll(i => Rules.Catalog.Get(i.Id).Kind == ItemKind.Potion || Rules.Catalog.Get(i.Id).Kind == ItemKind.Scroll);
            if (index >= quick.Count) { GamePanel.Toast("快捷栏为空，请在小屋把药剂放入胸挂"); return; }
            string effect; if (!Rules.Consume(quick[index], out effect)) return; potionCooldown = 5;
            Pulse(Position,effect=="mp"?Color.cyan:new Color(.4f,1,.6f),2);AudioDirector.Cue(22);
            if (effect == "return") { End(true); return; }
            if (effect == "attack") attackBuff = 60; if (effect == "speed") speedBuff = 60; if (effect == "defense") defenseBuff = 60;
            if (effect == "shield") shield = 100; if (effect == "strength") strengthBuff = 30;
            if (effect == "fireball") { foreach (var e in enemies) if (Vector2.Distance(Position, e.Node.transform.position) < 4) HurtEnemy(e, 60); Pulse(Position, Color.yellow, 4); }
            if (effect == "detect") foreach (var r in Floor.Rooms) r.Visited = true;
            GamePanel.Toast("使用成功");
        }
        private void Pulse(Vector2 p, Color color, float radius)
        {
            CombatFeedback.Halo(p,new Color(color.r,color.g,color.b,.45f),radius,.35f);
        }
        private void OnDestroy(){foreach(var sprite in lootSprites.Values)if(sprite!=null)Destroy(sprite);}
        public void TeleportForTest(Vector2 p) { player.transform.position = p; Run.X = p.x; Run.Y = p.y; cameraView.transform.position = new Vector3(p.x,p.y,-10); }
    }
}



