using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed class RaceRecordAction
    {
        public string Kind { get; private set; }
        public string HeroId { get; private set; }
        public int SlotId { get; private set; }
        public int Level { get; private set; }
        public int Gold { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        internal RaceRecordAction(string kind, string heroId, int slotId, int level, int gold, long milliseconds)
        { Kind = kind; HeroId = heroId; SlotId = slotId; Level = level; Gold = gold; ElapsedMilliseconds = milliseconds; }
    }

    public sealed class RaceRecordSpeedChange
    {
        public int Speed { get; private set; }
        public long ElapsedMilliseconds { get; private set; }
        internal RaceRecordSpeedChange(int speed, long milliseconds) { Speed = speed; ElapsedMilliseconds = milliseconds; }
    }

    // 独立复制存档摘要，不持有任何可变战斗对象。
    public sealed class RaceRecord
    {
        public long ElapsedMilliseconds { get; private set; }
        public int SpeedMask { get; private set; }
        public int CastleHp { get; private set; }
        public int LeakedCount { get; private set; }
        public int BossCount { get; private set; }
        public string RuleVersion { get; private set; }
        public string LevelId { get; private set; }
        public string QualificationVersion { get; private set; }
        public bool SummaryTruncated { get; private set; }
        public ReadOnlyCollection<RaceRecordAction> Actions { get; private set; }
        public ReadOnlyCollection<RaceRecordSpeedChange> SpeedChanges { get; private set; }
        private readonly long[] speedMilliseconds;
        public string SpeedSummary
        {
            get {
                string speeds = ""; int count = 0;
                for (int i = 0; i < 3; i++) if ((SpeedMask & (1 << i)) != 0) { speeds += (count++ == 0 ? "" : "/") + (i + 1); }
                return (count == 1 ? "全程" : "混合") + speeds + "×";
            }
        }
        public long GetSpeedMilliseconds(int speed)
        {
            if (speed < 1 || speed > 3) throw new ArgumentOutOfRangeException("speed", "倍率仅支持1、2、3");
            return speedMilliseconds[speed - 1];
        }
        internal RaceRecord(string levelId, string ruleVersion, long milliseconds, int mask, int castle, int leaks,
            int bosses, long[] durations, IList<RaceRecordAction> actions, IList<RaceRecordSpeedChange> changes, bool truncated)
        {
            LevelId = levelId; RuleVersion = ruleVersion; QualificationVersion = "all-boss-v1";
            ElapsedMilliseconds = milliseconds; SpeedMask = mask; CastleHp = castle; LeakedCount = leaks; BossCount = bosses;
            speedMilliseconds = (long[])durations.Clone(); SummaryTruncated = truncated;
            Actions = new List<RaceRecordAction>(actions).AsReadOnly(); SpeedChanges = new List<RaceRecordSpeedChange>(changes).AsReadOnly();
        }
    }

    public sealed class RaceRecordResult
    {
        public RaceRecord PreviousRecord { get; private set; }
        public RaceRecord CurrentRecord { get; private set; }
        public RaceRecord BestRecord { get; private set; }
        public RaceSnapshot Snapshot { get; private set; }
        public bool IsNewRecord { get; private set; }
        public bool IsFirstRecord { get { return IsNewRecord && PreviousRecord == null; } }
        public bool IsEqualRecord { get { return CurrentRecord != null && PreviousRecord != null && CurrentRecord.ElapsedMilliseconds == PreviousRecord.ElapsedMilliseconds; } }
        public bool HasValidTime { get { return CurrentRecord != null; } }
        public string IneligibilityReason { get; private set; }
        internal RaceRecordResult(RaceRecord previous, RaceRecord current, RaceSnapshot snapshot, bool isNew, string reason)
        { PreviousRecord = previous; CurrentRecord = current; Snapshot = snapshot; IsNewRecord = isNew; BestRecord = isNew ? current : previous; IneligibilityReason = reason ?? ""; }
    }
}
