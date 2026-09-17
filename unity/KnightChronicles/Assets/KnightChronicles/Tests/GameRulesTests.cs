using System;
using System.IO;
using System.Linq;
using KnightChronicles.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace KnightChronicles.Tests
{
    public sealed class GameRulesTests
    {
        private Catalog catalog;
        private GameRules rules;
        [SetUp] public void Setup() { catalog = Catalog.Parse(Resources.Load<TextAsset>("Data/items").text); rules = new GameRules(catalog, GameRules.NewProfile(catalog)); }
        [Test] public void DeathDeletesCarriedButPreservesWarehouseQuestsAndGrowth()
        {
            string error; rules.State.Warehouse.Add(new Item { Id = "ore_0", Count = 12 }); rules.State.Experience = 300;
            rules.AcceptQuest("quest_0_0", out error); rules.QuestEvent("kill", 3, false); rules.BeginRun(0, 100, out error);
            rules.State.PocketCoins = 50; var report = rules.FinishRun(false);
            Assert.IsTrue(rules.State.Equipped.All(x => x == null)); Assert.IsEmpty(rules.State.RigItems); Assert.IsEmpty(rules.State.PackItems);
            Assert.IsNull(rules.State.ActiveRun); Assert.AreEqual(0, rules.State.PocketCoins); Assert.AreEqual(12, rules.State.Warehouse[0].Count);
            Assert.AreEqual(600, rules.State.Coins); Assert.AreEqual(300, rules.State.Experience); Assert.AreEqual(3, rules.State.Quests[0].Progress);
            Assert.IsFalse(report.Extracted); Assert.IsNotEmpty(report.Items);
        }
        [Test] public void ExtractionRegistersExactItemsOnceAndRetainsOverflow()
        {
            string error; var id = catalog.All.First(x => x.Kind == ItemKind.Weapon && x.Tier == 1).Id;
            rules.State.PackItems.Add(catalog.Create(id, new Random(1))); rules.State.PackItems.Add(catalog.Create(id, new Random(2)));
            for (var i = 0; i < 48; i++) rules.State.Warehouse.Add(catalog.Create("eq_0001", new Random(i)));
            Assert.IsTrue(rules.BeginRun(0, 5, out error)); var report = rules.FinishRun(true);
            Assert.AreEqual(1, report.Unlocks.Count(x => x == id)); Assert.AreEqual(2, rules.State.CareerLevel); Assert.AreEqual(1, rules.State.SkillPoints);
            Assert.AreEqual(3, rules.State.PackItems.Count); Assert.IsTrue(rules.ShopVisible(catalog.Get(id)));
        }
        [Test] public void FullContainersAndInsufficientMoneyDoNotPartiallyMutate()
        {
            string error; rules.State.Coins = 0; var count = rules.State.PackItems.Count;
            Assert.IsFalse(rules.Buy("key", out error)); Assert.AreEqual(count, rules.State.PackItems.Count);
            rules.BeginRun(0, 10, out error); var weight = rules.Weight;
            Assert.IsFalse(rules.Pickup(new Item { Id = "ore_0", Count = 99 }, out error)); Assert.AreEqual(weight, rules.Weight);
        }
        [Test] public void EntireSkillTreeCosts95AndEnforcesPrerequisitesAndMaximums()
        {
            rules.State.SkillPoints = 100; string error;
            Assert.AreEqual(95, SkillTree.All.Sum(n => n.Maximum)); Assert.IsFalse(SkillTree.Learn(rules.State, "sword_skill1", out error));
            for (var i = 0; i < 3; i++) Assert.IsTrue(SkillTree.Learn(rules.State, "sword_damage", out error));
            Assert.IsFalse(SkillTree.Learn(rules.State, "sword_damage", out error)); Assert.IsTrue(SkillTree.Learn(rules.State, "sword_skill1", out error));
            Assert.AreEqual(96, rules.State.SkillPoints);
        }
        [Test] public void CraftingLegendaryConsumesMaterialsOnceAndHasThreeUniqueEnchants()
        {
            string error; var id = catalog.All.First(d => d.Kind == ItemKind.Weapon && d.Tier == 4).Id;
            rules.State.Warehouse.Add(new Item { Id = "ore_4", Count = 3 }); rules.State.Coins = 1000;
            Assert.IsTrue(Crafting.Craft(rules, id, 4, out error)); Assert.IsEmpty(rules.State.Warehouse);
            var item = rules.State.PackItems.First(i => i.Id == id); Assert.AreEqual(3, item.Enchants.Distinct().Count());
            var before = rules.State.Coins; Assert.IsFalse(Crafting.Craft(rules, id, 4, out error)); Assert.AreEqual(before, rules.State.Coins);
        }
        [Test] public void SetRequiresAllThreeMatchingWorkingParts()
        {
            var pieces = catalog.All.Where(d => d.Kind == ItemKind.Armor && d.Branch == "plate" && d.Tier == 0).Take(3).ToArray();
            foreach (var d in pieces) rules.State.Equipped[(int)d.Slot] = catalog.Create(d.Id, new Random(1));
            Assert.AreEqual(108, rules.MaximumHp, .01); rules.State.Equipped[(int)Slot.Legs].Durability = 0; Assert.AreEqual(100, rules.MaximumHp);
        }
        [Test] public void JsonRoundtripIncludesRunAndItemProperties()
        {
            string error; rules.BeginRun(0, 99, out error); var original = rules.State; var restored = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));
            Assert.AreEqual(5, restored.ActiveRun.Floors.Count); Assert.AreEqual(original.Equipped[0].Id, restored.Equipped[0].Id);
            Assert.AreEqual(original.RigItems[0].Count, restored.RigItems[0].Count); Assert.AreEqual(original.ActiveRun.Floors[1].ExtractRoom, restored.ActiveRun.Floors[1].ExtractRoom);
            Assert.IsNull(restored.Equipped[1]); Assert.IsNull(restored.Equipped[2]); Assert.IsNull(restored.Equipped[3]); Assert.IsNull(restored.LastReport);
            rules.FinishRun(false); restored = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(rules.State));
            Assert.IsNull(restored.ActiveRun); Assert.IsTrue(restored.Equipped.All(i => i == null));
        }
        [Test] public void ThousandDungeonsAreConnectedAndOnlyL2L4Extract()
        {
            for (var seed = 0; seed < 1000; seed++) foreach (var floor in DungeonGenerator.Generate(seed, seed % 5).Floors)
            {
                Assert.AreEqual(floor.Number == 2 || floor.Number == 4, floor.ExtractRoom >= 0);
                Assert.AreEqual(floor.Rooms.Count, floor.Rooms.Select(r => r.X + ":" + r.Y).Distinct().Count());
                for (var i = 1; i < floor.Rooms.Count; i++) Assert.That(floor.Rooms[i].Parent, Is.InRange(0, i - 1));
            }
        }
    }
}
