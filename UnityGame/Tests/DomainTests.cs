using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

internal static class DomainTests
{
    private static int passed;
    private static int failed;
    private static readonly string[] Skills = { "focus", "sniper", "duelist", "twin", "cleave", "pierce", "fire", "spread_fire", "rain", "mark", "stun", "slow", "knockback", "chain", "ambush", "buff", "vulnerable", "link", "assist", "disrupt", "execute", "splash", "adaptive", "heal", "supply" };
    private static readonly string[] Ids = { "guan_yu", "huang_zhong", "xu_chu", "taishi_ci", "lv_bu", "zhao_yun", "zhou_yu", "lu_xun", "yuan_shao", "guo_jia", "zhang_fei", "zhuge_liang", "ma_chao", "zhang_jiao", "lv_meng", "cao_cao", "diao_chan", "pang_tong", "xun_yu", "sima_yi", "sun_shangxiang", "zhang_liao", "jiang_wei", "hua_tuo", "lu_su" };

    private static int Main(string[] args)
    {
        Run("当前连环快照按组去重并独立复制且补入沿用原时限", delegate {
            BattleSession b = Combat("pang_tong", 5); b.Heroes[0].Level = 3; b.Heroes[0].NextBasicAt = 100; b.Tick(.04f); b.Heroes[0].NextAttackAt = 100;
            LinkGroup group = b.Enemies[0].Links; int gold = b.Gold; float time = b.Time, health = b.Enemies[1].Health;
            System.Collections.IList links = ReadLinks(b); Equal(1, links.Count); object snapshot = links[0]; Check(snapshot.GetType().IsValueType);
            int id = FeedbackValue<int>(snapshot, "Id"); Equal("pang_tong", FeedbackValue<string>(snapshot, "HeroId")); Near(group.Until, FeedbackValue<float>(snapshot, "Until"));
            int[] ids = FeedbackValue<int[]>(snapshot, "EnemyIds"); Point2[] positions = FeedbackValue<Point2[]>(snapshot, "Positions"); Equal(3, ids.Length); Equal(ids.Length, positions.Length); Check(ids.SequenceEqual(group.Members));
            int originalId = ids[0]; Point2 originalPosition = positions[0]; ids[0] = -99; positions[0] = new Point2(-99, -99); links.Add(snapshot);
            ReadLinks(b, links); Equal(1, links.Count); Check(FeedbackValue<int[]>(links[0], "EnemyIds").Contains(originalId)); Equal(originalId, group.Members[0]);
            Near(0, Point2.Distance(originalPosition, FeedbackValue<Point2[]>(links[0], "Positions")[0])); Near(time, b.Time); Near(health, b.Enemies[1].Health); Equal(gold, b.Gold);
            Equal(1, b.DrainEvents().Count(x => x.Kind == "link_created"));
            b.SetPaused(true); b.Tick(10); object paused = ReadLinks(b)[0]; Equal(id, FeedbackValue<int>(paused, "Id")); Near(group.Until, FeedbackValue<float>(paused, "Until")); Near(time, b.Time);
            b.SetPaused(false); b.Enemies[0].Health = .01f; b.Enemies[0].BurnUntil = b.Time + 1; b.Enemies[0].BurnDamage = 100; b.Tick(.04f);
            snapshot = ReadLinks(b)[0]; Equal(id, FeedbackValue<int>(snapshot, "Id")); Near(group.Until, FeedbackValue<float>(snapshot, "Until")); ids = FeedbackValue<int[]>(snapshot, "EnemyIds");
            Equal(3, ids.Length); Check(!ids.Contains(originalId)); Check(ids.SequenceEqual(group.Members));
        });
        Run("连环快照过滤真实死亡成员并在到期和终局清空", delegate {
            BattleSession b = Combat("pang_tong", 3); b.Heroes[0].Level = 2; b.Heroes[0].NextBasicAt = 100; b.Tick(.04f); b.Heroes[0].NextAttackAt = 100;
            int removed = b.Enemies[0].Id; b.Enemies[0].Health = .01f; b.Enemies[0].BurnUntil = b.Time + 1; b.Enemies[0].BurnDamage = 100; b.Tick(.04f);
            System.Collections.IList links = ReadLinks(b); Equal(1, links.Count); int[] ids = FeedbackValue<int[]>(links[0], "EnemyIds"); Equal(2, ids.Length); Check(!ids.Contains(removed));
            Point2[] positions = FeedbackValue<Point2[]>(links[0], "Positions"); for (int i = 0; i < ids.Length; i++) Near(0, Point2.Distance(positions[i], b.Enemies.First(x => x.Id == ids[i]).Position));
            b.Tick(3.1f); ReadLinks(b, links); Equal(0, links.Count);
            b.Heroes[0].NextAttackAt = 0; b.Tick(.04f); ReadLinks(b, links); Equal(1, links.Count);
            foreach (EnemyUnit enemy in b.Enemies) { enemy.Health = .01f; enemy.BurnUntil = b.Time + 1; enemy.BurnDamage = 100; }
            b.Tick(.04f); Equal(BattleState.Victory, b.State); ReadLinks(b, links); Equal(0, links.Count);
        });
        Run("敌人反馈有效截止时间沿用真实实秒且暂停冻结到期归零", delegate {
            BattleSession b = Combat("lu_xun", 1); b.Tick(.04f); b.Heroes[0].NextAttackAt = 100; b.Heroes[0].NextBasicAt = 100; EnemyUnit enemy = b.Enemies[0];
            enemy.StunUntil = b.Time + .3f; enemy.SlowUntil = b.Time + .2f; enemy.SlowFactor = .55f;
            object status = ReadFeedback(b)[1][0]; Near(enemy.StunUntil, FeedbackValue<float>(status, "StunUntil")); Near(enemy.SlowUntil, FeedbackValue<float>(status, "SlowUntil")); Near(enemy.BurnUntil, FeedbackValue<float>(status, "BurnUntil")); Near(0, FeedbackValue<float>(status, "LinkUntil"));
            b.SetPaused(true); b.Tick(20); object paused = ReadFeedback(b)[1][0]; Near(FeedbackValue<float>(status, "BurnUntil"), FeedbackValue<float>(paused, "BurnUntil")); Near(FeedbackValue<float>(status, "StunUntil"), FeedbackValue<float>(paused, "StunUntil"));
            b.SetPaused(false); b.SetSpeed(3); b.Tick(.4f); status = ReadFeedback(b)[1][0]; Near(0, FeedbackValue<float>(status, "StunUntil")); Near(0, FeedbackValue<float>(status, "SlowUntil")); Near(enemy.BurnUntil, FeedbackValue<float>(status, "BurnUntil"));
            b.Tick(2.7f); status = ReadFeedback(b)[1][0]; Near(0, FeedbackValue<float>(status, "BurnUntil")); Equal(false, FeedbackValue<bool>(status, "Burning"));
            b = Combat("pang_tong", 3); b.Heroes[0].Level = 2; b.Tick(.04f); b.Heroes[0].NextAttackAt = 100; b.Heroes[0].NextBasicAt = 100;
            foreach (object linked in ReadFeedback(b)[1]) Near(b.Enemies[0].Links.Until, FeedbackValue<float>(linked, "LinkUntil"));
            b.Tick(3.1f); foreach (object expired in ReadFeedback(b)[1]) Near(0, FeedbackValue<float>(expired, "LinkUntil"));
        });
        Run("燃烧截止取当前火区直接燃烧与余烬有效最大值且离区重查", delegate {
            BattleSession lingering = Combat("zhou_yu", 1); lingering.Heroes[0].Level = 3; lingering.Tick(.04f); lingering.Heroes[0].NextAttackAt = 100; lingering.Heroes[0].NextBasicAt = 100; lingering.Tick(2.1f);
            Check(lingering.Enemies[0].BurnFromArea, "火区内的同源余烬应暂缓重复扣血"); Check(lingering.Enemies[0].EmberUntil > FeedbackValue<float>(ReadFeedback(lingering)[0][0], "Until"));
            Near(lingering.Enemies[0].EmberUntil, FeedbackValue<float>(ReadFeedback(lingering)[1][0], "BurnUntil"));
            BattleSession b = Combat("zhou_yu", 1); b.Heroes[0].Level = 3; b.Tick(.04f); b.Heroes[0].NextAttackAt = 100; b.Heroes[0].NextBasicAt = 100;
            float fireUntil = FeedbackValue<float>(ReadFeedback(b)[0][0], "Until"); Near(fireUntil, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil"));
            b.Tick(.04f); EnemyUnit enemy = b.Enemies[0]; enemy.BurnUntil = b.Time + 3; enemy.BurnDamage = 1;
            Near(enemy.BurnUntil, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil"));
            enemy.Travelled = 500; b.Tick(.04f); Near(enemy.BurnUntil, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil"));
            enemy.BurnUntil = 0; Near(enemy.EmberUntil, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil")); b.Tick(.9f);
            Near(0, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil"));
            enemy.Travelled = 0; enemy.Position = new Point2(0, 0); Near(fireUntil, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil"));
            enemy.Travelled = 500; enemy.Position = new Point2(500, 0); Near(0, FeedbackValue<float>(ReadFeedback(b)[1][0], "BurnUntil"));
        });
        Run("反馈事件使用发生实秒且倍率与暂停不改写历史", delegate {
            BattleSession b = Combat("guan_yu", 1); b.Tick(.2f);
            List<BattleEvent> events = b.DrainEvents();
            Near(0, FeedbackValue<float>(events.First(x => x.Kind == "recruit"), "OccurredAt"));
            BattleEvent first = events.First(x => x.Kind == "basic_attack");
            Near(1f / 30, FeedbackValue<float>(first, "OccurredAt"));
            Check(events.All(x => FeedbackValue<float>(x, "OccurredAt") <= b.Time));
            float frozen = b.Time; b.SetPaused(true); b.Tick(20); Near(frozen, b.Time);
            Near(1f / 30, FeedbackValue<float>(first, "OccurredAt")); Equal(0, b.DrainEvents().Count);
            b.SetPaused(false); b.SetSpeed(3); b.Heroes[0].NextAttackAt = 0; b.Heroes[0].NextBasicAt = 100; b.Tick(.04f);
            BattleEvent next = b.DrainEvents().First(x => x.Kind == "attack");
            Near(b.Time, FeedbackValue<float>(next, "OccurredAt")); Check(b.MarchTime > b.Time);
        });
        Run("协射来源对应友军且首箭允许超出荀彧射界", delegate {
            GameDatabase db = Fixture(); db.Levels[0].Slots[0].Position = new Point2(300, 0); db.Levels[0].Slots[1].Position = new Point2(600, 0); db.FindHero("xun_yu").Range = 350;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "xun_yu" }); b.TryRecruit("guan_yu", 0); b.TryRecruit("xun_yu", 1);
            foreach (HeroUnit hero in b.Heroes) hero.NextAttackAt = 100; b.Heroes[1].NextBasicAt = 100; b.Start(); b.Tick(.04f);
            List<BattleEvent> events = b.DrainEvents(); BattleEvent arrow = events.Single(x => x.Kind == "attack" && x.HeroId == "xun_yu");
            Equal("guan_yu", FeedbackValue<string>(arrow, "TriggerHeroId")); Equal(b.Enemies[0].Id, arrow.TargetEnemyId);
            Check(Point2.Distance(arrow.Position, arrow.Target) > db.FindHero("xun_yu").Range);
            Equal(null, FeedbackValue<string>(events.First(x => x.Kind == "basic_attack"), "TriggerHeroId"));
        });
        Run("改射和双箭各保留同一真实协射触发者且不递归", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                GameDatabase db = Fixture(); db.FindHero("guan_yu").Damage = 100000; db.Levels[0].Waves[0].Groups[0].Count = 5;
                BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "xun_yu" }); b.TryRecruit("guan_yu", 0); b.TryRecruit("xun_yu", 1);
                foreach (HeroUnit hero in b.Heroes) hero.NextAttackAt = 100; b.Heroes[1].NextBasicAt = 100; b.Heroes[1].Level = tier; b.Start(); b.Tick(.04f);
                List<BattleEvent> events = b.DrainEvents(); int killed = events.First(x => x.Kind == "kill").TargetEnemyId;
                List<BattleEvent> arrows = events.Where(x => x.Kind == "attack" && x.HeroId == "xun_yu").ToList(); Equal(tier == 3 ? 2 : 1, arrows.Count);
                Check(arrows.All(x => x.TargetEnemyId != killed && FeedbackValue<string>(x, "TriggerHeroId") == "guan_yu"));
                Equal(arrows.Count, arrows.Select(x => x.TargetEnemyId).Distinct().Count()); Near(b.Time + 1.5f, b.Heroes[1].AssistReadyAt);
            }
        });
        Run("连环建立事件保存三成员位置副本与原到期时间", delegate {
            BattleSession b = Combat("pang_tong", 4); b.Heroes[0].Level = 2; b.Heroes[0].NextBasicAt = 100; b.Tick(.04f);
            List<BattleEvent> events = b.DrainEvents(); BattleEvent created = events.SingleOrDefault(x => x.Kind == "link_created"); Check(created != null, "缺少真实连环建立事件");
            LinkGroup group = b.Enemies[0].Links; int[] ids = FeedbackValue<int[]>(created, "RelatedEnemyIds"); Point2[] positions = FeedbackValue<Point2[]>(created, "RelatedPositions");
            Equal(3, ids.Length); Equal(3, positions.Length); Check(ids.SequenceEqual(group.Members)); Check(FeedbackValue<int>(created, "EffectId") > 0);
            Near(group.Until, FeedbackValue<float>(created, "ExpiresAt")); Near(FeedbackValue<float>(created, "OccurredAt") + 3, group.Until); Equal("pang_tong", created.HeroId);
            for (int i = 0; i < ids.Length; i++) Near(0, Point2.Distance(positions[i], b.Enemies.First(x => x.Id == ids[i]).Position));
            int oldId = ids[0]; Point2 oldPosition = positions[0]; b.Enemies[0].Position = new Point2(800, 300); Near(0, Point2.Distance(oldPosition, positions[0]));
            ids[0] = -99; positions[0] = new Point2(-99, -99); Equal(oldId, group.Members[0]); Check(b.Enemies[0].Position.X == 800);
            Equal(1, events.Count(x => x.Kind == "notice" && x.Text == "连环传伤"));
        });
        Run("连环补入事件沿用组ID与到期且完整成员独立复制", delegate {
            BattleSession b = Combat("pang_tong", 6); b.Heroes[0].Level = 3; b.Heroes[0].NextBasicAt = 100; b.Tick(.04f);
            BattleEvent created = b.DrainEvents().SingleOrDefault(x => x.Kind == "link_created"); Check(created != null, "缺少连环建立事件");
            LinkGroup group = b.Enemies[0].Links; float until = group.Until; int removed = b.Enemies[0].Id;
            b.Enemies[0].Health = .01f; b.Enemies[0].BurnUntil = b.Time + 1; b.Enemies[0].BurnDamage = 100; b.Tick(.04f);
            List<BattleEvent> events = b.DrainEvents(); BattleEvent replaced = events.SingleOrDefault(x => x.Kind == "link_replaced"); Check(replaced != null, "缺少真实补入事件");
            Equal(FeedbackValue<int>(created, "EffectId"), FeedbackValue<int>(replaced, "EffectId")); Equal(removed, FeedbackValue<int>(replaced, "ReplacedEnemyId"));
            Near(until, FeedbackValue<float>(replaced, "ExpiresAt")); Near(until, group.Until); Check(FeedbackValue<float>(replaced, "OccurredAt") > FeedbackValue<float>(created, "OccurredAt"));
            int[] ids = FeedbackValue<int[]>(replaced, "RelatedEnemyIds"); Equal(3, ids.Length); Check(ids.SequenceEqual(group.Members)); Check(!ids.Contains(removed) && ids.Contains(replaced.TargetEnemyId));
            Equal(3, FeedbackValue<Point2[]>(replaced, "RelatedPositions").Length); ids[0] = -99; Check(!group.Members.Contains(-99));
            Equal(1, events.Count(x => x.Kind == "notice" && x.Text == "连环补入"));
            b.Enemies.First(x => group.Members.Contains(x.Id)).Health = .01f; b.Enemies.First(x => group.Members.Contains(x.Id)).BurnUntil = b.Time + 1; b.Enemies.First(x => group.Members.Contains(x.Id)).BurnDamage = 100; b.Tick(.04f);
            Equal(0, b.DrainEvents().Count(x => x.Kind == "link_replaced"));
        });
        Run("连环补入事件仅复制嵌套死亡后仍存活成员的真实位置", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 0; db.Levels[0].Waves[0].Groups[0].Count = 5;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "pang_tong", "guo_jia" }); b.TryRecruit("pang_tong", 0); b.TryRecruit("guo_jia", 1); b.Heroes[0].Level = 3;
            foreach (HeroUnit hero in b.Heroes) hero.NextBasicAt = 100; b.Heroes[1].NextAttackAt = 100; b.Start(); b.Tick(.04f);
            BattleEvent created = b.DrainEvents().Single(x => x.Kind == "link_created"); b.Heroes[0].NextAttackAt = 100;
            foreach (EnemyUnit enemy in b.Enemies) enemy.Travelled = 300;
            EnemyUnit source = b.Enemies[0], nestedVictim = b.Enemies[1]; LinkGroup group = source.Links; float until = group.Until; int gold = b.Gold;
            source.Health = .01f; source.BurnUntil = b.Time + 1; source.BurnDamage = 100; source.BurnOwner = b.Heroes[0]; nestedVictim.Health = 1;
            // 首名成员死亡时的印记先杀死第二成员，由嵌套结算触发补入。
            source.MarkAt = b.Time + 20; source.MarkDamage = 100; source.MarkOwner = b.Heroes[1]; b.Tick(.04f);
            List<BattleEvent> events = b.DrainEvents(); BattleEvent replaced = events.Single(x => x.Kind == "link_replaced");
            int[] ids = FeedbackValue<int[]>(replaced, "RelatedEnemyIds"); Point2[] positions = FeedbackValue<Point2[]>(replaced, "RelatedPositions");
            Check(!ids.Contains(source.Id), "补入副本不得携带已移除成员和默认坐标"); Equal(2, ids.Length); Equal(ids.Length, positions.Length);
            for (int i = 0; i < ids.Length; i++) { EnemyUnit member = b.Enemies.Single(x => x.Id == ids[i]); Near(0, Point2.Distance(member.Position, positions[i])); Near(300, positions[i].X); }
            Equal(nestedVictim.Id, FeedbackValue<int>(replaced, "ReplacedEnemyId")); Check(ids.Contains(replaced.TargetEnemyId));
            Equal(FeedbackValue<int>(created, "EffectId"), FeedbackValue<int>(replaced, "EffectId")); Near(until, FeedbackValue<float>(replaced, "ExpiresAt"));
            Check(group.Members.Contains(source.Id), "反馈过滤不得改写原玩法成员集合"); Equal(3, group.Members.Count); Near(until, group.Until);
            Equal(2, events.Count(x => x.Kind == "kill")); Equal(gold + 4, b.Gold);
        });
        Run("真实连环传伤保留减伤易伤过杀计算口径且只扩散一层", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 0; db.Levels[0].Waves[0].Groups[0].Count = 3;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "pang_tong", "guan_yu" }); b.TryRecruit("pang_tong", 0); b.TryRecruit("guan_yu", 1); b.Heroes[0].Level = 2;
            foreach (HeroUnit hero in b.Heroes) hero.NextBasicAt = 100; b.Heroes[1].NextAttackAt = 100; b.Start(); b.Tick(.04f); b.DrainEvents(); b.Heroes[0].NextAttackAt = 100;
            EnemyUnit source = b.Enemies[0], receiver = b.Enemies[1]; source.Travelled = 100; source.Health = 1; source.Definition = new EnemyDefinition { Id = "source", Name = "原目标", Armor = .2f, GoldReward = 2 }; source.VulnerableUntil = 100;
            receiver.Definition = new EnemyDefinition { Id = "receiver", Name = "接收目标", Armor = .5f }; receiver.VulnerableUntil = 100;
            float before = receiver.Health; b.Heroes[1].Definition.Damage = 100; b.Heroes[1].NextBasicAt = 0; b.Tick(.04f);
            List<BattleEvent> events = b.DrainEvents(); List<BattleEvent> transfers = events.Where(x => x.Kind == "link_hit").ToList(); Equal(2, transfers.Count);
            BattleEvent link = transfers.Single(x => x.TargetEnemyId == receiver.Id); Equal("pang_tong", link.HeroId); Equal("guan_yu", FeedbackValue<string>(link, "TriggerHeroId")); Equal(source.Id, FeedbackValue<int>(link, "SourceEnemyId"));
            float raw = events.Single(x => x.Kind == "basic_attack").Amount; float expected = raw * .8f * 1.25f * .3f * .5f * 1.25f;
            Near(expected, link.Amount); Near(before - expected, receiver.Health, .002f); Check(link.Amount > .3f, "传伤不得按原目标的一点剩余生命封顶");
            Near(0, Point2.Distance(source.Position, link.Position)); Near(0, Point2.Distance(receiver.Position, link.Target)); Equal(3, events.Count(x => x.Kind == "hit"));
            Near(events.Single(x => x.Kind == "hit" && x.TargetEnemyId == receiver.Id).Amount, link.Amount); Check(FeedbackValue<int>(link, "EffectId") > 0);
        });
        Run("区域反馈为值类型并清空复用容器且查询不消费事件", delegate {
            BattleSession b = Combat("zhou_yu", 1); b.Tick(.04f); System.Collections.IList[] feedback = ReadFeedback(b);
            Equal(1, feedback[0].Count); Equal(1, feedback[1].Count); object area = feedback[0][0]; object enemy = feedback[1][0]; Check(area.GetType().IsValueType && enemy.GetType().IsValueType);
            Equal("fire", FeedbackValue<string>(area, "Kind")); Equal("zhou_yu", FeedbackValue<string>(area, "HeroId")); Near(95, FeedbackValue<float>(area, "Radius")); Equal(null, FeedbackValue<string>(area, "RouteId"));
            Near(b.Time + 2.5f, FeedbackValue<float>(area, "Until")); Equal(true, FeedbackValue<bool>(enemy, "Burning"));
            int id = FeedbackValue<int>(area, "Id"), gold = b.Gold; float time = b.Time, health = b.Enemies[0].Health;
            area.GetType().GetField("Id").SetValue(area, -1); feedback[0][0] = area; feedback[0].Add(area); feedback[1].Add(enemy);
            for (int i = 0; i < 3; i++) ReadFeedback(b, feedback);
            Equal(1, feedback[0].Count); Equal(1, feedback[1].Count); Equal(id, FeedbackValue<int>(feedback[0][0], "Id")); Near(time, b.Time); Near(health, b.Enemies[0].Health); Equal(gold, b.Gold);
            Equal(1, b.DrainEvents().Count(x => x.Kind == "fire")); Equal(0, b.DrainEvents().Count);
        });
        Run("风眼真实双区半径到期替换与暂停保持一致", delegate {
            BattleSession b = Combat("zhuge_liang", 3); b.Heroes[0].NextAttackAt = 100; b.Heroes[0].NextBasicAt = 100; b.Tick(.04f);
            b.Enemies[0].Travelled = 0; b.Enemies[1].Travelled = 200; b.Enemies[2].Travelled = 300; b.Heroes[0].Level = 3; b.Heroes[0].NextAttackAt = 0; b.Tick(.04f);
            System.Collections.IList[] first = ReadFeedback(b); Equal(2, first[0].Count); int[] oldIds = first[0].Cast<object>().Select(x => FeedbackValue<int>(x, "Id")).ToArray();
            foreach (object wind in first[0]) { Equal("wind", FeedbackValue<string>(wind, "Kind")); Near(105, FeedbackValue<float>(wind, "Radius")); Near(b.Time + 2.5f, FeedbackValue<float>(wind, "Until")); Equal("road", FeedbackValue<string>(wind, "RouteId")); }
            b.Heroes[0].NextAttackAt = 0; b.Tick(.04f); System.Collections.IList[] replaced = ReadFeedback(b); Equal(2, replaced[0].Count);
            Check(replaced[0].Cast<object>().All(x => FeedbackValue<int>(x, "Id") > oldIds.Max()));
            b.SetPaused(true); b.Tick(10); System.Collections.IList[] paused = ReadFeedback(b); Equal(FeedbackValue<int>(replaced[0][0], "Id"), FeedbackValue<int>(paused[0][0], "Id"));
            b.SetPaused(false); b.Heroes[0].NextAttackAt = 100; b.Tick(2.6f); Equal(0, ReadFeedback(b)[0].Count);
        });
        Run("道路火带与燃烧快照遵循真实区间火种余烬和状态到期", delegate {
            BattleSession b = Combat("zhou_yu", 2); b.Heroes[0].Level = 3; b.Tick(.04f); b.Heroes[0].NextAttackAt = 100; b.Heroes[0].NextBasicAt = 100;
            object field = ReadFeedback(b)[0][0]; Equal("road", FeedbackValue<string>(field, "RouteId")); Near(190, FeedbackValue<float>(field, "End") - FeedbackValue<float>(field, "Start")); Near(0, FeedbackValue<float>(field, "Radius"));
            b.Tick(.04f); EnemyUnit enemy = b.Enemies[0]; enemy.Travelled = 500; b.Tick(.04f); Equal(true, FeedbackValue<bool>(ReadFeedback(b)[1][0], "Burning"));
            b.Tick(.9f); Equal(false, FeedbackValue<bool>(ReadFeedback(b)[1][0], "Burning"));
            enemy.BurnUntil = b.Time + .1f; enemy.BurnDamage = 1; enemy.StunUntil = b.Time + .1f; enemy.SlowUntil = b.Time + .1f; enemy.SlowFactor = .55f;
            object status = ReadFeedback(b)[1][0]; Equal(true, FeedbackValue<bool>(status, "Burning")); Equal(true, FeedbackValue<bool>(status, "Stunned")); Equal(true, FeedbackValue<bool>(status, "Slowed"));
            b.Tick(.2f); status = ReadFeedback(b)[1][0]; Equal(false, FeedbackValue<bool>(status, "Burning")); Equal(false, FeedbackValue<bool>(status, "Stunned")); Equal(false, FeedbackValue<bool>(status, "Slowed"));
        });
        Run("链状态仅随真实成员到期且终局清空所有反馈快照", delegate {
            BattleSession b = Combat("pang_tong", 3); b.Heroes[0].Level = 2; b.Tick(.04f); b.Heroes[0].NextAttackAt = 100; b.Heroes[0].NextBasicAt = 100;
            Check(ReadFeedback(b)[1].Cast<object>().All(x => FeedbackValue<bool>(x, "Linked"))); b.Tick(3.1f); Check(ReadFeedback(b)[1].Cast<object>().All(x => !FeedbackValue<bool>(x, "Linked")));
            b = Combat("zhou_yu", 1); b.Tick(.04f); b.Enemies[0].Health = .01f; b.Enemies[0].BurnUntil = b.Time + 1; b.Enemies[0].BurnDamage = 100; b.Tick(.04f); Equal(BattleState.Victory, b.State);
            System.Collections.IList[] feedback = ReadFeedback(b); Equal(0, feedback[0].Count); Equal(0, feedback[1].Count);
        });
        Run("双时钟倍率只推进进军与收入并保留实际秒", delegate {
            BattleSession b = Session(); b.Start(); b.Tick(2); b.SetSpeed(3); b.Tick(3);
            Near(5, b.Time, .002f); Near(11, Metric<float>(b, "MarchTime"), .002f);
            Near(5, (float)Metric<double>(b, "ElapsedSeconds"), .002f); Equal(5, Metric<int>(b, "SpeedMask")); Equal(1033, b.Gold);
            b.SetPaused(true); b.SetSpeed(2); b.Tick(50); Near(5, (float)Metric<double>(b, "ElapsedSeconds")); Equal(5, Metric<int>(b, "SpeedMask"));
        });
        Run("大帧实际用时不受固定步上限缩短", delegate {
            BattleSession b = Session(); b.Start(); b.Tick(100); Near(100, (float)Metric<double>(b, "ElapsedSeconds")); Check(b.Time <= 30.01f);
        });
        Run("切倍率不追溯未处理零碎步与大帧旧债务军饷", delegate {
            BattleSession b = Session(); b.Start(); b.Tick(.02f); b.SetSpeed(3); b.Tick(.02f);
            Near(.06f, b.MarchTime, .002f); Equal(1000, b.Gold);
            b = Session(); b.Start(); b.Tick(100); b.SetSpeed(3); b.Tick(.01f);
            Near(60, b.MarchTime, .002f); Equal(1180, b.Gold); Near(100.01f, (float)b.ElapsedSeconds, .002f);
        });
        Run("波中途招鲁肃升三阶后可预支当前波余款", delegate {
            GameDatabase db = Fixture(); db.Levels[0].InitialGold = 32; db.Levels[0].IncomePerSecond = 0; db.FindHero("guan_yu").Cost = 20;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "lu_su", "guan_yu" }); b.Start(); b.Tick(.04f);
            Equal(null, b.TryRecruit("lu_su", 0)); Equal(null, b.TryUpgrade(0)); Equal(null, b.TryUpgrade(0)); Equal(1, b.Gold);
            Equal(null, b.TryRecruit("guan_yu", 1)); Equal(0, b.Gold); Equal(19, b.Heroes[0].WaveSupplyPaid); Equal(19, b.Heroes[0].BenefitUsed);
        });
        Run("鲁肃可预支与交易差额只读查询不消费账本且暂停归零", delegate {
            GameDatabase db = Fixture(); db.Levels[0].InitialGold = 32; db.Levels[0].IncomePerSecond = 0;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "lu_su" }); b.Start(); b.Tick(.04f); b.TryRecruit("lu_su", 0); b.TryUpgrade(0); Equal(0, Metric<int>(b, "AvailableSupplyAdvance")); b.TryUpgrade(0);
            int gold = b.Gold, used = b.Heroes[0].BenefitUsed; Equal(21, Metric<int>(b, "AvailableSupplyAdvance"));
            var afford = typeof(BattleSession).GetMethod("CanAffordTransaction"); var shortfall = typeof(BattleSession).GetMethod("GetTransactionShortfall"); Check(afford != null && shortfall != null);
            Equal(true, (bool)afford.Invoke(b, new object[] { 20 })); Equal(false, (bool)afford.Invoke(b, new object[] { 23 })); Equal(1, (int)shortfall.Invoke(b, new object[] { 23 })); Equal(gold, b.Gold); Equal(used, b.Heroes[0].BenefitUsed);
            b.SetPaused(true); Equal(0, Metric<int>(b, "AvailableSupplyAdvance")); Equal(19, (int)shortfall.Invoke(b, new object[] { 20 }));
        });
        Run("张飞二阶最大范围包含真实外围震慑", delegate {
            HeroDefinition h = Fixture().FindHero("zhang_fei"); Near(h.Range * 1.2f, BattleSession.GetMaximumRange(h, 2)); Near(h.Range * 1.2f, BattleSession.GetMaximumRange(h, 3));
        });
        Run("通用模拟把超时未胜利作为失败结果返回", delegate {
            object result = typeof(DomainTests).GetMethod("Simulate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { Fixture() }); Equal((object)false, result);
        });
        Run("全部武将有持续普攻且一二三阶成长文本对应真实特色", delegate {
            var growth = typeof(BattleSession).GetMethod("GetGrowthDescription", new[] { typeof(HeroDefinition), typeof(int) }); Check(growth != null, "缺少成长解析接口");
            foreach (string id in Ids) {
                BattleSession b = Combat(id, 2); b.Tick(2.2f); List<BattleEvent> e = b.DrainEvents();
                Check(e.Count(x => x.Kind == "basic_attack" && x.HeroId == id) >= 2, id + "需要独立持续普攻");
                string[] text = Enumerable.Range(1, 3).Select(tier => (string)growth.Invoke(null, new object[] { b.Heroes[0].Definition, tier })).ToArray();
                Check(text.All(x => !string.IsNullOrEmpty(x)) && text.Distinct().Count() == 3, id + "三阶必须具有不同机制");
            }
        });
        Run("各倍率普攻技能与Boss预警保持相同实秒", delegate {
            List<float>[] timings = new List<float>[3];
            for (int speed = 1; speed <= 3; speed++) { BattleSession b = Combat("zhang_fei", 1, true); b.SetSpeed(speed); timings[speed - 1] = AttackTimes(b, "zhang_fei", 8.2f); }
            Check(timings[0].SequenceEqual(timings[1]) && timings[1].SequenceEqual(timings[2]));
        });
        Run("升级不重置独立普攻技能或蓄力冷却", delegate {
            BattleSession b = Combat("lv_bu", 1); b.Tick(.4f); HeroUnit hero = b.Heroes[0]; float ready = hero.NextAttackAt, charge = hero.ChargeUntil;
            Equal(null, b.TryUpgrade(0)); Near(ready, hero.NextAttackAt); Near(charge, hero.ChargeUntil); b.DrainEvents(); b.Tick(.1f); Equal(0, b.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("三阶雷链整次技能至多七个真实目标", delegate {
            BattleSession b = Combat("zhang_jiao", 12); b.Heroes[0].Level = 3; b.Tick(.04f);
            List<BattleEvent> attacks = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(7, attacks.Count); Equal(7, attacks.Select(x => x.TargetEnemyId).Distinct().Count());
        });
        Run("箭雨每轮去重且三阶整次最多两轮", delegate {
            BattleSession b = Combat("yuan_shao", 10); b.Heroes[0].Level = 3; b.Tick(1);
            List<BattleEvent> attacks = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Check(attacks.Count > 10); Check(attacks.GroupBy(x => x.TargetEnemyId).All(x => x.Count() <= 2));
        });
        Run("二阶太史慈单敌双箭三阶三箭且不重复递归", delegate {
            BattleSession b = Combat("taishi_ci", 1); b.Heroes[0].Level = 2; b.Tick(.04f); Equal(2, b.DrainEvents().Count(x => x.Kind == "attack"));
            b = Combat("taishi_ci", 5); b.Heroes[0].Level = 3; b.Tick(.04f); var e = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(3, e.Count); Equal(3, e.Select(x => x.TargetEnemyId).Distinct().Count());
        });
        Run("关羽转火保留两层与三阶满势追斩封顶两段", delegate {
            BattleSession b = Combat("guan_yu", 3); b.Heroes[0].Level = 2; b.Heroes[0].FocusStacks = 5; b.Heroes[0].LastTargetId = 999; b.Tick(.04f); Equal(2, b.Heroes[0].FocusStacks);
            b = Combat("guan_yu", 3); b.Tick(.04f); b.DrainEvents(); HeroUnit h = b.Heroes[0]; h.Level = 3; h.FocusStacks = 5; h.LastTargetId = b.Enemies[0].Id; h.NextAttackAt = 0; h.NextBasicAt = 100; b.Enemies[0].Health = 1; b.Tick(.04f);
            var a = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(2, a.Count); Equal(2, a.Select(x => x.TargetEnemyId).Distinct().Count());
        });
        Run("黄忠二阶只同路补射三阶可跨路线且只补一次", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                BattleSession b = Combat("huang_zhong", 3); b.Tick(.04f); b.DrainEvents(); HeroUnit h = b.Heroes[0]; h.Level = tier; h.NextBasicAt = 100; h.NextAttackAt = 0;
                EnemyUnit victim = b.Enemies[0]; victim.Health = 1; RouteDefinition side = new RouteDefinition { Id = "side", Points = victim.Route.Points };
                b.Enemies[1].Route = side; b.Enemies[2].Route = side; b.Tick(.04f);
                Equal(tier == 2 ? 1 : 2, b.DrainEvents().Count(x => x.Kind == "attack"));
            }
        });
        Run("许褚二阶单敌震慑与三阶自身周围横扫", delegate {
            BattleSession one = Combat("xu_chu", 1); one.Heroes[0].Level = 2; one.Tick(.04f); Check(one.Enemies[0].StunUntil > one.Time);
            for (int tier = 2; tier <= 3; tier++) {
                BattleSession b = Combat("xu_chu", 2); b.Heroes[0].Level = tier; b.Tick(.04f); b.DrainEvents(); b.Enemies[1].Route = new RouteDefinition { Id = "side", Points = new List<Point2> { new Point2(100, 200), new Point2(10000, 200) } }; b.Heroes[0].NextAttackAt = 0; b.Tick(.04f);
                Equal(tier == 2 ? 1 : 2, b.DrainEvents().Count(x => x.Kind == "attack"));
            }
        });
        Run("吕布二阶前半圆三阶回身扫且每敌只中一重斩", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                BattleSession b = Combat("lv_bu", 3); HeroUnit h = b.Heroes[0]; h.Level = tier; b.Tick(.04f); b.DrainEvents();
                b.Enemies[0].Travelled = 10; b.Enemies[1].Travelled = 100; b.Enemies[2].Travelled = 200; h.ChargeUntil = b.Time; h.NextAttackAt = 0; b.Tick(.04f);
                var hits = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(tier == 2 ? 2 : 3, hits.Count); Equal(hits.Count, hits.Select(x => x.TargetEnemyId).Distinct().Count());
            }
        });
        Run("赵云二阶枪线加宽三阶转向追击共享去重", delegate {
            for (int tier = 1; tier <= 2; tier++) {
                BattleSession b = Combat("zhao_yun", 2); HeroUnit h = b.Heroes[0]; h.Level = tier; b.Tick(.04f); b.DrainEvents();
                b.Enemies[1].Route = new RouteDefinition { Id = "side", Points = new List<Point2> { new Point2(0, 50), new Point2(10000, 50) } }; h.NextAttackAt = 0; b.Tick(.04f); Equal(tier, b.DrainEvents().Count(x => x.Kind == "attack"));
            }
            BattleSession c = Combat("zhao_yun", 4); c.Tick(.04f); c.DrainEvents(); c.Heroes[0].Level = 3; c.Heroes[0].NextAttackAt = 0; c.Heroes[0].NextBasicAt = 100; c.Enemies[0].Health = 1; c.Enemies[3].Route = new RouteDefinition { Id = "side", Points = new List<Point2> { new Point2(0, 160), new Point2(10000, 160) } }; c.Tick(.04f);
            var e = c.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(4, e.Count); Equal(4, e.Select(x => x.TargetEnemyId).Distinct().Count());
        });
        Run("陆逊各阶共享整次传播预算与代数并含初始火种", delegate {
            for (int tier = 1; tier <= 3; tier++) {
                BattleSession b = Combat("lu_xun", 12); b.Heroes[0].Level = tier; b.Tick(.04f);
                List<BattleEvent> events = b.DrainEvents(); Equal(tier >= 2 ? 2 : 1, events.Count(x => x.Kind == "fire"));
                FireSpreadBudget budget = b.Enemies[0].BurnBudget; Check(budget != null);
                foreach (EnemyUnit enemy in b.Enemies) enemy.Health = .01f; b.Tick(.1f); events.AddRange(b.DrainEvents());
                Check(budget.Visited.Count <= (tier == 3 ? 8 : tier == 2 ? 6 : 4)); Check(events.Where(x => x.Kind == "fire").Select(x => x.TargetEnemyId).Distinct().Count() <= budget.Maximum);
                foreach (EnemyUnit enemy in b.Enemies) Check(enemy.BurnGeneration <= (tier == 3 ? 2 : 1));
            }
        });
        Run("周瑜三阶火带余烬不会抹掉陆逊传播预算或压制异源灼烧", delegate {
            GameDatabase db = Fixture(); db.Levels[0].Waves[0].Groups[0].Count = 3;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "lu_xun", "zhou_yu" }); b.TryRecruit("lu_xun", 0); b.TryRecruit("zhou_yu", 1); b.Heroes[1].Level = 3; b.Start(); b.Tick(.04f);
            EnemyUnit initial = b.Enemies[0]; FireSpreadBudget budget = initial.BurnBudget; Check(budget != null);
            foreach (HeroUnit hero in b.Heroes) { hero.NextBasicAt = 100; hero.NextAttackAt = 100; } b.Tick(.04f);
            Check(object.ReferenceEquals(budget, initial.BurnBudget), "火带不得覆盖陆逊整次传播账本"); initial.Health = .01f; b.Tick(.04f);
            Check(!b.Enemies.Contains(initial)); Check(b.Enemies.Any(x => object.ReferenceEquals(budget, x.BurnBudget)), "陆逊初始目标在火带内倒下仍应传火");
        });
        Run("周瑜余烬离开火带后持续且不复用过期高伤", delegate {
            BattleSession b = Combat("zhou_yu", 1); b.Heroes[0].Level = 3; b.Tick(.1f); EnemyUnit enemy = b.Enemies[0]; b.Heroes[0].NextBasicAt = b.Heroes[0].NextAttackAt = 100; enemy.Travelled = 1000; float before = enemy.Health; b.Tick(.2f); Check(enemy.Health < before);
        });
        Run("周瑜二阶短火带沿道路覆盖圆区之外目标", delegate {
            for (int tier = 1; tier <= 2; tier++) {
                BattleSession b = Combat("zhou_yu", 2); b.Heroes[0].Level = tier; b.Tick(.04f); b.Heroes[0].NextBasicAt = b.Heroes[0].NextAttackAt = 100;
                b.Enemies[1].Travelled = 110; float health = b.Enemies[1].Health; b.Tick(.2f);
                if (tier == 1) Near(health, b.Enemies[1].Health); else Check(b.Enemies[1].Health < health);
            }
        });
        Run("郭嘉二阶双印三阶整次最多补印一次", delegate {
            BattleSession b = Combat("guo_jia", 8); b.Heroes[0].Level = 3; b.Tick(.04f); Equal(2, b.Enemies.Count(x => x.MarkAt > 0)); ReplacementBudget budget = b.Enemies.First(x => x.MarkAt > 0).MarkBudget;
            b.Heroes[0].NextBasicAt = 100; foreach (EnemyUnit enemy in b.Enemies.Take(3)) enemy.Health = 1; b.Tick(2);
            Equal(0, budget.Remaining); Check(b.Enemies.Count(x => x.MarkAt > 0) <= 1); Check(b.Enemies.Where(x => x.MarkAt > 0).All(x => !x.MarkCanRenew));
        });
        Run("张飞三阶回响不重复控制首轮目标只控制后来者", delegate {
            BattleSession b = Combat("zhang_fei", 2); b.Heroes[0].Level = 3; b.Tick(.04f); b.DrainEvents(); EnemyUnit old = b.Enemies[0], late = b.Enemies[1];
            // 第二名先退出首轮集合，使用新出兵模拟后来者。
            GameDatabase db = Fixture(); db.Levels[0].Waves[0].Groups[0].Count = 2; db.Levels[0].Waves[0].Groups[0].Interval = .4f;
            b = new BattleSession(db, db.Levels[0], new[] { "zhang_fei" }); b.TryRecruit("zhang_fei", 0); b.Heroes[0].Level = 3; b.Start(); b.Tick(.04f); int initial = b.Enemies[0].Id; b.DrainEvents(); b.Tick(.8f);
            var controls = b.DrainEvents().Where(x => x.Kind == "stun").ToList(); Check(controls.Count == 1 && controls[0].TargetEnemyId != initial);
        });
        Run("诸葛亮二三阶风眼分别持续覆盖一或两条路线", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                BattleSession b = Combat("zhuge_liang", 2); b.Tick(.04f); b.Heroes[0].Level = tier; b.Heroes[0].NextAttackAt = 0;
                b.Enemies[1].Route = new RouteDefinition { Id = "side", Points = new List<Point2> { new Point2(0, 210), new Point2(10000, 210) } }; b.Tick(.04f);
                b.Heroes[0].Definition.Range = 1; foreach (EnemyUnit enemy in b.Enemies) { enemy.SlowUntil = 0; enemy.SlowFactor = 1; } b.Tick(.3f);
                Equal(tier == 2 ? 1 : 2, b.Enemies.Count(x => x.SlowUntil > b.Time));
            }
        });
        Run("马超二阶枪线最多推三名三阶反扫不重复且Boss不退", delegate {
            BattleSession b = Combat("ma_chao", 6); b.Tick(.04f); b.Heroes[0].Level = 3; b.Heroes[0].ChargeUntil = b.Time; b.Heroes[0].NextAttackAt = 0;
            foreach (EnemyUnit enemy in b.Enemies) enemy.Travelled = 200; b.Tick(.04f); Equal(3, b.Enemies.Count(x => x.Travelled < 150));
        });
        Run("吕蒙二阶每敌只背袭一次三阶波及不再触发", delegate {
            BattleSession b = Combat("lv_meng", 4); b.Heroes[0].Level = 3; b.Tick(.04f); b.DrainEvents(); b.Heroes[0].NextBasicAt = 100;
            b.Enemies[0].Travelled = 520; foreach (EnemyUnit enemy in b.Enemies.Skip(1)) enemy.Travelled = 510; foreach (EnemyUnit enemy in b.Enemies) enemy.StunUntil = 0; b.Tick(.04f);
            var attacks = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Check(attacks.Count <= 12); Equal(4, b.Heroes[0].Backstabbed.Count); b.Tick(.5f); Equal(0, b.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("曹操二阶圈外最近友军与三阶最多三次追加不协同", delegate {
            GameDatabase db = Fixture(); db.FindHero("cao_cao").Range = 100; BattleSession b = new BattleSession(db, db.Levels[0], new[] { "cao_cao", "guan_yu", "taishi_ci", "zhao_yun", "xun_yu" });
            for (int i = 0; i < 5; i++) b.TryRecruit(new[] { "cao_cao", "guan_yu", "taishi_ci", "zhao_yun", "xun_yu" }[i], i);
            b.Heroes[0].Level = 3; b.Start(); b.Tick(.04f); var events = b.DrainEvents(); Equal(3, events.Count(x => x.Kind == "basic_attack" && x.Text == "号令追加普攻")); Equal(1, events.Count(x => x.Kind == "attack" && x.HeroId == "xun_yu"));
        });
        Run("貂蝉三阶共享至多两次传递且传递者不继续传递", delegate {
            BattleSession b = Combat("diao_chan", 8); b.Heroes[0].Level = 3; b.Tick(.04f); Equal(2, b.Enemies.Count(x => x.VulnerableUntil > b.Time)); ReplacementBudget budget = b.Enemies.First(x => x.VulnerableUntil > b.Time).VulnerableBudget;
            foreach (EnemyUnit enemy in b.Enemies.Where(x => x.VulnerableCanSpread)) { enemy.Health = .01f; enemy.BurnUntil = b.Time + 2; enemy.BurnDamage = 100; } b.Tick(.04f); Equal(0, budget.Remaining); Equal(2, b.Enemies.Count(x => x.VulnerableUntil > b.Time)); Check(b.Enemies.Where(x => x.VulnerableUntil > b.Time).All(x => !x.VulnerableCanSpread));
        });
        Run("庞统三阶补入一名沿用到期时间并不循环传伤", delegate {
            BattleSession b = Combat("pang_tong", 6); b.Heroes[0].Level = 3; b.Tick(.04f); LinkGroup group = b.Enemies[0].Links; Equal(3, group.Members.Count); float until = group.Until;
            b.Heroes[0].NextBasicAt = 100; b.Enemies[0].Health = .01f; b.Enemies[0].BurnDamage = 100; b.Enemies[0].BurnUntil = b.Time + 1; b.Tick(.04f);
            Check(group.Replaced); Equal(3, group.Members.Count); Near(until, group.Until); Check(b.DrainEvents().Count(x => x.Text == "连环补入") == 1);
        });
        Run("荀彧二阶击退后改射三阶双箭共用一次冷却", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                GameDatabase db = Fixture(); db.FindHero("guan_yu").Damage = 100000; db.Levels[0].Waves[0].Groups[0].Count = 5;
                BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "xun_yu" }); b.TryRecruit("guan_yu", 0); b.TryRecruit("xun_yu", 1); b.Heroes[1].Level = tier; b.Start(); b.Tick(.04f);
                var attacks = b.DrainEvents().Where(x => x.Kind == "attack" && x.HeroId == "xun_yu").ToList(); Equal(tier == 3 ? 2 : 1, attacks.Count); Equal(attacks.Count, attacks.Select(x => x.TargetEnemyId).Distinct().Count()); Check(b.Heroes[1].AssistReadyAt > b.Time + 1.4f);
            }
        });
        Run("贯穿雷链箭雨枪弓的首段技能命中可以触发一次协射", delegate {
            foreach (string id in new[] { "zhao_yun", "zhang_jiao", "yuan_shao", "jiang_wei" }) {
                GameDatabase db = Fixture(); db.Levels[0].Waves[0].Groups[0].Count = 5; BattleSession b = new BattleSession(db, db.Levels[0], new[] { id, "xun_yu" }); b.TryRecruit(id, 0); b.TryRecruit("xun_yu", 1);
                foreach (HeroUnit hero in b.Heroes) hero.NextBasicAt = 100; b.Start(); b.Tick(.04f); Equal(1, b.DrainEvents().Count(x => x.Kind == "attack" && x.HeroId == "xun_yu"));
            }
        });
        Run("司马懿二阶破绽延长三阶接力只一次并保持预警延迟上限", delegate {
            BattleSession b = Combat("sima_yi", 2, true); b.Heroes[0].Level = 3; b.Tick(.04f); EnemyUnit first = b.Enemies[0], second = b.Enemies[1];
            first.WarningUntil = b.Time + .1f; second.WarningUntil = b.Time + 2; second.DisruptImmuneUntil = 0; first.Definition.Skill = "guard"; first.AbilityDelay = 1.4f; b.Tick(.2f);
            Near(.75f, first.RecoverUntil - b.Time - 2, .2f); Check(!second.DisruptCanRelay); Check(second.AbilityDelay <= 1.4f); b.Tick(.1f); Check(first.RecoverExtension == 0);
        });
        Run("孙尚香二三阶追射封顶且不重置冷却", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                BattleSession b = Combat("sun_shangxiang", 6); b.Tick(.04f); b.DrainEvents(); b.Heroes[0].Level = tier; b.Heroes[0].NextBasicAt = 100; b.Heroes[0].NextAttackAt = 0; foreach (EnemyUnit enemy in b.Enemies) enemy.Health = 1; b.Tick(.04f);
                Equal(tier, b.DrainEvents().Count(x => x.Kind == "attack")); Near(b.Time + 3, b.Heroes[0].NextAttackAt, .04f);
            }
        });
        Run("张辽三阶首敌击退后只冲击未中溅射者最多两名", delegate {
            BattleSession b = Combat("zhang_liao", 8); b.Tick(.04f); b.DrainEvents(); b.Heroes[0].Level = 3; b.Heroes[0].NextAttackAt = 0; b.Heroes[0].NextBasicAt = 100; b.Enemies[0].Health = 1; b.Tick(.04f);
            var e = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(6, e.Count); Equal(6, e.Select(x => x.TargetEnemyId).Distinct().Count());
        });
        Run("姜维二阶提前换阵三阶换阵首轮接力一次", delegate {
            for (int tier = 1; tier <= 3; tier++) {
                GameDatabase db = Fixture(); db.Levels[0].Slots[0].Position = new Point2(500, 0); BattleSession b = new BattleSession(db, db.Levels[0], new[] { "jiang_wei" }); b.TryRecruit("jiang_wei", 0); b.Heroes[0].Level = tier; b.Start(); b.Tick(.04f);
                Equal(tier == 1 ? 0 : 1, b.DrainEvents().Count(x => x.Kind == "attack"));
            }
        });
        Run("姜维三阶近敌换阵首轮接力仅一轮二阶不接力", delegate {
            for (int tier = 2; tier <= 3; tier++) {
                BattleSession b = Combat("jiang_wei", 2); b.Heroes[0].Level = tier; b.Tick(.04f); b.DrainEvents(); b.Tick(3.1f); Equal(tier == 2 ? 1 : 3, b.DrainEvents().Count(x => x.Kind == "attack"));
            }
        });
        Run("华佗三阶Boss漏兵提前医护共享单波两点且零城防不救", delegate {
            for (int damage = 3; damage <= 10; damage += 7) {
                GameDatabase db = Fixture(); db.FindHero("hua_tuo").Range = 0; db.Enemies[0].IsBoss = true; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = damage; db.Levels[0].Waves[0].Groups[0].Count = 2; db.Levels[0].Waves[0].Groups[0].Interval = .4f;
                BattleSession b = new BattleSession(db, db.Levels[0], new[] { "hua_tuo" }); b.TryRecruit("hua_tuo", 0); b.Heroes[0].Level = 3; b.Start(); b.Tick(.3f);
                Equal(damage == 3 ? 9 : 0, b.CastleHealth); Equal(damage == 3 ? 2 : 0, b.Heroes[0].BenefitUsed); b.Tick(1); Check(b.Heroes[0].BenefitUsed <= 2);
            }
        });
        Run("鲁肃二阶首兵预支三阶交易补差失败原子性与总账", delegate {
            GameDatabase db = Fixture(); db.Levels[0].InitialGold = 30; db.FindHero("lu_su").Range = 0; db.FindHero("guan_yu").Cost = 21; db.FindHero("huang_zhong").Cost = 40;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "lu_su", "guan_yu", "huang_zhong" }); b.TryRecruit("lu_su", 0); b.Heroes[0].Level = 3; b.Start(); b.Tick(.04f); Equal(30, b.Gold); Equal(10, b.Heroes[0].WaveSupplyPaid);
            int before = b.Gold, used = b.Heroes[0].BenefitUsed; Check(b.TryRecruit("huang_zhong", 1) == null); Equal(0, b.Gold); Equal(used + 10, b.Heroes[0].BenefitUsed);
            int spent = b.GoldSpent; Check(b.TryRecruit("guan_yu", 2) != null); Equal(spent, b.GoldSpent); Equal(20, b.Heroes[0].BenefitUsed); Equal(0, b.Gold); Check(!b.IsRecruited("guan_yu"));
        });
        Run("鲁肃波中升三阶不论是否补差预支都结算同额补给", delegate {
            int[] totals = new int[2];
            for (int advance = 0; advance < 2; advance++) {
                GameDatabase db = Fixture(); db.FindHero("lu_su").Range = 0; db.FindHero("huang_zhong").Cost = 970; db.Enemies[0].Speed = 100000; db.Levels[0].IncomePerSecond = 0;
                BattleSession b = new BattleSession(db, db.Levels[0], new[] { "lu_su", "huang_zhong" }); b.TryRecruit("lu_su", 0); b.Start(); b.Tick(.04f); Equal(null, b.TryUpgrade(0)); Equal(null, b.TryUpgrade(0));
                if (advance == 1) { Equal(null, b.TryRecruit("huang_zhong", 1)); Equal(1, b.Heroes[0].BenefitUsed); }
                b.Tick(.2f); Equal(BattleState.Victory, b.State); totals[advance] = b.Heroes[0].BenefitUsed; Equal(21, totals[advance]);
            }
            Equal(totals[0], totals[1]);
        });
        Run("备战调位保留实例等级军饷消费与招募历史", delegate {
            GameDatabase db = Fixture(); LevelDefinition level = db.Levels[0];
            BattleSession b = new BattleSession(db, level, new[] { "guan_yu", "huang_zhong" });
            Equal(null, b.TryRecruit("guan_yu", 0)); Equal(null, b.TryUpgrade(0));
            HeroUnit hero = b.Heroes[0]; int gold = b.Gold, spent = b.GoldSpent; Point2 origin = hero.Position; b.DrainEvents();
            Equal(null, b.TryMoveHero(0, 3)); Equal(1, b.Heroes.Count); Check(object.ReferenceEquals(hero, b.Heroes[0]));
            Equal(3, hero.SlotId); Equal(2, hero.Level); Near(0, Point2.Distance(level.Slots[3].Position, hero.Position));
            Equal(gold, b.Gold); Equal(spent, b.GoldSpent); Check(b.IsRecruited("guan_yu"));
            List<BattleEvent> events = b.DrainEvents(); Equal(1, events.Count); Equal("move", events[0].Kind); Equal("guan_yu", events[0].HeroId);
            Near(0, Point2.Distance(origin, events[0].Position)); Near(0, Point2.Distance(hero.Position, events[0].Target));
            Check(b.TryRecruit("guan_yu", 0) != null); Equal(gold, b.Gold); Equal(spent, b.GoldSpent);
            Equal(null, b.TryRecruit("huang_zhong", 0)); Equal(2, b.Heroes.Count);
        });
        Run("无效来源目的占位与同位调位均不产生副作用", delegate {
            BattleSession b = Session("guan_yu", "huang_zhong"); b.TryRecruit("guan_yu", 0); b.TryRecruit("huang_zhong", 1);
            HeroUnit first = b.Heroes[0], second = b.Heroes[1]; int gold = b.Gold, spent = b.GoldSpent; Point2 position = first.Position; b.DrainEvents();
            foreach (int[] move in new[] { new[] { 999, 2 }, new[] { 2, 3 }, new[] { 0, 999 }, new[] { 0, 1 } })
            {
                Check(b.TryMoveHero(move[0], move[1]) != null); Equal(0, first.SlotId); Equal(1, second.SlotId);
                Near(0, Point2.Distance(position, first.Position)); Equal(gold, b.Gold); Equal(spent, b.GoldSpent); Equal(2, b.Heroes.Count); Equal(0, b.DrainEvents().Count);
            }
            Equal(null, b.TryMoveHero(0, 0)); Equal(0, b.DrainEvents().Count); Equal(gold, b.Gold); Equal(spent, b.GoldSpent);
            Check(b.IsRecruited("guan_yu") && b.IsRecruited("huang_zhong"));
        });
        Run("开战暂停胜利失败均拒绝备战调位", delegate {
            foreach (BattleState state in new[] { BattleState.Running, BattleState.Paused, BattleState.Victory, BattleState.Defeat })
            {
                GameDatabase db = Fixture(); db.Heroes[0].Range = .01f;
                if (state == BattleState.Victory) db.Levels[0].Waves.Clear();
                if (state == BattleState.Defeat) { db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = 10; }
                BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu" }); b.TryRecruit("guan_yu", 0); HeroUnit hero = b.Heroes[0]; b.Start();
                if (state == BattleState.Paused) b.SetPaused(true);
                else if (state != BattleState.Running) b.Tick(.2f);
                Equal(state, b.State); int gold = b.Gold, spent = b.GoldSpent; Point2 position = hero.Position; b.DrainEvents();
                Check(b.TryMoveHero(0, 2) != null); Equal(0, hero.SlotId); Near(0, Point2.Distance(position, hero.Position));
                Equal(gold, b.Gold); Equal(spent, b.GoldSpent); Equal(0, b.DrainEvents().Count);
            }
        });
        Run("张飞节奏说明使用真实最低四秒冷却且尊重更慢配置", delegate {
            BattleSession b = Combat("zhang_fei", 1); string timing = BattleSession.GetAttackTimingDescription(b.Heroes[0].Definition);
            Check(timing.Contains("4") && timing.Contains("冷却"));
            List<float> attacks = AttackTimes(b, "zhang_fei", 8.3f); Equal(3, attacks.Count);
            Near(4, attacks[1] - attacks[0], .04f); Near(4, attacks[2] - attacks[1], .04f);
            b = Combat("zhang_fei", 1); b.Heroes[0].Definition.AttackInterval = 6;
            Check(BattleSession.GetAttackTimingDescription(b.Heroes[0].Definition).Contains("6")); attacks = AttackTimes(b, "zhang_fei", 6.2f); Equal(2, attacks.Count); Near(6, attacks[1] - attacks[0], .04f);
        });
        Run("吕布节奏说明与一秒蓄力四点八六秒冷却一致", delegate {
            BattleSession b = Combat("lv_bu", 1); b.Heroes[0].Definition.AttackInterval = 1.8f;
            string timing = BattleSession.GetAttackTimingDescription(b.Heroes[0].Definition); Check(timing.Contains("蓄力1秒") && timing.Contains("出手后") && timing.Contains("4.86"));
            b.Tick(.5f); Equal(0, b.DrainEvents().Count(x => x.Kind == "attack"));
            List<float> attacks = AttackTimes(b, "lv_bu", 7.2f); Equal(2, attacks.Count); Near(1.033f, attacks[0], .04f);
            Near(5.9f, attacks[1] - attacks[0], .07f); Near(4.86f, b.Heroes[0].NextAttackAt - attacks[1], .001f);
        });
        Run("马超独立三秒冷却与其他特色技能节奏一致", delegate {
            BattleSession b = Combat("ma_chao", 1); string timing = BattleSession.GetAttackTimingDescription(b.Heroes[0].Definition);
            Check(timing.Contains("蓄力0.55秒") && timing.Contains("出手后") && timing.Contains("3"));
            List<float> attacks = AttackTimes(b, "ma_chao", 4.3f); Equal(2, attacks.Count); Near(.6f, attacks[0], .04f); Near(3.6f, attacks[1] - attacks[0], .07f);
            foreach (string id in new[] { "guan_yu", "guo_jia", "yuan_shao", "sima_yi" })
            {
                b = Combat(id, 1); float interval = id == "sima_yi" ? 4 : 3;
                Check(BattleSession.GetAttackTimingDescription(b.Heroes[0].Definition).Contains(interval.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
                attacks = AttackTimes(b, id, interval + .2f); Equal(2, attacks.Count); Near(interval, attacks[1] - attacks[0], .04f);
            }
        });
        Run("荀彧说明与战斗均按附近友军命中及独立协射冷却触发", delegate {
            BattleSession solo = Combat("xun_yu", 1); solo.Heroes[0].Definition.AttackInterval = 99;
            string timing = BattleSession.GetAttackTimingDescription(solo.Heroes[0].Definition); Check(timing.Contains("附近友军") && timing.Contains("协射") && timing.Contains("1.5") && !timing.Contains("99"));
            Equal(0, AttackTimes(solo, "xun_yu", 4).Count);
            GameDatabase db = Fixture(); db.FindHero("guan_yu").BasicAttackInterval = .1f; db.FindHero("xun_yu").AttackInterval = 99;
            BattleSession pair = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "xun_yu" }); pair.TryRecruit("guan_yu", 0); pair.TryRecruit("xun_yu", 1); pair.Start();
            List<float> attacks = AttackTimes(pair, "xun_yu", 3.4f); Equal(3, attacks.Count);
            for (int i = 1; i < attacks.Count; i++) { Check(attacks[i] - attacks[i - 1] >= 1.5f - .001f); Check(attacks[i] - attacks[i - 1] < 1.65f); }
        });
        Run("吕蒙说明与真实伏击均限制每名敌人一次", delegate {
            BattleSession b = Combat("lv_meng", 2); b.Heroes[0].Definition.AttackInterval = 99;
            string timing = BattleSession.GetAttackTimingDescription(b.Heroes[0].Definition); Check(timing.Contains("每名敌人") && timing.Contains("一次") && !timing.Contains("99"));
            b.Tick(.1f); List<BattleEvent> attacks = b.DrainEvents().Where(x => x.Kind == "attack").ToList(); Equal(2, attacks.Count); Equal(2, attacks.Select(x => x.TargetEnemyId).Distinct().Count());
            b.Tick(4); Equal(0, b.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("普通突破记录保存真实路线剩余生命并跨事件消费独立保留", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 60000; db.Levels[0].Waves[0].Groups[0].Delay = .2f; db.Levels[0].Waves[0].Groups[0].HealthScale = 1.4f;
            BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); Equal(null, b.LastBreach); b.Start(); b.Tick(.3f);
            Equal(1, b.Enemies.Count); EnemyUnit enemy = b.Enemies[0]; enemy.Health = 4321; b.Tick(.1f);
            Equal(BattleState.Victory, b.State); Equal(9, b.CastleHealth); BreachRecord record = b.LastBreach; Check(record != null);
            Check(record.GetType().IsSealed); Equal("步卒", record.EnemyName); Equal("road", record.RouteId);
            Near(4321, record.Health); Near(14000, record.MaxHealth); Equal(false, record.IsBoss);
            BattleSession restarted = new BattleSession(db, db.Levels[0], new string[0]);
            b.DrainEvents(); enemy.Health = 0; enemy.MaxHealth = 1; enemy.Definition.Name = "已变化"; enemy.Route.Id = "changed"; b.Tick(20);
            Check(object.ReferenceEquals(record, b.LastBreach)); Equal("步卒", record.EnemyName); Equal("road", record.RouteId); Near(4321, record.Health); Near(14000, record.MaxHealth);
            Equal(null, restarted.LastBreach);
        });
        Run("末波Boss自然突破后城防仍在可胜利且保留突破快照", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].IsBoss = true; db.Enemies[0].Name = "张角"; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = 15;
            db.Levels[0].CastleMaxHealth = 30;
            BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); b.Start(); b.Tick(.04f); EnemyUnit enemy = b.Enemies[0]; enemy.Health = 6789; b.Tick(.1f);
            Equal(BattleState.Victory, b.State); Equal(15, b.CastleHealth); Equal("", b.DefeatReason); Equal(0, b.Enemies.Count);
            BreachRecord record = b.LastBreach; Equal("张角", record.EnemyName); Equal("road", record.RouteId); Equal(true, record.IsBoss); Near(6789, record.Health); Near(10000, record.MaxHealth);
            List<BattleEvent> events = b.DrainEvents(); BattleEvent leak = events.Single(x => x.Kind == "leak"); Equal(enemy.Id, leak.TargetEnemyId); Near(15, leak.Amount);
            Equal(1, b.LeakedCount); Equal(0, b.DefeatedCount); Equal(1, events.Count(x => x.Kind == "victory")); Equal(0, events.Count(x => x.Kind == "defeat"));
            enemy.Health = 0; enemy.Definition.Name = "已变化"; enemy.Route.Id = "changed"; b.Tick(20);
            Check(object.ReferenceEquals(record, b.LastBreach)); Equal("张角", record.EnemyName); Equal("road", record.RouteId); Near(6789, record.Health); Equal(true, record.IsBoss); Equal(null, Session().LastBreach);
        });
        Run("连续普通突破只保留最近一名突破者", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 100000;
            db.Enemies.Add(new EnemyDefinition { Id = "scout", Name = "轻骑", MaxHealth = 333, Speed = 100000, CastleDamage = 1 });
            db.Levels[0].Routes.Add(new RouteDefinition { Id = "side", Points = new List<Point2> { new Point2(0, 50), new Point2(10000, 50) } });
            db.Levels[0].Waves[0].Groups.Add(new SpawnGroup { EnemyId = "scout", RouteId = "side", Count = 1, Delay = .5f });
            BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); b.Start(); b.Tick(.2f); BreachRecord first = b.LastBreach; Equal("步卒", first.EnemyName); b.DrainEvents();
            b.Tick(.7f); Equal(BattleState.Victory, b.State); Equal(2, b.LeakedCount); BreachRecord last = b.LastBreach; Check(!object.ReferenceEquals(first, last));
            Equal("轻骑", last.EnemyName); Equal("side", last.RouteId); Near(333, last.Health); Equal("步卒", first.EnemyName);
        });
        Run("同名重叠敌人的攻击命中与击退事件保留实例身份", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].MaxHealth = 36; db.Levels[0].Waves[0].Groups[0].Count = 2;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "taishi_ci" }); Equal(null, b.TryRecruit("taishi_ci", 0));
            b.Start(); b.Tick(.04f); Equal(2, b.Enemies.Count);
            int[] enemyIds = b.Enemies.Select(x => x.Id).ToArray(); Equal(2, enemyIds.Distinct().Count());
            Near(0, Point2.Distance(b.Enemies[0].Position, b.Enemies[1].Position));
            List<BattleEvent> opening = b.DrainEvents();
            foreach (string kind in new[] { "attack", "hit" })
            {
                List<BattleEvent> matching = opening.Where(x => x.Kind == kind).ToList(); Check(matching.Count >= 2);
                Check(matching.Select(x => x.TargetEnemyId).Distinct().OrderBy(x => x).SequenceEqual(enemyIds.OrderBy(x => x)), kind + "必须分别指向两个重叠实例");
                Check(matching.All(x => x.HeroId == "taishi_ci"));
            }
            b.Tick(3.1f); Equal(BattleState.Victory, b.State); Equal(0, b.Enemies.Count);
            List<BattleEvent> kills = b.DrainEvents().Where(x => x.Kind == "kill").ToList(); Equal(2, kills.Count);
            Check(kills.Select(x => x.TargetEnemyId).OrderBy(x => x).SequenceEqual(enemyIds.OrderBy(x => x)), "击退事件必须保留已移除敌军的实例身份");
            Equal(2, b.DefeatedCount); Equal(0, b.LeakedCount);
        });
        Run("同名敌军漏兵事件在单位移除后仍能区分实例", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 100000; db.Levels[0].Waves[0].Groups[0].Count = 2;
            BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); b.Start(); b.Tick(.04f); Equal(2, b.Enemies.Count);
            int[] enemyIds = b.Enemies.Select(x => x.Id).ToArray(); b.DrainEvents(); b.Tick(.1f);
            Equal(BattleState.Victory, b.State); Equal(0, b.Enemies.Count); Equal(8, b.CastleHealth);
            List<BattleEvent> leaks = b.DrainEvents().Where(x => x.Kind == "leak").ToList(); Equal(2, leaks.Count);
            Check(leaks.Select(x => x.TargetEnemyId).OrderBy(x => x).SequenceEqual(enemyIds.OrderBy(x => x)), "漏兵事件必须分别对应已抵城的实例");
            Equal(0, b.DefeatedCount); Equal(2, b.LeakedCount);
        });
        Run("双Boss自然战斗生命周期事件保留各自身份与登场提示", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].MaxHealth = 36; db.Enemies[0].IsBoss = true; db.Enemies[0].CharacterId = "lu_su"; db.Enemies[0].Skill = "charge";
            db.Levels[0].Waves[0].Groups[0].Count = 2;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "zhang_fei" }); Equal(null, b.TryRecruit("zhang_fei", 0));
            b.Start(); b.Tick(.04f); Equal(2, b.Enemies.Count); int[] enemyIds = b.Enemies.Select(x => x.Id).ToArray();
            List<BattleEvent> events = b.DrainEvents();
            Equal(2, events.Count(x => x.Kind == "boss_spawn")); Equal(2, events.Count(x => x.Kind == "notice"));
            foreach (int enemyId in enemyIds)
            {
                BattleEvent spawn = events.Single(x => x.Kind == "boss_spawn" && x.TargetEnemyId == enemyId);
                BattleEvent notice = events.Single(x => x.Kind == "notice" && x.TargetEnemyId == enemyId);
                Equal(notice.Text, spawn.Text); Near(36, spawn.Amount); Near(notice.Amount, spawn.Amount);
                Near(0, Point2.Distance(notice.Position, spawn.Position));
            }
            for (int step = 0; step < 120 && b.State == BattleState.Running; step++) { b.Tick(.25f); events.AddRange(b.DrainEvents()); }
            Equal(BattleState.Victory, b.State); Equal(0, b.Enemies.Count); Equal(2, b.DefeatedCount); Equal(10, b.CastleHealth);
            foreach (string kind in new[] { "boss_spawn", "boss_warning", "boss_phase", "attack", "hit", "stun", "kill", "notice" })
            {
                List<BattleEvent> matching = events.Where(x => x.Kind == kind).ToList();
                Check(matching.All(x => enemyIds.Contains(x.TargetEnemyId)), kind + "必须指向真实Boss实例");
                foreach (int enemyId in enemyIds) Check(matching.Any(x => x.TargetEnemyId == enemyId), kind + "必须覆盖每名Boss");
                if (kind == "boss_spawn" || kind == "boss_phase" || kind == "kill") Equal(2, matching.Count);
            }
        });
        Run("敌军相关技能反馈指向实际目标且协同攻击保留身份", delegate {
            string[] heroIds = { "lv_bu", "zhou_yu", "lu_xun", "guo_jia", "zhuge_liang", "pang_tong", "sima_yi", "xun_yu" };
            string[] expectedKinds = { "notice", "fire", "fire", "fire", "slow", "notice", "notice", "attack" };
            for (int index = 0; index < heroIds.Length; index++)
            {
                string heroId = heroIds[index]; GameDatabase db = Fixture(); db.Enemies[0].Skill = "healer";
                db.Levels[0].Waves[0].Groups[0].Count = 2; db.Levels[0].Waves[0].Groups[0].Interval = .5f;
                string[] roster = heroId == "xun_yu" ? new[] { "guan_yu", heroId } : new[] { heroId };
                BattleSession b = new BattleSession(db, db.Levels[0], roster);
                for (int slot = 0; slot < roster.Length; slot++) Equal(null, b.TryRecruit(roster[slot], slot));
                b.Start(); bool sawFeedback = false, sawHealer = false;
                // 司马懿会将军医首次技能推迟至约11秒，观察窗口须覆盖自然施法。
                for (int step = 0; step < 300; step++)
                {
                    b.Tick(.04f);
                    foreach (BattleEvent e in b.DrainEvents())
                    {
                        bool heroFeedback = e.HeroId == heroId && new[] { "attack", "hit", "fire", "slow", "stun", "notice" }.Contains(e.Kind);
                        bool healerFeedback = e.HeroId == null && e.Kind == "notice";
                        if (!heroFeedback && !healerFeedback) continue;
                        EnemyUnit target = b.Enemies.Find(x => x.Id == e.TargetEnemyId);
                        Check(target != null, heroId + "的" + e.Kind + "必须指向真实敌军实例");
                        Near(0, Point2.Distance(e.Target, target.Position), .04f);
                        if (heroFeedback && e.Kind == expectedKinds[index]) sawFeedback = true;
                        if (healerFeedback) sawHealer = true;
                    }
                }
                Check(sawFeedback, heroId + "必须实际产生待验证的技能反馈");
                Check(sawHealer, heroId + "场景军医必须实际施放治疗，当前时间" + b.Time + "，下次技能" + string.Join("，", b.Enemies.Select(x => x.NextAbilityAt.ToString("F2")).ToArray()));
            }
        });
        Run("战斗情报倒计时跨准备运行暂停与终局", delegate {
            GameDatabase db = Fixture(); LevelDefinition level = db.Levels[0]; level.Waves[0].StartDelay = 2;
            BattleSession b = new BattleSession(db, level, new string[0]);
            Near(2, Metric<float>(b, "NextWaveCountdown")); Equal(false, Metric<bool>(b, "IsWaveActive")); Equal(0, Metric<int>(b, "RemainingEnemies"));
            b.Start(); b.Tick(.5f); Near(1.5f, Metric<float>(b, "NextWaveCountdown"));
            b.SetPaused(true); b.Tick(20); Near(1.5f, Metric<float>(b, "NextWaveCountdown"));
            b.SetPaused(false); b.Tick(1.5f); Near(0, Metric<float>(b, "NextWaveCountdown")); Equal(true, Metric<bool>(b, "IsWaveActive")); Equal(1, Metric<int>(b, "RemainingEnemies"));
            b.SetPaused(true); b.Tick(20); Equal(true, Metric<bool>(b, "IsWaveActive")); Equal(1, Metric<int>(b, "RemainingEnemies")); Near(0, Metric<float>(b, "NextWaveCountdown"));
        });
        Run("战斗情报包含待出兵且波间显示下波倒计时", delegate {
            GameDatabase db = Fixture(); LevelDefinition level = db.Levels[0]; db.Enemies[0].MaxHealth = 1; db.Heroes[0].Range = 20000;
            level.Waves[0].Groups[0].Count = 2; level.Waves[0].Groups[0].Interval = 2; level.Waves[0].Groups[0].Delay = 1;
            level.Waves.Add(new WaveDefinition { Name = "第二波", StartDelay = 3, Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 1 } } });
            BattleSession b = new BattleSession(db, level, new[] { "guan_yu" }); b.TryRecruit("guan_yu", 0); b.Start(); b.Tick(.1f);
            Equal(0, b.Enemies.Count); Equal(2, Metric<int>(b, "RemainingEnemies")); Equal(true, Metric<bool>(b, "IsWaveActive")); Near(0, Metric<float>(b, "NextWaveCountdown"));
            b.Tick(3.1f); Equal(false, Metric<bool>(b, "IsWaveActive")); Equal(0, Metric<int>(b, "RemainingEnemies")); Check(Metric<float>(b, "NextWaveCountdown") > 2.8f);
            b.Tick(4); Equal(BattleState.Victory, b.State); Equal(false, Metric<bool>(b, "IsWaveActive")); Near(0, Metric<float>(b, "NextWaveCountdown")); Equal("", Metric<string>(b, "DefeatReason"));
        });
        Run("累计花费只统计成功招募升级且取事件不丢统计", delegate {
            BattleSession b = Session("guan_yu"); Equal(0, Metric<int>(b, "GoldSpent"));
            Equal(null, b.TryRecruit("guan_yu", 0)); Equal(10, Metric<int>(b, "GoldSpent"));
            Check(b.TryRecruit("guan_yu", 1) != null); Check(b.TryUpgrade(33) != null); Equal(10, Metric<int>(b, "GoldSpent"));
            Equal(null, b.TryUpgrade(0)); Equal(18, Metric<int>(b, "GoldSpent")); b.DrainEvents(); Equal(18, Metric<int>(b, "GoldSpent"));
            b.Start(); b.SetPaused(true); Check(b.TryUpgrade(0) != null); Equal(18, Metric<int>(b, "GoldSpent"));
            Equal(0, Metric<int>(Session("guan_yu"), "GoldSpent"));
        });
        Run("累计击退漏兵与失败原因跨事件消费保留", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 100000; db.Levels[0].CastleMaxHealth = 1;
            BattleSession leak = new BattleSession(db, db.Levels[0], new string[0]); leak.Start(); leak.Tick(.2f);
            Equal(BattleState.Defeat, leak.State); Equal(1, Metric<int>(leak, "LeakedCount")); Equal(0, Metric<int>(leak, "DefeatedCount")); Equal("城池失守", Metric<string>(leak, "DefeatReason"));
            leak.DrainEvents(); leak.Tick(20); Equal(1, Metric<int>(leak, "LeakedCount")); Equal("城池失守", Metric<string>(leak, "DefeatReason")); Near(0, Metric<float>(leak, "NextWaveCountdown"));
            BattleSession kill = Combat("guan_yu", 1); kill.Enemies.Clear(); kill.Tick(.04f); kill.Enemies[0].Health = 1; kill.Tick(1);
            Equal(1, Metric<int>(kill, "DefeatedCount")); Equal(0, Metric<int>(kill, "LeakedCount")); kill.DrainEvents(); Equal(1, Metric<int>(kill, "DefeatedCount"));
        });
        Run("Boss致命突破原因与普通漏兵通关区分", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 100000; db.Enemies[0].IsBoss = true; db.Enemies[0].CastleDamage = 10;
            BattleSession boss = new BattleSession(db, db.Levels[0], new string[0]); boss.Start(); boss.Tick(.2f);
            Equal("敌将突破，守城失败", Metric<string>(boss, "DefeatReason")); Equal(1, Metric<int>(boss, "LeakedCount")); Equal(false, Metric<bool>(boss, "IsWaveActive"));
            db = Fixture(); db.Enemies[0].Speed = 100000; BattleSession normal = new BattleSession(db, db.Levels[0], new string[0]); normal.Start(); normal.Tick(.2f);
            Equal(BattleState.Victory, normal.State); Equal("", Metric<string>(normal, "DefeatReason")); Equal(1, Metric<int>(normal, "LeakedCount"));
        });
        Run("重复招募不再次扣费", delegate {
            BattleSession b = Session("guan_yu");
            Equal(null, b.TryRecruit("guan_yu", 0)); int gold = b.Gold;
            Check(b.TryRecruit("guan_yu", 1) != null); Equal(gold, b.Gold); Equal(1, b.Heroes.Count);
        });
        Run("招募历史独立于当前展示单位列表", delegate {
            BattleSession b = Session("guan_yu"); b.TryRecruit("guan_yu", 0); b.Heroes.Clear(); int gold = b.Gold;
            Check(b.TryRecruit("guan_yu", 1) != null); Equal(gold, b.Gold);
        });
        Run("军饷不足与无效阵位均无副作用", delegate {
            GameDatabase db = Fixture(); db.Levels[0].InitialGold = 9;
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu" });
            Check(b.TryRecruit("guan_yu", 0) != null); Equal(9, b.Gold); Equal(0, b.Heroes.Count); Equal(0, b.DrainEvents().Count);
            Check(b.TryRecruit("guan_yu", 999) != null); Equal(9, b.Gold);
        });
        Run("本关所有Boss身份排除", delegate {
            GameDatabase db = Fixture(); db.Levels[0].BossHeroIds.Add("guan_yu");
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "lv_bu" });
            Check(b.TryRecruit("guan_yu", 0) != null); Equal(1000, b.Gold);
            db.Enemies.Add(new EnemyDefinition { Id = "boss", CharacterId = "lv_bu", IsBoss = true, MaxHealth = 99, Speed = 1, CastleDamage = 1 });
            db.Levels[0].Waves[0].Groups.Add(new SpawnGroup { EnemyId = "boss", RouteId = "road", Count = 1 });
            BattleSession c = new BattleSession(db, db.Levels[0], new[] { "lv_bu" });
            Check(c.TryRecruit("lv_bu", 0) != null);
        });
        Run("阵容至多十人且不可招募未选角色", delegate {
            GameDatabase db = Fixture(); bool rejected = false;
            try { new BattleSession(db, db.Levels[0], Ids.Take(11)); } catch (ArgumentException) { rejected = true; }
            Check(rejected); Check(Session("guan_yu").TryRecruit("huang_zhong", 0) != null);
        });
        Run("三级升级及失败扣费原子性", delegate {
            BattleSession b = Session("guan_yu"); b.TryRecruit("guan_yu", 0);
            Equal(null, b.TryUpgrade(0)); Equal(null, b.TryUpgrade(0)); Equal(3, b.Heroes[0].Level);
            int gold = b.Gold; Check(b.TryUpgrade(0) != null); Equal(gold, b.Gold);
            GameDatabase db = Fixture(); db.Levels[0].InitialGold = 10;
            b = new BattleSession(db, db.Levels[0], new[] { "guan_yu" }); b.TryRecruit("guan_yu", 0);
            Check(b.TryUpgrade(0) != null); Equal(1, b.Heroes[0].Level); Equal(0, b.Gold);
        });
        Run("升级收益文案与满级查询", delegate {
            BattleSession b = Session("guan_yu"); Equal("", b.GetUpgradeDescription(0)); b.TryRecruit("guan_yu", 0);
            Check(b.GetUpgradeDescription(0).Contains("11.2") && b.GetUpgradeDescription(0).Contains("0 → 2")); b.TryUpgrade(0); b.TryUpgrade(0); Equal("已达最高阶", b.GetUpgradeDescription(0)); Equal(0, b.GetUpgradeCost(0));
        });
        Run("暂停与准备阶段冻结时间军饷敌军", delegate {
            BattleSession b = Session("guan_yu"); b.Tick(2); Equal(0f, b.Time); Equal(1000, b.Gold);
            b.Start(); b.Tick(1); b.SetPaused(true); float time = b.Time, progress = b.Enemies[0].Progress; int gold = b.Gold;
            b.Tick(100); Equal(time, b.Time); Equal(gold, b.Gold); Equal(progress, b.Enemies[0].Progress); Check(b.TryRecruit("guan_yu", 0) != null);
            b.SetPaused(false); b.Tick(1); Check(b.Time > time); Check(b.Gold > gold);
        });
        Run("倍率仅推进移动收入与进军时间", delegate {
            BattleSession a = Session(), b = Session(); a.Start(); b.Start(); b.SetSpeed(2); a.Tick(2); b.Tick(1);
            Near(2, a.Time); Near(1, b.Time); Near(a.MarchTime, b.MarchTime); Equal(a.Gold, b.Gold); Near(a.Enemies[0].Progress, b.Enemies[0].Progress);
        });
        Run("暂停冻结持续伤害与Boss技能倒计时", delegate {
            BattleSession b = Combat("zhou_yu", 1, true); b.Tick(0.04f); b.Enemies[0].NextAbilityAt = 0; b.Tick(0.04f); b.DrainEvents();
            EnemyUnit boss = b.Enemies[0]; float health = boss.Health, warning = boss.WarningUntil; b.SetPaused(true); b.Tick(20);
            Equal(health, boss.Health); Equal(warning, boss.WarningUntil); Equal(0, b.DrainEvents().Count);
            b.SetPaused(false); b.Tick(0.1f); Check(boss.Health < health);
        });
        Run("各倍率攻击Boss技能控制均按相同实秒", delegate {
            BattleSession a = Combat("guan_yu", 1, true), b = Combat("guan_yu", 1, true); b.SetSpeed(3); a.Tick(9); b.Tick(9);
            Near(a.Time, b.Time); Near(a.Enemies[0].Health, b.Enemies[0].Health); Near(a.Enemies[0].WarningUntil, b.Enemies[0].WarningUntil); Near(a.Enemies[0].Progress * 3, b.Enemies[0].Progress);
        });
        Run("抵城扣血并失败且结算停止", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 10000; db.Enemies[0].CastleDamage = 10;
            BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); b.Start(); b.Tick(2);
            Equal(BattleState.Defeat, b.State); Equal(0, b.CastleHealth); Check(b.DrainEvents().Any(x => x.Kind == "leak"));
            float time = b.Time; b.Tick(10); Equal(time, b.Time); Check(b.TryUpgrade(0) != null);
        });
        Run("Boss自然突破后继续本波且不发击退赏金", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].IsBoss = true; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = 15; db.Enemies[0].GoldReward = 999;
            LevelDefinition level = db.Levels[0]; level.CastleMaxHealth = 30; level.IncomePerSecond = 0;
            db.Enemies.Add(new EnemyDefinition { Id = "slow", Name = "后军", MaxHealth = 333, Speed = 1, CastleDamage = 1 });
            level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = "slow", RouteId = "road", Count = 1 });
            BattleSession b = new BattleSession(db, level, new string[0]); b.Start(); b.Tick(.04f); int bossId = b.Enemies.Single(x => x.Definition.IsBoss).Id; b.Tick(.2f);
            Equal(BattleState.Running, b.State); Equal(15, b.CastleHealth); Equal(1, b.Enemies.Count); Equal("slow", b.Enemies[0].Definition.Id); Check(b.IsWaveActive);
            Equal(1, b.LeakedCount); Equal(0, b.DefeatedCount); Equal(level.InitialGold, b.Gold); Equal("", b.DefeatReason);
            List<BattleEvent> events = b.DrainEvents(); BattleEvent leak = events.Single(x => x.Kind == "leak"); Equal(bossId, leak.TargetEnemyId); Near(15, leak.Amount);
            Equal(0, events.Count(x => x.Kind == "kill" || x.Kind == "victory" || x.Kind == "defeat"));
            float progress = b.Enemies[0].Progress; b.Tick(1); Check(b.Enemies[0].Progress > progress); Equal(BattleState.Running, b.State); Equal(level.InitialGold, b.Gold);
        });
        Run("Boss突破伤害使用配置且待出兵清空前不提前胜利", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].IsBoss = true; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = 7;
            LevelDefinition level = db.Levels[0]; level.CastleMaxHealth = 30; level.Waves[0].Groups[0].Count = 2; level.Waves[0].Groups[0].Interval = 1;
            BattleSession b = new BattleSession(db, level, new string[0]); b.Start(); b.Tick(.2f);
            Equal(BattleState.Running, b.State); Equal(23, b.CastleHealth); Equal(0, b.Enemies.Count); Equal(1, b.RemainingEnemies); Check(b.IsWaveActive); Equal(1, b.LeakedCount);
            Check(!b.DrainEvents().Any(x => x.Kind == "victory" || x.Kind == "defeat")); b.Tick(1.1f);
            Equal(BattleState.Victory, b.State); Equal(16, b.CastleHealth); Equal(0, b.RemainingEnemies); Equal(2, b.LeakedCount); Equal(0, b.DefeatedCount);
        });
        Run("Boss突破后等待并完成后续波才胜利", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].IsBoss = true; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = 15;
            LevelDefinition level = db.Levels[0]; level.CastleMaxHealth = 30;
            db.Enemies.Add(new EnemyDefinition { Id = "scout", Name = "轻骑", MaxHealth = 20, Speed = 100000, CastleDamage = 1 });
            level.Waves.Add(new WaveDefinition { Name = "第二波", StartDelay = 1, Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "scout", RouteId = "road", Count = 1 } } });
            BattleSession b = new BattleSession(db, level, new string[0]); b.Start(); b.Tick(.2f);
            Equal(BattleState.Running, b.State); Equal(1, b.CurrentWave); Equal(15, b.CastleHealth); Equal(0, b.RemainingEnemies); Check(!b.IsWaveActive); Check(b.NextWaveCountdown > .8f);
            Check(!b.DrainEvents().Any(x => x.Kind == "victory" || x.Kind == "defeat")); b.Tick(.5f); Equal(BattleState.Running, b.State); Equal(1, b.CurrentWave);
            b.Tick(1); Equal(BattleState.Victory, b.State); Equal(2, b.CurrentWave); Equal(14, b.CastleHealth); Equal(2, b.LeakedCount); Equal("", b.DefeatReason);
        });
        Run("连续双Boss突破累计扣城防并在第二次归零失败", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].IsBoss = true; db.Enemies[0].Name = "敌将"; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = 15;
            LevelDefinition level = db.Levels[0]; level.CastleMaxHealth = 30; level.Waves[0].Groups[0].Count = 2; level.Waves[0].Groups[0].Interval = .5f;
            BattleSession b = new BattleSession(db, level, new string[0]); b.Start(); b.Tick(.2f); BreachRecord first = b.LastBreach;
            Equal(BattleState.Running, b.State); Equal(15, b.CastleHealth); Equal(1, b.LeakedCount); Equal(1, b.RemainingEnemies);
            List<BattleEvent> events = b.DrainEvents(); Check(!events.Any(x => x.Kind == "defeat")); b.Tick(.6f); events.AddRange(b.DrainEvents());
            Equal(BattleState.Defeat, b.State); Equal(0, b.CastleHealth); Equal(2, b.LeakedCount); Equal(0, b.DefeatedCount); Equal("敌将突破，守城失败", b.DefeatReason);
            List<BattleEvent> leaks = events.Where(x => x.Kind == "leak").ToList(); Equal(2, leaks.Count); Equal(2, leaks.Select(x => x.TargetEnemyId).Distinct().Count()); Check(leaks.All(x => x.Amount == 15));
            Check(!object.ReferenceEquals(first, b.LastBreach)); Equal(true, first.IsBoss); Equal(true, b.LastBreach.IsBoss); Equal(1, events.Count(x => x.Kind == "defeat")); Equal(0, events.Count(x => x.Kind == "kill" || x.Kind == "victory"));
        });
        Run("Boss致命突破城防归零并冻结终局", delegate {
            foreach (int damage in new[] { 30, 45 })
            {
                GameDatabase db = Fixture(); db.Enemies[0].IsBoss = true; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = damage; db.Enemies[0].GoldReward = 999;
                LevelDefinition level = db.Levels[0]; level.CastleMaxHealth = 30; level.Waves[0].Groups[0].Count = 2; level.Waves[0].Groups[0].Interval = .5f;
                db.Enemies.Add(new EnemyDefinition { Id = "slow", MaxHealth = 20, Speed = 1, CastleDamage = 1 });
                level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = "slow", RouteId = "road", Count = 1 });
                BattleSession b = new BattleSession(db, level, new[] { "hua_tuo" }); Equal(null, b.TryRecruit("hua_tuo", 0)); b.Start(); b.Tick(.2f);
                Equal(BattleState.Defeat, b.State); Equal(0, b.CastleHealth); Equal("敌将突破，守城失败", b.DefeatReason); Equal(1, b.LeakedCount); Equal(0, b.DefeatedCount); Equal(0, b.Heroes[0].BenefitUsed);
                List<BattleEvent> events = b.DrainEvents(); Near(damage, events.Single(x => x.Kind == "leak").Amount); Equal(1, events.Count(x => x.Kind == "defeat")); Equal(0, events.Count(x => x.Kind == "kill" || x.Kind == "victory"));
                float time = b.Time, progress = b.Enemies[0].Progress; int gold = b.Gold, remaining = b.RemainingEnemies; BreachRecord record = b.LastBreach;
                b.Tick(20); b.SetPaused(false); b.Start(); b.Tick(20);
                Equal(BattleState.Defeat, b.State); Near(time, b.Time); Equal(gold, b.Gold); Near(progress, b.Enemies[0].Progress); Equal(remaining, b.RemainingEnemies); Equal(0, b.CastleHealth);
                Equal(1, b.LeakedCount); Equal(0, b.DefeatedCount); Check(object.ReferenceEquals(record, b.LastBreach)); Equal(0, b.DrainEvents().Count); Check(!b.IsWaveActive); Near(0, b.NextWaveCountdown);
                Check(b.TryRecruit("hua_tuo", 1) != null); Check(b.TryUpgrade(0) != null);
            }
        });
        Run("普通漏兵后城防仍在可以通关", delegate {
            GameDatabase db = Fixture(); db.Enemies[0].Speed = 10000; BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); b.Start(); b.Tick(2);
            Equal(BattleState.Victory, b.State); Equal(9, b.CastleHealth);
        });
        Run("路径按分段长度插值", delegate {
            GameDatabase db = Fixture(); db.Levels[0].Routes[0].Points = new List<Point2> { new Point2(0, 0), new Point2(100, 0), new Point2(100, 300) }; db.Enemies[0].Speed = 100;
            BattleSession b = new BattleSession(db, db.Levels[0], new string[0]); b.Start(); b.Tick(2);
            Near(100, b.Enemies[0].Position.X, 4); Near(100, b.Enemies[0].Position.Y, 4); Near(0.5f, b.Enemies[0].Progress, 0.01f);
        });
        Run("击退末波所有敌军才胜利", delegate {
            GameDatabase db = Fixture(); db.Heroes[0].Damage = 100000; db.Levels[0].Waves.Add(new WaveDefinition { StartDelay = 1, Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 1 } } });
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu" }); b.TryRecruit("guan_yu", 0); b.Start(); b.Tick(0.5f); Equal(BattleState.Running, b.State); b.Tick(4);
            Equal(BattleState.Victory, b.State); Equal(2, b.CurrentWave); Equal(0, b.Enemies.Count); Check(b.DrainEvents().Any(x => x.Kind == "victory"));
        });
        Run("事件取出后清空及重开状态独立", delegate {
            BattleSession b = Session("guan_yu"); b.TryRecruit("guan_yu", 0); Check(b.DrainEvents().Any(x => x.Kind == "recruit")); Equal(0, b.DrainEvents().Count);
            BattleSession fresh = Session("guan_yu"); Equal(null, fresh.TryRecruit("guan_yu", 0)); Equal(0f, fresh.Time); Equal(1, fresh.Heroes[0].Level);
        });
        Run("25种角色均可执行且技能有差异", delegate { foreach (string id in Ids) { BattleSession b = Session(id); b.TryRecruit(id, 0); b.Start(); b.Tick(10); Check(b.DrainEvents().Count > 0); } });
        Run("关羽连续锁定增伤且换目标重置", delegate {
            BattleSession b = Combat("guan_yu", 2); b.Tick(0.1f); List<BattleEvent> first = b.DrainEvents(); float opening = first.First(x => x.Kind == "attack").Amount;
            b.Tick(3.1f); Check(b.DrainEvents().First(x => x.Kind == "attack").Amount > opening);
            b.Enemies[0].Health = 1; b.Tick(1.1f); b.DrainEvents(); b.Tick(2.1f); Near(opening, b.DrainEvents().First(x => x.Kind == "attack").Amount);
        });
        Run("黄忠射程可覆盖基础范围外敌军", delegate {
            GameDatabase db = Fixture(); db.Levels[0].Slots[0].Position = new Point2(500, 0);
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "huang_zhong" }); b.TryRecruit("huang_zhong", 0); b.Start(); b.Tick(0.1f);
            Check(b.DrainEvents().Any(x => x.Kind == "attack"));
        });
        Run("展示范围辅助与技能最大射程一致", delegate {
            GameDatabase db = Fixture(); Near(520, BattleSession.GetMaximumRange(db.FindHero("huang_zhong"))); Near(580, BattleSession.GetMaximumRange(db.FindHero("jiang_wei"))); Near(400, BattleSession.GetMaximumRange(db.FindHero("guan_yu")));
        });
        Run("多路线优先补漏按剩余距离选择", delegate {
            BattleSession b = Combat("huang_zhong", 2); b.Tick(0.04f); b.DrainEvents(); b.Heroes[0].Definition.Range = 20000;
            b.Enemies[0].Travelled = 6000; b.Enemies[1].Travelled = 50; b.Enemies[1].RouteLength = 100;
            b.Enemies[1].Route = new RouteDefinition { Points = new List<Point2> { new Point2(0, 0), new Point2(100, 0) } }; b.Heroes[0].NextAttackAt = 0; b.Tick(0.04f);
            Check(b.DrainEvents().First(x => x.Kind == "attack").Target.X < 100, "应优先处理离城门只剩50距离的目标");
        });
        Run("许褚单敌重击与太史慈双目标", delegate {
            BattleSession single = Combat("xu_chu", 1), group = Combat("xu_chu", 2), twin = Combat("taishi_ci", 2);
            single.Tick(0.1f); group.Tick(0.1f); twin.Tick(0.1f);
            Check(single.DrainEvents().First(x => x.Kind == "attack").Amount > group.DrainEvents().First(x => x.Kind == "attack").Amount);
            Equal(2, twin.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("吕布蓄力期间不攻击随后扇形重斩", delegate {
            BattleSession b = Combat("lv_bu", 2); b.Tick(0.5f); Check(!b.DrainEvents().Any(x => x.Kind == "attack")); b.Tick(1); Check(b.DrainEvents().Count(x => x.Kind == "attack") == 2);
        });
        Run("赵云只穿透直线附近敌军", delegate {
            BattleSession b = Combat("zhao_yun", 3); b.Tick(0.01f); b.Tick(0.03f); b.DrainEvents();
            b.Enemies[2].Route = new RouteDefinition { Points = new List<Point2> { new Point2(0, 150), new Point2(10000, 150) } }; b.Heroes[0].NextAttackAt = b.Time; b.Tick(0.04f);
            Equal(2, b.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("周瑜区域灼烧与陆逊死亡传火", delegate {
            BattleSession fire = Combat("zhou_yu", 2); fire.Tick(0.1f); float health = fire.Enemies[0].Health; fire.Tick(0.3f); Check(fire.Enemies[0].Health < health);
            BattleSession spread = Combat("lu_xun", 2); spread.Tick(0.1f); EnemyUnit victim = spread.Enemies[0]; victim.Health = 0.01f; spread.Tick(0.1f);
            Check(!spread.Enemies.Contains(victim)); Check(spread.Enemies[0].BurnUntil > spread.Time);
        });
        Run("袁绍多区域箭雨与郭嘉死亡引爆", delegate {
            BattleSession rain = Combat("yuan_shao", 3); rain.Tick(0.1f); Check(rain.DrainEvents().Count(x => x.Kind == "attack") >= 3);
            BattleSession mark = Combat("guo_jia", 2); mark.Tick(0.1f); EnemyUnit victim = mark.Enemies[0]; EnemyUnit other = mark.Enemies[1]; float health = other.Health;
            victim.Health = 1; mark.Heroes[0].NextAttackAt = mark.Time; mark.Tick(0.04f); Check(other.Health < health); Check(mark.DrainEvents().Any(x => x.Text == "奇谋印记引爆"));
        });
        Run("张飞硬控诸葛减速与Boss抗性", delegate {
            BattleSession stun = Combat("zhang_fei", 1); stun.Tick(0.1f); Check(stun.Enemies[0].StunUntil > stun.Time);
            BattleSession slow = Combat("zhuge_liang", 1); slow.Tick(0.1f); Check(slow.Enemies[0].SlowUntil > slow.Time); Equal(0f, slow.Enemies[0].StunUntil);
            BattleSession boss = Combat("zhang_fei", 1, true); boss.Tick(0.04f); Check(boss.Enemies[0].StunUntil - boss.Time <= 0.31f);
            boss.Heroes[0].NextAttackAt = boss.Time; float until = boss.Enemies[0].StunUntil; boss.Tick(0.04f); Equal(until, boss.Enemies[0].StunUntil);
        });
        Run("马超击退普通兵而Boss不后退", delegate {
            BattleSession soldier = Combat("ma_chao", 1), boss = Combat("ma_chao", 1, true); soldier.Tick(1); boss.Tick(1);
            soldier.Enemies[0].Travelled = boss.Enemies[0].Travelled = 200; soldier.Heroes[0].NextAttackAt = boss.Heroes[0].NextAttackAt = 0;
            soldier.Tick(0.75f); boss.Tick(0.75f); Check(soldier.Enemies[0].Travelled < 150); Check(boss.Enemies[0].Travelled >= 200);
        });
        Run("马超蓄力后再发动枪击", delegate {
            BattleSession b = Combat("ma_chao", 1); b.Tick(0.3f); Check(!b.DrainEvents().Any(x => x.Kind == "attack")); b.Tick(0.5f); Equal(1, b.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("张角链电跳出自身射程与吕蒙每敌伏击一次", delegate {
            BattleSession chain = Combat("zhang_jiao", 2); chain.Heroes[0].Definition.Range = 120; chain.Tick(0.04f); chain.DrainEvents(); chain.Enemies[1].Position = new Point2(-50, 0); chain.Enemies[1].Travelled = -50; chain.Heroes[0].NextAttackAt = 0; chain.Tick(0.04f);
            Check(chain.DrainEvents().Count(x => x.Kind == "attack") == 2);
            BattleSession ambush = Combat("lv_meng", 1); ambush.Tick(0.1f); Equal(1, ambush.DrainEvents().Count(x => x.Kind == "attack")); ambush.Tick(4); Equal(0, ambush.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("曹操只提高伤害且貂蝉施加易伤", delegate {
            BattleSession baseline = Combat("guan_yu", 1), buff = Combat("guan_yu", 1); GameDatabase db = Fixture();
            BattleSession pair = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "cao_cao", "diao_chan" }); pair.TryRecruit("guan_yu", 0); pair.TryRecruit("cao_cao", 1); pair.TryRecruit("diao_chan", 2); pair.Start();
            baseline.Tick(0.04f); buff.Tick(0.04f); pair.Tick(0.04f);
            float normal = baseline.DrainEvents().First(x => x.Kind == "attack").Amount; Check(pair.DrainEvents().First(x => x.Kind == "attack" && x.HeroId == "guan_yu").Amount > normal);
            Near(buff.Heroes[0].NextAttackAt, pair.Heroes[0].NextAttackAt); Check(pair.Enemies[0].VulnerableUntil > pair.Time);
        });
        Run("庞统传伤不循环与荀彧独立协同冷却", delegate {
            BattleSession link = Combat("pang_tong", 2); link.Tick(0.1f); Check(link.Enemies[1].Health < link.Enemies[1].MaxHealth); Check(link.DrainEvents().Count(x => x.Kind == "hit") <= 3);
            GameDatabase db = Fixture(); BattleSession assist = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "xun_yu" }); assist.TryRecruit("guan_yu", 0); assist.TryRecruit("xun_yu", 1); assist.Start(); assist.Tick(0.1f);
            Equal(1, assist.DrainEvents().Count(x => x.Kind == "attack" && x.HeroId == "xun_yu")); assist.Tick(1); Equal(0, assist.DrainEvents().Count(x => x.Kind == "attack" && x.HeroId == "xun_yu"));
        });
        Run("司马懿延后Boss预告且不能连续无限推迟", delegate {
            BattleSession b = Combat("sima_yi", 1, true); b.Tick(0.04f); EnemyUnit boss = b.Enemies[0]; Check(boss.NextAbilityAt > 6.5f);
            float next = boss.NextAbilityAt; b.Heroes[0].NextAttackAt = 0; b.Tick(0.04f); Equal(next, boss.NextAbilityAt);
        });
        Run("孙尚香优先残血且击退不重置独立技能冷却", delegate {
            BattleSession b = Combat("sun_shangxiang", 2); b.Tick(0.04f); b.DrainEvents(); b.Enemies[1].Health = 1; b.Heroes[0].NextAttackAt = 0; b.Tick(0.04f);
            Equal(1, b.Enemies.Count); Near(b.Time + 3, b.Heroes[0].NextAttackAt, .04f);
        });
        Run("命中事件能标识已击退的相邻敌人", delegate {
            BattleSession b = Combat("sun_shangxiang", 2); b.Tick(0.04f); b.DrainEvents();
            EnemyUnit victim = b.Enemies[1], survivor = b.Enemies[0]; float health = survivor.Health;
            Check(Point2.Distance(victim.Position, survivor.Position) < 36, "两个敌人应处于旧界面的近邻匹配距离内");
            victim.Health = 1; b.Heroes[0].NextAttackAt = 0; b.Tick(0.04f);
            Check(!b.Enemies.Contains(victim)); Check(b.Enemies.Contains(survivor)); Near(health, survivor.Health);
            BattleEvent hit = b.DrainEvents().Single(x => x.Kind == "hit");
            Equal(victim.Id, hit.TargetEnemyId);
        });
        Run("张辽溅射与姜维远近交替", delegate {
            BattleSession splash = Combat("zhang_liao", 2); splash.Tick(0.1f); Equal(2, splash.DrainEvents().Count(x => x.Kind == "attack"));
            BattleSession adaptive = Combat("jiang_wei", 2); adaptive.Tick(0.1f); Equal(2, adaptive.DrainEvents().Count(x => x.Kind == "attack")); adaptive.Tick(3.1f); Equal(1, adaptive.DrainEvents().Count(x => x.Kind == "attack"));
        });
        Run("华佗与鲁肃波末收益有整局上限", delegate {
            GameDatabase db = Fixture(); db.Heroes[0].Damage = 100000; db.Levels[0].Waves.Clear();
            db.Levels[0].CastleMaxHealth = 20;
            db.Enemies.Add(new EnemyDefinition { Id = "leaker", MaxHealth = 10, Speed = 10000, CastleDamage = 1 });
            db.Levels[0].Routes.Add(new RouteDefinition { Id = "side", Points = new List<Point2> { new Point2(1000, 1000), new Point2(1100, 1000) } });
            for (int i = 0; i < 20; i++) db.Levels[0].Waves.Add(new WaveDefinition { Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 1 }, new SpawnGroup { EnemyId = "leaker", RouteId = "side", Count = 1 } } });
            BattleSession b = new BattleSession(db, db.Levels[0], new[] { "guan_yu", "hua_tuo", "lu_su" }); b.TryRecruit("guan_yu", 0); b.TryRecruit("hua_tuo", 1); b.TryRecruit("lu_su", 2); b.Start(); b.Tick(30);
            Equal(BattleState.Victory, b.State); Equal(8, b.Heroes[1].BenefitUsed); Equal(8, b.CastleHealth); Equal(100, b.Heroes[2].BenefitUsed);
        });
        Run("华佗在Boss突破后恢复仍受满城防单波两点及整局八点限制", delegate {
            foreach (int damage in new[] { 1, 3 })
            {
                GameDatabase db = Fixture(); LevelDefinition level = db.Levels[0]; level.CastleMaxHealth = 30; level.Waves.Clear();
                db.Enemies[0].IsBoss = true; db.Enemies[0].Speed = 100000; db.Enemies[0].CastleDamage = damage; db.FindHero("hua_tuo").Range = 0;
                int waves = damage == 1 ? 10 : 5;
                for (int i = 0; i < waves; i++) level.Waves.Add(new WaveDefinition { StartDelay = i == 0 ? 0 : .5f, Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 1 } } });
                BattleSession b = new BattleSession(db, level, new[] { "hua_tuo" }); Equal(null, b.TryRecruit("hua_tuo", 0)); b.Start(); b.Tick(.2f);
                Equal(BattleState.Running, b.State); Equal(30 - damage + Math.Min(2, damage), b.CastleHealth); Equal(Math.Min(2, damage), b.Heroes[0].BenefitUsed);
                List<BattleEvent> events = b.DrainEvents(); b.Tick(10); events.AddRange(b.DrainEvents());
                Equal(BattleState.Victory, b.State); Equal(8, b.Heroes[0].BenefitUsed); Equal(30 - waves * damage + 8, b.CastleHealth); Equal(waves, b.LeakedCount); Equal(0, b.DefeatedCount);
                List<BattleEvent> heals = events.Where(x => x.Kind == "notice" && x.HeroId == "hua_tuo").ToList(); Equal(damage == 1 ? 8 : 4, heals.Count); Check(heals.All(x => x.Amount > 0 && x.Amount <= 2)); Near(8, heals.Sum(x => x.Amount)); Near(Math.Min(2, damage), heals[0].Amount);
            }
        });
        Run("Boss预告恢复与半血阶段", delegate {
            BattleSession b = Combat("guan_yu", 1, true); b.Tick(0.1f); EnemyUnit boss = b.Enemies[0]; boss.Health = boss.MaxHealth * 0.49f; boss.NextAbilityAt = b.Time; b.Tick(0.04f);
            Check(boss.Enraged); Check(boss.WarningUntil > b.Time); Check(b.DrainEvents().Any(x => x.Kind == "boss_phase")); b.Tick(1.6f); Check(boss.RecoverUntil > b.Time); Equal(0f, boss.WarningUntil);
        });
        Run("Boss入场发送独立通知", delegate {
            BattleSession b = Combat("guan_yu", 1, true); b.Tick(0.04f); Check(b.DrainEvents().Any(x => x.Kind == "notice" && x.Text.Contains("登场")));
        });
        Run("护卫在附近保护Boss并在离开后解除", delegate {
            BattleSession b = Combat("huang_zhong", 2, true); b.Tick(0.04f); EnemyUnit boss = b.Enemies[0], escort = b.Enemies[1];
            boss.Definition.Skill = "guard"; escort.Definition = new EnemyDefinition { Id = "escort", MaxHealth = 10000, Speed = 1, CastleDamage = 1, Skill = "shield" }; boss.NextAbilityAt = 100;
            b.Heroes[0].NextAttackAt = 0; b.DrainEvents(); b.Tick(0.04f); float guarded = b.DrainEvents().First(x => x.Kind == "hit").Amount;
            escort.Route = new RouteDefinition { Points = new List<Point2> { new Point2(0, 500), new Point2(10000, 500) } }; b.Heroes[0].NextAttackAt = 0; b.Tick(0.04f);
            Check(b.DrainEvents().First(x => x.Kind == "hit").Amount > guarded);
        });
        Run("Boss冲锋与召唤技能产生不同后果", delegate {
            BattleSession charge = Combat("guan_yu", 1, true); charge.Tick(0.04f); charge.Enemies[0].NextAbilityAt = 0; charge.Tick(1.7f); Check(charge.Enemies[0].HasteUntil > charge.Time);
            GameDatabase db = Fixture(); db.Enemies.Add(new EnemyDefinition { Id = "boss", CharacterId = "lu_su", IsBoss = true, Skill = "summon", MaxHealth = 10000, Speed = 1, CastleDamage = 2 }); db.Levels[0].Waves[0].Groups[0].EnemyId = "boss";
            BattleSession summon = new BattleSession(db, db.Levels[0], new string[0]); summon.Start(); summon.Tick(0.04f); summon.Enemies[0].NextAbilityAt = 0; summon.Tick(1.7f); Equal(3, summon.Enemies.Count);
        });
        Run("Boss冲锋结束后再进入恢复", delegate {
            BattleSession b = Combat("guan_yu", 1, true); b.Tick(0.04f); EnemyUnit boss = b.Enemies[0]; boss.MoveSpeed = 100; boss.NextAbilityAt = 0; b.Tick(1.6f); float travelled = boss.Travelled;
            b.Tick(0.5f); Check(boss.Travelled - travelled >= 75, "冲锋阶段应先加速推进，随后恢复");
        });
        Run("Boss护卫恢复与干扰守将", delegate {
            BattleSession guard = Combat("guan_yu", 1, true); guard.Tick(0.04f); EnemyUnit enemy = guard.Enemies[0]; enemy.Definition.Skill = "guard"; enemy.Health = 5000; enemy.NextAbilityAt = 0; guard.Tick(1.7f); Check(enemy.Health > 5000);
            BattleSession disrupt = Combat("guan_yu", 1, true); disrupt.Tick(0.04f); disrupt.Enemies[0].Definition.Skill = "disrupt"; disrupt.Enemies[0].NextAbilityAt = 0; disrupt.Tick(1.7f); Check(disrupt.Heroes[0].NextAttackAt > disrupt.Time + 0.8f);
        });
        Run("普通军医恢复附近敌军且护盾减伤", delegate {
            BattleSession healer = Combat("guan_yu", 2); healer.Tick(0.04f); healer.Enemies[0].Definition.Skill = "healer"; healer.Enemies[0].NextAbilityAt = 0; healer.Enemies[1].Health = 5000; healer.Tick(0.04f); Check(healer.Enemies[1].Health > 5000);
            BattleSession normal = Combat("guan_yu", 1), shield = Combat("guan_yu", 1); normal.Tick(0.04f); shield.Tick(0.04f); shield.Enemies[0].Definition.Skill = "shield"; normal.Heroes[0].NextAttackAt = shield.Heroes[0].NextAttackAt = 0; normal.DrainEvents(); shield.DrainEvents(); normal.Tick(0.04f); shield.Tick(0.04f);
            Check(normal.DrainEvents().First(x => x.Kind == "hit").Amount > shield.DrainEvents().First(x => x.Kind == "hit").Amount);
        });
        Run("非有限输入不污染时钟并限制倍速", delegate {
            BattleSession b = Session(); b.Start(); b.Tick(float.NaN); b.Tick(float.PositiveInfinity); b.Tick(-1); Equal(0f, b.Time);
            bool rejected = false; try { b.SetSpeed(0); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected);
        });
        Run("异常大帧限制推进量且后续继续消化", delegate {
            BattleSession b = Session(); b.Start(); b.Tick(600); Check(b.Time <= 30.01f); b.Tick(0.1f); Near(60, b.Time, 0.01f);
        });
        Run("配置校验接受有效目录并报告重复及无效路径", delegate {
            GameDatabase db = Fixture(); Equal(0, CatalogValidator.Validate(db).Count);
            db.Heroes.Add(db.Heroes[0]); db.Levels[0].Slots.Add(db.Levels[0].Slots[0]); db.Levels[0].Waves[0].Groups[0].RouteId = "missing";
            Check(CatalogValidator.Validate(db).Count >= 3);
        });
        Run("配置校验报告空字段与非有限数值", delegate {
            Check(CatalogValidator.Validate(null).Count > 0); GameDatabase db = Fixture(); db.Heroes[0].Damage = float.NaN; db.Enemies[0].Armor = float.PositiveInfinity; db.Levels[0].Waves[0].Groups[0].Interval = -1; db.Levels[0].Routes[0].Points.Clear();
            Check(CatalogValidator.Validate(db).Count >= 4); db.Levels[0].Waves = null; Check(CatalogValidator.Validate(db).Count > 0);
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("生成的25人5关目录通过规则校验", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            Equal(25, db.Heroes.Count); Equal(5, db.Levels.Count); List<string> issues = CatalogValidator.Validate(db); Check(issues.Count == 0, string.Join("；", issues.ToArray()));
            foreach (LevelDefinition level in db.Levels) { Check(level.Slots.Count >= 30); new BattleSession(db, level, level.RecommendedRoster); }
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("正式25人持续普攻实际间隔至多八成秒且倍率与技能独立", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            foreach (HeroDefinition definition in db.Heroes)
            {
                List<float>[][] timings = new List<float>[3][];
                for (int speed = 1; speed <= 3; speed++)
                {
                    timings[speed - 1] = FormalSustainedAttackTimes(definition, speed, false);
                    Check(timings[speed - 1][0].Count >= 19, definition.Id + "需在十五实秒内持续普攻至少十九次");
                    for (int n = 1; n < timings[speed - 1][0].Count; n++)
                    {
                        float interval = timings[speed - 1][0][n] - timings[speed - 1][0][n - 1];
                        Check(interval >= .4f - .001f && interval <= .8f + .001f, definition.Id + "实际普攻间隔需在0.4至0.8实秒，实际" + interval);
                    }
                }
                for (int speed = 1; speed < 3; speed++)
                {
                    Check(timings[0][0].SequenceEqual(timings[speed][0]), definition.Id + "普攻不能随进军倍率加快");
                    Check(timings[0][1].SequenceEqual(timings[speed][1]), definition.Id + "技能不能随进军倍率加快");
                }
                List<float>[] withoutBasics = FormalSustainedAttackTimes(definition, 1, true);
                Check(timings[0][1].SequenceEqual(withoutBasics[1]), definition.Id + "快普攻不能推进技能或蓄力冷却");
            }
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("终局参考布阵可通关", delegate {
            ReferenceFinale(new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0])));
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("虎牢推荐阵容正常经济参考全部Boss击退", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            string[] roster = { "guan_yu", "zhao_yun", "zhang_fei", "huang_zhong", "zhou_yu", "zhuge_liang", "sun_shangxiang", "cao_cao", "hua_tuo", "lu_su" };
            LevelDefinition level = db.FindLevel("hulao"); Check(level != null); Check(level.RecommendedRoster.SequenceEqual(roster));
            ReferenceNormalEconomy(db, level, "虎牢推荐", roster, new[] { 33, 13, 14, 12, 34, 17, 31, 23, 15, 32 });
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("黄巾推荐阵容正常经济参考可通关", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            ReferenceHuangjin(db, "推荐", db.FindLevel("huangjin").RecommendedRoster.ToArray(), new[] { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 });
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("黄巾替代阵容正常经济参考可通关", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            ReferenceHuangjin(db, "替代", new[] { "huang_zhong", "lu_xun", "sun_shangxiang", "zhou_yu", "taishi_ci", "jiang_wei", "zhuge_liang", "guo_jia", "cao_cao", "diao_chan" }, new[] { 28, 10, 12, 7, 5, 6, 20, 4, 3, 2 });
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("黄巾核心提前升二阶正常经济参考可通关", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            ReferenceHuangjin(db, "提前升阶", db.FindLevel("huangjin").RecommendedRoster.ToArray(), new[] { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 }, true);
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("官渡推荐阵容正常经济参考可通关", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            LevelDefinition level = db.FindLevel("guandu"); Check(level != null);
            string[] roster = { "guan_yu", "huang_zhong", "taishi_ci", "zhao_yun", "zhou_yu", "lu_xun", "zhuge_liang", "lv_meng", "pang_tong", "lu_su" };
            Check(level.RecommendedRoster.SequenceEqual(roster), "官渡推荐阵容须与固定参考阵容及顺序相同");
            ReferenceNormalEconomy(db, level, "官渡推荐", roster, new[] { 32, 24, 10, 27, 18, 19, 9, 1, 26, 25 });
        });
        if (args.Length > 0 && File.Exists(args[0])) Run("赤壁推荐阵容正常经济参考可通关", delegate {
            GameDatabase db = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            LevelDefinition level = db.FindLevel("chibi"); Check(level != null);
            string[] roster = { "guan_yu", "huang_zhong", "taishi_ci", "zhao_yun", "zhou_yu", "zhuge_liang", "ma_chao", "diao_chan", "sima_yi", "hua_tuo" };
            Check(level.RecommendedRoster.SequenceEqual(roster), "赤壁推荐阵容须与固定参考阵容及顺序相同");
            ReferenceNormalEconomy(db, level, "赤壁推荐", roster, new[] { 25, 31, 24, 21, 28, 20, 29, 27, 9, 16 });
        });
        if (args.Length > 1 && args[1] == "--simulate" && File.Exists(args[0])) Run("通用全关自动布阵必须全部胜利", delegate {
            Check(Simulate(new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<GameDatabase>(File.ReadAllText(args[0]))), "至少一关自动布阵未胜利");
        });
        Console.WriteLine("规则测试：通过 {0}，失败 {1}。", passed, failed); return failed == 0 ? 0 : 1;
    }

    // 反射允许先在旧规则上运行新增契约断言，避免以编译失败冒充红灯。
    private static T FeedbackValue<T>(object value, string name)
    {
        Check(value != null, "反馈对象不能为空"); System.Reflection.FieldInfo field = value.GetType().GetField(name);
        Check(field != null, "缺少反馈字段：" + name); return (T)field.GetValue(value);
    }
    private static System.Collections.IList[] ReadFeedback(BattleSession battle, System.Collections.IList[] reuse = null)
    {
        Type fieldType = typeof(BattleSession).Assembly.GetType("YanyiSandpan.Domain.CombatFieldSnapshot"), enemyType = typeof(BattleSession).Assembly.GetType("YanyiSandpan.Domain.EnemyFeedbackSnapshot");
        Check(fieldType != null && enemyType != null, "缺少区域或敌军反馈值类型");
        if (reuse == null) reuse = new[] { (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(fieldType)), (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(enemyType)) };
        System.Reflection.MethodInfo method = typeof(BattleSession).GetMethod("ReadCombatFeedback"); Check(method != null, "缺少只读反馈查询");
        method.Invoke(battle, new object[] { reuse[0], reuse[1] }); return reuse;
    }
    private static System.Collections.IList ReadLinks(BattleSession battle, System.Collections.IList reuse = null)
    {
        Type linkType = typeof(BattleSession).Assembly.GetType("YanyiSandpan.Domain.LinkFeedbackSnapshot"); Check(linkType != null, "缺少当前连环组反馈值类型");
        if (reuse == null) reuse = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(linkType));
        System.Reflection.MethodInfo method = typeof(BattleSession).GetMethod("ReadLinkFeedback"); Check(method != null, "缺少当前连环组只读查询"); method.Invoke(battle, new object[] { reuse }); return reuse;
    }
    private static GameDatabase Fixture()
    {
        GameDatabase db = new GameDatabase();
        for (int i = 0; i < Skills.Length; i++) db.Heroes.Add(new HeroDefinition { Id = Ids[i], Name = Ids[i], Skill = Skills[i], Cost = 10, Range = 400, Damage = 10, AttackInterval = 1 });
        db.Enemies.Add(new EnemyDefinition { Id = "soldier", Name = "步卒", MaxHealth = 10000, Speed = 1, CastleDamage = 1, GoldReward = 2 });
        LevelDefinition level = new LevelDefinition { Id = "test", Name = "演武", InitialGold = 1000, IncomePerSecond = 3, CastleMaxHealth = 10 };
        level.Routes.Add(new RouteDefinition { Id = "road", Points = new List<Point2> { new Point2(0, 0), new Point2(10000, 0) } });
        for (int i = 0; i < 34; i++) level.Slots.Add(new SlotDefinition { Id = i, Position = new Point2(100, i * 5) });
        level.Waves.Add(new WaveDefinition { Name = "第一波", Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 1 } } });
        db.Levels.Add(level); return db;
    }
    private static BattleSession Session(params string[] roster) { GameDatabase db = Fixture(); return new BattleSession(db, db.Levels[0], roster); }
    private static BattleSession Combat(string heroId, int count, bool boss = false)
    {
        GameDatabase db = Fixture(); db.Levels[0].Waves[0].Groups[0].Count = count;
        db.Enemies[0].IsBoss = boss; db.Enemies[0].CharacterId = boss ? "lu_su" : null; db.Enemies[0].Skill = boss ? "charge" : "normal";
        BattleSession b = new BattleSession(db, db.Levels[0], new[] { heroId }); b.TryRecruit(heroId, 0); b.Start(); return b;
    }
    private static void ReferenceFinale(GameDatabase db)
    {
        string[] roster = { "huang_zhong", "lu_xun", "zhou_yu", "cao_cao", "guo_jia", "taishi_ci", "zhuge_liang", "sun_shangxiang", "zhao_yun", "diao_chan" };
        int[] slots = { 39, 14, 24, 15, 13, 16, 5, 18, 17, 19 };
        LevelDefinition level = db.FindLevel("finale"); Equal(25, db.Heroes.Count); Check(level != null);
        Equal(10, roster.Distinct().Count()); Check(roster.Length <= 10); Check(level.RecommendedRoster.SequenceEqual(roster), "终局推荐阵容须与已验证参考阵容及顺序相同");
        BattleSession b = new BattleSession(db, level, roster); Equal(level.InitialGold, b.Gold); Equal(1f, b.Speed);
        foreach (string id in roster) Check(db.FindHero(id) != null && b.IsInRoster(id), "参考人物必须存在且不是本关Boss：" + id);
        Dictionary<string, int> expectedBossKills = new Dictionary<string, int>(), actualBossKills = new Dictionary<string, int>();
        foreach (SpawnGroup group in level.Waves.SelectMany(x => x.Groups))
        {
            EnemyDefinition enemy = db.FindEnemy(group.EnemyId); if (!enemy.IsBoss) continue;
            string text = enemy.Name + "被击退";
            if (!expectedBossKills.ContainsKey(text)) { expectedBossKills[text] = 0; actualBossKills[text] = 0; }
            expectedBossKills[text] += group.Count;
        }
        Check(expectedBossKills.Count > 0); int nextOrder = 0, spent = 0, rewards = 0;
        // 只通过会话操作消费军饷，不修改角色、敌军、收入或生命状态。
        for (int step = 0; step < 3200 && (b.State == BattleState.Preparation || b.State == BattleState.Running); step++)
        {
            while (nextOrder < roster.Length * 3)
            {
                int index = nextOrder % roster.Length, tier = nextOrder / roster.Length + 1;
                int cost = tier == 1 ? db.FindHero(roster[index]).Cost : b.GetUpgradeCost(slots[index]);
                if (b.Gold < cost) break;
                int before = b.Gold;
                Equal(null, tier == 1 ? b.TryRecruit(roster[index], slots[index]) : b.TryUpgrade(slots[index]));
                Equal(before - cost, b.Gold); spent += cost; nextOrder++;
            }
            if (b.State == BattleState.Preparation) b.Start(); b.Tick(0.25f);
            foreach (BattleEvent e in b.DrainEvents())
            {
                if (e.Kind != "kill") continue;
                rewards += (int)e.Amount; if (actualBossKills.ContainsKey(e.Text)) actualBossKills[e.Text]++;
            }
            int income = (int)Math.Floor((double)b.Time * level.IncomePerSecond + 0.0001);
            Equal(level.InitialGold + income + rewards - spent, b.Gold);
        }
        Equal(BattleState.Victory, b.State); Equal(level.Waves.Count, b.CurrentWave); Equal(10, b.Heroes.Count); Equal(0, b.Enemies.Count);
        foreach (KeyValuePair<string, int> expected in expectedBossKills) Equal(expected.Value, actualBossKills[expected.Key]);
        Console.WriteLine("  参考布阵：{0:F1}秒，城防{1}，Boss全部击退，军饷收支逐帧核对。", b.Time, b.CastleHealth);
    }
    private static bool Simulate(GameDatabase db)
    {
        bool allVictories = true;
        foreach (LevelDefinition level in db.Levels)
        {
            BattleSession b = new BattleSession(db, level, level.RecommendedRoster); int kills = 0, leaks = 0, warnings = 0, peak = 0;
            Dictionary<int, EnemyUnit> bosses = new Dictionary<int, EnemyUnit>();
            for (int step = 0; step < 4800 && (b.State == BattleState.Preparation || b.State == BattleState.Running); step++)
            {
                foreach (string heroId in level.RecommendedRoster)
                {
                    if (b.IsRecruited(heroId) || !b.IsInRoster(heroId) || b.Gold < db.FindHero(heroId).Cost) continue;
                    SlotDefinition best = null; float bestScore = float.MinValue;
                    foreach (SlotDefinition slot in level.Slots)
                    {
                        if (b.Heroes.Any(x => x.SlotId == slot.Id)) continue; float score = 0, range = BattleSession.GetMaximumRange(db.FindHero(heroId));
                        foreach (RouteDefinition route in level.Routes)
                        {
                            float defended = 0;
                            foreach (HeroUnit other in b.Heroes) defended += RouteCoverage(route, other.Position, BattleSession.GetMaximumRange(other.Definition)) * CoverageWeight(other.Definition);
                            score += RouteCoverage(route, slot.Position, range) / (0.15f + defended);
                        }
                        if (score > bestScore) { best = slot; bestScore = score; }
                    }
                    if (best != null) b.TryRecruit(heroId, best.Id);
                }
                if (b.Heroes.Count == level.RecommendedRoster.Count)
                    foreach (HeroUnit hero in b.Heroes.OrderBy(x => x.Level)) if (b.Gold >= b.GetUpgradeCost(hero.SlotId)) b.TryUpgrade(hero.SlotId);
                if (b.State == BattleState.Preparation) b.Start(); b.Tick(0.25f); peak = Math.Max(peak, b.Enemies.Count);
                foreach (EnemyUnit enemy in b.Enemies) if (enemy.Definition.IsBoss) bosses[enemy.Id] = enemy;
                foreach (BattleEvent e in b.DrainEvents()) { if (e.Kind == "kill") kills++; if (e.Kind == "leak") leaks++; if (e.Kind == "boss_warning") warnings++; }
            }
            Console.WriteLine("模拟 {0}：{1}，{2:F1}秒，波{3}/{4}，城防{5}，部署{6}，击退{7}，漏兵{8}，预告{9}，峰值敌军{10}", level.Name, b.State, b.Time, b.CurrentWave, b.TotalWaves, b.CastleHealth, b.Heroes.Count, kills, leaks, warnings, peak);
            Console.WriteLine("  布阵：" + string.Join("，", b.Heroes.Select(x => x.Definition.Id + "@" + x.SlotId).ToArray()));
            Console.WriteLine("  路线覆盖评分：" + string.Join("，", level.Routes.Select(route => route.Id + "=" + b.Heroes.Sum(hero => RouteCoverage(route, hero.Position, BattleSession.GetMaximumRange(hero.Definition)) * CoverageWeight(hero.Definition)).ToString("0.00")).ToArray()));
            foreach (EnemyUnit boss in bosses.Values) if (boss.Progress >= 1) Console.WriteLine("  突破Boss {0}：剩余{1:F0}/{2:F0}生命", boss.Definition.Name, boss.Health, boss.MaxHealth);
            foreach (EnemyUnit boss in b.Enemies) if (boss.Definition.IsBoss) Console.WriteLine("  存活Boss {0}：剩余{1:F0}/{2:F0}生命，距离城门{3:F0}", boss.Definition.Name, boss.Health, boss.MaxHealth, boss.RouteLength - boss.Travelled);
            if (b.State != BattleState.Victory) allVictories = false;
        }
        return allVictories;
    }
    private static void ReferenceHuangjin(GameDatabase db, string label, string[] roster, int[] slotIds, bool earlyCoreUpgrade = false)
    {
        ReferenceNormalEconomy(db, db.FindLevel("huangjin"), "黄巾" + label, roster, slotIds, earlyCoreUpgrade);
    }
    private static void ReferenceNormalEconomy(GameDatabase db, LevelDefinition level, string label, string[] roster, int[] slotIds, bool earlyCoreUpgrade = false)
    {
        Check(level != null); Equal(roster.Length, slotIds.Length);
        Equal(roster.Length, roster.Distinct().Count()); Equal(roster.Length, slotIds.Distinct().Count()); Check(roster.Length <= 10);
        BattleSession b = new BattleSession(db, level, roster); Equal(level.InitialGold, b.Gold); Equal(1f, b.Speed);
        foreach (string id in roster) Check(b.IsInRoster(id), "参考阵容不应包含本关Boss：" + id);
        int spent = 0, eventSpent = 0, rewards = 0, supplies = 0, kills = 0, leaks = 0;
        int peakEnemies = 0, peakTickKills = 0, denseKillTicks = 0;
        Queue<KeyValuePair<string, int>> expectedExpenseEvents = new Queue<KeyValuePair<string, int>>();
        float firstUpgrade = -1, waiting = 0;
        Dictionary<string, int> bosses = new Dictionary<string, int>();
        foreach (SpawnGroup group in level.Waves.SelectMany(x => x.Groups))
        {
            EnemyDefinition enemy = db.FindEnemy(group.EnemyId); if (!enemy.IsBoss) continue;
            string key = enemy.Name + "被击退"; if (!bosses.ContainsKey(key)) bosses[key] = 0; bosses[key] += group.Count;
        }
        Check(bosses.Count > 0, "参考关卡必须包含Boss");
        Dictionary<string, int> actualBosses = bosses.ToDictionary(x => x.Key, x => 0);
        for (int step = 0; step < 3200 && (b.State == BattleState.Preparation || b.State == BattleState.Running); step++)
        {
            // 同一固定优先级每0.25秒检查，不足时可先招募后续更便宜的武将；不注入军饷或改敌军。
            for (int i = 0; i < roster.Length; i++)
            {
                // 对照策略只改变消费顺序：先部署两名核心，给第二名升二阶后再继续扩编。
                if (earlyCoreUpgrade && firstUpgrade < 0 && i >= 2) break;
                if (b.IsRecruited(roster[i]) || b.Gold < db.FindHero(roster[i]).Cost) continue;
                int cost = db.FindHero(roster[i]).Cost, before = b.Gold;
                Equal(null, b.TryRecruit(roster[i], slotIds[i])); spent += cost; Equal(before - cost, b.Gold);
                expectedExpenseEvents.Enqueue(new KeyValuePair<string, int>("recruit:" + roster[i], cost));
            }
            if (earlyCoreUpgrade && firstUpgrade < 0 && b.Heroes.Count >= 2 && b.Gold >= b.GetUpgradeCost(slotIds[1]))
            {
                int cost = b.GetUpgradeCost(slotIds[1]), before = b.Gold;
                Equal(null, b.TryUpgrade(slotIds[1])); spent += cost; Equal(before - cost, b.Gold);
                expectedExpenseEvents.Enqueue(new KeyValuePair<string, int>("upgrade:" + roster[1], cost));
                firstUpgrade = b.Time; Check(b.Heroes.Count < roster.Length, "核心升级应在招满阵容之前");
            }
            if (b.Heroes.Count == roster.Length)
                foreach (HeroUnit hero in b.Heroes.OrderBy(x => x.Level))
                {
                    if (hero.Level >= BattleSession.MaxHeroLevel || b.Gold < b.GetUpgradeCost(hero.SlotId)) continue;
                    int cost = b.GetUpgradeCost(hero.SlotId), before = b.Gold;
                    Equal(null, b.TryUpgrade(hero.SlotId)); spent += cost; Equal(before - cost, b.Gold);
                    expectedExpenseEvents.Enqueue(new KeyValuePair<string, int>("upgrade:" + hero.Definition.Id, cost));
                    if (firstUpgrade < 0) firstUpgrade = b.Time;
                }
            if (b.State == BattleState.Preparation) b.Start();
            bool waveWasActive = b.IsWaveActive; int waveBefore = b.CurrentWave; float beforeTime = b.Time, countdownBefore = b.NextWaveCountdown;
            b.Tick(.25f);
            peakEnemies = Math.Max(peakEnemies, b.Enemies.Count);
            if (!waveWasActive && waveBefore > 0) waiting += Math.Min(countdownBefore, b.Time - beforeTime);
            else if (waveWasActive && !b.IsWaveActive && b.State == BattleState.Running)
                waiting += Math.Max(0, level.Waves[b.CurrentWave].StartDelay - b.NextWaveCountdown);
            int tickKills = 0;
            foreach (BattleEvent e in b.DrainEvents())
            {
                if (e.Kind == "kill") { rewards += (int)e.Amount; kills++; tickKills++; if (actualBosses.ContainsKey(e.Text)) actualBosses[e.Text]++; }
                else if (e.Kind == "leak") leaks++;
                else if (e.Kind == "notice" && e.Text != null && e.Text.StartsWith("鲁肃补给军饷", StringComparison.Ordinal)) supplies += (int)e.Amount;
                else if (e.Kind == "recruit" || e.Kind == "upgrade")
                {
                    Check(expectedExpenseEvents.Count > 0, "消费事件必须对应成功的会话操作");
                    KeyValuePair<string, int> expected = expectedExpenseEvents.Dequeue();
                    Equal(expected.Key, e.Kind + ":" + e.HeroId);
                    if (e.Kind == "recruit") Equal(expected.Value, (int)e.Amount);
                    // 升级事件Amount表示新阶位，消费按对应成功操作记录的实际价格核对。
                    eventSpent += expected.Value;
                }
            }
            // 仅记录每0.25秒采样的节奏统计，不修改部署、经济或通关断言。
            peakTickKills = Math.Max(peakTickKills, tickKills); if (tickKills >= 2) denseKillTicks++;
            int income = (int)Math.Floor((double)b.Time * level.IncomePerSecond + .0001);
            Equal(0, expectedExpenseEvents.Count); Equal(spent, eventSpent); Equal(eventSpent, b.GoldSpent);
            Equal(level.InitialGold + income + rewards + supplies - eventSpent, b.Gold);
            Equal(kills, b.DefeatedCount); Equal(leaks, b.LeakedCount);
        }
        Console.WriteLine("  {0}基线：{1}，{2:F1}秒，首次升级{3:F1}秒，清波等待{4:F1}秒，城防{5}，击退{6}，漏兵{7}，消费{8}，结余{9}，补给{10}", label, b.State, b.Time, firstUpgrade, waiting, b.CastleHealth, kills, leaks, spent, b.Gold, supplies);
        Console.WriteLine("  固定布阵：" + string.Join("，", roster.Select((id, index) => id + "@" + slotIds[index]).ToArray()));
        Console.WriteLine("  节奏采样：峰值存活{0}，单次0.25秒最多击退{1}，两次及以上击退采样{2}", peakEnemies, peakTickKills, denseKillTicks);
        Console.WriteLine("  收支：初始{0} + 计时{1} + 击退{2} + 补给{3} - 消费{4} = 结余{5}；Boss {6}", level.InitialGold, (int)Math.Floor((double)b.Time * level.IncomePerSecond + .0001), rewards, supplies, spent, b.Gold,
            string.Join("，", actualBosses.Select(x => x.Key + " " + x.Value + "/" + bosses[x.Key]).ToArray()));
        Equal(BattleState.Victory, b.State); Equal(level.Waves.Count, b.CurrentWave); Equal(roster.Length, b.Heroes.Count); Check(firstUpgrade >= 0); Equal("", b.DefeatReason);
        if (earlyCoreUpgrade) Check(firstUpgrade <= 60, "核心应在首分钟内完成二阶升级");
        foreach (KeyValuePair<string, int> expected in bosses) Equal(expected.Value, actualBosses[expected.Key]);
        Near(level.Waves.Skip(1).Sum(x => x.StartDelay), waiting, .02f);
        Equal(0, b.RemainingEnemies); Equal(0, b.Enemies.Count); Check(!b.IsWaveActive); Near(0, b.NextWaveCountdown);
    }
    // 按路线长度归一化，再优先补齐覆盖较少的路线；评分只指导测试布阵，不等同于实战伤害。
    private static float RouteCoverage(RouteDefinition route, Point2 position, float range)
    {
        float score = 0, total = 0;
        for (int n = 1; n < route.Points.Count; n++)
        {
            Point2 a = route.Points[n - 1], z = route.Points[n]; float distance = Point2.Distance(a, z); total += distance;
            int samples = Math.Max(1, (int)(distance / 25));
            for (int k = 0; k < samples; k++)
            {
                float t = (k + 0.5f) / samples;
                Point2 p = new Point2(a.X + (z.X - a.X) * t, a.Y + (z.Y - a.Y) * t);
                float d = Point2.Distance(position, p); if (d < range) score += (1 - d / range) * distance / samples;
            }
        }
        return score / Math.Max(1, total);
    }
    private static float CoverageWeight(HeroDefinition hero)
    {
        if (hero.Skill == "heal" || hero.Skill == "supply" || hero.Skill == "assist") return 0.35f;
        if (hero.Skill == "buff" || hero.Skill == "vulnerable" || hero.Skill == "link") return 0.6f;
        return 1;
    }
    private static List<float>[] FormalSustainedAttackTimes(HeroDefinition definition, int speed, bool suppressBasics)
    {
        // 正式角色对持续在射程内的耐久目标出手；不改变角色数值，排除行军与击杀造成的空档。
        GameDatabase db = new GameDatabase(); db.Heroes.Add(definition);
        db.Enemies.Add(new EnemyDefinition { Id = "target", Name = "耐久目标", MaxHealth = 100000, Speed = 0 });
        LevelDefinition level = new LevelDefinition { Id = "tempo", InitialGold = definition.Cost, CastleMaxHealth = 30 };
        level.Slots.Add(new SlotDefinition { Id = 1, Position = new Point2(0, 0) });
        level.Routes.Add(new RouteDefinition { Id = "road", Points = new List<Point2> { new Point2(0, 0), new Point2(10000, 0) } });
        level.Waves.Add(new WaveDefinition { Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "target", RouteId = "road", Count = 1 } } });
        BattleSession battle = new BattleSession(db, level, new[] { definition.Id });
        Equal(null, battle.TryRecruit(definition.Id, 1));
        if (suppressBasics) battle.Heroes[0].NextBasicAt = 1000;
        battle.SetSpeed(speed); battle.Start();
        List<float>[] result = { new List<float>(), new List<float>() };
        for (int step = 0; step < 450; step++)
        {
            battle.Tick(1f / 30f);
            foreach (BattleEvent e in battle.DrainEvents())
            {
                if (e.Kind == "basic_attack") result[0].Add(battle.Time);
                else if (e.Kind == "attack") result[1].Add(battle.Time);
            }
        }
        Check(battle.Enemies.Count == 1 && battle.Enemies[0].Health > 0, "耐久目标须全程存在");
        return result;
    }
    private static List<float> AttackTimes(BattleSession battle, string heroId, float duration)
    {
        List<float> result = new List<float>();
        for (int step = 0; step < (int)Math.Ceiling(duration * 30); step++)
        {
            battle.Tick(1f / 30f);
            foreach (BattleEvent e in battle.DrainEvents()) if (e.Kind == "attack" && e.HeroId == heroId) result.Add(battle.Time);
        }
        return result;
    }
    private static T Metric<T>(BattleSession battle, string name)
    {
        var property = typeof(BattleSession).GetProperty(name);
        Check(property != null && property.CanRead, "缺少只读战斗接口：" + name);
        Check(!property.CanWrite || !property.GetSetMethod(true).IsPublic, "战斗情报不可公开写入：" + name);
        return (T)property.GetValue(battle, null);
    }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("通过：" + name); } catch (Exception e) { failed++; Console.WriteLine("失败：" + name + "：" + e.Message); } }
    private static void Check(bool value, string message = "断言未满足") { if (!value) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) { Check(EqualityComparer<T>.Default.Equals(expected, actual), "预期 " + expected + "，实际 " + actual); }
    private static void Near(float expected, float actual, float tolerance = 0.001f) { Check(Math.Abs(expected - actual) <= tolerance, "预期约 " + expected + "，实际 " + actual); }
}
