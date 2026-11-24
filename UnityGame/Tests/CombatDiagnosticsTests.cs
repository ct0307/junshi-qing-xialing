using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;
internal static class CombatDiagnosticsTests
{
    private static GameDatabase source;
    private static int passed, failed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(float a, float b) { Check(Math.Abs(a - b) < .02f, "数值不符 " + a + " / " + b); }
    private static void Run(string name, Action action) { try { action(); passed++; Console.WriteLine("通过 " + name); } catch (Exception e) { failed++; Console.WriteLine("失败 " + name + "：" + e); } }
    private static BattleSession Make(string id, bool enabled, int capacity = 16384)
    {
        GameDatabase db = new JavaScriptSerializer().Deserialize<GameDatabase>(new JavaScriptSerializer().Serialize(source));
        foreach (HeroDefinition h in db.Heroes) { h.Cost = 10; h.Damage = 100; h.Range = 400; h.BasicDamageScale = 1; h.SkillDamageScale = 1; h.BasicAttackInterval = 50; h.SkillCooldown = 50; }
        db.Enemies.Clear(); db.Enemies.Add(new EnemyDefinition { Id = "soldier", Name = "测试兵", Skill = "normal", MaxHealth = 10000, Speed = .01f, GoldReward = 2, CastleDamage = 1 });
        LevelDefinition level = new LevelDefinition { Id = "test", InitialGold = 10000, CastleMaxHealth = 100 };
        level.Routes.Add(new RouteDefinition { Id = "road", Points = new List<Point2> { new Point2(0, 0), new Point2(100000, 0) } });
        level.Slots.Add(new SlotDefinition { Id = 0, Position = new Point2(0, 0) });
        level.Waves.Add(new WaveDefinition { Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 4 } } });
        BattleSession b = new BattleSession(db, level, new[] { id });
        if (enabled) b.EnableCombatDiagnostics(capacity);
        Check(b.TryRecruit(id, 0) == null, "招募失败"); b.Heroes[0].NextBasicAt = b.Heroes[0].NextAttackAt = 100;
        b.Start(); b.Tick(.04f); b.DrainEvents(); return b;
    }
    private static IEnumerable<CombatDiagnosticEntry> Entries(BattleSession b) { for (int i = 0; i < b.Diagnostics.Count; i++) yield return b.Diagnostics[i]; }
    private static List<BattleEvent> Fire(BattleSession b) { b.Heroes[0].NextAttackAt = 0; b.Tick(.04f); if (b.Heroes[0].ChargeUntil > 0) b.Tick(1.1f); return b.DrainEvents(); }
    private static float Direct(BattleSession b, EnemyUnit enemy, float amount, int seq)
    { return (float)typeof(BattleSession).GetMethod("DirectDamage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(b, new object[] { b.Heroes[0], enemy, amount, new FactionCastContext { SequenceId = seq }, FactionDamageKind.SkillDirect }); }
    private static bool SameSnapshotValue(object left, object right)
    {
        if (left == null || right == null) return left == right;
        Dictionary<string, object> leftFields = left as Dictionary<string, object>, rightFields = right as Dictionary<string, object>;
        if (leftFields != null || rightFields != null)
            return leftFields != null && rightFields != null && leftFields.Count == rightFields.Count && leftFields.All(pair => rightFields.ContainsKey(pair.Key) && SameSnapshotValue(pair.Value, rightFields[pair.Key]));
        object[] leftItems = left as object[], rightItems = right as object[];
        if (leftItems != null || rightItems != null)
            return leftItems != null && rightItems != null && leftItems.Length == rightItems.Length && leftItems.Select((item, index) => SameSnapshotValue(item, rightItems[index])).All(equal => equal);
        return left.Equals(right);
    }
    private static bool SameSnapshot(JavaScriptSerializer serializer, string left, string right)
    { return SameSnapshotValue(serializer.DeserializeObject(left), serializer.DeserializeObject(right)); }
    private static int ComparePair(BattleSession off, BattleSession on, JavaScriptSerializer serializer)
    {
        List<BattleEvent> offEvents = off.DrainEvents(), onEvents = on.DrainEvents();
        Check(SameSnapshot(serializer, serializer.Serialize(offEvents), serializer.Serialize(onEvents)), "诊断开关改变旧事件");
        Near(off.Time, on.Time); Near(off.MarchTime, on.MarchTime); Near(off.Speed, on.Speed);
        Check(off.ElapsedSeconds == on.ElapsedSeconds && off.RaceElapsedSeconds == on.RaceElapsedSeconds && off.SpeedMask == on.SpeedMask, "领域时钟或竞速时钟变化");
        Check(off.State == on.State && off.Gold == on.Gold && off.GoldSpent == on.GoldSpent && off.CastleHealth == on.CastleHealth && off.CurrentWave == on.CurrentWave && off.RemainingEnemies == on.RemainingEnemies && off.DefeatedCount == on.DefeatedCount && off.LeakedCount == on.LeakedCount, "领域状态变化");
        string offHeroes = serializer.Serialize(off.Heroes), onHeroes = serializer.Serialize(on.Heroes), offEnemies = serializer.Serialize(off.Enemies), onEnemies = serializer.Serialize(on.Enemies);
        Check(SameSnapshot(serializer, offHeroes, onHeroes), "武将公开状态变化");
        Check(SameSnapshot(serializer, offEnemies, onEnemies), "敌军公开状态变化");
        for (int n = 0; n < on.Enemies.Count; n++)
        {
            Near(off.Enemies[n].SlowFactor, on.Enemies[n].SlowFactor); Near(off.Enemies[n].Travelled, on.Enemies[n].Travelled);
            Near(off.Enemies[n].EmberUntil, on.Enemies[n].EmberUntil); Near(off.Enemies[n].EmberDamage, on.Enemies[n].EmberDamage);
        }
        for (int n = 0; n < on.Heroes.Count; n++)
        { Near(off.Heroes[n].ChargeUntil, on.Heroes[n].ChargeUntil); Near(off.Heroes[n].NextAttackAt, on.Heroes[n].NextAttackAt); Near(off.Heroes[n].NextBasicAt, on.Heroes[n].NextBasicAt); }
        return onEvents.Count;
    }
    private static void CheckPairScenario(int speed, bool dynamicSpeed, bool pause)
    {
        foreach (string id in new[] { "lv_bu", "zhou_yu", "zhuge_liang" }) foreach (int heroLevel in new[] { 1, 2, 3 })
        {
            BattleSession off = Make(id, false), on = Make(id, true);
            foreach (BattleSession b in new[] { off, on })
            {
                b.Heroes[0].Level = heroLevel; b.Heroes[0].NextAttackAt = 0;
                b.Heroes[0].Definition.SkillCooldown = 1.5f; b.Heroes[0].Definition.BasicAttackInterval = .75f;
                b.Heroes[0].NextBasicAt = 0; b.SetSpeed(speed);
            }
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            int[] speedCycle = { 1, 2, 3, 1, 3, 2 }; float[] deltas = { .017f, .04f, .1f, .073f };
            for (int i = 0; i < 180; i++)
            {
                if (dynamicSpeed) { int targetSpeed = speedCycle[(i / 25) % speedCycle.Length]; off.SetSpeed(targetSpeed); on.SetSpeed(targetSpeed); }
                if (pause && (i == 45 || i == 120))
                {
                    off.SetPaused(true); on.SetPaused(true);
                    float beforeTime = on.Time, beforeMarch = on.MarchTime; double beforeElapsed = on.ElapsedSeconds, beforeRace = on.RaceElapsedSeconds;
                    int beforeCount = on.Diagnostics.Count, beforeGold = on.Gold;
                    string beforeEnemies = serializer.Serialize(on.Enemies), beforeHeroes = serializer.Serialize(on.Heroes);
                    foreach (float pausedDelta in new[] { .017f, 60f, 3600f })
                    {
                        off.Tick(pausedDelta); on.Tick(pausedDelta);
                        Check(ComparePair(off, on, serializer) == 0, "暂停输入不应产生旧事件");
                        Check(on.Time == beforeTime && on.MarchTime == beforeMarch && on.ElapsedSeconds == beforeElapsed && on.RaceElapsedSeconds == beforeRace && on.Gold == beforeGold && on.Diagnostics.Count == beforeCount, "暂停输入仍推进时钟、收入或诊断");
                        Check(SameSnapshot(serializer, serializer.Serialize(on.Enemies), beforeEnemies) && SameSnapshot(serializer, serializer.Serialize(on.Heroes), beforeHeroes), "暂停输入仍推进角色");
                    }
                    off.SetPaused(false); on.SetPaused(false);
                }
                float delta = deltas[i % deltas.Length]; off.Tick(delta); on.Tick(delta); ComparePair(off, on, serializer);
            }
            Check(Entries(on).Count(e => e.Kind == CombatDiagnosticKind.Cast) >= 2, "对照未覆盖重复施放");
            Check(!on.Diagnostics.Truncated, "对照诊断已截断");
        }
    }
    private static int Main(string[] args)
    {
        source = new JavaScriptSerializer().Deserialize<GameDatabase>(File.ReadAllText(args[0]));
        Run("默认关闭、启用只读有界且身份唯一", delegate {
            BattleSession off = Make("lv_bu", false), on = Make("lv_bu", true), other = Make("lv_bu", true);
            Check(off.Diagnostics == null, "默认必须关闭"); Check(on.Diagnostics.Capacity == 16384 && on.Diagnostics.Count == 0, "启用应预分配空缓冲");
            Check(on.Diagnostics.SessionId != other.Diagnostics.SessionId, "会话身份不应复用"); Check(on.Diagnostics.AllocatedBytes < 4 * 1024 * 1024, "超过4MiB");
            bool rejected = false; try { on.EnableCombatDiagnostics(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "战中不得重开丢弃记录");
        });
        Run("过杀真实健康差、致死同源、低raw仍诊断且旧返回值不变", delegate {
            BattleSession b = Make("lv_bu", true); EnemyUnit enemy = b.Enemies[0]; enemy.Health = 1;
            Near(100, Direct(b, enemy, 100, 42)); CombatDiagnosticEntry damage = Entries(b).Single(e => e.Kind == CombatDiagnosticKind.Damage); CombatDiagnosticEntry kill = Entries(b).Single(e => e.Kind == CombatDiagnosticKind.Kill);
            Near(1, damage.ActualDamage); Near(1, damage.HealthBefore); Near(0, damage.HealthAfter); Check(kill.AttackSequenceId == 42 && kill.HeroId == "lv_bu" && kill.TargetEnemyId == enemy.Id, "致死来源断裂");
            b.DrainEvents(); Direct(b, b.Enemies[0], .1f, 43); Check(Entries(b).Any(e => e.AttackSequenceId == 43 && e.ActualDamage > 0), "低raw丢失"); Check(!b.DrainEvents().Any(e => e.Kind == "hit"), "不应改变旧hit阈值");
        });
        Run("三将直接伤害序列及真实施放记录", delegate {
            foreach (string id in new[] { "lv_bu", "zhou_yu", "zhuge_liang" }) { BattleSession b = Make(id, true); List<BattleEvent> events = Fire(b); BattleEvent cast = events.Single(e => e.Kind == "skill_cast"); Check(Entries(b).Count(e => e.Kind == CombatDiagnosticKind.Cast) == 1, "施放次数失真"); Check(Entries(b).Any(e => e.Kind == CombatDiagnosticKind.Damage && e.HeroId == id && e.AttackSequenceId == cast.AttackSequenceId && e.DamageKind == CombatDamageKind.SkillDirect), "直接伤害序列缺失"); }
        });
        Run("火区同强最小effect标签、旧区原序列与余烬不继承新施放", delegate {
            BattleSession b = Make("zhou_yu", true); b.Heroes[0].Level = 3; Fire(b); int oldSequence = Entries(b).Single(e => e.Kind == CombatDiagnosticKind.Cast).AttackSequenceId;
            b.Tick(.04f); int effect = Entries(b).First(e => e.DamageKind == CombatDamageKind.FireArea).EffectId;
            Fire(b); int nextSequence = Entries(b).Where(e => e.Kind == CombatDiagnosticKind.Cast).Last().AttackSequenceId; b.Tick(.1f);
            CombatDiagnosticEntry area = Entries(b).Last(e => e.Kind == CombatDiagnosticKind.Damage && e.DamageKind == CombatDamageKind.FireArea); Check(area.AttackSequenceId == oldSequence && area.EffectId == effect && nextSequence != oldSequence, "同强或旧火来源错误");
            foreach (EnemyUnit e in b.Enemies) { e.Travelled = 1000; e.Position = new Point2(1000, 0); } b.Tick(.1f);
            CombatDiagnosticEntry ember = Entries(b).Last(e => e.DamageKind == CombatDamageKind.Ember); Check(ember.AttackSequenceId == oldSequence && ember.EffectId == effect, "余烬丢失原火区");
        });
        Run("诸葛直接减速与风眼冻结来源及真实状态", delegate {
            BattleSession b = Make("zhuge_liang", true); b.Heroes[0].Level = 3; Fire(b); int sequence = Entries(b).Single(e => e.Kind == CombatDiagnosticKind.Cast).AttackSequenceId;
            CombatDiagnosticEntry slow = Entries(b).First(e => e.DamageKind == CombatDamageKind.SlowDirect); Near(.55f, slow.SlowFactor); Check(slow.ExpiresAt > slow.Time && slow.AttackSequenceId == sequence, "直接减速不可信");
            b.Tick(.1f); CombatDiagnosticEntry wind = Entries(b).Last(e => e.DamageKind == CombatDamageKind.SlowArea); Check(wind.AttackSequenceId == sequence && wind.EffectId > 0 && wind.TargetEnemyId > 0, "风眼来源不可信"); Near(b.Enemies[0].SlowFactor, wind.SlowFactor);
        });
        Run("旧火区到期后同强新火刷新余烬归于新施放", delegate {
            BattleSession b = Make("zhou_yu", true); b.Heroes[0].Level = 3; Fire(b); b.Tick(1); Fire(b);
            int sequence = Entries(b).Where(e => e.Kind == CombatDiagnosticKind.Cast).Last().AttackSequenceId;
            b.Tick(1.6f); foreach (EnemyUnit e in b.Enemies) { e.Travelled = 1000; e.Position = new Point2(1000, 0); } b.Tick(.1f);
            Check(Entries(b).Last(e => e.DamageKind == CombatDamageKind.Ember).AttackSequenceId == sequence, "已到期旧区永久占用同强余烬标签");
        });
        Run("连环传伤不伪造主将直接归因、其他来源未归因", delegate {
            BattleSession b = Make("lv_bu", true); EnemyUnit target = b.Enemies[0], other = b.Enemies[1]; LinkGroup link = new LinkGroup { Id = 99, Owner = b.Heroes[0], Until = b.Time + 10 }; link.Members.Add(target.Id); link.Members.Add(other.Id); target.Links = other.Links = link;
            Direct(b, target, 100, 55); CombatDiagnosticEntry transfer = Entries(b).Single(e => e.Kind == CombatDiagnosticKind.Damage && e.TargetEnemyId == other.Id); Check(transfer.HeroId == null && transfer.AttackSequenceId == 0 && transfer.DamageKind == CombatDamageKind.LinkTransfer && transfer.EffectId == 99 && transfer.TriggerHeroId == "lv_bu" && transfer.SourceEnemyId == target.Id, "传伤伪造归因");
            BattleSession untracked = Make("guan_yu", true); Direct(untracked, untracked.Enemies[0], 10, 77); Check(Entries(untracked).Single().DamageKind == CombatDamageKind.Unattributed && Entries(untracked).Single().HeroId == null, "首批外来源不应归因");
        });
        Run("超限停止明细明确截断计数且终局导出", delegate {
            BattleSession b = Make("lv_bu", true, 2); for (int i = 0; i < 5; i++) Direct(b, b.Enemies[0], 1, i + 1);
            Check(b.Diagnostics.Count == 2 && b.Diagnostics.Truncated && b.Diagnostics.DroppedCount == 3, "截断未显式计数");
            bool rejected = false; try { b.ExportCombatDiagnostics(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "战中不得导出");
            b.Enemies.Clear(); b.Tick(.1f); Check(b.State == BattleState.Victory && b.ExportCombatDiagnostics().Length == 2, "终局导出失败");
        });
        Run("最大缓冲结构内存、容量边界和索引保护", delegate {
            Check(Marshal.SizeOf(typeof(CombatDiagnosticEntry)) <= 128, "结构超过保守预算");
            foreach (int capacity in new[] { 0, 16385 }) { bool rejected = false; try { Make("lv_bu", true, capacity); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected, "越界容量未拒绝"); }
            BattleSession b = Make("lv_bu", true); b.Enemies[0].Health = 1000000;
            for (int i = 0; i < 16400; i++) Direct(b, b.Enemies[0], 1, i + 1);
            Check(b.Diagnostics.Count == 16384 && b.Diagnostics.DroppedCount == 16 && b.Diagnostics.Truncated, "真实最大容量截断错误");
            Check(256L + (long)Marshal.SizeOf(typeof(CombatDiagnosticEntry)) * b.Diagnostics.Capacity * 2 < 4 * 1024 * 1024, "缓冲与终局复制总量超过4MiB");
            bool invalidIndex = false; try { CombatDiagnosticEntry unused = b.Diagnostics[b.Diagnostics.Count]; } catch (ArgumentOutOfRangeException) { invalidIndex = true; } Check(invalidIndex, "索引可读取未写内容");
            Console.WriteLine("结构 " + Marshal.SizeOf(typeof(CombatDiagnosticEntry)) + " 字节；缓冲保守预算 " + b.Diagnostics.AllocatedBytes + " 字节");
        });
        Run("关卡规则解析与实例一致且基线兼容", delegate {
            LevelDefinition level = new LevelDefinition(); Check(BattleSession.ResolveRuleVersion(level, true) == "faction-army-v1", "军势基线变化"); Check(BattleSession.ResolveRuleVersion(level, false) == "growth-tempo-v2", "普通基线变化");
            level.CombatRuleVersion = "sensation-mn"; Check(BattleSession.ResolveRuleVersion(level, true) == "sensation-mn-army" && BattleSession.ResolveRuleVersion(level, false) == "sensation-mn-solo", "实验规则未隔离");
        });
        Run("诊断开关逐步状态和旧事件完全相同", delegate {
            foreach (string id in new[] { "lv_bu", "zhou_yu", "zhuge_liang" }) { BattleSession off = Make(id, false), on = Make(id, true); off.Heroes[0].Level = on.Heroes[0].Level = 3; off.Heroes[0].NextAttackAt = on.Heroes[0].NextAttackAt = 0;
                JavaScriptSerializer serializer = new JavaScriptSerializer(); for (int i = 0; i < 150; i++) { off.Tick(.04f); on.Tick(.04f); Check(serializer.Serialize(off.DrainEvents()) == serializer.Serialize(on.DrainEvents()), "旧事件变化"); Near(off.Time, on.Time); Check(off.Gold == on.Gold && off.State == on.State && off.Enemies.Count == on.Enemies.Count, "领域状态变化"); for (int n = 0; n < on.Enemies.Count; n++) { Near(off.Enemies[n].Health, on.Enemies[n].Health); Near(off.Enemies[n].SlowUntil, on.Enemies[n].SlowUntil); Near(off.Enemies[n].Travelled, on.Enemies[n].Travelled); } }
            }
        });
        Run("诊断开关2倍三将1至3阶变步长逐步等价", delegate { CheckPairScenario(2, false, false); });
        Run("诊断开关3倍三将1至3阶变步长逐步等价", delegate { CheckPairScenario(3, false, false); });
        Run("诊断开关六方向动态倍率直选逐步等价", delegate { CheckPairScenario(1, true, false); });
        Run("诊断开关动态倍率暂停长输入后恢复逐步等价", delegate { CheckPairScenario(1, true, true); });
        Console.WriteLine("诊断专项：" + passed + " 通过，" + failed + " 失败"); return failed == 0 ? 0 : 1;
    }
}
