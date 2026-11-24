using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 仅在编辑器中主动调用，观察真实界面对象，不访问反馈组件的私有计数。
public static class UiBattleFeedbackChecks
{
    public static void Verify()
    {
        var canvasObject = new GameObject("战斗反馈验证", typeof(RectTransform), typeof(Canvas));
        try
        {
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var ui = new UiFactory();
            var root = (RectTransform)canvasObject.transform;
            VerifyAttackGeometry(ui, root);
            VerifyAttackMerging(ui, root);
            VerifyDomainMultiTargetProjectiles(ui, root);
            VerifyTargetedAttackIndependence(ui, root);
            VerifyUnknownKillIds(ui, root);
            VerifyKillFeedback(ui, root);
            VerifyKillWindowAndReset(ui, root);
            VerifyExpiredEventDoesNotSuppressFresh(ui, root);
            VerifyBudget(ui, root);
            VerifyPausedFeedback(ui, root);
            VerifyUnitLifecycle(ui, root);
            Debug.Log("战斗反馈验证通过：方向攻击形状、武将独立合并、真实连破、数字限额、重要反馈、暂停冻结及复用");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void VerifyAttackGeometry(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "攻击形状验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        Point2 origin = new Point2(500, 500);
        Vector2 direction = new Vector2(.8f, .6f);
        try
        {
            var cleave = new HeroDefinition { Id = "lv_bu", Skill = "cleave", Range = 220 };
            effects.Emit(Attack(cleave.Id, origin, Offset(origin, direction, 100, 0)), cleave);
            List<Graphic> areas = AttackAreas(layer);
            Require(areas.Count == 1, "吕布应绘制一片朝向目标的真实攻击扇面，当前可见范围图形为" + areas.Count);
            Graphic fan = areas[0];
            Require(fan.GetComponent<CanvasRenderer>() != null && !fan.raycastTarget,
                "攻击范围图形必须有CanvasRenderer且不拦截布阵输入");
            Require(fan.color.a > 0 && fan.color.a <= .28f, "瞬时扇面应使用低透明度，不能遮住军阵和敌军");
            List<Vector2> fanMesh = MeshInBattleCoordinates(fan, layer);
            Require(Contains(fanMesh, Offset(origin, direction, 210, 0)), "吕布扇面没有延伸至真实射程");
            Require(!Contains(fanMesh, Offset(origin, direction, 230, 0)), "吕布扇面超出真实射程");
            Require(Contains(fanMesh, Polar(origin, direction, 150, 71.5f)), "吕布扇面没有覆盖cos=.3以内的侧向目标");
            Require(!Contains(fanMesh, Polar(origin, direction, 150, 74)), "吕布扇面超过约145度真实夹角");
            Require(Contains(fanMesh, Polar(origin, direction, 150, -71.5f)), "吕布扇面的另一侧方向不正确");
            Require(!Contains(fanMesh, Offset(origin, direction, -60, 0)), "吕布攻击扇面错误覆盖身后");
            effects.Advance(.6f);
            Require(AttackAreas(layer).Count == 0, "攻击扇面不能作为持续伤害区或军阵预览留在场上");

            effects.Clear();
            var pierce = new HeroDefinition { Id = "zhao_yun", Skill = "pierce", Range = 360 };
            effects.Emit(Attack(pierce.Id, origin, Offset(origin, direction, 100, 0)), pierce);
            areas = AttackAreas(layer);
            Require(areas.Count == 1, "赵云应绘制一条完整贯穿带");
            Graphic band = areas[0];
            Require(band.GetComponent<CanvasRenderer>() != null && !band.raycastTarget && band.color.a > 0 && band.color.a <= .28f,
                "贯穿带应低透明、可渲染且不拦截输入");
            List<Vector2> bandMesh = MeshInBattleCoordinates(band, layer);
            Require(Contains(bandMesh, Offset(origin, direction, 350, 0)), "赵云贯穿带停在首个目标，未延伸到最大射程");
            Require(!Contains(bandMesh, Offset(origin, direction, 370, 0)), "赵云贯穿带超出最大射程");
            Require(Contains(bandMesh, Offset(origin, direction, 210, 37)), "赵云贯穿带没有覆盖真实38以内线距");
            Require(!Contains(bandMesh, Offset(origin, direction, 210, 40)), "赵云贯穿带错误覆盖38以外线距");
            Require(!Contains(bandMesh, Offset(origin, direction, -15, 0)), "赵云贯穿带错误覆盖起点身后");
            effects.Advance(.6f);
            Require(AttackAreas(layer).Count == 0, "赵云贯穿带未及时回收");

            effects.Clear();
            var focus = new HeroDefinition { Id = "guan_yu", Skill = "focus", Range = 220 };
            effects.Emit(Attack(focus.Id, origin, Offset(origin, direction, 100, 0)), focus);
            Require(AttackAreas(layer).Count == 0, "关羽单体专注攻击不能绘制群攻范围");
            Require(VisibleRoots(layer) > 0, "关羽单体攻击应保留目标斩击反馈");
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyAttackMerging(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "攻击合并验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            foreach (string skill in new[] { "cleave", "pierce", "slow", "stun" })
            {
                effects.Clear();
                var first = new HeroDefinition { Id = "first_" + skill, Skill = skill, Range = 360 };
                var second = new HeroDefinition { Id = "second_" + skill, Skill = skill, Range = 360 };
                Point2 origin = new Point2(500, 500);
                effects.Emit(Attack(first.Id, origin, new Point2(620, 500)), first);
                int singleAttack = VisibleRoots(layer);
                Require(singleAttack > 0, skill + "缺少可观察攻击反馈");
                effects.Emit(Attack(first.Id, origin, new Point2(720, 520)), first);
                effects.Emit(Attack(first.Id, origin, new Point2(800, 540)), first);
                Require(VisibleRoots(layer) == singleAttack, skill + "同一武将群目标事件没有合并主攻击轨迹");
                effects.Emit(Attack(second.Id, origin, new Point2(620, 500)), second);
                Require(VisibleRoots(layer) > singleAttack, skill + "另一武将的攻击被相同位置限频压掉");
                effects.Emit(new BattleEvent { Kind = "hit", Position = new Point2(620, 500), Amount = 17, TargetEnemyId = 101 });
                effects.Emit(new BattleEvent { Kind = "hit", Position = new Point2(800, 540), Amount = 19, TargetEnemyId = 102 });
                Require(HasText(layer, "17") && HasText(layer, "19"), "轨迹合并不能吞掉真实命中数字");
            }
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyDomainMultiTargetProjectiles(UiFactory ui, RectTransform root)
    {
        TextAsset source = Resources.Load<TextAsset>("Game/database");
        Require(source != null, "多目标弹道检查缺少实际武将配置");
        GameDatabase original = JsonUtility.FromJson<GameDatabase>(source.text);
        foreach (string heroId in new[] { "taishi_ci", "yuan_shao" })
        {
            HeroDefinition hero = original.FindHero(heroId);
            Require(hero != null, "多目标弹道检查缺少武将" + heroId);
            var database = new GameDatabase();
            database.Heroes.Add(hero);
            database.Enemies.Add(new EnemyDefinition { Id = "projectile_fixture", Name = "弹道验证敌军", MaxHealth = 10000, Speed = 0, CastleDamage = 1 });
            var level = new LevelDefinition { Id = "projectile_fixture", InitialGold = 1000, CastleMaxHealth = 100 };
            Point2 origin = new Point2(500, 500);
            level.Slots.Add(new SlotDefinition { Id = 0, Position = origin });
            level.Routes.Add(new RouteDefinition { Id = "upper", Points = new List<Point2> { new Point2(640, 400), new Point2(1400, 400) } });
            level.Routes.Add(new RouteDefinition { Id = "lower", Points = new List<Point2> { new Point2(640, 600), new Point2(1400, 600) } });
            level.Waves.Add(new WaveDefinition { Name = "分散双目标", Groups = new List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "projectile_fixture", RouteId = "upper", Count = 1, Interval = 1 },
                new SpawnGroup { EnemyId = "projectile_fixture", RouteId = "lower", Count = 1, Interval = 1 }
            } });
            var session = new BattleSession(database, level, new[] { heroId });
            Require(session.TryRecruit(heroId, 0) == null, hero.Name + "真实多目标夹具未成功招募");
            session.DrainEvents();
            session.Start();
            session.Tick(1f / 30f);
            List<BattleEvent> attacks = session.DrainEvents().FindAll(item => item.Kind == "attack" && item.HeroId == heroId);
            Require(attacks.Count == 2 && attacks[0].TargetEnemyId != attacks[1].TargetEnemyId,
                hero.Name + "必须在同一个真实Domain Tick对两个独立敌人产生attack事件");
            Require(Point2.Distance(attacks[0].Target, attacks[1].Target) > 150,
                hero.Name + "多目标夹具必须使用分散目标，不能用重叠敌人掩盖弹道遗漏");

            RectTransform layer = ui.Rect(root, hero.Name + "真实多目标弹道", 0, 0, 1920, 1080);
            var effects = new BattleEffects(ui, layer);
            try
            {
                foreach (BattleEvent attack in attacks) effects.Emit(attack, hero);
                List<RectTransform> arrows = ProjectileRoots(layer);
                Require(arrows.Count == 2, hero.Name + "两个真实不同目标的attack应各保留弹道，实际可见" + arrows.Count + "条");
                effects.Advance(.12f);
                arrows = ProjectileRoots(layer);
                foreach (BattleEvent attack in attacks)
                {
                    Vector2 start = heroId == "yuan_shao" ? new Vector2(attack.Target.X - 18, attack.Target.Y - 219) : new Vector2(attack.Position.X, attack.Position.Y - 64);
                    Vector2 end = new Vector2(attack.Target.X, attack.Target.Y - 38);
                    Vector2 expected = Vector2.Lerp(start, end, .12f / (heroId == "yuan_shao" ? .22f : .19f));
                    int matching = 0;
                    foreach (RectTransform arrow in arrows)
                    {
                        Vector2 actual = new Vector2(arrow.anchoredPosition.x, -arrow.anchoredPosition.y);
                        if (Vector2.Distance(actual, expected) < .2f) matching++;
                    }
                    Require(matching == 1, hero.Name + "没有恰好一条可见弹道朝向真实目标" + attack.TargetEnemyId);
                }
            }
            finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
        }
    }

    private static void VerifyTargetedAttackIndependence(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "独立目标弹道验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            foreach (string skill in new[] { "twin", "rain", "chain", "ambush", "focus", "sniper" })
            {
                effects.Clear();
                var hero = new HeroDefinition { Id = "targeted_" + skill, Skill = skill, Range = 360 };
                BattleEvent first = Attack(hero.Id, new Point2(500, 500), new Point2(640, 400));
                first.TargetEnemyId = 701;
                BattleEvent second = Attack(hero.Id, first.Position, new Point2(640, 600));
                second.TargetEnemyId = 702;
                effects.Emit(first, hero);
                int firstCount = VisibleRoots(layer);
                effects.Emit(first, hero);
                Require(VisibleRoots(layer) == firstCount, skill + "同一目标重复事件未在短窗内合并");
                effects.Emit(second, hero);
                Require(VisibleRoots(layer) > firstCount, skill + "同一武将对不同目标的真实攻击被整体限频吞掉");
                if (skill != "focus") Require(ProjectileRoots(layer).Count == 2, skill + "未显示指向两个独立目标的弹道");
            }
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyUnknownKillIds(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "无身份击破验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            Point2 position = new Point2(1100, 700);
            EmitKill(effects, 0, position);
            EmitKill(effects, -1, position);
            Require(ComboTexts(layer).Count == 0 && !HasLocalGoldGraphic(layer, position),
                "无有效敌人ID的kill不能触发真实击破金圈或计入连破");
            EmitKill(effects, 101, position);
            EmitKill(effects, 102, position);
            Require(ComboTexts(layer).Count == 0, "无有效敌人ID的kill被加入后续真实连破计数");
            EmitKill(effects, 103, position);
            Require(ComboTexts(layer).Count == 1 && ComboTexts(layer)[0].text == "连破 ×3", "有效ID的真实击破不能被无身份事件污染");
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyKillFeedback(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "真实击破验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            Point2 position = new Point2(1100, 700);
            EmitKill(effects, 1, position);
            Require(ComboTexts(layer).Count == 0, "单次击破不能显示连破");
            Require(HasLocalGoldGraphic(layer, position), "真实击破缺少短促局部金点或金圈");
            EmitKill(effects, 1, position);
            effects.Emit(new BattleEvent { Kind = "hit", TargetEnemyId = 2, Position = position, Amount = 30 });
            effects.Emit(new BattleEvent { Kind = "leak", TargetEnemyId = 3, Position = position });
            EmitKill(effects, 2, new Point2(1120, 700));
            effects.Advance(.1f);
            Require(ComboTexts(layer).Count == 0, "重复敌人、命中或漏兵被错误计入连破");
            List<GraphicState> pending = Capture(layer);
            for (int i = 0; i < 60; i++) effects.Advance(0);
            Compare(pending, Capture(layer), "暂停时尚未成组的击破窗口或局部反馈发生变化");
            EmitKill(effects, 3, new Point2(1140, 700));
            EmitKill(effects, 4, new Point2(1160, 700));
            EmitKill(effects, 5, new Point2(1180, 700));
            effects.Advance(.31f);
            List<Text> combos = ComboTexts(layer);
            Require(combos.Count == 1 && combos[0].text == "连破 ×5", "短窗内五个唯一真实kill应聚合为单条连破 ×5");
            UiTypographyChecks.Check(combos[0], "连破反馈", true);
            CheckComboOutline(combos[0]);
            Vector2 center = BattleCenter(combos[0], layer);
            Require(Vector2.Distance(center, new Vector2(1140, 700)) < 180 && center.y > 300,
                "连破应显示在击破附近，不能占用HUD提示条");
            Require(combos[0].text.IndexOf("吕布", StringComparison.Ordinal) < 0, "kill未携带武将归因时不能声称某将一击连破");
            List<GraphicState> before = Capture(layer);
            for (int i = 0; i < 60; i++) effects.Advance(0);
            Compare(before, Capture(layer), "暂停时连破或击破局部反馈继续推进");
            effects.Advance(1.5f);
            Require(ComboTexts(layer).Count == 0, "连破反馈没有按游戏时间回收");
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyKillWindowAndReset(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "连破窗口复用验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            Point2 position = new Point2(1000, 700);
            EmitKill(effects, 10, position);
            EmitKill(effects, 11, position);
            effects.Advance(.71f);
            EmitKill(effects, 12, position);
            effects.Advance(.71f);
            Require(ComboTexts(layer).Count == 0, "超过0.7有效实秒的击破仍被累加为连破");
            EmitKill(effects, 10, position);
            EmitKill(effects, 11, position);
            effects.Advance(.41f);
            Require(ComboTexts(layer).Count == 0, "已经统计过的敌人ID在新窗口被重复统计");

            effects.Clear();
            Require(layer.GetComponentsInChildren<Graphic>().Length == 0, "Clear未立即隐藏攻击、金点和文字");
            EmitKill(effects, 10, position);
            EmitKill(effects, 11, position);
            EmitKill(effects, 12, position);
            effects.Advance(.41f);
            Require(ComboTexts(layer).Count == 1 && ComboTexts(layer)[0].text == "连破 ×3", "Clear未清除已击破ID、窗口或连破限频");
            Text comboLabel = ComboTexts(layer)[0];
            UiTypographyChecks.Check(comboLabel, "回池复用后的连破反馈", true);
            CheckComboOutline(comboLabel);

            effects.Clear();
            for (int i = 0; i < 14; i++)
                effects.Emit(new BattleEvent { Kind = "hit", Position = new Point2(100 + (i % 7) * 100, 400 + (i / 7) * 100), Amount = 27 + i });
            int damage;
            Require(comboLabel.gameObject.activeInHierarchy && int.TryParse(comboLabel.text, out damage), "未实际复用带描边的连破标签验证伤害数字");
            foreach (Text text in layer.GetComponentsInChildren<Text>())
            {
                Outline outline = text.GetComponent<Outline>();
                Require(outline == null || !outline.enabled, "Clear后普通伤害数字继承了连破描边");
            }

            effects.Clear();
            EmitKill(effects, 20, position);
            EmitKill(effects, 21, position);
            effects.Clear();
            EmitKill(effects, 22, position);
            effects.Advance(.41f);
            Require(ComboTexts(layer).Count == 0, "Clear之前未成组的击破混入了下一场");
            effects.Clear();
            var hero = new HeroDefinition { Id = "reset_cleave", Skill = "cleave", Range = 220 };
            effects.Emit(Attack(hero.Id, position, new Point2(1120, 700)), hero);
            effects.Clear();
            effects.Emit(new BattleEvent { Kind = "hit", Position = position, Amount = 27 });
            Require(AttackAreas(layer).Count == 0 && HasText(layer, "27"), "范围图形回池后污染了数字反馈");
            effects.Clear();
            effects.Emit(Attack(hero.Id, position, new Point2(1120, 700)), hero);
            Require(AttackAreas(layer).Count == 1, "Clear没有重置同武将攻击限频");
            effects.Advance(.6f);
            Require(AttackAreas(layer).Count == 0, "复用范围图形的寿命没有重置");
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyExpiredEventDoesNotSuppressFresh(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "过期事件不抢新反馈", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            effects.Advance(1);
            var hero = new HeroDefinition { Id = "age_sniper", Skill = "sniper", Range = 250 };
            BattleEvent old = Attack(hero.Id, new Point2(900, 700), new Point2(1000, 700));
            old.TargetEnemyId = 700; old.OccurredAt = .1f;
            effects.Emit(old, hero, new BattleFeedbackResult());
            Require(layer.GetComponentsInChildren<Graphic>().Length == 0, "已过期攻击重新播放了完整图形寿命");
            BattleEvent fresh = Attack(hero.Id, old.Position, old.Target);
            fresh.TargetEnemyId = old.TargetEnemyId; fresh.OccurredAt = .98f;
            effects.Emit(fresh, hero, new BattleFeedbackResult());
            Require(layer.GetComponentsInChildren<Graphic>().Length > 0, "旧事件按取件时间占据冷却，吞掉了同批新鲜攻击");

            effects.Clear(); effects.Advance(1);
            effects.Emit(new BattleEvent { Kind = "hit", Position = old.Target, Amount = 17, OccurredAt = .1f }, null, new BattleFeedbackResult());
            Require(CountNumberTexts(layer) == 0, "已过期命中重新播放了完整数字寿命");
            effects.Emit(new BattleEvent { Kind = "hit", Position = old.Target, Amount = 19, OccurredAt = .98f }, null, new BattleFeedbackResult());
            Require(HasText(layer, "19"), "旧命中占据区域限频，吞掉了同批新鲜伤害数字");
        }
        finally { effects.Dispose(); UnityEngine.Object.DestroyImmediate(layer.gameObject); }
    }

    private static void VerifyBudget(UiFactory ui, RectTransform root)
    {
        RectTransform layer = ui.Rect(root, "反馈容量验证", 0, 0, 1920, 1080);
        var effects = new BattleEffects(ui, layer);
        try
        {
            for (int i = 0; i < 100; i++)
                effects.Emit(new BattleEvent { Kind = "hit", Position = new Point2((i % 10) * 100, (i / 10) * 100), Amount = 20 });
            int numbers = 0;
            foreach (Text text in layer.GetComponentsInChildren<Text>())
            {
                int amount;
                if (int.TryParse(text.text, out amount)) numbers++;
            }
            Require(numbers > 0 && numbers <= 14, "100次分散命中应显示1至14个伤害数字，实际为" + numbers);

            // 继续产生箭矢施加满池压力，验证重要反馈仍能取得显示位置。
            for (int i = 0; i < 100; i++)
            {
                var archer = new HeroDefinition { Id = "feedback_archer_" + i, Skill = "sniper" };
                effects.Emit(Attack(archer.Id, new Point2(100, 300), new Point2(500, 300)), archer);
            }
            effects.Emit(new BattleEvent { Kind = "stun", Position = new Point2(1400, 500) });
            effects.Emit(new BattleEvent { Kind = "recruit", Position = new Point2(1600, 500) });
            Require(HasText(layer, "震慑"), "普通反馈密集时未显示重要控制提示");
            Require(HasText(layer, "入阵"), "普通反馈密集时未显示入阵提示");
            Require(layer.childCount <= 90, "反馈池创建对象超过90个，实际为" + layer.childCount);

            int controlCount = CountText(layer, "震慑");
            for (int i = 0; i < 20; i++)
                effects.Emit(new BattleEvent { Kind = "stun", Position = new Point2(1400, 500) });
            Require(CountText(layer, "震慑") == controlCount, "相同位置的重复控制提示没有抑制");
            effects.Emit(new BattleEvent { Kind = "boss_warning", Position = new Point2(1200, 500) });
            Image warning = FindAtlasImage(layer, "boss_warning");
            Require(warning != null, "预算检查缺少真实Boss预警图形");
            for (int i = 0; i < 30; i++) EmitKill(effects, 1000 + i, new Point2(1000 + i * 2, 700));
            effects.Advance(.41f);
            Require(warning.gameObject.activeInHierarchy, "连破和击破金点抢占了Boss预警");
            Require(layer.childCount <= 90, "加入连破后反馈池超出90个对象预算");
            Require(CountNumberTexts(layer) <= 14, "加入击破后数字超出14个预算");
        }
        finally
        {
            effects.Dispose();
            UnityEngine.Object.DestroyImmediate(layer.gameObject);
        }
    }

    private static void VerifyPausedFeedback(UiFactory ui, RectTransform root)
    {
        RectTransform pausedLayer = ui.Rect(root, "暂停反馈验证", 0, 0, 1920, 1080);
        RectTransform referenceLayer = ui.Rect(root, "正常反馈对照", 0, 0, 1920, 1080);
        var paused = new BattleEffects(ui, pausedLayer);
        var reference = new BattleEffects(ui, referenceLayer);
        try
        {
            SeedFeedback(paused);
            SeedFeedback(reference);
            paused.Advance(.1f);
            reference.Advance(.1f);
            List<GraphicState> before = Capture(pausedLayer);
            Require(HasText(pausedLayer, "入阵"), "暂停前缺少可观察的入阵提示");
            for (int i = 0; i < 60; i++) paused.Advance(0);
            Compare(before, Capture(pausedLayer), "暂停时反馈位置、颜色或可见性发生变化");

            // 暂停组和未收到零增量调用的对照组继续走相同游戏时间，寿命应一致。
            paused.Advance(.4f);
            reference.Advance(.4f);
            Compare(Capture(referenceLayer), Capture(pausedLayer), "暂停调用改变了反馈后续寿命");
            Require(HasText(pausedLayer, "入阵"), "暂停后恢复时入阵提示提前消失");
            paused.Advance(2);
            reference.Advance(2);
            Require(pausedLayer.GetComponentsInChildren<Text>().Length == 0, "恢复后反馈未正常到期回收");
            Compare(Capture(referenceLayer), Capture(pausedLayer), "暂停组与对照组回收状态不同");
        }
        finally
        {
            paused.Dispose();
            reference.Dispose();
            UnityEngine.Object.DestroyImmediate(pausedLayer.gameObject);
            UnityEngine.Object.DestroyImmediate(referenceLayer.gameObject);
        }
    }

    private static void SeedFeedback(BattleEffects effects)
    {
        effects.Emit(new BattleEvent { Kind = "recruit", Position = new Point2(300, 400) });
        effects.Emit(new BattleEvent { Kind = "hit", Position = new Point2(500, 400), Amount = 18 });
        effects.Emit(new BattleEvent { Kind = "attack", Position = new Point2(200, 300), Target = new Point2(500, 300) },
            new HeroDefinition { Id = "feedback_archer", Skill = "sniper" });
    }

    private static void VerifyUnitLifecycle(UiFactory ui, RectTransform root)
    {
        var view = new UnitView(ui, root, true);
        try
        {
            view.SetDefinition("Game/Characters/guan_yu", "关羽", 190, false);
            Image body = view.Root.Find("人物").GetComponent<Image>();
            Require(body.sprite != null, "命中与退场验证缺少人物素材");
            view.Place(new Point2(400, 400), 0, .7f, false, false, 10);
            view.Animate(10);
            Color idle = body.color;
            view.Hit(10);
            view.Animate(10.02f);
            Require(body.color != idle, "命中后人物未出现短暂颜色反馈");
            view.Animate(10.2f);
            Require(body.color == idle, "命中反馈结束后人物颜色未恢复");

            CanvasGroup visibility = view.Root.GetComponent<CanvasGroup>();
            view.Retire(11);
            view.Animate(11.12f);
            Require(view.IsRetiring && !view.IsRetired && visibility.alpha > 0 && visibility.alpha < 1,
                "退场中段应部分透明且尚未结束");
            float pausedAlpha = visibility.alpha;
            Vector2 pausedPosition = body.rectTransform.anchoredPosition;
            view.Animate(11.12f);
            Require(visibility.alpha == pausedAlpha && body.rectTransform.anchoredPosition == pausedPosition,
                "相同游戏时间重复更新导致退场继续推进");
            view.Animate(11.25f);
            Require(view.IsRetired && visibility.alpha == 0, "退场结束后未标记完成或仍可见");

            view.Hide();
            view.SetDefinition("Game/Characters/guan_yu", "关羽", 190, false);
            Require(view.Root.gameObject.activeInHierarchy && visibility.alpha == 1 && !view.IsRetiring && !view.IsRetired,
                "回池后重新使用未恢复人物可见状态");
            Require(body.rectTransform.localScale == Vector3.one && view.Root.Find("生命底").gameObject.activeSelf,
                "回池后重新使用未恢复人物比例或血条");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(view.Root.gameObject);
        }
    }

    private static bool HasText(RectTransform layer, string value) { return CountText(layer, value) > 0; }

    private static void CheckComboOutline(Text label)
    {
        Outline outline = label.GetComponent<Outline>();
        Require(outline != null && outline.enabled, "连破文字缺少可见的深色细描边");
        Color color = outline.effectColor;
        Require(color.r < .25f && color.g < .25f && color.b < .25f && color.a >= .8f && outline.useGraphicAlpha,
            "连破描边应使用深色并随文字一起淡出");
        Vector2 distance = outline.effectDistance;
        Require(Mathf.Abs(distance.x) >= 1 && Mathf.Abs(distance.x) <= 1.5f && Mathf.Abs(distance.y) >= 1 && Mathf.Abs(distance.y) <= 1.5f,
            "连破描边应保持1至1.5像素的细轮廓");
    }

    private static BattleEvent Attack(string heroId, Point2 origin, Point2 target)
    { return new BattleEvent { Kind = "attack", HeroId = heroId, Position = origin, Target = target }; }

    private static void EmitKill(BattleEffects effects, int enemyId, Point2 position)
    {
        float now = (float)typeof(BattleEffects).GetField("gameTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effects);
        effects.Emit(new BattleEvent { Kind = "kill", TargetEnemyId = enemyId, Position = position, Amount = 5, OccurredAt = now });
    }

    private static List<Graphic> AttackAreas(RectTransform layer)
    {
        var result = new List<Graphic>();
        foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>())
            if (!(graphic is Image) && !(graphic is Text)) result.Add(graphic);
        return result;
    }

    private static List<Vector2> MeshInBattleCoordinates(Graphic graphic, RectTransform layer)
    {
        // 只读取实际绘制顶点，不依赖尚未存在的新类型，也不访问生产类的私有计数。
        MethodInfo populate = graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(VertexHelper) }, null);
        Require(populate != null, "攻击范围图形没有可观察绘制网格");
        var vertices = new List<UIVertex>();
        using (var helper = new VertexHelper())
        {
            populate.Invoke(graphic, new object[] { helper });
            helper.GetUIVertexStream(vertices);
        }
        Require(vertices.Count >= 3 && vertices.Count % 3 == 0, "攻击范围图形未产生真实三角形");
        var result = new List<Vector2>();
        foreach (UIVertex vertex in vertices)
        {
            Vector3 point = layer.InverseTransformPoint(graphic.transform.TransformPoint(vertex.position));
            result.Add(new Vector2(point.x, -point.y));
        }
        return result;
    }

    private static bool Contains(List<Vector2> triangles, Point2 point)
    {
        Vector2 test = new Vector2(point.X, point.Y);
        for (int i = 0; i < triangles.Count; i += 3)
        {
            Vector2 a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            float ab = Cross(b - a, test - a), bc = Cross(c - b, test - b), ca = Cross(a - c, test - c);
            if ((ab >= -.001f && bc >= -.001f && ca >= -.001f) || (ab <= .001f && bc <= .001f && ca <= .001f)) return true;
        }
        return false;
    }

    private static float Cross(Vector2 first, Vector2 second) { return first.x * second.y - first.y * second.x; }

    private static Point2 Offset(Point2 origin, Vector2 direction, float forward, float sideways)
    {
        return new Point2(origin.X + direction.x * forward - direction.y * sideways,
            origin.Y + direction.y * forward + direction.x * sideways);
    }

    private static Point2 Polar(Point2 origin, Vector2 direction, float distance, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return Offset(origin, direction, Mathf.Cos(radians) * distance, Mathf.Sin(radians) * distance);
    }

    private static int VisibleRoots(RectTransform layer)
    {
        int count = 0;
        for (int i = 0; i < layer.childCount; i++) if (layer.GetChild(i).gameObject.activeInHierarchy) count++;
        return count;
    }

    private static List<RectTransform> ProjectileRoots(RectTransform layer)
    {
        var result = new List<RectTransform>();
        foreach (Image image in layer.GetComponentsInChildren<Image>())
            if (image.gameObject.name == "箭镞") result.Add((RectTransform)image.transform.parent);
        return result;
    }

    private static List<Text> ComboTexts(RectTransform layer)
    {
        var result = new List<Text>();
        foreach (Text text in layer.GetComponentsInChildren<Text>())
            if (text.text.StartsWith("连破", StringComparison.Ordinal)) result.Add(text);
        return result;
    }

    private static Vector2 BattleCenter(Graphic graphic, RectTransform layer)
    {
        Vector3 point = layer.InverseTransformPoint(graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
        return new Vector2(point.x, -point.y);
    }

    private static bool HasLocalGoldGraphic(RectTransform layer, Point2 position)
    {
        foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>())
        {
            if (graphic is Text || graphic.color.a <= 0) continue;
            Vector2 size = graphic.rectTransform.rect.size;
            if (size.x > 80 || size.y > 80) continue;
            if (Vector2.Distance(BattleCenter(graphic, layer), new Vector2(position.X, position.Y)) > 65) continue;
            if (graphic.color.r > graphic.color.b * 1.25f && graphic.color.g > graphic.color.b) return true;
        }
        return false;
    }

    private static Image FindAtlasImage(RectTransform layer, string name)
    {
        foreach (Image image in layer.GetComponentsInChildren<Image>())
            if (image.sprite != null && image.sprite.texture != null && image.sprite.texture.name.IndexOf(name, StringComparison.Ordinal) >= 0) return image;
        return null;
    }

    private static int CountNumberTexts(RectTransform layer)
    {
        int count = 0;
        foreach (Text text in layer.GetComponentsInChildren<Text>())
        {
            int amount;
            if (int.TryParse(text.text, out amount)) count++;
        }
        return count;
    }

    private static int CountText(RectTransform layer, string value)
    {
        int count = 0;
        foreach (Text text in layer.GetComponentsInChildren<Text>()) if (text.text == value) count++;
        return count;
    }

    private static List<GraphicState> Capture(RectTransform layer)
    {
        var result = new List<GraphicState>();
        foreach (Graphic graphic in layer.GetComponentsInChildren<Graphic>(true))
        {
            Text text = graphic as Text;
            result.Add(new GraphicState
            {
                Visible = graphic.gameObject.activeInHierarchy,
                Position = graphic.transform.position,
                Rotation = graphic.transform.rotation,
                Size = graphic.rectTransform.rect.size,
                Color = graphic.color,
                Text = text == null ? "" : text.text
            });
        }
        return result;
    }

    private static void Compare(List<GraphicState> expected, List<GraphicState> actual, string message)
    {
        Require(expected.Count == actual.Count, message + "：对象数量变化");
        for (int i = 0; i < expected.Count; i++)
        {
            GraphicState a = expected[i], b = actual[i];
            Require(a.Visible == b.Visible && a.Position == b.Position && a.Rotation == b.Rotation && a.Size == b.Size && a.Color == b.Color && a.Text == b.Text,
                message + "：第" + i + "个图形不一致");
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private struct GraphicState
    {
        public bool Visible;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Size;
        public Color Color;
        public string Text;
    }
}
