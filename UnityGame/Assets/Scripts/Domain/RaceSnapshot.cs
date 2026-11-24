using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YanyiSandpan.Domain
{
    public sealed class RaceSpeedChange
    {
        public int Speed { get; private set; }
        public double ElapsedSeconds { get; private set; }
        internal RaceSpeedChange(int speed, double elapsedSeconds) { Speed = speed; ElapsedSeconds = elapsedSeconds; }
    }

    public sealed class RaceAction
    {
        public string Kind { get; private set; }
        public string HeroId { get; private set; }
        public int SlotId { get; private set; }
        public int Level { get; private set; }
        public int Gold { get; private set; }
        public double ElapsedSeconds { get; private set; }
        internal RaceAction(string kind, string heroId, int slotId, int level, int gold, double elapsedSeconds)
        { Kind = kind; HeroId = heroId; SlotId = slotId; Level = level; Gold = gold; ElapsedSeconds = elapsedSeconds; }
    }

    // 所有集合及倍率时长均复制；保存端可安全持有战斗中途的快照。
    public sealed class RaceSnapshot
    {
        public string LevelId { get; private set; }
        public string RuleVersion { get; private set; }
        public string QualificationVersion { get { return "all-boss-v1"; } }
        public double ElapsedSeconds { get; private set; }
        public int SpeedMask { get; private set; }
        public int CastleHealth { get; private set; }
        public int LeakedCount { get; private set; }
        public int ExpectedBossCount { get; private set; }
        public int SpawnedBossCount { get; private set; }
        public int DefeatedBossCount { get; private set; }
        public int BossBreachCount { get; private set; }
        public bool IsEligible { get; private set; }
        public string IneligibilityReason { get; private set; }
        public ReadOnlyCollection<RaceSpeedChange> SpeedChanges { get; private set; }
        public ReadOnlyCollection<RaceAction> Actions { get; private set; }
        public bool SummaryTruncated { get; private set; }
        private readonly double[] speedSeconds;

        internal RaceSnapshot(BattleSession session, string levelId, double[] seconds, int speedMask,
            IList<RaceSpeedChange> changes, IList<RaceAction> actions, bool truncated, string reason)
        {
            LevelId = levelId; RuleVersion = session.RuleVersion; ElapsedSeconds = session.RaceElapsedSeconds;
            SpeedMask = speedMask; CastleHealth = session.CastleHealth; LeakedCount = session.LeakedCount;
            ExpectedBossCount = session.ExpectedBossCount; SpawnedBossCount = session.SpawnedBossCount;
            DefeatedBossCount = session.DefeatedBossCount; BossBreachCount = session.BossBreachCount;
            IsEligible = session.IsRaceEligible; IneligibilityReason = reason; SummaryTruncated = truncated;
            speedSeconds = (double[])seconds.Clone();
            SpeedChanges = new List<RaceSpeedChange>(changes).AsReadOnly(); Actions = new List<RaceAction>(actions).AsReadOnly();
        }
        public double GetSpeedSeconds(int speed)
        {
            if (speed < 1 || speed > 3) throw new ArgumentOutOfRangeException("speed", "倍率仅支持1、2、3");
            return speedSeconds[speed - 1];
        }
    }
}
