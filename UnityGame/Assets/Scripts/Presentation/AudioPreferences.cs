using System;
using UnityEngine;

namespace YanyiSandpan.Presentation
{
    // 音量以千分比保存，方便使用内存字典验证迁移而不修改玩家偏好。
    public sealed class AudioPreferences
    {
        private const string Prefix = "yanyi.audio.";
        private readonly Action<string, int> write;
        private readonly Action save;
        private bool musicEnabled, soundEnabled;
        private float musicVolume, soundVolume;
        public event Action Changed;

        public AudioPreferences(Func<string, int, int> read, Action<string, int> write, Action save)
        {
            if (read == null || write == null || save == null) throw new ArgumentNullException("音频存储委托不能为空");
            this.write = write; this.save = save;
            if (read(Prefix + "migration", 0) < 1)
            {
                int enabled = read(Prefix + "enabled", 1) == 0 ? 0 : 1;
                Migrate(read, "music.enabled", enabled); Migrate(read, "sound.enabled", enabled);
                Migrate(read, "music.volume", 500); Migrate(read, "sound.volume", 800);
                write(Prefix + "migration", 1); save();
            }
            musicEnabled = read(Prefix + "music.enabled", 1) != 0;
            soundEnabled = read(Prefix + "sound.enabled", 1) != 0;
            musicVolume = Mathf.Clamp01(read(Prefix + "music.volume", 500) / 1000f);
            soundVolume = Mathf.Clamp01(read(Prefix + "sound.volume", 800) / 1000f);
        }

        public bool MusicEnabled { get { return musicEnabled; } set { if (musicEnabled == value) return; musicEnabled = value; Store("music.enabled", value ? 1 : 0); } }
        public bool SoundEnabled { get { return soundEnabled; } set { if (soundEnabled == value) return; soundEnabled = value; Store("sound.enabled", value ? 1 : 0); } }
        public float MusicVolume { get { return musicVolume; } set { float valid = Valid(value); if (musicVolume == valid) return; musicVolume = valid; Store("music.volume", Mathf.RoundToInt(valid * 1000)); } }
        public float SoundVolume { get { return soundVolume; } set { float valid = Valid(value); if (soundVolume == valid) return; soundVolume = valid; Store("sound.volume", Mathf.RoundToInt(valid * 1000)); } }

        private static float Valid(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value); }
        private void Migrate(Func<string, int, int> read, string key, int fallback)
        { if (read(Prefix + key, int.MinValue) == int.MinValue) write(Prefix + key, fallback); }
        private void Store(string key, int value) { write(Prefix + key, value); save(); if (Changed != null) Changed(); }
    }
}
