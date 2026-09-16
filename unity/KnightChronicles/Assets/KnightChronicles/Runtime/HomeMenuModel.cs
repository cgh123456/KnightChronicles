using System.Collections.Generic;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// 由档案状态推导 FR-1102 ④ 的一级入口清单、可见性与默认焦点（R-1102-1 / R-1102-9）。
    /// 纯数据模型，便于 EditMode 验收测试；UI 层只负责渲染与点击转发。
    /// </summary>
    public sealed class HomeMenuModel
    {
        public sealed class Entry
        {
            public string Id;
            public string Title;
            public string Subtitle;

            /// <summary>红点提示（局外成长 / 图鉴）。</summary>
            public bool ShowRedDot;

            /// <summary>附加徽标文案，如「推荐查看」；null 表示不显示。</summary>
            public string Badge;

            /// <summary>危险操作（退出游戏），使用警示色。</summary>
            public bool IsDestructive;
        }

        public readonly IReadOnlyList<Entry> Entries;

        /// <summary>R-1102-1：有有效 activeRun 时为「继续游戏」，否则固定为「开始远征」。</summary>
        public readonly int DefaultFocusIndex;

        public HomeMenuModel(HomeProfileService service)
        {
            var entries = new List<Entry>();

            if (service.ActiveRun != null)
            {
                entries.Add(new Entry
                {
                    Id = "continue",
                    Title = "继续游戏",
                    Subtitle = service.ActiveRun.Summary,
                });
            }

            entries.Add(new Entry
            {
                Id = "start",
                Title = service.FirstRun ? "开始新游戏" : "开始远征",
                Subtitle = "选择角色与出战武器",
            });
            entries.Add(new Entry
            {
                Id = "meta",
                Title = "局外成长",
                Subtitle = "解锁角色、武器与概率成长",
                ShowRedDot = service.HasAffordableMetaUnlock,
            });
            entries.Add(new Entry
            {
                Id = "archive",
                Title = "图鉴与档案",
                Subtitle = "查看发现、记录与成就",
                ShowRedDot = service.HasUnseenArchiveEntries,
            });
            entries.Add(new Entry
            {
                Id = "settings",
                Title = "设置",
                Subtitle = "画面、音频与操作",
            });
            entries.Add(new Entry
            {
                Id = "help",
                Title = "帮助",
                Subtitle = "了解远征规则",
                Badge = service.ShowHelpRecommendation ? "推荐查看" : null,
            });
            entries.Add(new Entry
            {
                Id = "exit",
                Title = "退出游戏",
                Subtitle = "保存局外进度后退出",
                IsDestructive = true,
            });

            Entries = entries;
            DefaultFocusIndex = service.ActiveRun != null ? IndexOf("continue") : IndexOf("start");
        }

        public int IndexOf(string id)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
