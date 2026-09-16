using System;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 首页的单一数据来源（FR-1102 R-1102-6）：宝石、档案统计、最近战绩与未完成远征
    /// 全部由本服务持有，UI 只读取与订阅事件，不做累加。
    /// 当前为 MVP 内存实现；接入 FR-901 存档服务时保持事件接口不变，UI 无需改动。
    /// </summary>
    public sealed class HomeProfileService
    {
        /// <summary>未完成远征的安全点快照（R-1102-9：仅存在有效快照时提供「继续游戏」）。</summary>
        public sealed class RunSnapshot
        {
            public string Summary;
        }

        /// <summary>最近一局战绩；<c>null</c> 表示尚无远征记录（TC-1102-3）。</summary>
        public sealed class RecordSnapshot
        {
            public bool Victory;
            public string ThemeLabel;
            public int Kills;
            public string Duration;
            public int GemsEarned;
        }

        public string PlayerName = "艾尔文";
        public string PlaytimeText = "累计 18小时42分";
        public int ExpeditionCount = 27;

        public int Gems { get; private set; }
        public RunSnapshot ActiveRun { get; private set; }
        public RecordSnapshot LastRecord { get; private set; }

        /// <summary>首次运行：开始远征文案变为「开始新游戏」（入口清单④）。</summary>
        public bool FirstRun;

        /// <summary>存在可解锁且宝石足够的局外成长项（红点，TC-1102-5）。</summary>
        public bool HasAffordableMetaUnlock;

        /// <summary>图鉴有新解锁未查看条目（红点）。</summary>
        public bool HasUnseenArchiveEntries;

        /// <summary>首次运行后的启动次数；≤ 3 时帮助挂「推荐查看」标记。</summary>
        public int LaunchesSinceFirstRun;

        /// <summary>存档写入失败（SaveFailed 状态，TC-1102-10）。</summary>
        public bool SaveFailed { get; private set; }

        /// <summary>宝石余额变化（R-1102-8：结算返回后刷新余额）。</summary>
        public event Action GemsChanged;

        /// <summary>远征快照、存档失败等页面级状态变化。</summary>
        public event Action StateChanged;

        /// <summary>快照无效被清除时的非阻断提示原因（R-1102-9）。</summary>
        public event Action<string> RunInvalidated;

        public HomeProfileService() : this(0, null, null) { }

        public HomeProfileService(int gems, RunSnapshot activeRun, RecordSnapshot lastRecord)
        {
            Gems = gems;
            ActiveRun = activeRun;
            LastRecord = lastRecord;
        }

        public bool ShowHelpRecommendation => LaunchesSinceFirstRun <= 3;

        public void SetGems(int value)
        {
            if (value == Gems)
            {
                return;
            }

            Gems = value;
            GemsChanged?.Invoke();
        }

        /// <summary>R-1102-10：确认放弃后原子删除 activeRun，之后不再显示「继续游戏」。</summary>
        public void AbandonActiveRun()
        {
            if (ActiveRun == null)
            {
                return;
            }

            ActiveRun = null;
            StateChanged?.Invoke();
        }

        /// <summary>R-1102-9：快照读取或重建失败时删除快照并给出一次非阻断提示原因。</summary>
        public void InvalidateActiveRun(string reason)
        {
            if (ActiveRun == null)
            {
                return;
            }

            ActiveRun = null;
            RunInvalidated?.Invoke(reason);
            StateChanged?.Invoke();
        }

        public void MarkSaveFailed()
        {
            if (SaveFailed)
            {
                return;
            }

            SaveFailed = true;
            StateChanged?.Invoke();
        }

        /// <summary>「重试」写档；MVP 阶段重试恒成功，正式实现接入 FR-901 落盘结果。</summary>
        public void RetrySave()
        {
            if (!SaveFailed)
            {
                return;
            }

            SaveFailed = false;
            StateChanged?.Invoke();
        }
    }
}
