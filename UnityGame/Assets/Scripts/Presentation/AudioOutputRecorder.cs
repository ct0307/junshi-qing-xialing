#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YanyiSandpan.Presentation
{
    // 仅编辑器采样探针；在音频线程复制Listener最终混音，正式构建不会包含它。
    public sealed class AudioOutputRecorder : MonoBehaviour
    {
        private readonly object sync = new object();
        private readonly List<float> samples = new List<float>();
        private bool recording;
        private int channels = 2;

        public void Begin()
        { lock (sync) { samples.Clear(); recording = true; } }

        public float[] End(out int channelCount)
        { lock (sync) { recording = false; channelCount = channels; return samples.ToArray(); } }

        private void OnAudioFilterRead(float[] data, int channelCount)
        {
            lock (sync)
            {
                if (!recording) return;
                channels = channelCount;
                int count = Math.Min(data.Length, 48000 * 2 * 15 - samples.Count);
                for (int i = 0; i < count; i++) samples.Add(data[i]);
            }
        }
    }
}
#endif
