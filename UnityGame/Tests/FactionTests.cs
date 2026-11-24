using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

internal static class FactionTests
{
    private static int passed, failed;
    private static GameDatabase source;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(float expected, float actual) { Check(Math.Abs(expected - actual) < .02f, "期望 " + expected + "，实际 " + actual); }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("通过 " + name); } catch (Exception e) { failed++; Console.WriteLine("失败 " + name + "：" + e.Message); } }
    private static FieldInfo FactionField { get { return typeof(HeroDefinition).GetField("Faction"); } }
    private static T Value<T>(object item, string name) { FieldInfo f = item.GetType().GetField(name); Check(f != null, "缺少事件字段 " + name); return (T)f.GetValue(item); }
    private static int Query(BattleSession b, string method, string faction) { MethodInfo m = typeof(BattleSession).GetMethod(method); Check(m != null, "缺少查询 " + method); return (int)m.Invoke(b, new object[] { faction }); }
    private static GameDatabase Fixture()
    {
        GameDatabase db = new JavaScriptSerializer().Deserialize<GameDatabase>(new JavaScriptSerializer().Serialize(source));
        foreach (HeroDefinition h in db.Heroes) { h.Cost = 10; h.Damage = 100; h.Range = 400; h.BasicDamageScale = 1; h.SkillDamageScale = 1; h.BasicAttackInterval = 50; h.SkillCooldown = 50; }
        db.Enemies.Clear(); db.Enemies.Add(new EnemyDefinition { Id = "soldier", Name = "测试兵", Skill = "normal", MaxHealth = 100000, Speed = .01f, CastleDamage = 1, GoldReward = 2 });
        LevelDefinition level = new LevelDefinition { Id = "test", InitialGold = 10000, CastleMaxHealth = 100 };
        level.Routes.Add(new RouteDefinition { Id = "road", Points = new List<Point2> { new Point2(0, 0), new Point2(100000, 0) } });
        for (int i = 0; i < 12; i++) level.Slots.Add(new SlotDefinition { Id = i, Position = new Point2(i * 300, 0) });
        level.Waves.Add(new WaveDefinition { Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "soldier", RouteId = "road", Count = 5 } } });
        db.Levels.Clear(); db.Levels.Add(level); return db;
    }
    private static BattleSession Create(GameDatabase db, string[] roster, bool enabled)
    {
        ConstructorInfo constructor = typeof(BattleSession).GetConstructor(new[] { typeof(GameDatabase), typeof(LevelDefinition), typeof(IEnumerable<string>), typeof(bool) });
        return constructor == null ? new BattleSession(db, db.Levels[0], roster) : (BattleSession)constructor.Invoke(new object[] { db, db.Levels[0], roster, enabled });
    }
    private static BattleSession Combat(string id, int count, bool enabled)
    {
        GameDatabase db = Fixture(); string faction = (string)FactionField.GetValue(db.FindHero(id));
        string[] roster = new[] { id }.Concat(db.Heroes.Where(h => h.Id != id && (string)FactionField.GetValue(h) == faction).Select(h => h.Id)).Take(Math.Max(count, 1)).ToArray();
        BattleSession b = Create(db, roster, enabled);
        foreach (string heroId in roster.Take(count)) Check(b.TryRecruit(heroId, b.Heroes.Count) == null, "招募失败");
        foreach (HeroUnit h in b.Heroes) { h.NextBasicAt = 100; h.NextAttackAt = 100; }
        b.Start(); b.Tick(.04f); b.DrainEvents(); return b;
    }
    private static List<BattleEvent> Fire(BattleSession b, bool basic)
    {
        if (basic) b.Heroes[0].NextBasicAt = 0; else b.Heroes[0].NextAttackAt = 0;
        b.Tick(.04f); return b.DrainEvents();
    }
    private static float FirstAmount(List<BattleEvent> events, string kind, string id) { return events.First(e => e.Kind == kind && e.HeroId == id).Amount; }
    private static int Main(string[] args)
    {
        source = new JavaScriptSerializer().Deserialize<GameDatabase>(File.ReadAllText(args[0]));
        Run("25将固定阵营字段及完整配置", delegate {
            Check(FactionField != null, "缺少 HeroDefinition.Faction，正式配置尚无军势归属");
            Check(source.Heroes.Count == 25, "应为25将");
            string[] factions = { "wei", "shu", "wu", "qun" }; int[] expected = { 6, 8, 6, 5 };
            for (int i = 0; i < factions.Length; i++) Check(source.Heroes.Count(h => (string)FactionField.GetValue(h) == factions[i]) == expected[i], factions[i] + "归属数量不符");
            string[][] expectedIds = { new[] { "xu_chu", "guo_jia", "cao_cao", "xun_yu", "sima_yi", "zhang_liao" }, new[] { "guan_yu", "huang_zhong", "zhao_yun", "zhang_fei", "zhuge_liang", "ma_chao", "pang_tong", "jiang_wei" }, new[] { "taishi_ci", "zhou_yu", "lu_xun", "lv_meng", "sun_shangxiang", "lu_su" }, new[] { "lv_bu", "yuan_shao", "zhang_jiao", "diao_chan", "hua_tuo" } };
            for (int i = 0; i < factions.Length; i++) foreach (string id in expectedIds[i]) Check((string)FactionField.GetValue(source.FindHero(id)) == factions[i], id + "固定归属错误");
        });
        Run("旧配置空值兼容未知阵营拒绝", delegate {
            GameDatabase db = Fixture(); FactionField.SetValue(db.Heroes[0], null); Check(!CatalogValidator.Validate(db).Any(e => e.Contains("阵营")), "空阵营应兼容");
            FactionField.SetValue(db.Heroes[0], "unknown"); Check(CatalogValidator.Validate(db).Any(e => e.Contains("阵营")), "未知阵营必须拒绝");
        });
        Run("0至5实际招募计数升级失败不计数", delegate {
            GameDatabase db = Fixture(); string[] ids = db.Heroes.Where(h => (string)FactionField.GetValue(h) == "wei").Select(h => h.Id).Take(5).ToArray(); BattleSession b = Create(db, ids, true);
            for (int i = 0; i <= 5; i++) { Check(Query(b, "GetFactionCount", "wei") == i, "实际人数错误"); Check(Query(b, "GetFactionTier", "wei") == (i >= 4 ? 4 : i >= 2 ? 2 : 0), "档位错误"); if (i < 5) Check(b.TryRecruit(ids[i], i) == null, "招募失败"); }
            Check(b.TryUpgrade(0) == null, "升级失败"); Check(b.TryRecruit(ids[0], 7) != null, "重复招募应失败"); Check(Query(b, "GetFactionCount", "wei") == 5, "重复人数");
            Check(b.TryRecruit(ids[1], -1) != null && Query(b, "GetFactionCount", "wu") == 0, "失败不得计数");
        });
        Run("规则版本与默认关闭旧构造", delegate {
            BattleSession enabled = Combat("xu_chu", 2, true), disabled = Combat("xu_chu", 2, false);
            PropertyInfo flag = typeof(BattleSession).GetProperty("FactionEnabled"), version = typeof(BattleSession).GetProperty("RuleVersion"); Check(flag != null && version != null, "缺少规则版本接口");
            Check((bool)flag.GetValue(enabled, null) && (string)version.GetValue(enabled, null) == "faction-army-v1", "军势版本错误"); Check(!(bool)flag.GetValue(disabled, null) && (string)version.GetValue(disabled, null) == "growth-tempo-v2", "旧版本错误");
            GameDatabase db = Fixture(); Check(!(bool)flag.GetValue(new BattleSession(db, db.Levels[0], new[] { "xu_chu" }), null), "三参数构造必须默认关闭");
        });
        Run("隔离220边界与跨营邻居无效坐标", delegate {
            HeroUnit h = new HeroUnit { Position = new Point2(0, 0) }, other = new HeroUnit { Position = new Point2(220, 0) }; Type rules = typeof(BattleSession).Assembly.GetType("YanyiSandpan.Domain.FactionRules"); MethodInfo method = rules.GetMethod("IsIsolated");
            foreach (float distance in new[] { 219.99f, 220f, 220.01f }) { other.Position = new Point2(distance, 0); Check((bool)method.Invoke(null, new object[] { h, new[] { h, other } }) == (distance >= 220), "220边界错误"); }
            other.Position = new Point2(float.NaN, 0); Check(!(bool)method.Invoke(null, new object[] { h, new[] { h, other } }), "无效坐标不得合格");
            GameDatabase db = Fixture(); BattleSession b = Create(db, new[] { "lv_bu", "hua_tuo", "guan_yu" }, true); b.TryRecruit("lv_bu", 0); b.TryRecruit("hua_tuo", 2); b.TryRecruit("guan_yu", 1); b.Heroes[2].Position = new Point2(219.99f, 0); MethodInfo query = typeof(BattleSession).GetMethod("IsFactionIsolated"); Check(query != null && !(bool)query.Invoke(b, new object[] { b.Heroes[0] }), "跨营友军应影响资格");
        });
        Run("魏普攻真实健康差两档与技能不放大", delegate {
            foreach (int count in new[] { 1, 2, 3, 4, 5 }) { BattleSession b = Combat("xu_chu", count, true); float before = b.Enemies[0].Health; List<BattleEvent> events = Fire(b, true); float expected = count >= 4 ? 112 : count >= 2 ? 106 : 100; Near(expected, FirstAmount(events, "basic_attack", "xu_chu")); Near(expected, before - b.Enemies[0].Health); }
            Near(FirstAmount(Fire(Combat("xu_chu", 2, false), false), "attack", "xu_chu"), FirstAmount(Fire(Combat("xu_chu", 2, true), false), "attack", "xu_chu"));
        });
        Run("蜀精英Boss受益普通盾兵不受益", delegate {
            foreach (string type in new[] { "elite", "boss", "shield" }) { BattleSession on = Combat("huang_zhong", 2, true), off = Combat("huang_zhong", 2, false); foreach (BattleSession b in new[] { on, off }) { b.Enemies[0].Definition.Id = type; b.Enemies[0].Definition.IsBoss = type == "boss"; } float factor = type == "shield" ? 1 : 1.06f; Near(FirstAmount(Fire(off, true), "basic_attack", "huang_zhong") * factor, FirstAmount(Fire(on, true), "basic_attack", "huang_zhong")); Near(FirstAmount(Fire(off, false), "attack", "huang_zhong") * factor, FirstAmount(Fire(on, false), "attack", "huang_zhong")); }
        });
        Run("吴弓技伏击火区点燃只乘一次", delegate {
            foreach (string id in new[] { "taishi_ci", "sun_shangxiang", "lv_meng", "zhou_yu", "lu_xun" }) { BattleSession on = Combat(id, 2, true), off = Combat(id, 2, false); if (id == "lv_meng") { foreach (BattleSession b in new[] { on, off }) { b.Heroes[0].Ambushed.Clear(); } } float expected = FirstAmount(Fire(off, false), "attack", id) * 1.06f; Near(expected, FirstAmount(Fire(on, false), "attack", id)); if (id == "lu_xun") Near(off.Enemies[0].BurnDamage * 1.06f, on.Enemies[0].BurnDamage); }
            BattleSession fireOn = Combat("zhou_yu", 2, true), fireOff = Combat("zhou_yu", 2, false); Fire(fireOn, false); Fire(fireOff, false); float beforeOn = fireOn.Enemies[0].Health, beforeOff = fireOff.Enemies[0].Health; fireOn.Tick(.2f); fireOff.Tick(.2f); Near((beforeOff - fireOff.Enemies[0].Health) * 1.06f, beforeOn - fireOn.Enemies[0].Health);
        });
        Run("群孤立普通与技能受益邻居靠近取消", delegate {
            BattleSession on = Combat("zhang_jiao", 2, true), off = Combat("zhang_jiao", 2, false); Near(FirstAmount(Fire(off, true), "basic_attack", "zhang_jiao") * 1.08f, FirstAmount(Fire(on, true), "basic_attack", "zhang_jiao")); Near(FirstAmount(Fire(off, false), "attack", "zhang_jiao") * 1.08f, FirstAmount(Fire(on, false), "attack", "zhang_jiao")); on.Heroes[1].Position = new Point2(219, 0); Near(100, FirstAmount(Fire(on, true), "basic_attack", "zhang_jiao"));
        });
        Run("特色施放事件序列多目标唯一且成长实时", delegate {
            BattleSession b = Combat("taishi_ci", 2, true); b.Heroes[0].Level = 3; List<BattleEvent> events = Fire(b, false); BattleEvent cast = events.Single(e => e.Kind == "skill_cast" && e.HeroId == "taishi_ci"); int seq = Value<int>(cast, "AttackSequenceId"); Check(seq > 0 && cast.HeroLevel == 3, "施放序列与等级错误"); foreach (BattleEvent e in events.Where(e => e.Kind == "attack" && e.HeroId == cast.HeroId)) { Check(Value<int>(e, "AttackSequenceId") == seq && e.HeroLevel == 3, "多目标序列不一致"); Near(1.06f, Value<float>(e, "FactionMultiplier")); }
        });
        Run("暂停倍率终局不伪造技能事件", delegate {
            BattleSession b = Combat("taishi_ci", 2, true); b.Heroes[0].NextAttackAt = 0; b.SetPaused(true); b.Tick(10); Check(b.DrainEvents().Count == 0, "暂停不得出手"); b.SetPaused(false); b.SetSpeed(3); Check(Fire(b, false).Count(e => e.Kind == "skill_cast" && e.HeroId == "taishi_ci") == 1, "倍率应沿用实秒冷却"); b.Heroes[0].NextAttackAt = 100; foreach (EnemyUnit e in b.Enemies) e.Health = 0; b.Enemies.Clear(); b.Tick(.1f); b.Heroes[0].NextAttackAt = 0; b.DrainEvents(); b.Tick(10); Check(!b.DrainEvents().Any(e => e.Kind == "skill_cast"), "终局不得出手");
        });
        Run("号令追加魏普攻含光环协射不继承魏增幅独立冷却", delegate {
            foreach (bool enabled in new[] { false, true }) {
                GameDatabase db = Fixture(); db.Levels[0].Slots[1].Position = new Point2(100, 0); db.Levels[0].Slots[2].Position = new Point2(200, 0);
                BattleSession b = Create(db, new[] { "cao_cao", "xu_chu", "xun_yu" }, enabled); b.TryRecruit("cao_cao", 0); b.TryRecruit("xu_chu", 1); b.TryRecruit("xun_yu", 2); b.Heroes[0].Level = 3;
                foreach (HeroUnit h in b.Heroes) { h.NextBasicAt = h.NextAttackAt = 100; } b.Start(); b.Tick(.04f); b.DrainEvents(); List<BattleEvent> events = Fire(b, false);
                BattleEvent extra = events.Single(e => e.Kind == "basic_attack" && e.HeroId == "xu_chu"); Near(124 * (enabled ? 1.06f : 1), extra.Amount);
                BattleEvent assist = events.Single(e => e.Kind == "attack" && e.HeroId == "xun_yu"); Near(124 * .8f, assist.Amount); Near(1, assist.FactionMultiplier); Check(assist.AttackSequenceId != events.Single(e => e.Kind == "skill_cast" && e.HeroId == "cao_cao").AttackSequenceId, "协射应有自己的施放序列"); Near(b.Time + 1.5f, b.Heroes[2].AssistReadyAt);
                b.Heroes[1].NextBasicAt = 0; b.Tick(.04f); Check(!b.DrainEvents().Any(e => e.Kind == "attack" && e.HeroId == "xun_yu"), "不得绕过1.5秒独立冷却");
            }
        });
        Run("连环继承源实际结算30百分比不重乘不递归", delegate {
            BattleSession b = Combat("huang_zhong", 2, true); EnemyUnit sourceEnemy = b.Enemies[0], receiver = b.Enemies[1]; sourceEnemy.Definition.IsBoss = true; sourceEnemy.Definition.Armor = .2f;
            sourceEnemy.VulnerableUntil = b.Time + 10; receiver.VulnerableUntil = b.Time + 10;
            LinkGroup group = new LinkGroup { Id = 99, Owner = b.Heroes[0], Until = b.Time + 10 }; foreach (EnemyUnit e in b.Enemies.Take(3)) { group.Members.Add(e.Id); e.Links = group; } float before = receiver.Health;
            List<BattleEvent> events = Fire(b, true); float raw = FirstAmount(events, "basic_attack", "huang_zhong"), expected = raw * .8f * 1.25f * .3f * .8f * 1.25f;
            Near(expected, before - receiver.Health); Check(events.Count(e => e.Kind == "link_hit") == 2, "传伤仅一层"); Near(expected, events.Single(e => e.Kind == "link_hit" && e.TargetEnemyId == receiver.Id).Amount); Check(events.Count(e => e.Kind == "faction_hit") == 1, "传伤不得再次触发军势增强");
        });
        Run("吴旧火区招募4人不追溯新火区使用4人档", delegate {
            GameDatabase db = Fixture(); string[] roster = { "zhou_yu", "lu_xun", "taishi_ci", "lv_meng" }; BattleSession b = Create(db, roster, true); b.TryRecruit(roster[0], 0); b.TryRecruit(roster[1], 1); foreach (HeroUnit h in b.Heroes) h.NextBasicAt = h.NextAttackAt = 100; b.Start(); b.Tick(.04f); Fire(b, false);
            b.TryRecruit(roster[2], 2); b.TryRecruit(roster[3], 3); foreach (HeroUnit h in b.Heroes) h.NextBasicAt = h.NextAttackAt = 100;
            float before = b.Enemies[0].Health; b.Tick(.1f); Near(53 * .1f, before - b.Enemies[0].Health);
            BattleEvent newShot = Fire(b, false).First(e => e.Kind == "attack" && e.HeroId == "zhou_yu"); Near(56, newShot.Amount); Check(newShot.FactionTier == 4, "新施放应升档");
        });
        Run("陆逊传播复制旧火种与余烬不重新增强", delegate {
            BattleSession on = Combat("lu_xun", 2, true), off = Combat("lu_xun", 2, false);
            foreach (BattleSession b in new[] { on, off }) { b.Enemies[0].BurnUntil = 0; Fire(b, false); EnemyUnit seed = b.Enemies.First(e => e.BurnDamage > 0); foreach (EnemyUnit e in b.Enemies.Where(e => e != seed)) { e.BurnUntil = 0; e.BurnDamage = 0; } seed.BurnBudget.Visited.Clear(); seed.BurnBudget.Visited.Add(seed.Id); seed.Health = .01f; b.Tick(.04f); }
            Near(off.Enemies.First(e => e.BurnDamage > 0).BurnDamage * 1.06f, on.Enemies.First(e => e.BurnDamage > 0).BurnDamage);
            BattleSession fireOn = Combat("zhou_yu", 2, true), fireOff = Combat("zhou_yu", 2, false); foreach (BattleSession b in new[] { fireOn, fireOff }) { b.Heroes[0].Level = 3; Fire(b, false); b.Tick(.04f); } Near(fireOff.Enemies[0].EmberDamage * 1.06f, fireOn.Enemies[0].EmberDamage);
        });
        Run("袁绍第二轮招募4群及邻居改变沿用初始快照", delegate {
            GameDatabase db = Fixture(); string[] roster = { "yuan_shao", "hua_tuo", "zhang_jiao", "diao_chan", "guan_yu" }; BattleSession b = Create(db, roster, true); b.TryRecruit(roster[0], 0); b.TryRecruit(roster[1], 1); foreach (HeroUnit h in b.Heroes) h.NextBasicAt = h.NextAttackAt = 100; b.Heroes[0].Level = 3; b.Start(); b.Tick(.04f);
            List<BattleEvent> first = Fire(b, false); BattleEvent firstShot = first.First(e => e.Kind == "attack" && e.HeroId == roster[0]); Check(firstShot.FactionTier == 2, "初始2人档");
            b.TryRecruit(roster[2], 2); b.TryRecruit(roster[3], 3); b.TryRecruit(roster[4], 4); b.Heroes[4].Position = new Point2(100, 0); foreach (HeroUnit h in b.Heroes) h.NextBasicAt = h.NextAttackAt = 100;
            b.Tick(.65f); List<BattleEvent> second = b.DrainEvents(); BattleEvent next = second.First(e => e.Kind == "attack" && e.HeroId == roster[0]); Near(firstShot.Amount, next.Amount); Check(next.FactionTier == 2 && next.AttackSequenceId == firstShot.AttackSequenceId, "第二轮沿用档位序列"); Check(second.Count(e => e.Kind == "skill_cast" && e.HeroId == roster[0]) == 0, "第二轮不得再发施放");
        });
        Run("吕布蓄力中招募升阶只更新成长不追溯军势资格", delegate {
            GameDatabase db = Fixture(); string[] roster = { "lv_bu", "hua_tuo", "yuan_shao", "zhang_jiao", "guan_yu" }; BattleSession b = Create(db, roster, true); b.TryRecruit(roster[0], 0); b.TryRecruit(roster[1], 1); foreach (HeroUnit h in b.Heroes) h.NextBasicAt = h.NextAttackAt = 100; b.Start(); b.Tick(.04f);
            BattleEvent charge = Fire(b, false).Single(e => e.Kind == "skill_charge"); Near(1, charge.Amount); Near(charge.OccurredAt + 1, charge.ExpiresAt);
            b.TryRecruit(roster[2], 2); b.TryRecruit(roster[3], 3); b.TryRecruit(roster[4], 4); b.Heroes[4].Position = new Point2(100, 0); foreach (HeroUnit h in b.Heroes.Skip(1)) h.NextBasicAt = h.NextAttackAt = 100; Check(b.TryUpgrade(0) == null, "蓄力中允许升级");
            b.Tick(1.1f); List<BattleEvent> events = b.DrainEvents(); BattleEvent shot = events.First(e => e.Kind == "attack" && e.HeroId == roster[0]); Near(100 * HeroGrowth.Resolve(b.Heroes[0].Definition, 2).DamageMultiplier * 3.5f * 1.08f, shot.Amount); Check(shot.HeroLevel == 2 && shot.FactionTier == 2 && shot.AttackSequenceId == charge.AttackSequenceId, "蓄力应保留军势快照并用释放成长"); Check(events.Count(e => e.Kind == "skill_cast" && e.HeroId == roster[0]) == 1, "真实释放一次");
        });
        Run("五关各营四人合法可达与不同英雄去重", delegate {
            foreach (LevelDefinition level in source.Levels) { BattleSession b = new BattleSession(source, level, new string[0], true); foreach (string faction in new[] { "wei", "shu", "wu", "qun" }) Check(source.Heroes.Count(h => h.Faction == faction && !level.BossHeroIds.Contains(h.Id) && !level.Waves.SelectMany(w => w.Groups).Any(g => source.FindEnemy(g.EnemyId).IsBoss && source.FindEnemy(g.EnemyId).CharacterId == h.Id)) >= 4, level.Id + faction + "不足4名合法角色"); Check(b.GetFactionCount("wei") == 0, "未招募不计数"); }
            HeroUnit hero = new HeroUnit { Definition = source.FindHero("xu_chu") }; Check(FactionRules.CountDeployed(new[] { hero, hero, new HeroUnit { Definition = hero.Definition } }, "wei") == 1, "相同英雄ID去重");
        });
        Run("纯军势查询完整来源矩阵不增强继承伤害", delegate {
            foreach (string faction in new[] { "wei", "shu", "wu", "qun", "", "unknown" }) foreach (int tier in new[] { 0, 2, 4 }) foreach (FactionDamageKind kind in Enum.GetValues(typeof(FactionDamageKind))) foreach (bool strong in new[] { false, true }) foreach (bool isolated in new[] { false, true }) {
                bool direct = kind == FactionDamageKind.Basic || kind == FactionDamageKind.SkillDirect;
                bool standard = faction == "wei" && kind == FactionDamageKind.Basic || faction == "shu" && direct && strong || faction == "wu" && (kind == FactionDamageKind.SkillDirect || kind == FactionDamageKind.SkillPeriodic);
                float expected = tier == 0 ? 1 : standard ? (tier == 2 ? 1.06f : 1.12f) : faction == "qun" && direct && isolated ? (tier == 2 ? 1.08f : 1.16f) : 1;
                Near(expected, FactionRules.GetMultiplier(faction, tier, kind, strong, isolated));
            }
        });
        Run("全25将一二三阶实际扣血符合军势来源范围", delegate {
            foreach (HeroDefinition definition in source.Heroes) for (int level = 1; level <= 3; level++) {
                if (definition.Skill == "assist") continue;
                BattleSession on = Combat(definition.Id, 2, true), off = Combat(definition.Id, 2, false);
                foreach (BattleSession b in new[] { on, off }) { b.Heroes[0].Level = level; b.Heroes[0].Ambushed.Clear(); foreach (EnemyUnit enemy in b.Enemies) enemy.Definition.IsBoss = true; }
                float beforeOn = on.Enemies.Sum(e => e.Health), beforeOff = off.Enemies.Sum(e => e.Health);
                List<BattleEvent> eventsOn = Fire(on, false), eventsOff = Fire(off, false);
                if (definition.Skill == "cleave" || definition.Skill == "knockback") { on.Tick(1.1f); off.Tick(1.1f); eventsOn.AddRange(on.DrainEvents()); eventsOff.AddRange(off.DrainEvents()); }
                float factor = definition.Faction == "wei" ? 1 : definition.Faction == "qun" ? 1.08f : 1.06f;
                float actualOn = beforeOn - on.Enemies.Sum(e => e.Health), actualOff = beforeOff - off.Enemies.Sum(e => e.Health);
                float extraBasics = definition.Skill == "buff" ? eventsOff.Where(e => e.Kind == "basic_attack").Sum(e => e.Amount) * .06f : 0;
                Check(Math.Abs(actualOn - actualOff * factor - extraBasics) < .5f, definition.Id + " " + level + "阶实际扣血倍率错误：" + actualOn + "/" + actualOff);
                Check(eventsOn.Any(e => e.Kind == "skill_cast" && e.HeroId == definition.Id), definition.Id + "缺少施放事件");
            }
        });
        Run("蜀吴群4人直击替换2人档实际健康差", delegate {
            foreach (string id in new[] { "huang_zhong", "taishi_ci", "zhang_jiao" }) {
                BattleSession on = Combat(id, 4, true), off = Combat(id, 4, false); foreach (BattleSession b in new[] { on, off }) foreach (EnemyUnit enemy in b.Enemies) enemy.Definition.IsBoss = true;
                float beforeOn = on.Enemies.Sum(e => e.Health), beforeOff = off.Enemies.Sum(e => e.Health); List<BattleEvent> eventsOn = Fire(on, false), eventsOff = Fire(off, false);
                float factor = id == "zhang_jiao" ? 1.16f : 1.12f; Check(Math.Abs((beforeOn - on.Enemies.Sum(e => e.Health)) - (beforeOff - off.Enemies.Sum(e => e.Health)) * factor) < .3f, "四人档实际扣血错误"); Near(FirstAmount(eventsOff, "attack", id) * factor, FirstAmount(eventsOn, "attack", id));
            }
        });
        Run("军势开启五关多阵容经济与终局模拟", delegate { FactionSimulationTests.Run(source); });
        Console.WriteLine("军势测试：{0}通过，{1}失败", passed, failed); return failed == 0 ? 0 : 1;
    }
}
