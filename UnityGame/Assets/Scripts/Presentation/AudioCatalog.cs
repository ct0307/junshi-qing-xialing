using System;
using System.Collections.Generic;
using UnityEngine;

namespace YanyiSandpan.Presentation
{
    [Serializable]
    public sealed class AudioCue
    {
        public string Id;
        public string[] Paths;
        public float Gain = 1;
        public float Cooldown;
        public int Priority = 30;
        public bool Loop;
    }

    // 短音在交互前预载；长音乐只保存路径，交给双源播放器按需加载。
    internal sealed class AudioCatalog : IDisposable
    {
        [Serializable] private sealed class Document { public int Version; public AudioCue[] Cues; }
        private readonly Dictionary<string, AudioCue> cues = new Dictionary<string, AudioCue>();
        private readonly Dictionary<string, AudioClip[]> sounds = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, int> lastVariants = new Dictionary<string, int>();
        private readonly HashSet<string> warned = new HashSet<string>();
        private readonly Dictionary<string, AudioClip> fallback = AudioFallbackClips.Create();

        public AudioCatalog()
        {
            TextAsset json = Resources.Load<TextAsset>("Audio/catalog");
            if (json != null)
            {
                try
                {
                    Document document = JsonUtility.FromJson<Document>(json.text);
                    if (document != null && document.Version == 1 && document.Cues != null)
                        foreach (AudioCue cue in document.Cues)
                        {
                            if (cue == null || string.IsNullOrEmpty(cue.Id) || cue.Paths == null || cue.Paths.Length == 0) continue;
                            cue.Gain = float.IsNaN(cue.Gain) || float.IsInfinity(cue.Gain) ? 1 : Mathf.Clamp(cue.Gain, 0, 2);
                            cue.Priority = Mathf.Clamp(cue.Priority, 0, 100);
                            cue.Cooldown = float.IsNaN(cue.Cooldown) || float.IsInfinity(cue.Cooldown) ? 0 : Mathf.Max(0, cue.Cooldown);
                            cues[cue.Id] = cue;
                            if (cue.Id.StartsWith("music.", StringComparison.Ordinal)) continue;
                            var available = new List<AudioClip>();
                            foreach (string path in cue.Paths)
                            {
                                AudioClip clip = Resources.Load<AudioClip>(path);
                                if (clip != null) { if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData(); available.Add(clip); }
                            }
                            sounds[cue.Id] = available.ToArray();
                        }
                }
                catch (Exception exception) { Debug.LogWarning("音频目录无效，使用关键提示回退：" + exception.Message); }
            }
        }

        public AudioCue Cue(string id)
        {
            AudioCue cue;
            if (cues.TryGetValue(id, out cue)) return cue;
            bool music = id.StartsWith("music.", StringComparison.Ordinal);
            return new AudioCue { Id = id, Gain = 1, Priority = music ? 100 : id.StartsWith("boss.", StringComparison.Ordinal) ? 90 : 65, Loop = music && id != "music.victory" && id != "music.defeat" };
        }

        public string MusicPath(string id)
        { AudioCue cue; return cues.TryGetValue(id, out cue) && cue.Paths.Length > 0 ? cue.Paths[0] : null; }

        public AudioClip Sound(string id)
        {
            AudioClip[] variants;
            if (sounds.TryGetValue(id, out variants) && variants.Length > 0)
            {
                int previous; bool hasPrevious = lastVariants.TryGetValue(id, out previous);
                int index = UnityEngine.Random.Range(0, variants.Length);
                if (hasPrevious && variants.Length > 1 && index == previous) index = (index + UnityEngine.Random.Range(1, variants.Length)) % variants.Length;
                lastVariants[id] = index;
                return variants[index];
            }
            return Fallback(id);
        }

        public AudioClip Fallback(string id)
        {
            if (warned.Add(id)) Debug.LogWarning("音频资源缺失，已回退或安静跳过：" + id);
            string key = id;
            if (id.StartsWith("identity.", StringComparison.Ordinal)) key = "action.recruit";
            if (id == "boss.phase" || id == "castle.leak" || id == "wave.start") key = "boss.warning";
            if (id == "ui.reject") key = "music.defeat";
            AudioClip clip;
            return fallback.TryGetValue(key, out clip) ? clip : null;
        }

        public bool IsFallback(AudioClip clip) { return clip != null && fallback.ContainsValue(clip); }
        public void Dispose()
        { foreach (AudioClip clip in fallback.Values) { if (Application.isPlaying) UnityEngine.Object.Destroy(clip); else UnityEngine.Object.DestroyImmediate(clip); } }
    }
}
