using System;
using System.Collections.Generic;
using System.Globalization;

namespace KnightChronicles.Runtime.Core
{
    public enum ItemKind { Weapon, Armor, Rig, Pack, Material, Treasure, Potion, Scroll, Key }
    public enum Slot { Weapon, Head, Chest, Legs, Rig, Pack }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string Id, Name, Branch, Set, Effect;
        public ItemKind Kind;
        public Slot Slot;
        public int Tier, Power, Price, Durability, Capacity, CarryBonus, Stack = 1;
        public float Weight, Range, Interval, Speed, Mana;
    }

    [Serializable]
    public sealed class Item
    {
        public string Id;
        public int Count = 1, Durability, Maximum;
        public float WearRemainder;
        public List<string> Enchants = new List<string>();
        public Item Copy()
        {
            return new Item { Id = Id, Count = Count, Durability = Durability, Maximum = Maximum, WearRemainder = WearRemainder,
                Enchants = new List<string>(Enchants) };
        }
    }

    [Serializable]
    public sealed class SkillRank { public string Id; public int Rank; }
    [Serializable]
    public sealed class KeyBinding { public string Action, Key; }
    [Serializable]
    public sealed class QuestProgress { public string Id; public int Progress; public bool Claimed; }
    [Serializable]
    public sealed class RunReport
    {
        public bool Extracted;
        public int Floor, Kills, Coins;
        public List<Item> Items = new List<Item>();
        public List<string> Unlocks = new List<string>();
    }
    [Serializable]
    public sealed class Profile
    {
        public int Version = 1, Coins = 600, PocketCoins, Experience, CareerLevel = 1, SkillPoints;
        public int Runs, Extractions, Kills, Deepest, CraftCount, HiddenOpened, Chapter;
        public int WarehouseLevel = 1, TrainingLevel = 1, CollectionLevel = 1, WeightPoints, AttributePoints;
        public List<Item> Warehouse = new List<Item>();
        public List<Item> RigItems = new List<Item>();
        public List<Item> PackItems = new List<Item>();
        public Item[] Equipped = new Item[6];
        public List<Item> Exhibits = new List<Item>();
        public List<string> Unlocked = new List<string>();
        public List<SkillRank> Skills = new List<SkillRank>();
        public List<QuestProgress> Quests = new List<QuestProgress>();
        public List<KeyBinding> Bindings = new List<KeyBinding>();
        public RunData ActiveRun;
        public RunReport LastReport;
        public bool ReportPending;
        public float MasterVolume = 0.6f, MusicVolume = 0.5f, EffectsVolume = 0.8f, Zoom = 1f;
        public int Quality = 1;
        public int Rank { get { return Experience >= 4500 ? 4 : Experience >= 2100 ? 3 : Experience >= 900 ? 2 : Experience >= 300 ? 1 : 0; } }
        public int WarehouseCapacity { get { return 48 + (WarehouseLevel - 1) * 16; } }
        public int CollectionCapacity { get { return new[] { 8, 16, 24, 36, 50 }[CollectionLevel - 1]; } }
        public int Skill(string id) { var node = Skills.Find(s => s.Id == id); return node == null ? 0 : node.Rank; }
    }

    public sealed class Catalog
    {
        public readonly List<ItemDefinition> All = new List<ItemDefinition>();
        private readonly Dictionary<string, ItemDefinition> byId = new Dictionary<string, ItemDefinition>();
        public ItemDefinition Get(string id)
        {
            ItemDefinition result;
            if (id == null || !byId.TryGetValue(id, out result)) throw new ArgumentException("未知物品：" + id);
            return result;
        }
        public void Add(ItemDefinition d) { byId.Add(d.Id, d); All.Add(d); }
        // TSV is generated from the reviewed equipment list; stable IDs are persisted in saves.
        public static Catalog Parse(string tsv)
        {
            var catalog = new Catalog();
            foreach (var line in tsv.Replace("\r", "").Split('\n'))
            {
                if (line.Length == 0 || line.StartsWith("id\t")) continue;
                var v = line.Split('\t');
                if (v.Length != 18) throw new FormatException("物品表列数错误：" + line);
                catalog.Add(new ItemDefinition { Id = v[0], Name = v[1], Kind = (ItemKind)Enum.Parse(typeof(ItemKind), v[2]),
                    Branch = v[3], Slot = (Slot)Enum.Parse(typeof(Slot), v[4]), Tier = Int(v[5]), Power = Int(v[6]),
                    Price = Int(v[7]), Weight = Float(v[8]), Durability = Int(v[9]), Range = Float(v[10]),
                    Interval = Float(v[11]), Set = v[12], Effect = v[13], Capacity = Int(v[14]),
                    CarryBonus = Int(v[15]), Stack = Int(v[16]), Speed = Float(v[17]) });
            }
            return catalog;
        }
        private static int Int(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }
        private static float Float(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
        public Item Create(string id, Random random)
        {
            var d = Get(id);
            var item = new Item { Id = id, Maximum = d.Durability, Durability = d.Durability };
            if (d.Kind == ItemKind.Weapon)
            {
                var pool = new List<string> { "sharp", "fire", "slaughter", "drain", "haste", "critical", "pierce", "light", "mana", "greed" };
                for (var i = 0; i < Math.Max(0, d.Tier - 1); i++)
                {
                    var index = random.Next(pool.Count); item.Enchants.Add(pool[index]); pool.RemoveAt(index);
                }
            }
            return item;
        }
    }
}
