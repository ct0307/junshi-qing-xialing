using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed class MenuViews
    {
        private readonly GameFlow game;
        private readonly UiFactory ui;
        private readonly GameDatabase database;

        public MenuViews(GameFlow game, UiFactory ui, GameDatabase database)
        { this.game = game; this.ui = ui; this.database = database; }

        private void Header(Transform root, string title, string subtitle, System.Action back)
        {
            AppTheme.Box(ui, root, "页面浅绢", 0, 0, 1920, 1080, BattlePalette.Silk);
            AppTheme.Box(ui, root, "页眉铜线", 40, 151, 1840, 2, BattlePalette.Copper);
            AppTheme.Button(ui, root, "‹ 返回", 45, 36, 155, 68, back, BattlePalette.Mist, 26);
            AppTheme.Title(ui, root, title, 235, 20, 1250, 70, 44);
            AppTheme.Label(ui, root, subtitle, 239, 88, 1470, 32, 22, BattlePalette.Muted);
        }

        public void Main(RectTransform root)
        {
            AppTheme.Box(ui, root, "主菜单浅绢", 0, 0, 1920, 1080, BattlePalette.Silk);
            if (database.Levels.Count > 0)
            {
                Image map = ui.Picture(root, database.Levels[0].MapResource, 0, 0, 1920, 1080);
                map.color = new Color(1, 1, 1, .18f);
            }
            AppTheme.Box(ui, root, "题字留白", 60, 130, 930, 790, new Color(.957f, .941f, .867f, .92f));
            AppTheme.Box(ui, root, "题字铜线", 120, 690, 430, 2, BattlePalette.Copper);
            AppTheme.Box(ui, root, "题签", 113, 175, 70, 88, BattlePalette.Danger);
            AppTheme.Label(ui, root, "令", 113, 175, 70, 88, 45, BattlePalette.Silk, TextAnchor.MiddleCenter);
            AppTheme.Title(ui, root, "军师请下令", 110, 270, 880, 150, 96);
            AppTheme.Label(ui, root, "三国名将 · 布阵守城", 117, 450, 830, 65, 40, BattlePalette.Jade);
            AppTheme.Label(ui, root, "从二十五位演义名将中选出你的阵容。\n部署守将、审时升级，在重重敌阵中守住城关。", 120, 555, 900, 110, 29);
            AppTheme.Button(ui, root, "开始推演  ›", 120, 735, 430, 100, game.ShowLevels, null, 36);
            AppTheme.Label(ui, root, "离线单机 · 每局最多十将 · 同关敌将不可选", 123, 863, 1120, 45, 24, BattlePalette.Muted);
            string[] ids = { "guan_yu", "zhao_yun", "zhuge_liang" };
            for (int i = 0; i < ids.Length; i++)
            {
                HeroDefinition hero = database.FindHero(ids[i]);
                if (hero == null) continue;
                float x = 1005 + i * 210;
                float y = 350 + (i == 1 ? -70 : 20);
                // 椭圆贴住既有立绘台座，保持人物与地图之间的接地层次。
                ui.Circle(root, "主菜单接地阴影_" + hero.Id, x + 55, y + 294, 225, 35,
                    new Color(.09f, .2f, .14f, .13f));
                ui.Picture(root, hero.SpriteId, x, y, 340, 340);
                AppTheme.Title(ui, root, hero.Name, x + 75, y + 337, 180, 55, 32, BattlePalette.Ink, TextAnchor.MiddleCenter);
            }
            AppTheme.Label(ui, root, "军师一声令，名将守山河。", 1190, 800, 620, 65, 33, BattlePalette.Muted, TextAnchor.MiddleCenter);
            AppTheme.Button(ui, root, "声音设置", 1560, 958, 275, 66, game.ShowAudioSettings, BattlePalette.Mist, 26);
        }

        public void Levels(RectTransform root)
        {
            Header(root, "选择战役", "五处关山，五番推演。点击关卡先查看敌将情报。", game.ShowMainMenu);
            IList<LevelDefinition> levels = game.CampaignLevels;
            int count = levels.Count;
            float width = count > 0 ? Mathf.Min(344, (1800 - (count - 1) * 22) / count) : 344;
            for (int i = 0; i < count; i++)
            {
                LevelDefinition level = levels[i];
                float x = 60 + i * (width + 22);
                UiButton card = AppTheme.Button(ui, root, "", x, 235, width, 710, () => game.ShowIntel(level), BattlePalette.Silk);
                // 地图铺满卡片宽度；保持等比，放大道路与城关的有效展示。
                ui.Picture(card.Rect, level.MapResource, 0, 0, width, 248);
                string number = "第" + (i + 1) + "局" + (level.Id == "huangjin" ? " · 推荐首战" : "");
                AppTheme.Label(ui, card.Rect, number, 25, 255, width - 50, 40, 24, BattlePalette.Danger);
                AppTheme.Title(ui, card.Rect, level.Name, 23, 305, width - 46, 80, 42, BattlePalette.Ink);
                AppTheme.Label(ui, card.Rect, level.Subtitle, 25, 390, width - 50, 65, 25, BattlePalette.Ink);
                AppTheme.Label(ui, card.Rect, "敌将：" + BossNames(level) + "\n" + level.Waves.Count + "波敌军 · " + level.Slots.Count + "处阵位",
                    25, 462, width - 50, 72, 23, BattlePalette.Ink);
                FastestRecord record = game.Progress.GetFastestRecord(level.Id, BattleSession.ResolveRuleVersion(level, true));
                RaceRecord race = game.Progress.GetRaceRecord(level.Id, BattleSession.ResolveRuleVersion(level, true));
                Text fastest = AppTheme.Label(ui, card.Rect, "竞速最快 " + (race == null ? "—" : BattleResultView.FormatRecordTime(race.ElapsedMilliseconds / 1000.0)),
                    25, 536, width - 50, 32, 22, BattlePalette.Ink);
                fastest.gameObject.name = "关卡最快";
                FastestRecord recent = game.Progress.GetRecentRecord(level.Id, BattleSession.ResolveRuleVersion(level, true));
                Text latest = AppTheme.Label(ui, card.Rect, "最近通关 " + (recent == null ? "—" : BattleResultView.FormatRecordTime(recent.ElapsedMilliseconds / 1000.0)),
                    25, 568, width - 50, 32, 22, BattlePalette.Ink);
                latest.gameObject.name = "关卡最近";
                Text old = AppTheme.Label(ui, card.Rect, "历史参考 " + (record == null ? "—" : BattleResultView.FormatRecordTime(record.ElapsedMilliseconds / 1000.0)),
                    25, 600, width - 50, 32, 21, BattlePalette.Muted);
                old.gameObject.name = "关卡普通历史最快";
                FastestRecord previous = game.Progress.GetFastestRecord(level.Id, GameProgress.PreviousFastestRuleVersion);
                Text previousText = AppTheme.Label(ui, card.Rect, "旧版历史 " + (previous == null ? "—" : BattleResultView.FormatRecordTime(previous.ElapsedMilliseconds / 1000.0)),
                    25, 632, width - 50, 32, 21, BattlePalette.Muted);
                previousText.gameObject.name = "关卡旧版最快";
                bool cleared = game.Progress.IsCleared(level.Id);
                AppTheme.Box(ui, card.Rect, "通关标记", 24, 670, width - 48, 34, cleared ? BattlePalette.Jade : BattlePalette.Mist);
                AppTheme.Label(ui, card.Rect, cleared ? "已通关  城防最佳 " + game.Progress.GetBestCastle(level.Id) : "查看敌情  ›",
                    26, 670, width - 52, 34, 21, cleared ? BattlePalette.Silk : BattlePalette.Ink, TextAnchor.MiddleCenter);
            }
            AppTheme.Label(ui, root, "竞速资格：守关成功，且全部敌将击退；普通漏兵允许。纪录仅保存在本机。", 60, 970, 1800, 45, 24, BattlePalette.Muted, TextAnchor.MiddleCenter);
        }

        public void Intel(RectTransform root, LevelDefinition level)
        {
            Header(root, level.Name + " · 敌将情报", level.Subtitle, game.ShowLevels);
            Image map = AppTheme.Box(ui, root, "地形预览", 40, 190, 1070, 690, BattlePalette.Mist);
            ui.Picture(map.transform, level.MapResource, 8, 8, 1054, 593);
            AppTheme.Label(ui, map.transform, level.Theme + "  ·  " + level.Routes.Count + "条敌军路线  ·  " + level.Slots.Count + "处阵位",
                25, 616, 1020, 58, 27, BattlePalette.Jade, TextAnchor.MiddleCenter);
            RaceRecord race = game.Progress.GetRaceRecord(level.Id, BattleSession.ResolveRuleVersion(level, true));
            AppTheme.Label(ui, root, "竞速最快 " + (race == null ? "— · 全部敌将击退后建立" : BattleResultView.FormatRecordTime(race.ElapsedMilliseconds / 1000.0)) +
                "\n" + level.Story, 50, 894, 1050, 135, 25, BattlePalette.Ink);
            AppTheme.Title(ui, root, "本关敌将", 1130, 190, 720, 60, 36, BattlePalette.Jade);
            float rowHeight = Mathf.Min(205, 570f / Mathf.Max(1, level.BossHeroIds.Count));
            for (int i = 0; i < level.BossHeroIds.Count; i++)
            {
                string id = level.BossHeroIds[i];
                HeroDefinition hero = database.FindHero(id);
                EnemyDefinition enemy = database.Enemies.Find(e => e.IsBoss && e.CharacterId == id);
                float y = 270 + i * rowHeight;
                AppTheme.Box(ui, root, "敌将情报", 1130, y, 720, rowHeight - 15, BattlePalette.Mist);
                ui.Picture(root, UiFactory.PortraitPath(id), 1145, y + 12, 135, 135);
                AppTheme.Title(ui, root, hero == null ? id : hero.Name, 1305, y + 14, 500, 48, 34, BattlePalette.Ink);
                string detail = enemy == null ? "守关强敌" : BossSkill(enemy.Skill) + "\n基础生命 " + Mathf.RoundToInt(enemy.MaxHealth) +
                    " · 抵城损失 " + enemy.CastleDamage + " 城防";
                AppTheme.Label(ui, root, detail, 1305, y + 65, 510, rowHeight - 85, 23, BattlePalette.Muted);
            }
            AppTheme.Label(ui, root, "本关敌将不可选 · 初始军饷 " + level.InitialGold + " · 每进军秒收入 " + level.IncomePerSecond +
                "\n全部波次结束且城防仍存，即可普通通关。\n全部敌将击退才计竞速最快；普通漏兵允许。",
                1135, 835, 710, 120, 22, BattlePalette.Muted);
            AppTheme.Button(ui, root, "前往选将  ›", 1320, 967, 530, 76, () => game.ShowRoster(level));
        }

        public void Roster(RectTransform root, LevelDefinition level, IList<string> previousRoster = null)
        {
            Header(root, "战前选将", "点击卡片查看详情，右下勾选加入或移出；每局最多10人，同关全部敌将不可选。", () => game.ShowIntel(level));
            List<string> selected = new List<string>();
            if (previousRoster != null)
                foreach (string id in previousRoster)
                    if (selected.Count < 10 && !selected.Contains(id) && database.FindHero(id) != null && !level.BossHeroIds.Contains(id)) selected.Add(id);
            Dictionary<string, UiButton> cards = new Dictionary<string, UiButton>();
            Dictionary<string, Text> states = new Dictionary<string, Text>();
            Dictionary<string, Text> names = new Dictionary<string, Text>();
            Dictionary<string, Text> roles = new Dictionary<string, Text>();
            Dictionary<string, UiButton> choices = new Dictionary<string, UiButton>();
            Dictionary<string, Outline> focuses = new Dictionary<string, Outline>();
            Dictionary<int, UiButton> tiers = new Dictionary<int, UiButton>();
            Text count = AppTheme.Label(ui, root, "", 1465, 196, 370, 70, 40, BattlePalette.Jade);
            Text hint = AppTheme.Label(ui, root, "可先采用推荐阵容，再按喜好调整。", 1468, 282, 350, 105, 25, BattlePalette.Muted);
            Text armyPreview = AppTheme.Label(ui, root, "", 635, 987, 710, 66, 21, BattlePalette.Muted);
            armyPreview.gameObject.name = "名单军势预览";
            Text description = AppTheme.Label(ui, root, "点击武将查看出手节奏和三阶成长。", 1468, 480, 380, 440, 24, BattlePalette.Ink, TextAnchor.UpperLeft);
            description.gameObject.name = "选将详情";
            HeroDefinition inspected = null;
            int inspectedLevel = 1;
            System.Action inspect = () =>
            {
                // 查看焦点和阶数只影响详情，不使用入队底色表达。
                foreach (KeyValuePair<string, Outline> focus in focuses)
                    focus.Value.enabled = inspected != null && focus.Key == inspected.Id;
                foreach (KeyValuePair<int, UiButton> tier in tiers)
                {
                    bool active = tier.Key == inspectedLevel;
                    tier.Value.Background.color = active ? BattlePalette.Jade : BattlePalette.Mist;
                    tier.Value.Label.color = active ? BattlePalette.Silk : BattlePalette.Ink;
                }
                if (inspected == null) return;
                description.text = inspected.Name + " · " + FactionTitle(inspected.Faction) + " · " + inspected.Role + " · " + inspectedLevel + "阶\n" +
                    "招募 " + inspected.Cost + " · 最大射界 " + Mathf.RoundToInt(BattleSession.GetMaximumRange(inspected, inspectedLevel)) +
                    "\n基础威力 " + Mathf.RoundToInt(inspected.Damage) + "\n" +
                    BattleSession.GetAttackTimingDescription(inspected, inspectedLevel) + "\n\n" +
                    BattleSession.GetGrowthDescription(inspected, inspectedLevel);
            };
            for (int tier = 1; tier <= 3; tier++)
            {
                int capturedTier = tier;
                UiButton preview = AppTheme.Button(ui, root, tier + "阶", 1468 + (tier - 1) * 122, 398, 108, 72,
                    () => { inspectedLevel = capturedTier; inspect(); }, BattlePalette.Mist, 25);
                preview.Rect.name = "选将成长" + tier;
                tiers[tier] = preview;
            }
            UiButton begin = null;
            System.Action refresh = () =>
            {
                count.text = "已选 " + selected.Count + " / 10";
                string prediction = "名单可达：";
                foreach (string faction in new[] { "wei", "shu", "wu", "qun" })
                {
                    int members = 0;
                    foreach (string id in selected) if (database.FindHero(id).Faction == faction) members++;
                    int tier = FactionRules.GetTier(members);
                    prediction += FactionTitle(faction) + (tier > 0 ? tier + "人档" : "未成") + "  ";
                }
                armyPreview.text = prediction + "\n名单预览，实际招募后才生效";
                if (begin != null) begin.Button.interactable = selected.Count > 0;
                foreach (HeroDefinition hero in database.Heroes)
                {
                    if (!cards.ContainsKey(hero.Id)) continue;
                    bool excluded = level.BossHeroIds.Contains(hero.Id);
                    bool active = selected.Contains(hero.Id);
                    cards[hero.Id].Background.color = excluded ? BattlePalette.Line : active ? BattlePalette.Mist : Color.Lerp(BattlePalette.Silk, BattlePalette.Mist, .3f);
                    states[hero.Id].text = excluded ? "本关敌将" : active ? "已入阵容" : "饷 " + hero.Cost;
                    choices[hero.Id].Label.text = excluded ? "禁选" : active ? "✓" : "+";
                    choices[hero.Id].Background.color = excluded ? BattlePalette.Line : active ? BattlePalette.Jade : BattlePalette.Mist;
                    choices[hero.Id].Label.color = excluded ? BattlePalette.Muted : active ? BattlePalette.Silk : BattlePalette.Ink;
                    states[hero.Id].color = excluded ? BattlePalette.Muted : BattlePalette.Ink;
                    names[hero.Id].color = excluded ? BattlePalette.Muted : BattlePalette.Ink;
                    roles[hero.Id].color = excluded ? BattlePalette.Muted : BattlePalette.Ink;
                }
                inspect();
            };
            for (int i = 0; i < database.Heroes.Count; i++)
            {
                HeroDefinition hero = database.Heroes[i];
                int col = i % 5;
                int row = i / 5;
                UiButton card = AppTheme.Button(ui, root, "", 50 + col * 276, 185 + row * 151, 258, 134, () =>
                {
                    inspected = hero;
                    inspectedLevel = 1;
                    inspect();
                    GameAudio.Button();
                }, BattlePalette.Silk, 28, false);
                card.Rect.name = "选将卡_" + hero.Id;
                ui.Border(card.Background, BattlePalette.Jade, 4);
                focuses[hero.Id] = card.Background.GetComponent<Outline>();
                focuses[hero.Id].enabled = false;
                AppTheme.Box(ui, card.Rect, "将牌底线", 0, 133, 258, 1, BattlePalette.Line);
                // 为88×88独立加入区留出右侧空间，正文保持完整单行。
                ui.Picture(card.Rect, UiFactory.PortraitPath(hero.Id), 4, 13, 64, 108);
                names[hero.Id] = AppTheme.Title(ui, card.Rect, hero.Name, 70, 3, 96, 40, 30, BattlePalette.Ink);
                Text factionMark = AppTheme.Label(ui, card.Rect, FactionTitle(hero.Faction), 182, 4, 60, 32, 23, BattlePalette.Jade, TextAnchor.MiddleCenter);
                factionMark.gameObject.name = "阵营_" + hero.Id;
                // 按字体实际生成的字框顺排，避免中文行高大于字号时相互覆盖。
                float roleY = 3 + names[hero.Id].rectTransform.rect.height + 2;
                roles[hero.Id] = AppTheme.Label(ui, card.Rect, hero.Role, 70, roleY, 96, 36, 24, BattlePalette.Ink);
                float stateY = roleY + roles[hero.Id].rectTransform.rect.height + 2;
                states[hero.Id] = AppTheme.Label(ui, card.Rect, "", 70, stateY, 96, 34, 23, BattlePalette.Ink);
                choices[hero.Id] = AppTheme.Button(ui, card.Rect, "+", 168, 44, 88, 88, () =>
                {
                    bool rejected = false;
                    if (level.BossHeroIds.Contains(hero.Id)) { hint.text = "本关敌将不可加入己方阵容。"; rejected = true; }
                    else if (selected.Contains(hero.Id)) { selected.Remove(hero.Id); hint.text = "已将" + hero.Name + "移出阵容。"; }
                    else if (selected.Count >= 10) { hint.text = "阵容已满10人，请先移出一名守将。"; rejected = true; }
                    else { selected.Add(hero.Id); hint.text = "已将" + hero.Name + "加入阵容。"; }
                    if (rejected) GameAudio.Reject(); else GameAudio.Button();
                    refresh();
                }, BattlePalette.Mist, 36, false);
                choices[hero.Id].Rect.name = "选择_" + hero.Id;
                cards[hero.Id] = card;
            }
            AppTheme.Button(ui, root, "推荐阵容", 50, 985, 275, 66, () =>
            {
                selected.Clear();
                foreach (string id in level.RecommendedRoster)
                    if (selected.Count < 10 && !selected.Contains(id) && !level.BossHeroIds.Contains(id) && database.FindHero(id) != null) selected.Add(id);
                if (selected.Count == 0)
                    foreach (HeroDefinition hero in database.Heroes)
                        if (selected.Count < 10 && !level.BossHeroIds.Contains(hero.Id)) selected.Add(hero.Id);
                hint.text = "已采用推荐阵容，可继续调整。";
                refresh();
            }, BattlePalette.Mist, 26);
            AppTheme.Button(ui, root, "清空阵容", 345, 985, 245, 66, () => { selected.Clear(); refresh(); }, BattlePalette.Mist, 26);
            begin = AppTheme.Button(ui, root, "进入沙盘  ›", 1450, 968, 400, 80, () => game.BeginBattle(level, selected));
            refresh();
        }

        private string BossNames(LevelDefinition level)
        {
            List<string> names = new List<string>();
            foreach (string id in level.BossHeroIds)
            { HeroDefinition hero = database.FindHero(id); names.Add(hero == null ? id : hero.Name); }
            return string.Join("、", names.ToArray());
        }

        private static string FactionTitle(string id)
        { return id == "wei" ? "魏" : id == "shu" ? "蜀" : id == "wu" ? "吴" : id == "qun" ? "群" : "未属"; }

        private static string BossSkill(string skill)
        {
            switch (skill)
            {
                case "charge": return "冲阵：蓄势后加速冲关。提前招募、升级覆盖路线的守将。";
                case "summon": return "援军：召唤随从。提前部署清群守将，技能会自动触发。";
                case "guard": return "固守：恢复生命，施技后进入破绽期。提前升级沿线守将。";
                case "rage": return "怒袭：短时迅行，半血后加速。提前部署减速或控制守将。";
                case "disrupt": return "扰阵：延后附近守将出手。备战时分散布阵，提前安排升级。";
                default: return "敌将会自动施技，半血后加速。提前招募、升级沿线守将。";
            }
        }
    }
}
