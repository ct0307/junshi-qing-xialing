using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

public static class UiSensationFeedbackChecks
{
    public static int SlotRaycasts { get; private set; }
    public static void Verify()
    {
        SlotRaycasts = 0;
        var root = new GameObject("技能层次验收", typeof(RectTransform), typeof(Canvas));
        try
        {
            var ui = new UiFactory();
            RectTransform layer = ui.Rect(root.transform, "瞬时层", 0, 0, 1920, 1080);
            RectTransform ground = ui.Rect(root.transform, "地面层", 0, 0, 1920, 1080);
            var effects = new BattleEffects(ui, layer, ground);
            var hero = new HeroDefinition { Id = "lv_bu", Skill = "cleave", Range = 200 };
            var cast = new BattleEvent { Kind = "skill_cast", HeroId = hero.Id, HeroLevel = 2, AttackSequenceId = 17,
                Position = new Point2(600, 215), Target = new Point2(700, 300), SkillRange = 200 };
            effects.Emit(cast, hero);
            Require(layer.GetComponentsInChildren<BattleAttackGraphic>().Length == 1, "真实主斩缺少唯一几何");
            int count = layer.GetComponentsInChildren<Image>().Length;
            for (int i = 0; i < 8; i++) effects.Emit(new BattleEvent { Kind = "attack", HeroId = hero.Id,
                HeroLevel = 2, AttackSequenceId = 17, Position = cast.Position, Target = cast.Target, TargetEnemyId = i + 1 }, hero);
            Require(layer.GetComponentsInChildren<BattleAttackGraphic>().Length == 1, "多目标重复主斩");
            Require(layer.GetComponentsInChildren<Image>().Length == count, "多目标重复主效果");
            effects.Clear();
            for (int i = 0; i < 120; i++) effects.Emit(new BattleEvent { Kind = "hit", Amount = 4, Position = new Point2(i * 90, 500) });
            effects.SyncCombatFeedback(new List<CombatFieldSnapshot> { new CombatFieldSnapshot { Id = 1, Kind = "fire", Position = new Point2(500, 300), Radius = 95, Until = 2.5f } }, new LevelDefinition(), new List<HeroUnit>());
            Require(ground.GetComponentsInChildren<BattleFieldGraphic>().Length == 1, "密集反馈挤掉真实区域");
            Require(layer.childCount + ground.childCount <= 90, "表现对象超预算");
            effects.Clear();
            // 普通数字至多14，需用90条独立普攻弹道真实压满总池。
            for (int i = 0; i < 90; i++) effects.Emit(new BattleEvent { Kind = "basic_attack", Position = new Point2(100 + i, 300), Target = new Point2(900, 300) });
            Require(layer.GetComponentsInChildren<Image>().Length >= 90, "压力夹具未实际填满效果池");
            effects.SyncCombatFeedback(new List<CombatFieldSnapshot> { new CombatFieldSnapshot { Id = 2, Kind = "wind", Position = new Point2(500, 300), Radius = 105, Until = 2.5f } }, new LevelDefinition(), new List<HeroUnit>());
            Require(ground.GetComponentsInChildren<BattleFieldGraphic>().Length == 1, "满池普通弹道挤掉真实区域");
            effects.Clear();
            var crowded = new List<CombatFieldSnapshot>();
            for (int i = 0; i < 90; i++) crowded.Add(new CombatFieldSnapshot { Id = 100 + i, Kind = "fire", Position = new Point2(500, 300), Radius = 95, Until = 2.5f });
            effects.SyncCombatFeedback(crowded, new LevelDefinition(), new List<HeroUnit>());
            Require(ground.GetComponentsInChildren<BattleFieldGraphic>().Length == 90, "高优先区域压力夹具未满池");
            effects.Emit(new BattleEvent { Kind = "skill_charge", Amount = 1, Position = new Point2(500, 300) });
            Require(ground.GetComponentsInChildren<BattleFieldGraphic>().Length == 90, "蓄力抢占真实区域");
            effects.Emit(new BattleEvent { Kind = "boss_warning", Position = new Point2(1200, 300) });
            Require(ground.GetComponentsInChildren<BattleFieldGraphic>().Length == 89 && layer.GetComponentsInChildren<Image>().Length > 0,
                "Boss危险未在全高优先区域池中保留");
            Require(layer.childCount + ground.childCount <= 90, "抢占后总池超预算");
            effects.Dispose();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("技能层次验收通过：施放主效果、多目标去重、区域保留与总池预算");
    }

    public static void CaptureShowcase(GameDatabase database)
    {
        string[] roster = { "lv_bu", "zhou_yu", "zhuge_liang", "huang_zhong", "zhao_yun", "guan_yu", "zhang_fei", "ma_chao", "hua_tuo", "lu_su" };
        int[] slots = { 4, 5, 6, 25, 12, 29, 16, 23, 9, 20 };
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            LevelDefinition level = database.FindLevel("huangjin");
            scene.Flow.BeginBattle(level, roster.ToList());
            BattleSession b = scene.Flow.CurrentSession;
            VerifySlotRaycasts(scene, "空场");
            bool charge = false, slash = false, fields = false, full = false;
            var regions = new List<CombatFieldSnapshot>(); var enemies = new List<EnemyFeedbackSnapshot>();
            for (int step = 0; step < 12000 && b.State != BattleState.Victory && b.State != BattleState.Defeat; step++)
            {
                for (int index = 0; index < roster.Length; index++)
                {
                    if (index >= 3 && b.Heroes.Any(h => (h.Definition.Id == "zhou_yu" || h.Definition.Id == "zhuge_liang") && h.Level < 2)) break;
                    HeroDefinition hero = database.FindHero(roster[index]);
                    if (!b.IsRecruited(hero.Id) && !b.CanAffordTransaction(hero.Cost)) break;
                    if (!b.IsRecruited(hero.Id) && b.CanAffordTransaction(hero.Cost)) Require(b.TryRecruit(hero.Id, slots[index]) == null, "技能实景正常招募失败");
                }
                foreach (HeroUnit hero in b.Heroes)
                    if ((hero.Definition.Id == "zhou_yu" || hero.Definition.Id == "zhuge_liang") && hero.Level < 2 && b.CanAffordTransaction(b.GetUpgradeCost(hero.SlotId)))
                        Require(b.TryUpgrade(hero.SlotId) == null, "技能实景正常升级失败");
                if (b.State == BattleState.Preparation) b.Start();
                scene.Flow.Tick(.05f);
                HeroUnit general = b.Heroes.Find(h => h.Definition.Id == "lv_bu");
                float until = general == null ? 0 : (float)typeof(HeroUnit).GetField("ChargeUntil", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(general);
                if (!charge && until > b.Time + .1f) { UiRaceChecks.CaptureThree(scene, "sensation-lvbu-charge"); charge = true; }
                if (!slash && scene.Root.GetComponentsInChildren<BattleAttackGraphic>().Any()) { scene.Flow.Tick(.1f); UiRaceChecks.CaptureThree(scene, "sensation-lvbu-slash"); slash = true; }
                b.ReadCombatFeedback(regions, enemies);
                if (!fields && regions.Any(r => r.Kind == "fire") && regions.Any(r => r.Kind == "wind"))
                { UiRaceChecks.CaptureThree(scene, "sensation-fire-wind"); fields = true; }
                if (!full && b.Heroes.Count == 10)
                { UiRaceChecks.CaptureThree(scene, "sensation-ten-generals"); VerifySlotRaycasts(scene, "十将"); full = true; }
                if (charge && slash && fields && full) break;
            }
            Require(charge && slash && fields && full, "正常经济未捕获蓄力/主斩/火风区域/十将实景");
            Debug.Log("技能实景12图通过：正常经济，无补钱/改血/强制敌人");
        }
    }

    private static void VerifySlotRaycasts(UiRefinementChecks.Scene scene, string state)
    {
        var eventObject = new GameObject("阵位射线事件系统", typeof(EventSystem));
        EventSystem events = eventObject.GetComponent<EventSystem>();
        RaycasterManager.GetRaycasters().Add(scene.Raycaster);
        RenderTexture previous = scene.Camera.targetTexture;
        try
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2340, 1080) })
            {
                RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
                try
                {
                    ((RectTransform)scene.Root.parent).sizeDelta = new Vector2(size.x, size.y);
                    scene.Camera.orthographicSize = size.y / 2f;
                    scene.Camera.transform.position = new Vector3(size.x / 2f, -size.y / 2f, -100);
                    scene.Camera.targetTexture = target; scene.Camera.aspect = (float)size.x / size.y;
                    SafeAreaLayout.Apply(scene.Root, size.x, size.y, size.x == 2340 ? new Rect(96, 42, 2160, 1008) : new Rect(0, 0, size.x, size.y));
                    Canvas.ForceUpdateCanvases(); scene.Camera.Render();
                    SlotHitArea[] areas = scene.Root.GetComponentsInChildren<SlotHitArea>();
                    Require(areas.Length == 30, "黄巾完整候选未生成30个点击区");
                    foreach (SlotHitArea area in areas)
                    {
                        RectTransform rect = (RectTransform)area.transform;
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(scene.Camera, rect.TransformPoint(rect.rect.center));
                        var pointer = new PointerEventData(events) { position = point };
                        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                        Require(hits.Count > 0 && hits[0].gameObject == area.gameObject, state + "阵位中心被遮挡/串位：" + point);
                        SlotRaycasts++;
                    }
                }
                finally { scene.Camera.targetTexture = previous; RenderTexture.ReleaseTemporary(target); }
            }
        }
        finally
        {
            RaycasterManager.GetRaycasters().Remove(scene.Raycaster); UnityEngine.Object.DestroyImmediate(eventObject);
            ((RectTransform)scene.Root.parent).sizeDelta = new Vector2(1920, 1080);
            scene.Camera.orthographicSize = 540; scene.Camera.transform.position = new Vector3(960, -540, -100);
            scene.Camera.targetTexture = previous; scene.Camera.aspect = 16f / 9f;
            SafeAreaLayout.Apply(scene.Root, 1920, 1080, new Rect(0, 0, 1920, 1080));
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
