using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public struct CombatFieldSnapshot
    {
        public int Id;
        public string Kind, HeroId, RouteId;
        public Point2 Position;
        public float Radius, Start, End, Until;
    }

    public struct EnemyFeedbackSnapshot
    {
        public int EnemyId;
        public bool Stunned, Slowed, Burning, Linked;
        public float StunUntil, SlowUntil, BurnUntil, LinkUntil;
    }

    public struct LinkFeedbackSnapshot
    {
        public int Id;
        public string HeroId;
        public float Until;
        public int[] EnemyIds;
        public Point2[] Positions;
    }

    public sealed partial class BattleSession
    {
        // 调用者复用容器；仅复制值，不消费事件、不推进时钟或修正规则状态。
        public void ReadCombatFeedback(List<CombatFieldSnapshot> fields, List<EnemyFeedbackSnapshot> enemies)
        {
            if (fields == null) throw new ArgumentNullException("fields");
            if (enemies == null) throw new ArgumentNullException("enemies");
            fields.Clear(); enemies.Clear();
            if (State == BattleState.Victory || State == BattleState.Defeat) return;
            foreach (FireArea fire in fires)
                if (Time < fire.Until)
                    fields.Add(new CombatFieldSnapshot { Id = fire.Id, Kind = "fire", HeroId = fire.Owner.Definition.Id, Position = fire.Position, Radius = fire.Route == null ? 95 : 0, RouteId = fire.Route == null ? null : fire.Route.Id, Start = fire.Start, End = fire.End, Until = fire.Until });
            foreach (SlowArea wind in winds)
                if (Time < wind.Until)
                    fields.Add(new CombatFieldSnapshot { Id = wind.Id, Kind = "wind", HeroId = wind.Owner.Definition.Id, Position = wind.Position, Radius = 105, RouteId = wind.RouteId, Until = wind.Until });
            foreach (EnemyUnit enemy in Enemies)
            {
                if (enemy.Health <= 0) continue;
                // 截止仅描述查询时仍生效的来源；离开火区后，下次查询立即重算。
                float burnUntil = Time < enemy.BurnUntil ? enemy.BurnUntil : 0;
                // 火区内只抑制余烬的重复扣血，其独立截止时间仍然有效。
                if (Time < enemy.EmberUntil) burnUntil = Math.Max(burnUntil, enemy.EmberUntil);
                foreach (FireArea fire in fires)
                    if (Time < fire.Until && (fire.Route == null ? Point2.Distance(enemy.Position, fire.Position) <= 95 : enemy.Route == fire.Route && enemy.Travelled >= fire.Start && enemy.Travelled <= fire.End)) burnUntil = Math.Max(burnUntil, fire.Until);
                float stunUntil = Time < enemy.StunUntil ? enemy.StunUntil : 0;
                float slowUntil = Time < enemy.SlowUntil && enemy.SlowFactor < 1 ? enemy.SlowUntil : 0;
                float linkUntil = enemy.Links != null ? (Time < enemy.Links.Until && enemy.Links.Members.Contains(enemy.Id) ? enemy.Links.Until : 0) : (Time < enemy.LinkUntil ? enemy.LinkUntil : 0);
                enemies.Add(new EnemyFeedbackSnapshot {
                    EnemyId = enemy.Id, Stunned = stunUntil > Time, Slowed = slowUntil > Time,
                    Burning = burnUntil > Time, Linked = linkUntil > Time,
                    StunUntil = stunUntil, SlowUntil = slowUntil, BurnUntil = burnUntil, LinkUntil = linkUntil
                });
            }
        }

        // 当前有效组按实例去重，成员和坐标每次复制，不暴露内部组或敌军引用。
        public void ReadLinkFeedback(List<LinkFeedbackSnapshot> links)
        {
            if (links == null) throw new ArgumentNullException("links");
            links.Clear();
            if (State == BattleState.Victory || State == BattleState.Defeat) return;
            HashSet<LinkGroup> seen = new HashSet<LinkGroup>();
            foreach (EnemyUnit enemy in Enemies)
            {
                LinkGroup group = enemy.Links;
                if (enemy.Health <= 0 || group == null || Time >= group.Until || !seen.Add(group)) continue;
                List<int> ids = new List<int>(); List<Point2> positions = new List<Point2>();
                foreach (int memberId in group.Members)
                {
                    EnemyUnit member = Enemies.Find(x => x.Id == memberId && x.Health > 0);
                    if (member == null) continue;
                    ids.Add(member.Id); positions.Add(member.Position);
                }
                if (ids.Count > 0) links.Add(new LinkFeedbackSnapshot { Id = group.Id, HeroId = group.Owner.Definition.Id, Until = group.Until, EnemyIds = ids.ToArray(), Positions = positions.ToArray() });
            }
        }
    }
}
