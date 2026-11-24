using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public enum BattleState { Preparation, Running, Paused, Victory, Defeat }

    public sealed class BreachRecord
    {
        public string EnemyName, RouteId;
        public float Health, MaxHealth;
        public bool IsBoss;
        public int Wave, Gold;
        public float Speed;
        public double ElapsedSeconds;

        internal BreachRecord Copy() { return (BreachRecord)MemberwiseClone(); }
    }

    public sealed class HeroUnit
    {
        public HeroDefinition Definition;
        public int SlotId;
        public int Level = 1;
        public Point2 Position;
        internal float NextAttackAt, ChargeUntil, NextBasicAt;
        internal int LastTargetId = -1, FocusStacks, AttackCount, BenefitUsed;
        internal float AssistReadyAt;
        internal int WaveBenefitUsed, WaveSupplyPaid, WaveSupplyBudget;
        internal bool LeakedHealUsed, AdaptiveBow, AdaptiveRelay;
        internal readonly HashSet<int> Ambushed = new HashSet<int>();
        internal readonly HashSet<int> Backstabbed = new HashSet<int>();
    }

    public sealed class EnemyUnit
    {
        public int Id;
        public EnemyDefinition Definition;
        public Point2 Position;
        public float Health, MaxHealth, Progress, SlowUntil, StunUntil, WarningUntil;
        public bool Enraged;
        internal RouteDefinition Route;
        internal float RouteLength, Travelled, MoveSpeed, NextAbilityAt, RecoverUntil, RecoverStartsAt, HasteUntil;
        internal float ControlImmuneUntil, SlowFactor = 1f, VulnerableUntil, MarkAt, MarkDamage;
        internal float BurnUntil, BurnDamage, LinkUntil, DisruptImmuneUntil;
        internal int LinkId = -1;
        internal bool FireSpreads;
        internal HeroUnit BurnOwner, MarkOwner;
        internal FireSpreadBudget BurnBudget;
        internal int BurnGeneration;
        internal float BurnSpreadAt;
        internal bool BurnSpreadUsed, MarkCanRenew, VulnerableCanSpread;
        internal HeroUnit VulnerableOwner, DisruptOwner;
        internal float RecoverExtension, AbilityDelay;
        internal bool DisruptCanRelay;
        internal bool BurnFromArea;
        internal float EmberUntil, EmberDamage;
        internal HeroUnit EmberOwner;
        internal CombatDiagnosticSource EmberDiagnosticSource;
        internal LinkGroup Links;
        internal ReplacementBudget MarkBudget, VulnerableBudget;
        internal FactionCastContext MarkContext;
    }

    internal sealed class FireSpreadBudget
    {
        internal readonly HashSet<int> Visited = new HashSet<int>();
        internal int Maximum, Generations;
    }

    internal sealed class LinkGroup
    {
        internal readonly List<int> Members = new List<int>();
        internal int Id;
        internal float Until;
        internal bool Replaced;
        internal HeroUnit Owner;
    }
    internal sealed class ReplacementBudget { internal int Remaining; }

    public sealed class BattleEvent
    {
        public string Kind, HeroId, Text;
        public string TriggerHeroId;
        public Point2 Position, Target;
        public float Amount;
        public float OccurredAt, ExpiresAt;
        public int TargetEnemyId;
        public int SourceEnemyId, EffectId, ReplacedEnemyId;
        public int[] RelatedEnemyIds;
        public Point2[] RelatedPositions;
        public int HeroLevel;
        public int AttackSequenceId, FactionTier;
        public float FactionMultiplier = 1;
        public float SkillRange;
    }
}
