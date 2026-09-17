using System;
using System.Collections.Generic;

namespace KnightChronicles.Runtime.Core
{
    [Serializable] public sealed class Room
    {
        public int X, Y, Parent = -1;
        public bool Hidden, Opened, Visited;
    }
    [Serializable] public sealed class SearchPoint
    {
        public int Room, Type;
        public float X, Y;
        public bool Searched;
    }
    [Serializable] public sealed class EnemyState
    {
        public int Room, Type;
        public float X, Y, Hp;
        public bool Dead;
    }
    [Serializable] public sealed class GroundItem
    {
        public float X, Y;
        public Item Item;
    }
    [Serializable] public sealed class FloorData
    {
        public int Number, DownRoom, ExtractRoom = -1;
        public List<Room> Rooms = new List<Room>();
        public List<SearchPoint> Searches = new List<SearchPoint>();
        public List<EnemyState> Enemies = new List<EnemyState>();
        public List<GroundItem> Drops = new List<GroundItem>();
    }
    [Serializable] public sealed class RunData
    {
        public int Seed, Difficulty, Floor = 1, Kills, RollCounter;
        public float Hp = 100, Mp = 50, X, Y;
        public List<FloorData> Floors = new List<FloorData>();
        public List<SkillRank> GenerationSkills = new List<SkillRank>();
        public int GenerationSkill(string id) { var n = GenerationSkills.Find(s => s.Id == id); return n == null ? 0 : n.Rank; }
    }

    public static class DungeonGenerator
    {
        public const float Spacing = 16, RoomSize = 12;
        public static RunData Generate(int seed, int difficulty, int hiddenSkill = 0)
        {
            if (difficulty < 0 || difficulty > 4) throw new ArgumentOutOfRangeException("difficulty");
            var random = new Random(seed);
            var run = new RunData { Seed = seed, Difficulty = difficulty };
            for (var floor = 1; floor <= 5; floor++)
            {
                var data = new FloorData { Number = floor };
                var count = random.Next(7, 11);
                data.Rooms.Add(new Room { X = 0, Y = 0, Opened = true });
                // A self avoiding main path exceeds 60% of rooms and guarantees stairs/extraction reachability.
                for (var i = 1; i < count; i++)
                {
                    int x, y;
                    var previous = data.Rooms[i - 1];
                    do { x = previous.X + (random.Next(2) == 0 ? 1 : 0); y = previous.Y + (x == previous.X ? 1 : 0); }
                    while (data.Rooms.Exists(r => r.X == x && r.Y == y));
                    data.Rooms.Add(new Room { X = x, Y = y, Parent = i - 1, Opened = true });
                }
                data.DownRoom = count - 1;
                if (floor == 2 || floor == 4) data.ExtractRoom = count / 2;
                var hidden = Math.Min(2, random.Next(3) + (random.NextDouble() < hiddenSkill * 0.15 ? 1 : 0));
                for (var i = 0; i < hidden; i++)
                {
                    var parent = random.Next(count);
                    var p = data.Rooms[parent];
                    var x = p.X - 1; var y = p.Y;
                    if (data.Rooms.Exists(r => r.X == x && r.Y == y)) { x = p.X; y = p.Y - 1; }
                    if (!data.Rooms.Exists(r => r.X == x && r.Y == y))
                        data.Rooms.Add(new Room { X = x, Y = y, Parent = parent, Hidden = true });
                }
                var searchCount = random.Next(8, 16);
                for (var i = 0; i < searchCount; i++) AddSearch(data, random, random.Next(count), floor);
                for (var i = count; i < data.Rooms.Count; i++)
                { var points = random.Next(2, 5); for (var j = 0; j < points; j++) AddSearch(data, random, i, 5); }
                for (var i = 1; i < count; i++)
                {
                    var room = data.Rooms[i];
                    var enemies = i == count - 1 && floor == 5 ? 1 : random.Next(1, 3);
                    for (var j = 0; j < enemies; j++)
                    {
                        var type = floor == 5 && i == count - 1 ? 3 : random.Next(i < 3 ? 2 : 3);
                        data.Enemies.Add(new EnemyState { Room = i, Type = type, X = room.X * Spacing + j * 2 - 1,
                            Y = room.Y * Spacing, Hp = (type == 3 ? 180 : 24 + type * 12) * DifficultyMultiplier(difficulty) });
                    }
                }
                run.Floors.Add(data);
            }
            run.Floors[0].Rooms[0].Visited = true;
            return run;
        }
        private static void AddSearch(FloorData floor, Random r, int room, int depth)
        {
            var p = floor.Rooms[room];
            floor.Searches.Add(new SearchPoint { Room = room, Type = r.Next(depth == 5 ? 2 : 0, 6),
                X = p.X * Spacing + r.Next(-4, 5), Y = p.Y * Spacing + r.Next(-4, 5) });
        }
        public static float DifficultyMultiplier(int d) { return new[] { 1f, 1.5f, 2.2f, 3.2f, 4.5f }[d]; }
        public static bool Walkable(FloorData floor, float x, float y)
        {
            foreach (var r in floor.Rooms)
            {
                if (r.Opened && Math.Abs(x - r.X * Spacing) < RoomSize / 2 - 0.4f && Math.Abs(y - r.Y * Spacing) < RoomSize / 2 - 0.4f) return true;
                if (r.Parent < 0 || !r.Opened) continue;
                var p = floor.Rooms[r.Parent];
                var ax = r.X * Spacing; var ay = r.Y * Spacing; var bx = p.X * Spacing; var by = p.Y * Spacing;
                if (Math.Abs(ay - by) < 0.1f && x >= Math.Min(ax, bx) && x <= Math.Max(ax, bx) && Math.Abs(y - ay) < 1.6f) return true;
                if (Math.Abs(ax - bx) < 0.1f && y >= Math.Min(ay, by) && y <= Math.Max(ay, by) && Math.Abs(x - ax) < 1.6f) return true;
            }
            return false;
        }
    }
}
