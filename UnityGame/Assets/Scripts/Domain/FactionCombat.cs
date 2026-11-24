using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    // 只冻结军势资格；原成长数值和冷却仍在原来的时点读取。
    internal sealed class FactionCastContext
    {
        internal int SequenceId, Tier;
        internal string Faction;
        internal bool Isolated;
    }

    public sealed partial class BattleSession
    {
        public bool FactionEnabled { get; private set; }
        public string RuleVersion { get { return ResolveRuleVersion(level, FactionEnabled); } }
        public static string ResolveRuleVersion(LevelDefinition definition, bool factionEnabled)
        {
            return definition != null && !string.IsNullOrEmpty(definition.CombatRuleVersion) ? definition.CombatRuleVersion + (factionEnabled ? "-army" : "-solo") : factionEnabled ? "faction-army-v1" : "growth-tempo-v2";
        }
        public int GetFactionCount(string faction) { return FactionRules.CountDeployed(Heroes, faction); }
        public int GetFactionTier(string faction) { return FactionRules.GetTier(GetFactionCount(faction)); }
        public bool IsFactionIsolated(HeroUnit hero) { return hero != null && Heroes.Contains(hero) && FactionRules.IsIsolated(hero, Heroes); }
        private int nextAttackSequenceId;
        private readonly Dictionary<HeroUnit, FactionCastContext> skillContexts = new Dictionary<HeroUnit, FactionCastContext>();
        private FactionCastContext Snapshot(HeroUnit hero)
        {
            return new FactionCastContext { SequenceId = ++nextAttackSequenceId, Faction = hero.Definition.Faction,
                Tier = FactionEnabled ? GetFactionTier(hero.Definition.Faction) : 0, Isolated = FactionEnabled && IsFactionIsolated(hero) };
        }
        private FactionCastContext SkillContext(HeroUnit hero)
        {
            FactionCastContext context;
            return skillContexts.TryGetValue(hero, out context) ? context : null;
        }
        private FactionCastContext BeginSkill(HeroUnit hero, EnemyUnit target, bool release)
        {
            FactionCastContext context = Snapshot(hero); skillContexts[hero] = context;
            if (release) EmitSkillCast(hero, target, context);
            return context;
        }
        private void EmitSkillCast(HeroUnit hero, EnemyUnit target, FactionCastContext context)
        {
            BattleEvent cast = Emit("skill_cast", hero.Definition.Id, hero.Position, target.Position, 0, "特色施放", target.Id);
            Stamp(cast, hero, context, FactionDamageKind.SkillDirect, target);
            if (Diagnostics != null && (hero.Definition.Id == "lv_bu" || hero.Definition.Id == "zhou_yu" || hero.Definition.Id == "zhuge_liang"))
                RecordDiagnostic(CombatDiagnosticKind.Cast, DiagnosticSource(hero, context, CombatDamageKind.SkillDirect, 0, CombatTriggerKind.SkillCast), target, target.Health, target.Health, 0, 0);
        }
        private float FactionMultiplier(FactionCastContext context, FactionDamageKind kind, EnemyUnit target)
        {
            return context == null ? 1 : FactionRules.GetMultiplier(context.Faction, context.Tier, kind, target != null && FactionRules.IsStrongTarget(target.Definition), context.Isolated);
        }
        private void Stamp(BattleEvent item, HeroUnit hero, FactionCastContext context, FactionDamageKind kind, EnemyUnit target)
        {
            item.HeroLevel = hero.Level;
            if (context == null) return;
            item.AttackSequenceId = context.SequenceId; item.FactionTier = context.Tier;
            item.FactionMultiplier = FactionMultiplier(context, kind, target);
        }
        private float PeriodicDamage(HeroUnit hero, float amount)
        { return amount * FactionMultiplier(SkillContext(hero), FactionDamageKind.SkillPeriodic, null); }
        private float DirectDamage(HeroUnit hero, EnemyUnit target, float raw, FactionCastContext context, FactionDamageKind kind)
        {
            float multiplier = FactionMultiplier(context, kind, target);
            CombatDiagnosticSource source = DiagnosticSource(hero, context, kind == FactionDamageKind.Basic ? CombatDamageKind.Basic : CombatDamageKind.SkillDirect, 0, kind == FactionDamageKind.Basic ? CombatTriggerKind.BasicAttack : CombatTriggerKind.SkillCast);
            float dealt = Damage(hero, target, raw * multiplier, true, source);
            if (multiplier > 1 && dealt > 0)
            {
                BattleEvent hit = Emit("faction_hit", hero.Definition.Id, hero.Position, target.Position, dealt, "军势增强", target.Id);
                Stamp(hit, hero, context, kind, target);
            }
            return dealt;
        }
    }
}
