using System;
using System.Collections.Generic;
using YanyiSandpan.Presentation;
#if UNITY_EDITOR
using UnityEngine;
using YanyiSandpan.Domain;
#endif

// 使用内存字典验证真实存储服务，绝不读写玩家偏好。
public static class UiFastestRecordChecks
{
    private static int assertionCount;

    public static void Run()
    {
        assertionCount = 0;
        VerifyComparisonAndSnapshot();
        VerifyLegacyCompatibility();
        VerifyInvalidTimes();
        VerifyInvalidSnapshots();
        VerifyCorruptRecords();
        VerifyMillisecondsAndSpeedSummary();
        VerifyRecentRecordsAndRuleVersions();
#if UNITY_EDITOR
        Debug.Log("最快纪录内存检查通过：" + assertionCount + "项断言");
#else
        Console.WriteLine("最快纪录内存检查通过：" + assertionCount + "项断言");
#endif
    }

    private static void VerifyComparisonAndSnapshot()
    {
        var storage = new MemoryStorage();
        GameProgress progress = storage.Open();
        Require(progress.GetFastestRecord("sample") == null, "空存档应无最快纪录");
        VictoryRecordResult first = progress.RecordVictory("sample", 20, 100, 2, 4, 1);
        Require(first.IsFirstRecord && first.IsNewRecord && first.PreviousRecord == null && first.HasValidTime,
            "首次纪录标志或旧纪录不正确");
        Require(first.CurrentRecord == first.BestRecord && first.BestRecord.ElapsedMilliseconds == 100000 && storage.SaveCount == 1,
            "首次100000毫秒未作为同一份快照保存一次");
        Require(storage.Values.ContainsKey("yanyi.recent." + GameProgress.FastestRuleVersion + ".sample.ready"),
            "首次合法胜利没有保存最近通关，较慢局只能停留在结算页");
        VictoryRecordResult slower = progress.RecordVictory("sample", 25, 110, 1, 2, 0);
        Require(!slower.IsNewRecord && !slower.IsFirstRecord && slower.CurrentRecord.ElapsedMilliseconds == 110000 &&
            slower.BestRecord.ElapsedMilliseconds == 100000, "较慢110000毫秒覆盖旧纪录或丢失本次成绩");
        Require(progress.GetBestCastle("sample") == 25 && slower.BestRecord.CastleHp == 20,
            "最佳城防应独立更新，最快纪录不得混入另一局城防");
        Require(progress.GetRecentRecord("sample").ElapsedMilliseconds == 110000 &&
            progress.GetRecentRecord("sample").CastleHp == 25, "较慢胜利离开结算后丢失最近通关");
        int writes = storage.WriteCount, saves = storage.SaveCount;
        VictoryRecordResult equal = progress.RecordVictory("sample", 15, 100, 4, 8, 2);
        Require(!equal.IsNewRecord && equal.IsEqualRecord && equal.BestRecord.ElapsedMilliseconds == 100000 &&
            progress.GetFastestRecord("sample").CastleHp == 20 && progress.GetRecentRecord("sample").CastleHp == 15 &&
            storage.WriteCount == writes + 9 && storage.SaveCount == saves + 1,
            "相等100000毫秒应只更新最近通关，不得覆盖最快快照");
        VictoryRecordResult faster = progress.RecordVictory("sample", 15, 90, 7, 8, 2);
        Require(faster.IsNewRecord && !faster.IsFirstRecord && faster.PreviousRecord.ElapsedMilliseconds == 100000 &&
            faster.CurrentRecord.ElapsedMilliseconds == 90000 && faster.BestRecord == faster.CurrentRecord,
            "更快90000毫秒未保留比较前100000毫秒旧纪录");
        Require(first.BestRecord.ElapsedMilliseconds == 100000 && first.BestRecord.CastleHp == 20,
            "后续刷新修改了旧结算持有的快照");
        FastestRecord record = storage.Open().GetFastestRecord("sample");
        Require(record.ElapsedMilliseconds == 90000 && record.SpeedMask == 7 && record.CastleHp == 15 &&
            record.LeakedCount == 8 && record.BossBreachCount == 2 && progress.GetBestCastle("sample") == 25,
            "跨实例重读后时间、倍率、城防与突破不属于同一局");
        Require(record.SpeedSummary == "混合1/2/3×", "混合倍率摘要不正确");
        progress.RecordVictory("other", 8, 50, 1, 0, 0);
        Require(progress.GetFastestRecord("other").ElapsedMilliseconds == 50000 &&
            progress.GetFastestRecord("sample").ElapsedMilliseconds == 90000, "不同关卡最快纪录互相覆盖");
    }

