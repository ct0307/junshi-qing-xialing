#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 真实结算按钮与折叠详情验收；短路线只用于造局，记录保存在隔离内存。
public static class UiRefinementResultChecks
{
    public static void Run()
    {
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        Verify(database);
    }

    public static void Verify(GameDatabase original)
    {
        Verify(original, null);
    }

    // 由总验收传入本轮输出目录，普通Verify不生成或覆盖图片。
    public static void Capture(GameDatabase original, string output)
    {
        if (string.IsNullOrEmpty(output)) throw new ArgumentException("结算截图需要独立输出目录", "output");
        Directory.CreateDirectory(output);
        Verify(original, output);
    }

    private static void Verify(GameDatabase original, string output)
    {
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        EnemyDefinition boss = database.Enemies.First(enemy => enemy.IsBoss && enemy.CastleDamage == 15);
        Require(level != null && database.Levels.Count > 1, "结算精修夹具缺少首关或下一关");
        level.Name = "结算精修验收";
        level.CastleMaxHealth = 30;
        level.IncomePerSecond = 0;
        level.Routes = new List<RouteDefinition> { new RouteDefinition { Id = "result-route",
            Points = new List<Point2> { new Point2(600, 400), new Point2(610, 400) } } };
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "结算精修短路夹具", StartDelay = 6,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = boss.Id, RouteId = "result-route", Count = 1, Interval = .25f } } } };

        using (var scene = new ResultScene(database))
        {
            int[] speeds = { 2, 1, 2, 3 };
            string[] states = { "首次纪录", "未刷新纪录", "未刷新纪录（与最佳相同）", "刷新纪录" };
            string[] names = { "first", "slower", "equal", "improved" };
            long firstTime = 0;
            for (int run = 0; run < speeds.Length; run++)
            {
                BeginAndFinish(scene, level, speeds[run]);
                BattleSession session = scene.Flow.CurrentSession;
                Require(session.State == BattleState.Victory && session.BossBreachCount == 1,
                    "结算夹具未自然产生保有城防且Boss突破一次的胜利");
                RectTransform result = NamedRect(scene.Root, "战斗结算遮罩");
                CheckSummary(result, level, session);
                CheckButtonColor(NamedButton(result, "结算下一关"), BattlePalette.Jade, "有下一关的胜利应以“下一关”为青玉主按钮");
                CheckButtonColor(NamedButton(result, "结算重新推演"), BattlePalette.Mist, "有下一关的胜利重试应为次级色");
                Require(result.GetComponentsInChildren<Button>().Count(button =>
                    button.GetComponent<Image>() != null && button.GetComponent<Image>().color == BattlePalette.Jade) == 1,
                    "默认胜利结算必须只有一个青玉主操作");

                FastestRecord recent = scene.Flow.Progress.GetRecentRecord(level.Id);
                FastestRecord best = scene.Flow.Progress.GetFastestRecord(level.Id);
                string record = NamedText(result, "结算时间纪录").text;
                Require(record.Contains("本次 " + RecordTime(recent)) && record.Contains("个人最佳 " + RecordTime(best)) &&
                    record.Contains(states[run]) && record.Contains("已保存为最近通关"),
                    "默认结算未完整区分本次、个人最佳、成绩状态与最近通关保存结果");
                if (run == 0) firstTime = best.ElapsedMilliseconds;
                else if (run == 1) Require(recent.ElapsedMilliseconds > firstTime && best.ElapsedMilliseconds == firstTime,
                    "较慢胜利错误覆盖最快快照");
                else if (run == 2) Require(recent.ElapsedMilliseconds == firstTime && best.ElapsedMilliseconds == firstTime,
                    "同速胜利改变最快纪录");
                else Require(best.ElapsedMilliseconds < firstTime && record.Contains("快了"), "更快胜利缺少真实刷新差额");

                CaptureThree(scene, output, "result-" + names[run] + "-default");
                CheckDetails(scene, result, level, session, best, output, "result-" + names[run] + "-details");
            }

            BattleSession victory = scene.Flow.CurrentSession;
            CheckLastLevel(scene, level, victory);
            int saves = scene.Storage.SaveCount, writes = scene.Storage.WriteCount;
            var savedValues = new Dictionary<string, int>(scene.Storage.Values);
            FastestRecord historicalBest = scene.Flow.Progress.GetFastestRecord(level.Id);
            long savedBest = historicalBest.ElapsedMilliseconds;
            level.Waves[0].Groups[0].Count = 2;
            BeginAndFinish(scene, level, 1);
            BattleSession defeat = scene.Flow.CurrentSession;
            RectTransform failed = NamedRect(scene.Root, "战斗结算遮罩");
            Require(defeat.State == BattleState.Defeat && scene.Storage.SaveCount == saves && scene.Storage.WriteCount == writes &&
                scene.Storage.Values.Count == savedValues.Count && savedValues.All(entry =>
                    scene.Storage.Values.ContainsKey(entry.Key) && scene.Storage.Values[entry.Key] == entry.Value) &&
                scene.Flow.Progress.GetFastestRecord(level.Id).ElapsedMilliseconds == savedBest,
                "失败结算改写最快纪录或未自然进入失败");
            CheckSummary(failed, level, defeat);
            CheckButtonColor(NamedButton(failed, "结算重新推演"), BattlePalette.Jade, "失败必须以“重新推演”为青玉主按钮");
            string failedRecord = NamedText(failed, "结算时间纪录").text;
            Require(!failed.GetComponentsInChildren<Button>().Any(button => button.name == "结算下一关") &&
                failedRecord.Contains("未刷新纪录 · 本局未通关"),
                "失败结算出现下一关或丢失纪录保持说明");
            Require(failedRecord.Contains("个人最佳 " + RecordTime(historicalBest)) && !failedRecord.Contains("已保存为最近通关"),
                "失败结算丢失既有个人最佳用时，或把失败误报为已保存最近通关");
            CaptureThree(scene, output, "result-defeat-default");
            CheckDetails(scene, failed, level, defeat, historicalBest, output, "result-defeat-details");
        }
        Debug.Log("结算精修检查通过：胜败主操作、六项真实统计、首次/慢局/同速/刷新状态、详情按钮、最佳快照、突破复盘及会话/存档只读");
    }

    private static void CheckSummary(RectTransform result, LevelDefinition level, BattleSession session)
    {
        string[] names = { "城防", "用时", "击退", "漏兵", "军饷支出", "波次" };
        int seconds = Math.Max(0, (int)Math.Floor(session.ElapsedSeconds));
        string[] values = { session.CastleHealth + " / " + level.CastleMaxHealth,
            seconds / 60 + "分" + (seconds % 60).ToString("00") + "秒", session.DefeatedCount + " 名敌军",
            session.LeakedCount + " 名敌军", session.GoldSpent.ToString(), session.CurrentWave + " / " + session.TotalWaves };
        for (int index = 0; index < names.Length; index++)
        {
            Transform value = result.Find("结算弹窗/统计" + names[index] + "/统计数值");
            Require(value != null && value.gameObject.activeInHierarchy && value.GetComponent<Text>().text == values[index],
                "默认结算统计与本局不符：" + names[index]);
        }
        Require(NamedText(result, "结算标题").text == (session.State == BattleState.Victory ? "守关大捷" : "此局失守"),
            "默认结算结果不清楚");
        Require(!result.GetComponentsInChildren<Text>().Any(text => text.name == "最佳纪录快照" || text.name == "结算建议"),
            "默认结算仍把历史快照或突破复盘全部铺开");
        CheckTypography(result, "默认结算");
    }

    private static void CheckDetails(ResultScene scene, RectTransform result, LevelDefinition level,
        BattleSession session, FastestRecord best, string output, string imageName)
    {
        Button toggle = NamedButton(result, "结算详情开关");
        RectTransform detail = NamedRect(result, "结算详情区域", true);
        Require(!detail.gameObject.activeInHierarchy && toggle.interactable, "结算详情应默认折叠且有可用真实入口");
        RectTransform toggleRect = (RectTransform)toggle.transform;
        Require(toggleRect.rect.width >= 104 && toggleRect.rect.height >= 88, "结算详情入口未达到最小触控热区");
        var values = new Dictionary<string, int>(scene.Storage.Values);
        int writes = scene.Storage.WriteCount, saves = scene.Storage.SaveCount;
        double elapsed = session.ElapsedSeconds;
        float time = session.Time, march = session.MarchTime, speed = session.Speed;
        int gold = session.Gold, spent = session.GoldSpent, health = session.CastleHealth;
        BattleState state = session.State;
        BreachRecord breach = session.LastBreach;

        toggle.onClick.Invoke();
        Require(detail.gameObject.activeInHierarchy, "真实详情按钮未展开详情");
        Text snapshot = NamedText(detail, "最佳纪录快照");
        if (best == null) Require(snapshot.text == "个人最佳：—", "没有本次纪录结果时详情伪造了最佳快照");
        else Require(snapshot.text.Contains("城防 " + best.CastleHp + " / " + level.CastleMaxHealth) &&
            snapshot.text.Contains("漏兵 " + best.LeakedCount) && snapshot.text.Contains("Boss突破 " + best.BossBreachCount) &&
            snapshot.text.Contains(best.SpeedSummary), "详情最佳快照并非同一次历史最佳数据");
        Require(breach != null && NamedText(detail, "结算建议").text.Contains("最后突破：" + breach.EnemyName) &&
            NamedText(detail, "结算建议").text.Contains("路线1") && NamedText(detail, "结算建议").text.Contains("剩余生命") &&
            NamedText(detail, "结算建议标签").text.Contains("本局Boss突破 " + session.BossBreachCount),
            "详情丢失本局真实突破敌军、路线、剩余生命或Boss次数");
        CheckTypography(result, "展开结算详情");
        CaptureThree(scene, output, imageName);
        scene.Flow.Tick(5);
        toggle.onClick.Invoke();
        Require(!detail.gameObject.activeInHierarchy && result.gameObject.activeInHierarchy &&
            scene.Flow.CurrentSession == session && NamedRect(scene.Root, "战斗结算遮罩") == result,
            "关闭详情没有恢复同一结算与同一会话");
        Require(session.State == state && session.ElapsedSeconds == elapsed && session.Time == time && session.MarchTime == march &&
            session.Speed == speed && session.Gold == gold && session.GoldSpent == spent && session.CastleHealth == health &&
            object.ReferenceEquals(session.LastBreach, breach), "阅读详情改变了终局会话状态");
        Require(scene.Storage.WriteCount == writes && scene.Storage.SaveCount == saves && scene.Storage.Values.Count == values.Count &&
            values.All(entry => scene.Storage.Values.ContainsKey(entry.Key) && scene.Storage.Values[entry.Key] == entry.Value),
            "展开或关闭结算详情重写了最快、最近或其他玩家记录");
        CheckSummary(result, level, session);
    }

    private static void CheckLastLevel(ResultScene scene, LevelDefinition level, BattleSession victory)
    {
        RectTransform sample = scene.Ui.Rect(scene.Root, "末关结算夹具", 0, 0, 1920, 1080);
        try
        {
            int retries = 0, rosters = 0, returns = 0;
            RectTransform result = BattleResultView.Create(scene.Ui, sample, level, victory,
                () => retries++, () => rosters++, null, () => returns++);
            CheckButtonColor(NamedButton(result, "结算重新推演"), BattlePalette.Jade, "末关胜利应保留重新推演主操作");
            Require(!result.GetComponentsInChildren<Button>().Any(button => button.name == "结算下一关"), "末关仍显示下一关");
            NamedButton(result, "结算重新推演").onClick.Invoke();
            NamedButton(result, "结算重新选将").onClick.Invoke();
            NamedButton(result, "结算返回关卡").onClick.Invoke();
            Require(retries == 1 && rosters == 1 && returns == 1, "结算原有导航回调发生变化");
        }
        finally { UnityEngine.Object.DestroyImmediate(sample.gameObject); }
    }

    private static void BeginAndFinish(ResultScene scene, LevelDefinition level, int speed)
    {
        scene.Flow.BeginBattle(level, new List<string> { "zhao_yun" });
        scene.Flow.CurrentSession.SetSpeed(speed);
        Button start = scene.Root.GetComponentsInChildren<Button>().First(button =>
            button.GetComponentInChildren<Text>() != null && button.GetComponentInChildren<Text>().text == "开战");
        Require(start.interactable, "夹具开战按钮不可用");
        start.onClick.Invoke();
        for (int tick = 0; tick < 4000 && scene.Flow.CurrentSession.State == BattleState.Running; tick++) scene.Flow.Tick(.05f);
        Require(scene.Flow.CurrentSession.State != BattleState.Running, "结算精修夹具未进入终局");
    }

    private static void CheckButtonColor(Button button, Color expected, string message)
    { Require(button.interactable && button.GetComponent<Image>().color == expected, message); }

    private static void CheckTypography(RectTransform root, string description)
    {
        Canvas.ForceUpdateCanvases();
        foreach (Text text in root.GetComponentsInChildren<Text>())
            if (!string.IsNullOrWhiteSpace(text.text)) UiTypographyChecks.Check(text, description + text.name);
    }

    private static void CaptureThree(ResultScene scene, string output, string name)
    {
        if (output == null) return;
        Capture(scene, output, name + "-1280", 1280, 720);
        Capture(scene, output, name + "-1920", 1920, 1080);
        Capture(scene, output, name + "-2400", 2400, 1080);
    }

    private static void Capture(ResultScene scene, string output, string name, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            scene.Camera.targetTexture = target;
            scene.Camera.aspect = (float)width / height;
            CheckTypography(NamedRect(scene.Root, "战斗结算遮罩"), name);
            scene.Camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            scene.Camera.targetTexture = null;
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static string RecordTime(FastestRecord record)
    { return BattleResultView.FormatRecordTime(record.ElapsedMilliseconds / 1000.0); }

    private static Text NamedText(RectTransform root, string name)
    { return root.GetComponentsInChildren<Text>().First(text => text.name == name); }

    private static Button NamedButton(RectTransform root, string name)
    { return root.GetComponentsInChildren<Button>().First(button => button.name == name); }

    private static RectTransform NamedRect(RectTransform root, string name, bool includeInactive = false)
    { return root.GetComponentsInChildren<RectTransform>(includeInactive).First(rect => rect.name == name); }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class MemoryStorage
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public int WriteCount;
        public int SaveCount;
        public GameProgress Open()
        {
            return new GameProgress((key, fallback) => { int value; return Values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => { Values[key] = value; WriteCount++; }, () => SaveCount++);
        }
    }

    private sealed class ResultScene : IDisposable
    {
        private readonly GameObject canvasObject;
        public readonly UiFactory Ui;
        public readonly RectTransform Root;
        public readonly Camera Camera;
        public readonly MemoryStorage Storage = new MemoryStorage();
        public readonly GameFlow Flow;

        public ResultScene(GameDatabase database)
        {
            canvasObject = new GameObject("结算精修隔离UI", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.pivot = new Vector2(0, 1);
            canvasRect.sizeDelta = new Vector2(1920, 1080);
            Camera = new GameObject("结算精修截图相机", typeof(Camera)).GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = 540;
            Camera.transform.position = new Vector3(960, -540, -100);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = UiFactory.Deep;
            Camera.nearClipPlane = .1f;
            Camera.farClipPlane = 200;
            canvas.worldCamera = Camera;
            Ui = new UiFactory();
            Root = Ui.Rect(canvasRect, "结算精修界面", 0, 0, 1920, 1080);
            Flow = new GameFlow(Ui, Root, database, Storage.Open());
        }

        public void Dispose()
        {
            try { Flow.Dispose(); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(Camera.gameObject);
            }
        }
    }
}
#endif
