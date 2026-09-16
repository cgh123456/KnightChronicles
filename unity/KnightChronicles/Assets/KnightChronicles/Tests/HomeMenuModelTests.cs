using NUnit.Framework;
using KnightChronicles.Runtime;

namespace KnightChronicles.Tests
{
    /// <summary>
    /// FR-1102 主菜单的入口清单与焦点规则验收（EditMode，无需 Play Mode）。
    /// </summary>
    public sealed class HomeMenuModelTests
    {
        private static HomeProfileService NewService(HomeProfileService.RunSnapshot run)
        {
            return new HomeProfileService(1240, run, null);
        }

        [Test]
        public void WithoutActiveRun_DefaultFocusIsStartExpedition()
        {
            // TC-1102-1：无有效 activeRun，默认焦点位于「开始远征」。
            var model = new HomeMenuModel(NewService(null));
            Assert.AreEqual(0, model.DefaultFocusIndex);
            Assert.AreEqual("start", model.Entries[model.DefaultFocusIndex].Id);
        }

        [Test]
        public void WithActiveRun_ContinueGameIsFirstAndFocused()
        {
            // TC-1102-2 / R-1102-9：仅存在有效快照时提供「继续游戏」，且为默认焦点。
            var model = new HomeMenuModel(NewService(new HomeProfileService.RunSnapshot { Summary = "灰烬圣堂 · 第 2 层" }));
            Assert.AreEqual("continue", model.Entries[0].Id);
            Assert.AreEqual(0, model.DefaultFocusIndex);
        }

        [Test]
        public void AfterAbandonRun_ContinueEntryDisappearsAndFocusFallsToStart()
        {
            // TC-1102-15：确认后快照删除，之后不再显示「继续游戏」。
            var service = NewService(new HomeProfileService.RunSnapshot { Summary = "灰烬圣堂" });
            service.AbandonActiveRun();
            var model = new HomeMenuModel(service);
            Assert.AreEqual(-1, model.IndexOf("continue"));
            Assert.AreEqual("start", model.Entries[model.DefaultFocusIndex].Id);
        }

        [Test]
        public void FirstRun_StartEntryTitleIsStartNewGame()
        {
            var service = NewService(null);
            service.FirstRun = true;
            var model = new HomeMenuModel(service);
            Assert.AreEqual("开始新游戏", model.Entries[model.IndexOf("start")].Title);
        }

        [Test]
        public void AffordableMetaUnlock_ShowsRedDotOnMetaEntry()
        {
            // TC-1102-5：存在可解锁且宝石足够的项时挂红点。
            var service = NewService(null);
            service.HasAffordableMetaUnlock = true;
            var model = new HomeMenuModel(service);
            Assert.IsTrue(model.Entries[model.IndexOf("meta")].ShowRedDot);
            Assert.IsFalse(model.Entries[model.IndexOf("archive")].ShowRedDot);
        }

        [Test]
        public void UnseenArchiveEntries_ShowsRedDotOnArchiveEntry()
        {
            var service = NewService(null);
            service.HasUnseenArchiveEntries = true;
            var model = new HomeMenuModel(service);
            Assert.IsTrue(model.Entries[model.IndexOf("archive")].ShowRedDot);
        }

        [Test]
        public void HelpBadge_ShowsWithinThreeLaunchesThenDisappears()
        {
            var service = NewService(null);
            service.LaunchesSinceFirstRun = 3;
            var shown = new HomeMenuModel(service);
            Assert.AreEqual("推荐查看", shown.Entries[shown.IndexOf("help")].Badge);

            service.LaunchesSinceFirstRun = 4;
            var hidden = new HomeMenuModel(service);
            Assert.IsNull(hidden.Entries[hidden.IndexOf("help")].Badge);
        }

        [Test]
        public void WithoutRun_EntriesContainAllSixSpecEntries()
        {
            // 入口清单④：开始远征、局外成长、图鉴与档案、设置、帮助、退出游戏。
            var model = new HomeMenuModel(NewService(null));
            CollectionAssert.AreEquivalent(
                new[] { "start", "meta", "archive", "settings", "help", "exit" },
                System.Linq.Enumerable.Select(model.Entries, e => e.Id));
        }
    }

    /// <summary>
    /// HomeProfileService 的事件与状态迁移验收。
    /// </summary>
    public sealed class HomeProfileServiceTests
    {
        [Test]
        public void SetGems_RaisesGemsChangedOncePerChange()
        {
            var service = new HomeProfileService(100, null, null);
            var changes = 0;
            service.GemsChanged += () => changes++;

            service.SetGems(150);
            service.SetGems(150);  // 同值不触发
            Assert.AreEqual(1, changes);
            Assert.AreEqual(150, service.Gems);
        }

        [Test]
        public void AbandonActiveRun_ClearsRunAndRaisesStateChanged()
        {
            var service = new HomeProfileService(0, new HomeProfileService.RunSnapshot { Summary = "灰烬圣堂" }, null);
            var changes = 0;
            service.StateChanged += () => changes++;

            service.AbandonActiveRun();
            service.AbandonActiveRun();  // 幂等
            Assert.IsNull(service.ActiveRun);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SaveFailed_FlowsThroughRetry()
        {
            var service = new HomeProfileService(0, null, null);
            var changes = 0;
            service.StateChanged += () => changes++;

            Assert.IsFalse(service.SaveFailed);
            service.MarkSaveFailed();
            service.MarkSaveFailed();  // 幂等
            Assert.IsTrue(service.SaveFailed);
            service.RetrySave();
            Assert.IsFalse(service.SaveFailed);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void InvalidateActiveRun_RaisesReasonOnce()
        {
            var service = new HomeProfileService(0, new HomeProfileService.RunSnapshot { Summary = "灰烬圣堂" }, null);
            string reason = null;
            service.RunInvalidated += r => reason = r;

            service.InvalidateActiveRun("未完成远征无法恢复，已保留局外进度");
            Assert.IsNull(service.ActiveRun);
            Assert.AreEqual("未完成远征无法恢复，已保留局外进度", reason);

            reason = null;
            service.InvalidateActiveRun("再次调用不应触发");
            Assert.IsNull(reason);
        }
    }
}
