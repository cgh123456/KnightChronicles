using System;
using System.Collections.Generic;

namespace KnightChronicles.Runtime.Core
{
    public sealed class SkillNode
    {
        public string Id, Name, Branch, Prerequisite, Prerequisite2;
        public int Maximum = 1, Required = 1;
    }
    public static class SkillTree
    {
        public static readonly List<SkillNode> All = Build();
        private static List<SkillNode> Build()
        {
            var nodes = new List<SkillNode>();
            var branches = new[] { "blade", "sword", "spear", "dagger", "staff" };
            var names = new[] { "刀", "骑士剑", "长枪", "匕首", "法杖" };
            var first = new[] { "旋风斩", "格挡反击", "贯穿突刺", "影袭", "奥术洪流" };
            var second = new[] { "斩首姿态", "守护之壁", "横扫千军", "毒刃", "陨星术" };
            var ultimate = new[] { "血怒", "不屈", "破阵", "处决者", "元素共鸣" };
            for (var i = 0; i < 5; i++)
            {
                var b = branches[i];
                nodes.Add(new SkillNode { Id = b + "_damage", Name = names[i] + " · 伤害 +4%/点", Branch = names[i], Maximum = 3 });
                nodes.Add(new SkillNode { Id = b + "_mastery", Name = names[i] + " · " + (i == 3 ? "暴击 +3%/点" : i == 4 ? "蓄力速度 +6%/点" : "攻速 +3%/点"), Branch = names[i], Maximum = 2 });
                nodes.Add(new SkillNode { Id = b + "_skill1", Name = "解锁 " + first[i] + "（Q）", Branch = names[i], Prerequisite = b + "_damage", Required = 3 });
                nodes.Add(new SkillNode { Id = b + "_skill2", Name = "解锁 " + second[i] + "（R）", Branch = names[i], Prerequisite = b + "_mastery", Required = 2 });
                nodes.Add(new SkillNode { Id = b + "_enhance1", Name = first[i] + " 强化：效果 +30% / CD −15%", Branch = names[i], Prerequisite = b + "_skill1" });
                nodes.Add(new SkillNode { Id = b + "_enhance2", Name = second[i] + " 强化：效果 +30% / CD −15%", Branch = names[i], Prerequisite = b + "_skill2" });
                nodes.Add(new SkillNode { Id = b + "_ultimate", Name = "终极 · " + ultimate[i], Branch = names[i], Prerequisite = b + "_enhance1", Prerequisite2 = b + "_enhance2" });
            }
            var survival = new[] { "hp", "defense", "speed", "potion", "mp", "wear" };
            var small = new[] { "体魄：生命 +8%/点", "铁骨：防御 +5%/点", "轻步：移速 +2%/点", "炼金：药剂 +8%/点", "蓝血：蓝量 +10%/点", "匠心：磨损 −10%/点" };
            var passives = new[] { "韧性", "再生", "脱身", "炼金胃", "冥想", "巧手" };
            for (var i = 0; i < 6; i++)
            {
                var b = "survival_" + survival[i];
                nodes.Add(new SkillNode { Id = b + "_small", Name = small[i], Branch = "生存", Maximum = 2 });
                nodes.Add(new SkillNode { Id = b + "_medium", Name = "解锁 " + passives[i], Branch = "生存", Prerequisite = b + "_small", Required = 2 });
                nodes.Add(new SkillNode { Id = b + "_large", Name = "强化 " + passives[i], Branch = "生存", Prerequisite = b + "_medium" });
            }
            var exploration = new[] { "quality", "sell", "hidden", "search", "value", "trace", "empty", "double", "map", "exit", "discount", "key", "depth" };
            var descriptions = new[] { "寻宝者：高档掉落 +5%/级", "商魂：售价 +3%/级", "密室嗅觉：密室权重 +15%/级", "搜刮直觉：搜索速度 +25%", "矿脉感知：高价值 +8%", "留痕：标记两个搜索点", "拾荒者：出空率 −5%/级", "宝藏猎人：双倍产出 +5%/级", "地图师：揭示相邻房间", "撤离本能：显示撤离方向", "精打细算：购买 −5%/级", "钥匙匠：20% 不消耗", "深入险境：层深品质 +3%/层" };
            var maximums = new[] { 3, 3, 2, 1, 1, 1, 2, 2, 1, 1, 2, 1, 1 };
            for (var i = 0; i < exploration.Length; i++) nodes.Add(new SkillNode { Id = "explore_" + exploration[i], Name = descriptions[i], Branch = "探索", Maximum = maximums[i] });
            return nodes;
        }
        public static bool CanLearn(Profile state, SkillNode node)
        {
            return state.ActiveRun == null && state.SkillPoints > 0 && state.Skill(node.Id) < node.Maximum
                && (node.Prerequisite == null || state.Skill(node.Prerequisite) >= node.Required)
                && (node.Prerequisite2 == null || state.Skill(node.Prerequisite2) >= 1);
        }
        public static bool Learn(Profile state, string id, out string error)
        {
            error = null; var node = All.Find(n => n.Id == id);
            if (node == null || !CanLearn(state, node)) { error = "技能点不足、前置未满足或已达上限"; return false; }
            var rank = state.Skills.Find(s => s.Id == id); if (rank == null) { rank = new SkillRank { Id = id }; state.Skills.Add(rank); }
            rank.Rank++; state.SkillPoints--; return true;
        }
    }
    public static class Crafting
    {
        public static bool Craft(GameRules rules, string id, int materialTier, out string error)
        {
            error = null; var d = rules.Catalog.Get(id); var state = rules.State;
            if (state.ActiveRun != null || d.Kind > ItemKind.Armor || d.Tier != materialTier || materialTier < 0 || materialTier > 4)
            { error = "配方档位必须与最低素材档位一致"; return false; }
            var ore = "ore_" + materialTier; var count = 0;
            foreach (var i in state.Warehouse) if (i.Id == ore) count += i.Count;
            var cost = (int)Math.Ceiling(d.Price * 0.5f);
            if (count < 3 || state.Coins < cost) { error = "需要同档矿石 ×3 与 " + cost + " 金币（素材须在仓库）"; return false; }
            var item = rules.Catalog.Create(id, new Random());
            if (!rules.Pickup(item, out error)) return false;
            var remaining = 3;
            for (var i = state.Warehouse.Count - 1; i >= 0 && remaining > 0; i--)
            { var mat = state.Warehouse[i]; if (mat.Id != ore) continue; var amount = Math.Min(mat.Count, remaining); mat.Count -= amount; remaining -= amount; if (mat.Count == 0) state.Warehouse.RemoveAt(i); }
            state.Coins -= cost; state.CraftCount++; rules.QuestEvent("craft", 1, false); return true;
        }
    }
    public static class Story
    {
        public static readonly string[][] Chapters = {
            new[] { "第一章 · 地下的回声", "公会长：旧王朝的地窖再次传来钟声。带回遗物的人说，他们听见了自己的名字。", "公会长：先从浅层开始调查。记住，勇气不是走到最深处，而是带着答案回来。", "你接过公会徽章，踏上地下城的阶梯。" },
            new[] { "第二章 · 失落的誓言", "公会长：你带回的遗物刻着守墓骑士的誓言。他们守护的不是黄金，是一扇不能打开的门。", "公会长：钟声越来越近。深入调查，但别忘了为自己留下退路。", "你把誓言抄在披风内侧，决定寻找钟声的源头。" },
            new[] { "第三章 · 最后的火种", "公会长：守墓者早已忘记为何而战。宝藏层的灯火，是他们留下的最后记忆。", "公会长：你不必继承他们的囚笼。带回真相，让小镇的人替他们记住那段岁月。", "公会为你点亮一盏灯。地下城仍会变化，但归途已有名字。" }
        };
        public static bool Advance(Profile state, out string error)
        {
            error = null;
            if (state.ActiveRun != null || state.Chapter >= 3 || state.Rank < state.Chapter) { error = "需要更高冒险者等级，或剧情已完成"; return false; }
            state.Chapter++; return true;
        }
    }
}
