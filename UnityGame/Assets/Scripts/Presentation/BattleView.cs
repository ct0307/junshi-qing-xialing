using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class BattleView : IDisposable
    {
        public BattleSession Session { get; private set; }
        private readonly GameFlow game;
        private readonly UiFactory ui;
        private readonly GameDatabase database;
        private readonly LevelDefinition level;
        private readonly List<string> roster;
        private readonly RectTransform unitLayer;
        private readonly RectTransform labelLayer;
        private readonly RectTransform cardLayer;
        private readonly RectTransform root;
        private readonly BattleEffects effects;
        private readonly BattleFeedbackState feedback = new BattleFeedbackState(.7f);
        private readonly List<BattleFeedbackResult> feedbackResults = new List<BattleFeedbackResult>();
        private readonly List<CombatFieldSnapshot> feedbackFields = new List<CombatFieldSnapshot>();
        private readonly List<EnemyFeedbackSnapshot> feedbackEnemies = new List<EnemyFeedbackSnapshot>();
        private readonly BattleIntelView intel;
        private readonly BattlePlacementGuide placementGuide;
        private readonly HeroSkillPreview skillPreview;
        private readonly Dictionary<int, UnitView> enemyViews = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, UnitView> justRetiredViews = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, UnitView> heroViews = new Dictionary<int, UnitView>();
        private readonly Dictionary<string, UnitView> heroViewsById = new Dictionary<string, UnitView>();
        private readonly Stack<UnitView> enemyPool = new Stack<UnitView>();
        private readonly List<UnitView> retiringViews = new List<UnitView>();
        private readonly Dictionary<int, SlotView> slots = new Dictionary<int, SlotView>();
        private readonly List<int> removeIds = new List<int>();
        private readonly HashSet<int> aliveIds = new HashSet<int>();
        private readonly List<UnitView> sorting = new List<UnitView>();
        private readonly List<CardView> cards = new List<CardView>();
        private readonly Text goldText;
        private readonly Text incomeText;
        private readonly Text castleText;
        private readonly Text castleStateText;
        private readonly Text waveText;
        private readonly Text phaseText;
        private readonly Text elapsedText;
        private readonly Text lossText;
        private readonly Text notice;
        private readonly Text detailName;
        private readonly Text detailInfo;
        private readonly Text detailRole;
        private readonly Image castleFill;
        private readonly Image range;
        private readonly Image nextRange;
        private readonly Image selection;
        private readonly MarchSpeedControls speedControls;
        private readonly UiButton pauseButton;
        private readonly UiButton actionButton;
        private readonly UiButton detailButton;
        private RectTransform detailOverlay;
        private bool resumeAfterDetails;
        private readonly bool showHuangjinTutorial;
        private string selectedHero;
        private int selectedSlot = -1;
        private int previewSlot = -1;
        private int hoveredSlot = -1;
        private int cardPage;
        private int lastGold = -1;
        private int lastSupplyAdvance = -1;
        private bool ended;
        private float lastTime;
        private float noticeUntil;
        private bool priorityNotice;
        private float refreshAt;
        private float lossUntil;

        public BattleView(GameFlow game, UiFactory ui, RectTransform root, GameDatabase database,
            LevelDefinition level, List<string> roster)
        {
            this.game = game;
            this.ui = ui;
            this.database = database;
            this.level = level;
            this.roster = new List<string>(roster);
            this.root = root;
            showHuangjinTutorial = level.Id == "huangjin" && (game == null || !game.Progress.IsCleared(level.Id));
            Session = new BattleSession(database, level, roster, true);
            ui.Box(root, "战场底色", 0, 0, 1920, 1080, BattlePalette.Silk, false);
            RectTransform ground = BattleLayout.CreateGround(ui, root, level.MapResource);
            HuangjinScenery.Create(ui, ground, level);
            RectTransform slotLayer = ui.Rect(ground, "可交互阵位", 0, 0, 1920, 1080);
            foreach (SlotDefinition definition in level.Slots)
            {
                int slotId = definition.Id;
                // 点击背景抵消地面纵缩；底座仍保持沙盘透视，规则中心不变。
                float hitHeight = 88 / BattleLayout.GroundScale;
                UiButton button = ui.Button(slotLayer, "", definition.Position.X - 60, definition.Position.Y - hitHeight / 2,
                    120, hitHeight, () => ClickSlot(slotId), Color.clear, 20, false);
                button.Rect.gameObject.AddComponent<SlotHitArea>().Configure(slotId, ground, level.Slots);
                Image marker = ui.Circle(button.Rect, "阵位底座", 13, hitHeight / 2 - 24, 94, 48, new Color(.55f, .65f, .47f, .12f));
                ui.Border(marker, new Color(.23f, .47f, .38f, .36f), 1.5f);
                Text label = ui.Label(button.Rect, "", 13, hitHeight / 2 - 25, 94, 48, 24, UiFactory.Ink, TextAnchor.MiddleCenter);
                EventTrigger trigger = button.Rect.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(data =>
                { hoveredSlot = slotId; RefreshPlacement(); });
                trigger.triggers.Add(enter);
                EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(data => { if (hoveredSlot == slotId) hoveredSlot = -1; RefreshPlacement(); });
                trigger.triggers.Add(exit);
                slots.Add(slotId, new SlotView(button, marker, label, definition.Position));
            }
            placementGuide = ui.Rect(ground, "道路覆盖预览", 0, 0, 1920, 1080).gameObject.AddComponent<BattlePlacementGuide>();
            placementGuide.gameObject.SetActive(false);
            skillPreview = ui.Rect(ground, "成长技能形状", 0, 0, 1920, 1080).gameObject.AddComponent<HeroSkillPreview>();
            skillPreview.gameObject.SetActive(false);
            isolationGuide = ui.Rect(ground, "孤阵距离预览", 0, 0, 1920, 1080).gameObject.AddComponent<FactionDistanceGuide>();
            isolationGuide.gameObject.SetActive(false);
            range = ui.Circle(ground, "攻击范围", 0, 0, 100, 100, new Color(.92f, .85f, .58f, .65f), true);
            range.gameObject.SetActive(false);
            nextRange = ui.Circle(ground, "下一阶攻击范围", 0, 0, 100, 100, new Color(.48f, .87f, .82f, .8f), true);
            nextRange.gameObject.SetActive(false);
            selection = ui.Circle(ground, "选中阵位", 0, 0, 112, 62, UiFactory.Gold, true);
            selection.gameObject.SetActive(false);
            RectTransform fieldLayer = ui.Rect(ground, "配合区域", 0, 0, 1920, 1080);
            unitLayer = ui.Rect(ground, "棋兵", 0, 0, 1920, 1080);
            RectTransform effectLayer = ui.Rect(ground, "战斗反馈", 0, 0, 1920, 1080);
            effects = new BattleEffects(ui, effectLayer, fieldLayer);
            labelLayer = ui.Rect(ground, "战场名签", 0, 0, 1920, 1080);
            ui.Box(root, "上方信息栏", 0, 0, 1920, 176, BattlePalette.Silk, false);
            // 资源与操作占第一行，敌情独占第二行；收入不再落入战况标题区域。
            ui.Box(root, "顶栏行分隔", 24, 100, 1872, 1, BattlePalette.Line, false);
            ui.Box(root, "独立战况带", 24, 104, 1872, 64, Color.Lerp(BattlePalette.Silk, BattlePalette.Mist, .4f), false);
            ui.Box(root, "军饷分隔", 510, 20, 1, 56, BattlePalette.Line, false);
            ui.Box(root, "城防分隔", 850, 20, 1, 56, BattlePalette.Line, false);
            ui.Box(root, "用时分隔", 1220, 20, 1, 56, BattlePalette.Line, false);
            UiButton backButton = ui.Button(root, "返回", 24, 8, 112, 80, () =>
            {
                if (game == null) return;
                GameAudio.Button();
                game.HandleBack();
            }, BattlePalette.Mist, 26, false);
            BattleTypography.Flat(backButton.Background);
            ui.Border(backButton.Background, BattlePalette.Line, 1);
            backButton.Label.color = BattlePalette.Ink;
            backButton.Rect.name = "战场返回";
            backButton.Button.interactable = game != null;
            Text levelTitle = ui.Label(root, level.Name, 160, 10, 230, 68, 40, BattlePalette.Ink);
            levelTitle.gameObject.name = "关卡标题";
            BattleTypography.Display(levelTitle, 40);
            if (levelTitle.preferredWidth > 230) BattleTypography.Display(levelTitle, 26);
            phaseText = ui.Label(root, "布阵中", 400, 23, 90, 42, 20, BattlePalette.Jade);
            phaseText.gameObject.name = "战斗阶段";
            ui.Label(root, "军饷", 530, 16, 60, 60, 22, BattlePalette.Muted);
            goldText = ui.Label(root, "", 602, 16, 104, 60, 34, BattlePalette.Ink);
            goldText.gameObject.name = "军饷数值";
            goldText.fontStyle = FontStyle.Bold;
            incomeText = ui.Label(root, "", 718, 16, 126, 60, 20, BattlePalette.Muted);
            incomeText.gameObject.name = "军饷实秒收入";
            ui.Label(root, "城防", 874, 16, 52, 60, 22, BattlePalette.Muted);
            castleText = ui.Label(root, "", 936, 16, 128, 60, 30, BattlePalette.Ink);
            castleText.gameObject.name = "城防数值";
            castleStateText = ui.Label(root, "", 1076, 16, 66, 60, 18, BattlePalette.Danger);
            castleStateText.gameObject.name = "城防状态";
            ui.Box(root, "城防槽", 936, 80, 128, 4, BattlePalette.Line, false);
            castleFill = ui.Box(root, "城防", 936, 80, 128, 4, BattlePalette.Jade, false);
            lossText = ui.Label(root, "", 1148, 16, 60, 60, 24, BattlePalette.Danger);
            lossText.gameObject.name = "城防损失";
            ui.Label(root, "用时", 1234, 16, 48, 60, 22, BattlePalette.Muted);
            elapsedText = ui.Label(root, "00:00", 1294, 16, 100, 60, 32, BattlePalette.Ink);
            elapsedText.fontStyle = FontStyle.Bold;
            elapsedText.gameObject.name = "实际用时";
            waveText = ui.Label(root, "", 32, 108, 142, 46, 26, BattlePalette.Ink);
            waveText.gameObject.name = "波次进度";
            intel = new BattleIntelView(ui, root, database, level);
            Text marchLabel = ui.Label(root, "进军", 1490, 8, 48, 80, 22, BattlePalette.Muted);
            marchLabel.name = "进军组名";
            speedControls = new MarchSpeedControls(ui, root, 1542, 8, "进军倍率", ChangeMarchSpeed);
            UiButton rules = ui.Button(root, "规则", 1406, 8, 72, 80, ShowBattleRules, BattlePalette.Silk, 24);
            BattleTypography.Flat(rules.Background);
            rules.Label.color = BattlePalette.Jade;
            rules.Rect.name = "战斗规则";
            pauseButton = ui.Button(root, "开战", 1778, 8, 118, 80, () =>
            {
                if (Session.State == BattleState.Preparation) StartBattle();
                else if (game != null) { GameAudio.Button(); game.PauseBattle(); }
            }, BattlePalette.Jade, 26, false);
            BattleTypography.Flat(pauseButton.Background);
            ui.Border(pauseButton.Background, BattlePalette.Ink, 1.5f);
            pauseButton.Label.color = BattlePalette.Silk;
            pauseButton.Rect.name = "战场主操作";
            ui.Box(root, "下方操作栏", 0, 832, 1920, 248, BattlePalette.Silk, false);
            Text rosterTitle = ui.Label(root, "随军将册", 36, 844, 160, 40, 26, BattlePalette.Ink);
            BattleTypography.Display(rosterTitle, 26);
            ui.Box(root, "军令状态底", 211, 836, 1061, 64, BattlePalette.Silk, false);
            notice = ui.Label(root, "", 223, 891, 1049, 28, 18, BattlePalette.Muted);
            notice.gameObject.name = "军令通知";
            BuildFactionBar();
            cardLayer = ui.Rect(root, "五张武将卡", 0, 0, 1455, 1080);
            ui.Box(root, "详情分隔", 1452, 844, 1, 215, BattlePalette.Line, false);
            Image growthPanel = ui.Box(root, "成长浅玉底", 1470, 840, 408, 228, BattlePalette.Mist, false);
            ui.Border(growthPanel, BattlePalette.Line, 1);
            detailName = ui.Label(root, "布阵迎敌", 1486, 842, 272, 42, 28, BattlePalette.Ink);
            BattleTypography.Display(detailName, 28);
            detailName.gameObject.name = "成长标题";
            detailButton = ui.Button(root, "详情", 1786, 832, 92, 88, ShowHeroDetails, BattlePalette.Mist, 22);
            BattleTypography.Flat(detailButton.Background);
            detailButton.Label.color = BattlePalette.Jade;
            detailButton.Rect.name = "武将详情";
            detailRole = ui.Label(root, "", 1486, 884, 382, 35, 20, BattlePalette.Jade);
            detailRole.gameObject.name = "成长状态";
            detailInfo = ui.Label(root, "先点武将卡，再点空阵位招募。", 1486, 923, 382, 70, 22, BattlePalette.Ink, TextAnchor.UpperLeft);
            detailInfo.gameObject.name = "成长变化";
            actionButton = ui.Button(root, "选择武将后部署", 1486, 996, 382, 72, UsePrimaryAction, BattlePalette.Mist, 24, false);
            BattleTypography.Flat(actionButton.Background);
            ui.Border(actionButton.Background, BattlePalette.Line, 1);
            actionButton.Label.color = BattlePalette.Ink;
            ui.Box(root, "成长区青玉线", 1470, 840, 408, 2, BattlePalette.Jade, false);
            ui.Box(root, "军令栏铜线", 24, 172, 1872, 2, BattlePalette.Copper, false);
            ui.Box(root, "将册铜线", 24, 832, 1872, 2, BattlePalette.Copper, false);
            BuildCards();
            RefreshHud();
            SetNotice(GetTutorialHint() ?? "先选武将，再点空位部署；准备好后点击右上开战。");
        }

        private void StartBattle()
        {
            if (Session.State != BattleState.Preparation) return;
            Session.Start();
            GameAudio.Button();
            GameAudio.SyncBattle(Session);
            SetNotice("敌军来袭！敌将抵城扣城防，城防归零才失败；守完全部波次且城防仍存即可胜利。");
            RefreshHud();
        }

        private void ClickSlot(int slotId)
        {
            rangeUntil = Session.Time + 1.6f;
            HeroUnit unit = Session.Heroes.Find(h => h.SlotId == slotId);
            if (unit != null)
            {
                GameAudio.Button();
                selectedSlot = slotId;
                selectedHero = null;
                SetNotice("已选" + unit.Definition.Name + (Session.State == BattleState.Preparation ? " · 可点空位调位，或查看升阶。" : " · 可查看升阶收益。"));
            }
            else if (!string.IsNullOrEmpty(selectedHero))
            {
                string error = Session.TryRecruit(selectedHero, slotId);
                if (error != null) { GameAudio.Reject(); SetNotice(error, true); }
                else
                {
                    selectedSlot = slotId;
                    selectedHero = null;
                    SyncUnits();
                    HandleEvents();
                }
            }
            else if (Session.Heroes.Exists(h => h.SlotId == selectedSlot))
            {
                string error = Session.TryMoveHero(selectedSlot, slotId);
                if (error != null) { GameAudio.Reject(); SetNotice(error, true); }
                else
                {
                    GameAudio.Button();
                    selectedSlot = slotId;
                    hoveredSlot = -1;
                    SyncUnits();
                    HandleEvents();
                }
            }
            else
            {
                GameAudio.Reject();
                selectedSlot = -1;
                SetNotice("先点下方武将卡，再点空阵位。点击守将可查看升级。", true);
            }
            if (selectedHero == null) previewSlot = -1;
            BuildCards();
            RefreshHud();
        }

        private void SelectHero(string id)
        {
            rangeUntil = Session.Time + 1.6f;
            selectedSlot = -1;
            if (Session.IsRecruited(id))
            {
                HeroUnit unit = Session.Heroes.Find(h => h.Definition.Id == id);
                if (unit != null) selectedSlot = unit.SlotId;
                selectedHero = null;
                SetNotice(Session.State == BattleState.Preparation ?
                    "该武将已出阵 · 可点空位调位，或查看升阶。" :
                    "该武将已出阵 · 可查看升阶收益。");
            }
            else
            {
                selectedHero = selectedHero == id ? null : id;
                previewSlot = selectedHero == null ? -1 : FindPreviewSlot(database.FindHero(selectedHero));
                HeroDefinition hero = database.FindHero(id);
                bool unaffordable = !Session.CanAffordTransaction(hero.Cost);
                SetNotice(unaffordable ? "军饷不足：招募" + hero.Name + "需要" + hero.Cost + "。" :
                    "已选" + hero.Name + " · 点击空阵位部署。", unaffordable);
            }
            BuildCards();
            RefreshHud();
        }

        private void BuildCards()
        {
            cards.Clear();
            for (int i = cardLayer.childCount - 1; i >= 0; i--)
            { Transform child = cardLayer.GetChild(i); RemoveObject(child.gameObject); }
            int start = cardPage * 5;
            for (int i = 0; i < 5 && start + i < roster.Count; i++)
            {
                HeroDefinition hero = database.FindHero(roster[start + i]);
                if (hero == null) continue;
                UiButton card = ui.Button(cardLayer, "", 36 + i * 280, 922, 266, 144, () => SelectHero(hero.Id), BattlePalette.Silk);
                card.Rect.name = "战斗武将卡";
                BattleTypography.Flat(card.Background);
                ui.Border(card.Background, BattlePalette.Line, 1.5f);
                ui.Picture(card.Rect, UiFactory.PortraitPath(hero.Id), 6, 5, 104, 132);
                // 状态独占上沿；姓名、定位和军饷共用左边界，避免整张卡反白抢过战场。
                Image statePanel = ui.Box(card.Rect, "出阵状态", 154, 8, 104, 28, BattlePalette.Mist, false);
                Text state = ui.Label(statePanel.transform, "", 0, 0, 104, 28, 18, BattlePalette.Jade, TextAnchor.MiddleCenter);
                Text name = ui.Label(card.Rect, hero.Name, 116, 34, 142, 44, 32, BattlePalette.Ink);
                BattleTypography.Display(name, 32);
                Text role = ui.Label(card.Rect, hero.Role, 116, 78, 142, 29, 20, BattlePalette.Muted);
                Text cost = ui.Label(card.Rect, "军饷 " + hero.Cost, 116, 108, 142, 31, 22, BattlePalette.Ink);
                Image focus = ui.Box(card.Rect, "选将记号", 126, -3, 14, 6, BattlePalette.Jade, false);
                focus.gameObject.SetActive(false);
                cards.Add(new CardView(hero, card, name, role, cost, state, statePanel, focus));
            }
            int pages = Mathf.Max(1, Mathf.CeilToInt(roster.Count / 5f));
            UiButton next = ui.Button(cardLayer, pages > 1 ? (cardPage + 1) + "/" + pages + "  ›" : "1/1", 1300, 832, 130, 72,
                () => { cardPage = (cardPage + 1) % pages; BuildCards(); }, Color.clear, 22);
            BattleTypography.Flat(next.Background);
            next.Rect.name = "将册分页";
            next.Label.color = BattlePalette.Jade;
            next.Button.interactable = pages > 1;
            RefreshCards();
        }

        // 军饷收入只改文字和颜色，保留卡片、头像与按钮组件。
        private void RefreshCards()
        {
            lastSupplyAdvance = Session.AvailableSupplyAdvance;
            HeroUnit selected = Session.Heroes.Find(h => h.SlotId == selectedSlot);
            foreach (CardView card in cards)
            {
                bool recruited = Session.IsRecruited(card.Definition.Id);
                HeroUnit deployed = Session.Heroes.Find(h => h.Definition.Id == card.Definition.Id);
                bool active = selectedHero == card.Definition.Id || (selected != null && selected.Definition.Id == card.Definition.Id);
                card.Button.Background.color = active ? BattlePalette.Mist : BattlePalette.Silk;
                card.Border.effectColor = active ? BattlePalette.Jade : BattlePalette.Line;
                card.Focus.gameObject.SetActive(active);
                card.Name.color = BattlePalette.Ink;
                card.Role.color = BattlePalette.Muted;
                bool affordable = Session.CanAffordTransaction(card.Definition.Cost);
                card.Cost.color = !affordable && !recruited ? BattlePalette.Danger : BattlePalette.Ink;
                string state = recruited ? "已出阵 " + deployed.Level + "阶" : !affordable ? "还差 " + Session.GetTransactionShortfall(card.Definition.Cost) :
                    Session.Gold < card.Definition.Cost ? "可预支 " + (card.Definition.Cost - Session.Gold) : active ? "待部署" : "可招募";
                if (card.State.text != state) card.State.text = state;
                card.State.color = recruited ? BattlePalette.Silk : !affordable ? BattlePalette.Danger : BattlePalette.Jade;
                card.StatePanel.color = recruited ? BattlePalette.Jade : BattlePalette.Mist;
                if (recruited && FactionPulse(card.Definition.Faction)) card.StatePanel.color = BattlePalette.Copper;
                card.Role.text = FactionName(card.Definition.Faction) + " · " + card.Definition.Role;
                card.Role.fontSize = 18;
                card.Cost.fontSize = deployed != null ? 18 : 22;
                if (deployed != null) card.Cost.text = upgrades.WaitingLevel(deployed.Definition.Id) > 0 ? "待新阶施放" :
                    deployed.Definition.Faction == "qun" && Session.GetFactionTier("qun") > 0 ?
                    (Session.IsFactionIsolated(deployed) ? "孤阵生效" : "近友军·未生效") : "点选查看成长";
            }
        }

        private void UsePrimaryAction()
        {
            HeroUnit selected = Session.Heroes.Find(h => h.SlotId == selectedSlot);
            if (selected != null)
            {
                string error = Session.TryUpgrade(selectedSlot);
                if (error != null) { GameAudio.Reject(); SetNotice(error, true); }
                else { SyncUnits(); HandleEvents(); }
            }
            else if (selectedHero != null) { GameAudio.Button(); selectedHero = null; previewSlot = -1; SetNotice("已取消选将。点击武将卡继续布阵。"); }
            RefreshCards();
            RefreshHud();
        }

        public void Tick(float realDeltaTime)
        {
            if (ended) return;
            if (Session.State == BattleState.Preparation || Session.State == BattleState.Running) uiClock += Mathf.Max(0, realDeltaTime);
            Session.Tick(realDeltaTime);
            float gameDelta = Mathf.Max(0, Session.Time - lastTime);
            lastTime = Session.Time;
            SyncUnits();
            effects.Advance(gameDelta);
            SyncFeedback();
            GameAudio.SyncBattle(Session);
            HandleEvents();
            AdvanceArmyFeedback();
            foreach (UnitView view in heroViews.Values) view.Animate(Session.Time);
            foreach (UnitView view in enemyViews.Values) view.Animate(Session.Time);
            RefreshAnnotations();
            if (Session.State == BattleState.Running && Session.Time > rangeUntil) RefreshPlacement();
            for (int i = retiringViews.Count - 1; i >= 0; i--)
            {
                UnitView view = retiringViews[i];
                view.Animate(Session.Time);
                if (!view.IsRetired) continue;
                view.Hide();
                enemyPool.Push(view);
                retiringViews.RemoveAt(i);
            }
            if (Session.Time >= refreshAt || Session.Gold != lastGold || Session.AvailableSupplyAdvance != lastSupplyAdvance)
            {
                if (Session.Gold != lastGold || Session.AvailableSupplyAdvance != lastSupplyAdvance) RefreshCards();
                RefreshHud();
                refreshAt = Session.Time + .12f;
            }
            if (noticeUntil > 0 && Session.Time > noticeUntil) { noticeUntil = 0; RefreshHint(); }
            if (Session.State == BattleState.Victory || Session.State == BattleState.Defeat)
            {
                ended = true;
                foreach (UnitView view in retiringViews) { view.Hide(); enemyPool.Push(view); }
                retiringViews.Clear();
                foreach (UnitView view in enemyViews.Values) view.ClearStatus();
                effects.Clear();
                feedback.Clear();
                upgrades.Clear();
                RefreshCards();
                RefreshHud();
                if (game != null) game.CompleteBattle(Session);
            }
        }

        private void SyncUnits()
        {
            justRetiredViews.Clear();
            // 武将身份不随调位改变，复用棋兵和名签，只重建阵位索引。
            heroViews.Clear();
            foreach (HeroUnit hero in Session.Heroes)
            {
                UnitView view;
                if (!heroViewsById.TryGetValue(hero.Definition.Id, out view))
                {
                    view = new UnitView(ui, unitLayer, false, BattleLayout.GroundScale, labelLayer);
                    view.SetDefinition(hero.Definition.SpriteId, hero.Definition.Name, BattleLayout.HeroSize, false);
                    heroViewsById.Add(hero.Definition.Id, view);
                }
                heroViews.Add(hero.SlotId, view);
                view.Place(hero.Position, hero.Level, 1, false, false, Session.Time);
            }
            aliveIds.Clear();
            foreach (EnemyUnit enemy in Session.Enemies)
            {
                aliveIds.Add(enemy.Id);
                UnitView view;
                if (!enemyViews.TryGetValue(enemy.Id, out view))
                {
                    view = enemyPool.Count > 0 ? enemyPool.Pop() : new UnitView(ui, unitLayer, true, BattleLayout.GroundScale, labelLayer);
                    view.SetDefinition(enemy.Definition.SpriteId, enemy.Definition.IsBoss ? enemy.Definition.Name : "", enemy.Definition.IsBoss ? 195 : 112, enemy.Definition.IsBoss);
                    enemyViews.Add(enemy.Id, view);
                }
                view.Place(enemy.Position, 0, enemy.MaxHealth <= 0 ? 0 : enemy.Health / enemy.MaxHealth,
                    enemy.StunUntil > Session.Time, enemy.SlowUntil > Session.Time, Session.Time);
                view.SetHealthLane(enemy.Id);
                view.SetWarning(enemy.WarningUntil > Session.Time);
                if (enemy.Enraged) view.Tint(new Color(1, .76f, .62f, 1));
            }
            removeIds.Clear();
            foreach (int id in enemyViews.Keys) if (!aliveIds.Contains(id)) removeIds.Add(id);
            foreach (int id in removeIds)
            {
                UnitView view = enemyViews[id];
                view.Retire(Session.Time);
                retiringViews.Add(view);
                // 保留本帧退出的身份，稍后分发的致命命中仍能找到原棋兵。
                justRetiredViews.Add(id, view);
                enemyViews.Remove(id);
            }
            sorting.Clear();
            sorting.AddRange(heroViews.Values);
            sorting.AddRange(enemyViews.Values);
            sorting.AddRange(retiringViews);
            sorting.Sort((a, b) =>
            {
                int depth = a.Position.Y.CompareTo(b.Position.Y);
                return depth != 0 ? depth : a.IsBoss.CompareTo(b.IsBoss);
            });
            foreach (UnitView view in sorting) view.Root.SetAsLastSibling();
        }

        private void RefreshHud()
        {
            lastGold = Session.Gold;
            goldText.text = Session.Gold.ToString();
            incomeText.text = "+" + (level.IncomePerSecond * Session.Speed).ToString("0.##") + "/实秒";
            bool lowCastle = Session.CastleHealth > 0 && Session.CastleHealth <= level.CastleMaxHealth / 2;
            castleText.text = Session.CastleHealth + " / " + level.CastleMaxHealth;
            castleStateText.text = lowCastle ? "低城防" : "";
            castleText.color = lowCastle || Session.CastleHealth == 0 ? BattlePalette.Danger : BattlePalette.Ink;
            castleFill.color = lowCastle ? BattlePalette.Danger : BattlePalette.Jade;
            UiFactory.FillWidth(castleFill.rectTransform, 128 * Mathf.Clamp01((float)Session.CastleHealth / Mathf.Max(1, level.CastleMaxHealth)));
            elapsedText.text = BattleResultView.FormatTime(Session.RaceElapsedSeconds);
            if (Session.Time >= lossUntil) lossText.text = "";
            waveText.text = "第" + Session.CurrentWave + " / " + Session.TotalWaves + "波";
            speedControls.Refresh((int)Session.Speed, !ended && (Session.State == BattleState.Preparation || Session.State == BattleState.Running || Session.State == BattleState.Paused));
            switch (Session.State)
            {
                case BattleState.Preparation: pauseButton.Label.text = "开战"; phaseText.text = "布阵中"; break;
                case BattleState.Running: pauseButton.Label.text = "暂停"; phaseText.text = "守关中"; break;
                case BattleState.Paused: pauseButton.Label.text = "暂停中"; phaseText.text = "推演暂停"; break;
                default: pauseButton.Label.text = "结算"; phaseText.text = "战斗结束"; break;
            }
            phaseText.color = Session.State == BattleState.Paused ? BattlePalette.Copper : BattlePalette.Jade;
            pauseButton.Background.color = BattlePalette.Jade;
            pauseButton.Label.color = BattlePalette.Silk;
            intel.Refresh(Session);
            RefreshFactionBar();
            HeroUnit selected = Session.Heroes.Find(h => h.SlotId == selectedSlot);
            detailButton.Button.interactable = selected != null || selectedHero != null;
            foreach (KeyValuePair<int, SlotView> pair in slots)
            {
                bool occupied = heroViews.ContainsKey(pair.Key);
                pair.Value.Marker.gameObject.SetActive(!occupied);
            }
            if (selected != null)
            {
                bool max = selected.Level >= BattleSession.MaxHeroLevel;
                detailName.text = selected.Definition.Name + "  " + selected.Level + "阶" + (max ? " · 满阶" : " → " + (selected.Level + 1) + "阶");
                float currentRange = BattleSession.GetMaximumRange(selected.Definition, selected.Level);
                float upcomingRange = BattleSession.GetMaximumRange(selected.Definition, Mathf.Min(BattleSession.MaxHeroLevel, selected.Level + 1));
                detailRole.text = "射界 " + Mathf.RoundToInt(currentRange) + (max ? " · 满阶" : Mathf.Abs(upcomingRange - currentRange) > .01f ? " → " + Mathf.RoundToInt(upcomingRange) : " · 保持不变");
                detailInfo.text = max ? "已掌握三阶全部特色。\n点击详情查看机制与触发限制。" : UpgradeSummary(selected.Definition, selected.Level + 1);
                int waiting = upgrades.WaitingLevel(selected.Definition.Id);
                if (waiting > 0) detailInfo.text = "已获：" + UpgradeSummary(selected.Definition, waiting) + "\n等待下次真实特色施放";
                detailRole.text += " · " + FactionName(selected.Definition.Faction);
                int cost = Session.GetUpgradeCost(selectedSlot);
                bool affordable = Session.CanAffordTransaction(cost);
                int advance = Mathf.Max(0, cost - Session.Gold);
                actionButton.Label.text = max ? "已满级" : affordable ? "升级  " + cost + (advance > 0 ? " · 预支 " + advance : " 军饷") :
                    "升级 " + cost + " · 还差 " + Session.GetTransactionShortfall(cost);
                actionButton.Button.interactable = !max && affordable && Session.State != BattleState.Paused && Session.State != BattleState.Victory && Session.State != BattleState.Defeat;
                actionButton.Background.color = !max && affordable ? BattlePalette.Jade : BattlePalette.Mist;
                actionButton.Label.color = !max && affordable ? BattlePalette.Silk : BattlePalette.Ink;
            }
            else if (selectedHero != null)
            {
                HeroDefinition hero = database.FindHero(selectedHero);
                detailName.text = hero.Name + " · 待部署";
                detailRole.text = hero.Role + " · 招募 " + hero.Cost + " 军饷";
                detailInfo.text = "金线为可触及道路，形状为朝向示意。\n点击空阵位招募；详情查看节奏与成长。";
                actionButton.Label.text = "取消选择";
                actionButton.Button.interactable = true;
                actionButton.Background.color = BattlePalette.Mist;
                actionButton.Label.color = BattlePalette.Ink;
            }
            else
            {
                int tutorialStage = GetTutorialStage();
                detailName.text = tutorialStage < 0 ? (Session.State == BattleState.Preparation ? "布阵迎敌" : "审势调兵") :
                    tutorialStage <= 1 ? "入门 1/3 · 道路覆盖" : tutorialStage == 2 ? "入门 2/3 · 培养核心" : "入门 3/3 · 控制配合";
                detailRole.text = Session.Heroes.Count + " / " + roster.Count + " 名武将已出阵";
                detailInfo.text = Session.Heroes.Count == 0 ? "先选武将，再点空阵位。\n优先覆盖道路回转处。" :
                    Session.State == BattleState.Preparation ? "点击守将查看成长与升阶费用。\n开战前可点空位调位。" :
                    "点击守将查看成长与升阶费用。\n先培养核心，再按敌情补阵。";
                actionButton.Label.text = Session.State == BattleState.Preparation ? "准备就绪，点击右上开战" : "选将与升级可随时进行";
                actionButton.Button.interactable = false;
                actionButton.Background.color = BattlePalette.Mist;
                actionButton.Label.color = BattlePalette.Muted;
            }
            RefreshPlacement();
            if (noticeUntil <= 0) RefreshHint();
        }

        private void ShowHeroDetails()
        {
            HeroUnit selected = Session.Heroes.Find(h => h.SlotId == selectedSlot);
            HeroDefinition hero = selected == null ? database.FindHero(selectedHero) : selected.Definition;
            if (hero == null) return;
            int tier = selected == null ? 1 : selected.Level;
            if (detailOverlay != null) RemoveObject(detailOverlay.gameObject);
            resumeAfterDetails = Session.State == BattleState.Running;
            if (resumeAfterDetails) { Session.SetPaused(true); GameAudio.SetBattlePaused(true); RefreshHud(); }
            detailOverlay = ui.Rect(root, "武将技能详情", 0, 0, 1920, 1080);
            Image shade = ui.Box(detailOverlay, "详情遮罩", 0, 0, 1920, 1080, new Color(.02f, .08f, .06f, .85f), false);
            shade.raycastTarget = true;
            RectTransform panel = ui.Box(detailOverlay, "详情面板", 390, 185, 1140, 710, BattlePalette.Silk, false).rectTransform;
            ui.Border(panel.GetComponent<Image>(), BattlePalette.Copper, 1.5f);
            Text detailsTitle = ui.Label(panel, hero.Name + " · " + hero.Title + " · " + tier + "阶" + (resumeAfterDetails ? " · 阅读中暂停" : ""), 48, 25, 870, 68, 34, BattlePalette.Ink);
            BattleTypography.Display(detailsTitle, 34);
            UiButton close = ui.Button(panel, "关闭", 950, 24, 140, 72, () =>
            { CloseDetails(true); }, BattlePalette.Mist, 28);
            BattleTypography.Flat(close.Background);
            ui.Border(close.Background, BattlePalette.Line, 1);
            close.Label.color = BattlePalette.Ink;
            ui.Label(panel, hero.Description + "\n" + BattleSession.GetAttackTimingDescription(hero, tier) +
                "\n最大射界 " + Mathf.RoundToInt(BattleSession.GetMaximumRange(hero, tier)), 48, 117, 1040, 154, 27, BattlePalette.Muted, TextAnchor.UpperLeft);
            for (int rank = 1; rank <= 3; rank++)
                ui.Label(panel, rank + "阶 · " + BattleSession.GetGrowthDescription(hero, rank), 48, 292 + (rank - 1) * 121,
                    1040, 112, 26, rank == tier ? BattlePalette.Jade : BattlePalette.Ink, TextAnchor.UpperLeft);
        }

        private void ShowBattleRules()
        {
            if (game != null) { game.ShowMarchSpeedRules(); return; }
            if (detailOverlay != null) return;
            resumeAfterDetails = Session.State == BattleState.Running;
            if (resumeAfterDetails) { Session.SetPaused(true); GameAudio.SetBattlePaused(true); RefreshHud(); }
            detailOverlay = ui.Rect(root, "战斗规则弹窗", 0, 0, 1920, 1080);
            Image shade = ui.Box(detailOverlay, "规则遮罩", 0, 0, 1920, 1080, new Color(.02f, .08f, .06f, .85f), false);
            shade.raycastTarget = true;
            RectTransform panel = ui.Box(detailOverlay, "规则内容", 390, 210, 1140, 660, BattlePalette.Silk, false).rectTransform;
            ui.Border(panel.GetComponent<Image>(), BattlePalette.Copper, 1.5f);
            Text rulesTitle = ui.Label(panel, "进军与军饷" + (resumeAfterDetails ? " · 阅读中暂停" : ""), 48, 30, 1000, 65, 38, BattlePalette.Ink);
            BattleTypography.Display(rulesTitle, 38);
            Text speedTitle = ui.Label(panel, "进军倍率", 48, 122, 1000, 48, 30, BattlePalette.Jade);
            BattleTypography.Display(speedTitle, 30);
            ui.Label(panel, "1／2／3倍进军会加快敌军移动、出兵、波间等待与计时军饷。\n我方攻速、技能冷却与实际用时不随进军倍率改变。", 48, 180, 1040, 106, 28, BattlePalette.Ink, TextAnchor.UpperLeft);
            Text incomeTitle = ui.Label(panel, "当前收入", 48, 315, 1000, 48, 30, BattlePalette.Jade);
            BattleTypography.Display(incomeTitle, 30);
            ui.Label(panel, "基础每进军秒 +" + level.IncomePerSecond.ToString("0.##") + " 军饷 × 当前" + Session.Speed.ToString("0") + "倍\n= 每实际秒 +" + (level.IncomePerSecond * Session.Speed).ToString("0.##") + " 军饷；备战与暂停不计时、不发放计时收入。", 48, 370, 1040, 116, 28, BattlePalette.Ink, TextAnchor.UpperLeft);
            UiButton returnButton = ui.Button(panel, "返回战场", 48, 540, 1040, 88, () => CloseDetails(true), BattlePalette.Jade, 30);
            BattleTypography.Flat(returnButton.Background);
            returnButton.Label.color = BattlePalette.Silk;
        }

        public bool CloseDetails(bool resume)
        {
            if (detailOverlay == null) return false;
            bool restore = resume && resumeAfterDetails && Session.State == BattleState.Paused && !ended;
            resumeAfterDetails = false;
            RemoveObject(detailOverlay.gameObject); detailOverlay = null;
            if (restore) { Session.SetPaused(false); GameAudio.SetBattlePaused(false); GameAudio.SyncBattle(Session); }
            RefreshHud();
            return true;
        }

        private void RefreshPlacement()
        {
            if (range == null || placementGuide == null) return;
            HeroUnit selected = Session.Heroes.Find(h => h.SlotId == selectedSlot);
            SlotView target = null;
            HeroDefinition definition = selected == null ? database.FindHero(selectedHero) : selected.Definition;
            Point2 position = selected == null ? new Point2() : selected.Position;
            if (selected != null && Session.State == BattleState.Preparation && hoveredSlot >= 0 && !heroViews.ContainsKey(hoveredSlot))
            {
                if (slots.TryGetValue(hoveredSlot, out target)) position = target.Position;
            }
            if (selected == null && definition != null)
            {
                int id = hoveredSlot >= 0 && !heroViews.ContainsKey(hoveredSlot) ? hoveredSlot : previewSlot;
                if (slots.TryGetValue(id, out target)) position = target.Position;
            }
            bool visible = definition != null && (selected != null || target != null);
            bool inspecting = Session.State != BattleState.Running || selectedHero != null || Session.Time <= rangeUntil;
            visible &= inspecting;
            bool moving = selected != null && Session.State == BattleState.Preparation;
            bool choosingSlot = selectedHero != null || moving;
            foreach (KeyValuePair<int, SlotView> pair in slots)
            {
                bool focused = visible && target != null && pair.Value == target;
                pair.Value.Border.effectColor = focused ? BattlePalette.Copper : new Color(.23f, .47f, .38f, .36f);
                Color markerColor = focused ? BattlePalette.Silk : BattlePalette.Leaf;
                markerColor.a = focused ? .76f : .12f;
                pair.Value.Marker.color = markerColor;
                pair.Value.Label.text = heroViews.ContainsKey(pair.Key) || !choosingSlot ? "" :
                    moving ? (focused ? "移" : "") : "+";
            }
            range.gameObject.SetActive(visible);
            nextRange.gameObject.SetActive(false);
            placementGuide.gameObject.SetActive(visible);
            skillPreview.gameObject.SetActive(visible);
            RefreshFactionPlacement(selected, definition, position, visible, target != null);
            selection.gameObject.SetActive(selected != null);
            if (!visible) { placementGuide.Clear(); skillPreview.Clear(); return; }
            int heroLevel = selected == null ? 1 : selected.Level;
            float radius = BattleSession.GetMaximumRange(definition, heroLevel);
            ShowRange(position, radius);
            placementGuide.Show(level, position, radius);
            if (placementGuide.CoverageLength < radius * .8f && !detailInfo.text.Contains("道路覆盖较短"))
                detailInfo.text = "道路覆盖较短，建议换位。\n" + detailInfo.text.Split('\n')[0];
            skillPreview.Show(definition, heroLevel, position, FindSkillReference(position), level);
            if (selected != null && heroLevel < BattleSession.MaxHeroLevel)
            {
                float nextRadius = BattleSession.GetMaximumRange(definition, heroLevel + 1);
                nextRange.gameObject.SetActive(Mathf.Abs(nextRadius - radius) > .1f);
                UiFactory.Move(nextRange.rectTransform, position.X - nextRadius, position.Y - nextRadius);
                nextRange.rectTransform.sizeDelta = new Vector2(nextRadius * 2, nextRadius * 2);
            }
            if (selected != null) UiFactory.Move(selection.rectTransform, selected.Position.X - 56, selected.Position.Y - 31);
        }

        private Point2 FindSkillReference(Point2 origin)
        {
            float best = float.MaxValue;
            Point2 target = new Point2(origin.X + 150, origin.Y);
            foreach (RouteDefinition route in level.Routes)
                for (int segment = 1; segment < route.Points.Count; segment++)
                {
                    Point2 a = route.Points[segment - 1], b = route.Points[segment];
                    float dx = b.X - a.X, dy = b.Y - a.Y, square = dx * dx + dy * dy;
                    if (square < .001f) continue;
                    float fraction = Mathf.Clamp01(((origin.X - a.X) * dx + (origin.Y - a.Y) * dy) / square);
                    Point2 point = new Point2(a.X + dx * fraction, a.Y + dy * fraction);
                    float distance = Point2.Distance(origin, point);
                    if (distance >= best) continue;
                    best = distance;
                    target = distance < 1 ? b : point;
                }
            return target;
        }

        // 手机没有悬停：选将后先在道路覆盖最多的空阵位显示参考，不执行招募。
        private int FindPreviewSlot(HeroDefinition hero)
        {
            float radius = BattleSession.GetMaximumRange(hero);
            float bestScore = -1;
            int best = -1;
            foreach (SlotDefinition slot in level.Slots)
            {
                if (heroViews.ContainsKey(slot.Id)) continue;
                float score = 0;
                foreach (RouteDefinition route in level.Routes)
                    for (int i = 1; i < route.Points.Count; i++)
                    {
                        Point2 a = route.Points[i - 1], b = route.Points[i];
                        float length = Point2.Distance(a, b);
                        int samples = Mathf.Max(1, Mathf.CeilToInt(length / 25));
                        for (int j = 0; j < samples; j++)
                        {
                            float t = (j + .5f) / samples;
                            if (Point2.Distance(slot.Position, new Point2(Mathf.Lerp(a.X, b.X, t), Mathf.Lerp(a.Y, b.Y, t))) <= radius)
                                score += length / samples;
                        }
                    }
                if (score > bestScore) { bestScore = score; best = slot.Id; }
            }
            return best;
        }

        private void ShowRange(Point2 position, float radius)
        {
            range.gameObject.SetActive(true);
            UiFactory.Move(range.rectTransform, position.X - radius, position.Y - radius);
            range.rectTransform.sizeDelta = new Vector2(radius * 2, radius * 2);
        }

        private void HandleEvents()
        {
            List<BattleEvent> events = Session.DrainEvents();
            feedbackResults.Clear();
            foreach (BattleEvent item in events) feedbackResults.Add(feedback.Observe(item));
            GameAudio.EventBatch(events, feedbackResults, Session.Time);
            for (int index = 0; index < events.Count; index++)
            {
                BattleEvent battleEvent = events[index];
                HandleGrowthEvent(battleEvent);
                effects.Emit(battleEvent, database.FindHero(battleEvent.HeroId), feedbackResults[index]);
                if (battleEvent.Kind == "attack" || battleEvent.Kind == "basic_attack")
                {
                    HeroUnit unit = Session.Heroes.Find(h => h.Definition.Id == battleEvent.HeroId);
                    if (unit != null && heroViews.ContainsKey(unit.SlotId) && battleEvent.Kind == "basic_attack")
                        heroViews[unit.SlotId].Attack(battleEvent.OccurredAt, false);
                }
                else if (battleEvent.Kind == "hit")
                {
                    UnitView target;
                    if (enemyViews.TryGetValue(battleEvent.TargetEnemyId, out target) ||
                        justRetiredViews.TryGetValue(battleEvent.TargetEnemyId, out target)) target.Hit(battleEvent.OccurredAt);
                }
                else if (battleEvent.Kind == "kill" || battleEvent.Kind == "leak")
                {
                    UnitView target;
                    if (justRetiredViews.TryGetValue(battleEvent.TargetEnemyId, out target)) target.Retire(battleEvent.OccurredAt);
                }
                switch (battleEvent.Kind)
                {
                    case "wave":
                        SetNotice(GetTutorialHint() ?? battleEvent.Text);
                        break;
                    case "leak":
                        lossText.text = "-" + Mathf.RoundToInt(battleEvent.Amount);
                        lossUntil = Session.Time + 2.5f;
                        // 抵城当帧更新城防，避免节流刷新让提示和顶栏数值短暂不一致。
                        RefreshHud();
                        SetNotice("敌军突破 · 城防 -" + Mathf.RoundToInt(battleEvent.Amount) +
                            " · 剩余 " + Session.CastleHealth + "/" + level.CastleMaxHealth +
                            (Session.CastleHealth > 0 ? "，继续守关；城防归零才失败。" : "，城防归零。"), true);
                        break;
                    case "boss_warning": case "boss_phase": case "defeat": case "victory":
                        if (!string.IsNullOrEmpty(battleEvent.Text)) SetNotice(battleEvent.Text, true);
                        break;
                    case "notice":
                        EnemyUnit source = Session.Enemies.Find(enemy => enemy.Id == battleEvent.TargetEnemyId);
                        bool bossNotice = string.IsNullOrEmpty(battleEvent.HeroId) && source != null && source.Definition.IsBoss;
                        // 普通技能反馈不抢占入门军令，Boss消息仍可即时覆盖教学。
                        if (!string.IsNullOrEmpty(battleEvent.Text) && (bossNotice || GetTutorialStage() < 0))
                            SetNotice(battleEvent.Text, bossNotice);
                        break;
                    case "upgrade": break;
                    case "recruit": case "move":
                        if (!string.IsNullOrEmpty(battleEvent.Text)) SetNotice(battleEvent.Text + " · 方向示意，实际朝敌军。");
                        break;
                }
            }
        }

        private void SyncFeedback()
        {
            Session.ReadCombatFeedback(feedbackFields, feedbackEnemies);
            effects.SyncCombatFeedback(feedbackFields, level, Session.Heroes);
            foreach (EnemyFeedbackSnapshot state in feedbackEnemies)
            {
                UnitView view;
                if (enemyViews.TryGetValue(state.EnemyId, out view)) view.SetFeedback(state);
            }
            // Boss预警覆盖普通状态，防止新链/燃标记遮掉蓄势提示。
            foreach (EnemyUnit enemy in Session.Enemies)
            {
                UnitView view;
                if (enemyViews.TryGetValue(enemy.Id, out view)) view.SetWarning(enemy.WarningUntil > Session.Time);
            }
        }

        private bool SetNotice(string message, bool priority = false)
        {
            // 阶段教学和普通操作不能盖住尚未结束的预警或失败反馈。
            if (!priority && priorityNotice && noticeUntil > Session.Time && Session.State != BattleState.Preparation) return false;
            upgradeBannerUntilUi = 0;
            notice.text = message;
            notice.color = priority ? BattlePalette.Danger : BattlePalette.Muted;
            priorityNotice = priority;
            noticeUntil = Session.Time + 4;
            return true;
        }

        private int GetTutorialStage()
        {
            if (!showHuangjinTutorial || Session.CurrentWave >= 4 || Session.State == BattleState.Victory || Session.State == BattleState.Defeat) return -1;
            return Session.CurrentWave;
        }

        private string GetTutorialHint()
        {
            switch (GetTutorialStage())
            {
                case 0: return "先点武将卡，再点空位；金色道路显示覆盖。选中守将后，开战前可点空位调位。";
                case 1: return "第一波：先点卡片再点空位补阵，让金色覆盖对准道路回转处。";
                case 2: return "第二波：点击守将查看升阶收益，优先升级覆盖稳定的核心输出。";
                case 3: return "第三波：若阵中有控制，让其配合主力输出，用减速、眩晕延长受击时间。";
                default: return null;
            }
        }

        private void RefreshHint()
        {
            notice.color = BattlePalette.Muted;
            if (selectedHero != null)
            {
                HeroDefinition hero = database.FindHero(selectedHero);
                notice.text = !Session.CanAffordTransaction(hero.Cost) ? "军饷不足：" + hero.Name + "还差 " + Session.GetTransactionShortfall(hero.Cost) + " 军饷。" :
                    Session.Gold < hero.Cost ? "已选择" + hero.Name + " · 可预支 " + (hero.Cost - Session.Gold) + " 补给完成招募。" : "已选择" + hero.Name + " · 点击亮起的空阵位部署。";
            }
            else if (selectedSlot >= 0 && Session.State == BattleState.Preparation)
                notice.text = "已选守将 · 可点空位调位，或查看升阶。";
            else if (GetTutorialHint() != null) notice.text = GetTutorialHint();
            else if (selectedSlot >= 0) notice.text = "已选守将 · 可查看升阶收益。";
            else if (Session.Heroes.Count == 0) notice.text = "先部署武将覆盖道路回转处 · 准备就绪后点击开战";
            else notice.text = Session.IsWaveActive ? "查看上方余敌与敌型 · 点击守将培养核心，也可继续补阵" :
                "下一波将至 · 利用间隙补阵，或点击核心守将查看升阶收益";
        }

        private static void RemoveObject(GameObject item)
        {
            if (item == null) return;
            item.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }

        public void Dispose() { CloseDetails(false); feedback.Clear(); effects.Dispose(); }

        private sealed class CardView
        {
            public readonly HeroDefinition Definition;
            public readonly UiButton Button;
            public readonly Text Name, Role, Cost, State;
            public readonly Image StatePanel, Focus;
            public readonly Outline Border;
            public CardView(HeroDefinition definition, UiButton button, Text name, Text role, Text cost, Text state, Image statePanel, Image focus)
            {
                Definition = definition; Button = button; Name = name; Role = role; Cost = cost; State = state; StatePanel = statePanel; Focus = focus;
                Border = button.Background.GetComponent<Outline>();
            }
        }

        private sealed class SlotView
        {
            public readonly UiButton Button;
            public readonly Image Marker;
            public readonly Text Label;
            public readonly Point2 Position;
            public readonly Outline Border;
            public SlotView(UiButton button, Image marker, Text label, Point2 position)
            { Button = button; Marker = marker; Label = label; Position = position; Border = marker.GetComponent<Outline>(); }
        }
    }
}
