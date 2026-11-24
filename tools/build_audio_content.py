#!/usr/bin/env python3
"""基于 CC0 真实乐器采样制作原创游戏音频；--verify-only 完全只读。"""
from __future__ import annotations

import argparse
import base64
from concurrent.futures import ThreadPoolExecutor
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
import math
from pathlib import Path
import sys
import urllib.parse
import urllib.request
import wave
import warnings

import numpy as np
from scipy import signal
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs/audio"
SOURCE = DOCS / "source"
AUDIO = ROOT / "UnityGame/Assets/Resources/Audio"
RATE = 48000
COMMIT = "440300901dfe9275fd84e0b7763af1f8443ae62e"
REPO = "https://api.github.com/repos/sgossner/VSCO-2-CE"
ATTRIBUTION = "VSCO 2 Community Edition — Versilian Studios / Sam Gossner；Ivy Audio / Simon Dalzell；采样剪辑 Elan Hickler / Soundemote。"

# 音名采用 MIDI 标准编号；只选择本次作品实际使用的原始采样。
SAMPLES = {
    "harp_c3": ("Strings/Harp/KSHarp_C3_mf.wav", "harp", 48),
    "harp_e3": ("Strings/Harp/KSHarp_E3_mf.wav", "harp", 52),
    "harp_g3": ("Strings/Harp/KSHarp_G3_mf.wav", "harp", 55),
    "harp_b3": ("Strings/Harp/KSHarp_B3_mf.wav", "harp", 59),
    "harp_d4": ("Strings/Harp/KSHarp_D4_mf.wav", "harp", 62),
    "harp_f4": ("Strings/Harp/KSHarp_F4_mf.wav", "harp", 65),
    "harp_a4": ("Strings/Harp/KSHarp_A4_mf.wav", "harp", 69),
    "harp_c5": ("Strings/Harp/KSHarp_C5_mf.wav", "harp", 72),
    "harp_e5": ("Strings/Harp/KSHarp_E5_mf.wav", "harp", 76),
    # 该组文件名采用不同八度编号；根音由采样频谱核对，不能直接照抄文件名。
    "flute_c4": ("Woodwinds/Flute/susNV/LDFlute_susNV_C4_v1_1.wav", "flute", 72),
    "flute_e4": ("Woodwinds/Flute/susNV/LDFlute_susNV_E4_v1_1.wav", "flute", 76),
    "flute_a4": ("Woodwinds/Flute/susNV/LDFlute_susNV_A4_v1_1.wav", "flute", 81),
    "flute_c5": ("Woodwinds/Flute/susNV/LDFlute_susNV_C5_v1_1.wav", "flute", 84),
    "cello_c1": ("Strings/Cello Section/pizzT/pizzT_C1_v1_RR1.wav", "cello", 36),
    "cello_c3": ("Strings/Cello Section/pizzT/pizzT_C3_v1_RR1.wav", "cello", 60),
    "cello_g3": ("Strings/Cello Section/pizzT/pizzT_G3_v1_RR1.wav", "cello", 67),
    "drum_soft": ("Percussion/BDrumNewhit_v2_rr1_Sum.wav", "drum", None),
    "drum_strong": ("Percussion/BDrumNewhit_v4_rr1_Sum.wav", "drum", None),
    "wood": ("Percussion/LogDrumLo_MedM_v2_rr1_Sum.wav", "wood", None),
    "clave": ("Percussion/Claves1_Hit_v2_rr1_Sum.wav", "clave", None),
    "conga": ("Percussion/Conga-HitN_v2_rr1_Sum.wav", "conga", None),
    "gong": ("Percussion/gongHit_p.wav", "gong", None),
    "metal": ("Percussion/Anvil_Hit1_v1_Sum.wav", "metal", None),
    "cymbal": ("Percussion/cymbal-crash1_pp_rr1.wav", "cymbal", None),
    "bell": ("Percussion/BellTree_Stroke1_v1_Sum.wav", "bell", None),
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_text(value) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2) + "\n"


