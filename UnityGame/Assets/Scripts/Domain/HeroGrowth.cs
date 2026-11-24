using System;
using System.Globalization;

namespace YanyiSandpan.Domain
{
    // 每次按当前等级解析，规则和界面共用；不保存可被界面修改的成长状态。
    public sealed class HeroGrowthProfile
    {
        public int Level, FocusRetainedStacks, TargetLimit, SecondaryTargetLimit, Rounds, ChainLimit;
        public int FireSeeds, FireGenerations, FireBudget, SlowAreaCount;
        public float DamageMultiplier, MaximumRangeMultiplier, LineWidth, AreaRadius, OuterControlRadius, SplashRange;
        public bool FollowupOnKill, CrossRoute, FullCircle, SecondDirection, FireEmbers, ActiveFireSpread;
        public bool RenewMark, DelayedEcho, Backstab, BackstabSplash, BuffOutside, BuffExtraAttacks;
        public bool VulnerableSpread, ReplaceLink, AssistRetarget, DisruptRecovery, DisruptRelay;
        public bool AdaptiveSwitch, AdaptiveRelay, HealSoldierLeak, HealBossBreach;
        public bool SupplyHalfAdvance, SupplyTransactionAdvance;
    }

    public static class HeroGrowth
    {
        public static HeroGrowthProfile Resolve(HeroDefinition definition, int level)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            int stage = Math.Max(1, Math.Min(3, level));
            string skill = definition.Skill;
            bool second = stage >= 2, third = stage >= 3;
            var profile = new HeroGrowthProfile
            {
                Level = stage, DamageMultiplier = 1 + .12f * (stage - 1),
                MaximumRangeMultiplier = skill == "sniper" ? (third ? 1.5f : 1.3f) : skill == "adaptive" ? 1.45f : 1,
                FocusRetainedStacks = second ? 2 : 0,
                TargetLimit = 1, SecondaryTargetLimit = second ? 3 : int.MaxValue,
                Rounds = skill == "rain" && third ? 2 : 1,
                ChainLimit = third ? 7 : second ? 5 : 4,
                FireSeeds = second ? 2 : 1, FireGenerations = third ? 2 : 1,
                FireBudget = third ? 8 : second ? 6 : 4,
                SlowAreaCount = third ? 2 : second ? 1 : 0,
                LineWidth = second ? 65 : 38,
                AreaRadius = skill == "duelist" && third ? definition.Range : 75,
                OuterControlRadius = definition.Range * (second ? 1.2f : 1),
                SplashRange = second ? 220 : 130,
                FollowupOnKill = (skill == "sniper" && second) || (skill == "pierce" && third) ||
                    (skill == "focus" && third) || (skill == "execute" && second) ||
                    (skill == "splash" && third),
                CrossRoute = third && (skill == "sniper" || skill == "execute"),
                FullCircle = third && (skill == "duelist" || skill == "cleave"),
                SecondDirection = third && (skill == "pierce" || skill == "cleave" || skill == "knockback"),
                FireEmbers = skill == "fire" && third,
                ActiveFireSpread = skill == "spread_fire" && third,
                RenewMark = skill == "mark" && third,
                DelayedEcho = skill == "stun" && third,
                Backstab = skill == "ambush" && second,
                BackstabSplash = skill == "ambush" && third,
                BuffOutside = skill == "buff" && second,
                BuffExtraAttacks = skill == "buff" && third,
                VulnerableSpread = skill == "vulnerable" && third,
                ReplaceLink = skill == "link" && third,
                AssistRetarget = skill == "assist" && second,
                DisruptRecovery = skill == "disrupt" && second,
                DisruptRelay = skill == "disrupt" && third,
                AdaptiveSwitch = skill == "adaptive" && second,
                AdaptiveRelay = skill == "adaptive" && third,
                HealSoldierLeak = skill == "heal" && second,
                HealBossBreach = skill == "heal" && third,
                SupplyHalfAdvance = skill == "supply" && second,
                SupplyTransactionAdvance = skill == "supply" && third
            };
            switch (skill)
            {
                case "twin": profile.TargetLimit = third ? 3 : 2; break;
                case "mark": case "vulnerable": profile.TargetLimit = second ? 2 : 1; break;
                case "link": profile.TargetLimit = second ? 3 : 2; break;
                case "execute": profile.TargetLimit = stage; break;
                case "rain": profile.TargetLimit = second ? 4 : 3; break;
                case "assist": profile.TargetLimit = third ? 2 : 1; break;
                case "knockback": profile.TargetLimit = second ? 3 : 1; break;
            }
            return profile;
        }

