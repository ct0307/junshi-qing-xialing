using UnityEngine;
using UnityEngine.Audio;

namespace YanyiSandpan.Presentation
{
    // 每个固定声部仅使用 clip + Play，池满时按逻辑重要度抢占或丢弃。
    internal sealed class AudioVoicePool
    {
        private sealed class Voice
        {
            public AudioSource Source;
            public double EndsAt;
            public int Priority;
            public float Gain;
        }
        private readonly Voice[] voices;

        public AudioVoicePool(Transform parent, string name, int count, AudioMixerGroup group)
        {
            voices = new Voice[count];
            for (int i = 0; i < count; i++) voices[i] = new Voice { Source = CreateSource(parent, name + (i + 1), group) };
        }

        internal static AudioSource CreateSource(Transform parent, string name, AudioMixerGroup group)
        {
            var child = new GameObject(name); child.transform.SetParent(parent, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            source.outputAudioMixerGroup = group; source.pitch = 1; source.volume = 0;
            return source;
        }

        public bool Play(AudioClip clip, float gain, int priority, bool variedPitch, double now, float volume)
        {
            if (clip == null || clip.loadState == AudioDataLoadState.Failed) return false;
            Voice selected = null;
            foreach (Voice voice in voices)
                if (voice.Source.clip == null || voice.EndsAt <= now) { selected = voice; break; }
            if (selected == null)
            {
                foreach (Voice voice in voices)
                    if (selected == null || voice.Priority < selected.Priority || (voice.Priority == selected.Priority && voice.EndsAt < selected.EndsAt)) selected = voice;
                if (priority < selected.Priority) return false;
            }
            AudioSource source = selected.Source;
            source.Stop(); source.clip = clip; source.loop = false;
            source.pitch = variedPitch ? Random.Range(.97f, 1.03f) : 1;
            source.priority = Mathf.Clamp(256 - priority * 2, 0, 256);
            selected.Priority = priority; selected.Gain = gain;
            selected.EndsAt = now + clip.length / source.pitch;
            source.volume = Mathf.Clamp01(gain * volume); source.Play();
            return true;
        }

        public void Advance(double now, float volume)
        {
            foreach (Voice voice in voices)
            {
                if (voice.Source.clip != null && voice.EndsAt <= now) { voice.Source.Stop(); voice.Source.clip = null; }
                voice.Source.volume = Mathf.Clamp01(voice.Gain * volume);
            }
        }

        public void Stop()
        { foreach (Voice voice in voices) { voice.Source.Stop(); voice.Source.clip = null; voice.EndsAt = 0; } }
    }
}
