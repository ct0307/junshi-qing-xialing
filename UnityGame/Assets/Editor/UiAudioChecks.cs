using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 真实服务、音源与隔离存储的回归检查；不会读写玩家的 PlayerPrefs。
public static class UiAudioChecks
{
    private static readonly Type Service = typeof(GameAudio);
    private static double now;

    public static void RunFromCommandLine() { Verify(); }

    public static void Verify()
    {
        Require(Service.GetProperty("MusicEnabled") != null, "音频缺少独立音乐开关 MusicEnabled");
        Require(Service.GetProperty("SoundEnabled") != null, "音频缺少独立音效开关 SoundEnabled");
        VerifyPreferences();
        AudioMixerSetup.Ensure();
        AudioAssetImporter.VerifyAndReimport();
        VerifyCatalog();
        var owner = new GameObject("隔离音频验证");
        now = 100;
        try
        {
            object settings = Preferences(new Dictionary<string, int>());
            MethodInfo initialize = Service.GetMethods().SingleOrDefault(method => method.Name == "Initialize" && method.GetParameters().Length == 3);
            Require(initialize != null, "音频缺少隔离存储/时钟初始化入口");
            initialize.Invoke(null, new object[] { owner, settings, new Func<double>(() => now) });
            VerifySources(owner);
            var source = Resources.Load<TextAsset>("Game/database");
            Require(source != null, "音频测试缺少真实关卡配置");
            var database = JsonUtility.FromJson<GameDatabase>(source.text);
            var level = database.FindLevel("huangjin");
            var session = new BattleSession(database, level, level.RecommendedRoster);
            VerifyMusic(session);
            VerifyEvents(owner, session);
            VerifyBurstEvents(owner);
            VerifyExpiredShortSounds();
            VerifyRepresentativeCastSounds();
            VerifyGates(owner, session);
            VerifyResult(owner);
            VerifyMissingAssets(owner, database);
            Require(owner.GetComponentsInChildren<AudioSource>().All(audio => audio.pitch >= .969f && audio.pitch <= 1.031f), "倍速或恢复改变了音高预算");
            Debug.Log("音频逻辑验证通过：独立设置迁移、11声部与混音路由、战况音乐、去重/限频、终局与暂停门控；这不是设备听测");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    private static object Preferences(Dictionary<string, int> values)
    {
        Type type = Service.Assembly.GetType("YanyiSandpan.Presentation.AudioPreferences");
        Require(type != null, "缺少可隔离存储的 AudioPreferences");
        return Activator.CreateInstance(type, new object[] {
            new Func<string, int, int>((key, fallback) => { int result; return values.TryGetValue(key, out result) ? result : fallback; }),
            new Action<string, int>((key, value) => values[key] = value), new Action(() => {}) });
    }

    private static void VerifyRepresentativeCastSounds()
    {
        GameAudio.BeginBattle("huangjin");
        int sequence = 600;
        foreach (string hero in new[] { "lv_bu", "zhou_yu", "zhuge_liang" })
        {
            now += 2;
            long before = GameAudio.SoundSequence;
            var cast = new BattleEvent { Kind = "skill_cast", HeroId = hero, AttackSequenceId = ++sequence };
            GameAudio.Event(cast);
            Require(GameAudio.SoundSequence == before + 1, "真实主技能缺少一次主声：" + hero);
            GameAudio.Event(cast);
            for (int i = 0; i < 8; i++) GameAudio.Event(new BattleEvent { Kind = "attack", HeroId = hero, AttackSequenceId = sequence, TargetEnemyId = i + 1 });
            now += .4;
            GameAudio.Event(new BattleEvent { Kind = hero == "zhou_yu" ? "fire" : "slow", HeroId = hero });
            Require(GameAudio.SoundSequence == before + 1, "重复施放或多目标重复主声：" + hero);
        }
        GameAudio.BeginBattle("huangjin");
    }

    private static void VerifyPreferences()
    {
        var values = new Dictionary<string, int> { { "yanyi.audio.enabled", 0 } };
        object settings = Preferences(values);
        Require(!(bool)Read(settings, "MusicEnabled") && !(bool)Read(settings, "SoundEnabled"), "旧静音没有迁移为音乐、音效双关");
        Set(settings, "MusicEnabled", true);
        Set(settings, "MusicVolume", .27f);
        Set(settings, "SoundVolume", 2f);
        settings = Preferences(values);
        Require((bool)Read(settings, "MusicEnabled") && !(bool)Read(settings, "SoundEnabled"), "再次打开设置时旧静音覆盖了新设置");
        Require(Math.Abs((float)Read(settings, "MusicVolume") - .27f) < .002f && (float)Read(settings, "SoundVolume") == 1f, "音量没有独立保存/限制范围");
        Set(settings, "MusicVolume", float.NaN);
        Require((float)Read(settings, "MusicVolume") == 0, "非法音量未安全静音");
        settings = Preferences(new Dictionary<string, int>());
        Require((bool)Read(settings, "MusicEnabled") && (bool)Read(settings, "SoundEnabled"), "新玩家默认开关错误");
        Require((float)Read(settings, "MusicVolume") == .5f && (float)Read(settings, "SoundVolume") == .8f, "音乐50%/音效80%默认值错误");
    }

    private static void VerifySources(GameObject owner)
    {
        AudioSource[] sources = owner.GetComponentsInChildren<AudioSource>();
        Require(sources.Length == 11, "应固定11个音源，实际" + sources.Length);
        Require(sources.All(audio => !audio.playOnAwake && audio.spatialBlend == 0 && audio.outputAudioMixerGroup != null), "音源未使用2D原生混音器路由");
        Require(sources.Count(audio => audio.outputAudioMixerGroup.name == "Music") == 2, "音乐应独占双源");
        Require(sources.Count(audio => audio.outputAudioMixerGroup.name == "UI") == 1, "UI声部应为1");
        Require(sources.Count(audio => audio.outputAudioMixerGroup.name == "Battle") == 6, "普通战斗声部应为6");
        Require(sources.Count(audio => audio.outputAudioMixerGroup.name == "Critical") == 2, "关键声部应为2");
    }

    [Serializable] private sealed class Catalog { public int Version; public Cue[] Cues; }
    [Serializable] private sealed class Cue { public string Id; public string[] Paths; }
    private static void VerifyCatalog()
    {
        TextAsset json = Resources.Load<TextAsset>("Audio/catalog");
        Require(json != null, "缺少Audio/catalog配置");
        Catalog catalog = JsonUtility.FromJson<Catalog>(json.text);
        Require(catalog.Version == 1 && catalog.Cues.Length == 28 && catalog.Cues.Select(cue => cue.Id).Distinct().Count() == 28, "首发应有28个唯一Cue");
        Require(catalog.Cues.Sum(cue => cue.Paths.Length) == 43, "首发应有43个音频文件");
        foreach (Cue cue in catalog.Cues)
            foreach (string path in cue.Paths)
            {
                AudioClip clip = Resources.Load<AudioClip>(path);
                Require(clip != null && clip.length > .01f, "音频资源无法加载：" + path);
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                bool music = cue.Id.StartsWith("music.", StringComparison.Ordinal);
                Require(importer != null && importer.defaultSampleSettings.loadType == (music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad), "音频加载策略不正确：" + path);
                Require(importer.defaultSampleSettings.compressionFormat == (music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM), "音频编码策略不正确：" + path);
                Require(Math.Abs(importer.defaultSampleSettings.quality - .7f) < .001f, "导入音乐压缩档位未更新：" + path);
                Require(music || importer.forceToMono, "短音应导入为单声道：" + path);
            }
    }

    private static void VerifyMusic(BattleSession session)
    {
        Call("ShowMenu"); Advance(1.2f);
        Require(Text("CurrentMusicCue") == "music.menu", "菜单音乐未选择正确曲目");
        Call("BeginBattle", "huangjin"); Call("SyncBattle", session); Advance(1.2f);
        Require(Text("CurrentMusicCue") == "music.menu", "备战提前播放了战斗曲");
        session.Start(); Call("SyncBattle", session); Advance(1.2f);
        Require(Text("CurrentMusicCue") == "music.battle.huangjin", "实际开战未切黄巾配乐");
        session.Enemies.Add(new EnemyUnit { Id = 900, Health = 10, Definition = new EnemyDefinition { IsBoss = true } });
        Call("SyncBattle", session); Advance(1.2f);
        Require(Text("CurrentMusicCue") == "music.boss", "Boss集合未驱动压力段");
        AudioSource selected = MusicSources().First(audio => audio.clip != null && audio.volume > 0);
        AudioClip clip = selected.clip;
        session.Enemies.Add(new EnemyUnit { Id = 901, Health = 10, Definition = new EnemyDefinition { IsBoss = true } });
        Call("SyncBattle", session); Advance(.1f);
        Require(selected.clip == clip, "追加Boss重启了音乐");
        session.Enemies.Clear(); Call("SyncBattle", session); Advance(1.2f);
        Require(Text("CurrentMusicCue") == "music.battle.huangjin", "最后Boss离场后没有返回关卡曲");
        session.SetSpeed(2); Call("SyncBattle", session);
        Require(MusicSources().All(audio => audio.pitch == 1), "2倍速改变了BGM音高");
    }

    private static void VerifyEvents(GameObject owner, BattleSession session)
    {
        long before = Number("SoundSequence");
        Emit("boss_warning", null, 900);
        long warning = Number("SoundSequence");
        Emit("boss_warning", null, 900); Emit("boss_warning", null, 901);
        Require(warning == before + 1 && Number("SoundSequence") == warning, "同帧预警没有合并");
        Advance(.16f); Emit("boss_warning", null, 900);
        Require(Number("SoundSequence") == warning + 1, "同Boss后续预警被永久屏蔽");
        Advance(.2f); Emit("boss_phase", null, 900);
        long phase = Number("SoundSequence"); Advance(2f); Emit("boss_phase", null, 900);
        Require(Number("SoundSequence") == phase, "同Boss第二阶段重复发声");
        Emit("boss_phase", null, 901);
        Require(Number("SoundSequence") == phase + 1, "不同Boss阶段被错误合并");
        Advance(2f);
        for (int i = 0; i < 100; i++) Emit("attack", "zhou_yu", i);
        long attack = Number("SoundSequence"); Emit("fire", "zhou_yu", 1);
        Require(Number("SoundSequence") == attack, "点燃与同次火攻重复发声");
        for (int i = 0; i < 100; i++) Emit("hit", null, i);
        Require(Number("SoundSequence") <= attack + 1 && owner.GetComponentsInChildren<AudioSource>().Length == 11, "密集伤害造成无限叠声");
        Advance(2f); Emit("recruit", "lv_bu", 0);
        Require(Text("LastSoundCue") == "identity.lv_bu", "吕布专属标识没有替换通用招募声");
        Call("Button"); AudioClip first = owner.GetComponentsInChildren<AudioSource>().Single(audio => audio.outputAudioMixerGroup.name == "UI").clip;
        Advance(.1f); Call("Button"); AudioClip second = owner.GetComponentsInChildren<AudioSource>().Single(audio => audio.outputAudioMixerGroup.name == "UI").clip;
        Require(first != null && second != null && first != second, "点击变体连续重复或未加载真实片段");
    }

    private static void VerifyGates(GameObject owner, BattleSession session)
    {
        Call("SetBattlePaused", true); Advance(.4f);
        Require(Flag("MusicPaused"), "手动暂停没有保留音乐位置");
        long before = Number("SoundSequence"); Emit("hit", null, 1); Call("Button");
        Require(Number("SoundSequence") == before + 1, "暂停应阻止战斗短声并保留UI反馈");
        Call("SetApplicationPaused", true); Call("SetApplicationFocused", false);
        before = Number("SoundSequence"); Call("Button");
        Require(Number("SoundSequence") == before && Flag("SystemSuspended"), "后台仍允许UI出声");
        Call("SetApplicationPaused", false);
        Require(Flag("SystemSuspended"), "暂停通知解除时错误覆盖失焦门");
        Call("SetApplicationFocused", true);
        Require(!Flag("SystemSuspended") && Flag("MusicPaused"), "回前台覆盖了手动暂停");
        Set(null, "MusicEnabled", false); Call("SetBattlePaused", false); Advance(.4f);
        Require(MusicSources().All(audio => audio.volume == 0), "继续战斗覆盖用户音乐静音");
        Set(null, "MusicEnabled", true); Advance(.4f);
        Call("ShowMenu"); Advance(1.2f); Call("SetApplicationFocused", false);
        Require(Flag("MusicPaused"), "菜单失焦未停音");
        Call("SetApplicationFocused", true); Advance(.4f);
        Require(!Flag("MusicPaused"), "菜单回前台未恢复当前曲");
        Call("BeginBattle", "huangjin"); Call("SyncBattle", session); Advance(1.2f);
    }

    private static void VerifyBurstEvents(GameObject owner)
    {
        // 使用与画面相同的聚合器；可控真实时钟与事件实秒分别推进。
        int nextEnemy = 20000; float gameAt = 10;
        var gains = new List<float>();
        foreach (int count in new[] { 3, 6, 10 })
        {
            Call("BeginBattle", "huangjin"); Advance(1);
            var tracker = new BattleFeedbackState(.7f);
            List<BattleEvent> batch = Kills(count, ref nextEnemy, gameAt);
            List<BattleFeedbackResult> results = batch.Select(tracker.Observe).ToList();
            long before = Number("SoundSequence");
            GameAudio.EventBatch(batch, results, gameAt);
            Require(Number("SoundSequence") == before + 1 && Text("LastSoundCue") == "enemy.defeat", "同批连破没有以一次最高档替代普通击退声");
            AudioSource[] battle = owner.GetComponentsInChildren<AudioSource>().Where(audio => audio.outputAudioMixerGroup.name == "Battle").ToArray();
            Require(battle.Count(audio => audio.clip != null) == 1 && battle.Where(audio => audio.clip != null).All(audio => audio.pitch == 1), "连破创建多声部或提前引入分档音高");
            gains.Add(battle.Single(audio => audio.clip != null).volume);
            results = batch.Select(tracker.Observe).ToList();
            GameAudio.EventBatch(batch, results, gameAt);
            Require(Number("SoundSequence") == before + 1, "重复击退ID仍产生批次声音");
            gameAt += 2;
        }
        Require(gains[0] < gains[1] && gains[1] < gains[2], "批次没有采用同批最高档的响度");

        Call("BeginBattle", "huangjin"); Advance(1);
        long sequence = Number("SoundSequence");
        Burst(ref nextEnemy, gameAt);
        Require(Number("SoundSequence") == sequence + 1, "连破限频夹具未成功发出首声");
        Advance(.1f); gameAt += .1f; Burst(ref nextEnemy, gameAt);
        Require(Number("SoundSequence") == sequence + 1, "跨批0.2秒内连破重复发声");
        // Cue仍可保留0.25秒冷却；0.2秒是批次门的下限，不要求更早突破素材冷却。
        Advance(.18f); gameAt += .18f; Burst(ref nextEnemy, gameAt);
        Require(Number("SoundSequence") == sequence + 2, "跨批冷却结束后连破没有恢复");

        foreach (string gate in new[] { "pause", "mute", "background", "terminal" })
        {
            Call("BeginBattle", "huangjin"); Advance(1); gameAt += 2;
            if (gate == "pause") Call("SetBattlePaused", true);
            if (gate == "mute") Set(null, "SoundEnabled", false);
            if (gate == "background") Call("SetApplicationFocused", false);
            if (gate == "terminal") Emit("victory", null, 0);
            sequence = Number("SoundSequence");
            var tracker = new BattleFeedbackState(.7f); List<BattleEvent> batch = Kills(10, ref nextEnemy, gameAt);
            GameAudio.EventBatch(batch, batch.Select(tracker.Observe).ToList(), gameAt);
            Require(Number("SoundSequence") == sequence, "连破突破了" + gate + "门控");
            if (gate == "pause") Call("SetBattlePaused", false);
            if (gate == "mute") Set(null, "SoundEnabled", true);
            if (gate == "background") Call("SetApplicationFocused", true);
            Advance(1); gameAt += 1;
            GameAudio.EventBatch(new List<BattleEvent>(), new List<BattleFeedbackResult>(), gameAt);
            Require(Number("SoundSequence") == sequence, "解除" + gate + "门控后补播了旧连破");
            GameAudio.EventBatch(batch, batch.Select(tracker.Observe).ToList(), gameAt);
            Require(Number("SoundSequence") == sequence, "解除门控后重复ID补播旧连破");
        }

        Call("BeginBattle", "huangjin"); Advance(1); gameAt += 2;
        sequence = Number("SoundSequence");
        var groups = new[] { "guan_yu", "xun_yu", "zhou_yu", "zhuge_liang", "pang_tong", "hua_tuo" };
        for (int i = 0; i < 10; i++)
        {
            foreach (string hero in groups) Emit("attack", hero, 0);
            Emit("hit", null, 0); Burst(ref nextEnemy, gameAt);
            Advance(.09f); gameAt += .09f;
        }
        Require(Number("SoundSequence") - sequence <= 16, "连破绕过每实秒16次普通音预算");
        Require(owner.GetComponentsInChildren<AudioSource>().Length == 11, "批次压力增长了固定11音源");
        object instance = owner.GetComponent<GameAudio>();
        var plays = (Queue<double>)Service.GetField("ordinaryPlays", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        Require(plays.Count <= 16, "普通音预算队列超过16");
        Call("BeginBattle", "huangjin");
        Debug.Log("连破音频批次检查通过：最高档替代、重复ID静音、跨批限频、暂停/静音/后台/终局不补播、11音源和16次普通预算；未进行设备听测");
    }

    private static List<BattleEvent> Kills(int count, ref int nextEnemy, float at)
    {
        var result = new List<BattleEvent>();
        for (int i = 0; i < count; i++) result.Add(new BattleEvent { Kind = "kill", TargetEnemyId = ++nextEnemy, OccurredAt = at, Position = new Point2(800 + i * 5, 500) });
        return result;
    }

    private static void VerifyExpiredShortSounds()
    {
        foreach (string kind in new[] { "attack", "basic_attack", "hit", "fire", "stun", "slow", "faction_hit" })
        {
            foreach (float age in new[] { .25f, 1f })
            {
                Call("BeginBattle", "huangjin"); Advance(1);
                var stale = new BattleEvent { Kind = kind, HeroId = "guan_yu", OccurredAt = 10 - age };
                var fresh = new BattleEvent { Kind = kind, HeroId = "guan_yu", OccurredAt = 10 };
                long before = Number("SoundSequence");
                GameAudio.EventBatch(new[] { stale }, new[] { new BattleFeedbackResult() }, 10);
                Require(Number("SoundSequence") == before, kind + "在" + age + "秒后补播了过期短音");
                // 不推进真实时钟：旧事件若占用了角色、技能组或Cue冷却，新鲜短音会被阻止。
                GameAudio.EventBatch(new[] { fresh }, new[] { new BattleFeedbackResult() }, 10);
                Require(Number("SoundSequence") == before + 1, kind + "旧事件占用了新鲜短音的冷却");
                string expected = kind == "faction_hit" ? "attack.tactic" : kind == "hit" || kind == "basic_attack" ? "combat.hit" : kind == "fire" ? "attack.fire" : kind == "stun" || kind == "slow" ? "attack.control" : "attack.melee";
                Require(Text("LastSoundCue") == expected, kind + "过滤旧事件后没有播放正确的新鲜Cue");
            }
            Call("BeginBattle", "huangjin"); Advance(1);
            long boundary = Number("SoundSequence");
            GameAudio.EventBatch(new[] { new BattleEvent { Kind = kind, HeroId = "guan_yu", OccurredAt = 9.751f } }, new[] { new BattleFeedbackResult() }, 10);
            Require(Number("SoundSequence") == boundary + 1, kind + "未达到0.25秒就被误判过期");
        }
        Call("BeginBattle", "huangjin"); Advance(1);
        long critical = Number("SoundSequence");
        GameAudio.EventBatch(new[] { new BattleEvent { Kind = "boss_warning", TargetEnemyId = 45000, OccurredAt = 0 } }, new[] { new BattleFeedbackResult() }, 10);
        Require(Number("SoundSequence") == critical + 1 && Text("LastSoundCue") == "boss.warning", "短音过期过滤误伤了Boss关键预警");
        Call("SetBattlePaused", true);
        critical = Number("SoundSequence");
        GameAudio.EventBatch(new[] { new BattleEvent { Kind = "boss_warning", TargetEnemyId = 45001, OccurredAt = 0 } }, new[] { new BattleFeedbackResult() }, 10);
        Require(Number("SoundSequence") == critical, "关键预警绕过了原有暂停门控");
        GameAudio.EventBatch(new[] { new BattleEvent { Kind = "defeat", OccurredAt = 0 } }, new[] { new BattleFeedbackResult() }, 10);
        Require(Text("CurrentMusicCue") == "music.defeat", "过期过滤阻止了终局接管音乐");
        Call("BeginBattle", "huangjin");
        Debug.Log("短音寿命检查通过：六种过期短音丢弃、不占新鲜事件冷却、0.25秒边界及Boss/终局原有门控");
    }

    private static void Burst(ref int nextEnemy, float at)
    {
        var tracker = new BattleFeedbackState(.7f); List<BattleEvent> batch = Kills(3, ref nextEnemy, at);
        GameAudio.EventBatch(batch, batch.Select(tracker.Observe).ToList(), at);
    }

    private static void VerifyResult(GameObject owner)
    {
        Emit("boss_warning", null, 500); Emit("leak", null, 501); Emit("defeat", null, 0); Advance(.3f);
        Require(Text("CurrentMusicCue") == "music.defeat", "失败短句未接管音乐");
        Require(owner.GetComponentsInChildren<AudioSource>().Where(audio => audio.outputAudioMixerGroup.name != "Music").All(audio => audio.clip == null), "终局保留了旧预警/普通声");
        long before = Number("SoundSequence"); Emit("defeat", null, 0); Emit("boss_warning", null, 500); Emit("hit", null, 1);
        Require(Number("SoundSequence") == before, "终局之后仍接收旧战斗事件");
        AudioClip expiredResult = MusicSources().Single(audio => audio.clip != null && audio.volume > 0).clip;
        Call("SetApplicationPaused", true); Call("SetApplicationFocused", false);
        Call("SetApplicationPaused", false); Call("SetApplicationFocused", true);
        Require(MusicSources().All(audio => audio.clip != expiredResult), "结算挂起后仍保留过期结果源，恢复时可能补播旧结算");
        Call("BeginBattle", "hulao"); Advance(20f);
        Require(Text("CurrentMusicCue") == "music.menu", "重试后旧结果排程覆盖新备战曲");
        Emit("boss_phase", null, 900);
        Require(Number("SoundSequence") > before, "新会话未清除阶段去重记录");
    }

    private static void VerifyMissingAssets(GameObject owner, GameDatabase database)
    {
        // 仅改变隔离实例的目录，模拟丢失路径；不删除或修改工程素材。
        object catalog = Service.GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner.GetComponent<GameAudio>());
        var sounds = (Dictionary<string, AudioClip[]>)catalog.GetType().GetField("sounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(catalog);
        var cues = (Dictionary<string, AudioCue>)catalog.GetType().GetField("cues", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(catalog);
        sounds.Remove("boss.warning");
        cues["music.battle.hulao"].Paths = new[] { "Audio/Music/missing_test_clip" };
        int warningLogs = 0, musicLogs = 0;
        Application.LogCallback handler = (message, stack, type) =>
        {
            if (!message.StartsWith("音频资源缺失", StringComparison.Ordinal)) return;
            if (message.EndsWith("boss.warning", StringComparison.Ordinal)) warningLogs++;
            if (message.EndsWith("music.battle.hulao", StringComparison.Ordinal)) musicLogs++;
        };
        Application.logMessageReceived += handler;
        try
        {
            Advance(1); long before = Number("SoundSequence");
            Emit("boss_warning", null, 901); Advance(1); Emit("boss_warning", null, 901);
            Require(Number("SoundSequence") == before + 2 && warningLogs == 1, "关键资源缺失未回退，或重复刷缺失日志");
            var level = database.FindLevel("hulao");
            var session = new BattleSession(database, level, level.RecommendedRoster); session.Start();
            Call("SyncBattle", session); Advance(1); Call("SyncBattle", session); Advance(1);
            Require(Text("CurrentMusicCue") == "music.menu" && musicLogs == 1, "音乐路径缺失未保留当前曲，或反复加载刷错");
        }
        finally { Application.logMessageReceived -= handler; }
    }

    private static AudioSource[] MusicSources() { return UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(audio => audio.outputAudioMixerGroup != null && audio.outputAudioMixerGroup.name == "Music").ToArray(); }
    private static void Advance(float delta) { now += delta; object instance = UnityEngine.Object.FindAnyObjectByType<GameAudio>(); instance.GetType().GetMethod("Advance").Invoke(instance, new object[] { delta }); }
    private static void Emit(string kind, string hero, int enemy) { GameAudio.Event(new BattleEvent { Kind = kind, HeroId = hero, TargetEnemyId = enemy }); }
    private static void Call(string name, params object[] arguments) { Service.GetMethods().Single(method => method.Name == name && method.GetParameters().Length == arguments.Length).Invoke(null, arguments); }
    private static object Read(object target, string name) { PropertyInfo property = (target == null ? Service : target.GetType()).GetProperty(name); Require(property != null, "缺少可观测音频属性：" + name); return property.GetValue(target, null); }
    private static void Set(object target, string name, object value) { (target == null ? Service : target.GetType()).GetProperty(name).SetValue(target, value, null); }
    private static string Text(string name) { return (string)Read(null, name); }
    private static bool Flag(string name) { return (bool)Read(null, name); }
    private static long Number(string name) { return Convert.ToInt64(Read(null, name)); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