        public static string GetDescription(HeroDefinition definition, int level)
        {
            if (definition == null) return "";
            HeroGrowthProfile value = Resolve(definition, level);
            bool second = value.Level >= 2, third = value.Level >= 3;
            switch (definition.Skill)
            {
                case "focus": return "连击积累刀势，转火保留" + value.FocusRetainedStacks + "层。" + (third ? "满势重斩至多两段，击退后追斩。" : "");
                case "sniper": return "神射覆盖" + Format(value.MaximumRangeMultiplier) + "倍射界。" + (second ? (third ? "击退后可跨路线补射一次。" : "击退后同路线补射一次。") : "优先补漏。");
                case "duelist": return "单敌重击，多敌横扫。" + (second ? "重击附短暂震慑。" : "") + (third ? "横扫覆盖自身周围。" : "");
                case "twin": return "每轮至多" + value.TargetLimit + "箭。" + (second ? "单敌时集火，目标击退后重新选敌。" : "双箭分射不同敌人。");
                case "cleave": return "持续普攻，独立蓄力重斩。" + (third ? "回身扫覆盖整圈，每敌仅中一次。" : second ? "重斩覆盖前方半圆。" : "重斩覆盖朝向目标的扇面。");
                case "pierce": return "枪线半宽" + Format(value.LineWidth) + "，贯穿真实直线目标。" + (third ? "击退后转向追击一次，每敌仅中一次。" : "");
                case "fire": return (second ? "火区沿道路延伸为火带。" : "火攻留下持续燃烧区。") + (third ? "离开火带仍有余烬，同源不叠加。" : "");
                case "spread_fire": return value.FireSeeds + "个火种，至多传播" + value.FireGenerations + "代、" + value.FireBudget + "名敌人。" + (third ? "燃烧中段可主动传播。" : "击退后传播。");
                case "rain": return value.TargetLimit + "处箭雨，" + value.Rounds + "轮。" + (third ? "后轮重新瞄准，每敌每轮最多中一次。" : "优先覆盖不同敌群。");
                case "mark": return "奇谋同时挂印" + value.TargetLimit + "名敌人，延迟爆裂。" + (third ? "爆裂击退后补印一次，不再递归补印。" : "");
                case "stun": return "怒喝拦截敌群，Boss硬控不超过0.3秒。" + (third ? "回响只控制首轮未命中的后来者。" : second ? "外围另有短震慑。" : "");
                case "slow": return "八阵减速，最多维持" + value.SlowAreaCount + "个风眼。" + (third ? "优先照顾不同路线。" : "") + "重叠不叠加。";
                case "knockback": return "蓄力枪击推退至多" + value.TargetLimit + "名普通兵。" + (third ? "追加反向短扫。" : "") + "Boss不后退。";
                case "chain": return "雷链最多命中" + value.ChainLimit + "名不同敌人。" + (third ? "分叉共享去重与跳数预算。" : "附极短停顿。");
                case "ambush": return "每敌入区伏击一次。" + (second ? "离区背袭一次。" : "") + (third ? "背袭波及附近至多两敌，不继续触发。" : "");
                case "buff": return "号令提高友军伤害。" + (second ? "额外覆盖圈外最近一名友军。" : "") + (third ? "最多三名友军各追加一次普攻。" : "");
                case "vulnerable": return "同时对" + value.TargetLimit + "名强敌施加易伤。" + (third ? "击退后向附近至多两敌传递一轮。" : "") + "同类效果不叠加。";
                case "link": return "连环连接" + value.TargetLimit + "名敌人，传伤只扩散一层。" + (third ? "节点倒下可补入一名，不刷新时长。" : "");
                case "assist": return "友军合格命中时协射，独立冷却。" + (second ? "原目标已倒下时自动改射。" : "") + (third ? "每轮分两箭，不递归协射。" : "");
                case "disrupt": return "干扰特殊敌军施技，受免疫与延迟上限约束。" + (second ? "本次破绽延长0.75秒。" : "") + (third ? "可接力干扰另一名预警敌军一次。" : "");
                case "execute": return "优先残血，技能整轮至多追击" + value.TargetLimit + "名敌人。" + (third ? "追击允许跨路线。" : "");
                case "splash": return "队首攻击附后向溅射，范围" + Format(value.SplashRange) + "。" + (second ? "次目标最多3名。" : "") + (third ? "击退后冲击至多两名未受溅射者。" : "");
                case "adaptive": return "枪弓交替。" + (second ? "按敌距提前换阵，减少空挥。" : "") + (third ? "换阵首轮枪弓接力一次。" : "");
                case "heal": return "医护每波最多2、全局最多8城防。" + (third ? "普通或Boss突破后可提前修复，不救零城防。" : second ? "首次普通漏兵后可提前修复。" : "波末修复。");
                case "supply": return "补给整局最多100军饷。" + (second ? "首兵出场预支一半，波末结余。" : "波末结算。") + (third ? "招募或升阶可预支本波余款补差。" : "");
                default: return definition.Description ?? "";
            }
        }

