using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 与瞬时反馈共用90对象池；持久区域在人物下方，不占用独立的无上限池。
    public sealed partial class BattleEffects
    {
        private readonly Dictionary<int, Effect> liveFields = new Dictionary<int, Effect>();
        private readonly HashSet<int> currentFields = new HashSet<int>();
        private readonly List<int> staleFields = new List<int>();
        private readonly Dictionary<string, Point2> feedbackHeroes = new Dictionary<string, Point2>();

        public void SyncCombatFeedback(List<CombatFieldSnapshot> fields, LevelDefinition level, IList<HeroUnit> heroes)
        {
            eventAge = 0;
            feedbackHeroes.Clear();
            foreach (HeroUnit hero in heroes) feedbackHeroes[hero.Definition.Id] = hero.Position;
            currentFields.Clear();
            foreach (CombatFieldSnapshot field in fields)
            {
                if (field.Until <= gameTime) continue;
                currentFields.Add(field.Id);
                if (liveFields.ContainsKey(field.Id)) continue;
                Effect effect = Take(field.Until - gameTime, true, 3);
                if (effect == null) continue;
                effect.Persistent = true; effect.FieldId = field.Id; effect.Duration = field.Until;
                effect.Root.SetParent(fieldParent, false); effect.Root.SetAsFirstSibling();
                UiFactory.Move(effect.Root, 0, 0);
                if (effect.Field == null)
                    effect.Field = ui.Rect(effect.Root, "真实技能区域", 0, 0, 1920, 1080).gameObject.AddComponent<BattleFieldGraphic>();
                effect.Field.gameObject.SetActive(true);
                effect.Field.Configure(field, field.Kind != "fire" || string.IsNullOrEmpty(field.RouteId) ? null : level.Routes.Find(route => route.Id == field.RouteId));
                liveFields.Add(field.Id, effect);
            }
            staleFields.Clear();
            foreach (KeyValuePair<int, Effect> field in liveFields)
                if (!currentFields.Contains(field.Key)) staleFields.Add(field.Key);
            foreach (int id in staleFields)
            {
                int index = active.IndexOf(liveFields[id]);
                if (index >= 0) Release(index);
            }
        }

        private void LinkCue(BattleEvent item)
        {
            Color color = UiFactory.Hex("DAB976");
            if (item.Kind == "link_hit")
            {
                if (AllowCue("link:" + item.SourceEnemyId + ":" + item.TargetEnemyId, item.Target, .10f))
                    Streak(item.Position, item.Target, color, .18f, 3);
                return;
            }
            if (item.RelatedEnemyIds == null || item.RelatedPositions == null || item.ExpiresAt <= gameTime) return;
            int count = Mathf.Min(item.RelatedEnemyIds.Length, item.RelatedPositions.Length);
            for (int a = 0; a < count; a++)
                for (int b = a + 1; b < count; b++)
                {
                    if (item.Kind == "link_replaced" && item.RelatedEnemyIds[a] != item.TargetEnemyId && item.RelatedEnemyIds[b] != item.TargetEnemyId) continue;
                    Streak(item.RelatedPositions[a], item.RelatedPositions[b], color, Mathf.Min(.35f, item.ExpiresAt - item.OccurredAt), 2.5f);
                }
        }

        private void AssistCue(BattleEvent item)
        {
            Color color = UiFactory.Hex("A9E1CF");
            Projectile(item.Position, item.Target, color, .19f, 36);
            Point2 trigger;
            if (feedbackHeroes.TryGetValue(item.TriggerHeroId, out trigger)) Streak(trigger, item.Position, color, .18f, 2);
            if (AllowCue("assist:" + item.HeroId, item.Position, .8f)) Floating(item.Position, "协射", color, .45f);
        }
    }
}
