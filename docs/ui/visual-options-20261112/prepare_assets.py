"""仅生成独立提案的资源副本和数据，不写正式工程。"""
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
GAME = ROOT / "UnityGame/Assets/Resources/Game"
ASSETS = HERE / "assets"
if ASSETS.exists():
    raise SystemExit("资源目录已存在，拒绝覆盖；请使用独立版本目录。")
database = json.loads((GAME / "database.json").read_text(encoding="utf-8-sig"))
level = next(item for item in database["Levels"] if item["Id"] == "huangjin")
hero_ids = ["guan_yu", "huang_zhong", "zhao_yun", "zhou_yu", "lu_xun"]
heroes = [next(item for item in database["Heroes"] if item["Id"] == hero_id) for hero_id in hero_ids]
sources = [GAME / "database.json", GAME / "Maps/huangjin.png", GAME / "Fonts/Chinese.otf", GAME / "Fonts/OFL.txt"]
for folder in ["Characters", "Portraits"]:
    sources.extend(GAME / folder / (hero_id + ".png") for hero_id in hero_ids)
sources.extend([GAME / "Enemies/infantry.png", GAME / "Enemies/archer.png"])
digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
before = {path.relative_to(ROOT).as_posix(): digest(path) for path in sources}
ASSETS.mkdir()
with Image.open(GAME / "Maps/huangjin.png") as original:
    original.crop((38, 144, 1882, 842)).save(ASSETS / "map.png")
for source_folder, target_folder in [("Characters", "characters"), ("Portraits", "portraits")]:
    (ASSETS / target_folder).mkdir()
    for hero_id in hero_ids:
        shutil.copyfile(GAME / source_folder / (hero_id + ".png"), ASSETS / target_folder / (hero_id + ".png"))
(ASSETS / "enemies").mkdir()
for enemy_id in ["infantry", "archer"]:
    shutil.copyfile(GAME / "Enemies" / (enemy_id + ".png"), ASSETS / "enemies" / (enemy_id + ".png"))
shutil.copyfile(GAME / "Fonts/Chinese.otf", ASSETS / "chinese.otf")
shutil.copyfile(GAME / "Fonts/OFL.txt", ASSETS / "OFL.txt")
data = {"level": {"id": level["Id"], "name": level["Name"], "slots": level["Slots"],
                  "routes": level["Routes"], "waveCount": len(level["Waves"])},
        "heroes": [{key: hero[key] for key in ["Id", "Name", "Title", "Role", "Description", "Cost", "Accent"]} for hero in heroes],
        "boundary": "浏览器设计演示；现有正式素材；未接入Unity；不模拟真实战斗或写存档。"}
(ASSETS / "data.json").write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
(ASSETS / "data.js").write_text("window.PROPOSAL_DATA = " + json.dumps(data, ensure_ascii=False) + ";\n", encoding="utf-8")
after = {path.relative_to(ROOT).as_posix(): digest(path) for path in sources}
if before != after:
    raise RuntimeError("输入资源发生变化。")
report = {"SourcesUnchanged": True, "SourceFiles": before, "MapCrop": [38, 144, 1882, 842],
          "RoutePointCount": sum(len(route["Points"]) for route in level["Routes"]), "SlotCount": len(level["Slots"]),
          "Assets": [{"Path": path.relative_to(HERE).as_posix(), "Bytes": path.stat().st_size, "Sha256": digest(path)}
                     for path in sorted(ASSETS.rglob("*")) if path.is_file()]}
(HERE / "asset-manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"SourcesUnchanged": True, "AssetCount": len(report["Assets"]), "SlotCount": report["SlotCount"],
                  "RoutePointCount": report["RoutePointCount"]}, ensure_ascii=False))
