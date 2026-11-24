using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 本轮独立验收，不覆盖历史画面。
public static class UiFactionArmyChecks
{
    public static void Run()
    {
        string output = Path.GetFullPath("../docs/ui/runtime-faction-army-20261112");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "verification.txt"), "本轮验收进行中，尚未通过");
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        var holder = new GameObject("遮挡回归", typeof(RectTransform));
        try
        {
            var view = new UnitView(new UiFactory(), (RectTransform)holder.transform, false);
            view.SetDefinition(database.FindHero("yuan_shao").SpriteId, "袁绍", BattleLayout.HeroSize, false);
            view.Place(new Point2(700, 600), 2, 1, false, false, 0);
            Require(!holder.GetComponentsInChildren<Text>().Any(t => t.text == "袁绍"), "战斗常态仍显示遮挡人物的大姓名牌");
            var enemy = new UnitView(new UiFactory(), (RectTransform)holder.transform, true);
            enemy.SetDefinition("Game/Enemies/infantry", "", 112, false);
            enemy.Place(new Point2(750, 500), 0, 1, false, false, 0);
            Require(!enemy.Root.Find("生命底").gameObject.activeSelf, "满血普通敌兵仍有常驻血条");
        }
        finally { UnityEngine.Object.DestroyImmediate(holder); }
        VerifyUpgradeState();
        VerifyPlanningAndWaiting(database, output);
        VerifyRuntime(database, output);
        UiBattleRenderChecks.RunFactionArmy();
        AppIconSetup.Verify();
        File.WriteAllText(Path.Combine(output, "verification.txt"), "本轮验收通过 UTC=" + DateTime.UtcNow.ToString("o"));
    }

    private static void VerifyUpgradeState()
    {
        var state = new UpgradeFeedbackState();
        state.Upgrade("lv_bu", 2); state.Upgrade("lv_bu", 3);
        Require(!state.Consume("lv_bu", 2, 10), "连升错误消费旧阶施放");
        Require(state.WaitingLevel("lv_bu") == 3 && !state.Consume("zhou_yu", 3, 10), "强调串到其他实例");
        Require(state.Consume("lv_bu", 3, 11) && !state.Consume("lv_bu", 3, 11), "同序列多目标重复强调");
        state.Upgrade("yuan_shao", 2); state.Clear();
        Require(!state.Consume("yuan_shao", 2, 12), "终局清理后残留等待强调");
    }

    private static void VerifyPlanningAndWaiting(GameDatabase source, string output)
    {
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(source));
        LevelDefinition level = database.FindLevel("huangjin");
        level.InitialGold = 5000; level.Waves[0].StartDelay = 100;
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.ShowRoster(level);
            Require(scene.Root.GetComponentsInChildren<Text>().Count(t => t.name.StartsWith("阵营_", StringComparison.Ordinal)) == 25, "战前25将缺少阵营标记");
            scene.Root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>().text == "推荐阵容").onClick.Invoke();
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.name == "名单军势预览" && t.text.Contains("名单可达") && t.text.Contains("实际招募")), "战前名单预览没有区分实际招募");
            CaptureThree(scene, output, "roster-faction-preview");
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu", "yuan_shao", "diao_chan" });
            BattleSession session = scene.Flow.CurrentSession;
            Require(session.TryRecruit("lv_bu", 2) == null, "孤阵预览夹具招募失败"); scene.Flow.Tick(0);
            scene.Root.GetComponentsInChildren<Button>().Single(b => b.name == "战斗武将卡" && b.GetComponentsInChildren<Text>().Any(t => t.text == "貂蝉")).onClick.Invoke();
            BattleView battle = (BattleView)Read(scene.Flow, "battle");
            SlotDefinition near = level.Slots.First(s => s.Id != 2 && Point2.Distance(s.Position, session.Heroes[0].Position) < 220);
            typeof(BattleView).GetField("hoveredSlot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, near.Id);
            typeof(BattleView).GetMethod("RefreshHud", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, null);
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.name == "成长变化" && t.text.Contains("将影响1名")) && session.Heroes.Count == 1, "招募前预览没有提示既有群将资格受影响，或误执行招募");
            CaptureThree(scene, output, "qun-before-recruit-impact");
            Require(session.TryUpgrade(2) == null && session.TryUpgrade(2) == null, "无目标连升失败"); scene.Flow.Tick(0);
            var waiting = (UpgradeFeedbackState)Read(battle, "upgrades");
            Require(waiting.WaitingLevel("lv_bu") == 3 && session.TryUpgrade(2) != null, "连升没有保留最新阶或满级仍可升级");
            session.Start(); scene.Flow.Tick(1);
            Require(session.Enemies.Count == 0 && waiting.WaitingLevel("lv_bu") == 3, "无目标时制造特色施放或消费强调");
            session.SetPaused(true); scene.Flow.Tick(2); Require(waiting.WaitingLevel("lv_bu") == 3, "暂停消费了待施放强调");
        }
        using (var fresh = new UiRefinementChecks.Scene(database))
        {
            fresh.Flow.BeginBattle(level, new List<string> { "lv_bu" });
            Require(((UpgradeFeedbackState)Read(Read(fresh.Flow, "battle"), "upgrades")).WaitingLevel("lv_bu") == 0, "重开残留上局强调");
        }
    }

    private static void VerifyRuntime(GameDatabase source, string output)
    {
        var trace = new List<BattleEvent>();
        var captured = new HashSet<string>();
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(source));
        LevelDefinition level = database.FindLevel("huangjin");
        level.Id = "army_readability_fixture"; level.Name = "军势验收"; level.InitialGold = 50000;
        level.IncomePerSecond = 0; level.BossHeroIds.Clear(); level.Waves.Clear();
        level.Waves.Add(new WaveDefinition { Name = "隔离表现夹具", StartDelay = 0 });
        level.Routes.Clear();
        foreach (int slotId in new[] { 2, 10, 12 })
        {
            Point2 point = level.Slots.Single(s => s.Id == slotId).Position;
            level.Routes.Add(new RouteDefinition { Id = "fixture_" + slotId, Points = new List<Point2> { new Point2(point.X + 90, point.Y), new Point2(point.X + 220, point.Y) } });
        }
        foreach (RouteDefinition route in level.Routes)
            level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = "infantry", RouteId = route.Id, Count = 35, Interval = .2f, SpeedScale = .3f });
        database.FindEnemy("infantry").MaxHealth = 50000;
        database.FindEnemy("infantry").Speed = .01f;
        var roster = new List<string> { "zhou_yu", "yuan_shao", "lv_bu", "lu_xun", "lv_meng", "taishi_ci", "diao_chan", "hua_tuo", "zhang_jiao", "guan_yu" };
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, roster);
            BattleSession session = scene.Flow.CurrentSession;
            Require(session.FactionEnabled && session.RuleVersion == GameProgress.FastestRuleVersion, "实际游戏没有启用军势或成绩版本不一致");
            int[] ids = new[] { 2, 10, 12 }.Concat(level.Slots.Select(s => s.Id).Where(id => id != 2 && id != 10 && id != 12).Take(7)).ToArray();
            for (int i = 0; i < roster.Count; i++) Require(session.TryRecruit(roster[i], ids[i]) == null, "十将回归招募失败：" + roster[i]);
            scene.Flow.Tick(0);
            Require(session.GetFactionCount("wu") == 4 && session.GetFactionTier("wu") == 4 && session.GetFactionCount("qun") == 5, "军势统计不是实际部署人数");
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("四人成势")), "底栏缺少实际军势档位");
            Require(!scene.Root.GetComponentsInChildren<RectTransform>().Any(t => t.name == "名签底"), "袁绍10号位与吕布12号位仍有大姓名牌遮挡");
            Require(session.TryUpgrade(2) == null && session.TryUpgrade(10) == null && session.TryUpgrade(12) == null, "二阶升级失败");
            scene.Flow.Tick(0);
            CaptureThree(scene, output, "fixture-ten-heroes-tier2");
            session.Start();
            for (int i = 0; i < 660; i++)
            {
                int beforeCount = trace.Count;
                Step(scene, 1f / 30, trace);
                CaptureCastEvents(scene, output, trace, beforeCount, captured);
                if (i == 240) CaptureThree(scene, output, "fixture-attacks-tier2");
            }
            foreach (string id in roster.Take(3))
                Require(trace.Any(e => e.Kind == "skill_cast" && e.HeroId == id && e.HeroLevel == 2), "未出现真实二阶特色施放：" + id);
            Require(session.TryUpgrade(2) == null && session.TryUpgrade(10) == null && session.TryUpgrade(12) == null, "三阶升级失败");
            scene.Flow.Tick(0);
            var battle = (BattleView)Read(scene.Flow, "battle");
            var waiting = (UpgradeFeedbackState)Read(battle, "upgrades");
            Require(waiting.WaitingLevel("lv_bu") == 3, "升级没有等待实际技能");
            session.SetPaused(true); float before = session.Time; scene.Flow.Tick(2);
            Require(session.Time == before && waiting.WaitingLevel("lv_bu") == 3, "暂停推进施放等待");
            CaptureThree(scene, output, "fixture-paused-tier3");
            session.SetPaused(false);
            for (int i = 0; i < 420; i++)
            {
                int beforeCount = trace.Count;
                Step(scene, 1f / 30, trace);
                CaptureCastEvents(scene, output, trace, beforeCount, captured);
                if (i == 140) CaptureThree(scene, output, "fixture-attacks-tier3");
            }
            foreach (string id in roster.Take(3))
            {
                Require(trace.Any(e => e.Kind == "skill_cast" && e.HeroId == id && e.HeroLevel == 3), "未出现真实三阶施放：" + id);
                Require(waiting.WaitingLevel(id) == 0, "真实三阶施放没有消费强调：" + id);
            }
            var effects = Read(battle, "effects");
            Require(((System.Collections.ICollection)Read(effects, "active")).Count <= 90, "反馈突破共享预算");
            scene.Root.GetComponentsInChildren<Button>().Single(b => b.name == "军势_wu").onClick.Invoke();
            Require(session.State == BattleState.Paused && scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("群是玩法分类")), "军势详情未暂停或规则缺失");
            CaptureThree(scene, output, "fixture-army-rules");
            Require(battle.CloseDetails(true) && session.State == BattleState.Running, "军势详情关闭没有恢复");
            typeof(BattleView).GetMethod("SetNotice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(battle, new object[] { "隔离夹具Boss预警", true });
            float warningUntil = (float)Read(battle, "noticeUntil");
            HeroUnit ally = session.Heroes.Single(h => h.Definition.Id == "lu_xun");
            Require(session.TryUpgrade(ally.SlotId) == null, "预警竞争夹具升级失败");
            scene.Flow.Tick(0);
            Require((float)Read(battle, "noticeUntil") == warningUntil && scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "隔离夹具Boss预警"), "升级截短或覆盖Boss预警");
        }
        File.WriteAllText(Path.Combine(output, "actual-events.json"), JsonUtility.ToJson(new EventEvidence { Events = trace }, true));
    }

    private static void CaptureCastEvents(UiRefinementChecks.Scene scene, string output, List<BattleEvent> events, int start, HashSet<string> captured)
    {
        for (int i = start; i < events.Count; i++)
        {
            BattleEvent item = events[i];
            if ((item.Kind != "skill_cast" && item.Kind != "skill_charge") ||
                (item.HeroId != "lv_bu" && item.HeroId != "yuan_shao" && item.HeroId != "zhou_yu")) continue;
            string name = "fixture-" + item.HeroId + "-tier" + item.HeroLevel + "-" + item.Kind;
            if (captured.Add(name)) CaptureThree(scene, output, name);
        }
    }

    private static void Step(UiRefinementChecks.Scene scene, float delta, List<BattleEvent> trace)
    {
        scene.Flow.CurrentSession.Tick(delta);
        foreach (BattleEvent item in (List<BattleEvent>)Read(scene.Flow.CurrentSession, "events"))
            trace.Add(JsonUtility.FromJson<BattleEvent>(JsonUtility.ToJson(item)));
        scene.Flow.Tick(0);
    }
    private static object Read(object target, string name)
    { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target); }
    private static void CaptureThree(UiRefinementChecks.Scene scene, string output, string name)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
        {
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24), previous = scene.Camera.targetTexture, active = RenderTexture.active;
            float aspect = scene.Camera.aspect; Texture2D pixels = null;
            try
            {
                scene.Camera.targetTexture = target; scene.Camera.aspect = (float)size.x / size.y; Canvas.ForceUpdateCanvases();
                foreach (Text label in scene.Root.GetComponentsInChildren<Text>()) if (!string.IsNullOrEmpty(label.text)) UiTypographyChecks.Check(label, name + "/" + label.name + "/" + label.text);
                scene.Camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + "-" + size.x + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                scene.Camera.targetTexture = previous; scene.Camera.aspect = aspect; RenderTexture.active = active;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
            }
        }
    }
    [Serializable] private sealed class EventEvidence { public List<BattleEvent> Events; }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("军势与反馈专项：" + message); }
}