def save_json(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = json_text(value)
    if not path.exists() or path.read_text(encoding="utf-8") != payload:
        path.write_text(payload, encoding="utf-8", newline="\n")


def fetch_json(url: str):
    request = urllib.request.Request(url, headers={"User-Agent": "YanyiSandbox-AudioBuilder/1", "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(request, timeout=45) as response:
        return json.load(response)


def download_sources():
    """官方 API 获取同一固定版本；不禁用 TLS，不获取可执行文件。"""
    SOURCE.mkdir(parents=True, exist_ok=True)
    manifest_path = SOURCE / "source_manifest.json"
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest["Commit"] != COMMIT:
            raise ValueError("本地采样版本与脚本固定版本不符")
        for item in manifest["Samples"]:
            if sha256((ROOT / item["LocalPath"]).read_bytes()) != item["Sha256"]:
                raise ValueError("原始采样校验失败：" + item["Id"])
        if {x["Id"] for x in manifest["Samples"]} == set(SAMPLES) and all(x["MidiRoot"] == SAMPLES[x["Id"]][2] for x in manifest["Samples"]):
            print("原始采样缓存校验通过", flush=True)
            return
    tree_path = SOURCE / "VSCO2CE-tree.json"
    if tree_path.exists():
        tree = json.loads(tree_path.read_text(encoding="utf-8"))
    else:
        tree = fetch_json(REPO + "/git/trees/" + COMMIT + "?recursive=1")
        save_json(tree_path, tree)
    nodes = {x["path"]: x for x in tree["tree"]}

    def get_blob(path):
        node = nodes[path]
        blob = fetch_json(REPO + "/git/blobs/" + node["sha"])
        payload = base64.b64decode(blob["content"])
        expected = hashlib.sha1(b"blob " + str(len(payload)).encode() + b"\0" + payload).hexdigest()
        if expected != node["sha"]:
            raise ValueError("Git blob 完整性校验失败：" + path)
        return payload, node

    documents = []
    for remote, local in [("LICENSE", "VSCO2CE-LICENSE.txt"), ("README.md", "VSCO2CE-README.md"), ("Readme.txt", "VSCO2CE-Readme.txt")]:
        target = SOURCE / local
        if target.exists():
            payload = target.read_bytes()
        else:
            payload, _ = get_blob(remote)
            target.write_bytes(payload)
        if remote == "LICENSE" and b"CC0 1.0 Universal" not in payload:
            raise ValueError("来源未提供预期 CC0 许可")
        documents.append({"SourceUrl": "https://github.com/sgossner/VSCO-2-CE/blob/" + COMMIT + "/" + remote,
                          "LocalPath": target.relative_to(ROOT).as_posix(), "Sha256": sha256(payload)})

    def acquire(entry):
        key, (path, instrument, midi) = entry
        target = SOURCE / "samples" / (key + ".wav")
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            payload = target.read_bytes()
            node = nodes[path]
            expected = hashlib.sha1(b"blob " + str(len(payload)).encode() + b"\0" + payload).hexdigest()
            if expected != node["sha"]:
                raise ValueError("已有采样与源版本不符：" + key)
        else:
            payload, node = get_blob(path)
            target.write_bytes(payload)
        print("采样已核验 " + key, flush=True)
        return {"Id": key, "Instrument": instrument, "MidiRoot": midi,
                "SourcePath": path, "SourceUrl": "https://github.com/sgossner/VSCO-2-CE/blob/" + COMMIT + "/" + urllib.parse.quote(path),
                "ApiUrl": REPO + "/git/blobs/" + node["sha"], "LocalPath": target.relative_to(ROOT).as_posix(),
                "GitBlobSha1": node["sha"], "Sha256": sha256(payload), "Bytes": len(payload)}

    with ThreadPoolExecutor(max_workers=3) as pool:
        acquired = list(pool.map(acquire, SAMPLES.items()))
    save_json(manifest_path, {"Version": 1, "Repository": "https://github.com/sgossner/VSCO-2-CE", "Commit": COMMIT,
        "License": "CC0-1.0", "LicenseText": "docs/audio/source/VSCO2CE-LICENSE.txt", "Attribution": ATTRIBUTION,
        "RetrievedUtc": datetime.now(timezone.utc).isoformat(), "Documents": documents, "Samples": acquired})


def fade(audio: np.ndarray, attack=0.004, release=0.03):
    audio = audio.copy()
    a, r = min(len(audio), int(attack * RATE)), min(len(audio), int(release * RATE))
    if a:
        audio[:a] *= np.linspace(0, 1, a, dtype=np.float32)
    if r:
        audio[-r:] *= np.linspace(1, 0, r, dtype=np.float32)
    return audio


class SampleBank:
    def __init__(self):
        self.sources = {}
        self.cache = {}
        self.used = set()
        for key in SAMPLES:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", wavfile.WavFileWarning)
                rate, raw = wavfile.read(SOURCE / "samples" / (key + ".wav"))
            if raw.dtype.kind in "iu":
                raw = raw.astype(np.float32) / float(max(abs(np.iinfo(raw.dtype).min), np.iinfo(raw.dtype).max))
            else:
                raw = raw.astype(np.float32)
            if raw.ndim == 2:
                raw = raw.mean(axis=1)
            raw -= np.mean(raw)
            nonzero = np.flatnonzero(np.abs(raw) > np.max(np.abs(raw)) * 0.007)
            if not len(nonzero):
                raise ValueError("原始采样为空：" + key)
            raw = raw[max(0, nonzero[0] - int(rate * .004)):min(len(raw), nonzero[-1] + int(rate * .06))]
            if rate != RATE:
                divisor = math.gcd(rate, RATE)
                raw = signal.resample_poly(raw, RATE // divisor, rate // divisor).astype(np.float32)
            raw /= max(float(np.max(np.abs(raw))), 1e-8)
            self.sources[key] = fade(raw, .002, .025)

    def note(self, instrument, midi=None, duration=.5):
        options = [(key, info[2]) for key, info in SAMPLES.items() if info[1] == instrument]
        if midi is None:
            key, root = options[0]
        else:
            key, root = min(options, key=lambda pair: abs(pair[1] - midi))
        self.used.add(key)
        ratio = 1.0 if midi is None else 2 ** ((midi - root) / 12)
        cache_key = (key, midi, round(duration, 4))
        if cache_key not in self.cache:
            source = self.sources[key]
            positions = np.arange(min(int(duration * RATE), int(len(source) / ratio))) * ratio
            data = np.interp(positions, np.arange(len(source)), source).astype(np.float32)
            self.cache[cache_key] = fade(data, .012 if instrument == "flute" else .002, min(.06, duration * .18))
        return self.cache[cache_key]

    def one_shot(self, key, duration, pitch=1.0, reverse=False):
        self.used.add(key)
        source = self.sources[key]
        positions = np.arange(min(int(duration * RATE), int(len(source) / pitch))) * pitch
        data = np.interp(positions, np.arange(len(source)), source).astype(np.float32)
        if reverse:
            data = data[::-1].copy()
        return fade(data, .003, min(.04, duration * .15))


def place(buffer, mono, seconds, gain, pan=0.0, loop=False):
    start = int(round(seconds * RATE))
    if buffer.ndim == 2:
        gains = np.array([math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)], dtype=np.float32)
        data = mono[:, None] * gains[None, :] * gain
    else:
        data = mono * gain
    if loop:
        start %= len(buffer)
        first = min(len(data), len(buffer) - start)
        buffer[start:start + first] += data[:first]
        rest = min(len(data) - first, len(buffer))
        if rest:
            buffer[:rest] += data[first:first + rest]
    elif start < len(buffer):
        size = min(len(data), len(buffer) - start)
        buffer[start:start + size] += data[:size]


# 全新旋律动机；并非改写既有歌曲。音阶 C D E G A，句长 4 小节。
MELODIES = [
    [(0,72,1.5),(2,76,.75),(3,74,.8),(4,79,1.6),(6,76,.75),(7,74,.7),(8,72,1),(9.5,69,1),(11,67,.75),(12,69,1.3),(14,72,1.5)],
    [(0,67,1),(1.5,69,.7),(2.5,72,1.2),(4,74,1.4),(6,72,1.3),(8,76,1),(9.5,79,.7),(10.5,76,1),(12,74,1.6),(14,72,1.2)],
    [(0,72,.8),(1.5,74,.8),(3,76,.8),(4,79,1),(5.5,81,1),(7,79,.7),(8,76,1.5),(10,74,1),(12,69,1),(14,67,1.5)],
    [(0,76,1),(2,74,1),(4,72,1.8),(6.5,69,.7),(8,67,1.4),(10,69,1),(12,72,1.6),(14.5,74,.7)],
]


def music_track(bank, cue):
    menu = cue == "music.menu"
    boss = cue == "music.boss"
    bpm = 80 if menu else (112 if boss else 96)
    bars = 24 if menu or boss else 48
    beat = 60 / bpm
    duration = bars * 4 * beat
    output = np.zeros((round(duration * RATE), 2), dtype=np.float32)
    style = {"music.menu":0,"music.battle.huangjin":1,"music.battle.hulao":2,"music.battle.guandu":3,
             "music.battle.chibi":4,"music.battle.finale":5,"music.boss":6}[cue]
    roots = [48,45,43,45,48,43,45,43]
    for bar in range(bars):
        base = bar * 4
        root = roots[(bar // 2 + style) % len(roots)]
        section = bar // 16
        dynamic = .86 + .08 * (bar % 4) + .04 * section
        # 分关卡改变节奏、声部、音区和乐句，而非只改响度。
        arp = [0,7,12,7] if menu else ([0,7,12,14,7,12,19,12] if style in (4,5,6) else [0,12,7,14])
        for index, interval in enumerate(arp):
            timing = base + index * 4 / len(arp)
            if style == 3:
                timing += .18 if index % 2 else 0
            if not (style == 2 and bar % 2 == 1 and index > 1):
                place(output, bank.note("harp", root + interval + (12 if style == 4 else 0), 1.55 * beat), timing * beat,
                      (.11 if menu else .095) * dynamic, -.38, True)
        if not menu:
            for at in ([0,2.5] if style == 3 else [0,2]):
                bass_midi = root + (12 if style == 3 else 0)
                if style in (2,6):
                    bass_midi = max(36, root - 12)
                place(output, bank.note("cello", bass_midi, 1.1 * beat),
                      (base + at) * beat, .13 * dynamic, .12, True)
            if style == 5 and bar % 4 == 3:
                place(output, bank.note("cello", root + 24, .7 * beat), (base + 3) * beat, .055, .38, True)
        else:
            place(output, bank.note("harp", root - 12, 2.4 * beat), base * beat, .15, .2, True)
        if menu:
            if bar % 4 == 0:
                place(output, bank.one_shot("wood", .6, .8), base * beat, .021, .5, True)
        else:
            drum_key = "drum_strong" if style in (2,5,6) else "drum_soft"
            for at, gain in [(0,.16),(2,.105)]:
                place(output, bank.one_shot(drum_key, 1.3), (base + at) * beat, gain * dynamic, 0, True)
            if style in (5,6):
                for at in [1.5,3,3.5]:
                    place(output, bank.one_shot("conga", .42, 1.05), (base + at) * beat, .055, .28, True)
            pulse_key = "wood" if style in (1,4) else "clave"
            for at in ([1,3] if style != 3 else [.75,2.75,3.5]):
                place(output, bank.one_shot(pulse_key, .36), (base + at) * beat, .045 * dynamic, .45, True)
            if bar % 8 == 7:
                for at in [3,3.5,3.75]:
                    place(output, bank.one_shot("wood", .28, 1.2), (base + at) * beat, .04, .35, True)
            if bar % 16 == 0 and style >= 4:
                place(output, bank.one_shot("gong", 3.4, .82), base * beat, .045, -.15, True)
    # 每四小节一个主题句，休止保留战斗阅读空间。
    for phrase in range(bars // 4):
        pattern = MELODIES[(phrase + style) % len(MELODIES)]
        for index, (at, midi, length) in enumerate(pattern):
            if style == 2 and index % 2 == 1:
                continue
            offset = -7 if style in (2,6) else 0
            if menu and phrase % 3 == 2:
                instrument, gain = "harp", .14
            else:
                instrument, gain = "flute", .075 if menu else (.08 if style == 4 else .065)
            place(output, bank.note(instrument, midi + offset, length * beat),
                  (phrase * 16 + at) * beat, gain, .33, True)
    # 周期延迟混响将尾音自然回送至开头；不把尾音硬截成静音。
    dry = output.copy()
    for delay, gain in [(.079,.09),(.151,.075),(.263,.045)]:
        output += np.roll(dry[:, ::-1], round(delay * RATE), axis=0) * gain
    return master(output, loop=True), {"Bpm": bpm, "Bars": bars, "DurationSeconds": duration,
        "Arrangement": ["疏朗竖琴与长笛","木鼓与轻拨弦","低鼓与低音拨奏","切分木击与低音拨奏","高音竖琴与长笛长句","多层拨弦与密鼓","低音长笛与紧密低鼓"][style]}


def master(data, loop=False, sfx=False):
    data = data.astype(np.float32)
    data -= np.mean(data, axis=0)
    if loop:
        # 只修正极短的采样边界跳变；听感仍需人工循环试听。
        size = min(240, len(data))
        delta = data[0] - data[-1]
        data[-size:] += np.linspace(0, 1, size, dtype=np.float32)[:, None] * delta
    else:
        if data.ndim == 1:
            data = fade(data, .003, .025)
        else:
            data = np.stack([fade(data[:, index], .006, .14) for index in range(2)], axis=1)
    rms = float(np.sqrt(np.mean(data ** 2)))
    target = 10 ** ((-21 if not sfx else -17) / 20)
    factor = min(target / max(rms, 1e-8), 10 ** (-3.5 / 20) / max(float(np.max(np.abs(data))), 1e-8))
    return data * factor


def sfx_track(bank, cue, variant):
    durations = {"ui.click":.065,"ui.reject":.145,"action.recruit":.33,"action.upgrade":.52,
        "attack.melee":.18,"attack.bow":.145,"attack.fire":.34,"attack.control":.27,
        "attack.tactic":.23,"attack.support":.24,"combat.hit":.075,"enemy.defeat":.19,
        "castle.leak":.32,"wave.start":.58,"boss.warning":.82,"boss.phase":1.02,
        "identity.lv_bu":.68,"identity.zhang_fei":.58,"identity.zhuge_liang":.61}
    duration = durations[cue] * (1 + .035 * (variant - 1))
    output = np.zeros(round(duration * RATE), dtype=np.float32)
    pitch = 1 + .045 * (variant - 1)
    rng = np.random.default_rng(29100 + sum(ord(c) for c in cue) + variant * 37)

    def hit(key, at=0, gain=.5, length=None, reverse=False, tune=1.0):
        place(output, bank.one_shot(key, length or duration, pitch * tune, reverse), at, gain)

    def note(instrument, midi, at, gain, length):
        place(output, bank.note(instrument, midi + variant - 1, length), at, gain)

    def air(at, length, gain, rising=False):
        # 抽象气流/火焰属于原创噪声设计，不冒充实体乐器。
        count = max(16, round(length * RATE))
        noise = rng.standard_normal(count).astype(np.float32)
        noise = signal.sosfilt(signal.butter(2, [700,6500], fs=RATE, btype="bandpass", output="sos"), noise).astype(np.float32)
        envelope = np.linspace(.05, 1, count) if rising else np.exp(-np.linspace(0, 5, count))
        place(output, fade(noise * envelope, .006, .025), at, gain)

    if cue == "ui.click":
        hit("clave", gain=.6, tune=1.4)
    elif cue == "ui.reject":
        hit("wood", gain=.42, tune=.76)
        hit("wood", at=.065, gain=.34, tune=.60)
    elif cue in ("action.recruit", "action.upgrade"):
        notes = [60,64,67] if cue.endswith("recruit") else [60,64,67,72,76]
        for index, midi in enumerate(notes):
            note("harp", midi, .055 * index, .5 - index * .035, duration - .055 * index)
        if cue.endswith("upgrade"):
            hit("bell", at=.16, gain=.13, length=.36, tune=1.4)
    elif cue == "attack.melee":
        air(0,.12,.14)
        hit("metal", .035, .4, .14, tune=.85)
        hit("wood", .032,.25,.13)
    elif cue == "attack.bow":
        note("harp", 76, 0, .38, .12)
        air(.012,.13,.13)
    elif cue == "attack.fire":
        air(0,.34,.3)
        hit("cymbal", .015,.16,.3, tune=.74)
    elif cue == "attack.control":
        hit("bell", 0,.3,.24, True, 1.3)
        note("flute", 79, .08,.25,.16)
    elif cue == "attack.tactic":
        hit("wood", 0,.3,.15, tune=1.5)
        note("flute", 72, .03,.3,.17)
        note("harp", 79, .08,.2,.13)
    elif cue == "attack.support":
        note("harp", 64, 0,.5,.22)
        note("harp", 69,.045,.35,.18)
    elif cue == "combat.hit":
        hit("wood", 0,.45,.073, tune=1.6)
        hit("metal", 0,.12,.07, tune=1.4)
    elif cue == "enemy.defeat":
        hit("wood", 0,.3,.16, tune=.75)
        air(.025,.16,.14)
    elif cue == "castle.leak":
        hit("drum_strong", 0,.65,.31, tune=.78)
        hit("metal", .045,.24,.26, tune=.6)
    elif cue == "wave.start":
        for at,gain in [(0,.4),(.15,.45),(.31,.65)]:
            hit("drum_soft", at,gain,duration-at)
        hit("wood", .31,.2,.2)
    elif cue == "boss.warning":
        hit("gong", 0,.45,.8, tune=.85)
        hit("drum_strong", .08,.55,.7, tune=.8)
        hit("drum_strong", .35,.35,.4, tune=.7)
    elif cue == "boss.phase":
        hit("cymbal", 0,.4,.6, True, .8)
        hit("gong", .42,.7,.58, tune=.75)
        hit("drum_strong", .43,.55,.56)
    elif cue == "identity.lv_bu":
        for at in [0,.17,.34]:
            hit("metal", at,.26,duration-at,tune=.75)
            hit("drum_strong", at,.5,duration-at,tune=.83)
        note("harp", 55,.34,.35,.32)
    elif cue == "identity.zhang_fei":
        for at,gain in [(0,.45),(.12,.55),(.25,.8)]:
            hit("drum_strong", at,gain,duration-at,tune=.72)
        hit("wood", .25,.25,.3,tune=.8)
    elif cue == "identity.zhuge_liang":
        for index,midi in enumerate([67,72,76]):
            note("harp", midi, index * .09,.4,duration-index*.09)
        note("flute", 79,.23,.25,.35)
    return master(output, sfx=True), {"DurationSeconds": duration, "Variation": variant}


def result_track(bank, victory):
    duration = 4 if victory else 5
    output = np.zeros((duration * RATE, 2), dtype=np.float32)
    for index, midi in enumerate([60,64,67,72] if victory else [67,64,62,57]):
        at = index * (.32 if victory else .52)
        place(output, bank.note("harp", midi, duration - at), at, .3, -.3)
        if index in (0,3):
            place(output, bank.note("flute", midi + (12 if victory else 0), 1.2), at + .08, .12, .3)
    place(output, bank.one_shot("gong" if victory else "drum_soft", 2.2, .9), 1.0 if victory else 0, .08)
    dry = output.copy()
    for delay,gain in [(.13,.12),(.27,.07)]:
        shift = round(delay * RATE)
        output[shift:] += dry[:-shift, ::-1] * gain
    return master(output), {"DurationSeconds": duration, "Arrangement": "原创上行竖琴结句" if victory else "原创下行竖琴结句"}


def write_wav(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    wavfile.write(buffer, RATE, np.round(np.clip(data, -1, 1) * 32767).astype(np.int16))
    payload = buffer.getvalue()
    if not path.exists() or path.read_bytes() != payload:
        path.write_bytes(payload)


def write_pcm24_master(path, data):
    """直接量化原始浮点编曲；此函数不读取运行时16位音频。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    quantized = np.rint(np.clip(data, -1, 1).astype(np.float64) * 8388607).astype(np.int32).ravel()
    packed = np.empty((len(quantized), 3), dtype=np.uint8)
    for index in range(3):
        packed[:, index] = (quantized >> (8 * index)) & 255
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as stream:
        stream.setnchannels(1 if data.ndim == 1 else data.shape[1])
        stream.setsampwidth(3)
        stream.setframerate(RATE)
        stream.writeframes(packed.tobytes())
    payload = buffer.getvalue()
    if not path.exists() or path.read_bytes() != payload:
        path.write_bytes(payload)


def cue_priority(cue):
    if cue in ("music.victory", "music.defeat"):
        return 100
    if cue in ("boss.warning", "boss.phase"):
        return 90
    if cue == "castle.leak":
        return 75
    if cue.startswith(("action.", "identity.")) or cue == "wave.start":
        return 65
    if cue.startswith("attack.") or cue in ("combat.hit", "enemy.defeat"):
        return 30
    return 50


def export_tables(files):
    """便于表格软件复核的逐文件技术与授权台账。"""
    with (DOCS / "audio-ledger.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["CueId","名称","文件","采样率Hz","声道","位深","时长秒","峰值dBFS","均方根dBFS","循环采样跳变","SHA256","来源采样Id","采样许可","原创内容",
                         "TimeSignature","LoopStartFrame","LoopEndFrameExclusive","Frames","母版文件","母版SHA256"])
        for item in files:
            writer.writerow([item["CueId"],item["Name"],item["Path"],item["SampleRate"],item["Channels"],item["BitsPerSample"],
                item["DurationSeconds"],item["PeakDbfs"],item["RmsDbfs"],item["LoopSampleJump"],item["Sha256"],
                ";".join(item["SourceSampleIds"]),"CC0-1.0；原文见source/VSCO2CE-LICENSE.txt","本项目原创编曲/编辑；未套用成曲",
                item["TimeSignature"] or "null",item["LoopStartFrame"] if item["Loop"] else "null",
                item["LoopEndFrameExclusive"] if item["Loop"] else "null",item["Frames"],item["MasterPath"],item["MasterSha256"]])


def cue_rows():
    return list(csv.DictReader((ROOT / "docs/engineering/audio-cue-list.csv").read_text(encoding="utf-8-sig").splitlines()))


def metadata(path):
    rate, raw = wavfile.read(path)
    with wave.open(str(path)) as stream:
        bits = stream.getsampwidth() * 8
    container_bits = raw.dtype.itemsize * 8
    data = raw.astype(np.float32) / float(2 ** (container_bits - 1))
    peak = float(np.max(np.abs(data)))
    rms = float(np.sqrt(np.mean(data ** 2)))
    window = max(1, rate // 50)
    result = {"Path": path.relative_to(ROOT).as_posix(), "Sha256": sha256(path.read_bytes()), "Bytes": path.stat().st_size,
        "SampleRate": rate, "Channels": 1 if raw.ndim == 1 else raw.shape[1], "BitsPerSample": bits,
        "Frames": len(raw), "DurationSeconds": len(raw) / rate, "PeakDbfs": round(20 * math.log10(max(peak, 1e-10)), 4),
        "RmsDbfs": round(20 * math.log10(max(rms, 1e-10)), 4), "DcOffset": np.mean(data, axis=0).tolist(),
        "LoopSampleJump": float(np.max(np.abs(data[0] - data[-1]))),
        "Head20msRms": float(np.sqrt(np.mean(data[:window] ** 2))), "Tail20msRms": float(np.sqrt(np.mean(data[-window:] ** 2))),
        "ClippedSamples": int(np.count_nonzero(np.abs(raw.astype(np.int64)) >= ((2 ** (bits - 1) - 1) << (container_bits - bits))))}
    if bits == 24:
        result["NonzeroLowByteSamples"] = int(np.count_nonzero((raw.astype(np.int64) >> 8) & 255))
        result["LowByteNonzeroFraction"] = result["NonzeroLowByteSamples"] / raw.size
    return result


def preview(cues):
    output = np.zeros((60 * RATE, 2), dtype=np.float32)
    timeline = []
    # 九段音乐各四秒；其余24秒依次展示34个短音。
    music = [c for c in cues if c["Id"].startswith("music.")]
    for index, cue in enumerate(music):
        path = ROOT / "UnityGame/Assets/Resources" / (cue["Paths"][0] + ".wav")
        _, pcm = wavfile.read(path)
        data = pcm[:4 * RATE].astype(np.float32) / 32768
        data = np.stack([fade(data[:, c], .025, .08) for c in range(2)], axis=1)
        at = index * 4
        output[at * RATE:at * RATE + len(data)] += data
        timeline.append({"AtSeconds": at, "CueId": cue["Id"], "ExcerptSeconds": len(data) / RATE})
    paths = [(c["Id"], p) for c in cues if not c["Id"].startswith("music.") for p in c["Paths"]]
    total = sum(metadata(ROOT / "UnityGame/Assets/Resources" / (p + ".wav"))["DurationSeconds"] for _,p in paths)
    gap = (24 - total) / len(paths)
    if gap < 0:
        raise ValueError("短音总长超过试听板预留24秒")
    at = 36.0
    for cue, path in paths:
        _, pcm = wavfile.read(ROOT / "UnityGame/Assets/Resources" / (path + ".wav"))
        mono = pcm.astype(np.float32) / 32768 * .68
        available = max(0, len(output) - round(at * RATE))
        place(output, mono[:available], at, 1.0)
        timeline.append({"AtSeconds": round(at, 5), "CueId": cue, "ResourcePath": path, "ExcerptSeconds": len(mono) / RATE})
        at += len(mono) / RATE + gap
    write_wav(DOCS / "audio-preview-60s.wav", output)
    save_json(DOCS / "preview-timeline.json", timeline)


def build():
    download_sources()
    bank = SampleBank()
    cues, files, masters = [], [], []
    for row in cue_rows():
        cue = row["CueId"]
        is_music = cue.startswith("music.")
        loop = row["类型"] == "音乐循环"
        paths = []
        for index in range(1, int(row["文件数"]) + 1):
            bank.used.clear()
            if loop:
                data, info = music_track(bank, cue)
            elif is_music:
                data, info = result_track(bank, cue == "music.victory")
            else:
                data, info = sfx_track(bank, cue, index)
            resource = "Audio/" + ("Music/" if is_music else "Sfx/") + row["文件名前缀"] + f"_v{index:02d}"
            path = ROOT / "UnityGame/Assets/Resources" / (resource + ".wav")
            master_path = DOCS / "masters" / (resource.removeprefix("Audio/") + ".wav")
            write_pcm24_master(master_path, data)
            master_item = metadata(master_path)
            loop_fields = {"TimeSignature": "4/4" if loop else None, "LoopStartFrame": 0 if loop else None,
                           "LoopEndFrameExclusive": len(data) if loop else None}
            master_item.update({"CueId":cue,"Loop":loop,"RuntimePath":path.relative_to(ROOT).as_posix(),
                "FloatingPointSha256":sha256(data.astype("<f4").tobytes()),"DerivedFromRuntimePcm":False,
                "ProductionSource":"pre-quantization-float32-arrangement","SourceSampleIds":sorted(bank.used), **loop_fields})
            masters.append(master_item)
            write_wav(path, data)
            item = metadata(path)
            item.update({"CueId": cue, "Name": row["名称"], "Loop": loop, "License": "CC0-1.0 samples + original arrangement",
                "SourceSampleIds": sorted(bank.used), "Production": info, "MasterPath":master_item["Path"],
                "MasterSha256":master_item["Sha256"], **loop_fields})
            files.append(item)
            paths.append(resource)
            print("已导出 " + resource, flush=True)
        priority = cue_priority(cue)
        cooldown = .07 if cue.startswith("attack.") or cue == "combat.hit" else (.035 if cue == "ui.click" else .12)
        cues.append({"Id": cue, "Paths": paths, "Gain": 1.0, "Cooldown": 0.0 if is_music else cooldown,
                     "Priority": priority, "Loop": loop})
    save_json(AUDIO / "catalog.json", {"Version": 1, "Cues": cues})
    save_json(DOCS / "audio_manifest.json", {"Version":1,"SampleRate":RATE,"FrameUnit":"48000 Hz integer PCM frame; end is exclusive",
        "Files":files,"Masters":masters,
        "Attribution":ATTRIBUTION,"SourceManifest":"docs/audio/source/source_manifest.json",
        "HumanListening":"未进行人工听测；技术检查不代表审美或手机外放验收。"})
    export_tables(files)
    preview(cues)
    failures, report = verify()
    save_json(DOCS / "technical-report.json", report)
    if failures:
        raise ValueError("音频检查失败：\n" + "\n".join(failures))


def verify():
    """只读取工程；不下载、不重写 WAV、不刷新报告。"""
    failures = []
    checks = []
    try:
        manifest = json.loads((DOCS / "audio_manifest.json").read_text(encoding="utf-8"))
        sources = json.loads((SOURCE / "source_manifest.json").read_text(encoding="utf-8"))
        catalog = json.loads((AUDIO / "catalog.json").read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        return [str(error)], {"Passed":False,"Failures":[str(error)]}
    rows = cue_rows()
    if len(catalog.get("Cues", [])) != 28 or catalog.get("Version") != 1:
        failures.append("目录必须包含 Version=1 与 28 个 Cue")
    if len(manifest["Files"]) != 43:
        failures.append("最终清单必须为43文件")
    if len(manifest.get("Masters",[])) != 43 or len(list((DOCS / "masters").rglob("*.wav"))) != 43:
        failures.append("制作母版必须为43个真正PCM24文件")
    actual_files = list(AUDIO.glob("Music/*.wav")) + list(AUDIO.glob("Sfx/*.wav"))
    if len(actual_files) != 43 or len(list(AUDIO.glob("Music/*.wav"))) != 9:
        failures.append("磁盘最终文件数必须为9音乐＋34音效")
    if sources["License"] != "CC0-1.0" or sources["Commit"] != COMMIT:
        failures.append("采样来源或许可不符")
    for item in sources["Samples"] + sources["Documents"]:
        try:
            if sha256((ROOT / item["LocalPath"]).read_bytes()) != item["Sha256"]:
                failures.append("来源hash不符：" + item["LocalPath"])
        except OSError:
            failures.append("来源文件缺失：" + item["LocalPath"])
    hashes = set()
    by_cue = {c["Id"]:c for c in catalog["Cues"]}
    if set(by_cue) != {row["CueId"] for row in rows}:
        failures.append("Cue ID 集合不匹配")
    for row in rows:
        cue = row["CueId"]
        entry = by_cue.get(cue, {})
        if entry.get("Priority") != cue_priority(cue):
            failures.append("Cue 优先级不符：" + cue)
        if len(entry.get("Paths", [])) != int(row["文件数"]):
            failures.append("Cue 变体数不符：" + cue)
        for index, resource in enumerate(entry.get("Paths", []),1):
            expected = "Audio/" + ("Music/" if cue.startswith("music.") else "Sfx/") + row["文件名前缀"] + f"_v{index:02d}"
            if resource != expected:
                failures.append("资源路径不符：" + resource)
    for expected in manifest["Files"]:
        path = ROOT / expected["Path"]
        try:
            actual = metadata(path)
        except (OSError, ValueError) as error:
            failures.append(str(error))
            continue
        if actual["Sha256"] != expected["Sha256"]:
            failures.append("最终音频hash不符：" + expected["Path"])
        if actual["Sha256"] in hashes:
            failures.append("音频文件重复：" + expected["Path"])
        hashes.add(actual["Sha256"])
        is_music = expected["CueId"].startswith("music.")
        if actual["SampleRate"] != RATE or actual["Channels"] != (2 if is_music else 1) or actual["BitsPerSample"] != 16:
            failures.append("格式不符：" + expected["Path"])
        if actual["Frames"] < 100 or actual["RmsDbfs"] < -55 or actual["PeakDbfs"] > -1 or actual["ClippedSamples"]:
            failures.append("空音频或电平不符：" + expected["Path"])
        if abs(actual["DurationSeconds"] - expected["Production"]["DurationSeconds"]) > .001:
            failures.append("时长不符：" + expected["Path"])
        if expected["Loop"] and actual["LoopSampleJump"] > .005:
            failures.append("循环采样接缝跳变过大：" + expected["Path"])
        wanted_fields = {"TimeSignature":"4/4" if expected["Loop"] else None,
            "LoopStartFrame":0 if expected["Loop"] else None,
            "LoopEndFrameExclusive":actual["Frames"] if expected["Loop"] else None}
        if any(field not in expected or expected[field] != value for field,value in wanted_fields.items()):
            failures.append("拍号或48k整数循环区间不符：" + expected["Path"])
        if not expected["SourceSampleIds"] or not all(x in SAMPLES for x in expected["SourceSampleIds"]):
            failures.append("音频未登记授权采样：" + expected["Path"])
        checks.append(actual)
    master_checks = []
    for expected in manifest.get("Masters",[]):
        try:
            actual = metadata(ROOT / expected["Path"])
        except (OSError, ValueError, wave.Error) as error:
            failures.append(str(error))
            continue
        runtime = next((x for x in manifest["Files"] if x["Path"] == expected["RuntimePath"]), None)
        if (actual["BitsPerSample"] != 24 or actual["SampleRate"] != RATE or not runtime
                or actual["Frames"] != runtime["Frames"] or actual["Channels"] != runtime["Channels"]):
            failures.append("母版格式或时长不符：" + expected["Path"])
        if actual["Sha256"] != expected["Sha256"] or runtime and runtime["MasterSha256"] != actual["Sha256"]:
            failures.append("母版hash不符：" + expected["Path"])
        if expected.get("DerivedFromRuntimePcm") is not False or actual.get("LowByteNonzeroFraction",0) <= .5:
            failures.append("母版缺少真实24位精度证据：" + expected["Path"])
        if actual["PeakDbfs"] > -1 or actual["ClippedSamples"] or actual["RmsDbfs"] < -55:
            failures.append("母版电平不符：" + expected["Path"])
        wanted_fields = {"TimeSignature":"4/4" if expected["Loop"] else None,
            "LoopStartFrame":0 if expected["Loop"] else None,
            "LoopEndFrameExclusive":actual["Frames"] if expected["Loop"] else None}
        if any(field not in expected or expected[field] != value for field,value in wanted_fields.items()):
            failures.append("母版拍号或循环区间不符：" + expected["Path"])
        master_checks.append(actual)
    try:
        with wave.open(str(DOCS / "audio-preview-60s.wav")) as stream:
            if stream.getframerate() != RATE or stream.getnchannels() != 2 or stream.getnframes() != 60 * RATE:
                failures.append("试听板必须是60秒48k立体声")
    except (OSError, wave.Error) as error:
        failures.append(str(error))
    return failures, {"Version":1,"Passed":not failures,"CueCount":len(by_cue),"MusicCount":9,"SfxCount":34,
        "FileCount":len(checks),"UniqueSha256Count":len(hashes),"SourceSampleCount":len(sources["Samples"]),
        "MasterCount":len(master_checks),"MasterBitsPerSample":24,
        "Failures":failures,"Files":checks,"Masters":master_checks,
        "HumanListening":"未进行人工听测；循环只验证采样边界，不代表主观听感通过。"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-only", action="store_true", help="只读校验，离线运行，不修改报告")
    parser.add_argument("--download-only", action="store_true", help="仅下载并校验已锁定的 CC0 采样")
    args = parser.parse_args()
    if args.verify_only:
        failures, report = verify()
        print(json_text({k:v for k,v in report.items() if k not in ("Files", "Masters")}))
        return 1 if failures else 0
    if args.download_only:
        download_sources()
    else:
        build()
        print("技术核验通过：28 Cue，9 音乐＋34 音效；人工听测尚未进行。")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print("音频构建失败：" + str(error), file=sys.stderr)
        sys.exit(1)