    private static void VerifyLegacyCompatibility()
    {
        var storage = new MemoryStorage();
        storage.Values["yanyi.clear.legacy"] = 1;
        storage.Values["yanyi.best.legacy"] = 30;
        storage.Values["yanyi.fastest.old-rule.legacy.msLow"] = 40000;
        GameProgress progress = storage.Open();
        Require(progress.IsCleared("legacy") && progress.GetBestCastle("legacy") == 30 &&
            progress.GetFastestRecord("legacy") == null, "旧通关、城防或旧规则时间迁移有误");
        progress.RecordVictory("legacy", 29);
        Require(storage.SaveCount == 0 && progress.GetFastestRecord("legacy") == null,
            "旧重载不得推算最快时间或重写较低城防");
        VictoryRecordResult first = progress.RecordVictory("legacy", 10, 100, 1, 5, 1);
        Require(first.IsFirstRecord && progress.GetBestCastle("legacy") == 30 && first.BestRecord.CastleHp == 10 &&
            storage.Values["yanyi.fastest.old-rule.legacy.msLow"] == 40000, "新纪录覆盖了旧最佳城防或旧规则数据");
        progress.RecordVictory("old-overload", 18);
        progress.RecordVictory("old-overload", 18);
        Require(progress.IsCleared("old-overload") && progress.GetBestCastle("old-overload") == 18 &&
            progress.GetFastestRecord("old-overload") == null && storage.SaveCount == 2, "旧RecordVictory重载失去幂等兼容");
    }

    private static void VerifyInvalidTimes()
    {
        double[] invalid = { 0, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity,
            double.MaxValue, (double)long.MaxValue / 1000, 0.0001 };
        foreach (double seconds in invalid)
        {
            var storage = new MemoryStorage();
            GameProgress progress = storage.Open();
            VictoryRecordResult result = progress.RecordVictory("invalid", 18, seconds, 1, 0, 0);
            Require(!result.HasValidTime && !result.IsNewRecord && result.BestRecord == null &&
                progress.GetFastestRecord("invalid") == null, "非法或溢出时间被接纳：" + seconds);
            Require(progress.IsCleared("invalid") && progress.GetBestCastle("invalid") == 18 && storage.SaveCount == 1,
                "非法时间阻止了合法通关与城防保存：" + seconds);
            progress.RecordVictory("invalid", 18, 100, 1, 0, 0);
            int writes = storage.WriteCount, saves = storage.SaveCount;
            result = progress.RecordVictory("invalid", 18, seconds, 1, 0, 0);
            Require(!result.HasValidTime && result.PreviousRecord.ElapsedMilliseconds == 100000 &&
                result.BestRecord.ElapsedMilliseconds == 100000 && storage.WriteCount == writes && storage.SaveCount == saves,
                "非法时间覆盖或重写已有有效纪录：" + seconds);
        }
    }

    private static void VerifyInvalidSnapshots()
    {
        int[,] invalid = { { 0, 20, 0, 0 }, { 8, 20, 0, 0 }, { -1, 20, 0, 0 },
            { 1, 0, 0, 0 }, { 1, -1, 0, 0 }, { 1, 20, -1, 0 }, { 1, 20, 1, 2 }, { 1, 20, 0, -1 } };
        for (int index = 0; index < invalid.GetLength(0); index++)
        {
            var storage = new MemoryStorage();
            GameProgress progress = storage.Open();
            VictoryRecordResult result = progress.RecordVictory("snapshot", invalid[index, 1], 100,
                invalid[index, 0], invalid[index, 2], invalid[index, 3]);
            Require(!result.HasValidTime && progress.GetFastestRecord("snapshot") == null,
                "非法倍率、城防或突破快照被接纳：" + index);
            Require(progress.IsCleared("snapshot") == (invalid[index, 1] > 0), "城防归零或负数不应写通关");
        }
        var empty = new MemoryStorage();
        Require(!empty.Open().RecordVictory("", 20, 100, 1, 0, 0).IsNewRecord && empty.WriteCount == 0,
            "空关卡标识不能建立纪录");
    }

