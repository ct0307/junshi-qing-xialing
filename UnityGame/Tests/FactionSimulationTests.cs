using System;
using System.Collections.Generic;
using System.Linq;
using YanyiSandpan.Domain;

internal static class FactionSimulationTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string[] Roster(GameDatabase db, LevelDefinition level, string[] factions, int[] counts)
    {
        HashSet<string> excluded = new HashSet<string>(level.BossHeroIds);
        foreach (SpawnGroup g in level.Waves.SelectMany(w => w.Groups)) { EnemyDefinition enemy = db.FindEnemy(g.EnemyId); if (enemy.IsBoss) excluded.Add(enemy.CharacterId); }
        List<string> ids = new List<string>();
        for (int i = 0; i < factions.Length; i++) ids.AddRange(db.Heroes.Where(h => h.Faction == factions[i] && !excluded.Contains(h.Id)).Take(counts[i]).Select(h => h.Id));
        Check(ids.Count == counts.Sum() && ids.Count <= 10 && ids.Distinct().Count() == ids.Count, "非法模拟阵容"); return ids.ToArray();
    }
    private static float Coverage(RouteDefinition route, Point2 position, float range)
    {
        float score = 0, total = 0;
        for (int i = 1; i < route.Points.Count; i++) { Point2 a = route.Points[i - 1], z = route.Points[i]; float length = Point2.Distance(a, z); total += length; int n = Math.Max(1, (int)(length / 25)); for (int k = 0; k < n; k++) { float t = (k + .5f) / n; float distance = Point2.Distance(position, new Point2(a.X + (z.X - a.X) * t, a.Y + (z.Y - a.Y) * t)); if (distance < range) score += (1 - distance / range) * length / n; } }
        return score / Math.Max(1, total);
    }
    private static float Weight(HeroDefinition hero) { return hero.Skill == "heal" || hero.Skill == "supply" || hero.Skill == "assist" ? .35f : hero.Skill == "buff" || hero.Skill == "link" || hero.Skill == "vulnerable" ? .6f : 1; }
    private static void Simulate(GameDatabase db, LevelDefinition level, string label, string[] roster, bool enabled, int speed, bool earlyUpgrade)
    {
        BattleSession b = new BattleSession(db, level, roster, enabled); b.SetSpeed(speed);
        int rewards = 0, supply = 0, expenses = 0, bossKills = 0, expectedBosses = level.Waves.SelectMany(w => w.Groups).Where(g => db.FindEnemy(g.EnemyId).IsBoss).Sum(g => g.Count);
        bool coreUpgraded = false;
        for (int step = 0; step < 6000 && (b.State == BattleState.Preparation || b.State == BattleState.Running); step++)
        {
            foreach (string id in roster)
            {
                if (earlyUpgrade && b.Heroes.Count == 2 && !coreUpgraded) break;
                if (b.IsRecruited(id) || b.Gold < db.FindHero(id).Cost) continue;
                HeroDefinition hero = db.FindHero(id); SlotDefinition best = null; float bestScore = float.MinValue;
                foreach (SlotDefinition slot in level.Slots)
                {
                    if (b.Heroes.Any(h => h.SlotId == slot.Id)) continue;
                    float score = 0;
                    foreach (RouteDefinition route in level.Routes) score += Coverage(route, slot.Position, BattleSession.GetMaximumRange(hero)) / (.15f + b.Heroes.Sum(h => Coverage(route, h.Position, BattleSession.GetMaximumRange(h.Definition)) * Weight(h.Definition)));
                    // 群雄布阵仍使用真实220逻辑距离；射程覆盖与孤立收益同时参与选择。
                    if (hero.Faction == "qun" && b.Heroes.All(h => Point2.Distance(h.Position, slot.Position) >= 220)) score *= 1.15f;
                    if (score > bestScore) { bestScore = score; best = slot; }
                }
                if (best != null) { Check(b.TryRecruit(id, best.Id) == null, "合法招募失败"); expenses += hero.Cost; }
            }
            if (earlyUpgrade && b.Heroes.Count >= 2 && !coreUpgraded && b.Gold >= b.GetUpgradeCost(b.Heroes[0].SlotId)) { int cost = b.GetUpgradeCost(b.Heroes[0].SlotId); Check(b.TryUpgrade(b.Heroes[0].SlotId) == null, "核心升级失败"); expenses += cost; coreUpgraded = true; }
            if (b.Heroes.Count == roster.Length) foreach (HeroUnit h in b.Heroes.OrderBy(h => h.Level)) if (h.Level < 3 && b.Gold >= b.GetUpgradeCost(h.SlotId)) { int cost = b.GetUpgradeCost(h.SlotId); Check(b.TryUpgrade(h.SlotId) == null, "升级失败"); expenses += cost; }
            if (b.State == BattleState.Preparation) b.Start(); b.Tick(.25f);
            foreach (BattleEvent e in b.DrainEvents()) { if (e.Kind == "kill") { rewards += (int)e.Amount; if (db.Enemies.Any(enemy => enemy.IsBoss && e.Text == enemy.Name + "被击退")) bossKills++; } if (e.Kind == "notice" && e.Text.StartsWith("鲁肃补给军饷", StringComparison.Ordinal)) supply += (int)e.Amount; }
            int income = (int)Math.Floor((double)b.MarchTime * level.IncomePerSecond + .0001);
            Check(level.InitialGold + income + rewards + supply - expenses == b.Gold, level.Id + "军饷账本不守恒");
            Check(b.Heroes.All(h => b.IsInRoster(h.Definition.Id)), "Boss进入己方");
        }
        Check(b.State == BattleState.Victory || b.State == BattleState.Defeat, "模拟超时未终局");
        Console.WriteLine("军势模拟,{0},{1},enabled={2},speed={3},early={4},state={5},time={6:F1},castle={7},leak={8},boss={9}/{10},spent={11},gold={12}", level.Id, label, enabled, speed, earlyUpgrade, b.State, b.Time, b.CastleHealth, b.LeakedCount, bossKills, expectedBosses, expenses, b.Gold);
        if (label == "recommended" && enabled && speed == 1) Check(b.State == BattleState.Victory && bossKills == expectedBosses, "常速推荐阵容应全部击退Boss");
    }
    internal static void Run(GameDatabase db)
    {
        foreach (LevelDefinition level in db.Levels)
        {
            for (int speed = 1; speed <= 3; speed++) Simulate(db, level, "recommended", level.RecommendedRoster.ToArray(), true, speed, false);
            Simulate(db, level, "recommended", level.RecommendedRoster.ToArray(), false, 1, false);
            Simulate(db, level, "442-wei-shu-wu", Roster(db, level, new[] { "wei", "shu", "wu" }, new[] { 4, 4, 2 }), true, 1, false);
            Simulate(db, level, "433-shu-wu-qun", Roster(db, level, new[] { "shu", "wu", "qun" }, new[] { 4, 3, 3 }), true, 1, false);
            Simulate(db, level, "442-wu-qun-shu", Roster(db, level, new[] { "wu", "qun", "shu" }, new[] { 4, 4, 2 }), true, 1, false);
            Simulate(db, level, "early-core", level.RecommendedRoster.ToArray(), true, 1, true);
        }
    }
}
