using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

// 同一个驱动分别编译冻结版和当前Domain，逐行比较玩法轨迹，排除新增的纯反馈事件。
internal static class CombatTrace
{
    private static readonly string[][] Rosters = {
        new[] { "guan_yu", "huang_zhong", "zhao_yun", "zhou_yu", "lu_xun", "zhang_fei", "zhuge_liang", "ma_chao", "hua_tuo", "lu_su" },
        new[] { "huang_zhong", "lu_xun", "sun_shangxiang", "zhou_yu", "taishi_ci", "jiang_wei", "zhuge_liang", "guo_jia", "cao_cao", "diao_chan" },
        new[] { "guan_yu", "huang_zhong", "zhao_yun", "zhou_yu", "pang_tong", "zhang_fei", "zhuge_liang", "xun_yu", "hua_tuo", "lu_su" }
    };
    private static readonly int[][] Slots = {
        new[] { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 },
        new[] { 28, 10, 12, 7, 5, 6, 20, 4, 3, 2 },
        new[] { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 }
    };
    private static readonly HashSet<string> FeedbackEvents = new HashSet<string> { "link_created", "link_replaced", "link_hit" };

    private static int Main(string[] args)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        GameDatabase database = serializer.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
        Directory.CreateDirectory(args[1]);
        for (int roster = 0; roster < Rosters.Length; roster++)
            for (int speed = 1; speed <= 3; speed++) Run(database, roster, speed, args[1]);
        return 0;
    }

    private static void Run(GameDatabase database, int rosterIndex, int speed, string output)
    {
        string name = ((char)('A' + rosterIndex)).ToString() + "-" + speed + "x";
        LevelDefinition level = database.Levels.Find(item => item.Id == "huangjin");
        var battle = new BattleSession(database, level, Rosters[rosterIndex]);
        foreach (string id in Rosters[rosterIndex])
            if (!battle.IsInRoster(id)) throw new InvalidOperationException("非法阵容：" + id);
        battle.SetSpeed(speed);
        int spent = 0, rewards = 0, supply = 0;
        float firstRecruit = -1, firstUpgrade = -1;
        using (var writer = new StreamWriter(Path.Combine(output, name + ".trace"), false, new System.Text.UTF8Encoding(false)))
        using (var kills = new StreamWriter(Path.Combine(output, name + ".kills"), false, new System.Text.UTF8Encoding(false)))
        {
            for (int step = 0; step < 4800 && (battle.State == BattleState.Preparation || battle.State == BattleState.Running); step++)
            {
                for (int i = 0; i < Rosters[rosterIndex].Length; i++)
                {
                    string heroId = Rosters[rosterIndex][i];
                    int cost = database.FindHero(heroId).Cost;
                    if (battle.IsRecruited(heroId) || battle.Gold < cost) continue;
                    int before = battle.Gold;
                    string error = battle.TryRecruit(heroId, Slots[rosterIndex][i]);
                    if (error != null || battle.Gold != before - cost) throw new InvalidOperationException("招募扣费异常");
                    spent += cost;
                    if (firstRecruit < 0) firstRecruit = battle.Time;
                    writer.WriteLine("消费|{0:R}|招募|{1}|{2}|{3}", battle.Time, heroId, cost, battle.Gold);
                }
                if (battle.Heroes.Count == Rosters[rosterIndex].Length)
                    foreach (HeroUnit hero in battle.Heroes.OrderBy(item => item.Level))
                    {
                        int cost = battle.GetUpgradeCost(hero.SlotId);
                        if (hero.Level >= 3 || battle.Gold < cost) continue;
                        int before = battle.Gold;
                        string error = battle.TryUpgrade(hero.SlotId);
                        if (error != null || battle.Gold != before - cost) throw new InvalidOperationException("升阶扣费异常");
                        spent += cost;
                        if (firstUpgrade < 0) firstUpgrade = battle.Time;
                        writer.WriteLine("消费|{0:R}|升阶|{1}|{2}|{3}", battle.Time, hero.Definition.Id, cost, battle.Gold);
                    }
                if (battle.State == BattleState.Preparation) battle.Start();
                battle.Tick(.25f);
                foreach (BattleEvent item in battle.DrainEvents())
                {
                    if (FeedbackEvents.Contains(item.Kind)) continue;
                    writer.WriteLine("事件|{0}|{1}|{2}|{3:R}|{4:R},{5:R}|{6:R},{7:R}|{8}|{9}|{10:R}",
                        item.Kind, item.HeroId, item.TargetEnemyId, item.Amount, item.Position.X, item.Position.Y,
                        item.Target.X, item.Target.Y, item.Text, item.HeroLevel, item.SkillRange);
                    if (item.Kind == "kill")
                    {
                        rewards += (int)item.Amount;
                        FieldInfo occurredAt = typeof(BattleEvent).GetField("OccurredAt");
                        float time = occurredAt == null ? battle.Time : (float)occurredAt.GetValue(item);
                        kills.WriteLine("{0:R}|{1}", time, item.TargetEnemyId);
                    }
                    if (item.Kind == "notice" && item.Text != null && item.Text.StartsWith("鲁肃补给军饷", StringComparison.Ordinal)) supply += (int)item.Amount;
                }
                writer.WriteLine("状态|{0:R}|{1:R}|{2:R}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}",
                    battle.Time, battle.MarchTime, battle.ElapsedSeconds, battle.State, battle.CurrentWave,
                    battle.Gold, battle.CastleHealth, battle.GoldSpent, battle.DefeatedCount, battle.LeakedCount, battle.RemainingEnemies);
                foreach (EnemyUnit enemy in battle.Enemies)
                    writer.WriteLine("敌军|{0}|{1}|{2:R}|{3:R}|{4:R}|{5:R}|{6:R}|{7:R}|{8}",
                        enemy.Id, enemy.Definition.Id, enemy.Health, enemy.Progress, enemy.Position.X, enemy.Position.Y,
                        enemy.SlowUntil, enemy.StunUntil, enemy.Enraged);
                if (battle.GoldSpent != spent) throw new InvalidOperationException("累计消费不一致");
            }
            int income = battle.Gold - level.InitialGold - rewards - supply + spent;
            writer.WriteLine("总账|{0}+{1}+{2}+{3}-{4}={5}", level.InitialGold, income, rewards, supply, spent, battle.Gold);
        }
        Console.WriteLine("{0}：{1}，{2:F1}秒，城防{3}，击退{4}，漏兵{5}，首次招募{6:F2}，首次升阶{7:F2}",
            name, battle.State, battle.Time, battle.CastleHealth, battle.DefeatedCount, battle.LeakedCount, firstRecruit, firstUpgrade);
    }
}