    private static void VerifyCorruptRecords()
    {
        var source = new MemoryStorage();
        source.Open().RecordVictory("corrupt", 20, 100, 2, 3, 1);
        string prefix = "yanyi.fastest." + GameProgress.FastestRuleVersion + ".corrupt.";
        string[] fields = { "ready", "msHigh", "msLow", "mask", "castle", "leaks", "bosses", "checksum" };
        foreach (string field in fields)
        {
            var missing = new MemoryStorage(source.Values);
            missing.Values.Remove(prefix + field);
            Require(missing.Open().GetFastestRecord("corrupt") == null, "缺字段记录被读取：" + field);
            var changed = new MemoryStorage(source.Values);
            changed.Values[prefix + field]++;
            Require(changed.Open().GetFastestRecord("corrupt") == null, "损坏字段记录被读取：" + field);
        }
        foreach (int high in new[] { -1, int.MinValue })
        {
            var overflow = new MemoryStorage(source.Values);
            overflow.Values[prefix + "msHigh"] = high;
            Require(overflow.Open().GetFastestRecord("corrupt") == null, "负数或溢出高位被读取");
        }
        var zero = new MemoryStorage(source.Values);
        zero.Values[prefix + "msLow"] = 0;
        Require(zero.Open().GetFastestRecord("corrupt") == null, "零毫秒损坏纪录被读取");
        VictoryRecordResult repaired = zero.Open().RecordVictory("corrupt", 12, 110, 4, 8, 2);
        Require(repaired.IsFirstRecord && repaired.PreviousRecord == null && repaired.BestRecord.ElapsedMilliseconds == 110000 &&
            zero.Open().GetBestCastle("corrupt") == 20 && zero.Open().GetFastestRecord("corrupt").CastleHp == 12,
            "损坏时间未被有效首份纪录替代，或影响独立城防");
    }

    private static void VerifyMillisecondsAndSpeedSummary()
    {
        int[] masks = { 1, 2, 4, 3, 5, 6, 7 };
        string[] summaries = { "全程1×", "全程2×", "全程3×", "混合1/2×", "混合1/3×", "混合2/3×", "混合1/2/3×" };
        for (int index = 0; index < masks.Length; index++)
        {
            var storage = new MemoryStorage();
            VictoryRecordResult result = storage.Open().RecordVictory("speed", 20, 1.2345, masks[index], 0, 0);
            Require(result.BestRecord.ElapsedMilliseconds == 1235 && result.BestRecord.SpeedSummary == summaries[index] &&
                storage.Open().GetFastestRecord("speed").SpeedSummary == summaries[index], "毫秒四舍五入或倍率摘要不一致");
        }
        foreach (long milliseconds in new[] { 1L, 2147483648L, 4294967296L, 864000000000L })
        {
            var storage = new MemoryStorage();
            storage.Open().RecordVictory("long", 20, milliseconds / 1000.0, 1, 0, 0);
            Require(storage.Open().GetFastestRecord("long").ElapsedMilliseconds == milliseconds,
                "毫秒高低位重读错误：" + milliseconds);
        }
        var rounding = new MemoryStorage();
        rounding.Open().RecordVictory("equal-ms", 20, 1.2341, 1, 0, 0);
        Require(!rounding.Open().RecordVictory("equal-ms", 15, 1.234, 2, 0, 0).IsNewRecord,
            "相同整数毫秒不应刷新纪录");
    }

