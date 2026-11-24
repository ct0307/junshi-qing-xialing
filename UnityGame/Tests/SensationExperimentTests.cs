using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

// 正常开局推进到公共节点，再执行局部实验；不补钱、不改敌人血量、不伪造胜利。
internal static class SensationExperimentTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16777216 };
    private static object Run(GameDatabase db, string variant, int speed, int windSlot)
    {
        LevelDefinition level = db.FindLevel("huangjin");
        bool cooperation = windSlot > 0;
        string[] roster = cooperation ? new[] { "zhou_yu", "huang_zhong", "zhuge_liang" } : new[] { "lv_bu", "huang_zhong" };
        var b = new BattleSession(db, level, roster, true);
        b.EnableCombatDiagnostics();
        Require(b.RuleVersion.StartsWith("sensation-experiment-" + variant.ToLowerInvariant() + "-", StringComparison.Ordinal), "候选规则未隔离：" + b.RuleVersion);
        Require(b.TryRecruit(roster[0], cooperation ? 5 : 4) == null, "正常开局主将招募失败");
        int archerSlot = variant.Contains("M") ? 25 : 27;
        Require(b.TryRecruit("huang_zhong", archerSlot) == null, "正常开局黄忠招募失败");
        int reward = 0, supply = 0, waveReward = 0, peak = 0, deaths = 0, leaks = 0;
        float start = -1, end = -1, nodeAt = -1;
        int nodeGold = -1, startGold = 0;
        bool branched = false, entered = false, completed = false;
        var hits = new Dictionary<int, HashSet<int>>();
        var waveStarts = new List<object>();
        var rewardTimes = new List<object>();
        b.Start();
        for (int step = 0; step < 24000 && b.State == BattleState.Running; step++)
        {
            if (!branched && b.CurrentWave == 3 && !b.IsWaveActive)
            {
                branched = true; nodeAt = b.Time; nodeGold = b.Gold;
                if (cooperation)
                {
                    Require(b.TryRecruit("zhuge_liang", windSlot) == null, "公共节点诸葛招募不可达");
                    Require(b.TryUpgrade(5) == null && b.TryUpgrade(windSlot) == null, "公共节点双二阶经济不可达");
                }
                b.SetSpeed(speed);
            }
            b.Tick(1f / 30);
            if (b.CurrentWave == 4 && !entered)
            { entered = true; start = b.Time; startGold = b.Gold; }
            foreach (BattleEvent e in b.DrainEvents())
            {
                if (e.Kind == "kill") reward += (int)e.Amount;
                if (e.Kind == "notice" && e.Text.StartsWith("鲁肃补给军饷", StringComparison.Ordinal)) supply += (int)e.Amount;
                if (e.Kind == "wave") waveStarts.Add(new { wave = e.Amount, at = e.OccurredAt, gold = b.Gold });
                if (!entered || completed) continue;
                if (e.Kind == "kill") { waveReward += (int)e.Amount; deaths++; rewardTimes.Add(new { at = e.OccurredAt, amount = e.Amount, target = e.TargetEnemyId }); }
                if (e.Kind == "leak") leaks++;
                if (e.Kind == "skill_cast" && e.HeroId == roster[0]) hits[e.AttackSequenceId] = new HashSet<int>();
                HashSet<int> targets;
                if (e.Kind == "attack" && e.HeroId == roster[0] && hits.TryGetValue(e.AttackSequenceId, out targets)) targets.Add(e.TargetEnemyId);
            }
            // 展示用MarchTime为float，边界处会与真实double累计不同；只读采集真实累计值。
            double march = (double)typeof(BattleSession).GetField("marchClock", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(b);
            int income = (int)Math.Floor(march * level.IncomePerSecond + .00001);
            Require(level.InitialGold + income + reward + supply - b.GoldSpent == b.Gold, "局部试验账本不守恒 " + variant + " step=" + step + " march=" + b.MarchTime.ToString("R") + " income=" + income + " rewards=" + reward + " spent=" + b.GoldSpent + " actual=" + b.Gold);
            if (entered) peak = Math.Max(peak, b.Enemies.Count);
            if (entered && b.CurrentWave == 4 && !b.IsWaveActive)
            { completed = true; end = b.Time; break; }
        }
        Require(branched, "第3波公共节点未到达：" + variant);
        Require(completed, "第4波未清空：" + variant);
        Require(!b.Diagnostics.Truncated, "诊断明细截断，不能用于实验验收");
        var slowUntil = new Dictionary<int, float>();
        float periodic = 0, periodicWhileSlowed = 0;
        var damageByCast = new Dictionary<int, float>();
        for (int i = 0; i < b.Diagnostics.Count; i++)
        {
            CombatDiagnosticEntry entry = b.Diagnostics[i];
            if (entry.Time < start || entry.Time > end) continue;
            if (entry.Kind == CombatDiagnosticKind.Slow && entry.HeroId == "zhuge_liang") slowUntil[entry.TargetEnemyId] = entry.ExpiresAt;
            if (entry.Kind != CombatDiagnosticKind.Damage) continue;
            if (entry.HeroId == roster[0])
            {
                float previous; damageByCast.TryGetValue(entry.AttackSequenceId, out previous);
                damageByCast[entry.AttackSequenceId] = previous + entry.ActualDamage;
            }
            if (entry.DamageKind == CombatDamageKind.FireArea || entry.DamageKind == CombatDamageKind.Ember)
            {
                periodic += entry.ActualDamage;
                float until; if (slowUntil.TryGetValue(entry.TargetEnemyId, out until) && until > entry.Time) periodicWhileSlowed += entry.ActualDamage;
            }
        }
        return new { variant = variant, rule = b.RuleVersion, speed = speed, windSlot = windSlot, archerSlot = archerSlot,
            commonNodeAt = nodeAt, commonNodeGold = nodeGold, startAt = start, clearedAt = end,
            duration = end - start, startGold = startGold, endGold = b.Gold, spent = b.GoldSpent,
            rewards = waveReward, totalRewards = reward, march = b.MarchTime,
            kills = deaths, leaks = leaks, peakEnemies = peak,
            casts = hits.OrderBy(h => h.Key).Select(h => new { sequence = h.Key, uniqueTargets = h.Value.Count,
                actualDamage = damageByCast.ContainsKey(h.Key) ? damageByCast[h.Key] : 0 }).ToArray(),
            periodicDamage = periodic, periodicDamageWhileSlowed = periodicWhileSlowed,
            diagnostics = new { session = b.Diagnostics.SessionId.ToString(), count = b.Diagnostics.Count, truncated = b.Diagnostics.Truncated, allocatedBytes = b.Diagnostics.AllocatedBytes },
            rewardTimeline = rewardTimes, waveStarts = waveStarts };
    }

    private static int Main(string[] args)
    {
        try
        {
            var results = new List<object>();
            foreach (string variant in new[] { "B", "T", "N", "TN", "M", "MT", "MN", "MTN" })
            {
                GameDatabase db = Json.Deserialize<GameDatabase>(File.ReadAllText(Path.Combine(args[0], variant + ".json")));
                foreach (int speed in new[] { 1, 2, 3 }) results.Add(Run(db, variant, speed, 0));
                if (variant == "B" || variant == "MT" || variant == "MN")
                    foreach (int slot in new[] { 4, 6 }) results.Add(Run(db, variant, 1, slot));
            }
            File.WriteAllText(args[1], Json.Serialize(new { status = "Passed", results = results, humanFunChecked = false }));
            Console.WriteLine("局部实验通过：" + results.Count + "局，正常公共节点、独立候选、逐步账本守恒；非真人爽感验收");
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }
    private static void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
}
