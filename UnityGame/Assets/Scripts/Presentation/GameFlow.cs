using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 页面与会话的生命周期独立于系统输入，以真实流程接受自动检查。
    public sealed class GameFlow : IDisposable
    {
        public GameProgress Progress { get; private set; }
        public IList<LevelDefinition> CampaignLevels { get; private set; }
        public string PageKind { get; private set; }
        public BattleSession CurrentSession { get { return battle == null ? null : battle.Session; } }
        public LevelDefinition CurrentLevel { get { return currentLevel; } }
        private readonly UiFactory ui;
        private readonly RectTransform root;
        private readonly GameDatabase database;
        private readonly MenuViews menus;
        private RectTransform page;
        private RectTransform modal;
        private RectTransform audioSettings;
        private string modalKind;
        private BattleView battle;
        private BattleSession completedSession;
        private LevelDefinition currentLevel;
        private List<string> currentRoster = new List<string>();
        private bool resumeAfterMarchRules;
        private bool marchRulesInterrupted;
        private bool markMarchRulesRead;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private MarchSpeedControls pauseMarchControls;
        private bool AcceptsApplicationInput { get { return !applicationPaused && applicationFocused; } }

        public GameFlow(UiFactory ui, RectTransform root, GameDatabase database, GameProgress progress)
        {
            if (ui == null || root == null || database == null || progress == null)
                throw new ArgumentNullException("界面、内容与记录不能为空");
            this.ui = ui; this.root = root; this.database = database; Progress = progress;
            var orderedLevels = new List<LevelDefinition>();
            LevelDefinition firstBattle = database.FindLevel("huangjin");
            if (firstBattle != null) orderedLevels.Add(firstBattle);
            foreach (LevelDefinition level in database.Levels)
                if (level != firstBattle) orderedLevels.Add(level);
            CampaignLevels = orderedLevels.AsReadOnly();
            menus = new MenuViews(this, ui, database);
            ShowMainMenu();
        }

        private RectTransform NewPage(string kind)
        {
            CloseModal();
            if (battle != null) { battle.Dispose(); battle = null; }
            Remove(page);
            GameAudio.ShowMenu();
            PageKind = kind;
            page = ui.Rect(root, "页面", 0, 0, 1920, 1080);
            return page;
        }

        public void ShowMainMenu() { menus.Main(NewPage("Main")); }
        public void ShowLevels() { menus.Levels(NewPage("Levels")); }
        public void ShowIntel(LevelDefinition level) { currentLevel = level; menus.Intel(NewPage("Intel"), level); }
        public void ShowRoster(LevelDefinition level) { currentLevel = level; menus.Roster(NewPage("Roster"), level); }
        public void AdjustRoster()
        {
            if (currentLevel == null) return;
            var legal = new List<string>();
            foreach (string id in currentRoster)
                if (legal.Count < 10 && !legal.Contains(id) && database.FindHero(id) != null && !currentLevel.BossHeroIds.Contains(id)) legal.Add(id);
            menus.Roster(NewPage("Roster"), currentLevel, legal);
        }

        public void BeginBattle(LevelDefinition level, List<string> roster)
        {
            currentLevel = level;
            currentRoster = new List<string>(roster);
            completedSession = null;
            RectTransform content = NewPage("Battle");
            GameAudio.BeginBattle(level.Id);
            battle = new BattleView(this, ui, content, database, level, currentRoster);
            GameAudio.SyncBattle(battle.Session);
        }

        public void RetryBattle() { BeginBattle(currentLevel, currentRoster); }
        public void Tick(float realDeltaTime)
        {
            if (battle == null || !AcceptsApplicationInput) return;
            battle.Tick(realDeltaTime);
            GameAudio.SyncBattle(battle.Session);
        }

        public void HandleBack()
        {
            if (!AcceptsApplicationInput) return;
            if (audioSettings != null) { CloseAudioSettings(); return; }
            if (modalKind == "MarchRules") { CloseMarchSpeedRules(false); return; }
            if (battle != null && battle.CloseDetails(true)) return;
            if (battle != null)
            {
                if (modalKind == "PreparationExit") { CloseModal(); return; }
                if (modalKind == "Pause") { ResumeBattle(); return; }
                BattleState state = battle.Session.State;
                if (state == BattleState.Preparation) ShowPreparationExit();
                else if (state == BattleState.Running) PauseBattle();
                else if (state == BattleState.Paused) ResumeBattle();
                else ShowLevels();
                return;
            }
            if (PageKind == "Roster") ShowIntel(currentLevel);
            else if (PageKind == "Intel") ShowLevels();
            else if (PageKind == "Levels") ShowMainMenu();
        }

        private void ShowPreparationExit()
        {
            if (modal != null) return;
            RectTransform content = ShowModal("离开布阵", "继续布阵将保留部署；重新选将或返回关卡将结束本次布阵。", "PreparationExit");
            AppTheme.Button(ui, content, "继续布阵", 80, 235, 640, 76, CloseModal);
            AppTheme.Button(ui, content, "重新选将", 80, 335, 305, 68, () => ShowRoster(currentLevel), BattlePalette.Mist, 26);
            AppTheme.Button(ui, content, "返回关卡", 415, 335, 305, 68, ShowLevels, BattlePalette.Mist, 26);
        }

        public void PauseBattle()
        {
            if (battle == null) return;
            battle.CloseDetails(false);
            BattleSession session = battle.Session;
            if (session.State != BattleState.Running && session.State != BattleState.Paused) return;
            session.SetPaused(true);
            GameAudio.SetBattlePaused(true);
            if (modal != null) return;
            FastestRecord fastest = Progress.GetFastestRecord(currentLevel.Id, session.RuleVersion);
            string best = fastest == null ? "—" : BattleResultView.FormatRecordTime(fastest.ElapsedMilliseconds / 1000.0) + " · " + fastest.SpeedSummary;
            RaceRecord race = Progress.GetRaceRecord(currentLevel.Id, session.RuleVersion);
            string raceBest = race == null ? "—（全部敌将击退后建立）" : BattleResultView.FormatRecordTime(race.ElapsedMilliseconds / 1000.0);
            RectTransform content = ShowModal("推演暂停", "战斗、军饷收入与所有战场动画已暂停。\n竞速最快 " + raceBest + "\n本关个人最佳（历史参考） " + best, "Pause", 745);
            AppTheme.Button(ui, content, "继续守关", 80, 235, 640, 76, ResumeBattle);
            AppTheme.Button(ui, content, "重新推演", 80, 335, 305, 68, RetryBattle, BattlePalette.Mist, 26);
            AppTheme.Button(ui, content, "返回关卡", 415, 335, 305, 68, ShowLevels, BattlePalette.Mist, 26);
            AppTheme.Button(ui, content, "声音设置", 80, 435, 640, 66, ShowAudioSettings, BattlePalette.Mist, 26);
            AppTheme.Button(ui, content, "倍率与竞速规则", 80, 525, 305, 66, () => ShowMarchSpeedRules(false), BattlePalette.Mist, 24);
            AppTheme.Button(ui, content, "调整阵容", 415, 525, 305, 66, AdjustRoster, BattlePalette.Mist, 24);
            AppTheme.Label(ui, content, "进军", 80, 625, 68, 80, 26, BattlePalette.Muted);
            pauseMarchControls = new MarchSpeedControls(ui, content, 168, 625, "暂停进军倍率", ChangeMarchSpeedFromPause);
            pauseMarchControls.Refresh((int)session.Speed, AcceptsApplicationInput);
            AppTheme.Label(ui, content, "选档后仍暂停", 425, 625, 295, 80, 24, BattlePalette.Muted);
            battle.RefreshRaceHud();
        }

        public void ShowAudioSettings()
        {
            if (audioSettings != null) return;
            if (battle != null && battle.Session.State == BattleState.Running) PauseBattle();
            audioSettings = AudioSettingsView.Create(ui, root, CloseAudioSettings);
        }

        private void CloseAudioSettings() { Remove(audioSettings); audioSettings = null; }

        public void PauseForSystem()
        {
            if (battle == null) return;
            if (modalKind == "MarchRules")
            {
                marchRulesInterrupted = true;
                resumeAfterMarchRules = false;
                if (battle.Session.State == BattleState.Running) battle.Session.SetPaused(true);
                GameAudio.SetBattlePaused(true);
                return;
            }
            bool reading = battle.CloseDetails(false);
            if (battle.Session.State == BattleState.Running || reading && battle.Session.State == BattleState.Paused) PauseBattle();
        }

        public void SetApplicationPaused(bool paused)
        { applicationPaused = paused; if (paused) PauseForSystem(); RefreshMarchControls(); }
        public void SetApplicationFocused(bool focused)
        { applicationFocused = focused; if (!focused) PauseForSystem(); RefreshMarchControls(); }

        private void RefreshMarchControls()
        {
            if (battle == null) return;
            if (pauseMarchControls != null) pauseMarchControls.Refresh((int)battle.Session.Speed, AcceptsApplicationInput && audioSettings == null);
            battle.RefreshRaceHud();
        }

        private void ResumeBattle()
        {
            if (battle == null || !AcceptsApplicationInput || battle.Session.State != BattleState.Paused) return;
            CloseModal();
            battle.Session.SetPaused(false);
            GameAudio.SetBattlePaused(false);
            GameAudio.SyncBattle(battle.Session);
        }

        public void CompleteBattle(BattleSession session)
        {
            if (battle == null || session != battle.Session || session == completedSession ||
                (session.State != BattleState.Victory && session.State != BattleState.Defeat)) return;
            completedSession = session;
            battle.CloseDetails(false);
            bool won = session.State == BattleState.Victory;
            string ordinaryRecordIssue = "";
            if (won)
            {
                try { Progress.RecordVersionedVictory(currentLevel.Id, session.CastleHealth,
                    session.ElapsedSeconds, session.SpeedMask, session.LeakedCount, session.BossBreachCount, session.RuleVersion); }
                catch (Exception) { ordinaryRecordIssue = "普通通关结果保存失败，请重试。"; }
            }
            RaceRecordResult raceResult = Progress.RecordRaceVictory(session.GetRaceSnapshot());
            FastestRecord historicalBest = null;
            try { historicalBest = Progress.GetFastestRecord(currentLevel.Id, session.RuleVersion); }
            catch (Exception) { if (ordinaryRecordIssue.Length == 0) ordinaryRecordIssue = "普通通关历史读取失败。"; }
            int next = CampaignLevels.IndexOf(currentLevel) + 1;
            Action continueBattle = won && next > 0 && next < CampaignLevels.Count ?
                (Action)(() => ShowIntel(CampaignLevels[next])) : null;
            CloseModal();
            modalKind = "Result";
            modal = RaceResultView.Create(ui, root, database, currentLevel, session, RetryBattle,
                AdjustRoster, continueBattle, ShowLevels, raceResult, historicalBest, ordinaryRecordIssue);
        }

        public void ChangeMarchSpeed(int targetSpeed)
        { ChangeMarchSpeed(targetSpeed, false); }

        private void ChangeMarchSpeedFromPause(int targetSpeed)
        { ChangeMarchSpeed(targetSpeed, true); }

        private void ChangeMarchSpeed(int targetSpeed, bool fromPause)
        {
            if (battle == null || !AcceptsApplicationInput || audioSettings != null || battle.HasDetailsOpen ||
                targetSpeed < 1 || targetSpeed > 3 || (fromPause ? modalKind != "Pause" : modal != null)) return;
            BattleSession session = battle.Session;
            if (session.State != BattleState.Preparation && session.State != BattleState.Running && session.State != BattleState.Paused) return;
            if (session.Speed == targetSpeed) return;
            session.SetSpeed(targetSpeed);
            battle.ShowMarchSpeedNotice();
            battle.RefreshRaceHud();
            if (pauseMarchControls != null) pauseMarchControls.Refresh(targetSpeed, AcceptsApplicationInput);
            if (targetSpeed > 1 && !Progress.HasReadMarchSpeedIntro) ShowMarchSpeedRules(true);
        }

        public void ShowMarchSpeedRules(bool firstRead = false)
        {
            if (battle == null || modalKind == "MarchRules") return;
            battle.CloseDetails(false);
            BattleSession session = battle.Session;
            resumeAfterMarchRules = session.State == BattleState.Running;
            marchRulesInterrupted = false;
            markMarchRulesRead = firstRead;
            if (resumeAfterMarchRules) session.SetPaused(true);
            GameAudio.SetBattlePaused(session.State == BattleState.Paused);
            CloseModal(); modalKind = "MarchRules";
            modal = ui.Rect(root, "倍率规则遮罩", 0, 0, 1920, 1080);
            Image shade = AppTheme.Box(ui, modal, "遮罩", 0, 0, 1920, 1080, new Color(.02f, .09f, .07f, .72f), false); shade.raycastTarget = true;
            RectTransform panel = AppTheme.Box(ui, modal, "倍率规则纸面", 460, 130, 1000, 820, BattlePalette.Silk).rectTransform;
            AppTheme.Title(ui, panel, "进军倍率 · 以压力换时间", 55, 30, 890, 75, 40, BattlePalette.Jade);
            AppTheme.Label(ui, panel, "当前选择 " + session.Speed.ToString("0") + "× · 同关同规则共用一个竞速榜。", 55, 125, 890, 65, 27);
            AppTheme.Label(ui, panel, "提速会加快敌军移动、出兵、波间等待与计时军饷。\n我方攻击与技能速度不变，来敌更密，守关压力更高。", 55, 205, 890, 112, 28);
            AppTheme.Label(ui, panel, "竞速资格", 55, 344, 890, 50, 30, BattlePalette.Jade);
            AppTheme.Label(ui, panel, "守关成功，且全部敌将击退；普通漏兵允许。\n任意敌将突破后，仍可普通通关，本局不计竞速最快。\n只比较有效运行实秒；严格更快才刷新，倍率本身不加分。", 55, 406, 890, 160, 27);
            AppTheme.Label(ui, panel, "备战、暂停、阅读与后台不计时、不发放计时军饷。\n系统中断后需明确继续；确认说明不会替你开战。", 55, 590, 890, 90, 25, BattlePalette.Muted);
            UiButton confirm = AppTheme.Button(ui, panel, firstRead ? "明白了，继续" : "返回战场", 55, 707, 890, 76, () => CloseMarchSpeedRules(true));
            confirm.Rect.name = "倍率说明确认";
            battle.RefreshRaceHud();
        }

        private void CloseMarchSpeedRules(bool confirmed)
        {
            if (modalKind != "MarchRules" || battle == null || !AcceptsApplicationInput) return;
            bool resume = resumeAfterMarchRules && !marchRulesInterrupted && AcceptsApplicationInput;
            bool interrupted = marchRulesInterrupted;
            bool saveFailed = confirmed && markMarchRulesRead && !Progress.MarkMarchSpeedIntroRead();
            resumeAfterMarchRules = false; marchRulesInterrupted = false; markMarchRulesRead = false;
            CloseModal();
            if (resume && battle.Session.State == BattleState.Paused)
            { battle.Session.SetPaused(false); GameAudio.SetBattlePaused(false); }
            else if (interrupted || battle.Session.State == BattleState.Paused) PauseBattle();
            if (saveFailed) battle.ShowMarchSpeedSaveFailure();
            battle.RefreshRaceHud();
        }

        private RectTransform ShowModal(string title, string message, string kind, float height = 470)
        {
            CloseModal();
            modalKind = kind;
            modal = ui.Rect(root, "弹窗遮罩", 0, 0, 1920, 1080);
            Image shade = AppTheme.Box(ui, modal, "遮罩", 0, 0, 1920, 1080, new Color(.02f, .09f, .07f, .62f), false);
            shade.raycastTarget = true;
            Image box = AppTheme.Box(ui, modal, "弹窗", 560, height > 470 ? 235 : 290, 800, height, BattlePalette.Silk);
            ui.Border(box, BattlePalette.Copper);
            AppTheme.Title(ui, box.transform, title, 60, 34, 680, 80, 48, BattlePalette.Ink, TextAnchor.MiddleCenter);
            AppTheme.Label(ui, box.transform, message, 60, 109, 680, 116, 25, BattlePalette.Ink, TextAnchor.MiddleCenter);
            return box.rectTransform;
        }

        private void CloseModal() { CloseAudioSettings(); Remove(modal); modal = null; modalKind = null; pauseMarchControls = null; }

        private static void Remove(RectTransform target)
        {
            if (target == null) return;
            target.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(target.gameObject);
            else UnityEngine.Object.DestroyImmediate(target.gameObject);
        }

        public void Dispose()
        {
            CloseModal();
            if (battle != null) { battle.Dispose(); battle = null; }
            Remove(page); page = null;
            PageKind = "Closed";
            GameAudio.SetBattlePaused(true);
        }
    }
}
