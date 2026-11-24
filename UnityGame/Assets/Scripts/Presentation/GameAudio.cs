using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 表现层统一音频入口；只观察会话，不消费或修改战斗规则。
    public sealed class GameAudio : MonoBehaviour
    {
        private static GameAudio instance;
        private static AudioPreferences preferences;
        private AudioCatalog catalog;
        private AudioMusicPlayer music;
        private AudioVoicePool ui, ordinary, critical;
        private Func<double> clock;
        private readonly Dictionary<string, double> cooldowns = new Dictionary<string, double>();
        private readonly HashSet<int> phases = new HashSet<int>();
        private readonly HashSet<int> waves = new HashSet<int>();
        private readonly Queue<double> ordinaryPlays = new Queue<double>();
        private readonly Dictionary<string, int> representativeSequences = new Dictionary<string, int>();
        private BattleSession session;
        private string levelId = "huangjin";
        private bool battleOpen, battlePaused, applicationPaused, applicationUnfocused, terminal;
        private float musicIntensity = 1, currentIntensity = 1, duck = 1;
        private double duckUntil, resultEndsAt = -1;
        private long soundSequence;
        private string lastSoundCue;
        private float lastBurstAt = -1;

        private static AudioPreferences Settings
        {
            get
            {
                if (preferences == null) preferences = new AudioPreferences(PlayerPrefs.GetInt, PlayerPrefs.SetInt, PlayerPrefs.Save);
                return preferences;
            }
        }
        public static bool MusicEnabled { get { return Settings.MusicEnabled; } set { Settings.MusicEnabled = value; } }
        public static bool SoundEnabled { get { return Settings.SoundEnabled; } set { Settings.SoundEnabled = value; } }
        public static float MusicVolume { get { return Settings.MusicVolume; } set { Settings.MusicVolume = value; } }
        public static float SoundVolume { get { return Settings.SoundVolume; } set { Settings.SoundVolume = value; } }
        public static string CurrentMusicCue { get { return instance == null ? null : instance.music.CueId; } }
        public static string LastSoundCue { get { return instance == null ? null : instance.lastSoundCue; } }
        public static long SoundSequence { get { return instance == null ? 0 : instance.soundSequence; } }
        public static bool MusicPaused { get { return instance != null && instance.music.IsPaused; } }
        public static bool SystemSuspended { get { return instance != null && (instance.applicationPaused || instance.applicationUnfocused); } }

        public static bool Enabled
        {
            get { return MusicEnabled && SoundEnabled; }
            set { MusicEnabled = value; SoundEnabled = value; }
        }

        public static void Initialize(GameObject parent)
        { Initialize(parent, Settings, () => Time.realtimeSinceStartupAsDouble); }

        public static void Initialize(GameObject parent, AudioPreferences settings)
        { Initialize(parent, settings, () => Time.realtimeSinceStartupAsDouble); }

        public static void Initialize(GameObject parent, AudioPreferences settings, Func<double> realTime)
        {
            if (instance != null) return;
            if (parent == null || settings == null || realTime == null) throw new ArgumentNullException("音频初始化参数不能为空");
            preferences = settings;
            instance = parent.AddComponent<GameAudio>();
            instance.clock = realTime;
            instance.catalog = new AudioCatalog();
            AudioMixer mixer = Resources.Load<AudioMixer>("Audio/GameMixer");
            instance.music = new AudioMusicPlayer(parent.transform, Group(mixer, "Music"), instance.catalog);
            instance.ui = new AudioVoicePool(parent.transform, "界面音", 1, Group(mixer, "UI"));
            instance.ordinary = new AudioVoicePool(parent.transform, "战斗音", 6, Group(mixer, "Battle"));
            instance.critical = new AudioVoicePool(parent.transform, "关键提示", 2, Group(mixer, "Critical"));
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) parent.AddComponent<AudioListener>();
            preferences.Changed += instance.ApplyPreferences;
            instance.ApplyPreferences();
        }

        private static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(name)) if (group.name == name) return group;
            return null;
        }

        public static void ShowMenu()
        {
            if (instance == null) return;
            instance.ResetSession(false); instance.musicIntensity = 1;
            instance.music.Request("music.menu", 1); instance.ApplyMusicGate();
        }

        public static void BeginBattle() { BeginBattle("huangjin"); }
        public static void BeginBattle(string id)
        {
            if (instance == null) return;
            instance.ResetSession(true); instance.levelId = string.IsNullOrEmpty(id) ? "huangjin" : id;
            instance.musicIntensity = .708f; instance.music.Request("music.menu", 1); instance.ApplyMusicGate();
        }

        private void ResetSession(bool open)
        {
            battleOpen = open; battlePaused = terminal = false; session = null;
            resultEndsAt = -1; duckUntil = 0; duck = 1;
            lastBurstAt = -1;
            cooldowns.Clear(); phases.Clear(); waves.Clear(); ordinaryPlays.Clear(); representativeSequences.Clear();
            StopBattleSounds();
        }

        public static void SyncBattle(BattleSession current)
        {
            if (instance == null || !instance.battleOpen || current == null) return;
            if (instance.session != null && instance.session != current) return;
            instance.session = current;
            if (current.State == BattleState.Victory || current.State == BattleState.Defeat)
            { instance.Finish(current.State == BattleState.Victory); return; }
            SetBattlePaused(current.State == BattleState.Paused);
            if (instance.terminal) return;
            bool preparing = current.State == BattleState.Preparation;
            bool boss = current.Enemies.Exists(enemy => enemy.Health > 0 && enemy.Definition != null && enemy.Definition.IsBoss);
            instance.musicIntensity = preparing || (!current.IsWaveActive && !boss) ? .708f : 1;
            instance.music.Request(preparing ? "music.menu" : boss ? "music.boss" : "music.battle." + instance.levelId, 1);
        }

        public static void SetBattlePaused(bool paused)
        {
            if (instance == null) return;
            instance.battlePaused = paused;
            if (paused) instance.StopBattleSounds();
            instance.ApplyMusicGate();
        }

        public static void SetApplicationPaused(bool paused)
        { if (instance == null) return; instance.applicationPaused = paused; instance.ApplySystemGate(); }
        public static void SetApplicationFocused(bool focused)
        { if (instance == null) return; instance.applicationUnfocused = !focused; instance.ApplySystemGate(); }

        private void ApplySystemGate()
        {
            if (SystemSuspended)
            {
                ui.Stop(); StopBattleSounds();
                // 离开前台后丢弃过期结果短句；返回页面时只恢复菜单音乐。
                if (terminal) { resultEndsAt = -1; musicIntensity = .5f; music.DiscardResult(); music.Request("music.menu", .25f); }
            }
            ApplyMusicGate();
        }
        private void ApplyMusicGate()
        { music.SetPaused(SystemSuspended || battlePaused || !MusicEnabled || MusicVolume <= 0, SystemSuspended || !MusicEnabled || MusicVolume <= 0); }
        private void ApplyPreferences()
        {
            if (!SoundEnabled || SoundVolume <= 0) { ui.Stop(); StopBattleSounds(); }
            ApplyMusicGate();
        }
        private void StopBattleSounds() { ordinary.Stop(); critical.Stop(); }

        public static void Button() { if (instance != null) instance.Play("ui.click", instance.ui, false, .04, false); }
        public static void Reject() { if (instance != null) instance.Play("ui.reject", instance.ui, false, .08, false); }

        public static void Event(BattleEvent item)
        {
            if (instance == null || item == null || !instance.battleOpen) return;
            if (item.Kind == "victory" || item.Kind == "defeat") { instance.Finish(item.Kind == "victory"); return; }
            if (instance.terminal || instance.battlePaused || SystemSuspended || !SoundEnabled || SoundVolume <= 0) return;
            instance.HandleEvent(item);
        }

        // 与图形共用一次聚合结果；同批最高档替代击退声，不把历史短音排队补播。
        public static void EventBatch(IList<BattleEvent> items, IList<BattleFeedbackResult> feedback, float now)
        {
            if (items == null || feedback == null || items.Count != feedback.Count) return;
            BattleFeedbackResult burst = new BattleFeedbackResult();
            for (int index = 0; index < items.Count; index++)
            {
                BattleFeedbackResult result = feedback[index];
                if (result.CrossedTier && now - result.At < .2f && result.Tier >= burst.Tier) burst = result;
            }
            for (int index = 0; index < items.Count; index++)
            {
                BattleEvent item = items[index];
                if (item == null) continue;
                // 在进入角色、技能组与Cue冷却前丢弃过期短音，避免旧事件吞掉后续新鲜反馈。
                if (now - item.OccurredAt >= .25f && (item.Kind == "skill_cast" || item.Kind == "attack" || item.Kind == "basic_attack" ||
                    item.Kind == "hit" || item.Kind == "fire" || item.Kind == "stun" || item.Kind == "slow" || item.Kind == "faction_hit")) continue;
                if (item.Kind == "kill" && (burst.Tier > 0 || !feedback[index].Accepted || now - item.OccurredAt >= .25f)) continue;
                Event(item);
            }
            if (burst.Tier == 0 || instance == null || !instance.battleOpen || instance.terminal || instance.battlePaused ||
                SystemSuspended || !SoundEnabled || SoundVolume <= 0 || now - instance.lastBurstAt < .2f) return;
            if (instance.Play("enemy.defeat", instance.ordinary, true, .2, false, burst.Tier == 3 ? 1 : burst.Tier == 2 ? .95f : .8f))
                instance.lastBurstAt = now;
        }

        private void HandleEvent(BattleEvent item)
        {
            double now = clock();
            switch (item.Kind)
            {
                case "recruit":
                    string identity = item.HeroId == "lv_bu" || item.HeroId == "zhang_fei" || item.HeroId == "zhuge_liang" ? "identity." + item.HeroId : "action.recruit";
                    Play(identity, critical, true, .06, false); break;
                case "upgrade": Play("action.upgrade", critical, true, .06, false); break;
                case "faction_hit":
                    if (Ready("faction.hero." + item.HeroId, .8, now)) Play("attack.tactic", ordinary, true, .8, false, .26f);
                    break;
                case "basic_attack":
                    if (!Ready("basic.hero." + (item.HeroId ?? "basic"), .3, now)) return;
                    Play("combat.hit", ordinary, true, .18, true, .38f); break;
                case "skill_cast":
                    if (!IsRepresentativeId(item.HeroId) || item.AttackSequenceId <= 0) break;
                    int lastSequence;
                    if (representativeSequences.TryGetValue(item.HeroId, out lastSequence) && lastSequence == item.AttackSequenceId) break;
                    representativeSequences[item.HeroId] = item.AttackSequenceId;
                    // 每个真实序列只尝试一次主声，多目标命中不再次重播。
                    Play("attack." + AttackGroup(item.HeroId), ordinary, true, .08, true);
                    break;
                case "attack": case "fire": case "stun": case "slow":
                    if (IsRepresentativeId(item.HeroId) && (item.AttackSequenceId > 0 || representativeSequences.ContainsKey(item.HeroId))) return;
                    string group = item.Kind == "fire" ? "fire" : item.Kind == "stun" || item.Kind == "slow" ? "control" : AttackGroup(item.HeroId);
                    string heroKey = "cast.hero." + (item.HeroId ?? "control");
                    if (!Ready(heroKey, .1, now) || !Ready("cast.group." + group, .08, now)) return;
                    Play("attack." + group, ordinary, true, .08, true); break;
                case "hit": Play("combat.hit", ordinary, true, .12, true); break;
                case "kill": Play("enemy.defeat", ordinary, true, .25, false); break;
                case "leak": Play("castle.leak", critical, true, .4, false); break;
                case "wave": if (waves.Add((int)item.Amount)) Play("wave.start", critical, true, 0, false); break;
                case "boss_warning":
                    if (!Ready("warning.enemy." + item.TargetEnemyId, .15, now)) return;
                    if (Play("boss.warning", critical, true, .15, false)) duckUntil = now + .8;
                    break;
                case "boss_phase":
                    if (phases.Add(item.TargetEnemyId) && Play("boss.phase", critical, true, 0, false)) duckUntil = now + .8;
                    break;
            }
        }

        private static bool IsRepresentativeId(string id)
        { return id == "lv_bu" || id == "zhou_yu" || id == "zhuge_liang"; }

        private bool Ready(string key, double seconds, double now)
        {
            double next;
            if (cooldowns.TryGetValue(key, out next) && now + .00001 < next) return false;
            cooldowns[key] = now + seconds; return true;
        }

        private bool Play(string id, AudioVoicePool pool, bool battle, double minimumCooldown, bool variedPitch, float gain = 1)
        {
            if (SystemSuspended || !SoundEnabled || SoundVolume <= 0 || (battle && (battlePaused || terminal))) return false;
            double now = clock(); AudioCue cue = catalog.Cue(id);
            if (!Ready("cue." + id, Math.Max(minimumCooldown, cue.Cooldown), now)) return false;
            bool normal = pool == ordinary;
            while (ordinaryPlays.Count > 0 && ordinaryPlays.Peek() <= now - 1) ordinaryPlays.Dequeue();
            if (normal && ordinaryPlays.Count >= 16) return false;
            AudioClip clip = catalog.Sound(id);
            if (!pool.Play(clip, cue.Gain * gain, cue.Priority, variedPitch, now, SoundVolume * (normal && now < duckUntil ? .708f : 1))) return false;
            if (normal) ordinaryPlays.Enqueue(now);
            soundSequence++; lastSoundCue = id; return true;
        }

        private void Finish(bool victory)
        {
            if (terminal) return;
            terminal = true; battlePaused = false; resultEndsAt = -1;
            ui.Stop(); StopBattleSounds(); cooldowns.Clear(); ordinaryPlays.Clear(); duckUntil = 0; duck = 1;
            musicIntensity = 1; music.Request(victory ? "music.victory" : "music.defeat", .2f); ApplyMusicGate();
            if (SystemSuspended) ApplySystemGate();
        }

        private static string AttackGroup(string id)
        {
            switch (id)
            {
                case "huang_zhong": case "taishi_ci": case "yuan_shao": case "xun_yu": case "sun_shangxiang": return "bow";
                case "zhou_yu": case "lu_xun": return "fire";
                case "zhang_fei": case "zhuge_liang": case "zhang_jiao": case "sima_yi": return "control";
                case "guo_jia": case "cao_cao": case "diao_chan": case "pang_tong": return "tactic";
                case "hua_tuo": case "lu_su": return "support";
                default: return "melee";
            }
        }

        private void Update() { Advance(Time.unscaledDeltaTime); }
        public void Advance(float unscaledDeltaTime)
        {
            if (music == null || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)) return;
            float delta = Mathf.Max(0, unscaledDeltaTime); double now = clock();
            float targetDuck = now < duckUntil ? .501f : 1;
            duck = Mathf.MoveTowards(duck, targetDuck, delta * (targetDuck < duck ? .499f / .06f : .499f / .35f));
            currentIntensity = Mathf.MoveTowards(currentIntensity, musicIntensity, delta / .3f);
            music.Advance(delta, MusicEnabled ? MusicVolume : 0, currentIntensity * duck);
            ui.Advance(now, SoundEnabled && !SystemSuspended ? SoundVolume : 0);
            ordinary.Advance(now, SoundEnabled && !SystemSuspended && !battlePaused ? SoundVolume * (now < duckUntil ? .708f : 1) : 0);
            critical.Advance(now, SoundEnabled && !SystemSuspended && !battlePaused ? SoundVolume : 0);
            if (terminal && !SystemSuspended && (music.CueId == "music.victory" || music.CueId == "music.defeat"))
            {
                if (resultEndsAt < 0) resultEndsAt = now + music.ClipLength;
                else if (now >= resultEndsAt) { resultEndsAt = -1; musicIntensity = .5f; music.Request("music.menu", 1); }
            }
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (preferences != null) preferences.Changed -= ApplyPreferences;
            if (music != null) music.Dispose();
            if (catalog != null) catalog.Dispose();
            preferences = null; instance = null;
        }
    }
}
