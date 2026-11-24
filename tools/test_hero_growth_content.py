"""核对正式25人配置的节奏预算及生成可复现性，不改素材。"""

import importlib.util
import json
import math
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NEW_FIELDS = {"BasicAttackInterval", "BasicDamageScale", "SkillCooldown", "SkillDamageScale"}


class HeroGrowthContentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.database = json.loads((ROOT / "UnityGame/Assets/Resources/Game/database.json").read_text(encoding="utf-8"))
        cls.baseline = json.loads((ROOT / "tools/fixtures/wave_pacing_baseline.json").read_text(encoding="utf-8"))

    def test_all_twenty_five_heroes_have_explicit_finite_combat_budgets(self):
        self.assertEqual(25, len(self.database["Heroes"]))
        for hero in self.database["Heroes"]:
            with self.subTest(hero=hero["Id"]):
                self.assertTrue(NEW_FIELDS <= set(hero), "缺少独立普攻／技能预算")
                for field in NEW_FIELDS:
                    self.assertTrue(math.isfinite(hero[field]) and hero[field] > 0, field)
                # 试玩反馈版明确采用原普攻间隔70%，本轮正式配置范围固定为0.420～0.770秒。
                self.assertGreaterEqual(hero["BasicAttackInterval"], .420)
                self.assertLessEqual(hero["BasicAttackInterval"], .770)
                self.assertGreaterEqual(hero["SkillCooldown"], 1.5)
                self.assertLessEqual(hero["SkillCooldown"], 6)
                self.assertLessEqual(hero["BasicDamageScale"], 1)
                self.assertLessEqual(hero["SkillDamageScale"], 6)

    def test_identity_prices_and_routes_are_preserved(self):
        identity = ("Id", "Name", "Title", "Role", "Skill", "SpriteId", "Accent", "Cost", "Range", "Damage", "AttackInterval")
        for before, after in zip(self.baseline["Heroes"], self.database["Heroes"]):
            self.assertEqual({key: before[key] for key in identity}, {key: after[key] for key in identity})
        expected_counts = {level["Id"]: (30 if level["Id"] == "huangjin" else len(level["Slots"])) for level in self.baseline["Levels"]}
        self.assertEqual(expected_counts, {level["Id"]: len(level["Slots"]) for level in self.database["Levels"]})
        self.assertEqual(176, sum(len(level["Slots"]) for level in self.database["Levels"]))
        self.assertEqual([level["Routes"] for level in self.baseline["Levels"]], [level["Routes"] for level in self.database["Levels"]])
        self.assertEqual(43, sum(len(level["Waves"]) for level in self.database["Levels"]))

    def test_basic_attacks_preserve_distinct_character_rhythms(self):
        heroes = {hero["Id"]: hero for hero in self.database["Heroes"]}
        self.assertGreaterEqual(len({hero["BasicAttackInterval"] for hero in heroes.values()}), 5)
        self.assertLess(heroes["zhao_yun"]["BasicAttackInterval"], heroes["lv_bu"]["BasicAttackInterval"])
        self.assertLess(heroes["sun_shangxiang"]["BasicAttackInterval"], heroes["huang_zhong"]["BasicAttackInterval"])

    def test_generator_reproduces_all_runtime_definitions(self):
        spec = importlib.util.spec_from_file_location("growth_content_generator", ROOT / "tools/build_game_content.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        self.assertEqual(self.database, module.make_database())


if __name__ == "__main__":
    unittest.main(verbosity=2)
