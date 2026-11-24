using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 真实uGUI成长与操作验收；记录只使用隔离内存，不写玩家存档。
public static class UiGrowthChecks
{
    private static string Output = Path.GetFullPath("../docs/ui/runtime-growth");

    // 精修回归复用真实成长检查，临时输出至本轮目录，结束后恢复旧入口路径。
    public static void Verify(GameDatabase database, string output)
    {
        string previous = Output;
        try
        {
            Output = Path.GetFullPath(output);
            Verify(database);
        }
        finally { Output = previous; }
    }

    public static void Run()
    {
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        Verify(database);
    }

    public static void Verify(GameDatabase database)
    {
        Directory.CreateDirectory(Output);
        using (var scene = new GrowthScene(database))
        {
            LevelDefinition level = database.FindLevel("huangjin");
            CaptureThree(scene, "menu");
            scene.Flow.ShowLevels();
            Require(scene.Root.GetComponentsInChildren<Text>().Count(t => t.name == "关卡最快" && t.text == "本版最快 —") == database.Levels.Count,
                "无本版有效记录时每关应显示最快破折号");
            CaptureThree(scene, "levels-empty-records");
            scene.Flow.ShowRoster(level);
            Button card = scene.Root.GetComponentsInChildren<Button>().First(b =>
                b.GetComponentsInChildren<Text>().Any(t => t.text == "关羽"));
            card.onClick.Invoke();
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 0 / 10"),
                "查看卡片详情改变了阵容：卡片主体只能查看，必须由独立勾选加入或移出");
            Require(card.GetComponentsInChildren<Button>().Length == 2, "选将卡缺少独立勾选触区");
            Button choice = card.GetComponentsInChildren<Button>().First(b => b != card);
            choice.onClick.Invoke();
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 1 / 10"), "独立勾选未加入阵容");
            card.onClick.Invoke();
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 1 / 10"), "已选卡查看详情错误移出了阵容");
            foreach (HeroDefinition hero in database.Heroes)
            {
                Button subject = scene.Root.GetComponentsInChildren<Button>().First(b => b.name == "选将卡_" + hero.Id);
                subject.onClick.Invoke();
                for (int tier = 1; tier <= 3; tier++)
                {
                    NamedButton(scene.Root, "选将成长" + tier).onClick.Invoke();
                    Text detail = NamedText(scene.Root, "选将详情");
                    Require(detail.text.Contains(BattleSession.GetGrowthDescription(hero, tier)), "选将成长未读取实际等级：" + hero.Name + tier);
                    UiTypographyChecks.Check(detail, hero.Name + tier + "阶选将详情");
                }
            }
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 1 / 10"), "查看25将三阶成长时更改了阵容");
            CaptureThree(scene, "roster-details-do-not-select");
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.name == "实际用时"), "战中缺少实际用时");
            Button march = scene.Root.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "进军倍率3");
            Require(march != null, "战中缺少明确命名的进军倍率按钮");
            march.onClick.Invoke(); NamedButton(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(scene.Flow.CurrentSession.Speed == 3, "进军按钮未提供3倍");
            Transform deck = scene.Root.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == "五张武将卡");
            Require(deck != null && deck.GetComponentsInChildren<Button>().Count(b => b.name == "战斗武将卡") == 5,
                "底部应显示五张武将卡，十人阵容分两页");
            Button page = deck.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.Contains("/"));
            Require(page.GetComponentInChildren<Text>().text.Contains("1/2"), "十人阵容首页应为1/2");
            page.onClick.Invoke();
            Require(FindRect(scene.Root, "五张武将卡").GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<Text>().text.Contains("2/2")), "五卡翻页未到2/2");
            NamedButton(scene.Root, "进军倍率1").onClick.Invoke();
            Click(scene.Root, "开战");
            scene.Flow.Tick(1);
            Require(NamedText(scene.Root, "军饷实秒收入").text.Contains("+" + level.IncomePerSecond.ToString("0.##") + "/实秒"), "1倍军饷实秒收入错误");
            NamedButton(scene.Root, "进军倍率2").onClick.Invoke();
            scene.Flow.Tick(1);
            Require(NamedText(scene.Root, "军饷实秒收入").text.Contains("+" + (level.IncomePerSecond * 2).ToString("0.##") + "/实秒"), "2倍军饷实秒收入未即时换算");
            NamedButton(scene.Root, "进军倍率3").onClick.Invoke();
            scene.Flow.Tick(1);
            Require(NamedText(scene.Root, "军饷实秒收入").text.Contains("+" + (level.IncomePerSecond * 3).ToString("0.##") + "/实秒"), "3倍军饷实秒收入未即时换算");
            Require(Math.Abs(scene.Flow.CurrentSession.ElapsedSeconds - 3) < .001 && NamedText(scene.Root, "实际用时").text == "00:03",
                "1/2/3倍切换后的3实秒被错误显示为倍率累计时间");
            scene.Flow.PauseBattle();
            double elapsed = scene.Flow.CurrentSession.ElapsedSeconds;
            scene.Flow.Tick(2);
            Require(scene.Flow.CurrentSession.ElapsedSeconds == elapsed, "暂停时实际计时仍推进");
            CaptureThree(scene, "paused-clock");
        }
        VerifyGrowthPreview(database);
        VerifySkillGeometry(database);
        VerifySupplyAdvance(database);
        VerifyRecordResults(database);
        Debug.Log("成长UI真实交互检查通过：25将三阶查看不改阵容、五卡两页、成长范围与费用、1/2/3进军实秒计时、暂停、首次刷新及较慢纪录、真实突破损失；三尺寸输出 " + Output);
    }

    private static void VerifyGrowthPreview(GameDatabase original)
    {
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        level.Id = "growth-preview-fixture"; level.Name = "成长界面验收"; level.InitialGold = 6000;
        level.BossHeroIds.Clear();
        using (var scene = new GrowthScene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "huang_zhong", "zhao_yun", "lv_bu", "zhang_fei", "lu_su" });
            SelectBattleHero(scene.Root, "黄忠");
            ClickSlot(scene.Root, 0);
            HeroUnit hero = scene.Flow.CurrentSession.Heroes[0];
            for (int tier = 1; tier <= 3; tier++)
            {
                Text title = NamedText(scene.Root, "成长标题");
                Require(title.text.Contains(tier + "阶"), "固定预览当前等级不符");
                float radius = BattleSession.GetMaximumRange(hero.Definition, tier);
                Require(Math.Abs(FindRect(scene.Root, "攻击范围").sizeDelta.x - radius * 2) < .01f, "当前覆盖仍使用旧等级范围");
                if (tier < 3)
                {
                    string expectedGrowth = tier == 1 ? scene.Flow.CurrentSession.GetUpgradeDescription(hero.SlotId).Split('\n')[0] :
                        "已获：" + HeroGrowth.GetUpgradeSummary(hero.Definition, tier - 1).Split('\n')[0].Split('；')[0] + "\n等待下次真实特色施放";
                    Require(NamedText(scene.Root, "成长变化").text == expectedGrowth, "当前成长变化或等待施放与规则说明不一致");
                    float nextRadius = BattleSession.GetMaximumRange(hero.Definition, tier + 1);
                    RectTransform nextRange = scene.Root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "下一阶攻击范围");
                    if (Math.Abs(nextRadius - radius) > .1f)
                        Require(nextRange.gameObject.activeInHierarchy && Math.Abs(nextRange.sizeDelta.x - nextRadius * 2) < .01f,
                            "不同射程的下一阶覆盖应可见并读取真实成长");
                    else Require(!nextRange.gameObject.activeInHierarchy, "相同射程不应重复显示下一阶覆盖圆环");
                }
                CaptureThree(scene, "upgrade-tier-" + tier);
                if (tier < 3) Click(scene.Root, "升级 ");
            }
            Button max = FindButton(scene.Root, "已满级");
            Require(!max.interactable && !scene.Root.GetComponentsInChildren<RectTransform>().Any(t => t.name == "下一阶攻击范围"), "满级状态仍可升级或显示不存在的下一阶");
            Click(scene.Root, "详情");
            CaptureThree(scene, "growth-full-details");
            Click(scene.Root, "关闭");
            SelectBattleHero(scene.Root, "赵云"); ClickSlot(scene.Root, 1);
            Require(NamedText(scene.Root, "军令通知").text.Contains("方向示意，实际朝敌军"), "定向形状预览未说明参考朝向");
            CaptureThree(scene, "shape-zhao-yun-tier-1-to-2");
            SelectBattleHero(scene.Root, "吕布"); ClickSlot(scene.Root, 2);
            CaptureThree(scene, "shape-lv-bu-tier-1-to-2");
            Click(scene.Root, "升级 ");
            CaptureThree(scene, "shape-lv-bu-tier-2-to-3");
            Click(scene.Root, "开战"); scene.Flow.Tick(.5f);
            double readingAt = scene.Flow.CurrentSession.ElapsedSeconds;
            Click(scene.Root, "详情");
            Require(scene.Flow.CurrentSession.State == BattleState.Paused, "战中打开详情未自动暂停");
            scene.Flow.Tick(2);
            Require(scene.Flow.CurrentSession.ElapsedSeconds == readingAt, "阅读详情计入了通关时间");
            scene.Flow.HandleBack();
            Require(scene.Flow.CurrentSession.State == BattleState.Running && !scene.Root.GetComponentsInChildren<RectTransform>().Any(t => t.name == "武将技能详情"), "返回未关闭详情并恢复其持有的暂停");
            Click(scene.Root, "详情"); scene.Flow.PauseForSystem();
            scene.Flow.Tick(2);
            Require(scene.Flow.CurrentSession.State == BattleState.Paused && scene.Flow.CurrentSession.ElapsedSeconds == readingAt &&
                scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "推演暂停"), "详情期间失焦未保持暂停等待手动继续");
            Click(scene.Root, "继续守关");
            Require(scene.Flow.CurrentSession.State == BattleState.Running, "失焦后无法手动继续");
            Click(scene.Root, "详情"); scene.Flow.RetryBattle();
            Require(scene.Flow.CurrentSession.State == BattleState.Preparation && scene.Flow.CurrentSession.ElapsedSeconds == 0,
                "阅读期间重开错误恢复了旧会话");
        }
        level.InitialGold = database.FindHero("zhao_yun").Cost;
        using (var scene = new GrowthScene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "zhao_yun" });
            SelectBattleHero(scene.Root, "赵云"); ClickSlot(scene.Root, 0);
            Button upgrade = FindButton(scene.Root, "升级 ");
            Require(!upgrade.interactable && upgrade.GetComponentInChildren<Text>().text.Contains("还差 " + scene.Flow.CurrentSession.GetUpgradeCost(level.Slots[0].Id)),
                "不足升级费用时没有固定差额或按钮仍可用");
            CaptureThree(scene, "upgrade-unaffordable");
        }
    }

    private static void VerifySkillGeometry(GameDatabase database)
    {
        var go = new GameObject("成长形状几何检查", typeof(RectTransform), typeof(CanvasRenderer), typeof(HeroSkillPreview));
        try
        {
            HeroSkillPreview preview = go.GetComponent<HeroSkillPreview>();
            Point2 origin = new Point2(700, 500), target = new Point2(900, 500);
            HeroDefinition spear = database.FindHero("zhao_yun"), cleave = database.FindHero("lv_bu");
            preview.Show(spear, 1, origin, target);
            var points = SkillVertices(preview);
            Require(Math.Abs(MaxHalfWidth(points, HeroSkillPreview.CurrentColor, origin) - HeroGrowth.Resolve(spear, 1).LineWidth) < .05f &&
                Math.Abs(MaxHalfWidth(points, HeroSkillPreview.NextColor, origin) - HeroGrowth.Resolve(spear, 2).LineWidth) < .05f,
                "赵云当前与下一阶真实网格未由38/65半宽驱动");
            preview.Show(cleave, 1, origin, target); points = SkillVertices(preview);
            List<UIVertex> current = ColoredFill(points, HeroSkillPreview.CurrentColor), next = ColoredFill(points, HeroSkillPreview.NextColor);
            Require(current.Count > 0 && next.Count > 0 && current.All(v => v.position.x >= origin.X - .01f) && next.All(v => v.position.x >= origin.X - .01f) &&
                MaxHalfWidth(points, HeroSkillPreview.NextColor, origin) > MaxHalfWidth(points, HeroSkillPreview.CurrentColor, origin) + 1,
                "吕布一阶扇面到二阶半圆没有真实几何差异");
            preview.Show(cleave, 2, origin, target); points = SkillVertices(preview);
            Require(ColoredFill(points, HeroSkillPreview.NextColor).Any(v => v.position.x < origin.X - cleave.Range * .9f), "吕布三阶回身整圈未覆盖背向区域");
            preview.Show(cleave, 3, origin, target); points = SkillVertices(preview);
            Require(ColoredFill(points, HeroSkillPreview.CurrentColor).Count > 0 && ColoredFill(points, HeroSkillPreview.NextColor).Count == 0,
                "三阶仍绘制不存在的下一阶几何");
            preview.Clear(); Require(SkillVertices(preview).Count == 0, "清除后技能形状仍残留");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    private static List<UIVertex> SkillVertices(HeroSkillPreview preview)
    {
        using (var helper = new VertexHelper())
        {
            typeof(HeroSkillPreview).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(preview, new object[] { helper });
            var points = new List<UIVertex>(); helper.GetUIVertexStream(points); return points;
        }
    }
    private static List<UIVertex> ColoredFill(List<UIVertex> vertices, Color color)
    {
        Color32 rgb = color;
        return vertices.Where(v => v.color.r == rgb.r && v.color.g == rgb.g && v.color.b == rgb.b && v.color.a > 0 && v.color.a < 80).ToList();
    }
    private static float MaxHalfWidth(List<UIVertex> vertices, Color color, Point2 origin)
    { return ColoredFill(vertices, color).Max(v => Mathf.Abs(v.position.y + origin.Y)); }

    private static void VerifySupplyAdvance(GameDatabase original)
    {
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        level.Id = "supply-ui-fixture"; level.Name = "预支升级验收"; level.IncomePerSecond = 0; level.BossHeroIds.Clear();
        HeroDefinition supply = database.FindHero("lu_su"), target = database.FindHero("zhao_yun");
        int cost = (int)Math.Ceiling(target.Cost * .75);
        int paidSupply = supply.Cost + (int)Math.Ceiling(supply.Cost * .75) + (int)Math.Ceiling(supply.Cost * 1.25);
        level.InitialGold = paidSupply + target.Cost + cost - 20;
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "预支守候夹具", StartDelay = 0,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "infantry", RouteId = level.Routes[0].Id, Count = 1, Interval = 1, HealthScale = 1000, SpeedScale = .001f } } } };
        using (var scene = new GrowthScene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "lu_su", "zhao_yun" });
            SelectBattleHero(scene.Root, supply.Name); ClickSlot(scene.Root, 0);
            Click(scene.Root, "升级 "); Click(scene.Root, "升级 ");
            SelectBattleHero(scene.Root, target.Name); ClickSlot(scene.Root, 1);
            Click(scene.Root, "开战"); scene.Flow.Tick(.1f);
            BattleSession session = scene.Flow.CurrentSession;
            int beforeGold = session.Gold, beforeSpent = session.GoldSpent, available = session.AvailableSupplyAdvance;
            Require(beforeGold < cost && session.CanAffordTransaction(cost) && available >= cost - beforeGold, "预支夹具未形成现金不足但合法余款足够的状态");
            Button upgrade = FindButton(scene.Root, "升级 ");
            Require(upgrade.interactable && upgrade.GetComponentInChildren<Text>().text.Contains("预支 " + (cost - beforeGold)), "合法预支时真实升级按钮仍禁用或未说明预支");
            CaptureThree(scene, "upgrade-supply-advance");
            upgrade.onClick.Invoke();
            HeroUnit upgraded = session.Heroes.First(h => h.Definition.Id == target.Id);
            Require(upgraded.Level == 2 && session.Gold == 0 && session.GoldSpent == beforeSpent + cost &&
                session.AvailableSupplyAdvance == available - (cost - beforeGold), "真实预支升级未按规则原子扣费或重复发钱");
        }
    }

    private static void VerifyRecordResults(GameDatabase original)
    {
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        EnemyDefinition boss = database.Enemies.First(e => e.IsBoss && e.CastleDamage == 15);
        level.Id = "growth-record-fixture"; level.Name = "纪录与损失验收"; level.CastleMaxHealth = 30;
        level.Routes = new List<RouteDefinition> { new RouteDefinition { Id = "short", Points = new List<Point2> { new Point2(600, 400), new Point2(610, 400) } } };
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "隔离短路夹具", StartDelay = 6,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = boss.Id, RouteId = "short", Count = 1, Interval = 1 } } } };
        using (var scene = new GrowthScene(database))
        {
            for (int run = 0; run < 3; run++)
            {
                if (run == 2) level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = "infantry", RouteId = "short", Count = 1, Interval = 1 });
                scene.Flow.BeginBattle(level, new List<string> { "zhao_yun" });
                if (run == 1) { NamedButton(scene.Root, "进军倍率3").onClick.Invoke(); NamedButton(scene.Root, "倍率说明确认").onClick.Invoke(); }
                Click(scene.Root, "开战");
                for (int tick = 0; tick < 400 && scene.Flow.CurrentSession.State == BattleState.Running; tick++) scene.Flow.Tick(.05f);
                Require(scene.Flow.CurrentSession.State == BattleState.Victory, "短路突破夹具未产生真实合法胜利");
                Text record = NamedText(scene.Root, "结算时间纪录");
                Require(record.text.Contains("本次") && record.text.Contains("个人最佳"), "结算缺少本次或个人最佳时间");
                if (run == 0) Require(record.text.Contains("首次纪录") && !record.text.Contains("快了"), "首次纪录错误计算为改善差额");
                else if (run == 1) Require(record.text.Contains("快了") && !record.text.Contains("首次纪录"), "严格更快未显示刷新差额");
                else Require(!record.text.Contains("快了") && !record.text.Contains("首次纪录"), "较慢胜利错误显示刷新");
                NamedButton(scene.Root, "结算详情开关").onClick.Invoke();
                Text snapshot = NamedText(scene.Root, "最佳纪录快照");
                Require(snapshot.text.Contains("城防 15 / 30") && snapshot.text.Contains("漏兵 1") && snapshot.text.Contains("Boss突破 1"), "个人最佳快照被本局较慢统计覆盖");
                Require(scene.Flow.CurrentSession.BossBreachCount == 1, "突破快照未来自真实会话");
                CaptureThree(scene, run == 0 ? "record-first" : run == 1 ? "record-improved" : "record-slower-keeps-best");
                NamedButton(scene.Root, "结算详情开关").onClick.Invoke();
            }
            // 毫秒差额只使用隔离内存纪录，已有终局仍来自上面的真实胜利。
            scene.Progress.RecordVictory("millisecond-ui-fixture", 30, 100, 1, 0, 0);
            VictoryRecordResult millisecond = scene.Progress.RecordVictory("millisecond-ui-fixture", 30, 99.999, 1, 0, 0);
            RectTransform sample = scene.Ui.Rect(scene.Root, "毫秒结算夹具", 0, 0, 1920, 1080);
            BattleResultView.Create(scene.Ui, sample, level, scene.Flow.CurrentSession, () => { }, () => { }, null, () => { }, millisecond);
            Text precise = NamedText(sample, "结算时间纪录");
            Require(precise.text.Contains("本次 01:39.999") && precise.text.Contains("快了0.001秒") && BattleResultView.FormatRecordTime(100) == "01:40.000",
                "1毫秒刷新被格式化成同一秒或快了0秒");
            foreach (Text text in sample.GetComponentsInChildren<Text>()) UiTypographyChecks.Check(text, "毫秒结算" + text.name);
            UnityEngine.Object.DestroyImmediate(sample.gameObject);
            long bestTime = scene.Progress.GetFastestRecord(level.Id).ElapsedMilliseconds;
            level.Waves[0].StartDelay = 0;
            level.Waves[0].Groups.Clear();
            level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = boss.Id, RouteId = "short", Count = 1, Interval = 1 });
            level.Waves.Add(new WaveDefinition { Name = "后续守候", StartDelay = 30,
                Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = boss.Id, RouteId = "short", Count = 1, Interval = 1 } } });
            scene.Flow.BeginBattle(level, new List<string> { "zhao_yun" }); Click(scene.Root, "开战");
            for (int tick = 0; tick < 100 && scene.Flow.CurrentSession.LeakedCount == 0; tick++) scene.Flow.Tick(.05f);
            Require(scene.Flow.CurrentSession.State == BattleState.Running && scene.Flow.CurrentSession.CastleHealth == 15, "损失夹具应继续守关");
            Require(NamedText(scene.Root, "城防损失").text == "-15" && scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("低城防")), "真实突破后缺少-15或低城防提示");
            CaptureThree(scene, "battle-loss-continues");
            for (int tick = 0; tick < 800 && scene.Flow.CurrentSession.State == BattleState.Running; tick++) scene.Flow.Tick(.05f);
            Require(scene.Flow.CurrentSession.State == BattleState.Defeat, "第二次突破未产生真实失败");
            Require(scene.Progress.GetFastestRecord(level.Id).ElapsedMilliseconds == bestTime, "真实失败刷新了最快纪录");
            Require(NamedText(scene.Root, "结算时间纪录").text.Contains("未刷新纪录"), "失败结算未说明纪录保持");
            CaptureThree(scene, "record-defeat-unchanged");
        }
    }

    private static void SelectBattleHero(RectTransform root, string heroName)
    {
        for (int page = 0; page < 2; page++)
        {
            RectTransform deck = FindRect(root, "五张武将卡");
            Button card = deck.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentsInChildren<Text>().Any(t => t.text == heroName));
            if (card != null) { card.onClick.Invoke(); return; }
            deck.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.Contains("/")).onClick.Invoke();
        }
        throw new InvalidOperationException("战斗阵容缺少" + heroName);
    }
    private static void ClickSlot(RectTransform root, int index)
    { FindRect(root, "可交互阵位").GetChild(index).GetComponent<Button>().onClick.Invoke(); }
    private static Button NamedButton(RectTransform root, string name)
    { return root.GetComponentsInChildren<Button>().First(b => b.name == name); }
    private static Text NamedText(RectTransform root, string name)
    { return root.GetComponentsInChildren<Text>().First(t => t.name == name); }
    private static RectTransform FindRect(RectTransform root, string name)
    { return root.GetComponentsInChildren<RectTransform>().First(t => t.name == name); }
    private static Button FindButton(RectTransform root, string prefix)
    { return root.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal)); }
    private static void Click(RectTransform root, string prefix)
    {
        Button button = FindButton(root, prefix); Require(button.interactable, "不可用按钮被点击：" + prefix); button.onClick.Invoke();
    }
    private static void CaptureThree(GrowthScene scene, string name)
    {
        Capture(scene, name + "-1280", 1280, 720); Capture(scene, name + "-1920", 1920, 1080); Capture(scene, name + "-2400", 2400, 1080);
    }
    private static void Capture(GrowthScene scene, string name, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active; Texture2D pixels = null;
        try
        {
            scene.Camera.targetTexture = target; scene.Camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            foreach (Text text in scene.Root.GetComponentsInChildren<Text>())
                if (!string.IsNullOrWhiteSpace(text.text)) UiTypographyChecks.Check(text, "成长界面" + text.name);
            scene.Camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            scene.Camera.targetTexture = null; RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class GrowthScene : IDisposable
    {
        public readonly RectTransform Root;
        public readonly GameFlow Flow;
        public readonly UiFactory Ui;
        public readonly Camera Camera;
        public readonly GameProgress Progress;
        private readonly GameObject canvasObject;
        public GrowthScene(GameDatabase database)
        {
            canvasObject = new GameObject("成长UI验证", typeof(RectTransform), typeof(Canvas));
            Camera = new GameObject("成长UI截图相机", typeof(Camera)).GetComponent<Camera>();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvas.transform;
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(1920, 1080);
            Camera.orthographic = true; Camera.orthographicSize = 540;
            Camera.transform.position = new Vector3(960, -540, -100);
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = UiFactory.Deep;
            Camera.nearClipPlane = .1f; Camera.farClipPlane = 200; canvas.worldCamera = Camera;
            Ui = new UiFactory(); Root = Ui.Rect(rect, "成长界面", 0, 0, 1920, 1080);
            var values = new Dictionary<string, int>();
            Progress = new GameProgress((key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => values[key] = value, () => { });
            Flow = new GameFlow(Ui, Root, database, Progress);
        }
        public void Dispose()
        {
            Flow.Dispose(); UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(Camera.gameObject);
        }
    }
}
