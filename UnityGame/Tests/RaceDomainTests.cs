using System;
using System.Collections;
using System.Reflection;
using YanyiSandpan.Domain;

internal static class RaceDomainTests
{
    private static int passed, failed;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) { Check(object.Equals(expected, actual), "预期 " + expected + "，实际 " + actual); }
    private static void Near(double expected, double actual) { Check(Math.Abs(expected - actual) < .000001, "时间不相等：" + expected + " / " + actual); }
    private static T Read<T>(object item, string name)
    {
        PropertyInfo p = item.GetType().GetProperty(name); FieldInfo f = item.GetType().GetField(name);
        Check(p != null || f != null, "缺少 API：" + name); return (T)(p != null ? p.GetValue(item, null) : f.GetValue(item));
    }
    private static object Snapshot(BattleSession b)
    {
        MethodInfo method = typeof(BattleSession).GetMethod("GetRaceSnapshot"); Check(method != null, "缺少 GetRaceSnapshot API"); return method.Invoke(b, null);
    }
    private static double Seconds(object s, int speed) { return (double)s.GetType().GetMethod("GetSpeedSeconds").Invoke(s, new object[] { speed }); }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("通过：" + name); } catch (Exception e) { failed++; Console.WriteLine("失败：" + name + "：" + e.Message); } }
    private static GameDatabase Fixture(int bosses, bool soldier)
    {
        GameDatabase db = new GameDatabase();
        db.Heroes.Add(new HeroDefinition { Id = "hero", Name = "测试将", Skill = "focus", Cost = 10, Damage = 100, Range = 2000, AttackInterval = 1 });
        db.Enemies.Add(new EnemyDefinition { Id = "boss", CharacterId = "enemy_general", Name = "敌将", IsBoss = true, MaxHealth = 100, Speed = 0, CastleDamage = 15, GoldReward = 5 });
        db.Enemies.Add(new EnemyDefinition { Id = "soldier", Name = "步兵", MaxHealth = 100, Speed = 100000, CastleDamage = 1 });
        LevelDefinition level = new LevelDefinition { Id = "test", InitialGold = 1000, CastleMaxHealth = 100, IncomePerSecond = 1 };
        level.Routes.Add(new RouteDefinition { Id = "road" }); level.Routes[0].Points.Add(new Point2(0, 0)); level.Routes[0].Points.Add(new Point2(1000, 0));
        level.Slots.Add(new SlotDefinition { Id = 0, Position = new Point2(0, 0) }); level.Slots.Add(new SlotDefinition { Id = 1, Position = new Point2(0, 100) });
        WaveDefinition wave = new WaveDefinition();
        if (bosses > 0) wave.Groups.Add(new SpawnGroup { EnemyId = "boss", RouteId = "road", Count = bosses });
        if (soldier) wave.Groups.Add(new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 1 });
        level.Waves.Add(wave); db.Levels.Add(level); return db;
    }
    private static BattleSession Battle(GameDatabase db) { return new BattleSession(db, db.Levels[0], new[] { "hero" }, true); }
    private static void BurnAll(BattleSession b)
    {
        foreach (EnemyUnit enemy in b.Enemies) { enemy.BurnUntil = b.Time + 1; enemy.BurnDamage = 100000; }
        b.Tick(1f / 30);
    }
    private static double Script(float frame)
    {
        GameDatabase db = Fixture(1, false); db.Enemies[0].Speed = 30; db.Heroes[0].Damage = 10;
        BattleSession b = Battle(db); b.TryRecruit("hero", 0); b.Start();
        // 固定现实时间点切换，拆分跨边界输入，避免把帧取样误差当成计时误差。
        double elapsed = 0; double[] boundaries = { .5, 1.25, 2, 200 }; int[] speeds = { 1, 2, 3, 1 };
        for (int stage = 0; stage < boundaries.Length && b.State == BattleState.Running; stage++) {
            b.SetSpeed(speeds[stage]);
            while (elapsed + .0000001 < boundaries[stage] && b.State == BattleState.Running) { float delta = (float)Math.Min(frame, boundaries[stage] - elapsed); b.Tick(delta); elapsed += delta; }
        }
        Equal(BattleState.Victory, b.State); Check(Read<bool>(Snapshot(b), "IsEligible"), "脚本须合格"); return Read<double>(Snapshot(b), "ElapsedSeconds");
    }
    private static int Main()
    {
        Run("六向目标切换保留旧片并用目标产生新片", delegate {
            for (int from = 1; from <= 3; from++) for (int target = 1; target <= 3; target++) {
                if (from == target) continue;
                GameDatabase db = Fixture(1, false); db.Levels[0].IncomePerSecond = 300;
                BattleSession b = Battle(db); b.SetSpeed(from); b.Start(); b.Tick(.01f); b.SetSpeed(target); b.Tick(.03f);
                object snapshot = Snapshot(b); double oldSeconds = .01, newSeconds = 1.0 / 30 - oldSeconds;
                Near(oldSeconds, Seconds(snapshot, from)); Near(newSeconds, Seconds(snapshot, target));
                for (int unused = 1; unused <= 3; unused++) if (unused != from && unused != target) Near(0, Seconds(snapshot, unused));
                Check(Math.Abs(b.MarchTime - (oldSeconds * from + newSeconds * target)) < .000001, "历史进军时间被改写");
                Equal(1000 + (int)Math.Floor((oldSeconds * from + newSeconds * target) * 300 + .00001), b.Gold);
                Equal(2, Read<IList>(snapshot, "SpeedChanges").Count);
            }
        });
        Run("预期来自出兵实例且额外禁选声明不计数", delegate { GameDatabase db = Fixture(3, false); db.Levels[0].BossHeroIds.Add("extra"); BattleSession b = Battle(db); Equal(3, Read<int>(b, "ExpectedBossCount")); b.Start(); b.Tick(.04f); Equal(3, Read<int>(b, "SpawnedBossCount")); BurnAll(b); Equal(3, Read<int>(b, "DefeatedBossCount")); Check(Read<bool>(b, "IsRaceEligible"), "同身份三实例须合格"); });
        Run("普通漏兵不阻止全Boss击退资格", delegate { BattleSession b = Battle(Fixture(1, true)); b.Start(); b.Tick(.04f); BurnAll(b); Equal(1, b.LeakedCount); Equal(99, b.CastleHealth); Check(Read<bool>(b, "IsRaceEligible"), "普通漏兵应合格"); });
        Run("Boss突破胜利保留普通胜利但无竞速资格", delegate { GameDatabase db = Fixture(1, false); db.Enemies[0].Speed = 100000; BattleSession b = Battle(db); b.Start(); b.Tick(.04f); Equal(BattleState.Victory, b.State); Equal(85, b.CastleHealth); Equal(1, b.BossBreachCount); Check(!Read<bool>(Snapshot(b), "IsEligible"), "突破不得合格"); Check(Read<string>(Snapshot(b), "IneligibilityReason").Length > 0, "必须给出原因"); });
        Run("未终局和未生成完不合格", delegate { GameDatabase db = Fixture(2, false); db.Levels[0].Waves[0].Groups[0].Interval = 10; BattleSession b = Battle(db); Check(!Read<bool>(b, "IsRaceEligible"), "准备期不得合格"); b.Start(); b.Tick(.04f); BurnAll(b); Equal(1, Read<int>(b, "SpawnedBossCount")); Check(!Read<bool>(b, "IsRaceEligible"), "未生成完整不得合格"); });
        Run("事件消费与重复死亡不影响实例计数", delegate { BattleSession b = Battle(Fixture(1, false)); b.Start(); b.Tick(.04f); EnemyUnit enemy = b.Enemies[0]; b.DrainEvents(); BurnAll(b); b.DrainEvents(); typeof(BattleSession).GetMethod("Kill", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b, new object[] { enemy }); Equal(1, Read<int>(b, "DefeatedBossCount")); Check(Read<bool>(b, "IsRaceEligible"), "事件无关"); });
        Run("直接移除敌军不得伪造真实击退", delegate { BattleSession b = Battle(Fixture(1, false)); b.Start(); b.Tick(.04f); b.Enemies.Clear(); b.Tick(.04f); Equal(BattleState.Victory, b.State); Check(!Read<bool>(b, "IsRaceEligible"), "必须真实伤害死亡"); });
        Run("终局大输入尾段不计竞速且旧实秒接口保留", delegate { GameDatabase db = Fixture(0, true); BattleSession b = Battle(db); b.Start(); b.Tick(20); Near(20, b.ElapsedSeconds); Near(1.0 / 30, Read<double>(b, "RaceElapsedSeconds")); Near(1.0 / 30, Read<double>(Snapshot(b), "ElapsedSeconds")); Equal(1, b.SpeedMask); });
        Run("30／60／120帧与长帧脚本终点一致", delegate { double first = Script(1f / 30); foreach (float frame in new[] { 1f / 60, 1f / 120, .37f, 2f }) Check(Math.Abs(first - Script(frame)) <= 1.0 / 30 + .000001, "终点超过一个规则步"); });
        Run("未处理帧与无效delta不写倍率摘要", delegate { BattleSession b = Battle(Fixture(1, false)); b.Start(); b.SetSpeed(3); b.Tick(0); b.Tick(-1); b.Tick(float.NaN); b.Tick(float.PositiveInfinity); b.Tick(.005f); object s = Snapshot(b); Near(0, Read<double>(s, "ElapsedSeconds")); Equal(0, Read<int>(s, "SpeedMask")); Equal(0, Read<IList>(s, "SpeedChanges").Count); });
        Run("消费片段决定倍率时长与切换时刻", delegate { BattleSession b = Battle(Fixture(1, false)); b.Start(); b.Tick(.01f); b.SetSpeed(3); b.Tick(.03f); object s = Snapshot(b); Near(1.0 / 30, Read<double>(s, "ElapsedSeconds")); Near(.01, Seconds(s, 1)); Near(1.0 / 30 - .01, Seconds(s, 3)); Equal(5, Read<int>(s, "SpeedMask")); IList changes = Read<IList>(s, "SpeedChanges"); Equal(2, changes.Count); Near(.01, Read<double>(changes[1], "ElapsedSeconds")); });
        Run("暂停后台冻结且暂停空切不记录", delegate { BattleSession b = Battle(Fixture(1, false)); b.Start(); b.Tick(.1f); object before = Snapshot(b); int gold = b.Gold; b.SetPaused(true); b.SetSpeed(2); b.Tick(100); b.SetSpeed(3); b.SetSpeed(1); b.SetPaused(false); b.Tick(.1f); object after = Snapshot(b); Near(.2, Read<double>(after, "ElapsedSeconds")); Equal(gold, b.Gold); Equal(1, Read<IList>(after, "SpeedChanges").Count); Near(.1, Read<double>(before, "ElapsedSeconds")); });
        Run("快照保持实际版本关卡与不可变摘要", delegate { BattleSession b = Battle(Fixture(1, false)); b.TryRecruit("hero", 0); object first = Snapshot(b); Equal("test", Read<string>(first, "LevelId")); Equal(b.RuleVersion, Read<string>(first, "RuleVersion")); Equal("all-boss-v1", Read<string>(first, "QualificationVersion")); IList actions = Read<IList>(first, "Actions"); Equal(1, actions.Count); Check(actions.IsReadOnly, "操作须只读"); bool rejected = false; try { actions.Clear(); } catch (NotSupportedException) { rejected = true; } Check(rejected, "不能修改摘要"); foreach (PropertyInfo p in first.GetType().GetProperties()) Check(p.GetSetMethod() == null, "快照无公开setter"); b.TryUpgrade(0); Equal(1, actions.Count); Equal(2, Read<IList>(Snapshot(b), "Actions").Count); });
        Run("真实招募调位升阶记录准备期与事务后军饷", delegate { BattleSession b = Battle(Fixture(1, false)); b.TryRecruit("hero", 0); b.TryRecruit("hero", 1); b.TryMoveHero(0, 1); b.TryUpgrade(1); IList actions = Read<IList>(Snapshot(b), "Actions"); Equal(3, actions.Count); object recruit = actions[0]; Equal("recruit", Read<string>(recruit, "Kind")); Equal("hero", Read<string>(recruit, "HeroId")); Equal(0, Read<int>(recruit, "SlotId")); Equal(1, Read<int>(recruit, "Level")); Equal(990, Read<int>(recruit, "Gold")); Near(0, Read<double>(recruit, "ElapsedSeconds")); Equal("upgrade", Read<string>(actions[2], "Kind")); Equal(1, Read<int>(actions[2], "SlotId")); Equal(2, Read<int>(actions[2], "Level")); Equal(b.Gold, Read<int>(actions[2], "Gold")); });
        Run("摘要限量但各档时长完整", delegate { GameDatabase db = Fixture(1, false); db.Heroes[0].Damage = 0; BattleSession b = Battle(db); b.TryRecruit("hero", 0); for (int i = 0; i < 100; i++) b.TryMoveHero(i % 2, 1 - i % 2); b.Start(); for (int i = 0; i < 1000; i++) { b.SetSpeed(i % 3 + 1); b.Tick(1f / 30); } object s = Snapshot(b); Equal(BattleState.Running, b.State); Equal(256, Read<IList>(s, "SpeedChanges").Count); Equal(64, Read<IList>(s, "Actions").Count); Check(Read<bool>(s, "SummaryTruncated"), "须显式截断"); Near(1000.0 / 30, Read<double>(s, "ElapsedSeconds")); Near(Read<double>(s, "ElapsedSeconds"), Seconds(s, 1) + Seconds(s, 2) + Seconds(s, 3)); });
        Run("首次和末次突破独立复制且携带当时事实", delegate { GameDatabase db = Fixture(0, true); db.Levels[0].Waves[0].Groups[0].Count = 2; db.Levels[0].Waves[0].Groups[0].Interval = 1; BattleSession b = Battle(db); b.SetSpeed(2); b.Start(); b.Tick(.04f); object first = Read<object>(b, "FirstBreach"); Check(first != null, "缺少首次突破"); Check(!object.ReferenceEquals(first, b.LastBreach), "首次末次不能共用对象"); Equal(1, Read<int>(first, "Wave")); Equal(1000, Read<int>(first, "Gold")); Equal(2f, Read<float>(first, "Speed")); Near(1.0 / 30, Read<double>(first, "ElapsedSeconds")); b.LastBreach.EnemyName = "外部改动"; Equal("步兵", Read<string>(first, "EnemyName")); b.Tick(1); Check(Read<double>(b.LastBreach, "ElapsedSeconds") > Read<double>(first, "ElapsedSeconds"), "末次应后发生"); Equal("步兵", Read<string>(Read<object>(b, "FirstBreach"), "EnemyName")); });
        Console.WriteLine("竞速专项：" + passed + " 通过，" + failed + " 失败"); return failed == 0 ? 0 : 1;
    }
}
