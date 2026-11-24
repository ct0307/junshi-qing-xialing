"""独立检查候选隔离、出兵预算及30槽布局，避免靠正式成绩验候选。"""
import json
from pathlib import Path
import unittest
import build_sensation_experiments as candidates

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.planning/combat-sensation-implementation-20261119/baseline/UnityGame/Assets/Resources/Game/database.json'


class SensationExperimentsTests(unittest.TestCase):
    def setUp(self):
        self.db = json.loads(SOURCE.read_text(encoding='utf-8'))

    def test_variants_do_not_mutate_source_or_other_levels(self):
        before = json.dumps(self.db, sort_keys=True)
        keys = set()
        for name in ['B', 'T', 'N', 'TN', 'M', 'MT', 'MN', 'MTN']:
            data = candidates.variant(self.db, name)
            self.assertEqual(data['Heroes'], self.db['Heroes'])
            self.assertEqual(data['Enemies'], self.db['Enemies'])
            self.assertEqual([l for l in data['Levels'] if l['Id'] != 'huangjin'],
                             [l for l in self.db['Levels'] if l['Id'] != 'huangjin'])
            key = next(l for l in data['Levels'] if l['Id'] == 'huangjin')['CombatRuleVersion']
            self.assertNotIn(key, keys)
            keys.add(key)
        self.assertEqual(json.dumps(self.db, sort_keys=True), before)

    def test_fourth_wave_keeps_endpoints_and_only_changes_main_batches(self):
        original = next(l for l in self.db['Levels'] if l['Id'] == 'huangjin')
        for name, count, interval in [('B', 12, .55), ('T', 12, .4), ('N', 16, .55), ('TN', 16, .4)]:
            level = next(l for l in candidates.variant(self.db, name)['Levels'] if l['Id'] == 'huangjin')
            self.assertEqual(level['Waves'][:3], original['Waves'][:3])
            self.assertEqual(level['Waves'][4:], original['Waves'][4:])
            groups = level['Waves'][3]['Groups']
            self.assertEqual(sum(g['Count'] for g in groups), count)
            self.assertTrue(all(g['Interval'] == interval for g in groups))
            times = sorted(g['Delay'] + n * g['Interval'] for g in groups for n in range(g['Count']))
            self.assertAlmostEqual(times[0], 0)
            self.assertAlmostEqual(times[-1], 17.6, places=4)
            self.assertTrue(all(g['HealthScale'] == 1.47 and g['SpeedScale'] == 1 for g in groups))

    def test_formation_is_complete_and_matches_approved_coordinates(self):
        level = next(l for l in candidates.variant(self.db, 'M')['Levels'] if l['Id'] == 'huangjin')
        self.assertEqual(len(level['Slots']), 30)
        self.assertEqual([s['Id'] for s in level['Slots']], list(range(1, 31)))
        self.assertEqual(level['Slots'][8]['Position'], {'X': 1650, 'Y': 250})
        self.assertEqual(level['Slots'][21]['Position'], {'X': 430, 'Y': 590})
        self.assertEqual(level['Slots'][-1]['Position'], {'X': 1340, 'Y': 410})


if __name__ == '__main__':
    unittest.main(verbosity=2)
