"""只为现有数据库增添25个Faction字段；不生成美术或其他配置。"""
import json
import re
import hashlib
from pathlib import Path
from build_game_content import HERO_FACTIONS, MANIFEST


def update(path):
    text = path.read_bytes().decode("utf-8")
    before = json.loads(text)
    assert len(before["Heroes"]) == 25
    assert {h["Id"] for h in before["Heroes"]} == set(HERO_FACTIONS)
    changed = text
    for hero in before["Heroes"]:
        if "Faction" in hero:
            assert hero["Faction"] == HERO_FACTIONS[hero["Id"]]
            continue
        pattern = r'("Id": "' + re.escape(hero["Id"]) + r'",)(\r?\n)([ ]*)'
        changed, count = re.subn(pattern, lambda m: m[1] + m[2] + m[3] + '"Faction": "' + HERO_FACTIONS[hero["Id"]] + '",' + m[2] + m[3], changed, count=1)
        assert count == 1
    clean = json.loads(changed)
    for hero in clean["Heroes"]:
        hero.pop("Faction", None)
    expected = json.loads(json.dumps(before))
    for hero in expected["Heroes"]:
        hero.pop("Faction", None)
    assert clean == expected, "禁止改变其他字段"
    manifest_text = MANIFEST.read_bytes().decode("utf-8")
    manifest = json.loads(manifest_text)
    without_factions = re.sub(r'^[ ]*"Faction": "(?:wei|shu|wu|qun)",\r?\n', '', text, flags=re.M)
    candidates = (text, changed, without_factions, without_factions.replace('\r\n', '\n'), without_factions.replace('\r\n', '\n').replace('\n', '\r\n'))
    assert manifest["DatabaseSha256"] in {hashlib.sha256(candidate.encode("utf-8")).hexdigest() for candidate in candidates}, "清单指纹存在无关变更，拒绝覆盖"
    digest = hashlib.sha256(changed.encode("utf-8")).hexdigest()
    manifest_changed, count = re.subn(r'("DatabaseSha256": ")[a-f0-9]{64}(")', lambda m: m[1] + digest + m[2], manifest_text, count=1)
    assert count == 1
    checked_manifest = json.loads(manifest_changed)
    checked_manifest["DatabaseSha256"] = manifest["DatabaseSha256"]
    assert checked_manifest == manifest, "禁止改变其他清单字段"
    path.write_bytes(changed.encode("utf-8"))
    MANIFEST.write_bytes(manifest_changed.encode("utf-8"))
    print("已验证：只添加25个Faction字段并同步清单指纹，其余配置和素材记录逐字段一致。")


if __name__ == "__main__":
    update(Path(__file__).resolve().parents[1] / "UnityGame/Assets/Resources/Game/database.json")
