using System;

namespace YanyiSandpan.Domain
{
    public enum CombatDiagnosticKind { Cast, Damage, Kill, Slow }
    public enum CombatDamageKind { Unattributed, Basic, SkillDirect, FireArea, Ember, SlowDirect, SlowArea, LinkTransfer }
    public enum CombatTriggerKind { Unattributed, BasicAttack, SkillCast, PersistentArea, Ember, LinkTransfer }

    // 所有明细为值类型；字符串仅引用既有角色ID，不逐击创建文本。
    public struct CombatDiagnosticEntry
    {
        public CombatDiagnosticKind Kind;
        public string HeroId, TriggerHeroId;
        public int AttackSequenceId, EffectId, SourceEnemyId, TargetEnemyId;
        public CombatDamageKind DamageKind;
        public CombatTriggerKind TriggerKind;
        public float Time, HealthBefore, HealthAfter, SlowFactor, ExpiresAt;
        public float ActualDamage { get { return Math.Max(0, HealthBefore - HealthAfter); } }
    }
    internal struct CombatDiagnosticSource
    {
        internal string HeroId, TriggerHeroId;
        internal int SequenceId, EffectId, SourceEnemyId;
        internal CombatDamageKind Kind;
        internal CombatTriggerKind Trigger;
    }
    public sealed class CombatDiagnosticsBuffer
    {
        public const int MaximumCapacity = 16384;
        private readonly CombatDiagnosticEntry[] entries;
        public Guid SessionId { get; private set; }
        public int Count { get; private set; }
        public int Capacity { get { return entries.Length; } }
        public bool Truncated { get { return DroppedCount > 0; } }
        public long DroppedCount { get; private set; }
        // 用每项128字节保守预算（含两个托管引用），另含数组与缓冲对象预留。
        public long AllocatedBytes { get { return 256L + Capacity * 128L; } }
        internal CombatDiagnosticsBuffer(int capacity)
        { if (capacity < 1 || capacity > MaximumCapacity) throw new ArgumentOutOfRangeException("capacity"); entries = new CombatDiagnosticEntry[capacity]; SessionId = Guid.NewGuid(); }
        public CombatDiagnosticEntry this[int index]
        { get { if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException("index"); return entries[index]; } }
        internal void Add(CombatDiagnosticEntry item)
        { if (Count == Capacity) { DroppedCount++; return; } entries[Count++] = item; }
        internal CombatDiagnosticEntry[] Export()
        { CombatDiagnosticEntry[] result = new CombatDiagnosticEntry[Count]; Array.Copy(entries, result, Count); return result; }
    }
    public sealed partial class BattleSession
    {
        public CombatDiagnosticsBuffer Diagnostics { get; private set; }
        public void EnableCombatDiagnostics(int capacity = CombatDiagnosticsBuffer.MaximumCapacity)
        {
            if (State != BattleState.Preparation || Diagnostics != null) throw new InvalidOperationException("诊断只可在备战时启用一次");
            Diagnostics = new CombatDiagnosticsBuffer(capacity);
        }
        public CombatDiagnosticEntry[] ExportCombatDiagnostics()
        {
            if (State != BattleState.Victory && State != BattleState.Defeat) throw new InvalidOperationException("仅终局可统一导出诊断");
            return Diagnostics == null ? new CombatDiagnosticEntry[0] : Diagnostics.Export();
        }
        private CombatDiagnosticSource DiagnosticSource(HeroUnit hero, FactionCastContext context, CombatDamageKind kind, int effectId, CombatTriggerKind trigger)
        {
            if (Diagnostics == null) return default(CombatDiagnosticSource);
            string id = hero == null ? null : hero.Definition.Id;
            bool tracked = id == "lv_bu" || id == "zhou_yu" || id == "zhuge_liang";
            return new CombatDiagnosticSource { HeroId = tracked ? id : null, TriggerHeroId = id, SequenceId = tracked && context != null ? context.SequenceId : 0,
                Kind = tracked ? kind : CombatDamageKind.Unattributed, EffectId = effectId, Trigger = tracked ? trigger : CombatTriggerKind.Unattributed };
        }
        private void RecordDiagnostic(CombatDiagnosticKind kind, CombatDiagnosticSource source, EnemyUnit enemy, float before, float after, float factor, float expiry)
        {
            if (Diagnostics == null) return;
            Diagnostics.Add(new CombatDiagnosticEntry { Kind = kind, HeroId = source.HeroId, TriggerHeroId = source.TriggerHeroId,
                AttackSequenceId = source.SequenceId, EffectId = source.EffectId, SourceEnemyId = source.SourceEnemyId, TargetEnemyId = enemy.Id,
                DamageKind = source.Kind, TriggerKind = source.Trigger, Time = Time, HealthBefore = before, HealthAfter = after, SlowFactor = factor, ExpiresAt = expiry });
        }
    }
}
