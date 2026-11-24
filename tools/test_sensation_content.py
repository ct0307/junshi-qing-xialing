"""冻结本轮黄巾改动边界，保护其他关卡和原角色/敌军。"""
import copy
import importlib.util
import json
from pathlib import Path
import unittest
from build_sensation_experiments import variant, BASELINE

ROOT = Path(__file__).resolve().parents[1]
DATABASE = ROOT / 'UnityGame/Assets/Resources/Game/database.json'


class SensationContentTests(unittest.TestCase):
    def test_runtime_matches_approved_mn_only(self):
        old = json.loads(BASELINE.read_text(encoding='utf-8'))
        expected = variant(old, 'MN')
        level = next(l for l in expected['Levels'] if l['Id'] == 'huangjin')
        level['CombatRuleVersion'] = 'sensation-hj-mn-v1'
        self.assertEqual(json.loads(DATABASE.read_text(encoding='utf-8')), expected)

    def test_generator_reproduces_runtime(self):
        spec = importlib.util.spec_from_file_location('content', ROOT / 'tools/build_game_content.py')
        content = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(content)
        self.assertEqual(content.make_database(), json.loads(DATABASE.read_text(encoding='utf-8')))


if __name__ == '__main__':
    unittest.main(verbosity=2)
