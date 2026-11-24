using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 隔离夹具只验收规则到画面的反馈契约，不能作为实关平衡或人工乐趣证据。
public static class UiCombatExperienceChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo EventsField = typeof(BattleSession).GetField("events", PrivateInstance);

    public static void Verify(GameDatabase database, string output)
    {
        Require(database != null && !string.IsNullOrEmpty(output), "战斗体验专项缺少数据库或独立输出目录");
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var evidence = new Evidence();
        for (int stage = 1; stage <= 3; stage++) VerifyFields(database, output, evidence, stage);
        VerifyLinks(database, output, evidence);
        VerifyAssist(database, output, evidence, 3, false);
        VerifyAssist(database, output, evidence, 2, true);
        VerifyAssist(database, output, evidence, 3, true);
        foreach (int count in new[] { 3, 6, 10 }) VerifyBurst(database, output, evidence, count);
        VerifyOldActionTimes(database, evidence);
        VerifyOldRetirement(database, evidence, false);
        VerifyOldRetirement(database, evidence, true);
        VerifyBossWarning(database, output, evidence);
        VerifyStatusReuse();
        File.WriteAllText(Path.Combine(output, "experience-fixtures.json"), JsonUtility.ToJson(evidence, true), new System.Text.UTF8Encoding(false));
        Debug.Log("战斗体验隔离夹具通过：三组真实配合、3/6/10连破、三尺寸、状态/区域生命周期和共享预算；不代表实关平衡、设备听感或人工乐趣验收");
    }

    private static void VerifyFields(GameDatabase original, string output, Evidence evidence, int stage)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, "风火" + stage + "阶", 10000, 100);
        Route(level, "near", new Point2(750, 480), new Point2(900, 480), new Point2(900, 720));
        Route(level, "side", new Point2(800, 680), new Point2(1250, 680));
        Slot(level, 0, 770, 640); Slot(level, 1, 740, 530);
        Spawn(level, "near", 1); Spawn(level, "side", 1);
        using (var scene = Begin(database, level, "zhuge_liang", "zhou_yu"))
        {
            BattleSession session = scene.Flow.CurrentSession;
            var trace = new List<BattleEvent>();
            session.Start(); Step(scene, 1, trace);
            Recruit(session, "zhuge_liang", 0, stage); Recruit(session, "zhou_yu", 1, stage == 1 ? 1 : 2);
            Step(scene, 1f / 30, trace);
            List<CombatFieldSnapshot> fields = Fields(session);
            Require(fields.Count(field => field.Kind == "wind") == (stage == 1 ? 0 : stage == 2 ? 1 : 2), "风眼数未按真实一/二/三阶生成");
            CombatFieldSnapshot fire = fields.Single(field => field.Kind == "fire");
            Require(stage == 1 ? fire.Radius == 95 && string.IsNullOrEmpty(fire.RouteId) : fire.Radius == 0 && fire.RouteId == "near", "圆形火区与沿路火带没有按阶区分");
            foreach (CombatFieldSnapshot wind in fields.Where(field => field.Kind == "wind")) Require(wind.Radius == 105, "风眼边界误用了武将射程");
            BattleEffects effects = Effects(scene);
            var graphics = scene.Root.GetComponentsInChildren<BattleFieldGraphic>();
            Require(graphics.Length == fields.Count, "真实规则区域与可见区域数量不一致");
            foreach (BattleFieldGraphic graphic in graphics)
            {
                Require(!graphic.raycastTarget && Mesh(graphic).Count > 0, "持久区域没有真实网格或遮挡了输入");
                Require(graphic.transform.IsChildOf(Named(scene.Root, "配合区域")), "持久区域没有进入人物下方的配合层");
            }
            Require(Named(scene.Root, "配合区域").GetSiblingIndex() < Named(scene.Root, "棋兵").GetSiblingIndex(), "配合区域覆盖了人物层");
            if (stage >= 2)
            {
                BattleFieldGraphic road = graphics.Single(graphic => ((IList)Read(graphic, "path")).Count > 0);
                var path = (List<Vector2>)Read(road, "path");
                Require(path.Count >= 4 && path.Any(point => Mathf.Abs(point.x - 900) < .1f && Mathf.Abs(point.y + 480) < .1f) && path.Any(point => point.y < -480.1f), "真实火带没有沿道路跨过折点");
            }
            CheckStatuses(scene, session);
            CaptureThree(scene, output, "fixture-wind-fire-stage" + stage);
            if (stage == 3)
            {
                VerifyPause(scene, effects, trace);
                var reusable = Active(effects).Select(item => ((RectTransform)Read(item, "Root")).GetInstanceID()).ToArray();
                effects.Clear();
                Require(Active(effects).Count == 0 && scene.Root.GetComponentsInChildren<BattleFieldGraphic>().Length == 0, "清理后仍有区域或活动反馈");
                effects.SyncCombatFeedback(fields, level, session.Heroes);
                Require(Active(effects).Any(item => reusable.Contains(((RectTransform)Read(item, "Root")).GetInstanceID())), "区域回池后未复用已有反馈对象");
                VerifyBudget(scene, effects, level, fields, session.Heroes);
                Step(scene, 3, trace);
                Require(Fields(session).Count == 0 && scene.Root.GetComponentsInChildren<BattleFieldGraphic>().Length == 0, "真实区域到期后仍保留旧网格");
                // 重新同步真实时钟后继续自然行军，直到关卡结束；禁止直接篡改State制造终局。
                Step(scene, 30, trace);
                Require(session.State == BattleState.Victory || session.State == BattleState.Defeat, "区域生命周期夹具未自然终局");
                Require(Fields(session).Count == 0 && Active(effects).Count == 0, "终局没有清空规则区域和表现池");
                Require(scene.Root.GetComponentsInChildren<BattleFieldGraphic>().Length == 0, "终局仍显示真实技能区域");
                CaptureThree(scene, output, "fixture-fields-terminal");
            }
            AddEvidence(evidence, "fixture-wind-fire-stage" + stage, trace, fields);
        }
    }

    private static void VerifyLinks(GameDatabase original, string output, Evidence evidence)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, "连环传伤与补入", 10000, .01f);
        Route(level, "weak", new Point2(820, 540), new Point2(1120, 540));
        Route(level, "one", new Point2(860, 500), new Point2(1460, 500));
        Route(level, "two", new Point2(900, 550), new Point2(1500, 550));
        Route(level, "three", new Point2(940, 600), new Point2(1540, 600));
        Slot(level, 0, 760, 560); Slot(level, 1, 760, 620);
        Spawn(level, "weak", 1, .012f); Spawn(level, "one", 1); Spawn(level, "two", 1); Spawn(level, "three", 1);
        using (var scene = Begin(database, level, "pang_tong", "guan_yu"))
        {
            BattleSession session = scene.Flow.CurrentSession; var trace = new List<BattleEvent>();
            Recruit(session, "pang_tong", 0, 3); session.Start();
            Step(scene, 1f / 30, trace);
            BattleEvent created = trace.Single(item => item.Kind == "link_created");
            Require(created.RelatedEnemyIds != null && created.RelatedEnemyIds.Length == 3 && created.EffectId > 0, "庞统三阶没有自然连接三名敌人");
            Require(trace.Any(item => item.Kind == "link_hit" && item.SourceEnemyId > 0 && item.Amount > 0 && item.EffectId == created.EffectId), "连环缺少有来源、有实际传伤量的真实事件");
            CheckStatuses(scene, session);
            Require(scene.Root.GetComponentsInChildren<Text>().Any(label => label.text.Contains("链")), "真实连环没有出现在敌军状态位");
            CaptureThree(scene, output, "fixture-link-created-hit");
            Recruit(session, "guan_yu", 1, 1);
            for (int i = 0; i < 80 && !trace.Any(item => item.Kind == "link_replaced"); i++) Step(scene, 1f / 30, trace);
            BattleEvent replaced = trace.FirstOrDefault(item => item.Kind == "link_replaced");
            Require(replaced != null && replaced.EffectId == created.EffectId && replaced.ReplacedEnemyId > 0 && replaced.TargetEnemyId != replaced.ReplacedEnemyId, "节点击退后未自然补入新敌人");
            Require(Mathf.Abs(replaced.ExpiresAt - created.ExpiresAt) < .0001f, "连环补入刷新了原到期时间");
            Require(replaced.RelatedEnemyIds.Length == 3 && !replaced.RelatedEnemyIds.Contains(replaced.ReplacedEnemyId) && replaced.RelatedEnemyIds.Contains(replaced.TargetEnemyId), "补入事件的成员副本错误");
            CaptureThree(scene, output, "fixture-link-replaced");
            Step(scene, Mathf.Max(.1f, created.ExpiresAt - session.Time + .1f), trace);
            var states = new List<EnemyFeedbackSnapshot>(); session.ReadCombatFeedback(new List<CombatFieldSnapshot>(), states);
            Require(states.All(state => !state.Linked), "原连环到期后状态仍残留");
            CheckStatuses(scene, session);
            AddEvidence(evidence, "fixture-link-created-hit-replaced", trace, Fields(session));
        }
    }

    private static void VerifyAssist(GameDatabase original, string output, Evidence evidence, int stage, bool retarget)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, "协射" + stage + "阶" + (retarget ? "改射" : "双箭"), 10000, .01f);
        Route(level, "original", new Point2(1000, 520), new Point2(1300, 520));
        Route(level, "reserve-one", new Point2(770, 680), new Point2(1370, 680));
        Route(level, "reserve-two", new Point2(770, 400), new Point2(1370, 400));
        Slot(level, 0, 810, 520); Slot(level, 1, 650, 550);
        Spawn(level, "original", 1); Spawn(level, "reserve-one", 1); Spawn(level, "reserve-two", 1);
        // 低血通过波次配置进入规则，保证改射由真实致命命中触发。
        if (retarget) level.Waves[0].Groups[0].HealthScale = .0001f;
        using (var scene = Begin(database, level, "guan_yu", "xun_yu"))
        {
            BattleSession session = scene.Flow.CurrentSession; var trace = new List<BattleEvent>();
            Recruit(session, "guan_yu", 0, 1); Recruit(session, "xun_yu", 1, stage); session.Start();
            for (int i = 0; i < 90 && !trace.Any(item => item.HeroId == "xun_yu" && !string.IsNullOrEmpty(item.TriggerHeroId)); i++) Step(scene, 1f / 30, trace);
            List<BattleEvent> arrows = trace.Where(item => item.Kind == "attack" && item.HeroId == "xun_yu" && item.TriggerHeroId == "guan_yu").ToList();
            Require(arrows.Count == (stage == 3 ? 2 : 1), "荀彧协射箭数与二/三阶规则不符");
            Require(arrows.Select(item => item.TargetEnemyId).Distinct().Count() == arrows.Count, "同轮双箭错误重复命中同一目标");
            HeroUnit assistant = session.Heroes.Single(hero => hero.Definition.Id == "xun_yu");
            if (!retarget)
            {
                Require(Point2.Distance(assistant.Position, arrows[0].Target) > BattleSession.GetMaximumRange(assistant.Definition, assistant.Level), "首箭夹具未保留射界外原目标");
                Require(Point2.Distance(assistant.Position, arrows[1].Target) <= BattleSession.GetMaximumRange(assistant.Definition, assistant.Level), "第二箭没有独立寻找射界内候补");
            }
            else
            {
                BattleEvent killed = trace.FirstOrDefault(item => item.Kind == "kill");
                Require(killed != null && arrows.All(item => item.TargetEnemyId != killed.TargetEnemyId), "原目标倒下后没有真实改射");
            }
            Require(Active(Effects(scene)).Count(item => (bool)Read(item, "Projectile")) >= arrows.Count, "协射事件没有独立箭迹");
            string name = "fixture-assist-stage" + stage + (retarget ? "-retarget" : "-two-arrows");
            CaptureThree(scene, output, name); AddEvidence(evidence, name, trace, Fields(session));
        }
    }

    private static void VerifyBurst(GameDatabase original, string output, Evidence evidence, int count)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, "连破" + count, 20, .01f);
        Route(level, "near", new Point2(810, 510), new Point2(1220, 510));
        Route(level, "far", new Point2(1520, 680), new Point2(1700, 680));
        Slot(level, 0, 700, 500); Spawn(level, "near", count); Spawn(level, "far", 1);
        using (var scene = Begin(database, level, "zhao_yun"))
        {
            BattleSession session = scene.Flow.CurrentSession; var trace = new List<BattleEvent>();
            session.Start(); Step(scene, .3f, trace); Recruit(session, "zhao_yun", 0, 1); Step(scene, 1f / 30, trace);
            List<BattleEvent> kills = trace.Where(item => item.Kind == "kill").ToList();
            Require(kills.Count == count && session.State == BattleState.Running, "赵云真实贯穿没有生成预期连破，或夹具提前终局");
            var tracker = new BattleFeedbackState(.7f); BattleFeedbackResult result = new BattleFeedbackResult();
            foreach (BattleEvent item in kills) result = tracker.Observe(item);
            Require(result.Count == count && result.Tier == (count == 10 ? 3 : count == 6 ? 2 : 1), "自然击退事件聚合档位错误");
            Text[] labels = scene.Root.GetComponentsInChildren<Text>().Where(label => label.text.StartsWith("连破", StringComparison.Ordinal)).ToArray();
            Require(labels.Length == 1 && labels[0].text == "连破 ×" + count && labels[0].fontSize == (count == 10 ? 37 : count == 6 ? 33 : 29), "连破必须复用一枚标签并使用29/33/37字号");
            Outline outline = labels[0].GetComponent<Outline>();
            Require(outline != null && outline.enabled && outline.effectColor.a >= .8f, "连破文字缺少深色描边");
            CheckText(labels[0], "真实连破" + count);
            CaptureThree(scene, output, "fixture-burst-" + count);
            VerifyPause(scene, Effects(scene), trace);
            Step(scene, 1, trace);
            Require(!scene.Root.GetComponentsInChildren<Text>().Any(label => label.text.StartsWith("连破", StringComparison.Ordinal)), "连破恢复后未按实秒到期");
            AddEvidence(evidence, "fixture-burst-" + count, trace, Fields(session));
        }
    }

    private static void VerifyBossWarning(GameDatabase original, string output, Evidence evidence)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, "Boss预警共存", 10000, .01f);
        EnemyDefinition boss = database.FindEnemy("boss_zhang_jiao"); boss.MaxHealth = 100000; boss.Speed = .01f;
        level.CastleMaxHealth = 1; database.FindEnemy("infantry").CastleDamage = 1;
        level.BossHeroIds.Add("zhang_jiao");
        Route(level, "near", new Point2(850, 520), new Point2(1500, 520));
        Route(level, "side", new Point2(850, 650), new Point2(1500, 650));
        Route(level, "breach", new Point2(1650, 720), new Point2(1660, 720));
        Slot(level, 0, 770, 610); Slot(level, 1, 740, 530);
        Spawn(level, "near", 1); Spawn(level, "side", 1);
        level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = boss.Id, RouteId = "near", Count = 1, Interval = .02f });
        // 延迟的真实来兵在Boss蓄势期间抵城，不能直接改State伪造终局。
        level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = "infantry", RouteId = "breach", Count = 1, Delay = 7, SpeedScale = 100000 });
        using (var scene = Begin(database, level, "zhuge_liang", "zhou_yu"))
        {
            BattleSession session = scene.Flow.CurrentSession; var trace = new List<BattleEvent>(); session.Start();
            for (int i = 0; i < 310 && !trace.Any(item => item.Kind == "boss_warning"); i++) Step(scene, 1f / 30, trace);
            Require(trace.Any(item => item.Kind == "boss_warning"), "Boss夹具没有自然产生蓄势事件");
            Recruit(session, "zhuge_liang", 0, 3); Recruit(session, "zhou_yu", 1, 2); Step(scene, 1f / 30, trace);
            Require(Fields(session).Count >= 2, "Boss预警时缺少真实风火区域");
            Require(scene.Root.GetComponentsInChildren<Text>().Any(label => label.text == "蓄势！"), "普通状态覆盖了Boss蓄势");
            Image warning = scene.Root.GetComponentsInChildren<Image>().FirstOrDefault(image => image.sprite != null && image.sprite.texture.name.Contains("boss_warning"));
            Require(warning != null, "Boss预警没有真实图形");
            VerifyBudget(scene, Effects(scene), level, Fields(session), session.Heroes);
            Require(warning != null && warning.gameObject.activeInHierarchy, "共享预算压力挤掉了Boss预警");
            CaptureThree(scene, output, "fixture-boss-warning-with-fields");
            AddEvidence(evidence, "fixture-boss-warning-with-fields", trace, Fields(session));
            Step(scene, 1, trace);
            Require(session.State == BattleState.Defeat && trace.Any(item => item.Kind == "leak"), "蓄势终局夹具没有由真实抵城触发败局");
            Require(session.Enemies.Any(enemy => enemy.Definition.IsBoss && enemy.WarningUntil > session.Time), "败局夹具未保留仍在真实蓄势的Boss");
            Require(session.Enemies.Any(enemy => !enemy.Definition.IsBoss && enemy.SlowUntil > session.Time), "败局夹具缺少仍受真实减速的存活敌人");
            var views = (Dictionary<int, UnitView>)Read(Battle(scene), "enemyViews");
            Require(views.Count > 0 && views.Values.All(view => string.IsNullOrEmpty(((Text)Read(view, "status")).text)), "败局的活敌保留了状态或Boss蓄势文字");
            Require(Active(Effects(scene)).Count == 0, "败局没有清理共享特效池");
            CaptureThree(scene, output, "fixture-defeat-clear-status");
            AddEvidence(evidence, "fixture-defeat-clear-status", trace, Fields(session));
        }
    }

    private static void VerifyOldActionTimes(GameDatabase original, Evidence evidence)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, "自然旧攻击与命中", 10000, .01f);
        Route(level, "near", new Point2(810, 510), new Point2(1220, 510));
        Slot(level, 0, 700, 500); Spawn(level, "near", 1);
        using (var scene = Begin(database, level, "guan_yu"))
        {
            BattleSession session = scene.Flow.CurrentSession; var trace = new List<BattleEvent>();
            Recruit(session, "guan_yu", 0, 1); session.Start();
            // 真实规则一次推进较大帧，所有旧事件仍由BattleView按原顺序消费。
            Step(scene, 1.5f, trace);
            BattleEvent attack = trace.Last(item => item.HeroId == "guan_yu" && (item.Kind == "attack" || item.Kind == "basic_attack"));
            BattleEvent skill = trace.Last(item => item.HeroId == "guan_yu" && item.Kind == "attack");
            BattleEvent hit = trace.Last(item => item.Kind == "hit");
            Require(session.Time - attack.OccurredAt > .3f && session.Time - hit.OccurredAt > .12f, "旧动作夹具没有形成已过期的真实反馈");
            UnitView hero = ((Dictionary<string, UnitView>)Read(Battle(scene), "heroViewsById"))["guan_yu"];
            UnitView target = ((Dictionary<int, UnitView>)Read(Battle(scene), "enemyViews"))[hit.TargetEnemyId];
            Require(Mathf.Abs((float)Read(hero, "attackAt") - attack.OccurredAt) < .0001f && Mathf.Abs((float)Read(hero, "skillAt") - skill.OccurredAt) < .0001f, "旧攻击按收件时间重新播放完整动作");
            Require(Mathf.Abs((float)Read(target, "hitAt") - hit.OccurredAt) < .0001f, "旧命中按收件时间重开闪亮");
            Require(((Image)Read(hero, "body")).rectTransform.localScale == Vector3.one, "已过期攻击仍保留动作缩放");
            Require(((Image)Read(target, "body")).color == (Color)Read(target, "bodyTint"), "已过期命中仍重新闪亮");
            AddEvidence(evidence, "fixture-natural-old-action-times", trace, Fields(session));
        }
    }

    private static void VerifyOldRetirement(GameDatabase original, Evidence evidence, bool leak)
    {
        GameDatabase database; LevelDefinition level = Fixture(original, out database, leak ? "自然旧漏兵退场" : "自然旧击退退场", leak ? 10000 : 20, leak ? 200 : .01f);
        Route(level, "near", new Point2(810, 510), new Point2(910, 510));
        Route(level, "far", new Point2(1520, 680), new Point2(2520, 680));
        Slot(level, 0, 700, 500); Spawn(level, "near", 1); Spawn(level, "far", 1, leak ? 1 : 500);
        using (var scene = Begin(database, level, "guan_yu"))
        {
            BattleSession session = scene.Flow.CurrentSession; var trace = new List<BattleEvent>();
            session.Start(); Step(scene, 1f / 30, trace);
            int id = session.Enemies.Single(enemy => enemy.Position.X < 1000).Id;
            UnitView retired = ((Dictionary<int, UnitView>)Read(Battle(scene), "enemyViews"))[id];
            if (!leak) Recruit(session, "guan_yu", 0, 1);
            Step(scene, leak ? 1 : .5f, trace);
            BattleEvent exit = trace.Single(item => item.TargetEnemyId == id && item.Kind == (leak ? "leak" : "kill"));
            Require(session.State == BattleState.Running && session.Time - exit.OccurredAt > .24f, "旧退场夹具没有形成延迟消费或提前终局");
            Require(Mathf.Abs((float)Read(retired, "retireAt") - exit.OccurredAt) < .0001f, "退场起点没有回溯真实离场事件");
            Require(retired.IsRetired && !retired.Root.gameObject.activeSelf && ((Stack<UnitView>)Read(Battle(scene), "enemyPool")).Contains(retired), "过期退场没有当帧完成并回池");
            AddEvidence(evidence, leak ? "fixture-natural-old-leak-retirement" : "fixture-natural-old-kill-retirement", trace, Fields(session));
        }
    }

    private static void VerifyBudget(UiRefinementChecks.Scene scene, BattleEffects effects, LevelDefinition level, List<CombatFieldSnapshot> fields, IList<HeroUnit> heroes)
    {
        effects.SyncCombatFeedback(fields, level, heroes);
        var tracker = new BattleFeedbackState(.7f); float at = scene.Flow.CurrentSession.Time;
        for (int i = 0; i < 160; i++)
        {
            Point2 point = new Point2(300 + i % 14 * 100, 300 + i / 14 * 90);
            var kill = new BattleEvent { Kind = "kill", TargetEnemyId = 100000 + i, Position = point, Amount = 5, OccurredAt = at };
            effects.Emit(kill, null, tracker.Observe(kill));
            effects.Emit(new BattleEvent { Kind = "hit", Position = point, Amount = 17, OccurredAt = at }, null, new BattleFeedbackResult());
        }
        List<object> active = Active(effects);
        Require(active.Count <= 90 && active.Count + Count(Read(effects, "pool")) <= 90, "真实区域与瞬时反馈没有共用90对象池");
        Require(active.Count(item => (bool)Read(item, "Number")) <= 14, "伤害/奖励数字超过共享14枚预算");
        Require(scene.Root.GetComponentsInChildren<Text>().Count(label => label.text.StartsWith("连破", StringComparison.Ordinal)) <= 1, "预算压力创建了多枚连破标签");
    }

    private static void VerifyPause(UiRefinementChecks.Scene scene, BattleEffects effects, List<BattleEvent> trace)
    {
        BattleSession session = scene.Flow.CurrentSession; float before = session.Time;
        session.SetPaused(true); Step(scene, 0, trace);
        GraphicStamp[] expected = Stamps(scene.Root);
        Step(scene, 3, trace);
        Require(session.Time == before, "暂停推进了真实游戏时钟");
        GraphicStamp[] actual = Stamps(scene.Root);
        Require(expected.Length == actual.Length, "暂停改变了图形对象数");
        for (int i = 0; i < expected.Length; i++) Require(expected[i].Same(actual[i]), "暂停移动、褪色或隐藏了反馈图形：" + expected[i].Name);
        session.SetPaused(false); Step(scene, 1f / 30, trace);
        Require(session.Time > before, "恢复后真实游戏时钟未继续");
        Require(Active(effects).Count <= 90, "恢复后特效池超预算");
    }

    private static void VerifyStatusReuse()
    {
        var root = new GameObject("隔离夹具_状态位生命周期", typeof(RectTransform));
        try
        {
            var view = new UnitView(new UiFactory(), (RectTransform)root.transform, true);
            view.SetDefinition("Game/Enemies/infantry", "验收兵", 150, false);
            var all = new EnemyFeedbackSnapshot { Stunned = true, Slowed = true, Linked = true, Burning = true };
            view.SetFeedback(all); Text status = (Text)Read(view, "status");
            Require(status.text == "晕 缓", "状态位没有按晕/缓/链/燃顺序限制最多两项"); CheckText(status, "两个简字状态");
            view.SetFeedback(new EnemyFeedbackSnapshot { Linked = true, Burning = true });
            Require(status.text == "链 燃", "连环与燃烧没有共用状态位"); CheckText(status, "链燃状态");
            view.SetWarning(true); Require(status.text == "蓄势！", "Boss预警未优先显示"); CheckText(status, "Boss蓄势");
            view.Retire(2); view.Retire(1); view.Retire(3);
            Require(Mathf.Abs((float)Read(view, "retireAt") - 1) < .0001f, "重复退场未采用最早时刻，或被较晚时刻续命");
            view.Animate(1.25f);
            Require(view.IsRetired && string.IsNullOrEmpty(status.text), "修正后的退场没有完成或清除状态");
            view.Hide(); view.SetDefinition("Game/Enemies/infantry", "复用兵", 150, false);
            Require(string.IsNullOrEmpty(status.text), "回池复用保留了上一敌人的状态");
            view.SetFeedback(all); view.Hide();
            Require(!view.Root.gameObject.activeSelf, "回池后状态仍可见");
            view.SetDefinition("Game/Enemies/infantry", "再次复用", 150, false);
            Require(string.IsNullOrEmpty(status.text), "Hide后再次出池残留状态");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CheckStatuses(UiRefinementChecks.Scene scene, BattleSession session)
    {
        var states = new List<EnemyFeedbackSnapshot>(); session.ReadCombatFeedback(new List<CombatFieldSnapshot>(), states);
        var views = (Dictionary<int, UnitView>)Read(Battle(scene), "enemyViews");
        foreach (EnemyFeedbackSnapshot state in states)
        {
            UnitView view; Require(views.TryGetValue(state.EnemyId, out view), "规则敌人缺少真实UnitView");
            string status = ((Text)Read(view, "status")).text;
            if (session.Enemies.Single(enemy => enemy.Id == state.EnemyId).WarningUntil > session.Time) Require(status == "蓄势！", "Boss蓄势被普通状态覆盖");
            else
            {
                var expected = new List<string>();
                if (state.Stunned) expected.Add("晕"); if (state.Slowed) expected.Add("缓"); if (state.Linked) expected.Add("链"); if (state.Burning) expected.Add("燃");
                Require(status == string.Join(" ", expected.Take(2).ToArray()), "显示状态与真实快照不符：" + status);
            }
        }
    }

    private static void Step(UiRefinementChecks.Scene scene, float delta, List<BattleEvent> trace)
    {
        // 复制事件后由真实BattleView消费，禁止DrainEvents导致截图缺反馈。
        scene.Flow.CurrentSession.Tick(delta);
        var pending = (List<BattleEvent>)EventsField.GetValue(scene.Flow.CurrentSession);
        foreach (BattleEvent item in pending) trace.Add(JsonUtility.FromJson<BattleEvent>(JsonUtility.ToJson(item)));
        scene.Flow.Tick(0);
    }

    private static GameDatabase Clone(GameDatabase source) { return JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(source)); }
    private static LevelDefinition Fixture(GameDatabase source, out GameDatabase database, string name, float health, float speed)
    {
        database = Clone(source); LevelDefinition level = database.FindLevel("huangjin");
        level.Id = "experience_fixture"; level.Name = "隔离验收"; level.Subtitle = "隔离夹具 · " + name; level.Story = "只用于表现契约验收，不作为实关平衡证据。";
        level.InitialGold = 5000; level.IncomePerSecond = 0; level.BossHeroIds.Clear(); level.Routes.Clear(); level.Slots.Clear(); level.Waves.Clear();
        level.Waves.Add(new WaveDefinition { Name = "隔离夹具", StartDelay = 0 });
        EnemyDefinition infantry = database.FindEnemy("infantry"); infantry.MaxHealth = health; infantry.Speed = speed; infantry.Armor = 0;
        return level;
    }
    private static UiRefinementChecks.Scene Begin(GameDatabase database, LevelDefinition level, params string[] heroes)
    {
        var scene = new UiRefinementChecks.Scene(database); scene.Root.name = "隔离战斗体验夹具_" + level.Subtitle;
        scene.Flow.BeginBattle(level, new List<string>(heroes)); return scene;
    }
    private static void Route(LevelDefinition level, string id, params Point2[] points) { level.Routes.Add(new RouteDefinition { Id = id, Points = new List<Point2>(points) }); }
    private static void Slot(LevelDefinition level, int id, float x, float y) { level.Slots.Add(new SlotDefinition { Id = id, Position = new Point2(x, y) }); }
    private static void Spawn(LevelDefinition level, string route, int count, float health = 1) { level.Waves[0].Groups.Add(new SpawnGroup { EnemyId = "infantry", RouteId = route, Count = count, Interval = .02f, HealthScale = health }); }
    private static void Recruit(BattleSession session, string hero, int slot, int stage)
    {
        string error = session.TryRecruit(hero, slot); Require(error == null, "夹具招募失败：" + hero + "：" + error);
        for (int i = 1; i < stage; i++) { error = session.TryUpgrade(slot); Require(error == null, "夹具升阶失败：" + error); }
    }
    private static List<CombatFieldSnapshot> Fields(BattleSession session) { var fields = new List<CombatFieldSnapshot>(); session.ReadCombatFeedback(fields, new List<EnemyFeedbackSnapshot>()); return fields; }
    private static BattleView Battle(UiRefinementChecks.Scene scene) { return (BattleView)Read(scene.Flow, "battle"); }
    private static BattleEffects Effects(UiRefinementChecks.Scene scene) { return (BattleEffects)Read(Battle(scene), "effects"); }
    private static object Read(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(field != null, "专项缺少可观察字段：" + target.GetType().Name + "." + name); return field.GetValue(target);
    }
    private static int Count(object collection) { return ((IEnumerable)collection).Cast<object>().Count(); }
    private static List<object> Active(BattleEffects effects) { return ((IEnumerable)Read(effects, "active")).Cast<object>().ToList(); }
    private static RectTransform Named(RectTransform root, string name) { return root.GetComponentsInChildren<RectTransform>().Single(item => item.name == name); }
    private static List<UIVertex> Mesh(Graphic graphic)
    {
        var vertices = new List<UIVertex>();
        using (var helper = new VertexHelper())
        { graphic.GetType().GetMethod("OnPopulateMesh", PrivateInstance, null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper }); helper.GetUIVertexStream(vertices); }
        return vertices;
    }
    private static void CheckText(Text label, string description)
    {
        Require(label.font != null, description + "缺少字体");
        label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
        foreach (char character in label.text)
            if (!char.IsWhiteSpace(character)) Require(label.font.HasCharacter(character), description + "缺少字形：" + character);
        UiTypographyChecks.Check(label, description);
    }
    private static void CaptureThree(UiRefinementChecks.Scene scene, string output, string name)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
        {
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
            RenderTexture previous = RenderTexture.active, previousCamera = scene.Camera.targetTexture;
            float previousAspect = scene.Camera.aspect; Texture2D pixels = null;
            try
            {
                scene.Camera.targetTexture = target; scene.Camera.aspect = (float)size.x / size.y; Canvas.ForceUpdateCanvases();
                foreach (Text label in scene.Root.GetComponentsInChildren<Text>()) if (!string.IsNullOrWhiteSpace(label.text)) CheckText(label, name + "/" + size.x + "/" + label.name);
                scene.Camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + "-" + size.x + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                scene.Camera.targetTexture = previousCamera; scene.Camera.aspect = previousAspect; RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
            }
        }
    }
    private static GraphicStamp[] Stamps(RectTransform root)
    { return root.GetComponentsInChildren<Graphic>(true).Select(graphic => new GraphicStamp { Name = graphic.name, Visible = graphic.gameObject.activeInHierarchy, Position = graphic.transform.position, Rotation = graphic.transform.rotation, Scale = graphic.transform.localScale, Color = graphic.color, Text = graphic is Text ? ((Text)graphic).text : "" }).ToArray(); }
    private static void AddEvidence(Evidence evidence, string name, List<BattleEvent> events, List<CombatFieldSnapshot> fields)
    { evidence.Fixtures.Add(new FixtureEvidence { Name = name, Events = events, Fields = fields }); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("战斗体验专项：" + message); }

    [Serializable] private sealed class Evidence
    {
        public string Scope = "隔离表现契约夹具；不作为实关平衡、设备听感或人工乐趣证据";
        public string[] Sizes = { "1280x720", "1920x1080", "2400x1080" };
        public List<FixtureEvidence> Fixtures = new List<FixtureEvidence>();
    }
    [Serializable] private sealed class FixtureEvidence { public string Name; public List<BattleEvent> Events; public List<CombatFieldSnapshot> Fields; }
    private struct GraphicStamp
    {
        public string Name, Text; public bool Visible; public Vector3 Position, Scale; public Quaternion Rotation; public Color Color;
        public bool Same(GraphicStamp value) { return Name == value.Name && Text == value.Text && Visible == value.Visible && Position == value.Position && Scale == value.Scale && Rotation == value.Rotation && Color == value.Color; }
    }
}
