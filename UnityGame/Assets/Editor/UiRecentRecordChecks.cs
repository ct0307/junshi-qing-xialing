#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 复制已核验的旧版快照到内存，所有新成绩均由隔离会话产生，不接触玩家偏好。
public static class UiRecentRecordChecks
{
    private static readonly string Output = Path.GetFullPath("../docs/ui/runtime-recent-records");

    public static void Run(GameDatabase original)
    {
        Directory.CreateDirectory(Output);
        var storage = new MemoryStorage();
        SeedPreviousRule(storage);
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        using (var scene = new RecordScene(database, storage))
        {
            scene.Flow.ShowLevels();
            CheckMenu(scene, level, null, null);
            Require(LevelText(scene.Root, level, "关卡旧版最快").text == "旧版最快 04:50.035" && storage.SaveCount == 0,
                "旧版成绩未保留展示，或读取旧档时伪造了最近通关");
            CaptureThree(scene, "levels-previous-rule");
        }

        // 极短路线只用于产生首次、较慢、同速与更快的合法胜利，不代表正式关卡时长。
        EnemyDefinition boss = database.Enemies.First(enemy => enemy.IsBoss && enemy.CastleDamage == 15);
        level.Name = "最近通关验收";
        level.CastleMaxHealth = 30; level.IncomePerSecond = 0;
        level.Routes = new List<RouteDefinition> { new RouteDefinition { Id = "recent-route",
            Points = new List<Point2> { new Point2(600, 400), new Point2(610, 400) } } };
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "最近通关短路夹具", StartDelay = 6,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = boss.Id, RouteId = "recent-route", Count = 1, Interval = .25f } } } };
        using (var scene = new RecordScene(database, storage))
        {
            int[] speeds = { 2, 1, 2, 3 };
            string[] names = { "first", "slower", "equal", "improved" };
            FastestRecord first = null, last = null;
            BattleSession oldSession = null;
            for (int run = 0; run < speeds.Length; run++)
            {
                scene.Flow.BeginBattle(level, new List<string> { "zhao_yun" });
                int savesBefore = storage.SaveCount;
                if (oldSession != null) scene.Flow.CompleteBattle(oldSession);
                Require(storage.SaveCount == savesBefore, "旧会话写入了新一局最近通关");
                BattleSession session = scene.Flow.CurrentSession;
                session.SetSpeed(speeds[run]);
                ClickText(scene.Root, "开战");
                Finish(scene);
                Require(session.State == BattleState.Victory && storage.SaveCount == savesBefore + 1,
                    "合法胜利未保存一次完整最近通关");
                FastestRecord recent = scene.Flow.Progress.GetRecentRecord(level.Id);
                FastestRecord fastest = scene.Flow.Progress.GetFastestRecord(level.Id);
                Require(recent != null && recent.ElapsedMilliseconds == (long)Math.Round(session.ElapsedSeconds * 1000,
                    MidpointRounding.AwayFromZero) && recent.SpeedMask == 1 << (speeds[run] - 1) &&
                    recent.CastleHp == session.CastleHealth && recent.LeakedCount == session.LeakedCount &&
                    recent.BossBreachCount == session.BossBreachCount, "最近通关不是当前胜利的完整快照");
                string result = NamedText(scene.Root, "结算时间纪录").text;
                Require(result.Contains("本次 " + Time(recent)) && result.Contains("个人最佳 " + Time(fastest)) &&
                    result.Contains("已保存为最近通关"), "结算未区分本次、最佳与最近通关保存结果");
                if (run == 0)
                {
                    first = fastest;
                    Require(result.Contains("首次纪录"), "本版首次胜利被当成旧版刷新");
                    CheckOptionalRecordResult(scene, level, session);
                }
                else if (run == 1)
                    Require(recent.ElapsedMilliseconds > first.ElapsedMilliseconds && fastest.ElapsedMilliseconds == first.ElapsedMilliseconds &&
                        result.Contains("未刷新纪录"), "较慢胜利未保存最近或错误刷新最快");
                else if (run == 2)
                    Require(recent.ElapsedMilliseconds == first.ElapsedMilliseconds && result.Contains("未刷新纪录（与最佳相同）"),
                        "相同成绩未明确说明最快保持");
                else Require(fastest.ElapsedMilliseconds < first.ElapsedMilliseconds && result.Contains("刷新纪录") && result.Contains("快了"),
                    "更快胜利未明确说明刷新");
                CaptureThree(scene, "result-" + names[run]);
                int writes = storage.WriteCount;
                scene.Flow.CompleteBattle(session); scene.Flow.Tick(1);
                Require(storage.WriteCount == writes && storage.SaveCount == savesBefore + 1,
                    "重复终局重写最近通关");
                NamedButton(scene.Root, "结算返回关卡").onClick.Invoke();
                Require(scene.Flow.PageKind == "Levels", "返回关卡按钮未执行真实导航");
                CheckMenu(scene, level, recent, fastest);
                CaptureThree(scene, "levels-" + names[run]);
                Require(scene.Flow.Progress.GetFastestRecord(level.Id, GameProgress.PreviousFastestRuleVersion).ElapsedMilliseconds == 290035 &&
                    scene.Flow.Progress.GetBestCastle(level.Id) == 30, "本版胜利覆盖旧版最快或旧最佳城防");
                last = recent; oldSession = session;
            }

            int saves = storage.SaveCount;
            scene.RebuildFlow(); scene.Flow.ShowLevels();
            CheckMenu(scene, level, last, last);
            Require(storage.SaveCount == saves, "重建流程读取记录时发生写入");
            CaptureThree(scene, "levels-reopened");

            level.Waves[0].Groups[0].Count = 2;
            scene.Flow.BeginBattle(level, new List<string> { "zhao_yun" });
            ClickText(scene.Root, "开战"); Finish(scene);
            Require(scene.Flow.CurrentSession.State == BattleState.Defeat && storage.SaveCount == saves &&
                scene.Flow.Progress.GetRecentRecord(level.Id).ElapsedMilliseconds == last.ElapsedMilliseconds &&
                scene.Flow.Progress.GetFastestRecord(level.Id).ElapsedMilliseconds == last.ElapsedMilliseconds,
                "失败覆盖了最近通关或本版最快");
            Require(NamedText(scene.Root, "结算时间纪录").text.Contains("未刷新纪录 · 本局未通关"),
                "失败结算未明确说明记录保持");
            CaptureThree(scene, "result-defeat-unchanged");
        }
        Debug.Log("最近通关真实流程检查通过：旧版快照保留、首次/慢局/相同/快局、返回关卡、重建流程、重复终局/旧会话/失败不误写及三尺寸裁切");
    }

    private static void SeedPreviousRule(MemoryStorage storage)
    {
        string prefix = "yanyi.fastest." + GameProgress.PreviousFastestRuleVersion + ".huangjin.";
        string[] fields = { "ready", "msHigh", "msLow", "mask", "castle", "leaks", "bosses", "checksum" };
        int[] values = { 1, 0, 290035, 7, 20, 6, 0, -526637781 };
        for (int index = 0; index < fields.Length; index++) storage.Values[prefix + fields[index]] = values[index];
        storage.Values["yanyi.clear.huangjin"] = 1; storage.Values["yanyi.best.huangjin"] = 30;
    }

    private static void CheckMenu(RecordScene scene, LevelDefinition level, FastestRecord recent, FastestRecord fastest)
    {
        Require(LevelText(scene.Root, level, "关卡最近").text == "最近通关 " + Time(recent), "选关没有显示当前最近通关");
        Require(LevelText(scene.Root, level, "关卡最快").text == "本版最快 " + Time(fastest), "选关本版最快被最近或旧版替换");
        Require(LevelText(scene.Root, level, "关卡旧版最快").text == "旧版最快 04:50.035", "选关丢失旧版最快");
    }

    private static void CheckOptionalRecordResult(RecordScene scene, LevelDefinition level, BattleSession session)
    {
        var ui = new UiFactory();
        RectTransform sample = ui.Rect(scene.Root, "兼容结算参数夹具", 0, 0, 1920, 1080);
        try
        {
            BattleResultView.Create(ui, sample, level, session, () => { }, () => { }, null, () => { });
            string text = NamedText(sample, "结算时间纪录").text;
            Require(text.Contains("未提供本次纪录结果") && !text.Contains("无效"),
                "省略可选纪录参数时把合法会话时间误报为无效");
            foreach (Text label in sample.GetComponentsInChildren<Text>()) UiTypographyChecks.Check(label, "兼容结算" + label.name);
        }
        finally { UnityEngine.Object.DestroyImmediate(sample.gameObject); }
    }

    private static string Time(FastestRecord record)
    { return record == null ? "—" : BattleResultView.FormatRecordTime(record.ElapsedMilliseconds / 1000.0); }

    private static Text LevelText(RectTransform root, LevelDefinition level, string name)
    {
        Text result = root.GetComponentsInChildren<Text>().FirstOrDefault(text => text.name == name &&
            text.transform.parent.GetComponentsInChildren<Text>().Any(sibling => sibling.text == level.Name));
        Require(result != null, "关卡缺少记录字段：" + name);
        return result;
    }

    private static Text NamedText(RectTransform root, string name)
    { return root.GetComponentsInChildren<Text>().First(text => text.name == name); }

    private static Button NamedButton(RectTransform root, string name)
    { return root.GetComponentsInChildren<Button>().First(button => button.name == name); }

    private static void ClickText(RectTransform root, string text)
    {
        root.GetComponentsInChildren<Button>().First(button => button.GetComponentInChildren<Text>() != null &&
            button.GetComponentInChildren<Text>().text == text).onClick.Invoke();
    }

    private static void Finish(RecordScene scene)
    {
        for (int step = 0; step < 4000 && scene.Flow.CurrentSession.State == BattleState.Running; step++) scene.Flow.Tick(.05f);
        Require(scene.Flow.CurrentSession.State != BattleState.Running, "最近通关夹具未进入终局");
    }

    private static void CaptureThree(RecordScene scene, string name)
    {
        Capture(scene, name + "-1280", 1280, 720); Capture(scene, name + "-1920", 1920, 1080);
        Capture(scene, name + "-2400", 2400, 1080);
    }

    private static void Capture(RecordScene scene, string name, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active; Texture2D pixels = null;
        try
        {
            scene.Camera.targetTexture = target; scene.Camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            foreach (Text text in scene.Root.GetComponentsInChildren<Text>())
                if (!string.IsNullOrWhiteSpace(text.text))
                    UiTypographyChecks.Check(text, "最近通关" + text.name, text.name.StartsWith("关卡", StringComparison.Ordinal));
            scene.Camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            scene.Camera.targetTexture = null; RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class MemoryStorage
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public int SaveCount;
        public int WriteCount;
        public GameProgress Open()
        {
            return new GameProgress((key, fallback) => { int value; return Values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => { Values[key] = value; WriteCount++; }, () => SaveCount++);
        }
    }

    private sealed class RecordScene : IDisposable
    {
        public readonly RectTransform Root;
        public readonly Camera Camera;
        public GameFlow Flow;
        private readonly GameDatabase database;
        private readonly MemoryStorage storage;
        private readonly UiFactory ui;
        private readonly GameObject canvasObject;

        public RecordScene(GameDatabase database, MemoryStorage storage)
        {
            this.database = database; this.storage = storage;
            canvasObject = new GameObject("最近通关隔离UI", typeof(RectTransform), typeof(Canvas));
            Camera = new GameObject("最近通关截图相机", typeof(Camera)).GetComponent<Camera>();
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvas.transform; rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(1920, 1080);
            Camera.orthographic = true; Camera.orthographicSize = 540; Camera.transform.position = new Vector3(960, -540, -100);
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = UiFactory.Deep;
            Camera.nearClipPlane = .1f; Camera.farClipPlane = 200; canvas.worldCamera = Camera;
            ui = new UiFactory(); Root = ui.Rect(rect, "最近通关界面", 0, 0, 1920, 1080);
            Flow = new GameFlow(ui, Root, database, storage.Open());
        }

        public void RebuildFlow()
        {
            Flow.Dispose(); Flow = new GameFlow(ui, Root, database, storage.Open());
        }

        public void Dispose()
        {
            Flow.Dispose(); UnityEngine.Object.DestroyImmediate(canvasObject); UnityEngine.Object.DestroyImmediate(Camera.gameObject);
        }
    }
}
#endif