    private static void VerifyRecentRecordsAndRuleVersions()
    {
        var storage = new MemoryStorage();
        string legacy = "yanyi.fastest." + GameProgress.PreviousFastestRuleVersion + ".huangjin.";
        string[] fields = { "ready", "msHigh", "msLow", "mask", "castle", "leaks", "bosses", "checksum" };
        int[] values = { 1, 0, 290035, 7, 20, 6, 0, -526637781 };
        for (int index = 0; index < fields.Length; index++) storage.Values[legacy + fields[index]] = values[index];
        storage.Values["yanyi.clear.huangjin"] = 1; storage.Values["yanyi.best.huangjin"] = 30;
        GameProgress progress = storage.Open();
        Require(progress.GetFastestRecord("huangjin") == null && progress.GetRecentRecord("huangjin") == null &&
            progress.GetFastestRecord("huangjin", GameProgress.PreviousFastestRuleVersion).ElapsedMilliseconds == 290035 &&
            storage.WriteCount == 0 && storage.SaveCount == 0,
            "旧版真实快照被隐藏、写回为本版最快或假造为最近通关");
        progress.RecordVictory("huangjin", 25, 300, 2, 2, 0);
        progress.RecordVictory("huangjin", 15, 320, 1, 4, 1);
        GameProgress reopened = new MemoryStorage(storage.Values).Open();
        FastestRecord recent = reopened.GetRecentRecord("huangjin");
        Require(recent.ElapsedMilliseconds == 320000 && recent.SpeedMask == 1 && recent.CastleHp == 15 &&
            recent.LeakedCount == 4 && recent.BossBreachCount == 1 &&
            reopened.GetFastestRecord("huangjin").ElapsedMilliseconds == 300000 && reopened.GetBestCastle("huangjin") == 30,
            "复制存储后重建服务丢失较慢局快照，或覆盖旧最佳城防");
        for (int index = 0; index < fields.Length; index++)
            Require(storage.Values[legacy + fields[index]] == values[index], "写入本版记录改动了旧版快照：" + fields[index]);
        string prefix = "yanyi.recent." + GameProgress.FastestRuleVersion + ".huangjin.";
        foreach (string field in fields)
        {
            var missing = new MemoryStorage(storage.Values); missing.Values.Remove(prefix + field);
            Require(missing.Open().GetRecentRecord("huangjin") == null && missing.Open().GetFastestRecord("huangjin") != null,
                "最近通关缺字段未拒绝，或污染独立最快：" + field);
            var corrupt = new MemoryStorage(storage.Values); corrupt.Values[prefix + field]++;
            Require(corrupt.Open().GetRecentRecord("huangjin") == null, "最近通关损坏字段未拒绝：" + field);
        }
        int writes = storage.WriteCount, saves = storage.SaveCount;
        progress.RecordVictory("huangjin", 15, double.NaN, 1, 4, 1);
        progress.RecordVictory("huangjin", 0, 10, 1, 4, 1);
        progress.RecordVictory("huangjin", 15, 10, 0, 4, 1);
        progress.RecordVictory("huangjin", 15);
        Require(storage.WriteCount == writes && storage.SaveCount == saves &&
            progress.GetRecentRecord("huangjin").ElapsedMilliseconds == 320000,
            "非法时长、失败城防、非法快照或旧重载覆盖了最近通关");
        progress.RecordVictory("huangjin", 12, 290, 4, 6, 1);
        Require(progress.GetFastestRecord("huangjin").ElapsedMilliseconds == 290000 &&
            progress.GetRecentRecord("huangjin").ElapsedMilliseconds == 290000,
            "更快胜利未同时更新本版最快与最近通关");
    }

#if UNITY_EDITOR
    // 复用主验证已有画布；夹具只验证流程去重和Boss容错，不代表关卡难度。
    public static void Run(UiFactory ui, RectTransform root, GameDatabase original)
    {
        Run();
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        Require(level != null && database.FindEnemy("boss_zhang_liao") != null, "最快纪录流程夹具缺少基础内容");
        level.CastleMaxHealth = 30;
        level.IncomePerSecond = 0;
        level.Routes = new List<RouteDefinition> { new RouteDefinition { Id = "fastest-route",
            Points = new List<Point2> { new Point2(1280, 580), new Point2(1322, 580) } } };
        level.Slots = new List<SlotDefinition> { new SlotDefinition { Id = 0, Position = new Point2(400, 400) } };
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "最快纪录夹具", StartDelay = 1,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "boss_zhang_liao", RouteId = "fastest-route", Count = 1, Interval = 1 } } } };
        var storage = new MemoryStorage();
        GameProgress progress = storage.Open();
        using (var flow = new GameFlow(ui, root, database, progress))
        {
            flow.BeginBattle(level, new List<string> { "lv_bu" });
            BattleSession session = flow.CurrentSession;
            flow.Tick(10);
            flow.CompleteBattle(session);
            Require(session.ElapsedSeconds == 0 && storage.SaveCount == 0, "备战或提前结算写入最快纪录");
            session.Start();
            session.SetSpeed(2);
            flow.Tick(0.1f);
            flow.PauseBattle();
            double pausedTime = session.ElapsedSeconds;
            session.SetSpeed(1); session.SetSpeed(3);
            flow.Tick(20);
            Require(session.ElapsedSeconds == pausedTime && session.SpeedMask == 2 && storage.SaveCount == 0,
                "暂停计时、空切倍率或未完成战斗写入纪录");
            flow.HandleBack();
            session.SetSpeed(2);
            for (int step = 0; step < 400 && session.State == BattleState.Running; step++) flow.Tick(0.05f);
            Require(session.State == BattleState.Victory && session.BossBreachCount == 1 && session.CastleHealth > 0,
                "Boss突破后城防仍存的合法胜利被拒绝");
            FastestRecord record = progress.GetFastestRecord(level.Id, session.RuleVersion);
            if (session.RuleVersion != GameProgress.FastestRuleVersion)
                Require(progress.GetFastestRecord(level.Id) == null, "新版本普通胜利写入了旧最快键");
            Require(record != null && record.SpeedMask == 2 && record.BossBreachCount == 1 &&
                record.LeakedCount == session.LeakedCount && record.CastleHp == session.CastleHealth &&
                record.ElapsedMilliseconds == (long)Math.Round(session.ElapsedSeconds * 1000, MidpointRounding.AwayFromZero) && storage.SaveCount == 1,
                "真实流程未保存有效实秒或完整胜利快照");
            int writes = storage.WriteCount;
            flow.CompleteBattle(session); flow.Tick(10);
            Require(storage.SaveCount == 1 && storage.WriteCount == writes, "重复终局重新写入最快纪录");
            flow.BeginBattle(level, new List<string> { "lv_bu" });
            flow.CompleteBattle(session);
            Require(storage.SaveCount == 1 && storage.WriteCount == writes && flow.CurrentSession.State == BattleState.Preparation,
                "旧会话污染新一局通关纪录");
            flow.CurrentSession.Start();
            flow.PauseBattle();
            bool hasBest = false;
            foreach (UnityEngine.UI.Text label in root.GetComponentsInChildren<UnityEngine.UI.Text>())
                if (label.text.Contains("本关个人最佳") && label.text.Contains(record.SpeedSummary)) hasBest = true;
            Require(hasBest, "暂停页未显示本关个人最佳与实际倍率摘要");
        }
        Debug.Log("最快纪录验证通过：内存存储、旧档兼容、非法/损坏拒绝、严格比较、快照、重复终局、旧会话与Boss容错胜利");
    }
#endif

    private sealed class MemoryStorage
    {
        public readonly Dictionary<string, int> Values;
        public int SaveCount;
        public int WriteCount;

        public MemoryStorage() { Values = new Dictionary<string, int>(); }
        public MemoryStorage(Dictionary<string, int> values) { Values = new Dictionary<string, int>(values); }

        public GameProgress Open()
        {
            return new GameProgress(
                (key, fallback) => { int value; return Values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => { Values[key] = value; WriteCount++; }, () => SaveCount++);
        }
    }

    private static void Require(bool condition, string message)
    {
        assertionCount++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
