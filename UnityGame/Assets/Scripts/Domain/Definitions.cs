using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    [Serializable]
    public struct Point2
    {
        public float X;
        public float Y;
        public Point2(float x, float y) { X = x; Y = y; }
        public static float Distance(Point2 a, Point2 b)
        { float x = a.X - b.X, y = a.Y - b.Y; return (float)Math.Sqrt(x * x + y * y); }
    }

    [Serializable]
    public class HeroDefinition
    {
        public string Id;
        public string Name;
        public string Faction;
        public string Title;
        public string Role;
        public string Description;
        public string Skill;
        public string SpriteId;
        public string Accent;
        public int Cost;
        public float Range;
        public float Damage;
        public float AttackInterval;
        // 旧配置为0时由规则按兵种和原输出预算补全。
        public float BasicAttackInterval;
        public float BasicDamageScale;
        public float SkillCooldown;
        public float SkillDamageScale;
    }

    [Serializable]
    public class EnemyDefinition
    {
        public string Id;
        public string Name;
        public string CharacterId;
        public string SpriteId;
        public string Skill;
        public float MaxHealth;
        public float Speed;
        public float Armor;
        public int CastleDamage;
        public int GoldReward;
        public bool IsBoss;
    }

    [Serializable]
    public class RouteDefinition
    {
        public string Id;
        public List<Point2> Points = new List<Point2>();
    }

    [Serializable]
    public class SlotDefinition
    {
        public int Id;
        public Point2 Position;
    }

    [Serializable]
    public class SpawnGroup
    {
        public string EnemyId;
        public string RouteId;
        public int Count;
        public float Interval;
        public float Delay;
        public float HealthScale = 1f;
        public float SpeedScale = 1f;
    }

    [Serializable]
    public class WaveDefinition
    {
        public string Name;
        public float StartDelay;
        public List<SpawnGroup> Groups = new List<SpawnGroup>();
    }

    [Serializable]
    public class LevelDefinition
    {
        public string Id;
        // 变更阵位或出兵时隔离成绩；空值沿用已有规则版本。
        public string CombatRuleVersion;
        public string Name;
        public string Subtitle;
        public string Story;
        public string Theme;
        public string MapResource;
        public int InitialGold;
        public float IncomePerSecond;
        public int CastleMaxHealth;
        public List<string> BossHeroIds = new List<string>();
        public List<string> RecommendedRoster = new List<string>();
        public List<RouteDefinition> Routes = new List<RouteDefinition>();
        public List<SlotDefinition> Slots = new List<SlotDefinition>();
        public List<WaveDefinition> Waves = new List<WaveDefinition>();
    }

    [Serializable]
    public class GameDatabase
    {
        public int SchemaVersion = 1;
        public List<HeroDefinition> Heroes = new List<HeroDefinition>();
        public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
        public List<LevelDefinition> Levels = new List<LevelDefinition>();
        public HeroDefinition FindHero(string id) { return Heroes.Find(x => x.Id == id); }
        public EnemyDefinition FindEnemy(string id) { return Enemies.Find(x => x.Id == id); }
        public LevelDefinition FindLevel(string id) { return Levels.Find(x => x.Id == id); }
    }
}
