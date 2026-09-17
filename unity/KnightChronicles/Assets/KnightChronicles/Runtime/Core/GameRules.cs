using System;
using System.Collections.Generic;

namespace KnightChronicles.Runtime.Core
{
    public sealed class GameRules
    {
        public readonly Catalog Catalog;
        public Profile State;
        private readonly Random random = new Random();
        public GameRules(Catalog catalog, Profile profile) { Catalog = catalog; State = profile; }
        public static Profile NewProfile(Catalog catalog)
        {
            var p = new Profile(); var r = new Random(1);
            p.Equipped[(int)Slot.Rig] = catalog.Create("rig_canvas", r);
            p.Equipped[(int)Slot.Pack] = catalog.Create("pack_canvas", r);
            var sword = catalog.All.Find(d => d.Kind == ItemKind.Weapon && d.Branch == "sword" && d.Tier == 0);
            p.Equipped[0] = catalog.Create(sword.Id, r);
            p.RigItems.Add(new Item { Id = "potion_hp", Count = 3 });
            p.RigItems.Add(new Item { Id = "potion_mp", Count = 2 });
            p.PackItems.Add(new Item { Id = "key", Count = 1 });
            return p;
        }
        public int Capacity(Slot slot)
        {
            var item = State.Equipped[(int)slot];
            return item == null ? 0 : Catalog.Get(item.Id).Capacity;
        }
        public float Weight
        {
            get { var total = 0f; foreach (var i in State.RigItems) total += Catalog.Get(i.Id).Weight * i.Count;
                foreach (var i in State.PackItems) total += Catalog.Get(i.Id).Weight * i.Count; return total; }
        }
        public float CarryLimit
        {
            get { var pack = State.Equipped[(int)Slot.Pack];
                return 60 + (pack == null ? 0 : Catalog.Get(pack.Id).CarryBonus) + State.WeightPoints * 10
                    + new[] { 0, 10, 20, 30, 50 }[State.TrainingLevel - 1]; }
        }
        public static float WeightSpeed(float ratio) { return ratio <= 0.5f ? 1 : ratio <= 0.8f ? 0.9f : ratio <= 1 ? 0.8f : 0.65f; }
        public float MoveSpeed
        {
            get { var speed = 5f * (1 + State.Skill("survival_speed_small") * 0.02f);
                for (var s = 1; s <= 3; s++) { var item = State.Equipped[s]; if (item != null && item.Durability > 0) speed += Catalog.Get(item.Id).Speed; }
                speed *= 1 + SetBonus("leather");
                return Math.Max(2, speed) * (State.ActiveRun == null ? 1 : WeightSpeed(Weight / CarryLimit)); }
        }
        public float SetBonus(string branch)
        {
            ItemDefinition first = null;
            for (var slot = 1; slot <= 3; slot++)
            { var i = State.Equipped[slot]; if (i == null || i.Durability <= 0) return 0; var d = Catalog.Get(i.Id);
                if (d.Branch != branch || first != null && (d.Set != first.Set || d.Tier != first.Tier)) return 0; first = d; }
            if (branch == "heavy") return new[] { .06f, .08f, .1f, .13f, .18f }[first.Tier];
            if (branch == "plate") return new[] { .08f, .1f, .12f, .15f, .2f }[first.Tier];
            if (branch == "light") return new[] { .1f, .12f, .15f, .18f, .25f }[first.Tier];
            if (branch == "leather") return new[] { .05f, .06f, .08f, .1f, .14f }[first.Tier];
            if (branch == "robe") return new[] { .15f, .2f, .25f, .35f, .5f }[first.Tier]; return 0;
        }
        public float MaximumHp { get { return 100 * (1 + 0.08f * State.Skill("survival_hp_small")) * (1 + SetBonus("plate")); } }
        public float MaximumMp { get { var mp = 50f * (1 + 0.1f * State.Skill("survival_mp_small"));
            for (var s = 1; s <= 3; s++) { var i = State.Equipped[s]; if (i != null && i.Durability > 0 && Catalog.Get(i.Id).Branch == "robe") mp += 25; } return mp * (1 + SetBonus("robe")); } }
        public float EnchantStrength { get { var w = Weapon; return w == null || w.Tier < 2 ? 0 : w.Tier == 2 ? 1 : w.Tier == 3 ? 1.3f : 1.7f; } }
        public bool HasEnchant(string id) { return Weapon != null && State.Equipped[0].Enchants.Contains(id); }
        public float AttackInterval
        {
            get { var w = Weapon; if (w == null) return .5f;
                var bonus = w.Branch == "dagger" || w.Branch == "staff" ? 0 : State.Skill(w.Branch + "_mastery") * .03f;
                return w.Interval / (1 + bonus + (HasEnchant("haste") ? .08f * EnchantStrength : 0)); }
        }
        public float RollCooldown { get { return 1.5f * (1 - SetBonus("light")) * (1 - (HasEnchant("light") ? .08f * EnchantStrength : 0)); } }
        public ItemDefinition Weapon
        {
            get { var w = State.Equipped[0]; return w != null && w.Durability > 0 ? Catalog.Get(w.Id) : null; }
        }
        public float AttackPower
        {
            get { var d = Weapon; if (d == null) return 5;
                var power = d.Power * (1 + State.Skill(d.Branch + "_damage") * 0.04f);
                if (HasEnchant("sharp")) power *= 1 + .08f * EnchantStrength;
                return power; }
        }
        public float Defense
        {
            get { var defense = 0f; for (var s = 1; s <= 3; s++) { var i = State.Equipped[s];
                if (i != null && i.Durability > 0) defense += Catalog.Get(i.Id).Power; }
                return Math.Min(0.6f, defense * 0.01f * (1 + State.Skill("survival_defense_small") * 0.05f)); }
        }
        private bool CanAdd(List<Item> list, int capacity, Item item)
        {
            var d = Catalog.Get(item.Id); var remaining = item.Count;
            foreach (var i in list) if (i.Id == item.Id && d.Stack > 1) remaining -= d.Stack - i.Count;
            return remaining <= 0 || list.Count + (remaining + d.Stack - 1) / d.Stack <= capacity;
        }
        private void Add(List<Item> list, Item item)
        {
            var remaining = item.Count; var d = Catalog.Get(item.Id);
            foreach (var i in list)
            {
                if (i.Id != item.Id || d.Stack <= 1) continue;
                var amount = Math.Min(d.Stack - i.Count, remaining); i.Count += amount; remaining -= amount;
            }
            while (remaining > 0) { var copy = item.Copy(); copy.Count = Math.Min(d.Stack, remaining); list.Add(copy); remaining -= copy.Count; }
        }
        public bool Pickup(Item item, out string error)
        {
            error = null;
            if (State.ActiveRun != null && Weight + Catalog.Get(item.Id).Weight * item.Count > CarryLimit * 1.2f) { error = "负重超过 120%，无法拾取"; return false; }
            var quick = Catalog.Get(item.Id).Kind == ItemKind.Potion || Catalog.Get(item.Id).Kind == ItemKind.Scroll;
            if (quick && CanAdd(State.RigItems, Capacity(Slot.Rig), item)) { Add(State.RigItems, item); return true; }
            if (CanAdd(State.PackItems, Capacity(Slot.Pack), item)) { Add(State.PackItems, item); return true; }
            if (CanAdd(State.RigItems, Capacity(Slot.Rig), item)) { Add(State.RigItems, item); return true; }
            error = "胸挂与背包已满"; return false;
        }
        public bool Transfer(List<Item> from, List<Item> to, int capacity, Item item, out string error)
        {
            error = null;
            if (State.ActiveRun != null && (from == State.Warehouse || to == State.Warehouse)) { error = "地下城无法使用仓库"; return false; }
            if (from == to || !from.Contains(item) || !CanAdd(to, capacity, item)) { error = "目标空间不足"; return false; }
            if (State.ActiveRun != null && (to == State.RigItems || to == State.PackItems) && from != State.RigItems && from != State.PackItems
                && Weight + Catalog.Get(item.Id).Weight * item.Count > CarryLimit * 1.2f) { error = "负重超限"; return false; }
            Add(to, item); from.Remove(item); return true;
        }
        public bool Equip(List<Item> source, Item item, out string error)
        {
            error = null; var d = Catalog.Get(item.Id);
            if (!source.Contains(item) || d.Kind > ItemKind.Pack) { error = "此物品不可穿戴"; return false; }
            if (State.ActiveRun != null && d.Kind != ItemKind.Weapon) { error = "请回镇更换防具和容器"; return false; }
            if (d.Kind <= ItemKind.Armor && d.Tier > State.Rank) { error = "冒险者等级不足，需要" + TierName(d.Tier); return false; }
            if ((d.Kind == ItemKind.Rig && State.RigItems.Count > d.Capacity) || (d.Kind == ItemKind.Pack && State.PackItems.Count > d.Capacity))
            { error = "新容器装不下现有物品"; return false; }
            var previous = State.Equipped[(int)d.Slot];
            // Swap uses the source cell; no item is discarded even when the container is full.
            source.Remove(item); if (previous != null) source.Add(previous); State.Equipped[(int)d.Slot] = item;
            return true;
        }
        public bool Unequip(Slot slot, out string error)
        {
            error = null;
            if (State.ActiveRun != null) { error = "请回镇卸下装备"; return false; }
            var item = State.Equipped[(int)slot]; if (item == null) return false;
            if (slot == Slot.Rig && State.RigItems.Count > 0 || slot == Slot.Pack && State.PackItems.Count > 0) { error = "先清空容器"; return false; }
            if (!CanAdd(State.Warehouse, State.WarehouseCapacity, item)) { error = "仓库已满"; return false; }
            Add(State.Warehouse, item); State.Equipped[(int)slot] = null; return true;
        }
        public bool ShopVisible(ItemDefinition d)
        {
            if (d.Kind == ItemKind.Weapon || d.Kind == ItemKind.Armor)
                return (d.Tier == 0 && Catalog.All.Find(x => x.Kind == d.Kind && x.Branch == d.Branch && x.Slot == d.Slot && x.Tier == 0) == d) || State.Unlocked.Contains(d.Id);
            return d.Kind != ItemKind.Material && d.Kind != ItemKind.Treasure && d.Tier <= State.Rank;
        }
        public bool Buy(string id, out string error)
        {
            error = null; var d = Catalog.Get(id); var price = (int)Math.Ceiling(d.Price * (1 - State.Skill("explore_discount") * 0.05f));
            if (State.ActiveRun != null || !ShopVisible(d)) { error = "物品尚未解锁"; return false; }
            if (State.Coins < price) { error = "保险箱金币不足"; return false; }
            var item = Catalog.Create(id, random);
            // Replacement containers can be bought after death even when no inventory space exists.
            if ((d.Kind == ItemKind.Rig || d.Kind == ItemKind.Pack) && State.Equipped[(int)d.Slot] == null) State.Equipped[(int)d.Slot] = item;
            else if (!Pickup(item, out error)) return false;
            State.Coins -= price; return true;
        }
        public bool Sell(List<Item> source, Item item, out string error)
        {
            error = null;
            if (State.ActiveRun != null || source == State.Exhibits || !source.Contains(item)) { error = "无法出售"; return false; }
            State.Coins += SellPrice(item); source.Remove(item); return true;
        }
        public int SellPrice(Item item) { return (int)(Catalog.Get(item.Id).Price * item.Count * (0.3f + 0.009f * State.Skill("explore_sell"))); }
        public bool Repair(Item item, bool maintain, out string error)
        {
            error = null; var d = Catalog.Get(item.Id);
            if (State.ActiveRun != null || d.Durability == 0 || item.Durability == item.Maximum) { error = "此物品无需维修"; return false; }
            if (maintain && item.Maximum < 30) { error = "耐久上限低于 30，请在小屋维修"; return false; }
            var discount = maintain && State.Skill("survival_wear_medium") > 0 || !maintain && State.Skill("survival_wear_large") > 0 ? .8f : 1;
            var price = (int)Math.Ceiling(item.Maximum * (maintain ? 2 : 4) * discount);
            if (State.Coins < price) { error = "金币不足"; return false; }
            State.Coins -= price; if (maintain) item.Maximum -= 10; item.Durability = item.Maximum; return true;
        }
        public bool BeginRun(int difficulty, int seed, out string error)
        {
            error = null;
            if (State.ActiveRun != null) { error = "请先继续或放弃当前远征"; return false; }
            if (difficulty < 0 || difficulty > State.Rank) { error = "难度尚未解锁"; return false; }
            if (Capacity(Slot.Rig) + Capacity(Slot.Pack) == 0) { error = "请先购买并穿戴胸挂或背包"; return false; }
            if (Weight > CarryLimit * 1.2f) { error = "负重超过 120%，请先整理物品"; return false; }
            State.Runs++; State.ActiveRun = DungeonGenerator.Generate(seed, difficulty, State.Skill("explore_hidden"));
            State.ActiveRun.Hp = MaximumHp; State.ActiveRun.Mp = MaximumMp;
            foreach (var s in State.Skills) State.ActiveRun.GenerationSkills.Add(new SkillRank { Id = s.Id, Rank = s.Rank });
            if(State.Skill("explore_map")>0)State.ActiveRun.Floors[0].Rooms[1].Visited=true;
            State.LastReport = null; return true;
        }
        public bool ChangeFloor(int direction, out string error)
        {
            error = null; var run = State.ActiveRun;
            if (run == null || direction != 1 && direction != -1 || run.Floor + direction < 1 || run.Floor + direction > 5) { error = "没有可通行的楼层"; return false; }
            run.Floor += direction; var floor = run.Floors[run.Floor - 1];
            var room = floor.Rooms[direction > 0 ? 0 : floor.DownRoom]; run.X = room.X * DungeonGenerator.Spacing; run.Y = room.Y * DungeonGenerator.Spacing;
            room.Visited = true; State.Deepest = Math.Max(State.Deepest, run.Floor); QuestEvent("explore", run.Floor, true); return true;
        }
        public RunReport FinishRun(bool extract)
        {
            var run = State.ActiveRun; if (run == null) throw new InvalidOperationException("没有进行中的远征");
            var report = new RunReport { Extracted = extract, Floor = run.Floor, Kills = run.Kills, Coins = State.PocketCoins };
            foreach (var i in State.RigItems) report.Items.Add(i.Copy()); foreach (var i in State.PackItems) report.Items.Add(i.Copy());
            foreach (var i in State.Equipped) if (i != null) report.Items.Add(i.Copy());
            if (extract)
            {
                foreach (var i in report.Items)
                {
                    var d = Catalog.Get(i.Id);
                    if (d.Kind <= ItemKind.Armor && !State.Unlocked.Contains(i.Id)) { State.Unlocked.Add(i.Id); report.Unlocks.Add(i.Id); }
                }
                State.Extractions++; State.CareerLevel++; State.SkillPoints++;
                QuestEvent("extract", 1, false);
                foreach (var i in State.PackItems) if (Catalog.Get(i.Id).Kind == ItemKind.Material) QuestEvent("loot", i.Count, false);
                State.Coins += State.PocketCoins;
                // Overflow remains carried and is explicitly shown to the player; no loot disappears.
                DepositAll(State.RigItems); DepositAll(State.PackItems);
            }
            else { State.RigItems.Clear(); State.PackItems.Clear(); State.Equipped = new Item[6]; }
            State.PocketCoins = 0; State.ActiveRun = null; State.LastReport = report; State.ReportPending = true; return report;
        }
        public void DepositAll(List<Item> source)
        {
            for (var i = source.Count - 1; i >= 0; i--) if (CanAdd(State.Warehouse, State.WarehouseCapacity, source[i])) { Add(State.Warehouse, source[i]); source.RemoveAt(i); }
        }
        private void Wear(Item item, float amount)
        {
            var factor = 1 - State.Skill("survival_wear_small") * .1f - (State.Skill("survival_wear_large") > 0 ? .3f : 0);
            item.WearRemainder += amount * factor; var loss = (int)Math.Floor(item.WearRemainder + .0001f);
            item.WearRemainder -= loss; item.Durability = Math.Max(0, item.Durability - loss);
        }
        public void WearWeapon() { var w = State.Equipped[0]; if (w != null) Wear(w, 1); }
        public float ReceiveDamage(float raw)
        {
            var reduction = Math.Min(.6f, 1 - (1 - Defense) * (1 - SetBonus("heavy")));
            var damage = Math.Max(1, raw * (1 - reduction));
            for (var s = 1; s <= 3; s++) { var i = State.Equipped[s]; if (i != null) Wear(i, 2); }
            if (State.ActiveRun != null) State.ActiveRun.Hp = Math.Max(0, State.ActiveRun.Hp - damage); return damage;
        }
        public bool Consume(Item item, out string effect)
        {
            effect = null; if (State.ActiveRun == null || !State.RigItems.Contains(item)) return false;
            var d = Catalog.Get(item.Id); if (d.Kind != ItemKind.Potion && d.Kind != ItemKind.Scroll) return false;
            effect = d.Effect;
            if (d.Effect == "hp") State.ActiveRun.Hp = Math.Min(MaximumHp, State.ActiveRun.Hp + d.Power * (1 + 0.08f * State.Skill("survival_potion_small")));
            if (d.Effect == "mp") State.ActiveRun.Mp = Math.Min(MaximumMp, State.ActiveRun.Mp + d.Power);
            var alchemy = State.Skill("survival_potion_medium") > 0 && random.NextDouble() < (State.Skill("survival_potion_large") > 0 ? .5 : .3);
            if (!alchemy) { item.Count--; if (item.Count == 0) State.RigItems.Remove(item); } return true;
        }
        public Item RollLoot(bool elite, int type)
        {
            var run = State.ActiveRun; var rng = new Random(unchecked(run.Seed + ++run.RollCounter * 7919));
            var roll = rng.Next(100); ItemKind kind = elite || State.Runs <= 3 && run.Kills == 1 ? ItemKind.Weapon
                : roll < 60 - run.Difficulty * 5 ? ItemKind.Material : roll < 90 - run.Difficulty * 5 ? ItemKind.Treasure : ItemKind.Weapon;
            var quality = .18 + run.Floor * .03 + run.GenerationSkill("explore_quality") * .05 + run.GenerationSkill("explore_depth") * run.Floor * .03;
            // Legendary equipment is crafting-only; legendary materials remain available in high difficulties.
            var tier = elite ? 3 : Math.Min(kind == ItemKind.Material || kind == ItemKind.Treasure ? 4 : 3,
                Math.Max(0, run.Difficulty - 1) + (rng.NextDouble() < quality ? 1 : 0));
            if (kind == ItemKind.Weapon && rng.Next(2) == 0 && !elite) kind = ItemKind.Armor;
            if (State.Runs <= 3 && run.Kills == 1 && !elite && kind <= ItemKind.Armor) tier = Math.Min(tier, State.Rank);
            var pool = Catalog.All.FindAll(d => d.Kind == kind && d.Tier == tier);
            return Catalog.Create(pool[rng.Next(pool.Count)].Id, rng);
        }
        public Item SearchLoot(SearchPoint point)
        {
            var run = State.ActiveRun; var r = new Random(unchecked(run.Seed + ++run.RollCounter * 7919));
            if (r.NextDouble() < .15 - run.GenerationSkill("explore_empty") * .05) return null;
            if (point.Type == 5) return Catalog.Create(r.Next(2) == 0 ? "potion_hp" : "potion_mp", r);
            var loot = RollLoot(run.Floor == 5 && point.Type == 3 || r.NextDouble() < run.GenerationSkill("explore_value") * .08, point.Type);
            if (Catalog.Get(loot.Id).Stack > 1 && r.NextDouble() < run.GenerationSkill("explore_double") * .05) loot.Count = 2; return loot;
        }
        public bool Unlock(Room room, out string error)
        {
            error = null;
            if (!room.Hidden || room.Opened) return false;
            var source = State.PackItems; var key = source.Find(i => Catalog.Get(i.Id).Kind == ItemKind.Key);
            if (key == null) { source = State.RigItems; key = source.Find(i => Catalog.Get(i.Id).Kind == ItemKind.Key); }
            if (key == null) { error = "需要一把钥匙，可在杂货铺购买"; return false; }
            if (State.Skill("explore_key") == 0 || random.NextDouble() >= .2) { key.Count--; if (key.Count == 0) source.Remove(key); }
            room.Opened = true; State.HiddenOpened++; QuestEvent("hidden", 1, false); return true;
        }
        public bool Upgrade(string area, out string error)
        {
            error = null;
            var level = area == "warehouse" ? State.WarehouseLevel : area == "training" ? State.TrainingLevel : State.CollectionLevel;
            if (State.ActiveRun != null || level >= 5) { error = "已达满级或当前不在小镇"; return false; }
            var price = new[] { 1000, 2500, 6000, 15000 }[level - 1];
            if (State.Coins < price) { error = "金币不足，升级需要 " + price; return false; }
            State.Coins -= price; if (area == "warehouse") State.WarehouseLevel++; else if (area == "training") State.TrainingLevel++; else State.CollectionLevel++; return true;
        }
        public bool SpendWeight(out string error)
        {
            error = null; if (State.ActiveRun != null || State.AttributePoints <= 0) { error = "没有可用属性点"; return false; }
            State.AttributePoints--; State.WeightPoints++; return true;
        }
        public bool Display(Item item, out string error)
        {
            error = null; if (State.ActiveRun != null || !State.Warehouse.Contains(item) || Catalog.Get(item.Id).Kind > ItemKind.Armor || State.Exhibits.Count >= State.CollectionCapacity)
            { error = "收藏室已满或物品不可陈列"; return false; }
            State.Exhibits.Add(item); State.Warehouse.Remove(item); return true;
        }
        public void QuestEvent(string type, int amount, bool maximum)
        {
            foreach (var q in State.Quests) { var d = QuestCatalog.Get(q.Id); if (!q.Claimed && d.Type == type) q.Progress = Math.Min(d.Target, maximum ? Math.Max(q.Progress, amount) : q.Progress + amount); }
        }
        public bool AcceptQuest(string id, out string error)
        {
            error = null; var d = QuestCatalog.Get(id);
            if (State.ActiveRun != null || d.Rank > State.Rank || State.Quests.Exists(q => q.Id == id) || State.Quests.FindAll(q => !q.Claimed).Count >= 3)
            { error = "无法接取：等级不足、重复任务或已接满三项"; return false; }
            State.Quests.Add(new QuestProgress { Id = id }); return true;
        }
        public bool ClaimQuest(string id, out string error)
        {
            error = null; var q = State.Quests.Find(x => x.Id == id); var d = QuestCatalog.Get(id);
            if (State.ActiveRun != null || q == null || q.Claimed || q.Progress < d.Target || State.LastReport == null || !State.LastReport.Extracted)
            { error = "成功撤离后才能结算已达成任务"; return false; }
            var old = State.Rank; q.Claimed = true; State.Coins += d.Coins; State.Experience += d.Xp; State.AttributePoints += (State.Rank - old) * 2; return true;
        }
        public static string TierName(int tier) { return new[] { "青铜", "白银", "黄金", "钻石", "传说" }[tier]; }
    }
    public sealed class QuestDefinition { public string Id, Name, Type; public int Rank, Target, Coins, Xp; }
    public static class QuestCatalog
    {
        public static readonly List<QuestDefinition> All = Build();
        private static List<QuestDefinition> Build()
        {
            var result = new List<QuestDefinition>();
            var types = new[] { "kill", "loot", "explore", "extract", "craft", "hidden" };
            var names = new[] { "清剿地下城", "物资回收", "深入调查", "平安归来", "工匠委托", "密室调查" };
            for (var rank = 0; rank < 5; rank++) for (var i = 0; i < types.Length; i++)
                result.Add(new QuestDefinition { Id = "quest_" + rank + "_" + i, Name = GameRules.TierName(rank) + " · " + names[i], Type = types[i], Rank = rank,
                    Target = i == 0 ? 6 + rank * 4 : i == 1 ? 3 + rank * 2 : i == 2 ? Math.Min(5, rank + 2) : i == 3 ? rank + 1 : 1,
                    Coins = 200 + rank * 200, Xp = 150 + rank * 150 });
            return result;
        }
        public static QuestDefinition Get(string id) { var d = All.Find(x => x.Id == id); if (d == null) throw new ArgumentException(id); return d; }
    }
}
