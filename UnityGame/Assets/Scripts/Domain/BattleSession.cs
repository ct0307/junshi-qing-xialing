using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public sealed partial class BattleSession
    {
        public const int MaxHeroLevel = 3;
        public BattleState State { get; private set; }
        public int Gold { get; private set; }
        public int CastleHealth { get; private set; }
        public int CurrentWave { get; private set; }
        public int TotalWaves { get { return level.Waves.Count; } }
        public float Time { get; private set; }
        public float MarchTime { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public int SpeedMask { get; private set; }
        public int BossBreachCount { get; private set; }
        public float Speed { get; private set; }
        // 情报只读且不消费事件；暂停时沿用冻结的游戏时钟。
        public bool IsWaveActive { get { return waveActive && State != BattleState.Victory && State != BattleState.Defeat; } }
        public float NextWaveCountdown
        {
            get
            {
                if (TotalWaves == 0 || State == BattleState.Victory || State == BattleState.Defeat || waveActive) return 0;
                if (State == BattleState.Preparation) return Math.Max(0, level.Waves[0].StartDelay) / Speed;
                return CurrentWave < TotalWaves ? Math.Max(0, nextWaveAt - MarchTime) / Speed : 0;
            }
        }
        public int RemainingEnemies
        {
            get
            {
                int count = Enemies.Count;
                foreach (PendingSpawn spawn in pending) count += spawn.Remaining;
                return count;
            }
        }
        public int DefeatedCount { get; private set; }
        public int LeakedCount { get; private set; }
        public int GoldSpent { get; private set; }
        public int AvailableSupplyAdvance
        {
            get { HeroUnit supply = FindAdvanceSupplier(); return supply == null ? 0 : AvailableSupply(supply); }
        }
        public bool CanAffordTransaction(int cost) { return cost >= 0 && GetTransactionShortfall(cost) == 0; }
        public int GetTransactionShortfall(int cost) { return Math.Max(0, cost - Gold - AvailableSupplyAdvance); }
        public string DefeatReason { get { return defeatReason; } }
        public BreachRecord LastBreach { get; private set; }
        public BreachRecord FirstBreach { get; private set; }
        public List<HeroUnit> Heroes { get; private set; }
        public List<EnemyUnit> Enemies { get; private set; }
        private readonly GameDatabase database;
        private readonly LevelDefinition level;
        private readonly HashSet<string> roster = new HashSet<string>();
        private readonly HashSet<string> excluded = new HashSet<string>();
        private readonly HashSet<string> recruited = new HashSet<string>();
        private readonly List<BattleEvent> events = new List<BattleEvent>();
        private readonly List<PendingSpawn> pending = new List<PendingSpawn>();
        private double accumulator, clock, marchClock, income;
        private float stepMarchDelta;
        private readonly Queue<ClockSlice> clockSlices = new Queue<ClockSlice>();
        private bool waveActive;
        private float nextWaveAt;
        private int nextEnemyId;
        private int nextEffectId;
        private string defeatReason = "";
        private const double Step = 1.0 / 30.0;

        private sealed class PendingSpawn
        {
            public SpawnGroup Group;
            public int Remaining;
            public float NextAt;
        }
        private sealed class ClockSlice { public double Remaining; public int Speed; }

        public BattleSession(GameDatabase database, LevelDefinition level, IEnumerable<string> roster, bool factionEnabled = false)
        {
            if (database == null || level == null || roster == null) throw new ArgumentNullException("战斗配置与阵容不能为空");
            this.database = database; this.level = level;
            FactionEnabled = factionEnabled;
            State = BattleState.Preparation; Gold = level.InitialGold; CastleHealth = level.CastleMaxHealth; Speed = 1;
            Heroes = new List<HeroUnit>(); Enemies = new List<EnemyUnit>();
            foreach (string id in roster)
            {
                if (database.FindHero(id) == null) throw new ArgumentException("阵容包含不存在的武将：" + id);
                this.roster.Add(id);
            }
            if (this.roster.Count > 10) throw new ArgumentException("战前最多选择10名武将");
            foreach (string id in level.BossHeroIds) excluded.Add(id);
            foreach (WaveDefinition wave in level.Waves)
                foreach (SpawnGroup group in wave.Groups)
                {
                    EnemyDefinition enemy = database.FindEnemy(group.EnemyId);
                    if (enemy == null || level.Routes.Find(x => x.Id == group.RouteId) == null) throw new ArgumentException("波次引用了不存在的敌军或路线");
                    if (enemy.IsBoss && !string.IsNullOrEmpty(enemy.CharacterId)) excluded.Add(enemy.CharacterId);
                    if (enemy.IsBoss) ExpectedBossCount += group.Count;
                }
        }
        public bool IsRecruited(string heroId) { return recruited.Contains(heroId); }
        public bool IsInRoster(string heroId) { return roster.Contains(heroId) && !excluded.Contains(heroId); }
        public static float GetMaximumRange(HeroDefinition definition)
        {
            return GetMaximumRange(definition, 1);
        }
        public static float GetMaximumRange(HeroDefinition definition, int heroLevel)
        {
            if (definition == null) return 0;
            HeroGrowthProfile growth = HeroGrowth.Resolve(definition, heroLevel);
            return Math.Max(definition.Range * growth.MaximumRangeMultiplier, definition.Skill == "stun" ? growth.OuterControlRadius : 0);
        }
        public static string GetGrowthDescription(HeroDefinition definition, int heroLevel) { return HeroGrowth.GetDescription(definition, heroLevel); }
        public static string GetAttackTimingDescription(HeroDefinition definition)
        { return GetAttackTimingDescription(definition, 1); }
        public static string GetAttackTimingDescription(HeroDefinition definition, int heroLevel)
        {
            if (definition == null) return "";
            string basic = "普攻每" + FormatSeconds(GetBasicCooldown(definition)) + "秒；";
            if (definition.Skill == "assist") return basic + "附近友军命中时协射，协射冷却" + FormatSeconds(AssistCooldown) + "秒";
            if (definition.Skill == "ambush") return basic + "每名敌人进入范围时伏击一次";
            string cooldown = FormatSeconds(GetAttackCooldown(definition));
            float charge = GetAttackCharge(definition);
            if (charge > 0) return basic + "蓄力" + FormatSeconds(charge) + "秒，出手后冷却" + cooldown + "秒";
            return basic + "技能冷却" + cooldown + "秒（按实秒）";
        }
        private static string FormatSeconds(float value) { return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
        public int GetUpgradeCost(int slotId)
        {
            HeroUnit hero = Heroes.Find(x => x.SlotId == slotId);
            return hero == null || hero.Level >= MaxHeroLevel ? 0 : (int)Math.Ceiling(hero.Definition.Cost * (hero.Level == 1 ? 0.75 : 1.25));
        }
        public string GetUpgradeDescription(int slotId)
        {
            HeroUnit hero = Heroes.Find(x => x.SlotId == slotId);
            if (hero == null) return "";
            if (hero.Level >= MaxHeroLevel) return "已达最高阶";
            return HeroGrowth.GetUpgradeSummary(hero.Definition, hero.Level);
        }
        public string TryRecruit(string heroId, int slotId)
        {
            if (!CanDeploy()) return "当前状态不能招募武将";
            HeroDefinition definition = database.FindHero(heroId);
            if (definition == null) return "武将不存在";
            if (excluded.Contains(heroId)) return "本关Boss不能加入己方";
            if (!roster.Contains(heroId)) return "该武将未加入本局阵容";
            if (IsRecruited(heroId)) return "每名武将每局只能招募一次";
            SlotDefinition slot = level.Slots.Find(x => x.Id == slotId);
            if (slot == null) return "阵位不存在";
            if (Heroes.Exists(x => x.SlotId == slotId)) return "该阵位已有武将";
            if (!TryFundTransaction(definition.Cost)) return "军饷不足，还差" + (definition.Cost - Gold) + "军饷";
            Gold -= definition.Cost; GoldSpent += definition.Cost;
            recruited.Add(heroId);
            Heroes.Add(new HeroUnit { Definition = definition, SlotId = slotId, Position = slot.Position, NextAttackAt = Time, WaveSupplyBudget = waveActive ? 15 : 0 });
            RecordRaceAction("recruit", Heroes[Heroes.Count - 1]);
            Emit("recruit", definition.Id, slot.Position, slot.Position, definition.Cost, definition.Name + "已出阵");
            return null;
        }
        public string TryMoveHero(int fromSlotId, int toSlotId)
        {
            if (State != BattleState.Preparation) return "仅备战阶段可以调位";
            if (level.Slots.Find(x => x.Id == fromSlotId) == null) return "来源阵位不存在";
            HeroUnit hero = Heroes.Find(x => x.SlotId == fromSlotId);
            if (hero == null) return "来源阵位没有武将";
            SlotDefinition destination = level.Slots.Find(x => x.Id == toSlotId);
            if (destination == null) return "目标阵位不存在";
            if (fromSlotId == toSlotId) return null;
            if (Heroes.Exists(x => x.SlotId == toSlotId)) return "目标阵位已有武将";
            Point2 origin = hero.Position;
            hero.SlotId = toSlotId; hero.Position = destination.Position;
            RecordRaceAction("move", hero);
            Emit("move", hero.Definition.Id, origin, hero.Position, 0, hero.Definition.Name + "已调位");
            return null;
        }
        public string TryUpgrade(int slotId)
        {
            if (!CanDeploy()) return "当前状态不能升级武将";
            HeroUnit hero = Heroes.Find(x => x.SlotId == slotId);
            if (hero == null) return "该阵位没有武将";
            if (hero.Level >= MaxHeroLevel) return "武将已满级";
            int cost = GetUpgradeCost(slotId);
            if (!TryFundTransaction(cost)) return "军饷不足，还差" + (cost - Gold) + "军饷";
            Gold -= cost; GoldSpent += cost; hero.Level++;
            RecordRaceAction("upgrade", hero);
            Emit("upgrade", hero.Definition.Id, hero.Position, hero.Position, hero.Level, "升至" + hero.Level + "级");
            return null;
        }
        private bool CanDeploy() { return State == BattleState.Preparation || State == BattleState.Running; }
        public void Start()
        {
            if (State != BattleState.Preparation) return;
            State = BattleState.Running; nextWaveAt = TotalWaves > 0 ? level.Waves[0].StartDelay : 0;
        }
        public void SetPaused(bool paused)
        {
            if (paused && State == BattleState.Running) State = BattleState.Paused;
            else if (!paused && State == BattleState.Paused) State = BattleState.Running;
        }
        public void SetSpeed(int speed)
        {
            if (speed < 1 || speed > 3) throw new ArgumentOutOfRangeException("speed", "倍速仅支持1、2、3");
            Speed = speed;
        }
        public void Tick(float realDeltaTime)
        {
            if (State != BattleState.Running || realDeltaTime <= 0 || float.IsNaN(realDeltaTime) || float.IsInfinity(realDeltaTime)) return;
            ElapsedSeconds += realDeltaTime;
            SpeedMask |= 1 << ((int)Speed - 1);
            accumulator += realDeltaTime;
            clockSlices.Enqueue(new ClockSlice { Remaining = realDeltaTime, Speed = (int)Speed });
            int steps = 0;
            while (accumulator + 0.0000001 >= Step && State == BattleState.Running && steps++ < 900)
            {
                accumulator -= Step; clock += Step; Time = (float)clock;
                stepMarchDelta = (float)ConsumeMarchTime(Step); marchClock += stepMarchDelta; MarchTime = (float)marchClock;
                Advance((float)Step);
            }
        }
        private double ConsumeMarchTime(double seconds)
        {
            double march = 0;
            while (seconds > .000000001 && clockSlices.Count > 0)
            {
                ClockSlice slice = clockSlices.Peek(); double used = Math.Min(seconds, slice.Remaining);
                RecordRaceTime(used, slice.Speed);
                march += used * slice.Speed; seconds -= used; slice.Remaining -= used;
                if (slice.Remaining < .000000001) clockSlices.Dequeue();
            }
            // 原固定步容差可提前消费不足十万分之一秒；只校正摘要，不改变进军算法。
            if (seconds > 0) RecordRaceTime(seconds, lastRaceSpeed == 0 ? (int)Speed : lastRaceSpeed);
            return march;
        }
        private void Advance(float delta)
        {
            income += level.IncomePerSecond * (double)stepMarchDelta;
            int earned = (int)(income + 0.00001); if (earned > 0) { Gold += earned; income -= earned; }
            if (!waveActive && CurrentWave < TotalWaves && MarchTime + 0.0001f >= nextWaveAt) BeginWave();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingSpawn spawn = pending[i];
                while (spawn.Remaining > 0 && MarchTime + 0.0001f >= spawn.NextAt)
                {
                    Spawn(database.FindEnemy(spawn.Group.EnemyId), level.Routes.Find(x => x.Id == spawn.Group.RouteId), spawn.Group.HealthScale, spawn.Group.SpeedScale, 0);
                    spawn.Remaining--; spawn.NextAt += spawn.Group.Interval;
                }
                if (spawn.Remaining == 0) pending.RemoveAt(i);
            }
            AdvanceEnemies(delta);
            if (State != BattleState.Running) return;
            AdvanceCombat(delta);
            if (waveActive && pending.Count == 0 && Enemies.Count == 0)
            {
                waveActive = false; WaveBenefits();
                if (CurrentWave < TotalWaves) nextWaveAt = MarchTime + level.Waves[CurrentWave].StartDelay;
            }
            if (CurrentWave == TotalWaves && !waveActive && Enemies.Count == 0) Finish(BattleState.Victory);
        }
        private void BeginWave()
        {
            WaveDefinition wave = level.Waves[CurrentWave++]; waveActive = true;
            foreach (HeroUnit hero in Heroes) { hero.WaveBenefitUsed = 0; hero.LeakedHealUsed = false; hero.WaveSupplyPaid = 0; hero.WaveSupplyBudget = 12 + 3 * hero.Level; }
            foreach (SpawnGroup group in wave.Groups) pending.Add(new PendingSpawn { Group = group, Remaining = group.Count, NextAt = MarchTime + group.Delay });
            Emit("wave", null, new Point2(), new Point2(), CurrentWave, wave.Name);
        }
        private EnemyUnit Spawn(EnemyDefinition definition, RouteDefinition route, float healthScale, float speedScale, float travelled)
        {
            float length = 0;
            for (int i = 1; i < route.Points.Count; i++) length += Point2.Distance(route.Points[i - 1], route.Points[i]);
            EnemyUnit enemy = new EnemyUnit { Id = ++nextEnemyId, Definition = definition, Route = route, RouteLength = length, MaxHealth = definition.MaxHealth * healthScale, MoveSpeed = definition.Speed * speedScale, Travelled = travelled, NextAbilityAt = Time + 5 + (nextEnemyId % 3) * 1.5f };
            enemy.Health = enemy.MaxHealth; Place(enemy); Enemies.Add(enemy);
            RegisterBossSpawn(enemy);
            foreach (HeroUnit hero in Heroes) if (hero.Definition.Skill == "supply" && hero.Level >= 2 && hero.WaveSupplyPaid == 0) PaySupply(hero, hero.WaveSupplyBudget / 2);
            if (definition.IsBoss)
            {
                Emit("notice", null, enemy.Position, enemy.Position, enemy.MaxHealth, definition.Name + "登场", enemy.Id);
                Emit("boss_spawn", null, enemy.Position, enemy.Position, enemy.MaxHealth, definition.Name + "登场", enemy.Id);
            }
            return enemy;
        }
        private static void Place(EnemyUnit enemy)
        {
            enemy.Progress = Math.Min(1f, Math.Max(0, enemy.Travelled / Math.Max(0.001f, enemy.RouteLength)));
            float remaining = enemy.Travelled;
            List<Point2> points = enemy.Route.Points;
            for (int i = 1; i < points.Count; i++)
            {
                float distance = Point2.Distance(points[i - 1], points[i]);
                if (remaining <= distance && distance > 0)
                {
                    float t = remaining / distance; enemy.Position = new Point2(points[i - 1].X + (points[i].X - points[i - 1].X) * t, points[i - 1].Y + (points[i].Y - points[i - 1].Y) * t); return;
                }
                remaining -= distance;
            }
            enemy.Position = points[points.Count - 1];
        }
        private void AdvanceEnemies(float delta)
        {
            foreach (EnemyUnit enemy in Enemies.ToArray())
            {
                if (enemy.Health <= 0) continue;
                if (enemy.Definition.IsBoss && !enemy.Enraged && enemy.Health <= enemy.MaxHealth * 0.5f)
                {
                    enemy.Enraged = true; Emit("boss_phase", null, enemy.Position, enemy.Position, 0.5f, enemy.Definition.Name + "进入第二阶段", enemy.Id);
                }
                AdvanceEnemySkill(enemy);
                if (Time < enemy.StunUntil) continue;
                float factor = Time < enemy.SlowUntil ? enemy.SlowFactor : 1f;
                if (enemy.Enraged) factor *= 1.18f;
                if (Time < enemy.HasteUntil) factor *= 1.8f;
                if (Time < enemy.WarningUntil) factor *= 0.35f;
                if (IsRecovering(enemy)) factor *= 0.6f;
                enemy.Travelled += enemy.MoveSpeed * factor * stepMarchDelta; Place(enemy);
                if (enemy.Progress < 1) continue;
                // 在移除前复制抵城信息，复盘不依赖已消费事件或可变敌军对象。
                LastBreach = new BreachRecord { EnemyName = enemy.Definition.Name, RouteId = enemy.Route.Id, Health = enemy.Health, MaxHealth = enemy.MaxHealth, IsBoss = enemy.Definition.IsBoss, Wave = CurrentWave, Gold = Gold, Speed = lastRaceSpeed == 0 ? Speed : lastRaceSpeed, ElapsedSeconds = RaceElapsedSeconds };
                if (FirstBreach == null) FirstBreach = LastBreach.Copy();
                Enemies.Remove(enemy); CastleHealth = Math.Max(0, CastleHealth - enemy.Definition.CastleDamage);
                if (enemy.Definition.IsBoss) BossBreachCount++;
                Emit("leak", null, enemy.Position, enemy.Position, enemy.Definition.CastleDamage, enemy.Definition.IsBoss ? "敌将突破城防" : "敌军抵城", enemy.Id);
                if (CastleHealth == 0) { Finish(BattleState.Defeat, enemy.Definition.IsBoss ? "敌将突破，守城失败" : "城池失守"); return; }
                foreach (HeroUnit hero in Heroes) if (hero.Definition.Skill == "heal" && hero.Level >= (enemy.Definition.IsBoss ? 3 : 2) && !hero.LeakedHealUsed) { hero.LeakedHealUsed = true; HealCastle(hero); }
            }
        }
        private void AdvanceEnemySkill(EnemyUnit enemy)
        {
            if (!enemy.Definition.IsBoss)
            {
                if (enemy.Definition.Skill == "healer" && Time >= enemy.NextAbilityAt)
                {
                    enemy.NextAbilityAt = Time + 6; OnDisruptedCast(enemy); enemy.AbilityDelay = 0;
                    foreach (EnemyUnit other in Enemies) if (Point2.Distance(enemy.Position, other.Position) <= 160) other.Health = Math.Min(other.MaxHealth, other.Health + other.MaxHealth * 0.08f);
                    Emit("notice", null, enemy.Position, enemy.Position, 0, "军医恢复附近敌军", enemy.Id);
                }
                return;
            }
            if (enemy.WarningUntil > 0 && Time >= enemy.WarningUntil)
            {
                enemy.WarningUntil = 0; enemy.RecoverStartsAt = Time; enemy.RecoverUntil = Time + 2; enemy.NextAbilityAt = Time + (enemy.Enraged ? 8 : 11); enemy.AbilityDelay = 0;
                string skill = enemy.Definition.Skill;
                if (skill == "charge" || skill == "rage")
                {
                    enemy.HasteUntil = Time + (skill == "charge" ? 2.2f : 3.5f);
                    enemy.RecoverStartsAt = enemy.HasteUntil; enemy.RecoverUntil = enemy.HasteUntil + 2;
                }
                else if (skill == "summon")
                {
                    EnemyDefinition soldier = database.Enemies.Find(x => !x.IsBoss && x.Skill != "healer");
                    if (soldier != null) for (int i = 0; i < (enemy.Enraged ? 3 : 2); i++) Spawn(soldier, enemy.Route, 0.7f, 1, Math.Max(0, enemy.Travelled - (i + 1) * 40));
                }
                else if (skill == "guard")
                {
                    enemy.Health = Math.Min(enemy.MaxHealth, enemy.Health + enemy.MaxHealth * 0.06f);
                    foreach (EnemyUnit escort in Enemies) if (!escort.Definition.IsBoss && Point2.Distance(escort.Position, enemy.Position) <= 140) Emit("notice", null, enemy.Position, escort.Position, 0.15f, "护卫援护", escort.Id);
                }
                else if (skill == "disrupt") foreach (HeroUnit hero in Heroes) if (Point2.Distance(hero.Position, enemy.Position) < 300) hero.NextAttackAt = Math.Max(hero.NextAttackAt, Time + 1.2f);
                OnDisruptedCast(enemy);
                Emit("notice", null, enemy.Position, enemy.Position, 0, enemy.Definition.Name + "施展技能，随后进入破绽期", enemy.Id);
            }
            else if (enemy.WarningUntil == 0 && Time >= enemy.NextAbilityAt)
            {
                enemy.WarningUntil = Time + 1.5f;
                Emit("boss_warning", null, enemy.Position, enemy.Position, 1.5f, enemy.Definition.Name + "即将施展技能", enemy.Id);
            }
        }
        private void WaveBenefits()
        {
            foreach (HeroUnit hero in Heroes)
            {
                if (hero.Definition.Skill == "heal") HealCastle(hero);
                else if (hero.Definition.Skill == "supply")
                {
                    hero.WaveSupplyBudget = CurrentSupplyBudget(hero);
                    PaySupply(hero, hero.WaveSupplyBudget - hero.WaveSupplyPaid);
                }
            }
        }
        private void HealCastle(HeroUnit hero)
        {
            if (CastleHealth <= 0) return;
            int amount = Math.Max(0, Math.Min(Math.Min(2 - hero.WaveBenefitUsed, 8 - hero.BenefitUsed), level.CastleMaxHealth - CastleHealth));
            CastleHealth += amount; hero.BenefitUsed += amount; hero.WaveBenefitUsed += amount;
            if (amount > 0) Emit("notice", hero.Definition.Id, hero.Position, hero.Position, amount, "华佗整顿医护，恢复城防" + amount);
        }
        private static int CurrentSupplyBudget(HeroUnit hero) { return Math.Max(hero.WaveSupplyBudget, 12 + 3 * hero.Level); }
        private int AvailableSupply(HeroUnit hero)
        { return Math.Max(0, Math.Min(100 - hero.BenefitUsed, CurrentSupplyBudget(hero) - hero.WaveSupplyPaid)); }
        private void PaySupply(HeroUnit hero, int requested)
        {
            hero.WaveSupplyBudget = CurrentSupplyBudget(hero);
            int amount = Math.Max(0, Math.Min(requested, AvailableSupply(hero))); if (amount == 0) return;
            Gold += amount; hero.BenefitUsed += amount; hero.WaveSupplyPaid += amount;
            Emit("notice", hero.Definition.Id, hero.Position, hero.Position, amount, "鲁肃补给军饷" + amount);
        }
        private bool TryFundTransaction(int cost)
        {
            if (!CanAffordTransaction(cost)) return false;
            if (Gold >= cost) return true;
            HeroUnit supply = FindAdvanceSupplier();
            if (supply == null) return false;
            PaySupply(supply, cost - Gold); return true;
        }
        private HeroUnit FindAdvanceSupplier()
        {
            if (!waveActive || State != BattleState.Running) return null;
            return Heroes.Find(x => x.Definition.Skill == "supply" && HeroGrowth.Resolve(x.Definition, x.Level).SupplyTransactionAdvance);
        }
        private void Finish(BattleState state, string reason = null)
        {
            State = state;
            defeatReason = state == BattleState.Defeat ? reason ?? "城池失守" : "";
            Emit(state == BattleState.Victory ? "victory" : "defeat", null, new Point2(), new Point2(), 0, reason ?? (state == BattleState.Victory ? "守城成功" : "城池失守"));
        }
        private bool IsRecovering(EnemyUnit enemy) { return Time >= enemy.RecoverStartsAt && Time < enemy.RecoverUntil; }
        private BattleEvent Emit(string kind, string heroId, Point2 position, Point2 target, float amount, string text, int targetEnemyId = 0)
        {
            if (kind == "kill") DefeatedCount++;
            else if (kind == "leak") LeakedCount++;
            HeroUnit source = heroId == null ? null : Heroes.Find(x => x.Definition.Id == heroId);
            BattleEvent result = new BattleEvent { Kind = kind, HeroId = heroId, Position = position, Target = target, Amount = amount, Text = text, TargetEnemyId = targetEnemyId, HeroLevel = source == null ? 0 : source.Level, SkillRange = source == null ? 0 : GetMaximumRange(source.Definition, source.Level), OccurredAt = Time };
            events.Add(result); return result;
        }
        public List<BattleEvent> DrainEvents() { List<BattleEvent> result = new List<BattleEvent>(events); events.Clear(); return result; }
    }
}
