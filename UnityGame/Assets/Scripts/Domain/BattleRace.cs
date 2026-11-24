using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public sealed partial class BattleSession
    {
        public double RaceElapsedSeconds { get { return clock; } }
        public int ExpectedBossCount { get; private set; }
        public int SpawnedBossCount { get { return spawnedBossIds.Count; } }
        public int DefeatedBossCount { get { return defeatedBossIds.Count; } }
        public bool IsRaceEligible
        {
            get { return State == BattleState.Victory && SpawnedBossCount == ExpectedBossCount && DefeatedBossCount == SpawnedBossCount && BossBreachCount == 0; }
        }
        private readonly HashSet<int> spawnedBossIds = new HashSet<int>();
        private readonly HashSet<int> defeatedBossIds = new HashSet<int>();
        private readonly double[] raceSpeedSeconds = new double[3];
        private readonly List<RaceSpeedChange> raceSpeedChanges = new List<RaceSpeedChange>();
        private readonly List<RaceAction> raceActions = new List<RaceAction>();
        private double raceConsumedSeconds;
        private int raceSpeedMask, lastRaceSpeed;
        private bool raceSummaryTruncated;

        private void RecordRaceTime(double seconds, int speed)
        {
            if (seconds <= 0) return;
            if (lastRaceSpeed != speed)
            {
                if (raceSpeedChanges.Count < 256) raceSpeedChanges.Add(new RaceSpeedChange(speed, raceConsumedSeconds));
                else raceSummaryTruncated = true;
                lastRaceSpeed = speed;
            }
            raceSpeedMask |= 1 << (speed - 1);
            raceSpeedSeconds[speed - 1] += seconds; raceConsumedSeconds += seconds;
        }
        private void RecordRaceAction(string kind, HeroUnit hero)
        {
            if (raceActions.Count < 64) raceActions.Add(new RaceAction(kind, hero.Definition.Id, hero.SlotId, hero.Level, Gold, RaceElapsedSeconds));
            else raceSummaryTruncated = true;
        }
        private void RegisterBossSpawn(EnemyUnit enemy) { if (enemy.Definition.IsBoss) spawnedBossIds.Add(enemy.Id); }
        private void RegisterBossDefeat(EnemyUnit enemy)
        {
            if (enemy.Definition.IsBoss && enemy.Health <= 0 && spawnedBossIds.Contains(enemy.Id)) defeatedBossIds.Add(enemy.Id);
        }
        public RaceSnapshot GetRaceSnapshot()
        {
            string reason = "";
            if (State != BattleState.Victory) reason = "尚未守关成功";
            else if (BossBreachCount > 0) reason = "本局有敌将突破，未满足竞速资格";
            else if (SpawnedBossCount != ExpectedBossCount) reason = "敌将尚未全部出场";
            else if (DefeatedBossCount != SpawnedBossCount) reason = "敌将尚未全部真实击退";
            return new RaceSnapshot(this, level.Id, raceSpeedSeconds, raceSpeedMask, raceSpeedChanges, raceActions, raceSummaryTruncated, reason);
        }
    }
}
