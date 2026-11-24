using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public static class CatalogValidator
    {
        private static readonly HashSet<string> HeroSkills = new HashSet<string>(new[] { "focus", "sniper", "duelist", "twin", "cleave", "pierce", "fire", "spread_fire", "rain", "mark", "stun", "slow", "knockback", "chain", "ambush", "buff", "vulnerable", "link", "assist", "disrupt", "execute", "splash", "adaptive", "heal", "supply" });
        private static readonly HashSet<string> EnemySkills = new HashSet<string>(new[] { "normal", "shield", "runner", "armored", "healer", "charge", "summon", "guard", "rage", "disrupt" });
        public static List<string> Validate(GameDatabase database)
        {
            List<string> errors = new List<string>();
            if (database == null) { errors.Add("游戏目录为空"); return errors; }
            if (database.SchemaVersion != 1) errors.Add("不支持的目录版本");
            if (database.Heroes == null || database.Enemies == null || database.Levels == null) { errors.Add("角色、敌军与关卡列表不能为空"); return errors; }
            HashSet<string> heroes = new HashSet<string>(), enemies = new HashSet<string>(), levels = new HashSet<string>();
            foreach (HeroDefinition hero in database.Heroes)
            {
                if (hero == null) { errors.Add("角色条目为空"); continue; }
                Unique(heroes, hero.Id, "角色", errors);
                if (!string.IsNullOrEmpty(hero.Faction) && !FactionRules.IsKnown(hero.Faction)) errors.Add("角色阵营未知：" + hero.Id);
                if (!HeroSkills.Contains(hero.Skill ?? "")) errors.Add("角色技能未知：" + hero.Id);
                if (hero.Cost <= 0 || !Positive(hero.Range) || !Positive(hero.Damage) || !Positive(hero.AttackInterval)) errors.Add("角色价格、射程、伤害与攻速必须大于零：" + hero.Id);
            }
            foreach (EnemyDefinition enemy in database.Enemies)
            {
                if (enemy == null) { errors.Add("敌军条目为空"); continue; }
                Unique(enemies, enemy.Id, "敌军", errors);
                if (!string.IsNullOrEmpty(enemy.Skill) && !EnemySkills.Contains(enemy.Skill)) errors.Add("敌军技能未知：" + enemy.Id);
                if (!Positive(enemy.MaxHealth) || !Positive(enemy.Speed) || !Nonnegative(enemy.Armor) || enemy.Armor > 0.8f || enemy.CastleDamage <= 0 || enemy.GoldReward < 0) errors.Add("敌军属性无效：" + enemy.Id);
                if (enemy.IsBoss && !heroes.Contains(enemy.CharacterId ?? "")) errors.Add("Boss角色身份不存在：" + enemy.Id);
            }
            foreach (LevelDefinition level in database.Levels)
            {
                if (level == null) { errors.Add("关卡条目为空"); continue; }
                Unique(levels, level.Id, "关卡", errors);
                if (level.InitialGold < 0 || !Nonnegative(level.IncomePerSecond) || level.CastleMaxHealth <= 0) errors.Add("关卡经济或城防数值无效：" + level.Id);
                if (level.Routes == null || level.Slots == null || level.Waves == null || level.BossHeroIds == null || level.RecommendedRoster == null) { errors.Add("关卡列表字段为空：" + level.Id); continue; }
                HashSet<string> routes = new HashSet<string>(), declaredBosses = new HashSet<string>(), actualBosses = new HashSet<string>();
                HashSet<int> slots = new HashSet<int>();
                foreach (RouteDefinition route in level.Routes)
                {
                    if (route == null) { errors.Add("路线条目为空：" + level.Id); continue; }
                    Unique(routes, route.Id, "路线", errors);
                    if (route.Points == null || route.Points.Count < 2) { errors.Add("路线至少需要两个点：" + route.Id); continue; }
                    float length = 0;
                    for (int i = 0; i < route.Points.Count; i++)
                    {
                        Point2 p = route.Points[i]; if (!Finite(p.X) || !Finite(p.Y)) errors.Add("路线坐标无效：" + route.Id);
                        if (i > 0) length += Point2.Distance(route.Points[i - 1], p);
                    }
                    if (!Positive(length)) errors.Add("路线长度无效：" + route.Id);
                }
                foreach (SlotDefinition slot in level.Slots)
                {
                    if (slot == null) { errors.Add("阵位条目为空：" + level.Id); continue; }
                    if (!slots.Add(slot.Id)) errors.Add("阵位编号重复：" + slot.Id);
                    if (!Finite(slot.Position.X) || !Finite(slot.Position.Y)) errors.Add("阵位坐标无效：" + slot.Id);
                }
                foreach (string id in level.BossHeroIds) { if (!heroes.Contains(id ?? "")) errors.Add("关卡Boss身份不存在：" + id); if (!declaredBosses.Add(id ?? "")) errors.Add("关卡Boss身份重复：" + id); }
                if (level.Waves.Count == 0) errors.Add("关卡至少需要一波敌军：" + level.Id);
                foreach (WaveDefinition wave in level.Waves)
                {
                    if (wave == null || wave.Groups == null) { errors.Add("波次条目为空：" + level.Id); continue; }
                    if (!Nonnegative(wave.StartDelay)) errors.Add("波间延迟无效：" + level.Id);
                    foreach (SpawnGroup group in wave.Groups)
                    {
                        if (group == null) { errors.Add("出兵组为空：" + level.Id); continue; }
                        if (!enemies.Contains(group.EnemyId ?? "")) errors.Add("出兵敌军不存在：" + group.EnemyId);
                        if (!routes.Contains(group.RouteId ?? "")) errors.Add("出兵路线不存在：" + group.RouteId);
                        if (group.Count <= 0 || !Nonnegative(group.Interval) || !Nonnegative(group.Delay) || !Positive(group.HealthScale) || !Positive(group.SpeedScale)) errors.Add("出兵数量或倍率无效：" + level.Id);
                        EnemyDefinition enemy = database.Enemies.Find(x => x != null && x.Id == group.EnemyId);
                        if (enemy != null && enemy.IsBoss) actualBosses.Add(enemy.CharacterId);
                    }
                }
                foreach (string id in actualBosses) if (!declaredBosses.Contains(id)) errors.Add("Boss排除名单缺少：" + id);
                HashSet<string> recommended = new HashSet<string>();
                foreach (string id in level.RecommendedRoster)
                {
                    if (!heroes.Contains(id ?? "")) errors.Add("推荐角色不存在：" + id);
                    if (!recommended.Add(id ?? "")) errors.Add("推荐角色重复：" + id);
                    if (declaredBosses.Contains(id ?? "") || actualBosses.Contains(id ?? "")) errors.Add("推荐阵容包含本关Boss：" + id);
                }
                if (recommended.Count > 10) errors.Add("推荐阵容超过10人：" + level.Id);
                if (level.Routes.Count == 0 || level.Slots.Count == 0) errors.Add("关卡缺少路线或阵位：" + level.Id);
            }
            if (database.Heroes.Count == 0 || database.Enemies.Count == 0 || database.Levels.Count == 0) errors.Add("目录内容不能为空");
            return errors;
        }
        private static void Unique(HashSet<string> values, string id, string kind, List<string> errors)
        { if (string.IsNullOrEmpty(id)) errors.Add(kind + "编号为空"); else if (!values.Add(id)) errors.Add(kind + "编号重复：" + id); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Positive(float value) { return Finite(value) && value > 0; }
        private static bool Nonnegative(float value) { return Finite(value) && value >= 0; }
    }
}
