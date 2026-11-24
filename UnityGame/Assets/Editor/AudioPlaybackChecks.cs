using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 需 -batchmode -force-audio -executeMethod AudioPlaybackChecks.RunFromCommandLine；自行退出，不加-quit。
public static class AudioPlaybackChecks
{
    private const string ActiveKey = "yanyi.audio.playback.active";
    private const string RegressionKey = "yanyi.audio.playback.regressions";
    private static GameObject owner;
    private static AudioOutputRecorder recorder;
    private static BattleSession session;
    private static List<Step> steps;
    private static readonly List<Sample> results = new List<Sample>();
    private static readonly List<string> regressionFailures = new List<string>();
    private static int index;
    private static bool recording, stageReady;
    private static double started, stageAt, dspStarted;
    private static string output;

    private sealed class Step
    {
        public string Name;
        public Action Enter;
        public Action Capture;
        public Action Validate;
        public Func<bool> WaitFor;
        public double Settle = 1.3, Duration = 1.2;
        public bool Silent;
    }
    [Serializable] private sealed class Sample { public string Name; public int Samples, Channels; public float Peak, Rms; public bool ExpectedSilence; }
    [Serializable] private sealed class Report { public bool Passed; public string Error; public int SampleRate; public double DspSeconds; public Sample[] Stages; public string[] RegressionFailures; }

    [InitializeOnLoadMethod]
    private static void Resume()
    { if (SessionState.GetBool(ActiveKey, false)) Subscribe(); }

    public static void RunFromCommandLine()
    { Start(false); }

    public static void RunRegressionsFromCommandLine()
    { Start(true); }

