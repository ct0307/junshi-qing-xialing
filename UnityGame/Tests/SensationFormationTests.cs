using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

internal static class SensationFormationTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
    private static float Coverage(RouteDefinition route, Point2 center, float radius)
    {
        float total = 0;
        for (int i = 1; i < route.Points.Count; i++)
        {
            Point2 a = route.Points[i - 1], z = route.Points[i];
            float dx = z.X - a.X, dy = z.Y - a.Y, length = Point2.Distance(a, z);
            float t = ((center.X - a.X) * dx + (center.Y - a.Y) * dy) / (length * length);
            float px = a.X + t * dx - center.X, py = a.Y + t * dy - center.Y;
            float squared = radius * radius - px * px - py * py;
            if (squared <= 0) continue;
            float half = (float)Math.Sqrt(squared) / length;
            total += Math.Max(0, Math.Min(1, t + half) - Math.Max(0, t - half)) * length;
        }
        return total;
    }
    private static object Matrix(GameDatabase db, LevelDefinition level, HeroDefinition hero, int tier, SlotDefinition slot)
    {
        // 机制夹具用于看真实施放形态；正常资源可达性由后面的整局策略另验。
        LevelDefinition fixture = Json.Deserialize<LevelDefinition>(Json.Serialize(level));
        fixture.InitialGold = 2000;
        var b = new BattleSession(db, fixture, new[] { hero.Id }, true);
        Require(b.TryRecruit(hero.Id, slot.Id) == null, "矩阵招募失败");
        for (int i = 1; i < tier; i++) Require(b.TryUpgrade(slot.Id) == null, "矩阵升级失败");
        b.Start(); int hits = 0, casts = 0; var targets = new HashSet<int>();
        for (int i = 0; i < 2100 && b.State == BattleState.Running; i++)
        {
            b.Tick(.05f);
            foreach (BattleEvent e in b.DrainEvents())
            {
                if (e.Kind == "hit" && e.HeroId == hero.Id) { hits++; targets.Add(e.TargetEnemyId); }
                if (e.Kind == "skill_cast" && e.HeroId == hero.Id) casts++;
            }
        }
        return new { heroId = hero.Id, tier = tier, slot = slot.Id,
            maximumRange = BattleSession.GetMaximumRange(hero, tier),
            roadCoverage = level.Routes.Sum(r => Coverage(r, slot.Position, BattleSession.GetMaximumRange(hero, tier))),
            directRangeCoverage = level.Routes.Sum(r => Coverage(r, slot.Position, hero.Range)),
            shape = hero.Skill, growth = HeroGrowth.Resolve(hero, tier), hits = hits, casts = casts, uniqueTargets = targets.Count,
            allyCandidates = level.Slots.Where(s => s.Id != slot.Id && Point2.Distance(s.Position, slot.Position) <= hero.Range).Select(s => s.Id).ToArray(),
            isolatedConflicts = level.Slots.Where(s => s.Id != slot.Id && Point2.Distance(s.Position, slot.Position) < 220).Select(s => s.Id).ToArray(),
            passiveIsNotSlotJustification = hero.Skill == "heal" || hero.Skill == "supply", mechanismFixture = true };
    }
    private static object Normal(GameDatabase db, LevelDefinition level, int seedSlot, string[] roster, int speed)
    {
        var b = new BattleSession(db, level, roster, true);
        var hitHeroes = new HashSet<string>(); var hitSlots = new HashSet<int>(); int peak = 0;
        if (seedSlot > 0) Require(b.TryRecruit(roster[0], seedSlot) == null, "正常资源阵位招募失败");
        for (int step = 0; step < 10000 && (b.State == BattleState.Preparation || b.State == BattleState.Running); step++)
        {
            foreach (string id in roster)
            {
                // 后段守将使用正常等待策略，先让首批抵达射界，再花累积军饷补防。
                // 这证明其后段截击用途，不把前段全清导致的零出手误判为零覆盖。
                if (seedSlot > 0 && !hitSlots.Contains(seedSlot)) break;
                HeroDefinition hero = db.FindHero(id);
                if (b.IsRecruited(id) || !b.CanAffordTransaction(hero.Cost)) continue;
                SlotDefinition best = level.Slots.Where(s => !b.Heroes.Any(h => h.SlotId == s.Id))
                    .OrderByDescending(s => level.Routes.Sum(r => Coverage(r, s.Position, BattleSession.GetMaximumRange(hero)))
                        / (1 + b.Heroes.Count(h => Point2.Distance(h.Position, s.Position) < 300))).ThenBy(s => s.Id).First();
                Require(b.TryRecruit(id, best.Id) == null, "正常自动招募失败");
            }
            if (b.Heroes.Count == roster.Length)
                foreach (HeroUnit h in b.Heroes.OrderBy(h => h.Level))
                    if (h.Level < 3 && b.CanAffordTransaction(b.GetUpgradeCost(h.SlotId))) Require(b.TryUpgrade(h.SlotId) == null, "正常升级失败");
            b.SetSpeed(speed); if (b.State == BattleState.Preparation) b.Start(); b.Tick(.1f);
            peak = Math.Max(peak, b.Enemies.Count);
            foreach (BattleEvent e in b.DrainEvents())
                if (e.Kind == "hit" && e.HeroId != null)
                { hitHeroes.Add(e.HeroId); hitSlots.Add(b.Heroes.Find(h => h.Definition.Id == e.HeroId).SlotId); }
        }
        Require(b.State == BattleState.Victory || b.State == BattleState.Defeat, "正常阵位策略未终局");
        Require(seedSlot <= 0 || hitSlots.Contains(seedSlot), "该阵位尚无正常资源实际出手证据：" + seedSlot);
        Require(speed != 1 || b.IsRaceEligible, "常速阵容未取得全Boss击退：" + seedSlot);
        return new { seedSlot = seedSlot, speed = speed, roster = roster, eligible = b.IsRaceEligible,
            state = b.State.ToString(), time = b.RaceElapsedSeconds, castle = b.CastleHealth,
            peak = peak, hitSlots = hitSlots.OrderBy(s => s).ToArray(), spent = b.GoldSpent, gold = b.Gold };
    }
    private static int Main(string[] args)
    {
        try
        {
            GameDatabase db = Json.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            LevelDefinition level = db.FindLevel("huangjin");
            var matrix = new List<object>();
            foreach (HeroDefinition h in db.Heroes.Where(h => !level.BossHeroIds.Contains(h.Id)))
                for (int tier = 1; tier <= 3; tier++) foreach (SlotDefinition slot in level.Slots) matrix.Add(Matrix(db, level, h, tier, slot));
            string[][] rosters = { level.RecommendedRoster.ToArray(),
                new[] { "lv_bu", "huang_zhong", "zhou_yu", "zhuge_liang", "zhao_yun", "guan_yu", "cao_cao", "xun_yu", "hua_tuo", "lu_su" },
                new[] { "zhao_yun", "guan_yu", "zhang_fei", "huang_zhong", "zhuge_liang", "ma_chao", "jiang_wei", "pang_tong", "zhou_yu", "lu_su" } };
            var normal = new List<object>();
            foreach (string[] roster in rosters) foreach (int speed in new[] { 1, 2, 3 }) normal.Add(Normal(db, level, 0, roster, speed));
            foreach (SlotDefinition slot in level.Slots) normal.Add(Normal(db, level, slot.Id, rosters[2], 1));
            File.WriteAllText(args[1], Json.Serialize(new { matrix = matrix, normal = normal,
                note = "机制矩阵不证明正常资源可达；整局策略单列，真人触控与快乐未验。" }));
            Console.WriteLine("阵位数据生成：" + matrix.Count + "个机制矩阵单元，" + normal.Count + "局正常资源策略"); return 0;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }
    private static void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
}