        public static string GetUpgradeSummary(HeroDefinition definition, int currentLevel)
        {
            if (definition == null) return "";
            if (currentLevel >= 3) return "已达最高阶";
            bool third = currentLevel >= 2;
            string change;
            switch (definition.Skill)
            {
                case "focus": change = third ? "满势追斩：无 → 最多2段" : "转火保留刀势：0 → 2层"; break;
                case "sniper": change = third ? "补射：同路 → 跨路；射界1.3 → 1.5倍" : "击退补射：无 → 同路1次"; break;
                case "duelist": change = third ? "横扫：目标附近 → 自身整圈" : "单敌重击震慑：无 → 有"; break;
                case "twin": change = third ? "每轮箭数：2 → 3" : "单敌聚焦与倒下转火：无 → 有"; break;
                case "cleave": change = third ? "重斩：前方半圆 → 整圈回身扫" : "重斩：前方扇面 → 前方半圆"; break;
                case "pierce": change = third ? "转向贯穿追击：无 → 最多1次" : "贯穿半宽：38 → 65"; break;
                case "fire": change = third ? "离区余烬：无 → 有" : "火区：圆形 → 沿路火带"; break;
                case "spread_fire": change = third ? "主动传播：无 → 有；传播上限6 → 8" : "初始火种：1 → 2；传播上限4 → 6"; break;
                case "rain": change = third ? "箭雨轮数：1 → 2；后轮重新瞄准" : "箭雨落点：3 → 4"; break;
                case "mark": change = third ? "击退补印：无 → 最多1次" : "同时挂印：1 → 2名"; break;
                case "stun": change = third ? "后来敌军回响：无 → 1轮" : "怒喝外圈：1 → 1.2倍射界"; break;
                case "slow": change = third ? "持续风眼：1 → 2个" : "持续风眼：0 → 1个"; break;
                case "knockback": change = third ? "反向短扫：无 → 有" : "枪线推退目标：1 → 3名"; break;
                case "chain": change = third ? "雷链：5 → 7名，允许分叉" : "雷链：4 → 5名不同敌人"; break;
                case "ambush": change = third ? "背袭波及：0 → 最多2名" : "每敌离区背袭：0 → 1次"; break;
                case "buff": change = third ? "号令追加普攻：无 → 至多3名友军" : "圈外号令：0 → 最近1名友军"; break;
                case "vulnerable": change = third ? "易伤传递：无 → 附近至多2名" : "同时易伤目标：1 → 2名"; break;
                case "link": change = third ? "失去节点后补位：无 → 1次" : "连环节点：2 → 3名"; break;
                case "assist": change = third ? "每轮协射：1 → 2箭" : "原目标倒下后改射：无 → 有"; break;
                case "disrupt": change = third ? "预警干扰接力：无 → 1次" : "本次破绽延长：0 → 0.75秒"; break;
                case "execute": change = third ? "整轮追击：2 → 3名，可跨路" : "整轮追击：1 → 2名"; break;
                case "splash": change = third ? "击退后追加冲击：0 → 至多2名" : "后向溅射距离：130 → 220"; break;
                case "adaptive": change = third ? "换阵首轮枪弓接力：无 → 1次" : "按敌距提前换阵：无 → 有"; break;
                case "heal": change = third ? "提前医护：普通漏兵 → 含Boss突破" : "医护时机：仅波末 → 可提前"; break;
                case "supply": change = third ? "交易补差预支：无 → 本波余款" : "补给时机：仅波末 → 首兵预支一半"; break;
                default: change = "基础威力成长"; break;
            }
            HeroGrowthProfile before = Resolve(definition, currentLevel);
            HeroGrowthProfile after = Resolve(definition, currentLevel + 1);
            return change + "\n基础威力 " + Format(definition.Damage * before.DamageMultiplier) +
                " → " + Format(definition.Damage * after.DamageMultiplier);
        }

        private static string Format(float value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