    private static void Start(bool regressions)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("音频采样入口仅用于批处理，完成时会退出Unity");
        AudioMixerSetup.Ensure(); AudioAssetImporter.VerifyAndReimport();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(RegressionKey, regressions);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Subscribe();
        EditorApplication.EnterPlaymode();
    }

    private static void Subscribe()
    { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    private static void Begin()
    {
        started = stageAt = EditorApplication.timeSinceStartup; dspStarted = AudioSettings.dspTime;
        output = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/audio-playback"));
        if (SessionState.GetBool(RegressionKey, false)) output += "-regressions";
        Directory.CreateDirectory(output);
        if (UnityEngine.Object.FindAnyObjectByType<YanyiGame>() != null) throw new InvalidOperationException("音频采样未隔离Bootstrap，请检查编辑器播放测试守卫");
        var values = new Dictionary<string, int>();
        var preferences = new AudioPreferences((key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
            (key, value) => values[key] = value, () => {});
        owner = new GameObject("真实音频输出采样");
        GameAudio.Initialize(owner, preferences);
        recorder = owner.AddComponent<AudioOutputRecorder>();
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        var level = database.FindLevel("huangjin");
        session = new BattleSession(database, level, level.RecommendedRoster);
        steps = new List<Step> {
            new Step { Name = "menu", Enter = () => GameAudio.ShowMenu() },
            new Step { Name = "battle", Enter = () => { GameAudio.BeginBattle("huangjin"); session.Start(); GameAudio.SyncBattle(session); } },
            new Step { Name = "boss", Enter = () => { session.Enemies.Add(new EnemyUnit { Id = 800, Health = 100, Definition = new EnemyDefinition { IsBoss = true } }); GameAudio.SyncBattle(session); } },
            new Step { Name = "warning", Settle = .3, Duration = 1.2,
                Enter = () => GameAudio.MusicEnabled = false,
                Capture = () => GameAudio.Event(new BattleEvent { Kind = "boss_warning", TargetEnemyId = 800 }) },
            new Step { Name = "background", Settle = .4, Silent = true,
                Enter = () => { GameAudio.MusicEnabled = true; GameAudio.SetApplicationPaused(true); GameAudio.SetApplicationFocused(false); GameAudio.Button(); } },
            new Step { Name = "still-paused", Settle = .4, Silent = true,
                Enter = () => { GameAudio.SetBattlePaused(true); GameAudio.SetApplicationPaused(false); GameAudio.SetApplicationFocused(true); } },
            new Step { Name = "paused-ui", Settle = .1, Duration = .4, Capture = () => GameAudio.Button() },
            new Step { Name = "resume-2x", Enter = () => { session.SetSpeed(2); GameAudio.SetBattlePaused(false); GameAudio.SyncBattle(session); } },
            new Step { Name = "victory", Settle = .3, Duration = 2,
                Enter = () => GameAudio.Event(new BattleEvent { Kind = "victory" }) },
            new Step { Name = "retry", Enter = () => GameAudio.BeginBattle("hulao") }
        };
        if (SessionState.GetBool(RegressionKey, false)) steps = RegressionSteps(database, level);
        index = 0; recording = false; stageReady = false; results.Clear(); regressionFailures.Clear(); steps[index].Enter();
        Debug.Log("音频PlayMode输出采样开始，输出目录：" + output);
    }

    private static List<Step> RegressionSteps(GameDatabase database, LevelDefinition level)
    {
        AudioSource retainedMenu = null;
        AudioClip retainedClip = null;
        int retainedPosition = 0;
        GameAudio audio = owner.GetComponent<GameAudio>();
        return new List<Step> {
            new Step { Name = "baseline-menu", Enter = () => GameAudio.ShowMenu(), Settle = 2, Duration = .5 },
            new Step { Name = "cancel-loading-keep-menu", Settle = 1.5, Duration = .5,
                Enter = () => {
                    retainedMenu = MusicSources().Single(source => source.clip != null && source.volume > 0);
                    retainedClip = retainedMenu.clip; retainedPosition = retainedMenu.timeSamples;
                    GameAudio.BeginBattle("huangjin");
                    session = new BattleSession(database, level, level.RecommendedRoster); session.Start(); GameAudio.SyncBattle(session);
                    // 同一主线程回调内取消请求，保证battle尚未被Update接入音乐源。
                    GameAudio.BeginBattle("hulao");
                },
                Validate = () => {
                    AudioSource current = MusicSources().Single(source => source.clip != null && source.volume > 0);
                    RegressionRequire(current == retainedMenu && current.clip == retainedClip && current.timeSamples > retainedPosition,
                        "取消战斗加载后菜单从头播放：原源位置=" + retainedPosition + "，当前位置=" + current.timeSamples + "，是否保留原源=" + (current == retainedMenu));
                } },
            new Step { Name = "pause-loading-resume-window", Settle = 1.5, Duration = .6,
                Enter = () => {
                    // 暂停自动Update，始终把gate保持在80ms淡出窗内；原生异步加载仍正常完成。
                    audio.enabled = false;
                    GameAudio.BeginBattle("finale");
                    session = new BattleSession(database, level, level.RecommendedRoster); session.Start(); GameAudio.SyncBattle(session);
                    GameAudio.SetBattlePaused(true); audio.Advance(.02f);
                    if (GameAudio.MusicPaused) throw new InvalidOperationException("竞态前置条件错误：20ms时已经完整暂停");
                },
                WaitFor = () => {
                    audio.Advance(0);
                    if (GameAudio.CurrentMusicCue != "music.battle.finale") return false;
                    if (GameAudio.MusicPaused) throw new InvalidOperationException("竞态前置条件错误：加载期间gate意外归零");
                    GameAudio.SetBattlePaused(false); audio.enabled = true; return true;
                },
                Validate = () => RegressionRequire(MusicSources().Any(source => source.clip != null && source.clip.name == "mus_battle_finale_v01" && source.isPlaying),
                    "20ms暂停窗内绑定新曲后恢复，新音乐源从未启动") },
            new Step { Name = "result-before-background", Settle = .4, Duration = .5,
                Enter = () => { audio.enabled = true; GameAudio.SetBattlePaused(false); GameAudio.Event(new BattleEvent { Kind = "defeat" }); } },
            new Step { Name = "result-background-return", Settle = .05, Duration = .7,
                Enter = () => {
                    AudioClip expired = MusicSources().Single(source => source.clip != null && source.volume > 0).clip;
                    if (GameAudio.CurrentMusicCue != "music.defeat") throw new InvalidOperationException("结算回归前置条件错误：结果尚未播放");
                    GameAudio.SetApplicationPaused(true); GameAudio.SetApplicationFocused(false);
                    GameAudio.SetApplicationPaused(false); GameAudio.SetApplicationFocused(true);
                    RegressionRequire(!MusicSources().Any(source => source.clip == expired && source.isPlaying),
                        "系统挂起结算后立即返回，过期结果源被恢复播放");
                },
                Validate = () => RegressionRequire(GameAudio.CurrentMusicCue == "music.menu", "结算后台返回未切回目标页menu") }
        };
    }

    private static AudioSource[] MusicSources()
    { return owner.GetComponentsInChildren<AudioSource>().Where(source => source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == "Music").ToArray(); }
    private static void RegressionRequire(bool condition, string message)
    { if (!condition) { regressionFailures.Add(message); Debug.LogError("音频回归失败：" + message); } }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (owner == null) Begin();
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 90) throw new TimeoutException("音频PlayMode采样超过90秒；DSP已推进" + (AudioSettings.dspTime - dspStarted).ToString("F2") + "秒");
            Step step = steps[index];
            if (!stageReady)
            {
                if (step.WaitFor != null && !step.WaitFor()) return;
                stageReady = true; stageAt = now;
            }
            if (!recording && now >= stageAt + step.Settle)
            {
                recorder.Begin(); recording = true; stageAt = now;
                if (step.Capture != null) step.Capture();
            }
            else if (recording && now >= stageAt + step.Duration)
            {
                if (SessionState.GetBool(RegressionKey, false))
                {
                    try { Capture(step); } catch (Exception exception) { regressionFailures.Add(step.Name + "：" + exception.Message); Debug.LogError("音频回归失败：" + exception.Message); }
                }
                else Capture(step);
                if (step.Validate != null) step.Validate();
                index++;
                if (index >= steps.Count) { Finish(regressionFailures.Count == 0 ? null : new InvalidOperationException(string.Join("\n", regressionFailures.ToArray()))); return; }
                recording = false; stageReady = false; stageAt = now; if (steps[index].Enter != null) steps[index].Enter();
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Capture(Step step)
    {
        int channels; float[] data = recorder.End(out channels);
        double square = 0; float peak = 0;
        foreach (float value in data) { peak = Mathf.Max(peak, Mathf.Abs(value)); square += value * (double)value; }
        float rms = data.Length == 0 ? 0 : (float)Math.Sqrt(square / data.Length);
        var sample = new Sample { Name = step.Name, Samples = data.Length, Channels = channels, Peak = peak, Rms = rms, ExpectedSilence = step.Silent };
        results.Add(sample);
        WriteWave(System.IO.Path.Combine(output, step.Name + ".wav"), data, channels, AudioSettings.outputSampleRate);
        if (data.Length == 0) throw new InvalidOperationException("未取得DSP输出采样：" + step.Name + "；请确认-force-audio和可用音频设备");
        if (step.Silent ? peak > .0001f : rms < .000005f) throw new InvalidOperationException("实际输出能量不符合预期：" + step.Name + "，peak=" + peak + "，rms=" + rms);
        AudioSource[] sources = owner.GetComponentsInChildren<AudioSource>();
        if (sources.Length != 11 || sources.Where(source => source.outputAudioMixerGroup.name == "Music").Any(source => source.pitch != 1))
            throw new InvalidOperationException("实际播放时声部超过11或音乐音高改变");
        Debug.Log("音频输出采样：" + step.Name + "，samples=" + data.Length + "，peak=" + peak.ToString("F6") + "，rms=" + rms.ToString("F6"));
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(ActiveKey, false); EditorApplication.update -= Tick;
        if (string.IsNullOrEmpty(output)) output = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/audio-playback"));
        Directory.CreateDirectory(output);
        File.WriteAllText(System.IO.Path.Combine(output, "report.json"), JsonUtility.ToJson(new Report {
            Passed = error == null, Error = error == null ? null : error.ToString(), SampleRate = AudioSettings.outputSampleRate,
            DspSeconds = AudioSettings.dspTime - dspStarted, Stages = results.ToArray(), RegressionFailures = regressionFailures.ToArray()
        }, true));
        if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
        if (error != null) Debug.LogException(error); else Debug.Log("音频PlayMode采样验证通过；WAV与report.json已保存，此检查不替代手机与人工听测");
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void WriteWave(string path, float[] samples, int channels, int sampleRate)
    {
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)channels);
            writer.Write(sampleRate); writer.Write(sampleRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
        }
    }
}
