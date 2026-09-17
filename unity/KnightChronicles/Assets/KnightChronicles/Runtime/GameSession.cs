using System;
using System.IO;
using KnightChronicles.Runtime.Core;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    public static class GameSession
    {
        private static GameRules rules;
        private static AtomicSaveStore store;
        public static string SaveError { get; private set; }
        public static string Notice { get; private set; }
        public static string EntryPage;
        public static GameRules Rules
        {
            get { if (rules == null) Load(); return rules; }
        }
        public static Profile State { get { return Rules.State; } }
        public static void BeginIsolatedTestSession(string directory)
        {
            var catalog = Catalog.Parse(Resources.Load<TextAsset>("Data/items").text);
            rules = new GameRules(catalog, GameRules.NewProfile(catalog));
            store = new AtomicSaveStore(Path.Combine(directory, "knight-test.save"));
            SaveError = null; Notice = null; EntryPage = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { rules = null; store = null; SaveError = null; Notice = null; EntryPage = null; }
        private static void Load()
        {
            var asset = Resources.Load<TextAsset>("Data/items");
            if (asset == null) throw new InvalidOperationException("缺少物品配置 Data/items");
            var catalog = Catalog.Parse(asset.text);
            store = new AtomicSaveStore(Path.Combine(Application.persistentDataPath, "knight-profile.save"));
            Profile profile = null;
            try
            {
                bool backup; var json = store.Read(out backup);
                if (json != null) profile = ProfileSerializer.Deserialize(json);
                if (backup) Notice = "存档损坏，已恢复上次备份。";
                if (profile != null && (profile.Version != 1 || profile.Equipped == null || profile.Equipped.Length != 6)) throw new FormatException("不支持的存档格式");
            }
            catch (Exception e) { SaveError = "存档无法读取，请保留存档并重试：" + e.Message; }
            rules = new GameRules(catalog, profile ?? GameRules.NewProfile(catalog));
            // Never overwrite an unreadable save automatically.
        }
        public static bool Save()
        {
            if (SaveError != null && SaveError.StartsWith("存档无法读取")) return false;
            try { store.Write(ProfileSerializer.Serialize(State)); SaveError = null; return true; }
            catch (Exception e) { SaveError = "保存失败，请重试：" + e.Message; return false; }
        }
        public static bool Commit(Func<GameRules, bool> action, bool checkpoint = false)
        {
            var before = ProfileSerializer.Serialize(State);
            var wasRunning = State.ActiveRun != null;
            if (!action(Rules)) return false;
            if (wasRunning && State.ActiveRun != null && !checkpoint) return true;
            if (Save()) return true;
            rules.State = ProfileSerializer.Deserialize(before); return false;
        }
        public static bool Finish(bool extract)
        {
            return Commit(r => { r.FinishRun(extract); return true; }, true);
        }
        public static bool RestoreCheckpoint()
        {
            try
            {
                bool backup; var json = store.Read(out backup);
                if (json == null) throw new IOException("没有可恢复的安全点");
                var restored = ProfileSerializer.Deserialize(json);
                if (restored == null || restored.ActiveRun == null) throw new FormatException("安全点无效");
                rules.State = restored; SaveError = null; return true;
            }
            catch (Exception e) { SaveError = "无法恢复安全点：" + e.Message; return false; }
        }
    }
}

