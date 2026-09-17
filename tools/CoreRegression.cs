using System;
using System.IO;
using System.Linq;
using KnightChronicles.Runtime.Core;

public static class CoreRegression
{
    private static int checks;
    private static Catalog catalog;
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    private static GameRules New() { return new GameRules(catalog, GameRules.NewProfile(catalog)); }
    private static Item Item(string id) { return catalog.Create(id, new Random(2)); }
    private static void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); }
    public static int Main(string[] args)
    {
        try
        {
            catalog = Catalog.Parse(File.ReadAllText(args[0]));
            Test("catalog", () => {
                Check(catalog.All.Count(d => d.Kind <= ItemKind.Armor) == 625, "625 equipment");
                Check(catalog.All.Count(d => d.Kind == ItemKind.Weapon) == 250, "250 weapons");
                Check(catalog.All.Count(d => d.Kind == ItemKind.Armor) == 375, "375 armor");
                foreach (var d in catalog.All) { Check(d.Stack > 0 && d.Price >= 0 && d.Weight >= 0, d.Id); var i = Item(d.Id);
                    Check(i.Enchants.Distinct().Count() == i.Enchants.Count, "unique enchants");
                    Check(i.Enchants.Count == (d.Kind == ItemKind.Weapon ? Math.Max(0, d.Tier - 1) : 0), "enchant slots"); }
            });
            Test("weight boundaries", () => {
                Check(GameRules.WeightSpeed(.5f) == 1, "50%"); Check(GameRules.WeightSpeed(.5001f) == .9f, "above 50%");
                Check(GameRules.WeightSpeed(.8f) == .9f, "80%"); Check(GameRules.WeightSpeed(.8001f) == .8f, "above 80%");
                Check(GameRules.WeightSpeed(1f) == .8f, "100%"); Check(GameRules.WeightSpeed(1.0001f) == .65f, "above 100%");
            });
            Test("buy and failed buy", () => { var g = New(); string e; var before = g.State.Coins;
                Check(g.Buy("key", out e), "buy key"); Check(g.State.Coins == before - 150, "debit once");
                g.State.Coins = 0; var count = g.State.PackItems.Sum(i => i.Count); Check(!g.Buy("key", out e), "reject poor");
                Check(g.State.PackItems.Sum(i => i.Count) == count, "no item on failure");
                var hidden = catalog.All.Find(d => d.Kind == ItemKind.Weapon && d.Tier == 2); Check(!g.ShopVisible(hidden), "unseen hidden");
            });
            Test("containers and overweight", () => { var g = New(); string e; Check(g.BeginRun(0, 3, out e), "begin");
                var heavy = new Item { Id = "ore_0", Count = 99 }; var before = g.Weight;
                Check(!g.Pickup(heavy, out e), "overweight refused"); Check(g.Weight == before, "no partial pickup");
                g.State.PackItems.Clear(); g.State.RigItems.Clear();
                var weapon = catalog.All.Find(d => d.Kind == ItemKind.Weapon && d.Tier == 0 && d.Branch == "dagger");
                for (var i = 0; i < g.Capacity(Slot.Pack); i++) g.State.PackItems.Add(Item(weapon.Id));
                for (var i = 0; i < g.Capacity(Slot.Rig); i++) g.State.RigItems.Add(new Item { Id = "key", Count = 99 });
                Check(!g.Pickup(Item("key"), out e), "full containers refused");
            });
            Test("equipment gates and swaps", () => { var g = New(); string e;
                var high = catalog.All.Find(d => d.Kind == ItemKind.Weapon && d.Tier == 3); var item = Item(high.Id); g.State.Warehouse.Add(item);
                Check(!g.Equip(g.State.Warehouse, item, out e), "tier gate"); Check(g.State.Warehouse.Contains(item), "rejected retained");
                g.State.Experience = 2100; Check(g.Equip(g.State.Warehouse, item, out e), "swap"); Check(g.State.Equipped[0] == item, "equipped");
                Check(g.State.Warehouse.Count == 1, "old weapon retained");
                item.Durability = 0; Check(g.Weapon == null && g.AttackPower == 5, "broken becomes fist");
            });
            Test("maintenance and repair", () => { var g = New(); string e; var w = g.State.Equipped[0]; w.Durability = 10; g.State.Coins = 5000;
                Check(g.Repair(w, true, out e), "maintain"); Check(w.Maximum == 90 && w.Durability == 90 && g.State.Coins == 4800, "maintain accounting");
                w.Maximum = 20; w.Durability = 0; Check(!g.Repair(w, true, out e), "maintenance floor");
                Check(g.Repair(w, false, out e), "repair low cap"); Check(w.Maximum == 20 && w.Durability == 20 && g.State.Coins == 4720, "repair accounting");
            });
            Test("death exact deletion and preservation", () => { for (var round = 0; round < 100; round++) {
                var g = New(); string e; var saved = Item("ore_0"); g.State.Warehouse.Add(saved); g.State.Experience = 300; g.State.SkillPoints = 4;
                g.State.Quests.Add(new QuestProgress { Id = "quest_0_0", Progress = 3 }); g.State.Unlocked.Add("eq_0001");
                g.BeginRun(0, round, out e); g.State.PocketCoins = 99; var report = g.FinishRun(false);
                Check(g.State.RigItems.Count == 0 && g.State.PackItems.Count == 0 && g.State.Equipped.All(i => i == null), "delete carried");
                Check(g.State.PocketCoins == 0 && g.State.ActiveRun == null, "delete pocket and snapshot");
                Check(g.State.Warehouse.Single() == saved && g.State.Coins == 600 && g.State.Experience == 300 && g.State.SkillPoints == 4, "preserve safe state");
                Check(g.State.Quests[0].Progress == 3 && g.State.Unlocked.Count == 1 && report.Items.Count > 0 && report.Coins == 99, "report and persistent quests");
            } });
            Test("extraction unlocks and overflow", () => { var g = New(); string e; var id = catalog.All.Find(d => d.Kind == ItemKind.Weapon && d.Tier == 1).Id;
                var w = Item(id); g.State.PackItems.Add(w); g.State.PackItems.Add(Item(id)); g.BeginRun(0, 8, out e); g.State.PocketCoins = 42;
                var report = g.FinishRun(true); Check(g.State.CareerLevel == 2 && g.State.SkillPoints == 1 && g.State.Extractions == 1, "career increment");
                Check(report.Unlocks.Count(x => x == id) == 1 && g.State.Unlocked.Contains(id) && g.ShopVisible(catalog.Get(id)), "idempotent unlock");
                Check(g.State.Coins == 642 && g.State.Warehouse.Count > 0 && g.State.PocketCoins == 0, "deposit");
                var overflow = New(); for (var i = 0; i < 48; i++) overflow.State.Warehouse.Add(Item("eq_0001"));
                var carried = overflow.State.RigItems.Sum(i => i.Count) + overflow.State.PackItems.Sum(i => i.Count); overflow.BeginRun(0, 1, out e); overflow.FinishRun(true);
                Check(overflow.State.RigItems.Sum(i => i.Count) + overflow.State.PackItems.Sum(i => i.Count) == carried, "overflow not lost");
            });
            Test("quests persist death and prevent duplicate rewards", () => { var g = New(); string e; g.AcceptQuest("quest_0_0", out e); g.QuestEvent("kill", 6, false);
                Check(!g.ClaimQuest("quest_0_0", out e), "extraction gate"); g.BeginRun(0, 1, out e); g.FinishRun(true);
                Check(g.ClaimQuest("quest_0_0", out e), "claim"); var coins = g.State.Coins; Check(!g.ClaimQuest("quest_0_0", out e) && coins == g.State.Coins, "no double reward");
            });
            Test("95 point skill tree and prerequisite gates", () => {
                var g = New(); string e; g.State.SkillPoints = 100;
                Check(SkillTree.All.Sum(n => n.Maximum) == 95, "95 total");
                Check(!SkillTree.Learn(g.State, "sword_skill1", out e), "skill gate");
                foreach (var n in SkillTree.All) for (var i = 0; i < n.Maximum; i++) Check(SkillTree.Learn(g.State, n.Id, out e), "learn " + n.Id);
                Check(g.State.SkillPoints == 5, "exact cost");
                foreach (var n in SkillTree.All) Check(!SkillTree.Learn(g.State, n.Id, out e), "maximum " + n.Id);
                g.BeginRun(0, 1, out e); Check(!SkillTree.Learn(g.State, "sword_damage", out e), "town only");
            });
            Test("crafting legendary, failure and set activation", () => {
                var g = New(); string e; var id = catalog.All.First(d => d.Kind == ItemKind.Weapon && d.Tier == 4).Id;
                g.State.Warehouse.Add(new Item { Id = "ore_4", Count = 3 }); g.State.Coins = 1000;
                Check(Crafting.Craft(g, id, 4, out e), "legend craft"); Check(g.State.Warehouse.Count == 0, "consume 3");
                var item = g.State.PackItems.Single(i => i.Id == id); Check(item.Enchants.Distinct().Count() == 3, "three unique");
                var before = g.State.Coins; Check(!Crafting.Craft(g, id, 4, out e) && g.State.Coins == before, "failure atomic");
                var set = catalog.All.Where(d => d.Kind == ItemKind.Armor && d.Branch == "plate" && d.Tier == 0).Take(3);
                foreach (var d in set) g.State.Equipped[(int)d.Slot] = Item(d.Id);
                Check(Math.Abs(g.MaximumHp-108)<.01, "plate set"); g.State.Equipped[3].Durability = 0; Check(g.MaximumHp==100, "broken deactivates");
            });
            Test("twenty whole rule loops including L5 backtrack and death recovery", () => {
                for (var i = 0; i < 20; i++) { var g = New(); string e; Check(g.BeginRun(0, i, out e), "begin loop");
                    for (var f = 0; f < 4; f++) Check(g.ChangeFloor(1, out e), "five floors");
                    Check(g.ChangeFloor(-1, out e), "backtrack");
                    g.State.ActiveRun.Kills=1; var loot = g.RollLoot(false,0); Check(g.Pickup(loot,out e), "loot");
                    var extracted = i % 2 == 0; var report = g.FinishRun(extracted); Check(report.Extracted==extracted, "outcome");
                    if (!extracted) { Check(g.Buy("rig_canvas",out e), "buy recovery rig"); Check(g.Capacity(Slot.Rig)==6, "recovery equips directly");
                        Check(g.Buy("pack_canvas",out e), "buy recovery pack"); Check(g.BeginRun(0,i+100,out e), "can reenter after death"); }
                }
            });
            Test("1000 deterministic connected dungeons and backtracking", () => {
                for (var seed = 0; seed < 1000; seed++) { var a = DungeonGenerator.Generate(seed, seed % 5); var b = DungeonGenerator.Generate(seed, seed % 5);
                    Check(a.Floors.Count == 5, "five floors"); foreach (var f in a.Floors) {
                        var count = f.Rooms.Count(r => !r.Hidden); Check(count >= 7 && count <= 10, "room count");
                        Check(f.Rooms.Select(r => r.X + ":" + r.Y).Distinct().Count() == f.Rooms.Count, "no overlap");
                        Check(f.ExtractRoom >= 0 == (f.Number == 2 || f.Number == 4), "extraction only L2 L4");
                        Check(f.Searches.Count(s => !f.Rooms[s.Room].Hidden) >= 8 && f.Searches.Count(s => !f.Rooms[s.Room].Hidden) <= 15, "search count");
                        for (var i = 1; i < f.Rooms.Count; i++) { var r = f.Rooms[i]; Check(r.Parent >= 0 && r.Parent < i, "connected tree");
                            var p = f.Rooms[r.Parent]; Check(Math.Abs(r.X-p.X)+Math.Abs(r.Y-p.Y)==1, "adjacent"); }
                        Check(string.Join(";", f.Rooms.Select(r => r.X + ":" + r.Y).ToArray()) == string.Join(";", b.Floors[f.Number-1].Rooms.Select(r => r.X + ":" + r.Y).ToArray()), "deterministic");
                    }
                }
                var g = New(); string e; g.BeginRun(0, 1, out e); for (var i = 0; i < 4; i++) Check(g.ChangeFloor(1, out e), "descend");
                Check(g.ChangeFloor(-1, out e) && g.State.ActiveRun.Floor == 4, "L5 back to L4");
            });
            Test("atomic save corruption backup", () => {
                var dir = Path.Combine(Path.GetTempPath(), "KnightRegression-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
                try { var path = Path.Combine(dir, "profile.save"); var store = new AtomicSaveStore(path); bool backup;
                    store.Write("{\"coins\":100}"); Check(store.Read(out backup) == "{\"coins\":100}" && !backup, "roundtrip");
                    store.Write("{\"coins\":200}"); File.WriteAllText(path, "corrupt"); Check(store.Read(out backup) == "{\"coins\":100}" && backup, "backup restore");
                    Check(!File.Exists(path + ".tmp"), "no orphan temporary");
                } finally { foreach (var file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
            });
            Test("null slots, dead inventory and run survive JSON roundtrips", () => {
                var g = New(); string e; g.State = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(g.State));
                Check(g.State.ActiveRun == null && g.State.LastReport == null && g.State.Equipped[1] == null, "optional nulls");
                g.BeginRun(0,5,out e); g.State = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(g.State));
                for(var i=0;i<4;i++) { Check(g.ChangeFloor(1,out e), "serialized floors"); g.State=ProfileSerializer.Deserialize(ProfileSerializer.Serialize(g.State)); }
                g.FinishRun(false); g.State=ProfileSerializer.Deserialize(ProfileSerializer.Serialize(g.State));
                Check(g.State.ActiveRun==null && g.State.Equipped.All(i=>i==null) && g.State.PackItems.Count==0, "dead persists");
                Check(g.Buy("rig_canvas",out e), "serialized recovery");
            });
            Console.WriteLine("RESULT " + checks + " checks passed"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
