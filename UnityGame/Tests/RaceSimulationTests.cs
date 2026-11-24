using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

// 使用真实配置与正常军饷搜索可重复策略；不改血量、不补钱、不强制终局。
internal static class RaceSimulationTests
{
    private sealed class Result
    {
        internal string Label, Policy;
        internal string[] Roster;
        internal RaceSnapshot Snapshot;
        internal BattleState State;
        internal BreachRecord FirstBreach;
        internal int Gold, Spent, Rewards, Supply;
    }
    private sealed class CoverageMap
    {
        internal readonly Dictionary<string, float[,]> Scores = new Dictionary<string, float[,]>();
        internal readonly Dictionary<int, int> SlotIndices = new Dictionary<int, int>();
        internal CoverageMap(GameDatabase db, LevelDefinition level)
        {
            for (int slot = 0; slot < level.Slots.Count; slot++) SlotIndices[level.Slots[slot].Id] = slot;
            foreach (HeroDefinition hero in db.Heroes)
            {
                var scores = new float[level.Slots.Count, level.Routes.Count];
                for (int slot = 0; slot < level.Slots.Count; slot++)
                    for (int route = 0; route < level.Routes.Count; route++)
                        scores[slot, route] = Coverage(level.Routes[route], level.Slots[slot].Position, BattleSession.GetMaximumRange(hero));
                Scores[hero.Id] = scores;
            }
        }
    }
    private static float Coverage(RouteDefinition route, Point2 position, float range)
    {
        float score = 0, total = 0;
        for (int i = 1; i < route.Points.Count; i++)
        {
            Point2 a = route.Points[i - 1], z = route.Points[i]; float length = Point2.Distance(a, z); total += length;
            int samples = Math.Max(1, (int)(length / 25));
            for (int n = 0; n < samples; n++)
            {
                float t = (n + .5f) / samples;
                float distance = Point2.Distance(position, new Point2(a.X + (z.X - a.X) * t, a.Y + (z.Y - a.Y) * t));
                if (distance < range) score += (1 - distance / range) * length / samples;
            }
        }
        return score / Math.Max(1, total);
    }
    private static float Weight(HeroDefinition hero)
    { return hero.Skill == "heal" || hero.Skill == "supply" || hero.Skill == "assist" ? .35f : hero.Skill == "buff" || hero.Skill == "link" || hero.Skill == "vulnerable" ? .6f : 1; }
    private static string[] MixedRoster(GameDatabase db, LevelDefinition level)
    {
        var excluded = new HashSet<string>(level.BossHeroIds);
        foreach (SpawnGroup group in level.Waves.SelectMany(w => w.Groups))
        { EnemyDefinition enemy = db.FindEnemy(group.EnemyId); if (enemy.IsBoss) excluded.Add(enemy.CharacterId); }
        var result = new List<string>(); string[] factions = { "wei", "shu", "wu" }; int[] counts = { 4, 4, 2 };
        for (int i = 0; i < factions.Length; i++) result.AddRange(db.Heroes.Where(h => h.Faction == factions[i] && !excluded.Contains(h.Id)).Take(counts[i]).Select(h => h.Id));
        Require(result.Count == 10 && result.Distinct().Count() == 10, "4/4/2参考名单不合法"); return result.ToArray();
    }
    private static int Speed(BattleSession b, string policy)
    {
        if (policy == "fixed2") return 2;
        if (policy == "fixed3") return 3;
        if (policy == "wait2") return b.IsWaveActive ? 1 : 2;
        if (policy == "wait3") return b.IsWaveActive ? 1 : 3;
        bool boss = b.Enemies.Any(e => e.Definition.IsBoss);
        if (policy == "low2") return !boss && b.Enemies.Count <= 2 ? 2 : 1;
        if (policy == "formed2") return !boss && b.Heroes.Count >= 4 && b.Enemies.Count < 5 ? 2 : 1;
        return 1;
    }
    private static string[] RecruitmentOrder(GameDatabase db, string[] roster, string priority)
    {
        if (priority == "fourth-faction")
        {
            string faction = roster.Select(db.FindHero).GroupBy(h => h.Faction)
                .Where(g => g.Key != "qun" && g.Count() >= 4).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key).FirstOrDefault();
            Require(faction != null, "第四人军势对照需要同营四人");
            return roster.OrderBy(id => db.FindHero(id).Faction == faction ? 0 : 1).ToArray();
        }
        if (priority == "function-first")
            return roster.OrderBy(id => Weight(db.FindHero(id)) < 1 ? 0 : 1).ToArray();
        return roster;
    }
    private static Result Simulate(GameDatabase db, LevelDefinition level, CoverageMap map, string label, string[] roster, string policy, bool early, bool fourthFaction = false)
    {
        var b = new BattleSession(db, level, roster, true); int expenses = 0, rewards = 0, supply = 0; bool coreUpgraded = false;
        string priorityFaction = fourthFaction ? db.FindHero(roster[0]).Faction : null;
        for (int step = 0; step < 6000 && (b.State == BattleState.Preparation || b.State == BattleState.Running); step++)
        {
            foreach (string id in roster)
            {
                if (early && b.Heroes.Count == 2 && !coreUpgraded) break;
                HeroDefinition hero = db.FindHero(id);
                if (fourthFaction && b.Heroes.Count(h => h.Definition.Faction == priorityFaction) < 4 && hero.Faction != priorityFaction) continue;
                if (b.IsRecruited(id) || !b.CanAffordTransaction(hero.Cost)) continue;
                SlotDefinition best = null; float bestScore = float.MinValue;
                foreach (SlotDefinition slot in level.Slots)
                {
                    if (b.Heroes.Any(h => h.SlotId == slot.Id)) continue;
                    float score = 0; int slotIndex = map.SlotIndices[slot.Id];
                    for (int route = 0; route < level.Routes.Count; route++)
                    {
                        float existing = .15f;
                        foreach (HeroUnit h in b.Heroes) existing += map.Scores[h.Definition.Id][map.SlotIndices[h.SlotId], route] * Weight(h.Definition);
                        score += map.Scores[id][slotIndex, route] / existing;
                    }
                    if (hero.Faction == "qun" && b.Heroes.All(h => Point2.Distance(h.Position, slot.Position) >= 220)) score *= 1.15f;
                    if (score > bestScore) { bestScore = score; best = slot; }
                }
                if (best != null) { Require(b.TryRecruit(id, best.Id) == null, "正常招募失败"); expenses += hero.Cost; }
            }
            if (early && b.Heroes.Count >= 2 && !coreUpgraded && b.CanAffordTransaction(b.GetUpgradeCost(b.Heroes[0].SlotId)))
            { int cost = b.GetUpgradeCost(b.Heroes[0].SlotId); Require(b.TryUpgrade(b.Heroes[0].SlotId) == null, "核心升级失败"); expenses += cost; coreUpgraded = true; }
            if (b.Heroes.Count == roster.Length)
                foreach (HeroUnit hero in b.Heroes.OrderBy(h => h.Level))
                    if (hero.Level < 3 && b.CanAffordTransaction(b.GetUpgradeCost(hero.SlotId)))
                    { int cost = b.GetUpgradeCost(hero.SlotId); Require(b.TryUpgrade(hero.SlotId) == null, "正常升级失败"); expenses += cost; }
            b.SetSpeed(Speed(b, policy)); if (b.State == BattleState.Preparation) b.Start(); b.Tick(.25f);
            foreach (BattleEvent e in b.DrainEvents())
            { if (e.Kind == "kill") rewards += (int)e.Amount; if (e.Kind == "notice" && e.Text.StartsWith("鲁肃补给军饷", StringComparison.Ordinal)) supply += (int)e.Amount; }
            int income = (int)Math.Floor((double)b.MarchTime * level.IncomePerSecond + .0001);
            Require(level.InitialGold + income + rewards + supply - expenses == b.Gold, level.Id + "账本不守恒");
            Require(b.Heroes.All(h => b.IsInRoster(h.Definition.Id)), "Boss进入己方");
        }
        Require(b.State == BattleState.Victory || b.State == BattleState.Defeat, level.Id + "模拟未终局");
        var result = new Result { Label = label, Policy = policy, Roster = roster, Snapshot = b.GetRaceSnapshot(), State = b.State,
            FirstBreach = b.FirstBreach, Gold = b.Gold, Spent = expenses, Rewards = rewards, Supply = supply };
        Console.WriteLine("竞速模拟,{0},{1},{2},state={3},eligible={4},time={5:F3},castle={6},boss={7}/{8},mask={9}",
            level.Id, label, policy, b.State, result.Snapshot.IsEligible, result.Snapshot.ElapsedSeconds, b.CastleHealth,
            b.DefeatedBossCount, b.ExpectedBossCount, result.Snapshot.SpeedMask);
        return result;
    }
    private static object Json(Result r)
    {
        RaceSnapshot s = r.Snapshot;
        return new { label = r.Label, policy = r.Policy, roster = r.Roster, state = r.State.ToString(), snapshot = s,
            speedSeconds = new[] { s.GetSpeedSeconds(1), s.GetSpeedSeconds(2), s.GetSpeedSeconds(3) }, firstBreach = r.FirstBreach,
            gold = r.Gold, spent = r.Spent, rewards = r.Rewards, supply = r.Supply };
    }
    private static int Main(string[] args)
    {
        try
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 16777216 };
            GameDatabase db = serializer.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            var reports = new List<object>(); int total = 0, qualified = 0, improvements = 0;
            foreach (LevelDefinition level in db.Levels)
            {
                var map = new CoverageMap(db, level); string[][] rosters = { level.RecommendedRoster.ToArray(), MixedRoster(db, level) };
                Require(!rosters[1].SequenceEqual(RecruitmentOrder(db, rosters[1], "fourth-faction")),
                    "第四人军势对照必须与混编默认招募顺序不同");
                var results = new List<Result>();
                Result recommended = Simulate(db, level, map, "recommended", rosters[0], "normal", false);
                Result mixed = Simulate(db, level, map, "442", rosters[1], "normal", false);
                Require(recommended.Snapshot.IsEligible && mixed.Snapshot.IsEligible, level.Id + "两套1倍参考必须全Boss击退");
                results.Add(recommended); results.Add(mixed);
                results.Add(Simulate(db, level, map, "early-core", rosters[0], "normal", true));
                Result fourth = Simulate(db, level, map, "fourth-faction", RecruitmentOrder(db, rosters[1], "fourth-faction"), "normal", false, true);
                Require(fourth.Snapshot.Actions.Where(a => a.Kind == "recruit").Take(4)
                    .Select(a => db.FindHero(a.HeroId).Faction).Distinct().Count() == 1, "第四人优先未实际形成四名同营");
                Require(!fourth.Snapshot.Actions.Select(a => a.HeroId + ":" + a.ElapsedSeconds.ToString("R"))
                    .SequenceEqual(mixed.Snapshot.Actions.Select(a => a.HeroId + ":" + a.ElapsedSeconds.ToString("R"))), "第四人对照没有真实操作差异");
                results.Add(fourth);
                results.Add(Simulate(db, level, map, "function-first", RecruitmentOrder(db, rosters[0], "function-first"), "normal", false));
                foreach (string policy in new[] { "wait2", "wait3", "low2", "formed2" })
                    for (int i = 0; i < rosters.Length; i++) results.Add(Simulate(db, level, map, i == 0 ? "recommended" : "442", rosters[i], policy, false));
                results.Add(Simulate(db, level, map, "pressure", rosters[0], "fixed2", false));
                results.Add(Simulate(db, level, map, "pressure", rosters[0], "fixed3", false));
                double reference = Math.Min(recommended.Snapshot.ElapsedSeconds, mixed.Snapshot.ElapsedSeconds);
                Result fastest = results.Where(r => r.Policy != "normal" && r.Policy != "fixed2" && r.Policy != "fixed3" &&
                    r.Snapshot.IsEligible && (r.Snapshot.SpeedMask & 6) != 0 && (r.Snapshot.SpeedMask & 1) != 0 && r.Snapshot.ElapsedSeconds < reference)
                    .OrderBy(r => r.Snapshot.ElapsedSeconds).FirstOrDefault();
                if (fastest != null)
                {
                    Result repeat = Simulate(db, level, map, "repeat-" + fastest.Label, fastest.Roster, fastest.Policy, false);
                    Require(repeat.Snapshot.IsEligible && Math.Abs(repeat.Snapshot.ElapsedSeconds - fastest.Snapshot.ElapsedSeconds) < .000001, "选中策略不能重复");
                    results.Add(repeat); improvements++;
                }
                reports.Add(new { levelId = level.Id, referenceSeconds = reference, improved = fastest != null,
                    fastest = fastest == null ? null : Json(fastest), results = results.Select(Json).ToArray() });
                total += results.Count; qualified += results.Count(r => r.Snapshot.IsEligible);
            }
            string output = args.Length > 1 ? args[1] : "race-simulation-results.json";
            File.WriteAllText(output, serializer.Serialize(new { total = total, qualified = qualified, improvedLevels = improvements, levels = reports.ToArray() }));
            Console.WriteLine("竞速策略验收：{0}局，{1}局合格，{2}/{3}关动态提速更快且复跑一致；详细结果：{4}", total, qualified, improvements, db.Levels.Count, output);
            Require(improvements == db.Levels.Count, "尚有未找到更快合格动态策略的关卡，保留结果继续诊断"); return 0;
        }
        catch (Exception e) { Console.WriteLine("竞速策略验收失败：" + e); return 1; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
