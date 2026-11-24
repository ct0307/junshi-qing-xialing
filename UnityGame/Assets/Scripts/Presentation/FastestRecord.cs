using System;

namespace YanyiSandpan.Presentation
{
    // 最快与最近通关共用完整快照，时间和守关质量始终来自同一场胜利。
    public sealed class FastestRecord
    {
        public long ElapsedMilliseconds { get; private set; }
        public int SpeedMask { get; private set; }
        public int CastleHp { get; private set; }
        public int LeakedCount { get; private set; }
        public int BossBreachCount { get; private set; }
        public string SpeedSummary
        {
            get
            {
                if (SpeedMask == 1) return "全程1×";
                if (SpeedMask == 2) return "全程2×";
                if (SpeedMask == 4) return "全程3×";
                string speeds = "";
                for (int index = 0; index < 3; index++)
                    if ((SpeedMask & (1 << index)) != 0) speeds += (speeds.Length == 0 ? "" : "/") + (index + 1);
                return speeds.Length == 0 ? "—" : "混合" + speeds + "×";
            }
        }

        internal FastestRecord(long elapsedMilliseconds, int speedMask, int castleHp, int leakedCount, int bossBreachCount)
        {
            ElapsedMilliseconds = elapsedMilliseconds; SpeedMask = speedMask; CastleHp = castleHp;
            LeakedCount = leakedCount; BossBreachCount = bossBreachCount;
        }

        internal static FastestRecord FromVictory(double elapsedSeconds, int speedMask, int castleHp, int leakedCount, int bossBreachCount)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0) return null;
            double milliseconds = Math.Round(elapsedSeconds * 1000, MidpointRounding.AwayFromZero);
            // long最大值转为double会向上舍入，边界必须严格排除后再转换。
            if (milliseconds <= 0 || milliseconds >= (double)long.MaxValue || double.IsInfinity(milliseconds)) return null;
            if (!IsValidSnapshot(speedMask, castleHp, leakedCount, bossBreachCount)) return null;
            return new FastestRecord((long)milliseconds, speedMask, castleHp, leakedCount, bossBreachCount);
        }

        internal static bool IsValidSnapshot(int speedMask, int castleHp, int leakedCount, int bossBreachCount)
        {
            return speedMask > 0 && (speedMask & ~7) == 0 && castleHp > 0 && leakedCount >= 0 &&
                bossBreachCount >= 0 && bossBreachCount <= leakedCount;
        }
    }

    // 先捕获旧纪录，再保存本次，结算页据此判断首次或刷新。
    public sealed class VictoryRecordResult
    {
        public FastestRecord PreviousRecord { get; private set; }
        public FastestRecord CurrentRecord { get; private set; }
        public FastestRecord BestRecord { get; private set; }
        public bool HasValidTime { get { return CurrentRecord != null; } }
        public bool IsFirstRecord { get { return IsNewRecord && PreviousRecord == null; } }
        public bool IsEqualRecord { get { return HasValidTime && PreviousRecord != null &&
            CurrentRecord.ElapsedMilliseconds == PreviousRecord.ElapsedMilliseconds; } }
        public bool IsNewRecord { get; private set; }

        internal VictoryRecordResult(FastestRecord previousRecord, FastestRecord currentRecord, bool isNewRecord)
        {
            PreviousRecord = previousRecord; CurrentRecord = currentRecord; IsNewRecord = isNewRecord;
            BestRecord = isNewRecord ? currentRecord : previousRecord;
        }
    }
}
