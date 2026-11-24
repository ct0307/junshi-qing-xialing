using UnityEngine;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class BattleEffects
    {
        private static bool IsRepresentative(HeroDefinition hero)
        { return hero != null && (hero.Skill == "cleave" || hero.Skill == "fire" || hero.Skill == "slow"); }

        private void ChargeCue(BattleEvent item)
        {
            Effect effect = Take(Mathf.Max(.1f, item.Amount), true);
            if (effect == null) return;
            effect.Charging = true;
            effect.Image.gameObject.SetActive(true);
            effect.Image.sprite = effect.RingSprite;
            effect.Color = new Color(1, .73f, .28f, .85f);
            effect.Image.color = effect.Color;
            effect.Image.rectTransform.sizeDelta = new Vector2(152, 152);
            UiFactory.Move(effect.Image.rectTransform, -76, -76);
            UiFactory.Move(effect.Root, item.Position.X, item.Position.Y);
        }

        private void RepresentativeCast(BattleEvent item, HeroDefinition hero)
        {
            if (!IsRepresentative(hero)) return;
            if (hero.Skill == "cleave")
            {
                // 主斩只来自真实施放，扇面保留规则角度；命中事件只负责敌人受击。
                AttackArea(item, hero, true, new Color(1, .71f, .29f, .16f));
                Sequence("slash", new Point2((item.Position.X + item.Target.X) * .5f, (item.Position.Y + item.Target.Y) * .5f - 32), .30f, 192, true);
            }
            else if (hero.Skill == "fire")
            {
                Ring(item.Position, 84, .32f, new Color(1, .65f, .31f), true);
                Projectile(item.Position, item.Target, new Color(1, .62f, .28f), .22f, 30, true);
                Sequence("fire", item.Target, .48f, 138, true);
            }
            else
            {
                Ring(item.Position, 112, .42f, UiFactory.Hex("9FD8CB"), true);
                Projectile(item.Position, item.Target, UiFactory.Hex("9FD8CB"), .25f, 28, true);
                // 一阶仅迟滞；持续风眼完全取自规则快照，不能用动画虚构。
            }
        }
    }
}
