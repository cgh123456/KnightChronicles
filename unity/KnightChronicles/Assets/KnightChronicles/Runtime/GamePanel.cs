using System;
using System.Collections.Generic;
using KnightChronicles.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnightChronicles.Runtime
{
    /// <summary>Shared, resolution independent in-game panels for town services and expeditions.</summary>
    public sealed class GamePanel : MonoBehaviour
    {
        private static GamePanel instance;
        public DungeonController Dungeon;
        private string page = "", confirmTitle, confirmText, message;
        private Action confirmed;
        private float messageUntil;
        private Vector2 scroll, inventoryScroll;
        private string filter = "", inventoryTab = "warehouse";
        private string skillGroup = "骑士剑";
        private ItemKind craftKind = ItemKind.Weapon;
        private int craftTier;
        private int storyNode;
        private string bindingCapture;
        private GUIStyle title, normal, button, small, caption, centered, tabButton;
        private GUIStyle hudText, hudSmall, hudHeading, hudCentered, hintText;
        private GUIStyle panel, rowCard;
        private bool map;
        private Item selectedItem, hoverItem, dragItem;
        private ItemDefinition hoverDefinition;
        private string selectedSource, dragSource;
        private Vector2 dragOrigin;
        private bool dragging;
        private bool drawingGui, drawingScroll;
        private int dragControl;
        private Rect scrollClip;
        private float canvasWidth, canvasHeight;
        private readonly List<DropArea> dropAreas = new List<DropArea>();
        private sealed class DropArea { public Rect Rect; public string Container; public Slot? Equipment; }
        public int PaintCount { get; private set; }
        public string CurrentPage { get { return page; } }
        public static RenderTexture CaptureTarget;
        public static bool Blocked { get { return instance != null && (instance.page.Length > 0 || instance.confirmed != null); } }
        private GameRules Rules { get { return GameSession.Rules; } }
        private Profile State { get { return Rules.State; } }
        public static void Toast(string text) { if (instance != null) { instance.message = text; instance.messageUntil = Time.unscaledTime + 4; } }
        public static void OpenPage(string page) { if (instance != null) instance.Open(page); }
        public static void Confirm(string title, string text, Action action)
        {
            if (instance == null) return; instance.CancelDrag(); instance.confirmTitle = title; instance.confirmText = text; instance.confirmed = action; instance.Freeze(); instance.Relayout();
        }
        private void Awake()
        {
            instance = this; Time.timeScale = 1;
            if (Dungeon == null && State.ActiveRun == null && State.LastReport != null && State.ReportPending) page = "report";
        }
        private void Start()
        {
            if (!string.IsNullOrEmpty(GameSession.EntryPage)) { var requested = GameSession.EntryPage; GameSession.EntryPage = null; Open(requested); }
            Freeze();
        }
        private void OnDestroy() { CancelDrag(); if (instance == this) instance = null; Time.timeScale = 1; }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void Open(string target) { CancelDrag(); page = target; scroll = inventoryScroll = Vector2.zero; scrollClip = new Rect(); filter = ""; bindingCapture = null; selectedItem = null; Freeze(); Relayout(); }
        private void Freeze()
        {
            Time.timeScale = Dungeon != null && (page.Length > 0 || confirmed != null) ? 0 : 1;
            var controller = FindObjectOfType<TopDownPlayerController>(); if (controller != null) controller.ControlsBlocked = page.Length > 0 || confirmed != null;
        }
        private void Close() { if (page == "report") { GameSession.Commit(r => { r.State.ReportPending = false; return true; }); AudioDirector.FloorChanged(); } CancelDrag(); bindingCapture = null; selectedItem = null; page = ""; confirmed = null; Freeze(); Relayout(); }
        private void CancelDrag()
        { if (dragControl != 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0; dragItem = null; dragSource = null; dragging = false; }
        private void Relayout()
        { if (drawingGui && Event.current != null && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) GUIUtility.ExitGUI(); }
        private void Navigate(string target) { CancelDrag(); Time.timeScale = 1; SceneManager.LoadScene(target); Relayout(); }
        private void Update()
        {
            if (bindingCapture != null) return;
            if (GameInput.Down("pause")) { if (confirmed != null) { confirmed = null; CancelDrag(); Freeze(); return; } else if (page.Length > 0) Close(); else Open("pause"); }
            if (confirmed != null) return;
            if (GameInput.Down("inventory")) { if (page == "inventory") Close(); else Open("inventory"); }
            if (Dungeon != null && GameInput.Down("map")) map = !map;
        }
        private void Styles()
        {
            if (title != null) return;
            var font = GameFont.World;
            normal = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, richText = true, padding = new RectOffset(4, 4, 7, 7) };
            normal.normal.textColor = UiTheme.Text;
            title = new GUIStyle(normal) { fontSize = 29, fontStyle = FontStyle.Bold }; title.normal.textColor = UiTheme.Gold;
            small = new GUIStyle(normal) { fontSize = 14 }; small.normal.textColor = UiTheme.Muted;
            caption = new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(2, 2, 1, 1) };
            centered = new GUIStyle(normal) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hudText = new GUIStyle(normal) { padding = new RectOffset(), margin = new RectOffset(), alignment = TextAnchor.MiddleLeft, wordWrap = false };
            hudSmall = new GUIStyle(small) { padding = new RectOffset(), margin = new RectOffset(), alignment = TextAnchor.MiddleLeft, wordWrap = false };
            hudHeading = new GUIStyle(title) { fontSize = 24, padding = new RectOffset(), margin = new RectOffset(), alignment = TextAnchor.MiddleLeft, wordWrap = false };
            hudCentered = new GUIStyle(hudText) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hintText = new GUIStyle(hudCentered) { fontSize = 17, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17, padding = new RectOffset(16, 16, 12, 12), border = new RectOffset(8, 8, 8, 8), wordWrap = true, margin = new RectOffset(4, 4, 4, 4) };
            button.normal.background = UiTheme.Brass; button.normal.textColor = UiTheme.Text;
            button.hover.background = UiTheme.Hover; button.hover.textColor = Color.white;
            button.active.background = UiTheme.Inset; button.active.textColor = UiTheme.Gold;
            button.onNormal.background = UiTheme.Hover; button.onNormal.textColor = UiTheme.Gold;
            tabButton = new GUIStyle(button) { fontSize = 15, padding = new RectOffset(10, 10, 9, 9) };
            panel = UiTheme.Panel(22);
            rowCard = new GUIStyle(panel) { padding = new RectOffset(12,12,10,10), margin = new RectOffset(2,2,4,4), border = new RectOffset(8,8,8,8) }; rowCard.normal.background = UiTheme.Inset;
            GUI.skin.font = font;
            GUI.skin.textField.font = font; GUI.skin.textField.fontSize = 17;
            GUI.skin.textField.normal.background = UiTheme.Inset; GUI.skin.textField.normal.textColor = UiTheme.Text;
        }
        private bool Button(string text, bool enabled = true, float width = 0)
        {
            var before = GUI.enabled; GUI.enabled = before && enabled;
            var pressed = width > 0 ? GUILayout.Button(text, button, GUILayout.Width(width)) : GUILayout.Button(text, button);
            GUI.enabled = before; if(pressed)AudioDirector.Cue((text.Length+page.Length)%70); return pressed;
        }
        private void Text(string text) { GUILayout.Label(text, normal); }
        private void OnGUI()
        {
            var capture = Event.current.type == EventType.Repaint && CaptureTarget != null;
            var previousTarget = RenderTexture.active;
            var previous = GUI.matrix; var originalEnabled = GUI.enabled;
            if (capture) Graphics.SetRenderTarget(CaptureTarget);
            drawingGui = true;
            try
            {
            dragControl = GUIUtility.GetControlID(0x4b434452, FocusType.Passive);
            Styles();
            var scale = Mathf.Min(Screen.height / 900f, Screen.width / 1200f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            var width = canvasWidth = Screen.width / scale; canvasHeight = Screen.height / scale;
            if (Event.current.type == EventType.Repaint) PaintCount++;
            hoverItem = null; hoverDefinition = null; dropAreas.Clear();
            GUI.enabled = originalEnabled && confirmed == null;
            if (Dungeon != null) DrawHud(width);
            else
            {
                GUILayout.BeginArea(new Rect(20, 20, 580, 156), panel);
                Text("冒险者小镇 · " + GameRules.TierName(State.Rank) + " / 职业者 Lv." + State.CareerLevel);
                Text("保险箱 " + State.Coins + " 金币    随身 " + State.PocketCoins + "    技能点 " + State.SkillPoints);
                GUILayout.BeginHorizontal(); if (Button("I 背包 / 仓库")) Open("inventory"); if (Button("公会任务")) Open("guild"); if (Button("远征入口")) Open("expedition"); GUILayout.EndHorizontal(); GUILayout.EndArea();
            }
            if (page.Length > 0)
            {
                GUI.DrawTexture(new Rect(0, 0, width, canvasHeight), UiTheme.Shade);
                GUILayout.BeginArea(new Rect(Mathf.Max(20, (width - 1140) / 2), (canvasHeight - 830) / 2, Mathf.Min(1140, width - 40), 830), panel);
                GUILayout.BeginHorizontal(); GUILayout.Label(PageTitle(), title); GUILayout.FlexibleSpace(); if (Button("关闭 · Esc", true, 150)) Close(); GUILayout.EndHorizontal();
                GUILayout.Label("保险箱 " + State.Coins + "   ·   随身 " + State.PocketCoins + "   ·   负重 " + Rules.Weight.ToString("F1") + " / " + Rules.CarryLimit, small);
                DrawNavigation();
                scroll = GUILayout.BeginScrollView(scroll);
                drawingScroll = true;
                switch (page)
                {
                    case "expedition": Expedition(); break;
                    case "inventory": Inventory(); break;
                    case "guild": Guild(); break;
                    case "hut": Hut(); break;
                    case "report": Report(); break;
                    case "settings": Settings(); break;
                    case "help": Help(); break;
                    case "pause": Pause(); break;
                    case "craft": Craft(); break;
                    default: Shop(); break;
                }
                drawingScroll = false; GUILayout.EndScrollView();
                if (Event.current.type == EventType.Repaint) scrollClip = ScreenRect(GUILayoutUtility.GetLastRect());
                GUILayout.EndArea();
            }
            GUI.enabled = originalEnabled;
            if (confirmed != null)
            {
                GUI.DrawTexture(new Rect(0, 0, width, canvasHeight), UiTheme.Shade);
                GUILayout.BeginArea(new Rect((width - 600) / 2, (canvasHeight - 330) / 2, 600, 330), panel); GUILayout.Label(confirmTitle, title); Text(confirmText); GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal(); if (Button("取消")) { confirmed = null; CancelDrag(); Freeze(); Relayout(); } if (Button("确认")) { var action = confirmed; confirmed = null; action(); Freeze(); Relayout(); } GUILayout.EndHorizontal(); GUILayout.EndArea();
            }
            if (messageUntil > Time.unscaledTime || GameSession.SaveError != null)
            { GUILayout.BeginArea(new Rect((width - 660) / 2, canvasHeight - 82, 660, 62), panel); GUILayout.Label(GameSession.SaveError ?? message, centered); GUILayout.EndArea(); }
            HandleDrag(); DrawTooltip();
            }
            finally
            {
            drawingGui = drawingScroll = false;
            GUI.enabled = originalEnabled;
            GUI.matrix = previous;
            if (capture) Graphics.SetRenderTarget(previousTarget);
            }
        }
        private void DrawNavigation()
        {
            if (page == "pause" || page == "settings" || page == "help" || page == "report") return;
            GUILayout.BeginHorizontal();
            var targets = Dungeon == null ? new[]{"inventory", "guild", "hut", "expedition"} : new[]{"inventory", "help"};
            var labels = Dungeon == null ? new[]{"行囊与仓库", "公会委托", "骑士小屋", "地下城"} : new[]{"随身行囊", "远征手册"};
            for (var i = 0; i < targets.Length; i++) if (GUILayout.Toggle(page == targets[i], labels[i], tabButton) && page != targets[i]) Open(targets[i]);
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.Space(10);
        }
        private string PageTitle()
        {
            switch (page) { case "inventory": return "背包与仓库"; case "expedition": return "地下城远征"; case "guild": return "冒险者公会";
                case "hut": return "冒险者小屋"; case "report": return State.LastReport != null && State.LastReport.Extracted ? "撤离成功" : "远征结束";
                case "smith": return "铁匠铺"; case "armor": return "防具店"; case "potions": return "药剂店"; case "scrolls": return "卷轴屋";
                case "general": return "杂货铺"; case "settings": return "设置"; case "help": return "远征指南"; case "craft": return "装备打造"; default: return "暂停"; }
        }
        private void Action(Func<GameRules, bool> action, Func<string> error = null)
        {
            if (GameSession.Commit(action)) Toast("已完成并保存"); else Toast(GameSession.SaveError ?? (error == null ? "操作无法完成" : error()));
            CancelDrag(); Relayout();
        }
        private void Expedition()
        {
            Text("地下城共五层。L2 与 L4 可以撤离；L5 宝藏层需要返回 L4。死亡会永久失去随身物品、装备和金币。");
            if (State.ActiveRun != null)
            {
                Text("未完成远征：L" + State.ActiveRun.Floor);
                if (Button("继续最近安全点")) Navigate("Town");
                if (Button("放弃远征")) Confirm("放弃远征？", "所有随身物品、装备、容器和金币永久删除。仓库与成长保留。", () => { if (GameSession.Finish(false)) Navigate("Town"); }); return;
            }
            for (var d = 0; d < 5; d++)
            {
                var difficulty = d; Text("T" + (d + 1) + " · " + GameRules.TierName(d) + "   怪物强度 ×" + DungeonGenerator.DifficultyMultiplier(d).ToString("F1") + "   掉落随深度提升");
                if (Button(d <= State.Rank ? "进入 T" + (d + 1) : "需要 " + GameRules.TierName(d) + " 冒险者", d <= State.Rank && GameSession.SaveError == null))
                { string error = null; if (GameSession.Commit(r => r.BeginRun(difficulty, Environment.TickCount, out error))) Navigate("Town"); else Toast(error ?? GameSession.SaveError); }
            }
            Text("新手补给：帆布胸挂 + 帆布背包 + 青铜骑士剑 + 药剂，可直接启程。死亡后可用保险箱金币在商店重新整备。");
        }
        private List<Item> CurrentList()
        {
            return inventoryTab == "warehouse" ? State.Warehouse : inventoryTab == "rig" ? State.RigItems : State.PackItems;
        }
        private void Inventory()
        {
            GUILayout.Label("骑士装备", title); GUILayout.BeginHorizontal();
            for (var slot = 0; slot < 6; slot++)
            {
                GUILayout.BeginVertical(GUILayout.Width(110));
                var rect = GUILayoutUtility.GetRect(98, 87); var s = (Slot)slot;
                DrawItemCell(rect, State.Equipped[slot], "equipped" + slot, SlotName(s));
                RegisterDrop(rect, null, s); GUILayout.Label(SlotName(s), caption, GUILayout.Height(24)); GUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            ContainerTab("rig", "胸挂", State.RigItems.Count, Rules.Capacity(Slot.Rig), true);
            ContainerTab("pack", "背包", State.PackItems.Count, Rules.Capacity(Slot.Pack), true);
            ContainerTab("warehouse", "安全仓库", State.Warehouse.Count, State.WarehouseCapacity, Dungeon == null);
            GUILayout.EndHorizontal();
            if (Dungeon != null && inventoryTab == "warehouse") inventoryTab = "pack";
            GUILayout.Label("单击查看详情 · 双击穿戴装备/转移物品 · 拖到容器标签或装备槽整理", small);
            var source = CurrentList(); var capacity = inventoryTab == "warehouse" ? State.WarehouseCapacity : Rules.Capacity(inventoryTab == "rig" ? Slot.Rig : Slot.Pack);
            var copy = source.ToArray();
            if (selectedItem != null && !ItemExists(selectedItem, selectedSource)) selectedItem = null;
            GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(704));
            var count = Math.Max(8, capacity); const int columns = 8;
            for (var row = 0; row < (count + columns - 1) / columns; row++)
            {
                GUILayout.BeginHorizontal();
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col; var rect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
                    if (index < count) { var item = index < copy.Length ? copy[index] : null; DrawItemCell(rect, item, inventoryTab, index >= capacity ? "锁定" : ""); if (index < capacity) RegisterDrop(rect, inventoryTab); }
                    GUILayout.Space(7);
                }
                GUILayout.EndHorizontal(); GUILayout.Space(7);
            }
            GUILayout.EndVertical(); GUILayout.Space(14); GUILayout.BeginVertical(GUILayout.Width(315));
            if (selectedItem == null)
            { GUILayout.Label("物品详情", title); Text("选择一个物品查看属性与附魔。"); GUILayout.Label("胸挂中的药剂与卷轴依次对应 1 / 2 / 3。仓库物品需要取出后才能在地下城使用。", small); }
            else
            {
                var item = selectedItem; var definition = Rules.Catalog.Get(item.Id);
                GUILayout.Label(TooltipText(definition, item), normal);
                if (selectedSource.StartsWith("equipped"))
                { var s = (Slot)int.Parse(selectedSource.Substring(8)); string error = null; if (Button("卸下并存入安全仓库", Dungeon == null)) Action(r => r.Unequip(s, out error), () => error); }
                else
                {
                    var sourceId = selectedSource; var from = ListFor(sourceId); string error = null;
                    if (definition.Kind <= ItemKind.Pack && Button("穿戴装备", Dungeon == null || definition.Kind == ItemKind.Weapon)) Action(r => r.Equip(from, item, out error), () => error);
                    if (selectedSource != "rig" && Button("放入胸挂")) MoveItem(selectedSource, "rig", item);
                    if (selectedSource != "pack" && Button("放入背包")) MoveItem(selectedSource, "pack", item);
                    if (selectedSource != "warehouse" && Button("存入安全仓库", Dungeon == null)) MoveItem(selectedSource, "warehouse", item);
                    if (Button("出售 · " + Rules.SellPrice(item) + " 金币", Dungeon == null))
                        Confirm("出售 " + definition.Name + "？", "将出售整组物品（×" + item.Count + "），获得 " + Rules.SellPrice(item) + " 金币。", () => Action(r => r.Sell(ListFor(sourceId), item, out error), () => error));
                }
            }
            GUILayout.EndVertical(); GUILayout.EndHorizontal();
        }
        private void ContainerTab(string id, string name, int used, int capacity, bool enabled)
        {
            var before = GUI.enabled; GUI.enabled = before && enabled;
            if (GUILayout.Toggle(inventoryTab == id, name + "   " + used + " / " + capacity, tabButton) && inventoryTab != id) { CancelDrag(); inventoryTab = id; Relayout(); }
            GUI.enabled = before; if (enabled) RegisterDrop(GUILayoutUtility.GetLastRect(), id);
        }
        private List<Item> ListFor(string id) { return id == "warehouse" ? State.Warehouse : id == "rig" ? State.RigItems : State.PackItems; }
        private bool ItemExists(Item item, string source)
        { return source != null && (source.StartsWith("equipped") ? State.Equipped[int.Parse(source.Substring(8))] == item : ListFor(source).Contains(item)); }
        private void MoveItem(string from, string to, Item item)
        {
            string error = null;
            if (from.StartsWith("equipped"))
            { if (to != "warehouse") { Toast("卸下的装备需要先放入安全仓库"); return; } var slot = (Slot)int.Parse(from.Substring(8)); Action(r => r.Unequip(slot, out error), () => error); }
            else Action(r => r.Transfer(ListFor(from), ListFor(to), to == "warehouse" ? r.State.WarehouseCapacity : r.Capacity(to == "rig" ? Slot.Rig : Slot.Pack), item, out error), () => error);
        }
        private void DoubleClick(Item item, string source)
        {
            if (source.StartsWith("equipped")) { if (Dungeon == null) MoveItem(source, "warehouse", item); return; }
            string error = null; var d = Rules.Catalog.Get(item.Id);
            if (d.Kind <= ItemKind.Pack) Action(r => r.Equip(ListFor(source), item, out error), () => error);
            else MoveItem(source, source == "warehouse" ? d.Kind == ItemKind.Potion || d.Kind == ItemKind.Scroll ? "rig" : "pack" : Dungeon == null ? "warehouse" : source == "rig" ? "pack" : "rig", item);
        }
        private void DrawItemCell(Rect rect, Item item, string source, string empty = "", bool interactive = true)
        {
            GUI.DrawTexture(rect, UiTheme.Inset); UiTheme.Frame(rect, new Color(.26f,.25f,.2f));
            if (item == null) { if (empty.Length > 0) GUI.Label(rect, empty, caption); return; }
            var d = Rules.Catalog.Get(item.Id); var color = WorldArt.Tier(d.Tier);
            UiTheme.Frame(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6), color, selectedItem == item ? 3 : 1.5f);
            UiTheme.Fill(new Rect(rect.x + 4, rect.yMax - 7, rect.width - 8, 3), color);
            var image = new Rect(rect.x + 6, rect.y + 4, rect.width - 12, rect.height - 12); GUI.DrawTexture(image, ItemIconAtlas.Get(d), ScaleMode.ScaleToFit);
            if (item.Count > 1) { UiTheme.Fill(new Rect(rect.xMax - 29, rect.yMax - 27, 25, 19), new Color(.025f,.03f,.035f,.9f)); GUI.Label(new Rect(rect.xMax - 29, rect.yMax - 27, 25, 19), item.Count.ToString(), caption); }
            if (d.Durability > 0)
            { var ratio = item.Maximum <= 0 ? 0 : (float)item.Durability / item.Maximum; UiTheme.Fill(new Rect(rect.x + 7, rect.yMax - 11, rect.width - 14, 3), new Color(.09f,.08f,.07f)); UiTheme.Fill(new Rect(rect.x + 7, rect.yMax - 11, (rect.width - 14) * ratio, 3), ratio < .25f ? new Color(.85f,.23f,.16f) : UiTheme.Gold); }
            if (rect.Contains(Event.current.mousePosition) && PointerVisible())
            {
                hoverItem = item; hoverDefinition = d; UiTheme.Frame(rect, UiTheme.Gold, 2);
                var e = Event.current;
                if (interactive && GUI.enabled && e.type == EventType.MouseDown && e.button == 0)
                { selectedItem = item; selectedSource = source; e.Use(); if(e.clickCount >= 2) { CancelDrag(); DoubleClick(item, source); } else { dragItem = item; dragSource = source; dragOrigin = GUIUtility.GUIToScreenPoint(e.mousePosition); GUIUtility.hotControl = dragControl; } Relayout(); }
            }
        }
        private void RegisterDrop(Rect rect, string container, Slot? equipment = null)
        {
            var global = ScreenRect(rect);
            if (drawingScroll && scrollClip.width > 0)
            { var x = Mathf.Max(global.xMin,scrollClip.xMin); var y = Mathf.Max(global.yMin,scrollClip.yMin); var right = Mathf.Min(global.xMax,scrollClip.xMax); var bottom = Mathf.Min(global.yMax,scrollClip.yMax); if(right<=x||bottom<=y)return;global=Rect.MinMaxRect(x,y,right,bottom); }
            dropAreas.Add(new DropArea { Rect = global, Container = container, Equipment = equipment });
        }
        private static Rect ScreenRect(Rect rect)
        { var p = GUIUtility.GUIToScreenPoint(rect.position); var end = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax,rect.yMax)); return new Rect(p,end-p); }
        private bool PointerVisible()
        { return !drawingScroll || scrollClip.width <= 0 || scrollClip.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)); }
        private void HandleDrag()
        {
            if (dragItem == null) return; var e = Event.current;
            if (confirmed != null || page != "inventory" || !ItemExists(dragItem, dragSource)) { CancelDrag(); return; }
            if (e.type == EventType.MouseDrag && Vector2.Distance(GUIUtility.GUIToScreenPoint(e.mousePosition), dragOrigin) > 8) { dragging = true; e.Use(); }
            if (dragging)
            { var rect = new Rect(e.mousePosition.x - 31, e.mousePosition.y - 31, 62, 62); var before = GUI.color; GUI.color = new Color(1,1,1,.85f); GUI.DrawTexture(rect, ItemIconAtlas.Get(Rules.Catalog.Get(dragItem.Id))); GUI.color = before; }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (dragging)
                {
                    var pointer = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    for (var i = dropAreas.Count - 1; i >= 0; i--) if (dropAreas[i].Rect.Contains(pointer))
                    { var area = dropAreas[i];
                        if (area.Equipment.HasValue && !dragSource.StartsWith("equipped"))
                        { string error = null; var item = dragItem; var d = Rules.Catalog.Get(item.Id); if (d.Slot != area.Equipment.Value || d.Kind > ItemKind.Pack) Toast("请拖入对应的装备部位"); else Action(r => r.Equip(ListFor(dragSource), item, out error), () => error); }
                        else if (area.Container != null && area.Container != dragSource) MoveItem(dragSource, area.Container, dragItem);
                        break;
                    }
                    e.Use();
                }
                CancelDrag();
            }
        }
        private static string SlotName(Slot slot) { return new[]{"武器", "头部", "胸甲", "护腿", "胸挂", "背包"}[(int)slot]; }
        private string TooltipText(ItemDefinition d, Item item = null)
        {
            var text = "<color=#" + ColorUtility.ToHtmlStringRGB(WorldArt.Tier(d.Tier)) + "><b>" + d.Name + "</b></color>\n" + GameRules.TierName(d.Tier) + " · " + (d.Kind <= ItemKind.Pack ? SlotName(d.Slot) : d.Kind == ItemKind.Potion ? "药剂" : d.Kind == ItemKind.Scroll ? "魔法卷轴" : d.Kind == ItemKind.Key ? "钥匙" : d.Kind == ItemKind.Material ? "打造素材" : "珍贵遗物");
            if (d.Kind == ItemKind.Weapon) text += "\n攻击 " + d.Power + "   距离 " + d.Range.ToString("F1") + "   间隔 " + d.Interval.ToString("F2") + " 秒";
            else if (d.Kind == ItemKind.Armor) text += "\n防御 " + d.Power + (d.Speed != 0 ? "   移速 " + d.Speed.ToString("+0.0;-0.0") : "") + "\n套装：" + d.Set;
            else if (d.Kind == ItemKind.Rig || d.Kind == ItemKind.Pack) text += "\n容量 " + d.Capacity + " 格" + (d.CarryBonus > 0 ? "   负重上限 +" + d.CarryBonus : "");
            else if (d.Kind == ItemKind.Potion) text += "\n" + EffectName(d.Effect) + (d.Power > 0 ? " " + d.Power : "") + "   共享冷却 5 秒";
            else if (d.Kind == ItemKind.Scroll) text += "\n" + EffectName(d.Effect) + "   共享冷却 5 秒";
            else if (d.Kind == ItemKind.Key) text += "\n开启地下城隐藏房间的石门。";
            else if (d.Kind == ItemKind.Material) text += "\n三枚同档矿石可打造一件装备。";
            if (d.Durability > 0) text += "\n耐久 " + (item == null ? d.Durability + "/" + d.Durability : item.Durability + "/" + item.Maximum) + (item != null && item.Durability == 0 ? " <color=#E06A54>已损坏</color>" : "");
            text += "\n重量 " + (d.Weight * (item == null ? 1 : item.Count)).ToString("F1") + "   基准价值 " + d.Price + " 金币";
            if (item != null && item.Count > 1) text += "   数量 ×" + item.Count;
            if (item != null && item.Enchants.Count > 0) { text += "\n\n附魔"; foreach(var enchant in item.Enchants) text += "\n<color=#DFC98D>" + EnchantName(enchant) + "</color> · " + EnchantDescription(enchant, d.Tier); }
            return text;
        }
        private static string EffectName(string effect)
        { var ids=new[]{"hp","mp","attack","speed","defense","return","fireball","detect","shield","strength","cleanse"};var names=new[]{"恢复生命","恢复法力","提升伤害","提升移动速度","提高防御","立即结束远征并撤离","释放火球","揭示附近搜索点","获得临时护盾","提高攻击伤害","清除负面状态"};var index=Array.IndexOf(ids,effect);return index<0?effect:names[index]; }
        private static string EnchantDescription(string id,int tier)
        {
            var strength = tier >= 4 ? 1.7f : tier == 3 ? 1.3f : 1;
            switch(id)
            {case "sharp":return "武器伤害 +" + (8*strength).ToString("0.#") + "%";
             case "haste":return "普通攻击速度 +" + (8*strength).ToString("0.#") + "%";
             case "slaughter":return "命中伤害 +" + (10*strength).ToString("0.#") + "%";
             case "drain":return "回复造成伤害的 " + (3*strength).ToString("0.#") + "% 生命";
             case "fire":return "命中额外造成 " + (5*strength).ToString("0.#") + " 火焰伤害";
             case "pierce":return "忽略目标 " + (10*strength).ToString("0.#") + "% 护甲效果";
             case "critical":return "暴击率 +" + (6*strength).ToString("0.#") + "%";
             case "mana":return "击杀回复 " + (3*strength).ToString("0.#") + " 法力";
             case "light":return "翻滚冷却减少 " + (8*strength).ToString("0.#") + "%";
             case "greed":return "击杀金币 +" + (10*strength).ToString("0.#") + "%";default:return id;}
        }
        private void DrawTooltip()
        {
            if (dragging || hoverDefinition == null || confirmed != null) return;
            var text = TooltipText(hoverDefinition, hoverItem); const float width = 390;
            var style = new GUIStyle(normal) { padding = new RectOffset(18,18,14,14) }; var height = Mathf.Min(610, style.CalcHeight(new GUIContent(text), width));
            var pointer = Event.current.mousePosition; var x = pointer.x + 22; if (x + width > canvasWidth - 12) x = pointer.x - width - 15;
            var rect = new Rect(Mathf.Clamp(x,12,canvasWidth-width-12),Mathf.Clamp(pointer.y+16,12,canvasHeight-height-12),width,height);
            GUI.Box(rect,GUIContent.none,panel); GUI.Label(rect,text,style); UiTheme.Frame(rect, WorldArt.Tier(hoverDefinition.Tier), 2);
        }
        private string Description(Item item)
        {
            var d = Rules.Catalog.Get(item.Id); var c = ColorUtility.ToHtmlStringRGB(WorldArt.Tier(d.Tier));
            return "<color=#" + c + ">" + d.Name + "</color> ×" + item.Count + "  " + (d.Durability > 0 ? item.Durability + "/" + item.Maximum + "耐久  " : "")
                + "重量 " + (d.Weight * item.Count).ToString("F1") + (d.Power > 0 ? "  属性 " + d.Power : "")
                + (item.Enchants.Count > 0 ? "\n附魔：" + string.Join(" / ", item.Enchants.ConvertAll(EnchantName).ToArray()) : "");
        }
        private static string EnchantName(string id)
        {
            var ids = new[] { "sharp", "haste", "slaughter", "drain", "fire", "pierce", "critical", "mana", "light", "greed" };
            var names = new[] { "锋利", "迅捷", "屠戮", "嗜血", "燃焰", "碎甲", "会心", "回蓝", "轻盈", "贪婪" };
            var index = Array.IndexOf(ids, id); return index < 0 ? id : names[index];
        }
        private void Shop()
        {
            Text("装备库存由基础物品与撤离解锁物品组成。购买使用保险箱金币，物品进入随身容器。");
            SearchField();
            foreach (var d in Rules.Catalog.All)
            {
                var kind = page == "smith" ? ItemKind.Weapon : page == "armor" ? ItemKind.Armor : page == "potions" ? ItemKind.Potion : page == "scrolls" ? ItemKind.Scroll : ItemKind.Key;
                if (!(d.Kind == kind || page == "general" && (d.Kind == ItemKind.Rig || d.Kind == ItemKind.Pack)) || !Rules.ShopVisible(d) || filter.Length > 0 && !d.Name.Contains(filter)) continue;
                var price = (int)Math.Ceiling(d.Price * (1 - State.Skill("explore_discount") * .05f));
                GUILayout.BeginHorizontal(rowCard); DrawDefinitionIcon(d); GUILayout.Space(14); GUILayout.BeginVertical();
                Text("<color=#" + ColorUtility.ToHtmlStringRGB(WorldArt.Tier(d.Tier)) + "><b>" + d.Name + "</b></color>"); GUILayout.Label(DefinitionSummary(d) + " · " + d.Weight.ToString("F1") + " 重量", small); GUILayout.EndVertical();
                string error = null; if (Button("购买\n" + price + " 金币", State.Coins >= price, 150)) Action(r => r.Buy(d.Id, out error), () => error); GUILayout.EndHorizontal();
            }
            if (Button("出售与整理物品")) Open("inventory");
            if (page == "smith" || page == "armor")
            {
                if (Button("打造装备（矿石 ×3 + 基准价 50%）")) { craftKind = page == "smith" ? ItemKind.Weapon : ItemKind.Armor; Open("craft"); }
                Text("保养：费用 = 耐久上限 ×2；耐久上限减少 10。小屋维修 ×4，保留上限。");
                foreach (var item in State.Equipped) if (item != null && Rules.Catalog.Get(item.Id).Durability > 0)
                {
                    GUILayout.BeginHorizontal(); Text(Description(item)); string error = null;
                    var price=(int)Math.Ceiling(item.Maximum*2*(State.Skill("survival_wear_medium")>0?.8f:1));
                    if (Button("保养 " + price, item.Durability < item.Maximum, 150)) Action(r => r.Repair(item, true, out error), () => error); GUILayout.EndHorizontal();
                }
            }
        }
        private void Craft()
        {
            Text("素材从仓库消耗，产物放入随身容器。传说装备仅可通过打造获得。\n配方：对应档位矿石 ×3 + 物品基准价 50%。");
            GUILayout.BeginHorizontal(); for (var tier = 0; tier < 5; tier++) if (Button(GameRules.TierName(tier)) && craftTier != tier) { craftTier = tier; Relayout(); } GUILayout.EndHorizontal();
            SearchField(); var ore = "ore_" + craftTier; var oreCount=0;foreach(var material in State.Warehouse)if(material.Id==ore)oreCount+=material.Count;
            GUILayout.Label(Rules.Catalog.Get(ore).Name + "  " + oreCount + " 枚（每件消耗 3 枚）",small);
            foreach (var d in Rules.Catalog.All) if (d.Kind == craftKind && d.Tier == craftTier && (filter.Length == 0 || d.Name.Contains(filter)))
            { var cost=(int)Math.Ceiling(d.Price*.5f);GUILayout.BeginHorizontal(rowCard); DrawDefinitionIcon(d);GUILayout.Space(14);GUILayout.BeginVertical();Text("<color=#"+ColorUtility.ToHtmlStringRGB(WorldArt.Tier(d.Tier))+"><b>"+d.Name+"</b></color>");GUILayout.Label(DefinitionSummary(d),small);GUILayout.EndVertical(); string error = null;
                if (Button("打造\n" + cost + " 金币", oreCount>=3&&State.Coins>=cost, 150)) Action(r => Crafting.Craft(r, d.Id, craftTier, out error), () => error); GUILayout.EndHorizontal(); }
        }
        private void SearchField()
        {GUILayout.Label("查找物品",small);var next=GUILayout.TextField(filter,GUILayout.Height(34));if(next!=filter){filter=next;Relayout();}GUILayout.Space(7);}
        private static string DefinitionSummary(ItemDefinition d)
        {return GameRules.TierName(d.Tier)+(d.Kind==ItemKind.Weapon?" · 攻击 "+d.Power+" · 间隔 "+d.Interval.ToString("F2")+" 秒":d.Kind==ItemKind.Armor?" · 防御 "+d.Power+" · "+d.Set:d.Kind==ItemKind.Rig||d.Kind==ItemKind.Pack?" · 容量 "+d.Capacity+" 格":d.Kind==ItemKind.Potion?" · "+EffectName(d.Effect)+" "+d.Power:d.Kind==ItemKind.Scroll?" · "+EffectName(d.Effect):" · 隐藏房间补给");}
        private void DrawDefinitionIcon(ItemDefinition definition)
        {
            var rect = GUILayoutUtility.GetRect(64,64,GUILayout.Width(64),GUILayout.Height(64)); GUI.DrawTexture(rect,UiTheme.Inset);
            GUI.DrawTexture(new Rect(rect.x+3,rect.y+3,58,58),ItemIconAtlas.Get(definition)); UiTheme.Frame(rect,WorldArt.Tier(definition.Tier),1.5f);
            if(rect.Contains(Event.current.mousePosition)&&PointerVisible()){hoverDefinition=definition;hoverItem=null;}
        }
        private void Guild()
        {
            Text("冒险者 " + GameRules.TierName(State.Rank) + " · 累计经验 " + State.Experience + " · 同时最多三项任务");
            foreach (var d in QuestCatalog.All)
            {
                if (d.Rank > State.Rank) continue; var q = State.Quests.Find(x => x.Id == d.Id); string error = null;
                GUILayout.BeginHorizontal(); Text(d.Name + " · " + (q == null ? "未接取" : q.Claimed ? "已完成" : q.Progress + "/" + d.Target) + " · 奖励 " + d.Coins + "金币 / " + d.Xp + "经验");
                if (q == null && Button("接取", true, 100)) Action(r => r.AcceptQuest(d.Id, out error), () => error);
                if (q != null && !q.Claimed && Button("结算", q.Progress >= d.Target, 100)) Action(r => r.ClaimQuest(d.Id, out error), () => error); GUILayout.EndHorizontal();
            }
            Text("猎杀/探索进度死亡保留；搜刮与撤离目标须成功带出。成功撤离后在公会结算。");
            if (State.Chapter < 3)
            {
                var chapter = Story.Chapters[State.Chapter]; storyNode = Mathf.Clamp(storyNode, 0, chapter.Length - 1);
                GUILayout.Label(chapter[0], title); Text(chapter[storyNode == 0 ? 1 : storyNode]);
                if (Button(storyNode < chapter.Length - 1 ? "下一句" : "完成本章", State.Rank >= State.Chapter))
                { if (storyNode < chapter.Length - 1) { storyNode++; Relayout(); } else { string error = null; storyNode = 0; Action(r => Story.Advance(r.State, out error), () => error); } }
                string skipError = null; if (Button("跳过本章", State.Rank >= State.Chapter)) { storyNode = 0; Action(r => Story.Advance(r.State, out skipError), () => skipError); }
            }
            else Text("主线三章已完成。地下城探索与收集仍可继续。");
        }
        private void Hut()
        {
            Text("小屋 · 安全存取、维修、升级、成长与收藏");
            if (Button("整理仓库 / 取出补给")) Open("inventory");
            foreach (var area in new[] { "warehouse", "training", "collection" })
            {
                var level = area == "warehouse" ? State.WarehouseLevel : area == "training" ? State.TrainingLevel : State.CollectionLevel;
                var name = area == "warehouse" ? "仓库" : area == "training" ? "锻炼" : "收藏室"; string error = null;
                if (Button(name + " Lv." + level + (level < 5 ? " → 升级 " + new[] { 1000, 2500, 6000, 15000 }[level - 1] + " 金币" : " · 满级"), level < 5)) Action(r => r.Upgrade(area, out error), () => error);
            }
            Text("属性点 " + State.AttributePoints + " · 负重加点 " + State.WeightPoints + "（每点 +10）");
            string weightError = null; if (Button("负重 +1", State.AttributePoints > 0)) Action(r => r.SpendWeight(out weightError), () => weightError);
            Text("职业技能点 " + State.SkillPoints + " · 技能树（全部 95 点）");
            GUILayout.BeginHorizontal(); foreach (var group in new[] { "刀", "骑士剑", "长枪", "匕首", "法杖", "生存", "探索" }) if (Button(group)&&skillGroup!=group) { skillGroup = group; Relayout(); } GUILayout.EndHorizontal();
            foreach (var node in SkillTree.All)
            {
                if (node.Branch != skillGroup) continue; GUILayout.BeginHorizontal();
                Text(node.Name + "  " + State.Skill(node.Id) + "/" + node.Maximum + (node.Prerequisite == null ? "" : "\n前置 " + SkillTree.All.Find(n => n.Id == node.Prerequisite).Name + " ×" + node.Required));
                string error = null; if (Button("学习 · 1 点", SkillTree.CanLearn(State, node), 140)) Action(r => SkillTree.Learn(r.State, node.Id, out error), () => error); GUILayout.EndHorizontal();
            }
            foreach (var item in State.Equipped)
            {
                if (item == null || Rules.Catalog.Get(item.Id).Durability == 0) continue; GUILayout.BeginHorizontal(); Text(Description(item)); string error = null;
                var price=(int)Math.Ceiling(item.Maximum*4*(State.Skill("survival_wear_large")>0?.8f:1));
                if (Button("维修 " + price, item.Durability < item.Maximum, 140)) Action(r => r.Repair(item, false, out error), () => error); GUILayout.EndHorizontal();
            }
            Text("收藏陈列 " + State.Exhibits.Count + "/" + State.CollectionCapacity + " · 全集 625 件");
            foreach (var item in State.Warehouse.ToArray()) if (Rules.Catalog.Get(item.Id).Kind <= ItemKind.Armor)
            { string error = null; if (Button("陈列 " + Rules.Catalog.Get(item.Id).Name)) Action(r => r.Display(item, out error), () => error); }
            foreach (var item in State.Exhibits.ToArray()) { string error = null; if (Button("退回 " + Rules.Catalog.Get(item.Id).Name)) Action(r => r.Transfer(r.State.Exhibits, r.State.Warehouse, r.State.WarehouseCapacity, item, out error), () => error); }
        }
        private void Report()
        {
            var report = State.LastReport; if (report == null) { Text("暂无战绩"); return; }
            Text((report.Extracted ? "物资已安全带出。" : "以下随身物品、装备与金币已永久消失。仓库与成长保留。") + "\n到达 L" + report.Floor + " · 击杀 " + report.Kills + " · 金币 " + report.Coins);
            if (report.Unlocks.Count > 0) { GUILayout.Label("新解锁装备 " + report.Unlocks.Count + " 件！商店永久上架", title); foreach (var id in report.Unlocks) Text("✦ " + Rules.Catalog.Get(id).Name); }
            if (report.Extracted) { Text("职业者 Lv." + State.CareerLevel + " · 可用技能点 " + State.SkillPoints); if (State.RigItems.Count + State.PackItems.Count > 0) Text("仓库空间不足：剩余物资保留在随身容器，请整理。"); }
            GUILayout.Label(report.Extracted ? "本次携回物资" : "遗失物资", title);
            for(var row=0;row<(report.Items.Count+7)/8;row++)
            {GUILayout.BeginHorizontal();for(var col=0;col<8;col++){var index=row*8+col;if(index>=report.Items.Count)break;var rect=GUILayoutUtility.GetRect(82,82,GUILayout.Width(82),GUILayout.Height(82));DrawItemCell(rect,report.Items[index],"report","",false);GUILayout.Space(9);}GUILayout.EndHorizontal();GUILayout.Space(9);}
            if (Button("返回小镇，整备下一次远征")) Close();
        }
        private void Settings()
        {
            if (bindingCapture != null)
            {
                Text("按下新按键或鼠标按钮：" + bindingCapture);
                var e = Event.current;
                if (e.type == EventType.KeyDown || e.type == EventType.MouseDown)
                { var key = e.type == EventType.MouseDown ? (KeyCode)((int)KeyCode.Mouse0 + e.button) : e.keyCode; string error;
                    if (GameInput.Rebind(bindingCapture, key, out error)) bindingCapture = null; else Toast(error); e.Use(); Relayout(); }
            }
            Text("主音量"); State.MasterVolume = GUILayout.HorizontalSlider(State.MasterVolume, 0, 1);
            Text("音乐音量"); State.MusicVolume = GUILayout.HorizontalSlider(State.MusicVolume, 0, 1);
            Text("音效音量"); State.EffectsVolume = GUILayout.HorizontalSlider(State.EffectsVolume, 0, 1);
            Text("镜头缩放 " + State.Zoom.ToString("F2")); State.Zoom = GUILayout.HorizontalSlider(State.Zoom, 0.85f, 1.15f);
            GUILayout.BeginHorizontal(); for (var q = 0; q < 3; q++) { var quality = q; if (Button(new[] { "低画质", "中画质", "高画质" }[q])) State.Quality = quality; } GUILayout.EndHorizontal();
            for (var i = 0; i < GameInput.Actions.Length; i++) if (Button(GameInput.Labels[i] + "：" + KeyName(GameInput.Key(GameInput.Actions[i])))) { bindingCapture = GameInput.Actions[i]; Relayout(); }
            if (Button("恢复默认设置")) { State.Bindings.Clear(); State.MasterVolume = .6f; State.MusicVolume = .5f; State.EffectsVolume = .8f; State.Zoom = 1; State.Quality = 1; bindingCapture = null; Relayout(); }
            if (Button("保存设置")) { AudioListener.volume = State.MasterVolume; QualitySettings.SetQualityLevel(State.Quality); if (Camera.main != null && Dungeon != null) Camera.main.orthographicSize = 7 * State.Zoom;
                if (State.ActiveRun == null) { if (GameSession.Save()) Toast("设置已保存"); } else Toast("设置已应用，将随最近安全点保存"); }
        }
        private void Help()
        {
            Text("搜 · 按住 E 搜索箱子、书架和补给；钥匙可以开启隐藏房。\n打 · 鼠标瞄准、左键攻击。法杖按住蓄力后释放；Space 翻滚有 0.3 秒无敌，冷却 1.5 秒。\n撤 · L2 与 L4 的绿色地标可立即撤离。L5 没有撤离点，请返回 L4。\n\nWASD 移动 · I 背包 · M 地图 · 1/2/3 使用胸挂补给 · Esc 暂停\n\n超过 80% 负重减速，超过 100% 无法翻滚，超过 120% 无法拾取。耐久归零的武器变为空手；回镇保养或维修。\n\n保险箱与仓库安全，随身金币与装备死亡永久消失。撤离会把物资放入仓库，出发前重新取出药剂。先在公会接任务，再开始远征。");
        }
        private void Pause()
        {
            if (Button("继续")) Close(); if (Button("设置")) Open("settings"); if (Button("远征指南")) Open("help");
            if (Dungeon != null)
            {
                Text("退出后可从最近清房/进层安全点继续。");
                if (Button("返回主菜单")) { if (GameSession.RestoreCheckpoint()) Navigate("Lobby"); else Toast(GameSession.SaveError); }
                if (Button("放弃本局")) Confirm("永久放弃本局？", "所有随身物品、装备、容器和金币永久删除。", () => Dungeon.End(false));
            }
            else if (Button("返回主菜单")) Navigate("Lobby");
        }
        private void DrawHud(float width)
        {
            var run = State.ActiveRun; if (run == null) return;
            var box = new Rect(18,18,365,191); GUI.Box(box,GUIContent.none,panel);
            GUI.Label(new Rect(37,27,330,30),"骑士  Lv." + State.CareerLevel + "   ·   " + GameRules.TierName(State.Rank),hudText);
            DrawBar(new Rect(39,65,321,28),run.Hp,Rules.MaximumHp,new Color(.68f,.15f,.16f),"HP  " + Mathf.CeilToInt(run.Hp) + " / " + Mathf.CeilToInt(Rules.MaximumHp));
            DrawBar(new Rect(39,101,321,22),run.Mp,Rules.MaximumMp,new Color(.13f,.36f,.65f),"MP  " + Mathf.CeilToInt(run.Mp) + " / " + Mathf.CeilToInt(Rules.MaximumMp));
            var ratio = Rules.Weight / Rules.CarryLimit; var weightColor = ratio > 1 ? "E96850" : ratio > .8f ? "E0B565" : "A9B0A5";
            GUI.Label(new Rect(38,127,325,28),"<color=#" + weightColor + ">负重 " + Rules.Weight.ToString("F1") + " / " + Rules.CarryLimit + "</color>    金币 " + State.PocketCoins,hudSmall);
            var weapon = State.Equipped[0]; GUI.Label(new Rect(38,153,326,36),weapon == null || weapon.Durability <= 0 ? "空手 · 回镇整备武器" : Rules.Catalog.Get(weapon.Id).Name + "   " + weapon.Durability + "/" + weapon.Maximum,hudSmall);
            GUI.Box(new Rect(width-318,18,300,104),GUIContent.none,panel);
            GUI.Label(new Rect(width-298,28,262,44),"L" + run.Floor + "  " + DungeonScenery.Themes[Mathf.Clamp(run.Floor-1,0,DungeonScenery.Themes.Length-1)],hudHeading);
            GUI.Label(new Rect(width-297,74,260,29),"难度 T" + (run.Difficulty+1) + "    " + (run.Floor==2||run.Floor==4 ? "本层可撤离" : run.Floor==5 ? "返回 L4 撤离" : "深入寻找出口"),hudSmall);
            var bottom = canvasHeight - 127; var barRect = new Rect((width-606)/2,bottom,606,109); GUI.Box(barRect,GUIContent.none,panel);
            var quick = State.RigItems.FindAll(i => Rules.Catalog.Get(i.Id).Kind == ItemKind.Potion || Rules.Catalog.Get(i.Id).Kind == ItemKind.Scroll);
            for (var i=0;i<3;i++)
            {var rect=new Rect(barRect.x+18+i*82,bottom+15,69,69);DrawItemCell(rect,i<quick.Count?quick[i]:null,"rig","",false);DrawCooldown(rect,Dungeon.QuickCooldown,5);GUI.Label(new Rect(rect.x,rect.yMax-1,69,24),KeyName(GameInput.Key("quick"+(i+1))),caption);}
            for(var i=1;i<=2;i++) DrawSkillCell(new Rect(barRect.x+284+(i-1)*98,bottom+15,80,69),i);
            var dodgeRect=new Rect(barRect.x+493,bottom+15,88,69);GUI.DrawTexture(dodgeRect,UiTheme.Inset);UiTheme.Frame(dodgeRect,UiTheme.Gold*.65f);GUI.Label(new Rect(dodgeRect.x,dodgeRect.y+12,88,36),"翻滚",hudCentered);DrawCooldown(dodgeRect,Dungeon.DodgeCooldown,Rules.RollCooldown);GUI.Label(new Rect(dodgeRect.x-4,dodgeRect.yMax-1,96,24),KeyName(GameInput.Key("dodge")),caption);
            var footerStyle=new GUIStyle(hudSmall){fontSize=13,wordWrap=true};var footerWidth=Mathf.Max(190,barRect.x-42);
            GUI.Label(new Rect(23,canvasHeight-67,footerWidth,47),KeyName(GameInput.Key("inventory"))+" 行囊  ·  "+KeyName(GameInput.Key("map"))+" 地图  ·  "+KeyName(GameInput.Key("pause"))+" 暂停",footerStyle);
            if(!string.IsNullOrEmpty(Dungeon.Prompt) || Dungeon.SearchProgress>0)
            {
                var prompt=Dungeon.Prompt??"";var textHeight=Mathf.Max(50,hintText.CalcHeight(new GUIContent(prompt),480));var searching=Dungeon.SearchProgress>0;
                var height=textHeight+24+(searching?25:0);var hint=new Rect((width-516)/2,bottom-height-18,516,height);
                GUI.Box(hint,GUIContent.none,panel);GUI.Label(new Rect(hint.x+18,hint.y+12,480,textHeight),prompt,hintText);
                if(searching)DrawBar(new Rect(hint.x+24,hint.yMax-28,468,18),Dungeon.SearchProgress,1,UiTheme.Gold,"搜索 " + (Dungeon.SearchProgress*100).ToString("F0") + "%");
            }
            if(Dungeon.Charge>0)DrawBar(new Rect((width-260)/2,bottom-32,260,17),Dungeon.Charge,2,new Color(.51f,.32f,.8f),"奥术蓄力");
            if (map)
            {
                var rect = new Rect(width - 510, 134, 490, 480); GUI.Box(rect, GUIContent.none, panel); GUI.Label(new Rect(rect.x+22,rect.y+12,435,44),"已探索地图 · "+KeyName(GameInput.Key("map"))+" 关闭",hudHeading); var f = Dungeon.Floor;
                var minX = 0; var minY = 0; var maxX = 1; var maxY = 1;
                foreach (var r in f.Rooms) if (r.Visited) { minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y); maxX = Math.Max(maxX, r.X); maxY = Math.Max(maxY, r.Y); }
                var size = Mathf.Min(55, 390f / Mathf.Max(maxX - minX + 1, maxY - minY + 1));
                for (var i = 0; i < f.Rooms.Count; i++) { var r = f.Rooms[i]; if (!r.Visited) continue;
                    var rr = new Rect(rect.x + 35 + (r.X - minX) * size, rect.y + 70 + (maxY - r.Y) * size, size - 5, size - 5);
                    GUI.color = i == f.ExtractRoom ? Color.green : Color.white; GUI.Box(rr, i == f.ExtractRoom ? "撤" : i == f.DownRoom ? "↓" : i == 0 ? "↑" : "", GUI.skin.box); GUI.color = Color.white;
                    if(Mathf.Abs(Dungeon.Position.x-r.X*16)<6&&Mathf.Abs(Dungeon.Position.y-r.Y*16)<6)GUI.Label(new Rect(rr.x+5,rr.y+5,35,30),"●",hudText);
                }
                if(State.Skill("explore_exit")>0&&f.ExtractRoom>=0&&!f.Rooms[f.ExtractRoom].Visited)
                { var exit=f.Rooms[f.ExtractRoom];GUI.Label(new Rect(rect.x+25,rect.y+410,430,40),"撤离方向："+Compass(new Vector2(exit.X*16,exit.Y*16)-Dungeon.Position),hudText); }
                if(State.Skill("explore_trace")>0)
                {var candidates=f.Searches.FindAll(s=>!s.Searched&&!f.Rooms[s.Room].Visited);var text="补给线索：";
                    for(var i=0;i<Math.Min(2,candidates.Count);i++)text+=Compass(new Vector2(candidates[i].X,candidates[i].Y)-Dungeon.Position)+"  ";
                    GUI.Label(new Rect(rect.x+25,rect.y+365,430,40),text,hudText);}
            }
        }
        private void DrawBar(Rect rect,float value,float maximum,Color color,string label)
        {
            GUI.DrawTexture(rect,UiTheme.Inset);var ratio=Mathf.Clamp01(value/Mathf.Max(1,maximum));
            var fill=new Rect(rect.x+3,rect.y+3,(rect.width-6)*ratio,Mathf.Max(2,rect.height-6));UiTheme.Fill(fill,color);UiTheme.Fill(new Rect(fill.x,fill.y,fill.width,Mathf.Max(1,fill.height*.26f)),Color.Lerp(color,Color.white,.2f));
            for(var i=1;i<4;i++)UiTheme.Fill(new Rect(rect.x+rect.width*i/4,rect.y+3,1,rect.height-6),new Color(.025f,.028f,.03f,.6f));
            UiTheme.Frame(rect,UiTheme.Gold*.6f);var style=new GUIStyle(caption){fontSize=rect.height<20?11:14,padding=new RectOffset(),wordWrap=false};style.normal.textColor=UiTheme.Text;GUI.Label(rect,label,style);
        }
        private void DrawCooldown(Rect rect,float remaining,float maximum)
        {
            if(remaining<=0)return;var height=(rect.height-5)*Mathf.Clamp01(remaining/Mathf.Max(1,maximum));UiTheme.Fill(new Rect(rect.x+3,rect.yMax-3-height,rect.width-6,height),new Color(.025f,.025f,.033f,.78f));GUI.Label(rect,remaining.ToString("F1"),hudCentered);
        }
        private void DrawSkillCell(Rect rect,int number)
        {
            GUI.DrawTexture(rect,UiTheme.Inset);var weapon=Rules.Weapon;var learned=weapon!=null&&State.Skill(weapon.Branch+"_skill"+number)>0;
            var index=weapon==null?-1:Array.IndexOf(new[]{"blade","sword","spear","dagger","staff"},weapon.Branch);
            var names=number==1?new[]{"旋风斩","格挡反击","贯穿突刺","影袭","奥术洪流"}:new[]{"斩首姿态","守护之壁","横扫千军","毒刃","陨星术"};
            if(weapon!=null){var previous=GUI.color;GUI.color=learned?Color.white:new Color(.4f,.4f,.4f,.8f);GUI.DrawTexture(new Rect(rect.x+15,rect.y+2,50,50),ItemIconAtlas.Get(weapon));GUI.color=previous;}
            GUI.Label(new Rect(rect.x-5,rect.y+40,rect.width+10,25),index<0?"无武器":names[index],caption);UiTheme.Frame(rect,learned?new Color(.42f,.58f,.75f):new Color(.3f,.3f,.29f));
            if(!learned){UiTheme.Fill(new Rect(rect.x+3,rect.y+3,rect.width-6,36),new Color(.02f,.025f,.033f,.55f));GUI.Label(new Rect(rect.x,rect.y+8,rect.width,26),"未学习",caption);}
            else {var maximum=number==1?new[]{12f,15f,10f,8f,18f}[index]:new[]{20f,18f,14f,16f,20f}[index];if(State.Skill(weapon.Branch+"_enhance"+number)>0)maximum*=.85f;DrawCooldown(rect,number==1?Dungeon.Skill1Cooldown:Dungeon.Skill2Cooldown,maximum);}
            GUI.Label(new Rect(rect.x,rect.yMax-1,rect.width,24),KeyName(GameInput.Key(number==1?"skill1":"skill2")),caption);
        }
        private static string KeyName(KeyCode key)
        {
            if(key>=KeyCode.Alpha0&&key<=KeyCode.Alpha9)return ((int)key-(int)KeyCode.Alpha0).ToString();
            if(key>=KeyCode.Keypad0&&key<=KeyCode.Keypad9)return "小键盘"+((int)key-(int)KeyCode.Keypad0);
            switch(key)
            {case KeyCode.Space:return "空格";case KeyCode.Escape:return "Esc";case KeyCode.Return:return "Enter";case KeyCode.KeypadEnter:return "Enter";
             case KeyCode.Mouse0:return "鼠标左键";case KeyCode.Mouse1:return "鼠标右键";case KeyCode.Mouse2:return "鼠标中键";
             case KeyCode.LeftShift:return "左 Shift";case KeyCode.RightShift:return "右 Shift";case KeyCode.LeftControl:return "左 Ctrl";case KeyCode.RightControl:return "右 Ctrl";
             case KeyCode.LeftAlt:return "左 Alt";case KeyCode.RightAlt:return "右 Alt";case KeyCode.UpArrow:return "↑";case KeyCode.DownArrow:return "↓";case KeyCode.LeftArrow:return "←";case KeyCode.RightArrow:return "→";
             default:return key.ToString();}
        }
        private static string Compass(Vector2 direction) { return new[]{"南","东南","东","东北","北","西北","西","西南"}[TopDownPlayerController.DirectionRow(direction)]; }
    }
}
