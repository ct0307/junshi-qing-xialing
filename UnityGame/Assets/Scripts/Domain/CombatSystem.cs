using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public sealed partial class BattleSession
    {
        private const float AssistCooldown = 1.5f;
        private static float GetAttackCharge(HeroDefinition definition)
        { return definition.Skill == "cleave" ? 1 : definition.Skill == "knockback" ? .55f : 0; }
        private static float LegacyCooldown(HeroDefinition definition)
        {
            float interval = Math.Max(.1f, definition.AttackInterval);
            if (definition.Skill == "cleave") return interval * 2.7f;
            if (definition.Skill == "stun" || definition.Skill == "disrupt") return Math.Max(interval, 4);
            if (definition.Skill == "knockback" || definition.Skill == "mark") return Math.Max(interval, 2.5f);
            return definition.Skill == "rain" ? interval * 1.8f : interval;
        }
        private static float GetAttackCooldown(HeroDefinition definition)
        { return definition.SkillCooldown > 0 ? definition.SkillCooldown : Math.Max(3, Math.Min(6, LegacyCooldown(definition))); }
        private static float GetBasicCooldown(HeroDefinition definition)
        {
            if (definition.BasicAttackInterval > 0) return definition.BasicAttackInterval;
            string skill = definition.Skill;
            return skill == "cleave" || skill == "stun" || skill == "heal" || skill == "supply" || skill == "buff" ? 1.05f : skill == "twin" || skill == "execute" || skill == "sniper" ? .75f : .9f;
        }
        private static float BasicScale(HeroDefinition definition) { return definition.BasicDamageScale > 0 ? definition.BasicDamageScale : .32f; }
        private static float SkillScale(HeroDefinition definition)
        {
            if (definition.SkillDamageScale > 0) return definition.SkillDamageScale;
            float oldCycle = LegacyCooldown(definition) + GetAttackCharge(definition);
            float newCycle = GetAttackCooldown(definition) + GetAttackCharge(definition);
            // 快普攻与特色技能共用原输出预算，避免直接叠加旧全量技能输出。
            return Math.Max(.35f, newCycle / oldCycle * .72f);
        }
        private sealed class FireArea
        {
            public int Id;
            public Point2 Position;
            public float Until, Damage, Start, End;
            public HeroUnit Owner;
            public RouteDefinition Route;
            public bool Embers;
            public CombatDiagnosticSource DiagnosticSource;
        }
        private sealed class SlowArea
        {
            public int Id;
            public Point2 Position;
            public string RouteId;
            public float Until;
            public HeroUnit Owner;
            public CombatDiagnosticSource DiagnosticSource;
        }
        private sealed class DelayedSkill
        {
            public FactionCastContext Context;
            public string Kind;
            public HeroUnit Owner;
            public float At, Damage, Radius;
            public Point2 Position;
            public readonly HashSet<int> Visited = new HashSet<int>();
        }
        private readonly List<FireArea> fires = new List<FireArea>();
        private readonly List<SlowArea> winds = new List<SlowArea>();
        private readonly List<DelayedSkill> delayedSkills = new List<DelayedSkill>();

        private void AdvanceCombat(float delta)
        {
            AdvanceAreas(delta);
            foreach (EnemyUnit enemy in Enemies.ToArray())
            {
                if (Time < enemy.BurnUntil) Damage(enemy.BurnOwner, enemy, enemy.BurnDamage * delta, false);
                if (Time < enemy.EmberUntil && !enemy.BurnFromArea) Damage(enemy.EmberOwner, enemy, enemy.EmberDamage * delta, false, enemy.EmberDiagnosticSource);
                if (enemy.Health <= 0) continue;
                if (enemy.BurnBudget != null && !enemy.BurnSpreadUsed && Time >= enemy.BurnSpreadAt && Time < enemy.BurnUntil && HeroGrowth.Resolve(enemy.BurnOwner.Definition, enemy.BurnOwner.Level).ActiveFireSpread)
                { enemy.BurnSpreadUsed = true; SpreadBurn(enemy); }
                if (enemy.MarkAt > 0 && Time >= enemy.MarkAt) ExplodeMark(enemy);
            }
            for (int i = delayedSkills.Count - 1; i >= 0; i--)
            {
                DelayedSkill action = delayedSkills[i]; if (Time < action.At) continue;
                delayedSkills.RemoveAt(i);
                if (action.Kind == "rain")
                {
                    FactionCastContext previous = SkillContext(action.Owner); skillContexts[action.Owner] = action.Context;
                    RainRound(action.Owner, action.Damage, false);
                    if (previous == null) skillContexts.Remove(action.Owner); else skillContexts[action.Owner] = previous;
                }
                else foreach (EnemyUnit enemy in Enemies.ToArray()) if (!action.Visited.Contains(enemy.Id) && Point2.Distance(enemy.Position, action.Position) <= action.Radius) ApplyControl(enemy, .6f);
            }
            foreach (HeroUnit hero in Heroes)
            {
                HeroGrowthProfile growth = HeroGrowth.Resolve(hero.Definition, hero.Level);
                string skill = hero.Definition.Skill;
                if (Time + .0001f >= hero.NextBasicAt)
                {
                    List<EnemyUnit> basicTargets = Targets(hero, GetMaximumRange(hero.Definition, hero.Level));
                    if (basicTargets.Count > 0) { BasicShot(hero, basicTargets[0], true); hero.NextBasicAt = Time + GetBasicCooldown(hero.Definition); }
                }
                if (skill == "assist") continue;
                if (skill == "ambush") { AdvanceAmbush(hero, growth); continue; }
                if (Time + .0001f < hero.NextAttackAt) continue;
                float range = skill == "sniper" || skill == "adaptive" && hero.AdaptiveBow ? GetMaximumRange(hero.Definition, hero.Level) : hero.Definition.Range;
                List<EnemyUnit> targets = Targets(hero, range);
                if (skill == "adaptive" && targets.Count == 0 && growth.AdaptiveSwitch)
                {
                    bool previous = hero.AdaptiveBow;
                    List<EnemyUnit> distant = Targets(hero, GetMaximumRange(hero.Definition, hero.Level));
                    if (distant.Count > 0) { hero.AdaptiveBow = Point2.Distance(hero.Position, distant[0].Position) > hero.Definition.Range; targets = distant; hero.AdaptiveRelay = hero.AdaptiveBow != previous; }
                }
                if (targets.Count == 0) continue;
                if (skill == "execute") SortByHealth(targets);
                else if (skill == "vulnerable") targets.Sort(delegate(EnemyUnit a, EnemyUnit b) { return b.Health.CompareTo(a.Health); });
                EnemyUnit target = targets[0]; float damage = Power(hero) * SkillScale(hero.Definition);
                if (GetAttackCharge(hero.Definition) > 0)
                {
                    if (hero.ChargeUntil == 0)
                    {
                        float charge = GetAttackCharge(hero.Definition); hero.ChargeUntil = Time + charge;
                        FactionCastContext charged = BeginSkill(hero, target, false);
                        BattleEvent chargeEvent = Emit("skill_charge", hero.Definition.Id, hero.Position, target.Position, charge, "特色蓄力", target.Id);
                        chargeEvent.ExpiresAt = hero.ChargeUntil; Stamp(chargeEvent, hero, charged, FactionDamageKind.SkillDirect, target);
                        Emit("notice", hero.Definition.Id, hero.Position, target.Position, charge, skill == "cleave" ? "蓄力重斩" : "蓄力枪击", target.Id); continue;
                    }
                    if (Time + .0001f < hero.ChargeUntil) continue;
                    hero.ChargeUntil = 0;
                    EmitSkillCast(hero, target, SkillContext(hero));
                }
                else BeginSkill(hero, target, true);
                hero.NextAttackAt = Time + GetAttackCooldown(hero.Definition); hero.AttackCount++;
                switch (skill)
                {
                    case "focus":
                        hero.FocusStacks = hero.LastTargetId == target.Id ? Math.Min(5, hero.FocusStacks + 1) : Math.Min(growth.FocusRetainedStacks, hero.FocusStacks);
                        hero.LastTargetId = target.Id;
                        float slash = damage * (1 + hero.FocusStacks * .22f);
                        if (hero.Level >= 3 && hero.FocusStacks == 5)
                        {
                            Shot(hero, target, slash * .6f, true);
                            EnemyUnit follow = target.Health > 0 ? target : FirstAlive(Targets(hero, range), null);
                            if (follow != null) Shot(hero, follow, slash * .6f, false);
                        }
                        else Shot(hero, target, slash, true);
                        break;
                    case "sniper":
                        Shot(hero, target, damage * 1.3f, true);
                        if (growth.FollowupOnKill && target.Health <= 0)
                        {
                            EnemyUnit follow = FirstAlive(Targets(hero, GetMaximumRange(hero.Definition, hero.Level)), growth.CrossRoute ? null : target.Route);
                            if (follow != null) Shot(hero, follow, damage * .8f, false);
                        }
                        break;
                    case "duelist":
                        if (targets.Count == 1) { Shot(hero, target, damage * 2, true); if (hero.Level >= 2 && target.Health > 0) ApplyControl(target, .45f); }
                        else foreach (EnemyUnit enemy in targets) if (Point2.Distance(enemy.Position, growth.FullCircle ? hero.Position : target.Position) < growth.AreaRadius) Shot(hero, enemy, damage * .8f, true);
                        break;
                    case "twin":
                        HashSet<int> arrowed = new HashSet<int>();
                        int arrows = hero.Level == 1 ? Math.Min(2, targets.Count) : growth.TargetLimit;
                        for (int n = 0; n < arrows; n++)
                        {
                            EnemyUnit next = FindUnvisited(Targets(hero, range), arrowed);
                            if (next == null && hero.Level >= 2) next = FirstAlive(Targets(hero, range), null);
                            if (next == null) break; arrowed.Add(next.Id); Shot(hero, next, damage, n == 0);
                        }
                        break;
                    case "cleave":
                        foreach (EnemyUnit enemy in targets) if (growth.FullCircle || InCone(hero.Position, target.Position, enemy.Position, hero.Level >= 2 ? 0 : .3f)) Shot(hero, enemy, damage * 3.5f, true);
                        break;
                    case "pierce":
                        HashSet<int> pierced = new HashSet<int>(); bool killed = Pierce(hero, target.Position, targets, growth.LineWidth, damage, pierced);
                        if (growth.SecondDirection && killed)
                        {
                            EnemyUnit next = FindUnvisited(Targets(hero, range), pierced);
                            if (next != null) Pierce(hero, next.Position, Targets(hero, range), growth.LineWidth, damage, pierced);
                        }
                        break;
                    case "fire":
                        Shot(hero, target, damage * .5f, true);
                        int fireId = ++nextEffectId;
                        fires.Add(new FireArea { Id = fireId, DiagnosticSource = DiagnosticSource(hero, SkillContext(hero), CombatDamageKind.FireArea, fireId, CombatTriggerKind.PersistentArea), Position = target.Position, Owner = hero, Damage = PeriodicDamage(hero, damage * .5f), Until = Time + 2.5f, Route = hero.Level >= 2 ? target.Route : null, Start = target.Travelled - 75, End = target.Travelled + 115, Embers = growth.FireEmbers });
                        Emit("fire", hero.Definition.Id, target.Position, target.Position, 2.5f, "火攻持续燃烧", target.Id); break;
                    case "spread_fire":
                        FireSpreadBudget budget = new FireSpreadBudget { Maximum = growth.FireBudget, Generations = growth.FireGenerations };
                        int seeds = 0;
                        foreach (EnemyUnit enemy in targets) if (enemy.BurnUntil <= Time && seeds < growth.FireSeeds) { Ignite(hero, enemy, PeriodicDamage(hero, damage * .5f), budget, 0); seeds++; }
                        if (seeds == 0) Ignite(hero, target, PeriodicDamage(hero, damage * .5f), budget, 0);
                        Shot(hero, target, damage * .6f, true); break;
                    case "rain":
                        RainRound(hero, damage, true);
                        if (growth.Rounds > 1) delayedSkills.Add(new DelayedSkill { Kind = "rain", Owner = hero, At = Time + .6f, Damage = damage, Context = SkillContext(hero) }); break;
                    case "mark":
                        int marks = 0; ReplacementBudget markBudget = new ReplacementBudget { Remaining = growth.RenewMark ? 1 : 0 };
                        foreach (EnemyUnit enemy in targets) if (enemy.MarkAt == 0 && marks < growth.TargetLimit) { PlaceMark(hero, enemy, damage * 2.2f, markBudget); marks++; }
                        Shot(hero, target, damage * .35f, true); break;
                    case "stun":
                        DelayedSkill echo = new DelayedSkill { Kind = "echo", Owner = hero, At = Time + .65f, Position = hero.Position, Radius = growth.OuterControlRadius };
                        foreach (EnemyUnit enemy in Targets(hero, growth.OuterControlRadius))
                        {
                            bool core = Point2.Distance(enemy.Position, hero.Position) <= hero.Definition.Range;
                            ApplyControl(enemy, core ? 1.1f : .45f); echo.Visited.Add(enemy.Id); Shot(hero, enemy, damage * (core ? .6f : .2f), true);
                        }
                        if (growth.DelayedEcho) delayedSkills.Add(echo); break;
                    case "slow":
                        foreach (EnemyUnit enemy in targets) { SlowEnemy(hero, enemy, 1.3f); Shot(hero, enemy, damage * .3f, true); }
                        MakeWinds(hero, growth, targets); break;
                    case "knockback":
                        HashSet<int> pushed = new HashSet<int>(); int pushedCount = 0;
                        foreach (EnemyUnit enemy in targets)
                        {
                            bool primary = enemy == target;
                            bool line = LineDistance(hero.Position, target.Position, enemy.Position) < growth.LineWidth && InCone(hero.Position, target.Position, enemy.Position, 0);
                            bool back = growth.SecondDirection && Point2.Distance(hero.Position, enemy.Position) <= range * .65f && !InCone(hero.Position, target.Position, enemy.Position, 0);
                            if (!primary && (!line || hero.Level == 1) && !back) continue;
                            if (!enemy.Definition.IsBoss && pushedCount >= (hero.Level >= 2 ? 3 : 1)) continue;
                            if (!pushed.Add(enemy.Id)) continue;
                            if (!enemy.Definition.IsBoss) { enemy.Travelled = Math.Max(0, enemy.Travelled - 85); Place(enemy); pushedCount++; }
                            else ApplyControl(enemy, .5f);
                            Shot(hero, enemy, damage * 1.4f, true);
                        }
                        break;
                    case "chain": CastChain(hero, target, damage, growth.ChainLimit, hero.Level >= 3); break;
                    case "buff":
                        Shot(hero, target, damage * .45f, true); Emit("notice", hero.Definition.Id, hero.Position, hero.Position, .18f, "号令增伤");
                        if (growth.BuffExtraAttacks)
                        {
                            int extra = 0; foreach (HeroUnit ally in Beneficiaries(hero)) { EnemyUnit aim = FirstAlive(Targets(ally, GetMaximumRange(ally.Definition, ally.Level)), null); if (aim == null) continue; BasicShot(ally, aim, false); if (++extra == 3) break; }
                        }
                        break;
                    case "vulnerable":
                        ReplacementBudget vulnerableBudget = new ReplacementBudget { Remaining = growth.VulnerableSpread ? 2 : 0 };
                        for (int n = 0; n < Math.Min(growth.TargetLimit, targets.Count); n++) { EnemyUnit enemy = targets[n]; enemy.VulnerableUntil = Math.Max(enemy.VulnerableUntil, Time + 2.5f); enemy.VulnerableOwner = hero; enemy.VulnerableCanSpread = growth.VulnerableSpread; enemy.VulnerableBudget = vulnerableBudget; }
                        Shot(hero, target, damage * .55f, true); break;
                    case "link":
                        LinkGroup group = new LinkGroup { Id = ++nextEffectId, Owner = hero, Until = Time + 3 };
                        for (int n = 0; n < Math.Min(growth.TargetLimit, targets.Count); n++) { group.Members.Add(targets[n].Id); targets[n].Links = group; targets[n].LinkUntil = group.Until; }
                        if (group.Members.Count > 1) { target.LinkId = group.Members[1]; Emit("notice", hero.Definition.Id, target.Position, targets[1].Position, .3f, "连环传伤", targets[1].Id); }
                        EmitLinkLifecycle("link_created", group, target.Position, target.Position, target.Id, 0);
                        Shot(hero, target, damage * .5f, true); break;
                    case "disrupt":
                        foreach (EnemyUnit enemy in targets) Disrupt(hero, enemy, true);
                        Shot(hero, target, damage * .7f, true); break;
                    case "execute":
                        HashSet<int> executed = new HashSet<int>(); EnemyUnit execution = target;
                        for (int n = 0; n < growth.TargetLimit && execution != null; n++)
                        {
                            executed.Add(execution.Id); Shot(hero, execution, damage * (execution.Health < execution.MaxHealth * .3f ? 1.8f : 1), n == 0);
                            if (execution.Health > 0) break;
                            List<EnemyUnit> follow = Targets(hero, growth.CrossRoute ? GetMaximumRange(hero.Definition, hero.Level) : range); SortByHealth(follow);
                            execution = FindUnvisited(follow, executed, growth.CrossRoute ? null : target.Route);
                        }
                        break;
                    case "splash":
                        Point2 front = target.Position; float progress = target.Progress; Shot(hero, target, damage, true);
                        HashSet<int> splashed = new HashSet<int>(); splashed.Add(target.Id); int splashCount = 0;
                        foreach (EnemyUnit enemy in targets) if (enemy.Id != target.Id && enemy.Progress <= progress && Point2.Distance(enemy.Position, front) < growth.SplashRange && InCone(hero.Position, front, enemy.Position, .55f))
                        { if (splashCount >= growth.SecondaryTargetLimit) break; splashed.Add(enemy.Id); Shot(hero, enemy, damage * .65f, false); splashCount++; }
                        if (growth.FollowupOnKill && target.Health <= 0)
                        {
                            int followCount = 0; foreach (EnemyUnit enemy in Targets(hero, range)) if (!splashed.Contains(enemy.Id) && enemy.Progress <= progress && Point2.Distance(enemy.Position, front) < growth.SplashRange * 1.25f) { Shot(hero, enemy, damage * .5f, false); if (++followCount >= 2) break; }
                        }
                        break;
                    case "adaptive":
                        bool switched = hero.AdaptiveRelay;
                        CastAdaptive(hero, targets, damage, hero.AdaptiveBow, true);
                        hero.AdaptiveBow = !hero.AdaptiveBow; hero.AdaptiveRelay = true;
                        if (growth.AdaptiveRelay && switched) CastAdaptive(hero, Targets(hero, hero.AdaptiveBow ? GetMaximumRange(hero.Definition, hero.Level) : hero.Definition.Range), damage * .5f, hero.AdaptiveBow, false);
                        break;
                    case "heal": case "supply": Shot(hero, target, damage * .5f, true); break;
                    default: Shot(hero, target, damage, true); break;
                }
            }
        }
        private void AdvanceAreas(float delta)
        {
            // 同源重叠火带只取最强，余烬在火带之外继续伤害。
            foreach (EnemyUnit enemy in Enemies.ToArray())
            {
                Dictionary<HeroUnit, float> strongest = new Dictionary<HeroUnit, float>();
                int emberDiagnosticId = int.MaxValue;
                foreach (FireArea fire in fires)
                {
                    if (Time >= fire.Until) continue;
                    bool hit = fire.Route == null ? Point2.Distance(enemy.Position, fire.Position) <= 95 : enemy.Route == fire.Route && enemy.Travelled >= fire.Start && enemy.Travelled <= fire.End;
                    if (!hit) continue;
                    float previous; strongest.TryGetValue(fire.Owner, out previous); strongest[fire.Owner] = Math.Max(previous, fire.Damage);
                    if (fire.Embers)
                    {
                        if (Diagnostics != null && (Time >= enemy.EmberUntil || fire.Damage * .6f > enemy.EmberDamage || fire.Damage * .6f == enemy.EmberDamage && fire.Id < emberDiagnosticId))
                        { enemy.EmberDiagnosticSource = fire.DiagnosticSource; enemy.EmberDiagnosticSource.Kind = CombatDamageKind.Ember; enemy.EmberDiagnosticSource.Trigger = CombatTriggerKind.Ember; emberDiagnosticId = fire.Id; }
                        enemy.EmberDamage = Time < enemy.EmberUntil ? Math.Max(enemy.EmberDamage, fire.Damage * .6f) : fire.Damage * .6f;
                        enemy.EmberUntil = Time + .8f; enemy.EmberOwner = fire.Owner;
                    }
                }
                enemy.BurnFromArea = enemy.EmberOwner != null && strongest.ContainsKey(enemy.EmberOwner);
                foreach (KeyValuePair<HeroUnit, float> hit in strongest)
                {
                    CombatDiagnosticSource source = default(CombatDiagnosticSource);
                    if (Diagnostics != null)
                    {
                        int chosenId = int.MaxValue;
                        foreach (FireArea fire in fires)
                        {
                            if (Time >= fire.Until || fire.Owner != hit.Key || fire.Damage != hit.Value || fire.Id >= chosenId) continue;
                            bool inside = fire.Route == null ? Point2.Distance(enemy.Position, fire.Position) <= 95 : enemy.Route == fire.Route && enemy.Travelled >= fire.Start && enemy.Travelled <= fire.End;
                            if (inside) { chosenId = fire.Id; source = fire.DiagnosticSource; }
                        }
                    }
                    Damage(hit.Key, enemy, hit.Value * delta, false, source);
                }
                foreach (SlowArea wind in winds) if (Time < wind.Until && Point2.Distance(wind.Position, enemy.Position) < 105) SlowEnemy(wind.Owner, enemy, .2f, wind.DiagnosticSource);
            }
            fires.RemoveAll(x => Time >= x.Until); winds.RemoveAll(x => Time >= x.Until);
        }
        private void MakeWinds(HeroUnit hero, HeroGrowthProfile growth, List<EnemyUnit> targets)
        {
            if (growth.SlowAreaCount == 0) return;
            winds.RemoveAll(x => x.Owner == hero); HashSet<string> routes = new HashSet<string>(); int made = 0;
            foreach (EnemyUnit enemy in targets) if (routes.Add(enemy.Route.Id)) { AddWind(hero, enemy); if (++made >= growth.SlowAreaCount) break; }
            if (made < growth.SlowAreaCount) foreach (EnemyUnit enemy in targets) { if (winds.Exists(x => x.Owner == hero && Point2.Distance(x.Position, enemy.Position) < 100)) continue; AddWind(hero, enemy); if (++made >= growth.SlowAreaCount) break; }
        }
        private void AddWind(HeroUnit hero, EnemyUnit enemy)
        {
            int id = ++nextEffectId;
            winds.Add(new SlowArea { Id = id, Owner = hero, Position = enemy.Position, RouteId = enemy.Route.Id, Until = Time + 2.5f,
                DiagnosticSource = DiagnosticSource(hero, SkillContext(hero), CombatDamageKind.SlowArea, id, CombatTriggerKind.PersistentArea) });
        }
        private void SlowEnemy(HeroUnit hero, EnemyUnit enemy, float duration)
        { SlowEnemy(hero, enemy, duration, DiagnosticSource(hero, SkillContext(hero), CombatDamageKind.SlowDirect, 0, CombatTriggerKind.SkillCast)); }
        private void SlowEnemy(HeroUnit hero, EnemyUnit enemy, float duration, CombatDiagnosticSource source)
        {
            float factor = enemy.Definition.IsBoss ? .8f : .55f;
            enemy.SlowFactor = Time < enemy.SlowUntil ? Math.Min(enemy.SlowFactor, factor) : factor; enemy.SlowUntil = Math.Max(enemy.SlowUntil, Time + duration);
            if (source.HeroId == "zhuge_liang") RecordDiagnostic(CombatDiagnosticKind.Slow, source, enemy, enemy.Health, enemy.Health, enemy.SlowFactor, enemy.SlowUntil);
            if (duration > 1) Emit("slow", hero.Definition.Id, hero.Position, enemy.Position, duration, "八阵减速", enemy.Id);
        }
        private void AdvanceAmbush(HeroUnit hero, HeroGrowthProfile growth)
        {
            foreach (EnemyUnit enemy in Targets(hero, hero.Definition.Range)) if (hero.Ambushed.Add(enemy.Id)) { BeginSkill(hero, enemy, true); Shot(hero, enemy, Power(hero) * SkillScale(hero.Definition) * 2.1f, true); if (enemy.Health > 0) ApplyControl(enemy, .35f); }
            if (!growth.Backstab) return;
            foreach (EnemyUnit enemy in Enemies.ToArray()) if (hero.Ambushed.Contains(enemy.Id) && !hero.Backstabbed.Contains(enemy.Id) && Point2.Distance(enemy.Position, hero.Position) > hero.Definition.Range)
            {
                hero.Backstabbed.Add(enemy.Id); BeginSkill(hero, enemy, true); Shot(hero, enemy, Power(hero) * .9f, false);
                if (growth.BackstabSplash) { int count = 0; foreach (EnemyUnit other in Enemies.ToArray()) if (other != enemy && Point2.Distance(enemy.Position, other.Position) <= 100) { Shot(hero, other, Power(hero) * .45f, false); if (++count == 2) break; } }
            }
        }
        private void RainRound(HeroUnit hero, float damage, bool triggerAssist)
        {
            HeroGrowthProfile growth = HeroGrowth.Resolve(hero.Definition, hero.Level); List<EnemyUnit> targets = Targets(hero, hero.Definition.Range); HashSet<int> hit = new HashSet<int>(); int centers = 0;
            foreach (EnemyUnit center in targets)
            {
                if (hit.Contains(center.Id)) continue;
                foreach (EnemyUnit enemy in targets) if (Point2.Distance(center.Position, enemy.Position) < 55 && hit.Add(enemy.Id)) Shot(hero, enemy, damage * .55f, triggerAssist && hit.Count == 1);
                if (++centers >= growth.TargetLimit) break;
            }
        }
        private bool Pierce(HeroUnit hero, Point2 aim, List<EnemyUnit> targets, float width, float damage, HashSet<int> visited)
        {
            bool killed = false; foreach (EnemyUnit enemy in targets) if (!visited.Contains(enemy.Id) && LineDistance(hero.Position, aim, enemy.Position) < width && InCone(hero.Position, aim, enemy.Position, 0)) { visited.Add(enemy.Id); Shot(hero, enemy, damage, visited.Count == 1); if (enemy.Health <= 0) killed = true; } return killed;
        }
        private void CastChain(HeroUnit hero, EnemyUnit target, float damage, int limit, bool branches)
        {
            Queue<EnemyUnit> frontier = new Queue<EnemyUnit>(); frontier.Enqueue(target); HashSet<int> visited = new HashSet<int>(); visited.Add(target.Id); int hits = 0;
            while (frontier.Count > 0 && hits < limit)
            {
                EnemyUnit current = frontier.Dequeue(); ApplyControl(current, .2f); Shot(hero, current, damage * Math.Max(.4f, 1 - hits * .1f), hits == 0); hits++;
                int added = 0; List<EnemyUnit> nearby = new List<EnemyUnit>(Enemies); nearby.Sort(delegate(EnemyUnit a, EnemyUnit b) { return Point2.Distance(a.Position, current.Position).CompareTo(Point2.Distance(b.Position, current.Position)); });
                foreach (EnemyUnit next in nearby) if (visited.Count < limit && Point2.Distance(next.Position, current.Position) < 180 && visited.Add(next.Id)) { frontier.Enqueue(next); if (++added >= (branches ? 2 : 1)) break; }
            }
        }
        private void CastAdaptive(HeroUnit hero, List<EnemyUnit> targets, float damage, bool bow, bool triggerAssist)
        {
            if (bow) { if (targets.Count > 0) Shot(hero, targets[0], damage * 1.25f, triggerAssist); }
            else foreach (EnemyUnit enemy in targets) if (Point2.Distance(enemy.Position, hero.Position) < hero.Definition.Range) { Shot(hero, enemy, damage * 1.1f, triggerAssist); triggerAssist = false; }
        }
        private List<EnemyUnit> Targets(HeroUnit hero, float range)
        {
            List<EnemyUnit> result = Enemies.FindAll(x => x.Health > 0 && Point2.Distance(hero.Position, x.Position) <= range);
            result.Sort(delegate(EnemyUnit a, EnemyUnit b) { int order = (a.RouteLength - a.Travelled).CompareTo(b.RouteLength - b.Travelled); return order == 0 ? a.Id.CompareTo(b.Id) : order; }); return result;
        }
        private static void SortByHealth(List<EnemyUnit> targets) { targets.Sort(delegate(EnemyUnit a, EnemyUnit b) { return (a.Health / a.MaxHealth).CompareTo(b.Health / b.MaxHealth); }); }
        private static EnemyUnit FirstAlive(List<EnemyUnit> enemies, RouteDefinition route) { return enemies.Find(x => x.Health > 0 && (route == null || x.Route == route)); }
        private static EnemyUnit FindUnvisited(List<EnemyUnit> enemies, HashSet<int> visited) { return FindUnvisited(enemies, visited, null); }
        private static EnemyUnit FindUnvisited(List<EnemyUnit> enemies, HashSet<int> visited, RouteDefinition route) { return enemies.Find(x => x.Health > 0 && !visited.Contains(x.Id) && (route == null || x.Route == route)); }
        private List<HeroUnit> Beneficiaries(HeroUnit commander)
        {
            List<HeroUnit> result = Heroes.FindAll(x => x != commander && Point2.Distance(x.Position, commander.Position) <= commander.Definition.Range);
            if (HeroGrowth.Resolve(commander.Definition, commander.Level).BuffOutside)
            {
                HeroUnit outside = null; float nearest = float.MaxValue; foreach (HeroUnit ally in Heroes) { float distance = Point2.Distance(ally.Position, commander.Position); if (ally != commander && !result.Contains(ally) && distance < nearest) { nearest = distance; outside = ally; } }
                if (outside != null) result.Add(outside);
            }
            return result;
        }
        private float Power(HeroUnit hero)
        {
            float multiplier = 1;
            foreach (HeroUnit other in Heroes) if (other != hero && other.Definition.Skill == "buff" && Beneficiaries(other).Contains(hero)) multiplier = Math.Max(multiplier, 1.18f + (other.Level - 1) * .03f);
            return BaseDamage(hero, hero.Level) * multiplier;
        }
        private static float BaseDamage(HeroUnit hero, int heroLevel) { return hero.Definition.Damage * HeroGrowth.Resolve(hero.Definition, heroLevel).DamageMultiplier; }
        private void BasicShot(HeroUnit hero, EnemyUnit enemy, bool triggerAssist)
        {
            if (enemy == null || enemy.Health <= 0) return;
            float damage = Power(hero) * BasicScale(hero.Definition);
            FactionCastContext context = Snapshot(hero);
            BattleEvent basic = Emit("basic_attack", hero.Definition.Id, hero.Position, enemy.Position, damage * FactionMultiplier(context, FactionDamageKind.Basic, enemy), triggerAssist ? "普通攻击" : "号令追加普攻", enemy.Id);
            Stamp(basic, hero, context, FactionDamageKind.Basic, enemy);
            DirectDamage(hero, enemy, damage, context, FactionDamageKind.Basic); if (triggerAssist) AssistShot(hero, enemy);
        }
        private void Shot(HeroUnit hero, EnemyUnit enemy, float amount, bool triggerAssist)
        {
            if (enemy == null || enemy.Health <= 0) return;
            FactionCastContext context = SkillContext(hero);
            BattleEvent attack = Emit("attack", hero.Definition.Id, hero.Position, enemy.Position, amount * FactionMultiplier(context, FactionDamageKind.SkillDirect, enemy), hero.Definition.Name + "特色发动", enemy.Id);
            Stamp(attack, hero, context, FactionDamageKind.SkillDirect, enemy);
            DirectDamage(hero, enemy, amount, context, FactionDamageKind.SkillDirect); if (triggerAssist) AssistShot(hero, enemy);
        }
        private void AssistShot(HeroUnit source, EnemyUnit original)
        {
            foreach (HeroUnit assist in Heroes)
            {
                if (assist == source || assist.Definition.Skill != "assist" || Time < assist.AssistReadyAt || Point2.Distance(assist.Position, source.Position) > assist.Definition.Range) continue;
                HeroGrowthProfile growth = HeroGrowth.Resolve(assist.Definition, assist.Level);
                EnemyUnit target = original.Health > 0 ? original : growth.AssistRetarget ? FirstAlive(Targets(assist, GetMaximumRange(assist.Definition, assist.Level)), null) : null;
                if (target == null) continue;
                FactionCastContext context = BeginSkill(assist, target, true);
                assist.AssistReadyAt = Time + AssistCooldown; HashSet<int> shot = new HashSet<int>();
                for (int n = 0; n < growth.TargetLimit && target != null; n++)
                {
                    shot.Add(target.Id); float damage = Power(assist) * .8f;
                    BattleEvent arrow = Emit("attack", assist.Definition.Id, assist.Position, target.Position, damage * FactionMultiplier(context, FactionDamageKind.SkillDirect, target), "协同箭击", target.Id); arrow.TriggerHeroId = source.Definition.Id;
                    Stamp(arrow, assist, context, FactionDamageKind.SkillDirect, target); DirectDamage(assist, target, damage, context, FactionDamageKind.SkillDirect);
                    target = FindUnvisited(Targets(assist, GetMaximumRange(assist.Definition, assist.Level)), shot);
                }
            }
        }
        private float Damage(HeroUnit owner, EnemyUnit enemy, float raw, bool canLink)
        { return Damage(owner, enemy, raw, canLink, DiagnosticSource(owner, null, CombatDamageKind.Unattributed, 0, CombatTriggerKind.Unattributed)); }
        private float Damage(HeroUnit owner, EnemyUnit enemy, float raw, bool canLink, CombatDiagnosticSource source)
        {
            if (enemy == null || enemy.Health <= 0 || raw <= 0) return 0;
            float armor = Math.Max(0, Math.Min(.8f, enemy.Definition.Armor));
            if (!IsRecovering(enemy) && (enemy.Definition.Skill == "shield" || enemy.Definition.Skill == "armored" || enemy.Definition.Skill == "guard")) armor = Math.Min(.8f, armor + .15f);
            if (enemy.Definition.IsBoss && enemy.Definition.Skill == "guard" && Enemies.Exists(x => x != enemy && !x.Definition.IsBoss && x.Health > 0 && Point2.Distance(x.Position, enemy.Position) <= 140)) armor = Math.Min(.8f, armor + .15f);
            if (IsRecovering(enemy)) armor *= .4f;
            float amount = raw * (1 - armor) * (Time < enemy.VulnerableUntil ? 1.25f : 1); if (IsRecovering(enemy)) amount *= 1.15f;
            List<EnemyUnit> linked = new List<EnemyUnit>();
            LinkGroup linkGroup = enemy.Links;
            if (canLink && enemy.Links != null && Time < enemy.Links.Until) foreach (int id in enemy.Links.Members) { EnemyUnit other = Enemies.Find(x => x.Id == id && x != enemy); if (other != null) linked.Add(other); }
            else if (canLink && Time < enemy.LinkUntil) { EnemyUnit other = Enemies.Find(x => x.Id == enemy.LinkId); if (other != null) linked.Add(other); }
            float healthBefore = enemy.Health;
            enemy.Health = Math.Max(0, enemy.Health - amount);
            RecordDiagnostic(CombatDiagnosticKind.Damage, source, enemy, healthBefore, enemy.Health, 0, 0);
            if (enemy.Health <= 0) RecordDiagnostic(CombatDiagnosticKind.Kill, source, enemy, healthBefore, enemy.Health, 0, 0);
            if (raw >= .8f) Emit("hit", owner == null ? null : owner.Definition.Id, enemy.Position, enemy.Position, amount, "命中", enemy.Id);
            if (enemy.Health <= 0) Kill(enemy);
            foreach (EnemyUnit other in linked)
            {
                // 返回值沿用接收者原减伤、易伤与过杀口径；语义事件不再触发结算。
                CombatDiagnosticSource transferSource = default(CombatDiagnosticSource);
                if (Diagnostics != null) transferSource = new CombatDiagnosticSource { Kind = CombatDamageKind.LinkTransfer, Trigger = CombatTriggerKind.LinkTransfer, TriggerHeroId = owner == null ? null : owner.Definition.Id, SourceEnemyId = enemy.Id, EffectId = linkGroup == null ? 0 : linkGroup.Id };
                float received = Damage(owner, other, amount * .3f, false, transferSource);
                if (received <= 0) continue;
                BattleEvent transfer = Emit("link_hit", linkGroup == null || linkGroup.Owner == null ? null : linkGroup.Owner.Definition.Id, enemy.Position, other.Position, received, "真实连环传伤", other.Id);
                transfer.TriggerHeroId = owner == null ? null : owner.Definition.Id; transfer.SourceEnemyId = enemy.Id;
                transfer.EffectId = linkGroup == null ? 0 : linkGroup.Id; transfer.ExpiresAt = linkGroup == null ? enemy.LinkUntil : linkGroup.Until;
            }
            return amount;
        }
        private void Kill(EnemyUnit enemy)
        {
            if (!Enemies.Remove(enemy)) return;
            RegisterBossDefeat(enemy);
            Gold += enemy.Definition.GoldReward; Emit("kill", null, enemy.Position, enemy.Position, enemy.Definition.GoldReward, enemy.Definition.Name + "被击退", enemy.Id);
            if (enemy.MarkAt > 0) ExplodeMark(enemy);
            if (enemy.BurnBudget != null && enemy.BurnUntil > Time) SpreadBurn(enemy);
            if (enemy.VulnerableCanSpread && Time < enemy.VulnerableUntil && enemy.VulnerableOwner != null)
            {
                ReplacementBudget budget = enemy.VulnerableBudget;
                foreach (EnemyUnit other in Enemies.ToArray()) if (budget != null && budget.Remaining > 0 && other.VulnerableUntil <= Time && Point2.Distance(other.Position, enemy.Position) < 140) { other.VulnerableUntil = enemy.VulnerableUntil; other.VulnerableOwner = enemy.VulnerableOwner; other.VulnerableCanSpread = false; budget.Remaining--; }
            }
            LinkGroup group = enemy.Links;
            if (group != null && Time < group.Until && !group.Replaced && HeroGrowth.Resolve(group.Owner.Definition, group.Owner.Level).ReplaceLink)
            {
                group.Replaced = true;
                EnemyUnit replacement = Enemies.Find(x => !group.Members.Contains(x.Id) && Point2.Distance(x.Position, enemy.Position) <= 180);
                if (replacement != null) { group.Members.Remove(enemy.Id); group.Members.Add(replacement.Id); replacement.Links = group; replacement.LinkUntil = group.Until; Emit("notice", group.Owner.Definition.Id, enemy.Position, replacement.Position, group.Until - Time, "连环补入", replacement.Id); EmitLinkLifecycle("link_replaced", group, enemy.Position, replacement.Position, replacement.Id, enemy.Id); }
            }
        }
        private void EmitLinkLifecycle(string kind, LinkGroup group, Point2 position, Point2 target, int targetEnemyId, int replacedEnemyId)
        {
            BattleEvent lifecycle = Emit(kind, group.Owner.Definition.Id, position, target, group.Until - Time, kind == "link_created" ? "连环建立" : "连环节点补入", targetEnemyId);
            lifecycle.EffectId = group.Id; lifecycle.ExpiresAt = group.Until; lifecycle.ReplacedEnemyId = replacedEnemyId;
            List<int> memberIds = new List<int>(); List<Point2> positions = new List<Point2>();
            // 嵌套击杀可能先移除其他成员；仅复制当时存活且位置可信的节点，不改玩法集合。
            foreach (int memberId in group.Members)
            {
                EnemyUnit member = Enemies.Find(x => x.Id == memberId && x.Health > 0);
                if (member == null) continue;
                memberIds.Add(member.Id); positions.Add(member.Position);
            }
            lifecycle.RelatedEnemyIds = memberIds.ToArray(); lifecycle.RelatedPositions = positions.ToArray();
        }
        private void Ignite(HeroUnit owner, EnemyUnit enemy, float damage, FireSpreadBudget budget, int generation)
        {
            if (enemy.Health <= 0 || budget.Visited.Count >= budget.Maximum || !budget.Visited.Add(enemy.Id)) return;
            if (enemy.BurnUntil > Time && enemy.BurnDamage > damage) return;
            enemy.BurnUntil = Time + 3; enemy.BurnDamage = damage; enemy.BurnOwner = owner; enemy.FireSpreads = true; enemy.BurnBudget = budget; enemy.BurnGeneration = generation; enemy.BurnSpreadAt = Time + 1.5f; enemy.BurnSpreadUsed = false;
            Emit("fire", owner.Definition.Id, owner.Position, enemy.Position, 3, "火种点燃", enemy.Id);
        }
        private void SpreadBurn(EnemyUnit source)
        {
            FireSpreadBudget budget = source.BurnBudget;
            if (budget == null || source.BurnGeneration >= budget.Generations) return;
            foreach (EnemyUnit other in Enemies.ToArray()) if (budget.Visited.Count < budget.Maximum && other.BurnUntil <= Time && Point2.Distance(other.Position, source.Position) < 140) Ignite(source.BurnOwner, other, source.BurnDamage, budget, source.BurnGeneration + 1);
        }
        private void PlaceMark(HeroUnit owner, EnemyUnit enemy, float damage, ReplacementBudget budget)
        { enemy.MarkAt = Time + 1.8f; enemy.MarkDamage = damage; enemy.MarkOwner = owner; enemy.MarkBudget = budget; enemy.MarkCanRenew = budget != null && budget.Remaining > 0; enemy.MarkContext = SkillContext(owner); }
        private void ExplodeMark(EnemyUnit enemy)
        {
            float damage = enemy.MarkDamage; HeroUnit owner = enemy.MarkOwner; bool renew = enemy.MarkCanRenew; enemy.MarkAt = 0; enemy.MarkDamage = 0; enemy.MarkCanRenew = false;
            if (owner == null) return;
            Emit("fire", owner.Definition.Id, enemy.Position, enemy.Position, damage, "奇谋印记引爆", enemy.Id); bool killed = enemy.Health <= 0;
            foreach (EnemyUnit other in Enemies.ToArray()) if (Point2.Distance(enemy.Position, other.Position) < 110) { DirectDamage(owner, other, damage, enemy.MarkContext, FactionDamageKind.SkillDirect); if (other.Health <= 0) killed = true; }
            if (renew && killed && enemy.MarkBudget != null && enemy.MarkBudget.Remaining > 0) { EnemyUnit next = Enemies.Find(x => x.MarkAt == 0 && Point2.Distance(x.Position, enemy.Position) < 160); if (next != null) { enemy.MarkBudget.Remaining--; PlaceMark(owner, next, damage, null); next.MarkContext = enemy.MarkContext; } }
        }
        private void Disrupt(HeroUnit owner, EnemyUnit enemy, bool relay)
        {
            if (Time < enemy.DisruptImmuneUntil || (!enemy.Definition.IsBoss && enemy.Definition.Skill != "healer")) return;
            float cap = enemy.Definition.IsBoss ? 1.4f : 1.5f, requested = enemy.Definition.IsBoss ? .7f : 1.5f;
            float delay = Math.Max(0, Math.Min(requested, cap - enemy.AbilityDelay)); if (delay <= 0) return;
            enemy.DisruptImmuneUntil = Time + 3; enemy.AbilityDelay += delay; enemy.NextAbilityAt += delay;
            if (enemy.WarningUntil > Time) enemy.WarningUntil += delay;
            HeroGrowthProfile growth = HeroGrowth.Resolve(owner.Definition, owner.Level);
            if (growth.DisruptRecovery) { enemy.RecoverExtension = .75f; enemy.DisruptOwner = owner; enemy.DisruptCanRelay = relay && growth.DisruptRelay; }
            Emit("notice", owner.Definition.Id, owner.Position, enemy.Position, delay, "干扰敌方技能", enemy.Id);
        }
        private void OnDisruptedCast(EnemyUnit enemy)
        {
            HeroUnit owner = enemy.DisruptOwner; bool relay = enemy.DisruptCanRelay;
            if (enemy.RecoverExtension > 0) { enemy.RecoverStartsAt = Math.Max(Time, enemy.RecoverStartsAt); enemy.RecoverUntil = Math.Max(Time, enemy.RecoverUntil) + enemy.RecoverExtension; enemy.RecoverExtension = 0; }
            enemy.DisruptCanRelay = false; enemy.DisruptOwner = null;
            if (!relay || owner == null) return;
            EnemyUnit next = Enemies.Find(x => x != enemy && x.Health > 0 && (x.Definition.IsBoss || x.Definition.Skill == "healer") && x.WarningUntil > Time && Time >= x.DisruptImmuneUntil);
            if (next != null) Disrupt(owner, next, false);
        }
        private void ApplyControl(EnemyUnit enemy, float duration)
        {
            if (enemy.Health <= 0 || Time < enemy.ControlImmuneUntil) return;
            float actual = enemy.Definition.IsBoss ? Math.Min(.3f, duration * .3f) : duration;
            enemy.StunUntil = Time + actual; enemy.ControlImmuneUntil = Time + actual + (enemy.Definition.IsBoss ? 2.5f : .25f);
            Emit("stun", null, enemy.Position, enemy.Position, actual, enemy.Definition.IsBoss ? "Boss控制减效" : "短暂眩晕", enemy.Id);
        }
        private static bool InCone(Point2 origin, Point2 aim, Point2 point, float cosine)
        {
            float ax = aim.X - origin.X, ay = aim.Y - origin.Y, bx = point.X - origin.X, by = point.Y - origin.Y;
            return ax * bx + ay * by >= cosine * Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
        }
        private static float LineDistance(Point2 origin, Point2 aim, Point2 point)
        {
            float ax = aim.X - origin.X, ay = aim.Y - origin.Y;
            return (float)Math.Abs(ax * (point.Y - origin.Y) - ay * (point.X - origin.X)) / Math.Max(.001f, Point2.Distance(origin, aim));
        }
    }
}
