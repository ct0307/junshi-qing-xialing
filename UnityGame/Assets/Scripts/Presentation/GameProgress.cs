using System;

namespace YanyiSandpan.Presentation
{
    // 运行时接PlayerPrefs，检查时接内存字典，导航使用同一套记录逻辑。
    public sealed partial class GameProgress
    {
        public const string FastestRuleVersion = "faction-army-v1";
        public const string PreviousFastestRuleVersion = "growth-tempo-v2";
        private readonly Func<string, int, int> readInt;
        private readonly Action<string, int> writeInt;
        private readonly Action save;

        public GameProgress(Func<string, int, int> readInt, Action<string, int> writeInt, Action save)
        {
            if (readInt == null || writeInt == null || save == null) throw new ArgumentNullException("记录存储不能为空");
            this.readInt = readInt; this.writeInt = writeInt; this.save = save;
        }

        public bool IsCleared(string levelId) { return readInt("yanyi.clear." + levelId, 0) == 1; }
        public int GetBestCastle(string levelId) { return Math.Max(0, readInt("yanyi.best." + levelId, 0)); }

        public void RecordVictory(string levelId, int castleHealth)
        {
            RecordVictory(levelId, castleHealth, 0, 0, 0, 0);
        }

        public FastestRecord GetFastestRecord(string levelId)
        {
            return GetFastestRecord(levelId, FastestRuleVersion);
        }

        public FastestRecord GetFastestRecord(string levelId, string ruleVersion)
        {
            if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(ruleVersion)) return null;
            return ReadRecord(RecordPrefix("fastest", levelId, ruleVersion));
        }

        public FastestRecord GetRecentRecord(string levelId)
        { return GetRecentRecord(levelId, FastestRuleVersion); }

        public FastestRecord GetRecentRecord(string levelId, string ruleVersion)
        {
            if (string.IsNullOrEmpty(levelId) || string.IsNullOrEmpty(ruleVersion)) return null;
            return ReadRecord(RecordPrefix("recent", levelId, ruleVersion));
        }

        private FastestRecord ReadRecord(string prefix)
        {
            int ready, high, low, mask, castle, leaks, bosses, checksum;
            if (!TryRead(prefix + "ready", out ready) || ready != 1 ||
                !TryRead(prefix + "msHigh", out high) || high < 0 ||
                !TryRead(prefix + "msLow", out low) || !TryRead(prefix + "mask", out mask) ||
                !TryRead(prefix + "castle", out castle) || !TryRead(prefix + "leaks", out leaks) ||
                !TryRead(prefix + "bosses", out bosses) || !TryRead(prefix + "checksum", out checksum)) return null;
            long milliseconds = ((long)high << 32) | (uint)low;
            if (milliseconds <= 0 || !FastestRecord.IsValidSnapshot(mask, castle, leaks, bosses)) return null;
            var record = new FastestRecord(milliseconds, mask, castle, leaks, bosses);
            return Checksum(record) == checksum ? record : null;
        }

        public VictoryRecordResult RecordVictory(string levelId, int castleHealth, double elapsedSeconds,
            int speedMask, int leakedCount, int bossBreachCount)
        { return RecordVersionedVictory(levelId, castleHealth, elapsedSeconds, speedMask, leakedCount, bossBreachCount, FastestRuleVersion); }

        public VictoryRecordResult RecordVersionedVictory(string levelId, int castleHealth, double elapsedSeconds,
            int speedMask, int leakedCount, int bossBreachCount, string ruleVersion)
        {
            if (string.IsNullOrEmpty(ruleVersion)) throw new ArgumentException("普通纪录规则版本不能为空");
            FastestRecord previous = GetFastestRecord(levelId, ruleVersion);
            FastestRecord current = string.IsNullOrEmpty(levelId) ? null :
                FastestRecord.FromVictory(elapsedSeconds, speedMask, castleHealth, leakedCount, bossBreachCount);
            bool isNewRecord = current != null && (previous == null || current.ElapsedMilliseconds < previous.ElapsedMilliseconds);
            var result = new VictoryRecordResult(previous, current, isNewRecord);
            if (castleHealth <= 0 || string.IsNullOrEmpty(levelId)) return result;
            bool changed = false;
            if (!IsCleared(levelId)) { writeInt("yanyi.clear." + levelId, 1); changed = true; }
            if (castleHealth > GetBestCastle(levelId)) { writeInt("yanyi.best." + levelId, castleHealth); changed = true; }
            if (isNewRecord) { WriteRecord(RecordPrefix("fastest", levelId, ruleVersion), current); changed = true; }
            // 每次合法胜利都保存最近通关；较慢或相同成绩也不能在离开结算后丢失。
            if (current != null) { WriteRecord(RecordPrefix("recent", levelId, ruleVersion), current); changed = true; }
            if (changed) save();
            return result;
        }

        private static string RecordPrefix(string kind, string levelId, string ruleVersion)
        { return "yanyi." + kind + "." + ruleVersion + "." + levelId + "."; }

        private bool TryRead(string key, out int value)
        {
            value = readInt(key, int.MinValue);
            // 两个不同缺省值区分缺键与合法的int.MinValue低32位。
            return value != int.MinValue || readInt(key, int.MaxValue) == int.MinValue;
        }

        private void WriteRecord(string prefix, FastestRecord record)
        {
            // 写入中断后不读取半份快照；最后才提交完整标记。
            writeInt(prefix + "ready", 0);
            writeInt(prefix + "msHigh", (int)(record.ElapsedMilliseconds >> 32));
            writeInt(prefix + "msLow", unchecked((int)record.ElapsedMilliseconds));
            writeInt(prefix + "mask", record.SpeedMask); writeInt(prefix + "castle", record.CastleHp);
            writeInt(prefix + "leaks", record.LeakedCount); writeInt(prefix + "bosses", record.BossBreachCount);
            writeInt(prefix + "checksum", Checksum(record));
            writeInt(prefix + "ready", 1);
        }

        private static int Checksum(FastestRecord record)
        {
            // 校验只用于识别损坏与不完整数据，不承担防篡改职责。
            unchecked
            {
                int value = 17;
                value = value * 31 + (int)(record.ElapsedMilliseconds >> 32);
                value = value * 31 + (int)record.ElapsedMilliseconds;
                value = value * 31 + record.SpeedMask; value = value * 31 + record.CastleHp;
                value = value * 31 + record.LeakedCount; value = value * 31 + record.BossBreachCount;
                return value;
            }
        }
    }
}
