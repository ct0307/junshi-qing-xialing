"""独立交付验收：按实际需求核对运行时音频，而非复刻生成算法。"""
import csv
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import unittest
import wave

import numpy as np
from scipy.signal import periodogram

ROOT = Path(__file__).resolve().parents[2]
AUDIO = ROOT / "UnityGame/Assets/Resources/Audio"
sys.dont_write_bytecode = True


class AudioDeliveryTests(unittest.TestCase):
    def test_pcm24_masters_have_real_low_bits_and_float_source(self):
        masters = list((ROOT / "docs/audio/masters").rglob("*.wav"))
        self.assertEqual(43, len(masters), "需要全部43个量化前浮点信号制作的24位母版")
        for path in masters:
            with wave.open(str(path)) as stream:
                self.assertEqual(48000, stream.getframerate())
                self.assertEqual(3, stream.getsampwidth())
                packed = np.frombuffer(stream.readframes(stream.getnframes()), dtype=np.uint8).reshape(-1, 3)
                self.assertGreater(np.count_nonzero(packed[:, 0]), len(packed) * .5, "低8位不能只是16位音频补零升位")
        spec = importlib.util.spec_from_file_location("audio_builder_float", ROOT / "tools/build_audio_content.py")
        builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(builder)
        floating, _ = builder.music_track(builder.SampleBank(), "music.menu")
        path = ROOT / "docs/audio/masters/Music/mus_menu_an_shang_shan_he_v01.wav"
        with wave.open(str(path)) as stream:
            packed = np.frombuffer(stream.readframes(stream.getnframes()), dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        actual = packed[:, 0] | (packed[:, 1] << 8) | (packed[:, 2] << 16)
        actual = np.where(actual & 0x800000, actual - 0x1000000, actual)
        expected = np.rint(floating.astype(np.float64).ravel() * 8388607).astype(np.int32)
        np.testing.assert_array_equal(expected, actual, "母版必须直接由量化前编曲输出")

    def test_loop_frames_meter_and_runtime_hash_stay_explicit(self):
        manifest = json.loads((ROOT / "docs/audio/audio_manifest.json").read_text(encoding="utf-8"))
        prior = json.loads((ROOT / "docs/audio/reproducibility-check.json").read_text(encoding="utf-8"))["WavSha256"]
        for item in manifest["Files"]:
            self.assertIn("TimeSignature", item)
            self.assertEqual("4/4" if item["Loop"] else None, item["TimeSignature"])
            self.assertEqual(0 if item["Loop"] else None, item["LoopStartFrame"])
            self.assertEqual(item["Frames"] if item["Loop"] else None, item["LoopEndFrameExclusive"])
            self.assertEqual(prior[item["Path"]], hashlib.sha256((ROOT / item["Path"]).read_bytes()).hexdigest())
        ledger = list(csv.DictReader((ROOT / "docs/audio/audio-ledger.csv").read_text(encoding="utf-8-sig").splitlines()))
        self.assertEqual(43, len(ledger))
        for row in ledger:
            self.assertIn("TimeSignature", row)
            self.assertIn("LoopStartFrame", row)
            self.assertIn("LoopEndFrameExclusive", row)

    def test_catalog_priorities_match_approved_groups(self):
        catalog = json.loads((AUDIO / "catalog.json").read_text(encoding="utf-8"))
        for entry in catalog["Cues"]:
            cue = entry["Id"]
            expected = 50
            if cue in ("music.victory", "music.defeat"):
                expected = 100
            elif cue in ("boss.warning", "boss.phase"):
                expected = 90
            elif cue == "castle.leak":
                expected = 75
            elif cue.startswith(("action.", "identity.")) or cue == "wave.start":
                expected = 65
            elif cue.startswith("attack.") or cue in ("combat.hit", "enemy.defeat"):
                expected = 30
            self.assertEqual(expected, entry["Priority"], cue)

    def test_verify_only_does_not_modify_inputs_or_reports(self):
        paths = [p for folder in [AUDIO, ROOT / "docs/audio"] for p in folder.rglob("*") if p.is_file()]
        before = {str(p): (p.stat().st_size, p.stat().st_mtime_ns) for p in paths}
        result = subprocess.run([sys.executable, "-X", "utf8", str(ROOT / "tools/build_audio_content.py"), "--verify-only"],
                                cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(before, {str(p): (p.stat().st_size, p.stat().st_mtime_ns) for p in paths})

    def test_flute_pitch_matches_concert_reference(self):
        spec = importlib.util.spec_from_file_location("audio_builder", ROOT / "tools/build_audio_content.py")
        builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(builder)
        bank = builder.SampleBank()
        note = bank.note("flute", 72, 1.0)
        frequencies, power = periodogram(note[4800:28800], 48000)
        candidates = (frequencies > 300) & (frequencies < 1500)
        frequency = frequencies[candidates][np.argmax(power[candidates])]
        self.assertAlmostEqual(523.25, frequency, delta=4, msg="MIDI72 应为 C5；不能按有偏差的文件名误高八度")

    def test_all_cues_have_distinct_final_files(self):
        rows = list(csv.DictReader((ROOT / "docs/engineering/audio-cue-list.csv").read_text(encoding="utf-8-sig").splitlines()))
        self.assertEqual(28, len(rows))
        files = list(AUDIO.glob("Music/*.wav")) + list(AUDIO.glob("Sfx/*.wav"))
        self.assertEqual(43, len(files), "需要 9 音乐和 34 音效，不能以资源缺失静默通过")
        catalog = json.loads((AUDIO / "catalog.json").read_text(encoding="utf-8"))
        self.assertEqual(1, catalog["Version"])
        self.assertEqual({r["CueId"] for r in rows}, {c["Id"] for c in catalog["Cues"]})
        mapped = [p for c in catalog["Cues"] for p in c["Paths"]]
        self.assertEqual(43, len(set(mapped)))
        for row in rows:
            entry = next(c for c in catalog["Cues"] if c["Id"] == row["CueId"])
            self.assertEqual(int(row["文件数"]), len(entry["Paths"]))
            for resource in entry["Paths"]:
                path = ROOT / "UnityGame/Assets/Resources" / (resource + ".wav")
                self.assertTrue(path.is_file(), str(path))
                with wave.open(str(path)) as stream:
                    self.assertEqual(48000, stream.getframerate())
                    self.assertEqual(2 if row["CueId"].startswith("music.") else 1, stream.getnchannels())
                    self.assertEqual(2, stream.getsampwidth())
                    self.assertGreater(stream.getnframes(), 0)
        digests = [hashlib.sha256(p.read_bytes()).hexdigest() for p in files]
        self.assertEqual(43, len(set(digests)), "每个变体和关卡必须有独立音频")

    def test_sources_and_preview_are_reproducible(self):
        self.assertTrue((ROOT / "docs/audio/source/source_manifest.json").is_file(), "需要下载与授权台账")
        manifest = json.loads((ROOT / "docs/audio/source/source_manifest.json").read_text(encoding="utf-8"))
        self.assertEqual("CC0-1.0", manifest["License"])
        self.assertEqual("440300901dfe9275fd84e0b7763af1f8443ae62e", manifest["Commit"])
        self.assertGreaterEqual(len(manifest["Samples"]), 12)
        for item in manifest["Samples"]:
            self.assertEqual(item["Sha256"], hashlib.sha256((ROOT / item["LocalPath"]).read_bytes()).hexdigest())
        self.assertIn("CC0 1.0 Universal", (ROOT / "docs/audio/source/VSCO2CE-LICENSE.txt").read_text(encoding="utf-8"))
        with wave.open(str(ROOT / "docs/audio/audio-preview-60s.wav")) as stream:
            self.assertEqual(60 * 48000, stream.getnframes())
            self.assertEqual(2, stream.getnchannels())


if __name__ == "__main__":
    unittest.main()
