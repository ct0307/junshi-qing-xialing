using UnityEngine;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class BattleEffects
    {
        public void EmphasizeCast(BattleEvent item, HeroDefinition hero)
        {
            eventAge = Mathf.Max(0, gameTime - item.OccurredAt);
            // 首次新阶技能只强调实际施放边缘，不加箭数、目标或伤害。
            if (hero.Skill == "cleave") AttackArea(item, hero, true, new Color(1, .77f, .35f, .75f), true);
            Ring(item.Position, 126, .6f, UiFactory.Gold, true);
            if (hero.Skill != "cleave") Ring(item.Target, hero.Skill == "rain" ? 110 : 142, .55f, UiFactory.Gold, false);
            eventAge = 0;
        }
    }
}
