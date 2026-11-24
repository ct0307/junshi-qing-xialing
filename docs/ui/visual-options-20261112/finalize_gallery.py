"""排版三案截图并核验交付和正式工程指纹，不修改正式工程。"""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
source_manifest = json.loads((ROOT / 'UnityGame/Builds/UIRefinement-20261108/source-verification.json').read_text(encoding='utf-8-sig'))
source_mismatches = [item['Path'] for item in source_manifest['Files'] if digest(ROOT / 'UnityGame' / item['Path']) != item['Sha256']]
if source_mismatches:
    raise RuntimeError('正式工程指纹与基线不同：' + repr(source_mismatches))
asset_manifest = json.loads((HERE / 'asset-manifest.json').read_text(encoding='utf-8'))
input_mismatches = [name for name, expected in asset_manifest['SourceFiles'].items() if digest(ROOT / name) != expected]
if input_mismatches:
    raise RuntimeError('输入素材发生变化：' + repr(input_mismatches))
license_source = ROOT / 'UnityGame/Assets/Resources/Game/Fonts/OFL.txt'
if (HERE / 'assets/OFL.txt').read_bytes() != license_source.read_bytes():
    raise RuntimeError('字体许可未正确随附。')
asset_manifest['SourceFiles'][license_source.relative_to(ROOT).as_posix()] = digest(license_source)
asset_manifest['Assets'] = [{'Path': path.relative_to(HERE).as_posix(), 'Bytes': path.stat().st_size, 'Sha256': digest(path)}
                            for path in sorted((HERE / 'assets').rglob('*')) if path.is_file()]
(HERE / 'asset-manifest.json').write_text(json.dumps(asset_manifest, ensure_ascii=False, indent=2), encoding='utf-8')

checks = json.loads((HERE / 'browser-checks.json').read_text(encoding='utf-8'))
for item in checks['NormalAndEdgeStates']:
    assert item['initial']['slots'] == 32 and item['initial']['heroes'] == 5 and not item['initial']['brokenImages']
    assert item['recruit'] == {'gold': '120', 'status': '已上阵'}
    assert '免费调位' in item['moveHint'] and '还差15' in item['shortageHint']
    assert item['emptySelectionUpgradeEnabled'] is False
    assert item['rulesPaused'] == 'paused' and item['resumed'] == 'running' and item['manualPaused'] == 'paused'
assert checks['NarrowGallery']['scrollWidth'] <= checks['NarrowGallery']['width']

for name in ['a-jade', 'b-lacquer', 'c-scroll']:
    for suffix, expected in [('1280', (1280,720)), ('1920', (1920,1080)), ('shortage-1280', (1280,720))]:
        path = HERE / (name + '-' + suffix + '.png')
        with Image.open(path) as screenshot:
            assert screenshot.size == expected, (path.name, screenshot.size, expected)
            screenshot.verify()

board = Image.new('RGB', (1600, 1140), '#182126')
draw = ImageDraw.Draw(board)
font_path = HERE / 'assets/chinese.otf'
font = lambda size: ImageFont.truetype(str(font_path), size)
draw.text((32, 20), '军师请下令  /  黄巾关视觉提案', font=font(28), fill='#e1e9e1')
draw.text((1210, 30), '2026.11.12  ·  三案对比', font=font(15), fill='#9baea9')
positions = [(32, 90), (816, 90), (32, 620)]
names = [('a-jade', 'A  青绿行军', '清爽军令条 · 战场优先'), ('b-lacquer', 'B  漆金军令', '侧边名册 · 策略氛围'), ('c-scroll', 'C  丹青游记', '纸面将册 · 轻盈手绘')]
for (x, y), (key, title, subtitle) in zip(positions, names):
    draw.text((x, y), title, font=font(23), fill='#d8e6d9')
    draw.text((x + 260, y + 8), subtitle, font=font(14), fill='#93aaa3')
    with Image.open(HERE / (key + '-1280.png')) as screenshot:
        picture = screenshot.convert('RGB').resize((752, 423), Image.Resampling.LANCZOS)
        board.paste(picture, (x, y + 45))
    draw.rectangle((x, y + 45, x + 751, y + 467), outline='#43564d', width=1)
draw.text((834, 666), '挑选时，先看这三件事', font=font(28), fill='#e1e9e1')
for y, title, body in [(736, '画面气质', 'A 清爽均衡  /  B 沉稳策略  /  C 轻盈亲和'),
                       (823, '操作布局', 'A 上下军令条  /  B 侧边名册  /  C 武将册'),
                       (910, '后续美术', '新地图与人物按选定方向重绘，保留路线与阵位。')]:
    draw.text((834, y), title, font=font(20), fill='#b8cdb8')
    draw.text((834, y + 34), body, font=font(15), fill='#a6b8b2')
draw.text((834, 1018), '三案沿用正式素材，仅展示UI设计；不是新美术完成稿。', font=font(15), fill='#c5af8f')
draw.text((32, 1100), '完整细节见各方案1280 / 1920图。可组合选择布局与美术方向；正式游戏本轮未改动。', font=font(15), fill='#8fa69e')
board.save(HERE / 'overview.png')

screenshots = []
for path in sorted(HERE.glob('*.png')):
    with Image.open(path) as image:
        size = image.size
        image.verify()
    screenshots.append({'Path': path.name, 'Size': list(size), 'Sha256': digest(path)})
report = {'VerifiedAtUtc': datetime.now(timezone.utc).isoformat(), 'DeliveryChecksPassed': True,
          'FormalSourceCount': len(source_manifest['Files']), 'FormalSourceHashesUnchanged': True,
          'InputSourceCount': len(asset_manifest['SourceFiles']), 'InputSourceHashesUnchanged': True,
          'FontLicenseIncluded': True, 'Screenshots': screenshots, 'BrowserEvidence': 'browser-checks.json',
          'UiOnly': True, 'NewMapOrCharacterArtCreated': False, 'UnityVerifiedThisTurn': False,
          'Pending': ['用户选择方案', '真正地图／人物重绘', 'Unity接入、触控及性能验证']}
(HERE / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'DeliveryChecksPassed': True, 'FormalSourceFilesUnchanged': len(source_manifest['Files']),
                  'Screenshots': len(screenshots), 'FontLicenseIncluded': True}, ensure_ascii=False))
