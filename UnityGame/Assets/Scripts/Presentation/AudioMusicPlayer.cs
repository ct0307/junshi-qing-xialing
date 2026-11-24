using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace YanyiSandpan.Presentation
{
    // 两个音乐源交替接曲；过期加载请求不会改变当前会话。
    internal sealed class AudioMusicPlayer
    {
        private readonly AudioCatalog catalog;
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly float[] weights = new float[2];
        private readonly float[] cueGains = new float[2];
        private readonly bool[] started = new bool[2];
        private readonly List<ResourceRequest> discardedLoads = new List<ResourceRequest>();
        private ResourceRequest loading;
        private AudioClip loaded;
        private string desiredCue;
        private float fadeDuration = 1, fadeElapsed, outgoingStart, gate = 1;
        private int incoming, outgoing = 1;
        private bool paused;
        public string CueId { get; private set; }
        public bool IsPaused { get; private set; }
        public float ClipLength { get { return sources[incoming].clip == null ? 0 : sources[incoming].clip.length; } }

        public AudioMusicPlayer(Transform parent, AudioMixerGroup group, AudioCatalog catalog)
        {
            this.catalog = catalog;
            sources[0] = AudioVoicePool.CreateSource(parent, "音乐A", group);
            sources[1] = AudioVoicePool.CreateSource(parent, "音乐B", group);
        }

        public void Request(string cue, float seconds)
        {
            if (desiredCue == cue) return;
            CancelLoad(); desiredCue = cue;
            // 新曲尚在加载时返回当前曲，只取消请求，保留当前音源和播放位置。
            if (CueId == cue && sources[incoming].clip != null) return;
            fadeDuration = Mathf.Max(.01f, seconds);
            string path = catalog.MusicPath(cue);
            if (string.IsNullOrEmpty(path)) { loaded = catalog.Fallback(cue); if (loaded != null) Switch(loaded); return; }
            if (Application.isPlaying) loading = Resources.LoadAsync<AudioClip>(path);
            else { loaded = Resources.Load<AudioClip>(path); CompleteLoad(); }
        }

        private void CompleteLoad()
        {
            if (loaded == null) { loaded = catalog.Fallback(desiredCue); if (loaded == null) return; }
            if (loaded.loadState == AudioDataLoadState.Unloaded) loaded.LoadAudioData();
            // 编辑器静态检查只观察资源绑定；实际播放始终等到异步数据就绪。
            if (loaded.loadState == AudioDataLoadState.Loading && Application.isPlaying) return;
            if (loaded.loadState == AudioDataLoadState.Failed) { Release(loaded); loaded = catalog.Fallback(desiredCue); }
            if (loaded != null) Switch(loaded);
        }

        private void Switch(AudioClip clip)
        {
            incoming = weights[0] <= weights[1] ? 0 : 1; outgoing = 1 - incoming;
            AudioClip previous = sources[incoming].clip;
            sources[incoming].Stop(); sources[incoming].clip = null;
            started[incoming] = false;
            Release(previous);
            AudioCue cue = catalog.Cue(desiredCue);
            sources[incoming].clip = clip; sources[incoming].loop = cue.Loop;
            sources[incoming].pitch = 1; sources[incoming].priority = cue.Loop ? 64 : 0;
            cueGains[incoming] = cue.Gain; weights[incoming] = 0;
            outgoingStart = weights[outgoing]; fadeElapsed = 0; CueId = desiredCue;
            loaded = null;
            if (!paused) { sources[incoming].Play(); started[incoming] = true; }
        }

        public void SetPaused(bool value, bool immediate)
        {
            if (paused == value && (!value || !immediate)) return;
            paused = value;
            if (paused && immediate) { gate = 0; PauseSources(); }
            if (!paused)
            {
                bool resumeSources = IsPaused;
                IsPaused = false;
                for (int i = 0; i < sources.Length; i++)
                {
                    AudioSource source = sources[i];
                    if (source.clip == null) continue;
                    // 淡出窗内新绑定的片段尚未启动，快速恢复也必须开始播放。
                    if (!started[i]) { source.Play(); started[i] = true; }
                    else if (resumeSources) source.UnPause();
                }
            }
        }

        public void Advance(float delta, float volume, float intensity)
        {
            if (loading != null && loading.isDone) { loaded = loading.asset as AudioClip; loading = null; CompleteLoad(); }
            else if (loaded != null) CompleteLoad();
            if (loading == null)
                for (int i = discardedLoads.Count - 1; i >= 0; i--)
                    if (discardedLoads[i].isDone) { Release(discardedLoads[i].asset as AudioClip); discardedLoads.RemoveAt(i); }
            gate = Mathf.MoveTowards(gate, paused ? 0 : 1, delta / .08f);
            if (paused && gate <= 0 && !IsPaused) PauseSources();
            if (!paused)
            {
                fadeElapsed += delta;
                float progress = Mathf.Clamp01(fadeElapsed / fadeDuration);
                weights[incoming] = Mathf.Sin(progress * Mathf.PI * .5f);
                weights[outgoing] = outgoingStart * Mathf.Cos(progress * Mathf.PI * .5f);
                if (progress >= 1 && sources[outgoing].clip != null)
                {
                    AudioClip previous = sources[outgoing].clip;
                    sources[outgoing].Stop(); sources[outgoing].clip = null; weights[outgoing] = 0; started[outgoing] = false;
                    Release(previous);
                }
            }
            for (int i = 0; i < sources.Length; i++) sources[i].volume = Mathf.Clamp01(weights[i] * cueGains[i] * volume * intensity * gate);
        }

        private void PauseSources()
        { foreach (AudioSource source in sources) { source.volume = 0; source.Pause(); } IsPaused = true; }
        private void CancelLoad()
        {
            if (loading != null) discardedLoads.Add(loading);
            loading = null; AudioClip discarded = loaded; loaded = null; Release(discarded);
        }
        private void Release(AudioClip clip)
        {
            if (clip == null || !Application.isPlaying || catalog.IsFallback(clip)) return;
            foreach (AudioSource source in sources) if (source.clip == clip) return;
            if (loaded == clip) return;
            Resources.UnloadAsset(clip);
        }

        public void DiscardResult()
        {
            if (desiredCue == "music.victory" || desiredCue == "music.defeat" || CueId == "music.victory" || CueId == "music.defeat") Stop();
        }

        private void Stop()
        {
            CancelLoad();
            desiredCue = CueId = null;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioClip clip = sources[i].clip; sources[i].Stop(); sources[i].clip = null;
                weights[i] = 0; started[i] = false; Release(clip);
            }
            outgoingStart = fadeElapsed = 0;
        }

        public void Dispose() { Stop(); }
    }
}
