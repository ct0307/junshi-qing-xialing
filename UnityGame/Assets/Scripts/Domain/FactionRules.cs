using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public enum FactionDamageKind { Inherited, Basic, SkillDirect, SkillPeriodic }

    public static class FactionRules
    {
        public const float IsolationDistance = 220f;
        public static bool IsKnown(string faction) { return faction == "wei" || faction == "shu" || faction == "wu" || faction == "qun"; }
        public static int GetTier(int count) { return count >= 4 ? 4 : count >= 2 ? 2 : 0; }
        public static int CountDeployed(IEnumerable<HeroUnit> heroes, string faction)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (heroes != null && IsKnown(faction)) foreach (HeroUnit hero in heroes)
                if (hero != null && hero.Definition != null && hero.Definition.Faction == faction && !string.IsNullOrEmpty(hero.Definition.Id)) ids.Add(hero.Definition.Id);
            return ids.Count;
        }
        public static bool IsStrongTarget(EnemyDefinition enemy) { return enemy != null && (enemy.IsBoss || enemy.Id == "elite"); }
        private static bool Finite(Point2 p) { return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y); }
        public static bool IsIsolated(HeroUnit hero, IEnumerable<HeroUnit> heroes)
        {
            if (hero == null || heroes == null || !Finite(hero.Position)) return false;
            foreach (HeroUnit other in heroes) if (other != null && !ReferenceEquals(other, hero) && (!Finite(other.Position) || Point2.Distance(hero.Position, other.Position) < IsolationDistance)) return false;
            return true;
        }
        // 持续伤害创建时放大；tick、传播与连环继承已结算值。
        public static float GetMultiplier(string faction, int tier, FactionDamageKind kind, bool strongTarget, bool isolated)
        {
            if (tier != 2 && tier != 4) return 1;
            bool direct = kind == FactionDamageKind.Basic || kind == FactionDamageKind.SkillDirect;
            float standard = tier == 4 ? 1.12f : 1.06f;
            if (faction == "wei" && kind == FactionDamageKind.Basic || faction == "shu" && direct && strongTarget || faction == "wu" && (kind == FactionDamageKind.SkillDirect || kind == FactionDamageKind.SkillPeriodic)) return standard;
            return faction == "qun" && direct && isolated ? (tier == 4 ? 1.16f : 1.08f) : 1;
        }
    }
}
