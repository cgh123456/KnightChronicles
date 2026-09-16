using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnightChronicles.Runtime
{
    /// <summary>
    /// FR-1102 开始页面（主菜单）的运行时 UGUI 组装器。
    /// 页面状态（Normal/Loading/SaveFailed/Modal/Transitioning）、入口清单与焦点规则依据 SRS §3.11；
    /// 选角、成长中心等目标页由后续模块接入，入口先以 Toast 标注路由，不假装已实现。
    /// </summary>
    public sealed class HomeMenuController : MonoBehaviour
    {
        // 序列化字段由 Inspector 赋值，编译器视为未赋值（CS0649）。
#pragma warning disable 0649
        [Header("预览状态（Inspector 控制；正式接入 FR-901 存档后移除）")]
        [SerializeField] private bool hasActiveRun;
        [SerializeField] private string activeRunSummary = "灰烬圣堂 · 第 2 层 · 最近安全点";
        [SerializeField] private int gems = 1240;
        [SerializeField] private bool previewNoHistory;
        [SerializeField] private bool previewFirstRun;
        [SerializeField] private bool previewAffordableMetaUnlock = true;
        [SerializeField] private bool previewUnseenArchive;
        [SerializeField] private bool simulateSaveFailure;
#pragma warning restore 0649

        private static readonly Color Ink = new Color32(244, 247, 251, 255);
        private static readonly Color Muted = new Color32(185, 199, 216, 255);
        private static readonly Color Gold = new Color32(214, 162, 74, 255);
        private static readonly Color Cyan = new Color32(73, 215, 232, 255);
        private static readonly Color Danger = new Color32(225, 115, 103, 255);

        private const float GemRollSeconds = 0.45f;     // R-1102：余额滚动动画 ≤ 0.5 s
        private const float TransitionSeconds = 0.16f;  // P-5：页面切换锁定窗口

        private HomeProfileService _service;
        private HomeMenuModel _model;
        private readonly List<Button> _menuButtons = new List<Button>();
        private Button _switchCharacterButton;

        private GameObject canvasRoot;
        private GameObject menuListRoot;
        private Text profileText;
        private Text gemsText;
        private GameObject recordSkeleton;
        private readonly List<Image> skeletonBars = new List<Image>();
        private Text recordEmptyText;
        private Text recordText;
        private GameObject recordDataRoot;
        private GameObject saveWarning;
        private GameObject modal;
        private Text modalTitle;
        private Text modalBody;
        private Button modalCancel;
        private Button modalConfirm;
        private GameObject toastPanel;
        private Text toastText;

        private bool _loading = true;
        private bool _locked;
        private int _displayedGems;
        private Coroutine _gemRoll;
        private Coroutine _skeletonPulse;

        private void Awake()
        {
            BuildService();
            BuildHome();
            RefreshTopBar();
            ShowRecordSkeleton();
            RebuildMenu();
        }

        private IEnumerator Start()
        {
            // R-1102-5：进入主菜单异步读取存档统计，完成前战绩卡显示骨架屏，不显示 0。
            yield return new WaitForSeconds(0.6f);
            _loading = false;
            RefreshRecordCard();
            if (simulateSaveFailure)
            {
                _service.MarkSaveFailed();  // TC-1102-10 的演示入口
            }
            RefreshSaveWarning();
        }

        private void OnDestroy()
        {
            if (_service == null)
            {
                return;
            }

            _service.GemsChanged -= OnGemsChanged;
            _service.StateChanged -= OnStateChanged;
        }

        private void BuildService()
        {
            _service = new HomeProfileService(
                gems,
                hasActiveRun ? new HomeProfileService.RunSnapshot { Summary = activeRunSummary } : null,
                previewNoHistory
                    ? null
                    : new HomeProfileService.RecordSnapshot
                    {
                        Victory = true,
                        ThemeLabel = "第 3 主题",
                        Kills = 184,
                        Duration = "28:41",
                        GemsEarned = 156,
                    });
            _service.FirstRun = previewFirstRun;
            _service.HasAffordableMetaUnlock = previewAffordableMetaUnlock;
            _service.HasUnseenArchiveEntries = previewUnseenArchive;
            _service.GemsChanged += OnGemsChanged;
            _service.StateChanged += OnStateChanged;
            _service.RunInvalidated += reason => ShowToast(reason);
            _displayedGems = _service.Gems;
        }

        private void BuildHome()
        {
            EnsureEventSystem();

            canvasRoot = new GameObject("HomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CreateBackground(canvasRoot.transform);
            CreateTopBar(canvasRoot.transform);
            CreateCharacterShowcase(canvasRoot.transform);
            CreateMenu(canvasRoot.transform);
            CreateFooterHints(canvasRoot.transform);
            CreateModal(canvasRoot.transform);
            CreateToast(canvasRoot.transform);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void Update()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (modal.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseModal();  // Esc 等同取消（P-6：默认焦点已在「取消」上）
                }
                return;
            }

            if (eventSystem.currentSelectedGameObject == null)
            {
                RestoreMenuSelection();  // 鼠标点空处后焦点回默认入口，键盘导航始终可用
            }

            // R-1102-3：←→ 在「角色展示区」与「按钮组」之间切换焦点域。
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                var current = eventSystem.currentSelectedGameObject;
                var inMenu = current != null && current.transform.IsChildOf(menuListRoot.transform);
                if (inMenu)
                {
                    eventSystem.SetSelectedGameObject(_switchCharacterButton.gameObject);
                    ShowToast("焦点区域：角色展示区");
                }
                else
                {
                    RestoreMenuSelection();
                    ShowToast("焦点区域：游戏入口");
                }
                return;
            }

            // 主菜单没有上级页面，Esc 对应退出确认。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ActivateEntry(_model.Entries[_model.IndexOf("exit")]);
            }
        }

        // ---------- UI 构建 ----------

        private void CreateBackground(Transform parent)
        {
            // B 路线：大厅背景由 HomeLobby2D 的 sprite 承担，UGUI 只做压暗与可读性遮罩。
            var background = CreateImage("HomeDimmer", parent, new Color(0.01f, 0.03f, 0.08f, 0.10f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var leftShade = CreateImage("LeftReadabilityShade", parent, new Color(0.02f, 0.04f, 0.09f, 0.22f));
            Stretch(leftShade.rectTransform, Vector2.zero, new Vector2(0.48f, 1f), Vector2.zero, Vector2.zero);

            var rightShade = CreateImage("MenuReadabilityShade", parent, new Color(0.025f, 0.055f, 0.12f, 0.78f));
            Stretch(rightShade.rectTransform, new Vector2(0.53f, 0.08f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero);
        }

        private void CreateTopBar(Transform parent)
        {
            saveWarning = new GameObject("SaveWarning", typeof(Image));
            saveWarning.transform.SetParent(parent, false);
            var warningImage = saveWarning.GetComponent<Image>();
            warningImage.color = new Color(0.29f, 0.09f, 0.10f, 0.96f);
            Stretch(warningImage.rectTransform, new Vector2(0.34f, 0.965f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
            var warningText = CreateText("Text", saveWarning.transform, "进度未能保存", 17, new Color32(255, 216, 208, 255), TextAnchor.MiddleCenter);
            Stretch(warningText.rectTransform, new Vector2(0.06f, 0f), new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero);
            var retry = CreateSimpleButton(saveWarning.transform, "重试", new Vector2(0.66f, 0.14f), new Vector2(0.94f, 0.86f), new Color(0.45f, 0.2f, 0.16f, 1f));
            retry.onClick.AddListener(RetrySave);
            saveWarning.SetActive(false);

            profileText = CreateText("Profile", parent, string.Empty, 22, Ink, TextAnchor.UpperLeft);
            Stretch(profileText.rectTransform, new Vector2(0.05f, 0.91f), new Vector2(0.36f, 0.98f), Vector2.zero, Vector2.zero);

            var title = CreateText("GameTitle", parent, "✦  骑士异闻录  ✦\nCHRONICLES OF THE KNIGHT", 36, Ink, TextAnchor.UpperCenter);
            Stretch(title.rectTransform, new Vector2(0.35f, 0.90f), new Vector2(0.65f, 0.98f), Vector2.zero, Vector2.zero);
            title.lineSpacing = 0.8f;

            gemsText = CreateText("GemBalance", parent, string.Empty, 25, Cyan, TextAnchor.UpperRight);
            Stretch(gemsText.rectTransform, new Vector2(0.72f, 0.91f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);
        }

        private void CreateCharacterShowcase(Transform parent)
        {
            var frame = CreateImage("CharacterShowcase", parent, new Color(0.035f, 0.08f, 0.15f, 0.10f));
            Stretch(frame.rectTransform, new Vector2(0.05f, 0.18f), new Vector2(0.46f, 0.79f), Vector2.zero, Vector2.zero);

            var pedestal = CreateImage("CharacterPedestalMarker", frame.transform, new Color(0.11f, 0.3f, 0.42f, 0.38f));
            Stretch(pedestal.rectTransform, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.29f), Vector2.zero, Vector2.zero);

            // R-1102-4：角色展示由当前出战配置决定，任何按钮悬停都不联动本区域。
            var placeholder = CreateText("CharacterDisplayLabel", frame.transform, "骑士 · 均衡型\n晨星长剑", 28, Ink, TextAnchor.LowerCenter);
            Stretch(placeholder.rectTransform, new Vector2(0.15f, 0.26f), new Vector2(0.85f, 0.4f), Vector2.zero, Vector2.zero);

            var copy = CreateText("CharacterDescription", parent, "持盾而行，于黯夜中守望最后的火种。", 19, Muted, TextAnchor.MiddleLeft);
            Stretch(copy.rectTransform, new Vector2(0.07f, 0.10f), new Vector2(0.43f, 0.155f), Vector2.zero, Vector2.zero);

            _switchCharacterButton = CreateSimpleButton(parent, "更换角色 →", new Vector2(0.07f, 0.165f), new Vector2(0.21f, 0.215f), new Color(0.15f, 0.23f, 0.31f, 0.98f));
            _switchCharacterButton.onClick.AddListener(SwitchCharacter);
        }

        private void CreateMenu(Transform parent)
        {
            var heading = CreateText("MenuHeading", parent, "远征中枢\n选择你的道路", 34, Ink, TextAnchor.UpperLeft);
            Stretch(heading.rectTransform, new Vector2(0.58f, 0.75f), new Vector2(0.90f, 0.86f), Vector2.zero, Vector2.zero);

            menuListRoot = new GameObject("MenuList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menuListRoot.transform.SetParent(parent, false);
            var layout = menuListRoot.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 11;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Stretch(menuListRoot.GetComponent<RectTransform>(), new Vector2(0.58f, 0.27f), new Vector2(0.91f, 0.73f), Vector2.zero, Vector2.zero);

            CreateRecordCard(parent);
        }

        private void CreateRecordCard(Transform parent)
        {
            var record = CreateImage("RecentRecord", parent, new Color(0.02f, 0.05f, 0.11f, 0.76f));
            Stretch(record.rectTransform, new Vector2(0.58f, 0.08f), new Vector2(0.91f, 0.22f), Vector2.zero, Vector2.zero);

            recordSkeleton = new GameObject("RecordSkeleton", typeof(RectTransform));
            recordSkeleton.transform.SetParent(record.transform, false);
            Stretch(recordSkeleton.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            for (var i = 0; i < 2; i++)
            {
                var bar = CreateImage($"Bar{i}", recordSkeleton.transform, new Color(1f, 1f, 1f, 0.12f));
                Stretch(bar.rectTransform, new Vector2(0.06f, 0.18f + i * 0.42f), new Vector2(0.5f + i * 0.2f, 0.48f + i * 0.42f), Vector2.zero, Vector2.zero);
                skeletonBars.Add(bar);
            }

            recordEmptyText = CreateText("RecordEmpty", record.transform, "尚无远征记录", 19, Muted, TextAnchor.MiddleCenter);
            Stretch(recordEmptyText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            recordDataRoot = new GameObject("RecordData", typeof(RectTransform));
            recordDataRoot.transform.SetParent(record.transform, false);
            Stretch(recordDataRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            recordText = CreateText("RecordText", recordDataRoot.transform, string.Empty, 17, Ink, TextAnchor.MiddleLeft);
            Stretch(recordText.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.72f, 0.88f), Vector2.zero, Vector2.zero);
            var detail = CreateSimpleButton(recordDataRoot.transform, "查看详情 →", new Vector2(0.74f, 0.2f), new Vector2(0.95f, 0.8f), new Color(0.15f, 0.23f, 0.31f, 1f));
            detail.onClick.AddListener(delegate { StartCoroutine(Transition("上一局结算回放（FR-803 只读视图）将在下一迭代接入。")); });
        }

        private void CreateFooterHints(Transform parent)
        {
            var hints = CreateText("InputHints", parent, "↑ ↓ 选择     Enter 确认     ← → 切换区域     Esc 退出游戏", 14, Muted, TextAnchor.MiddleCenter);
            Stretch(hints.rectTransform, new Vector2(0.29f, 0.015f), new Vector2(0.71f, 0.06f), Vector2.zero, Vector2.zero);
        }

        private void CreateModal(Transform parent)
        {
            modal = new GameObject("ConfirmModal", typeof(Image));
            modal.transform.SetParent(parent, false);
            var dimmer = modal.GetComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.72f);
            Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var panel = CreateImage("Panel", modal.transform, new Color(0.07f, 0.08f, 0.12f, 1f));
            Stretch(panel.rectTransform, new Vector2(0.32f, 0.32f), new Vector2(0.68f, 0.67f), Vector2.zero, Vector2.zero);
            modalTitle = CreateText("Title", panel.transform, string.Empty, 29, Ink, TextAnchor.UpperLeft);
            Stretch(modalTitle.rectTransform, new Vector2(0.08f, 0.60f), new Vector2(0.92f, 0.86f), Vector2.zero, Vector2.zero);
            modalBody = CreateText("Body", panel.transform, string.Empty, 18, Muted, TextAnchor.UpperLeft);
            Stretch(modalBody.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.58f), Vector2.zero, Vector2.zero);
            modalCancel = CreateSimpleButton(panel.transform, "取消", new Vector2(0.47f, 0.09f), new Vector2(0.67f, 0.24f), new Color(0.15f, 0.23f, 0.31f, 1f));
            modalConfirm = CreateSimpleButton(panel.transform, "确认", new Vector2(0.70f, 0.09f), new Vector2(0.92f, 0.24f), new Color(0.44f, 0.15f, 0.14f, 1f));
            modal.SetActive(false);
        }

        private void CreateToast(Transform parent)
        {
            toastPanel = CreateImage("Toast", parent, new Color(0.03f, 0.06f, 0.12f, 0.95f)).gameObject;
            Stretch(toastPanel.GetComponent<RectTransform>(), new Vector2(0.3f, 0.075f), new Vector2(0.7f, 0.135f), Vector2.zero, Vector2.zero);
            toastText = CreateText("Text", toastPanel.transform, string.Empty, 17, Ink, TextAnchor.MiddleCenter);
            Stretch(toastText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            toastPanel.SetActive(false);
        }

        // ---------- 菜单渲染 ----------

        private void RebuildMenu()
        {
            foreach (Transform child in menuListRoot.transform)
            {
                Destroy(child.gameObject);
            }

            _menuButtons.Clear();
            _model = new HomeMenuModel(_service);

            for (var i = 0; i < _model.Entries.Count; i++)
            {
                _menuButtons.Add(CreateMenuButton(_model.Entries[i]));
            }

            // TC-1102-6：↑↓ 在入口按钮组内循环不丢焦；左右留空，焦点域切换统一由 Update 处理。
            var count = _menuButtons.Count;
            for (var i = 0; i < count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = _menuButtons[(i + count - 1) % count];
                nav.selectOnDown = _menuButtons[(i + 1) % count];
                _menuButtons[i].navigation = nav;
            }

            RestoreMenuSelection();
        }

        private Button CreateMenuButton(HomeMenuModel.Entry entry)
        {
            var accent = entry.Id == "continue" ? Cyan : entry.IsDestructive ? Danger : Gold;
            var node = new GameObject(entry.Title, typeof(Image), typeof(Button), typeof(LayoutElement));
            node.transform.SetParent(menuListRoot.transform, false);
            var image = node.GetComponent<Image>();
            image.color = new Color(0.06f, 0.11f, 0.19f, 0.94f);
            var button = node.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(accent.r * 0.32f, accent.g * 0.32f, accent.b * 0.32f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var captured = entry;
            button.onClick.AddListener(delegate { ActivateEntry(captured); });
            node.GetComponent<LayoutElement>().minHeight = 66f;

            var accentBar = CreateImage("Accent", node.transform, accent);
            Stretch(accentBar.rectTransform, Vector2.zero, new Vector2(0.012f, 1f), Vector2.zero, Vector2.zero);

            var label = CreateText("Label", node.transform,
                entry.Title + "\n<size=15><color=#B9C7D8>" + entry.Subtitle + "</color></size>", 23, Ink, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.78f, 0.92f), Vector2.zero, Vector2.zero);
            label.supportRichText = true;

            if (entry.ShowRedDot)
            {
                var dot = CreateImage("RedDot", node.transform, new Color32(232, 84, 74, 255));
                dot.rectTransform.sizeDelta = new Vector2(11f, 11f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.82f, 0.68f);
                dot.rectTransform.anchoredPosition = Vector2.zero;
            }

            if (!string.IsNullOrEmpty(entry.Badge))
            {
                var badge = CreateText("Badge", node.transform, entry.Badge, 14, Gold, TextAnchor.MiddleRight);
                Stretch(badge.rectTransform, new Vector2(0.78f, 0.3f), new Vector2(0.94f, 0.7f), Vector2.zero, Vector2.zero);
            }

            return button;
        }

        private void RestoreMenuSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || _menuButtons.Count == 0 || (modal != null && modal.activeSelf))
            {
                return;
            }

            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(menuListRoot.transform))
            {
                return;  // 选中项仍在按钮组内则保持
            }

            eventSystem.SetSelectedGameObject(_menuButtons[_model.DefaultFocusIndex].gameObject);
        }

        // ---------- 入口行为 ----------

        private void ActivateEntry(HomeMenuModel.Entry entry)
        {
            if (_locked || _loading)
            {
                return;  // P-5：Loading/Transitioning 锁定（TC-1102-11 快速连点只触发一次转场）
            }

            switch (entry.Id)
            {
                case "continue":
                    StartCoroutine(Transition("正在从最近安全点恢复未完成远征…"));
                    break;
                case "start":
                    StartExpedition();
                    break;
                case "meta":
                    StartCoroutine(Transition("「局外成长中心」将在下一页面迭代接入（FR-1104）。"));
                    break;
                case "archive":
                    StartCoroutine(Transition("「图鉴与档案」将在下一页面迭代接入（FR-1106）。"));
                    break;
                case "settings":
                    StartCoroutine(Transition("「设置」将在下一页面迭代接入（FR-1105）。"));
                    break;
                case "help":
                    StartCoroutine(Transition("「帮助与教程」将在下一页面迭代接入（FR-1107）。"));
                    break;
                case "exit":
                    OpenExitConfirm();
                    break;
            }
        }

        private void StartExpedition()
        {
            if (_service.ActiveRun == null)
            {
                StartCoroutine(EnterTown());
                return;
            }

            // R-1102-10：必须明确后果，默认焦点落在「取消」；确认后原子删除快照再进入选角页。
            ShowConfirm(
                "放弃未完成远征？",
                "开始新远征将放弃当前未完成远征，且无法继续。局外宝石与解锁进度不会受影响。",
                "放弃并开始",
                delegate
                {
                    _service.AbandonActiveRun();
                    StartCoroutine(EnterTown());
                });
        }

        /// <summary>进入冒险者小镇（FR-1103 选角页接入前的占位流程）。</summary>
        private System.Collections.IEnumerator EnterTown()
        {
            yield return Transition("正在启程前往冒险者小镇…");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Town");
        }

        private void OpenExitConfirm()
        {
            // R-1102-7：退出前先完成存档落盘（此处由 FR-901 接入）；任何平台都二次确认。
            ShowConfirm(
                "确定退出《骑士异闻录》？",
                "确认后将先写入局外进度再退出；未完成远征会保留在最近的安全点，供下次继续。",
                "确认退出",
                delegate { StartCoroutine(Transition("局外进度已保存。编辑器模式下不会真正关闭窗口。")); });
        }

        private void SwitchCharacter()
        {
            if (_locked || _loading)
            {
                return;
            }

            StartCoroutine(EnterTown());
        }

        private void RetrySave()
        {
            _service.RetrySave();
            ShowToast("已成功写入局外档案。");
        }

        // ---------- 弹窗 / 提示 ----------

        private void ShowConfirm(string title, string body, string confirmLabel, UnityEngine.Events.UnityAction onConfirm)
        {
            modalTitle.text = title;
            modalBody.text = body;
            modalCancel.onClick.RemoveAllListeners();
            modalConfirm.onClick.RemoveAllListeners();
            modalCancel.onClick.AddListener(CloseModal);
            modalConfirm.onClick.AddListener(delegate
            {
                CloseModal();
                onConfirm();
            });
            modal.SetActive(true);
            EventSystem.current.SetSelectedGameObject(modalCancel.gameObject);  // P-6 / TC-1102-8
        }

        private void CloseModal()
        {
            modal.SetActive(false);
            RestoreMenuSelection();
        }

        private void OnStateChanged()
        {
            RebuildMenu();
            RefreshSaveWarning();
        }

        private void OnGemsChanged()
        {
            if (_gemRoll != null)
            {
                StopCoroutine(_gemRoll);
            }

            _gemRoll = StartCoroutine(RollGems(_service.Gems));
        }

        private void RefreshTopBar()
        {
            profileText.text = string.Format("旅者档案\n{0}  ·  {1} / {2} 次远征", _service.PlayerName, _service.PlaytimeText, _service.ExpeditionCount);
            _displayedGems = _service.Gems;
            gemsText.text = string.Format("◆  {0:N0}\n v0.1.0-dev", _displayedGems);
            RefreshSaveWarning();
        }

        private void RefreshSaveWarning()
        {
            saveWarning.SetActive(_service.SaveFailed);
        }

        private void ShowRecordSkeleton()
        {
            recordSkeleton.SetActive(true);
            recordEmptyText.gameObject.SetActive(false);
            recordDataRoot.SetActive(false);
            if (_skeletonPulse != null)
            {
                StopCoroutine(_skeletonPulse);
            }

            _skeletonPulse = StartCoroutine(PulseSkeleton());
        }

        private void RefreshRecordCard()
        {
            if (_loading)
            {
                ShowRecordSkeleton();
                return;
            }

            if (_skeletonPulse != null)
            {
                StopCoroutine(_skeletonPulse);
                _skeletonPulse = null;
            }

            recordSkeleton.SetActive(false);
            var record = _service.LastRecord;
            recordEmptyText.gameObject.SetActive(record == null);  // TC-1102-3：无战绩不显示 0 值
            recordDataRoot.SetActive(record != null);
            if (record != null)
            {
                recordText.text = string.Format(
                    "最近一局战绩  ·  {0}\n{1}（最远推进）     {2} 击杀     {3}     +{4} ◆",
                    record.Victory ? "通关" : "未通关",
                    record.ThemeLabel,
                    record.Kills,
                    record.Duration,
                    record.GemsEarned);
            }
        }

        // ---------- 协程 ----------

        private IEnumerator Transition(string message)
        {
            if (_locked)
            {
                yield break;
            }

            _locked = true;
            var group = canvasRoot.GetComponent<CanvasGroup>();
            yield return Fade(group, 1f, 0.88f);
            ShowToast(message);
            yield return Fade(group, 0.88f, 1f);
            _locked = false;
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to)
        {
            var t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / TransitionSeconds;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }

            group.alpha = to;
        }

        private IEnumerator RollGems(int target)
        {
            var start = _displayedGems;
            var t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / GemRollSeconds;
                _displayedGems = Mathf.RoundToInt(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
                gemsText.text = string.Format("◆  {0:N0}\n v0.1.0-dev", _displayedGems);
                yield return null;
            }

            _displayedGems = target;
            gemsText.text = string.Format("◆  {0:N0}\n v0.1.0-dev", _displayedGems);
        }

        private IEnumerator PulseSkeleton()
        {
            var t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                var alpha = 0.07f + 0.08f * (0.5f + 0.5f * Mathf.Sin(t * 4f));
                foreach (var bar in skeletonBars)
                {
                    bar.color = new Color(1f, 1f, 1f, alpha);
                }

                yield return null;
            }
        }

        private void ShowToast(string message)
        {
            toastText.text = message;
            toastPanel.SetActive(true);
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.6f);
        }

        private void HideToast()
        {
            toastPanel.SetActive(false);
        }

        // ---------- 控件工厂 ----------

        private Button CreateSimpleButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var node = new GameObject(label, typeof(Image), typeof(Button));
            node.transform.SetParent(parent, false);
            node.GetComponent<Image>().color = color;
            Stretch(node.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var text = CreateText("Text", node.transform, label, 17, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return node.GetComponent<Button>();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent, false);
            var image = node.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            node.transform.SetParent(parent, false);
            var text = node.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
