using System;
using System.Collections.Generic;
using UnityEngine;

namespace YanyiSandpan.Presentation
{
    // 保留原有七类合成短音作为缺失资源时的关键反馈。
    internal static class AudioFallbackClips
    {
        public static Dictionary<string, AudioClip> Create()
        {
            return new Dictionary<string, AudioClip> {
                { "ui.click", Melody("回退按钮轻触", new[] { 660f }, .065f, .14f) },
                { "action.recruit", Melody("回退招募入阵", new[] { 330f, 440f, 660f }, .07f, .2f) },
                { "action.upgrade", Melody("回退守将进阶", new[] { 440f, 660f, 880f }, .085f, .2f) },
                { "combat.hit", Melody("回退命中轻响", new[] { 190f }, .055f, .1f) },
                { "boss.warning", Melody("回退敌将预警", new[] { 220f, 0f, 220f, 294f }, .11f, .18f) },
                { "music.victory", Melody("回退守关大捷", new[] { 330f, 440f, 494f, 660f }, .17f, .22f) },
                { "music.defeat", Melody("回退此局失守", new[] { 330f, 294f, 220f }, .2f, .19f) }
            };
        }

        private static AudioClip Melody(string name, float[] notes, float noteSeconds, float gain)
        {
            const int sampleRate = 22050;
            int length = Mathf.CeilToInt(notes.Length * noteSeconds * sampleRate);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                double time = (double)i / sampleRate;
                int note = Math.Min(notes.Length - 1, (int)(time / noteSeconds));
                if (notes[note] <= 0) continue;
                double local = time - note * noteSeconds;
                double attack = Math.Min(1, local / .008);
                double release = Math.Min(1, (noteSeconds - local) / .025);
                double envelope = attack * Math.Max(0, release) * Math.Exp(-local * 7);
                double phase = local * notes[note] * Math.PI * 2;
                samples[i] = (float)((Math.Sin(phase) * .82 + Math.Sin(phase * 2) * .12 + Math.Sin(phase * 3) * .06) * envelope * gain);
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
