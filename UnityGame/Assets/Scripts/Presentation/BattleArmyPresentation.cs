using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class BattleView
    {
        private readonly UpgradeFeedbackState upgrades = new UpgradeFeedbackState();
        private readonly List<Text> factionLabels = new List<Text>();
        private static readonly string[] factionIds = { "wei", "shu", "wu", "qun" };
        private float rangeUntil;
        private float uiClock, upgradeBannerUntilUi;
        private bool armyPulseWasActive;
        private readonly int[] previousFactionTiers = new int[4];
        private readonly float[] factionPulseUntil = new float[4];
        private static readonly Vector3[] annotationCorners = new Vector3[4];
        private readonly Dictionary<UnitView, Rect> annotationBodies = new Dictionary<UnitView, Rect>();
        private readonly FactionDistanceGuide isolationGuide;

        private void RefreshFactionPlacement(HeroUnit selected, HeroDefinition definition, Point2 position, bool visible, bool previewing)
        {
            if (isolationGuide == null) return;
            isolationGuide.gameObject.SetActive(visible && definition != null);
            if (!visible || definition == null) return;
            bool qun = definition.Faction == "qun", isolated = true;
            int affected = 0;
            foreach (HeroUnit other in Session.Heroes)
            {
                if (other == selected) continue;
                if (Point2.Distance(other.Position, position) >= FactionRules.IsolationDistance) continue;
                isolated = false;
                if (other.Definition.Faction == "qun" && Session.IsFactionIsolated(other)) affected++;
            }
            isolationGuide.Show(position, Session.Heroes, selected, qun);
            if (selected == null || previewing)
            {
                string own = qun ? (isolated ? "此位孤阵：距离条件满足" : "此位孤阵：附近有友军") : "此位预览：" + FactionName(definition.Faction) + "将";
                detailInfo.text = own + "\n" + (affected > 0 ? "将影响" + affected + "名群将的孤阵资格" : "不影响现有群将孤阵资格");
            }
            else if (qun && upgrades.WaitingLevel(selected.Definition.Id) == 0)
                detailInfo.text = (isolated ? "孤阵：距离条件满足" : "孤阵：附近有友军，未生效") + "\n" +
                    (Session.GetFactionTier("qun") > 0 ? "群雄军势已成；距离线短显" : "还需招募同营至2人激活");
        }

        private static string FactionName(string id)
        { return id == "wei" ? "魏" : id == "shu" ? "蜀" : id == "wu" ? "吴" : id == "qun" ? "群" : "未归属"; }

        private void BuildFactionBar()
        {
            for (int i = 0; i < factionIds.Length; i++)
            {
                UiButton button = ui.Button(root, "", 223 + i * 204, 836, 194, 52, ShowFactionRules, BattlePalette.Mist, 20, false);
                button.Rect.name = "军势_" + factionIds[i];
                BattleTypography.Flat(button.Background);
                button.Label.color = BattlePalette.Jade;
                factionLabels.Add(button.Label);
            }
        }

        private void RefreshFactionBar()
        {
            string formed = "";
            for (int i = 0; i < factionIds.Length; i++)
            {
                int count = Session.GetFactionCount(factionIds[i]), tier = Session.GetFactionTier(factionIds[i]);
                factionLabels[i].text = FactionName(factionIds[i]) + count + (tier >= 4 ? " · 四人成势" : tier >= 2 ? " · 差" + (4 - count) + "升势" : "/2 · 待成势");
                factionLabels[i].gameObject.name = "军势文字_" + factionIds[i];
                factionLabels[i].color = tier > 0 ? BattlePalette.Jade : BattlePalette.Muted;
                if (tier > previousFactionTiers[i])
                {
                    formed += (formed.Length > 0 ? "、" : "") + FactionName(factionIds[i]) + tier + "人";
                    factionPulseUntil[i] = uiClock + .65f;
                }
                previousFactionTiers[i] = tier;
            }
            if (formed.Length > 0 && SetNotice("军势已成：" + formed + " · 方向示意，实际朝敌军"))
            { noticeUntil = Session.Time + 1.5f; upgradeBannerUntilUi = uiClock + 1.5f; }
        }

        private bool FactionPulse(string faction)
        { for (int i = 0; i < factionIds.Length; i++) if (factionIds[i] == faction) return uiClock < factionPulseUntil[i]; return false; }

        private void AdvanceArmyFeedback()
        {
            bool active = false;
            foreach (float until in factionPulseUntil) if (uiClock < until) active = true;
            if (active || armyPulseWasActive) RefreshCards();
            armyPulseWasActive = active;
            if (upgradeBannerUntilUi > 0 && uiClock >= upgradeBannerUntilUi)
            { upgradeBannerUntilUi = 0; noticeUntil = 0; RefreshHint(); }
        }

        private void ShowFactionRules()
        {
            if (detailOverlay != null) return;
            resumeAfterDetails = Session.State == BattleState.Running;
            if (resumeAfterDetails) { Session.SetPaused(true); GameAudio.SetBattlePaused(true); }
            detailOverlay = ui.Rect(root, "军势说明", 0, 0, 1920, 1080);
            Image shade = ui.Box(detailOverlay, "军势遮罩", 0, 0, 1920, 1080, new Color(.02f, .08f, .06f, .85f), false);
            shade.raycastTarget = true;
            RectTransform panel = ui.Box(detailOverlay, "军势面板", 360, 180, 1200, 720, BattlePalette.Silk, false).rectTransform;
            ui.Label(panel, "同营成势 · 两人成势，四人升势", 48, 28, 1104, 64, 34, BattlePalette.Jade);
            ui.Label(panel, "只统计已招募的不同武将；四人档替代两人档，仅惠及本阵营。", 48, 104, 1104, 56, 26, BattlePalette.Ink);
            string[] rules = {
                "魏 · 普攻伤害 +6% / +12%，含号令额外普攻；不含协射。",
                "蜀 · 对强敌的普攻与特色直击 +6% / +12%。强敌为精锐与Boss。",
                "吴 · 特色直击与持续伤害 +6% / +12%；不增加控制、箭数或火区时长。",
                "群 · 孤阵直击 +8% / +16%。距任意其他友军至少220才生效。"
            };
            for (int i = 0; i < rules.Length; i++)
                ui.Label(panel, rules[i], 48, 190 + i * 86, 1104, 72, 25, BattlePalette.Ink);
            ui.Label(panel, "群是玩法分类；华佗只计入群。军势版成绩单独记录。", 48, 536, 1104, 56, 24, BattlePalette.Muted);
            UiButton close = ui.Button(panel, "返回战场", 48, 618, 1104, 76, () => CloseDetails(true), BattlePalette.Jade, 28, false);
            close.Label.color = BattlePalette.Silk;
            RefreshHud();
        }

        private static string UpgradeSummary(HeroDefinition hero, int tier)
        {
            string text = HeroGrowth.GetUpgradeSummary(hero, Mathf.Max(1, tier - 1)).Split('\n')[0];
            int split = text.IndexOf('；');
            return split > 0 ? text.Substring(0, split) : text;
        }

        private void HandleGrowthEvent(BattleEvent item)
        {
            HeroUnit hero = Session.Heroes.Find(h => h.Definition.Id == item.HeroId);
            if (hero == null) return;
            UnitView view; heroViewsById.TryGetValue(item.HeroId, out view);
            if (item.Kind == "upgrade")
            {
                upgrades.Upgrade(item.HeroId, hero.Level);
                if (SetNotice(hero.Definition.Name + " " + hero.Level + "阶 · " + UpgradeSummary(hero.Definition, hero.Level)))
                { noticeUntil = Session.Time + .9f; upgradeBannerUntilUi = uiClock + .9f; }
                rangeUntil = Session.Time + .9f;
                RefreshCards(); RefreshHud();
            }
            else if (item.Kind == "skill_charge" && view != null && item.ExpiresAt > Session.Time)
                view.Charge(item.OccurredAt, item.ExpiresAt);
            else if (item.Kind == "skill_cast")
            {
                if (view != null) view.Cast(item.OccurredAt, hero.Definition.Skill, item.Target);
                if (upgrades.Consume(item.HeroId, item.HeroLevel, item.AttackSequenceId))
                {
                    if (Session.Time - item.OccurredAt < .5f) effects.EmphasizeCast(item, hero.Definition);
                    SetNotice(hero.Definition.Name + " · " + item.HeroLevel + "阶特色已施放");
                    RefreshCards(); RefreshHud();
                }
            }
        }

        private static Rect WorldRect(RectTransform target)
        {
            target.GetWorldCorners(annotationCorners);
            return Rect.MinMaxRect(annotationCorners[0].x, annotationCorners[0].y, annotationCorners[2].x, annotationCorners[2].y);
        }

        private void RefreshAnnotations()
        {
            // 只有未遮挡人物且远离道路的小阶徽才留在战场；姓名与阶位始终可在卡栏读取。
            annotationBodies.Clear();
            foreach (UnitView view in sorting) if (!view.IsRetired) annotationBodies[view] = WorldRect(view.BodyRect);
            foreach (UnitView hero in heroViews.Values)
            {
                bool safe = true;
                Rect badge = WorldRect(hero.RankRect);
                foreach (KeyValuePair<UnitView, Rect> other in annotationBodies)
                    if (other.Key != hero && badge.Overlaps(other.Value)) { safe = false; break; }
                if (safe)
                    foreach (RouteDefinition route in level.Routes)
                        for (int i = 1; i < route.Points.Count; i++)
                        {
                            Vector2 a = new Vector2(route.Points[i - 1].X, route.Points[i - 1].Y);
                            Vector2 b = new Vector2(route.Points[i].X, route.Points[i].Y), p = new Vector2(hero.Position.X, hero.Position.Y + 26);
                            Vector2 delta = b - a;
                            float t = delta.sqrMagnitude > .01f ? Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude) : 0;
                            if (Vector2.Distance(p, a + t * delta) < 48) safe = false;
                        }
                hero.SetRankVisible(safe);
            }
        }
    }
}
