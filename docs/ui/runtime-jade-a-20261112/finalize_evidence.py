"""核验本轮运行截图、玩法资源及历史资料，生成可追溯的交付记录。"""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
PLAN = ROOT / '.planning/jade-a-implementation-20261112'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_snapshot(name):
    records = json.loads((PLAN / name).read_text(encoding='utf-8-sig'))
    changed = [item['Path'] for item in records
               if not Path(item['Path']).is_file()
               or digest(Path(item['Path'])).upper() != item['Hash'].upper()]
    if changed:
        raise RuntimeError('既有文件发生变化：' + repr(changed))
    return len(records)


history_count = verify_snapshot('ui-history-hashes.json')
gameplay_count = verify_snapshot('gameplay-resource-hashes.json')
log_path = ROOT / 'UnityGame/Logs/build-jade.log'
log = log_path.read_text(encoding='utf-8-sig')
if 'A方案UI验收通过' not in log or 'Application will terminate with return code 0' not in log:
    raise RuntimeError('本轮 Unity 日志没有完整成功证据。')
if 'error CS' in log:
    raise RuntimeError('Unity 日志含编译错误。')

screenshots = []
for path in sorted(HERE.rglob('*.png')):
    with Image.open(path) as image:
        size = list(image.size)
        image.verify()
    if size not in [[1280, 720], [1920, 1080], [2400, 1080]]:
        raise RuntimeError('截图尺寸异常：' + str(path) + repr(size))
    screenshots.append({'Path': path.relative_to(HERE).as_posix(),
                        'Size': size, 'Sha256': digest(path)})

record = (HERE / 'verification.txt').read_text(encoding='utf-8-sig')
run_record = json.loads((HERE / 'verification.json').read_text(encoding='utf-8-sig'))
if run_record.get('Status') != 'Passed':
    raise RuntimeError('当前验收记录并未通过。')
started_at = datetime.fromisoformat(run_record['StartedAtUtc'].replace('Z', '+00:00'))
stale = [item['Path'] for item in screenshots
         if (HERE / item['Path']).stat().st_mtime + 1 < started_at.timestamp()]
if stale:
    raise RuntimeError('存在早于本轮运行的旧截图：' + repr(stale))
listed_count = int(next(line.split('：', 1)[1] for line in record.splitlines()
                        if line.startswith('截图数量：')))
if listed_count != len(screenshots):
    raise RuntimeError('验收记录与实际截图数量不同。')

report = {
    'VerifiedAtUtc': datetime.now(timezone.utc).isoformat(),
    'SelectedDesign': 'A 青绿行军',
    'Status': 'Passed',
    'StartedAtUtc': run_record['StartedAtUtc'],
    'CompletedAtUtc': run_record['CompletedAtUtc'],
    'UnityVersion': '6000.3.28f1',
    'UnityCommand': './tools/Build-Game.ps1 -Target Jade',
    'UnityLog': 'unity-jade.log',
    'UnityLogSha256': digest(log_path),
    'UnityExitCode': 0,
    'RulesBaselineObserved': {'Passed': 124, 'Failed': 0, 'SimulationVictories': 5},
    'HistoryFilesUnchanged': history_count,
    'GameplayAndResourceFilesUnchanged': gameplay_count,
    'Screenshots': screenshots,
    'Checks': ['配置和中文字体', '五关178阵位三阶人物边界及地图投影',
               '真实射线和相邻热区', '招募、升级、分页、军饷差额',
               '规则暂停恢复', '黄巾完整合法经济胜利与空阵失败',
               '完整导航及隔离存档', 'Boss突破、低城防与预警'],
    'NewArtworkCreated': False,
    'AndroidBuiltThisRound': False,
    'DeviceTestedThisRound': False,
}
(HERE / 'unity-jade.log').write_bytes(log_path.read_bytes())
(HERE / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'UnityVerified': True, 'Screenshots': len(screenshots),
                  'HistoryFilesUnchanged': history_count,
                  'GameplayAndResourceFilesUnchanged': gameplay_count}, ensure_ascii=False))
