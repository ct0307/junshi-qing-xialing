using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

internal static class RaceRecordTests
{
    private static int passed, failed;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) { Check(object.Equals(expected, actual), "预期 " + expected + "，实际 " + actual); }
    private static T Value<T>(object target, string name) { Check(target != null, "缺少对象：" + name); PropertyInfo p = target.GetType().GetProperty(name); Check(p != null, "缺少属性：" + name); return (T)p.GetValue(target, null); }
    private static object Call(object target, string name, params object[] args) { MethodInfo m = target.GetType().GetMethod(name); Check(m != null, "缺少 API：" + name); return m.Invoke(target, args); }
    private static object Record(GameProgress progress, RaceSnapshot snapshot) { return Call(progress, "RecordRaceVictory", snapshot); }
    private static object Read(GameProgress progress, string level, string rule) { return Call(progress, "GetRaceRecord", level, rule); }
    private static long Ms(object record) { return Value<long>(record, "ElapsedMilliseconds"); }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("通过：" + name); } catch (Exception e) { failed++; Console.WriteLine("失败：" + name + "：" + (e.InnerException ?? e).Message); } }
    private sealed class Storage
    {
        internal readonly Dictionary<string, int> Values;
        internal int Writes, Saves, Reads, FailAfter = -1, FailReadAfter = -1;
        internal bool FailSave;
        internal Storage() { Values = new Dictionary<string, int>(); }
        internal Storage(Dictionary<string, int> values) { Values = new Dictionary<string, int>(values); }
        internal GameProgress Open() { return new GameProgress((k, f) => { if (FailReadAfter == 0) { FailReadAfter = -1; throw new InvalidOperationException("模拟瞬时读取失败"); } if (FailReadAfter > 0) FailReadAfter--; Reads++; int v; return Values.TryGetValue(k, out v) ? v : f; }, (k, v) => { if (FailAfter == 0) throw new InvalidOperationException("模拟中断"); if (FailAfter > 0) FailAfter--; Values[k] = v; Writes++; }, () => { if (FailSave) throw new InvalidOperationException("模拟保存失败"); Saves++; }); }
    }
    private static GameDatabase Fixture(string id, bool breach, bool failure)
    {
        var db = new GameDatabase();
        db.Heroes.Add(new HeroDefinition { Id = "hero.汉将", Name = "测试将", Cost = 10, Damage = 0, Range = 2000, AttackInterval = 1 });
        db.Enemies.Add(new EnemyDefinition { Id = "boss", IsBoss = true, MaxHealth = 100, Speed = breach || failure ? 100000 : 0, CastleDamage = failure ? 100 : 15 });
        var level = new LevelDefinition { Id = id, InitialGold = 1000, CastleMaxHealth = 100 };
        level.Routes.Add(new RouteDefinition { Id = "road", Points = new List<Point2> { new Point2(0, 0), new Point2(1000, 0) } });
        level.Slots.Add(new SlotDefinition { Id = 0, Position = new Point2(0, 0) });
        level.Slots.Add(new SlotDefinition { Id = 1, Position = new Point2(0, 100) });
        level.Waves.Add(new WaveDefinition { Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "boss", RouteId = "road", Count = 1 } } }); db.Levels.Add(level); return db;
    }
    private static RaceSnapshot Snapshot(int steps, bool faction, string level, bool breach, bool failure, bool mixed)
    {
        GameDatabase db = Fixture(level, breach, failure);
        var b = new BattleSession(db, db.Levels[0], new[] { "hero.汉将" }, faction);
        b.TryRecruit("hero.汉将", 0); b.TryMoveHero(0, 1); b.TryUpgrade(1); b.Start();
        for (int i = 0; i < steps && b.State == BattleState.Running; i++) { if (mixed) b.SetSpeed(i % 3 + 1); b.Tick(1f / 30); }
        if (!breach && !failure) { foreach (EnemyUnit enemy in b.Enemies) { enemy.BurnUntil = b.Time + 1; enemy.BurnDamage = 100000; } b.Tick(1f / 30); }
        return b.GetRaceSnapshot();
    }
    private static RaceSnapshot Good(int steps) { return Snapshot(steps, true, "test", false, false, false); }
    private static string Prefix { get { return "yanyi.race.faction-army-v1.all-boss-v1.test."; } }
    private static void Set(object item, string property, object value) { item.GetType().GetProperty(property).GetSetMethod(true).Invoke(item, new[] { value }); }
    private static void Invalid(RaceSnapshot snapshot)
    {
        var storage = new Storage(); var result = Record(storage.Open(), snapshot);
        Check(!Value<bool>(result, "IsNewRecord"), "非法快照不得刷新"); Check(Value<string>(result, "IneligibilityReason").Length > 0, "须给出原因"); Equal(0, storage.Writes); Equal(0, storage.Saves); Check(Read(storage.Open(), "test", "faction-army-v1") == null, "不得产生存档");
    }
    // 测试独立生成已重算校验的坏数据，确保字段校验并非只依赖校验和。
    private static byte[] Payload(int mask, long duration, int actionCount, int changeCount, int heroLength, long actionTime, int speed)
    {
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
            writer.Write(1);
            foreach (string text in new[] { "test", "faction-army-v1", "all-boss-v1" }) { byte[] bytes = Encoding.UTF8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes); }
            writer.Write(100L); writer.Write(mask); writer.Write(100); writer.Write(0); writer.Write(1);
            writer.Write(duration); writer.Write(0L); writer.Write(0L); writer.Write(0); writer.Write(actionCount);
            if (actionCount > 0 && actionCount <= 64) {
                byte[] kind = Encoding.UTF8.GetBytes("recruit"); writer.Write(kind.Length); writer.Write(kind);
                writer.Write(heroLength); if (heroLength > 0 && heroLength <= 256) { byte[] hero = new byte[heroLength]; for (int i = 0; i < hero.Length; i++) hero[i] = 104; writer.Write(hero); }
                writer.Write(0); writer.Write(1); writer.Write(990); writer.Write(actionTime);
            }
            writer.Write(changeCount);
            if (changeCount > 0 && changeCount <= 256) { writer.Write(speed); writer.Write(0L); }
            writer.Flush(); return stream.ToArray();
        }
    }
    private static void InstallPayload(Storage storage, byte[] payload)
    {
        string slot = Prefix + "slot0."; storage.Values[Prefix + "active"] = 0; storage.Values[slot + "ready"] = 1; storage.Values[slot + "length"] = payload.Length;
        unchecked { uint checksum = 2166136261; foreach (byte b in payload) checksum = (checksum ^ b) * 16777619; storage.Values[slot + "checksum"] = (int)checksum; }
        for (int i = 0; i < (payload.Length + 3) / 4; i++) { uint value = 0; for (int j = 0; j < 4 && i * 4 + j < payload.Length; j++) value |= (uint)payload[i * 4 + j] << (8 * j); storage.Values[slot + "data" + i] = unchecked((int)value); }
    }
    private static int Main()
    {
        Run("倍率说明保存接口准确报告成功失败与重试", delegate {
            var storage = new Storage(); var progress = storage.Open();
            Equal((object)true, Call(progress, "MarkMarchSpeedIntroRead"));
            Equal((object)true, Call(progress, "MarkMarchSpeedIntroRead")); Equal(1, storage.Saves);
            var failure = new Storage { FailSave = true }; var pending = failure.Open();
            Equal((object)false, Call(pending, "MarkMarchSpeedIntroRead"));
            Check(!pending.HasReadMarchSpeedIntro && !failure.Open().HasReadMarchSpeedIntro, "失败不得声称已读");
            failure.FailSave = false; Equal((object)true, Call(pending, "MarkMarchSpeedIntroRead"));
            Check(failure.Open().HasReadMarchSpeedIntro, "成功重试必须可重新读取");
            var readFailure = new Storage { FailReadAfter = 0 };
            Equal((object)false, Call(readFailure.Open(), "MarkMarchSpeedIntroRead")); Equal(0, readFailure.Writes);
            var writeFailure = new Storage { FailAfter = 0 };
            Equal((object)false, Call(writeFailure.Open(), "MarkMarchSpeedIntroRead")); Equal(0, writeFailure.Saves);
        });
        Run("首次、较快、同值和较慢严格比较", delegate {
            var s = new Storage(); var p = s.Open(); RaceSnapshot first = Good(10); object a = Record(p, first);
            Check(Value<bool>(a, "IsFirstRecord") && Value<bool>(a, "IsNewRecord") && Value<bool>(a, "HasValidTime"), "首次标记"); Check(object.ReferenceEquals(first, Value<RaceSnapshot>(a, "Snapshot")), "本次真实快照"); Check(Value<object>(a, "PreviousRecord") == null, "首次无旧榜");
            object previous = Value<object>(a, "BestRecord"); object faster = Record(p, Good(5)); Equal(Ms(previous), Ms(Value<object>(faster, "PreviousRecord"))); Check(Value<bool>(faster, "IsNewRecord") && !Value<bool>(faster, "IsFirstRecord"), "更快标记");
            long best = Ms(Value<object>(faster, "BestRecord")); int writes = s.Writes, saves = s.Saves;
            object equal = Record(p, Good(5)); Check(Value<bool>(equal, "IsEqualRecord") && !Value<bool>(equal, "IsNewRecord"), "同值不刷新"); object slower = Record(p, Good(20)); Check(!Value<bool>(slower, "IsNewRecord") && !Value<bool>(slower, "IsEqualRecord"), "较慢不刷新"); Equal(best, Ms(Value<object>(slower, "BestRecord"))); Equal(writes, s.Writes); Equal(saves, s.Saves); Equal(best, Ms(Read(s.Open(), "test", "faction-army-v1")));
        });
        Run("同值城防不同仍保留原快照", delegate { var s = new Storage(); RaceSnapshot first = Good(5); Record(s.Open(), first); RaceSnapshot equal = Good(5); Set(equal, "CastleHealth", 99); Record(s.Open(), equal); Equal(100, Value<int>(Read(s.Open(), "test", "faction-army-v1"), "CastleHp")); });
        Run("失败、突破、未击退与空快照拒绝", delegate { Invalid(Snapshot(5, true, "test", true, false, false)); Invalid(Snapshot(5, true, "test", false, true, false)); RaceSnapshot s = Good(5); Set(s, "DefeatedBossCount", 0); Invalid(s); s = Good(5); Set(s, "SpawnedBossCount", 0); Invalid(s); Invalid(null); });
        Run("旧零突破与突破成绩不能迁新榜且互不写共享键", delegate { var s = new Storage(); var p = s.Open(); p.RecordVictory("test", 90, 10, 1, 0, 0); Check(Read(p, "test", "faction-army-v1") == null, "旧零突破不得认证"); p.RecordVictory("test", 95, 8, 1, 1, 1); var old = new Dictionary<string, int>(s.Values); Record(p, Good(5)); foreach (var pair in old) Equal(pair.Value, s.Values[pair.Key]); Check(p.GetBestCastle("test") == 95 && p.GetFastestRecord("test").BossBreachCount == 1, "新榜不得污染旧成绩"); var clean = new Storage(); Record(clean.Open(), Good(5)); Check(!clean.Open().IsCleared("test") && clean.Open().GetBestCastle("test") == 0, "新入口独立"); });
        Run("实际关卡和规则隔离", delegate { var s = new Storage(); Record(s.Open(), Good(5)); Record(s.Open(), Snapshot(10, false, "test", false, false, false)); Record(s.Open(), Snapshot(20, true, "other", false, false, false)); Check(Ms(Read(s.Open(), "test", "faction-army-v1")) < Ms(Read(s.Open(), "test", "growth-tempo-v2")), "规则隔离"); Check(Read(s.Open(), "other", "faction-army-v1") != null, "关卡隔离"); Check(Read(s.Open(), "missing", "faction-army-v1") == null, "缺关卡"); });
        Run("摘要跨实例保持真实操作、倍率时长和只读集合", delegate {
            RaceSnapshot snap = Snapshot(12, true, "test", false, false, true); var s = new Storage(); Record(s.Open(), snap); object record = Read(new Storage(s.Values).Open(), "test", snap.RuleVersion);
            Equal((long)Math.Round(snap.ElapsedSeconds * 1000, MidpointRounding.AwayFromZero), Ms(record)); Equal(snap.SpeedMask, Value<int>(record, "SpeedMask")); Equal(snap.CastleHealth, Value<int>(record, "CastleHp")); Equal(snap.DefeatedBossCount, Value<int>(record, "BossCount")); Equal("all-boss-v1", Value<string>(record, "QualificationVersion")); Equal("test", Value<string>(record, "LevelId")); Equal(snap.RuleVersion, Value<string>(record, "RuleVersion")); Equal("混合1/2/3×", Value<string>(record, "SpeedSummary"));
            long total = 0; for (int i = 1; i <= 3; i++) { long ms = (long)Call(record, "GetSpeedMilliseconds", i); Check(Math.Abs(ms - snap.GetSpeedSeconds(i) * 1000) <= 1.1, "时长必须来自实际片段"); total += ms; } Equal(Ms(record), total);
            IList actions = Value<IList>(record, "Actions"), changes = Value<IList>(record, "SpeedChanges"); Check(actions.IsReadOnly && changes.IsReadOnly, "摘要只读"); Equal(snap.Actions.Count, actions.Count); Equal(snap.SpeedChanges.Count, changes.Count);
            for (int i = 0; i < actions.Count; i++) { RaceAction a = snap.Actions[i]; Equal(a.Kind, Value<string>(actions[i], "Kind")); Equal(a.HeroId, Value<string>(actions[i], "HeroId")); Equal(a.SlotId, Value<int>(actions[i], "SlotId")); Equal(a.Level, Value<int>(actions[i], "Level")); Equal(a.Gold, Value<int>(actions[i], "Gold")); Equal((long)Math.Round(a.ElapsedSeconds * 1000, MidpointRounding.AwayFromZero), Value<long>(actions[i], "ElapsedMilliseconds")); }
            for (int i = 0; i < changes.Count; i++) { Equal(snap.SpeedChanges[i].Speed, Value<int>(changes[i], "Speed")); Equal((long)Math.Round(snap.SpeedChanges[i].ElapsedSeconds * 1000, MidpointRounding.AwayFromZero), Value<long>(changes[i], "ElapsedMilliseconds")); }
            foreach (PropertyInfo prop in record.GetType().GetProperties()) Check(prop.GetSetMethod() == null, "记录无公开setter"); foreach (object action in actions) foreach (PropertyInfo prop in action.GetType().GetProperties()) Check(prop.GetSetMethod() == null, "操作无公开setter");
        });
        Run("重复同快照结算不重写", delegate { var s = new Storage(); var p = s.Open(); RaceSnapshot snap = Good(5); Record(p, snap); int writes = s.Writes, saves = s.Saves; Record(p, snap); Record(s.Open(), snap); Equal(writes, s.Writes); Equal(saves, s.Saves); });
        Run("截断摘要跨实例恢复且时长完整", delegate {
            GameDatabase db = Fixture("test", false, false); var b = new BattleSession(db, db.Levels[0], new[] { "hero.汉将" }, true); b.TryRecruit("hero.汉将", 0);
            for (int i = 0; i < 100; i++) b.TryMoveHero(i % 2, 1 - i % 2);
            b.Start(); for (int i = 0; i < 1000; i++) { b.SetSpeed(i % 3 + 1); b.Tick(1f / 30); }
            foreach (EnemyUnit enemy in b.Enemies) { enemy.BurnUntil = b.Time + 1; enemy.BurnDamage = 100000; } b.Tick(1f / 30);
            RaceSnapshot snap = b.GetRaceSnapshot(); Check(snap.IsEligible && snap.SummaryTruncated, "夹具须合格且摘要截断"); var s = new Storage(); Record(s.Open(), snap); object record = Read(s.Open(), "test", "faction-army-v1");
            Check(Value<bool>(record, "SummaryTruncated"), "重读须保留截断标记"); Equal(64, Value<IList>(record, "Actions").Count); Equal(256, Value<IList>(record, "SpeedChanges").Count);
            long sum = 0; for (int i = 1; i <= 3; i++) { long duration = (long)Call(record, "GetSpeedMilliseconds", i); Check(Math.Abs(duration - snap.GetSpeedSeconds(i) * 1000) <= 1.1, "截断不能截断实际时长"); sum += duration; } Equal(Ms(record), sum);
        });
        Run("首次写每一个中断点均不可读取候选槽", delegate { var full = new Storage(); Record(full.Open(), Good(5)); for (int i = 0; i < full.Writes; i++) { var s = new Storage { FailAfter = i }; try { Record(s.Open(), Good(5)); } catch (Exception) { } s.FailAfter = -1; Check(Read(s.Open(), "test", "faction-army-v1") == null, "中断点 " + i + " 暴露未提交候选"); } });
        Run("已有最快时每个写入中断点均保留旧快照", delegate { var baseline = new Storage(); Record(baseline.Open(), Good(20)); long oldMs = Ms(Read(baseline.Open(), "test", "faction-army-v1")); var full = new Storage(baseline.Values); Record(full.Open(), Good(5)); for (int i = 0; i < full.Writes; i++) { var s = new Storage(baseline.Values) { FailAfter = i }; try { Record(s.Open(), Good(5)); } catch (Exception) { } s.FailAfter = -1; Equal(oldMs, Ms(Read(s.Open(), "test", "faction-army-v1"))); } });
        Run("保存异常回滚活动指针", delegate { var s = new Storage(); Record(s.Open(), Good(20)); long oldMs = Ms(Read(s.Open(), "test", "faction-army-v1")); s.FailSave = true; try { Record(s.Open(), Good(5)); } catch (Exception) { } s.FailSave = false; Equal(oldMs, Ms(Read(s.Open(), "test", "faction-army-v1"))); });
        Run("损坏、缺键、半写和超大长度均安全返回空", delegate { var s = new Storage(); Record(s.Open(), Good(5)); var keys = new List<string>(s.Values.Keys); foreach (string key in keys) { var missing = new Storage(s.Values); missing.Values.Remove(key); Check(Read(missing.Open(), "test", "faction-army-v1") == null, "缺键未拒绝：" + key); var changed = new Storage(s.Values); changed.Values[key] = unchecked(changed.Values[key] + 1); Check(Read(changed.Open(), "test", "faction-army-v1") == null, "损坏未拒绝：" + key); } var huge = new Storage(s.Values); int active = huge.Values[Prefix + "active"]; huge.Values[Prefix + "slot" + active + ".length"] = int.MaxValue; Check(Read(huge.Open(), "test", "faction-army-v1") == null, "超大长度不得分配"); });
        Run("重算校验后非法数量字符串时长倍率仍拒绝", delegate {
            var valid = new Storage(); InstallPayload(valid, Payload(1, 100, 0, 1, 0, 0, 1)); Check(Read(valid.Open(), "test", "faction-army-v1") != null, "独立payload格式夹具必须有效");
            InstallPayload(valid, Payload(1, 100, 1, 1, 1, 0, 1)); Check(Read(valid.Open(), "test", "faction-army-v1") != null, "带操作的独立payload夹具必须有效");
            byte[][] bad = {
                Payload(8, 100, 0, 1, 0, 0, 1), Payload(1, 101, 0, 1, 0, 0, 1), Payload(1, -1, 0, 1, 0, 0, 1),
                Payload(1, 100, -1, 1, 0, 0, 1), Payload(1, 100, int.MaxValue, 1, 0, 0, 1),
                Payload(1, 100, 1, 1, int.MaxValue, 0, 1), Payload(1, 100, 1, 1, -1, 0, 1),
                Payload(1, 100, 1, 1, 1, -1, 1), Payload(1, 100, 1, 1, 1, 101, 1),
                Payload(1, 100, 0, int.MaxValue, 0, 0, 1), Payload(1, 100, 0, -1, 0, 0, 1),
                Payload(1, 100, 0, 1, 0, 0, 4), Payload(1, 100, 0, 1, 0, 0, 2)
            };
            foreach (byte[] bytes in bad) { var s = new Storage(); InstallPayload(s, bytes); Check(Read(s.Open(), "test", "faction-army-v1") == null, "非法字段不得因校验和正确而通过"); }
            var tail = new Storage(); var extra = new List<byte>(Payload(1, 100, 0, 1, 0, 0, 1)); extra.Add(0); InstallPayload(tail, extra.ToArray()); Check(Read(tail.Open(), "test", "faction-army-v1") == null, "不得忽略尾随数据");
        });
        Run("无效时长、倍率、城防、漏兵和Boss字段拒绝", delegate { foreach (double t in new[] { 0, -1, double.NaN, double.PositiveInfinity, double.MaxValue }) { RaceSnapshot snap = Good(5); Set(snap, "ElapsedSeconds", t); Invalid(snap); } foreach (string field in new[] { "SpeedMask", "CastleHealth", "LeakedCount", "ExpectedBossCount", "SpawnedBossCount", "DefeatedBossCount", "BossBreachCount" }) { RaceSnapshot snap = Good(5); Set(snap, field, -1); Invalid(snap); } RaceSnapshot mask = Good(5); Set(mask, "SpeedMask", 8); Invalid(mask); RaceSnapshot duration = Good(5); double[] seconds = (double[])typeof(RaceSnapshot).GetField("speedSeconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duration); seconds[0] = double.NaN; Invalid(duration); duration = Good(5); seconds = (double[])typeof(RaceSnapshot).GetField("speedSeconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duration); seconds[0] += 1; Invalid(duration); });
        Run("只读异常不传递给界面", delegate { var p = new GameProgress((k, f) => { throw new InvalidOperationException("模拟读取失败"); }, (k, v) => { }, () => { }); Check(Read(p, "test", "faction-army-v1") == null, "读取失败返回空"); });
        Run("写前每个旧榜读取点瞬时失败均禁止覆盖并保留最快", delegate {
            var source = new Storage(); Record(source.Open(), Good(5)); var count = new Storage(source.Values); long best = Ms(Read(count.Open(), "test", "faction-army-v1"));
            foreach (int steps in new[] { 2, 20 }) for (int i = 0; i < count.Reads; i++) {
                var s = new Storage(source.Values) { FailReadAfter = i }; object result = Record(s.Open(), Good(steps));
                Check(!Value<bool>(result, "IsNewRecord") && !Value<bool>(result, "IsFirstRecord"), "读取异常不得误报刷新/首次，读取点" + i);
                Check(Value<string>(result, "IneligibilityReason").Contains("读取"), "读取失败须提供可理解原因"); Equal(0, s.Writes); Equal(0, s.Saves); Equal(best, Ms(Read(s.Open(), "test", "faction-army-v1")));
            }
        });
        Run("含分隔符和转义符的实际标识符相互隔离", delegate {
            var s = new Storage(); string[] ids = { "test.part", "test%2Epart", "test/part", "test%2Fpart", "test.all-boss-v1.part" };
            for (int i = 0; i < ids.Length; i++) { RaceSnapshot snap = Snapshot(i + 2, true, ids[i], false, false, false); object result = Record(s.Open(), snap); Check(Value<bool>(result, "IsFirstRecord"), "分隔符ID不得碰撞"); }
            for (int i = 0; i < ids.Length; i++) { object stored = Read(s.Open(), ids[i], "faction-army-v1"); Equal(ids[i], Value<string>(stored, "LevelId")); Equal((long)Math.Round(Snapshot(i + 2, true, ids[i], false, false, false).ElapsedSeconds * 1000, MidpointRounding.AwayFromZero), Ms(stored)); }
        });
        Run("倍率说明只在确认后保存且重复确认不写", delegate { var s = new Storage(); var p = s.Open(); Check(!Value<bool>(p, "HasReadMarchSpeedIntro"), "初次未读"); Read(p, "test", "faction-army-v1"); Record(p, Good(5)); Check(!Value<bool>(p, "HasReadMarchSpeedIntro"), "其他入口不得标记"); int writes = s.Writes, saves = s.Saves; Call(p, "MarkMarchSpeedIntroRead"); Equal(writes + 1, s.Writes); Equal(saves + 1, s.Saves); Equal(1, s.Values["yanyi.help.march-pressure-v1"]); Check(Value<bool>(s.Open(), "HasReadMarchSpeedIntro"), "重新读入仍已读"); Call(p, "MarkMarchSpeedIntroRead"); Equal(writes + 1, s.Writes); Equal(saves + 1, s.Saves); });
        Run("倍率说明保存及写入失败安全回滚并允许重试", delegate {
            var s = new Storage { FailSave = true }; var p = s.Open(); Call(p, "MarkMarchSpeedIntroRead"); Check(!Value<bool>(p, "HasReadMarchSpeedIntro") && !Value<bool>(s.Open(), "HasReadMarchSpeedIntro"), "save异常不得误标已读"); Equal(0, s.Saves);
            s.FailSave = false; Call(p, "MarkMarchSpeedIntroRead"); Check(Value<bool>(s.Open(), "HasReadMarchSpeedIntro"), "失败后可再次明确确认"); Equal(1, s.Saves);
            var writeFailure = new Storage { FailAfter = 0 }; Call(writeFailure.Open(), "MarkMarchSpeedIntroRead"); Check(!Value<bool>(writeFailure.Open(), "HasReadMarchSpeedIntro"), "write异常不得误标已读"); Equal(0, writeFailure.Saves);
            var readFailure = new Storage { FailReadAfter = 0 }; Call(readFailure.Open(), "MarkMarchSpeedIntroRead"); Equal(0, readFailure.Writes); Equal(0, readFailure.Saves);
        });
        Run("新战斗版本不得改写旧普通最快和最近通关", delegate {
            var s = new Storage(); var p = s.Open(); p.RecordVictory("huangjin", 30, 300, 1, 0, 0);
            var before = new Dictionary<string, int>(s.Values);
            Call(p, "RecordVersionedVictory", "huangjin", 29, 200.0, 3, 1, 0, "sensation-hj-mn-v1-army");
            foreach (var item in before) if (item.Key.StartsWith("yanyi.fastest.") || item.Key.StartsWith("yanyi.recent.")) Equal(item.Value, s.Values[item.Key]);
            Equal(200000L, p.GetFastestRecord("huangjin", "sensation-hj-mn-v1-army").ElapsedMilliseconds);
            Equal(300000L, p.GetFastestRecord("huangjin").ElapsedMilliseconds);
        });
        Run("旧最快记录完整回归", UiFastestRecordChecks.Run);
        Console.WriteLine("竞速存档专项：" + passed + " 通过，" + failed + " 失败"); return failed == 0 ? 0 : 1;
    }
}
