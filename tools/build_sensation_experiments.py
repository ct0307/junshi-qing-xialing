"""从冻结配置生成独立候选，默认只写本轮实验目录。"""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.planning/combat-sensation-implementation-20261119'
BASELINE = WORK / 'baseline/UnityGame/Assets/Resources/Game/database.json'

FORMATION = (
    [(x, 215) for x in [170, 340, 510, 680, 850, 1020, 1190, 1400]]
    + [(1650, 250), (1480, 420), (1800, 465), (1540, 595), (1740, 720)]
    + [(1600, 795), (1410, 790), (1210, 790), (1010, 790), (810, 790), (610, 790), (410, 790), (240, 790)]
    + [(x, 590) for x in [430, 690, 950, 1210]]
    + [(x, 410) for x in [300, 560, 820, 1080, 1340]]
)


def variant(source, name):
    if name not in ['B', 'T', 'N', 'TN', 'M', 'MT', 'MN', 'MTN']:
        raise ValueError('未知候选：' + name)
    result = copy.deepcopy(source)
    level = next(l for l in result['Levels'] if l['Id'] == 'huangjin')
    if 'M' in name:
        level['Slots'] = [dict(Id=i, Position=dict(X=x, Y=y)) for i, (x, y) in enumerate(FORMATION, 1)]
    if 'T' in name or 'N' in name:
        count = 4 if 'N' in name else 3
        interval = .4 if 'T' in name else .55
        last_start = 17.6 - (count - 1) * interval
        template = level['Waves'][3]['Groups'][0]
        level['Waves'][3]['Groups'] = [dict(template, Count=count, Interval=interval, Delay=round(i * last_start / 3, 6)) for i in range(4)]
    fingerprint = hashlib.sha256(json.dumps(level, sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:12]
    level['CombatRuleVersion'] = 'sensation-experiment-' + name.lower() + '-' + fingerprint
    return result


def main():
    source = json.loads(BASELINE.read_text(encoding='utf-8'))
    target = WORK / 'candidates'
    target.mkdir(parents=True, exist_ok=True)
    for name in ['B', 'T', 'N', 'TN', 'M', 'MT', 'MN', 'MTN']:
        data = variant(source, name)
        path = target / (name + '.json')
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
        print(name, hashlib.sha256(path.read_bytes()).hexdigest())


if __name__ == '__main__':
    main()
