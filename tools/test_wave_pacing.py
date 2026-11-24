"""对照精修前目录验证黄巾短批预算，明确允许Boss突破损失调整。"""

import collections
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "tools/fixtures/wave_pacing_baseline.json"
DATABASE = ROOT / "UnityGame/Assets/Resources/Game/database.json"
SIGNATURE = ("EnemyId", "RouteId", "HealthScale", "SpeedScale")


def signature(group):
    return tuple(group[key] for key in SIGNATURE)


def budget(wave):
    result = collections.Counter()
    for group in wave["Groups"]:
        result[signature(group)] += group["Count"]
    return result


def spawn_times(groups):
    return sorted(group["Delay"] + index * group["Interval"]
                  for group in groups for index in range(group["Count"]))


class WavePacingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.old = json.loads(BASELINE.read_text(encoding="utf-8"))
        cls.current = json.loads(DATABASE.read_text(encoding="utf-8"))
        cls.old_levels = {level["Id"]: level for level in cls.old["Levels"]}
        cls.levels = {level["Id"]: level for level in cls.current["Levels"]}

    def test_faction_mapping_is_exact_and_covers_all_25_heroes(self):
        # 固定设计归属独立于生成器，不能为通过旧比较而直接忽略未知阵营。
        members = {
            "wei": ("xu_chu", "guo_jia", "cao_cao", "xun_yu", "sima_yi", "zhang_liao"),
            "shu": ("guan_yu", "huang_zhong", "zhao_yun", "zhang_fei", "zhuge_liang", "ma_chao", "pang_tong", "jiang_wei"),
            "wu": ("taishi_ci", "zhou_yu", "lu_xun", "lv_meng", "sun_shangxiang", "lu_su"),
            "qun": ("lv_bu", "yuan_shao", "zhang_jiao", "diao_chan", "hua_tuo"),
        }
        expected = {hero_id: faction for faction, heroes in members.items() for hero_id in heroes}
        self.assertEqual(25, len(self.current["Heroes"]))
        self.assertEqual(expected, {hero["Id"]: hero.get("Faction") for hero in self.current["Heroes"]})

    def test_catalog_only_changes_approved_growth_faction_and_boss_breach_cost(self):
        # 成长预算另行验收，Faction由上项精确锁定；其余属性与四关排程继续保护。
        previous_bosses = [enemy for enemy in self.old["Enemies"] if enemy["IsBoss"]]
        self.assertEqual(8, len(previous_bosses))
        self.assertTrue(all(enemy["CastleDamage"] == 10 for enemy in previous_bosses))
        expected = {key: value for key, value in self.old.items() if key != "Levels"}
        expected["Enemies"] = [dict(enemy, CastleDamage=15) if enemy["IsBoss"] else enemy
                               for enemy in self.old["Enemies"]]
        current = {key: value for key, value in self.current.items() if key != "Levels"}
        growth_fields = {"BasicAttackInterval", "BasicDamageScale", "SkillCooldown", "SkillDamageScale", "Faction"}
        current["Heroes"] = [{key: value for key, value in hero.items() if key not in growth_fields}
                             for hero in current["Heroes"]]
        expected["Heroes"] = [dict(hero, Description="持续普攻并独立蓄力，挥动画戟发动近距重斩。")
                              if hero["Id"] == "lv_bu" else hero for hero in expected["Heroes"]]
        self.assertEqual(expected, current)
        self.assertEqual(list(self.old_levels), list(self.levels))
        for level_id, previous in self.old_levels.items():
            if level_id != "huangjin":
                with self.subTest(level=level_id):
                    self.assertEqual(previous, self.levels[level_id])

    def test_map_economy_tutorial_and_wave_waits_are_unchanged(self):
        old = self.old_levels["huangjin"]
        current = self.levels["huangjin"]
        self.assertEqual({key: value for key, value in old.items() if key not in {"Waves", "Slots", "CombatRuleVersion"}},
                         {key: value for key, value in current.items() if key not in {"Waves", "Slots", "CombatRuleVersion"}})
        self.assertEqual(current["CombatRuleVersion"], "sensation-hj-mn-v1")
        self.assertEqual(len(current["Slots"]), 30)
        self.assertEqual(old["Waves"][:3], current["Waves"][:3])
        self.assertEqual(len(old["Waves"]), len(current["Waves"]))
        for previous, wave in zip(old["Waves"], current["Waves"]):
            self.assertEqual({key: value for key, value in previous.items() if key != "Groups"},
                             {key: value for key, value in wave.items() if key != "Groups"})

    def test_every_wave_preserves_enemy_route_health_speed_budget_and_boss_schedule(self):
        previous_waves = self.old_levels["huangjin"]["Waves"]
        waves = self.levels["huangjin"]["Waves"]
        boss_waves = []
        for index, (previous, wave) in enumerate(zip(previous_waves, waves)):
            with self.subTest(wave=index + 1):
                expected = budget(previous)
                if index == 3:
                    expected[signature(previous["Groups"][0])] += 4
                self.assertEqual(expected, budget(wave))
                before = [group for group in previous["Groups"] if group["EnemyId"].startswith("boss_")]
                after = [group for group in wave["Groups"] if group["EnemyId"].startswith("boss_")]
                self.assertEqual(before, after)
                if after:
                    boss_waves.append((index, after))
        self.assertEqual([(len(waves) - 1, [previous_waves[-1]["Groups"][-1]])], boss_waves)
        self.assertEqual("boss_zhang_jiao", boss_waves[0][1][0]["EnemyId"])

    def test_later_main_troops_have_compact_batches_and_long_breaths(self):
        # 固定旧配置里的五个主兵组为验收对象，避免用生成器算法验证自身。
        for index in range(3, 8):
            previous = self.old_levels["huangjin"]["Waves"][index]
            wave = self.levels["huangjin"]["Waves"][index]
            source = previous["Groups"][0]
            batches = sorted((group for group in wave["Groups"]
                              if signature(group) == signature(source)), key=lambda group: group["Delay"])
            with self.subTest(wave=index + 1):
                self.assertGreaterEqual(len(batches), 3, "后五波主兵仍均匀长队，尚未形成短批")
                for batch in batches:
                    self.assertGreaterEqual(batch["Count"], 2)
                    self.assertLessEqual(batch["Count"], 4)
                    self.assertGreater(batch["Interval"], 0)
                    self.assertLessEqual(batch["Interval"], .65)
                    self.assertEqual(set(source), set(batch))
                    self.assertEqual(signature(source), signature(batch))
                pauses = [following["Delay"] - (current["Delay"] + (current["Count"] - 1) * current["Interval"])
                          for current, following in zip(batches, batches[1:])]
                self.assertGreaterEqual(min(pauses), 2, "短批间至少留两秒喘息")
                self.assertGreaterEqual(min(pauses), 3 * max(batch["Interval"] for batch in batches))
                times = spawn_times(batches)
                self.assertAlmostEqual(source["Delay"], times[0], places=4)
                self.assertAlmostEqual(spawn_times([source])[-1], times[-1], places=4,
                                       msg="最后一兵保持原时刻，不能靠提早全部出完制造加速")
                self.assertEqual(previous["Groups"][1:],
                                 [group for group in wave["Groups"] if signature(group) != signature(source)])


if __name__ == "__main__":
    unittest.main(verbosity=2)
