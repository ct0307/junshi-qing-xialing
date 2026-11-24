using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 复现玩家反馈：可见返回入口、继续布阵不丢状态，以及致命命中的受击反馈。
public static class UiFeedbackChecks
{
    private static readonly string Output = Path.GetFullPath("../docs/ui/runtime-feedback");

    public static void Run()
    {
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        Verify(database);
    }

    public static void Verify(GameDatabase database)
    {
        Directory.CreateDirectory(Output);
        VerifyVisibleBack(database);
        VerifyLethalHit(database);
        Debug.Log("玩家反馈回归通过：可见返回、保留布阵、重新选将、返回关卡、战斗返回暂停、致命命中闪亮与退场、三尺寸截图；存档全部隔离");
    }

    private static void VerifyVisibleBack(GameDatabase database)
    {
        using (var scene = new Scene(database))
        {
            LevelDefinition level = database.FindLevel("huangjin");
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            BattleSession session = scene.Flow.CurrentSession;
            Button back = NamedButton(scene.Root, "战场返回");
            Require(back != null && back.interactable && back.GetComponentInChildren<Text>().text == "返回",
                "准备阶段缺少真实可点击的返回按钮");
            HeroDefinition hero = database.FindHero(level.RecommendedRoster[0]);
            SelectHero(scene.Root, hero.Name);
            ClickSlot(scene.Root, level, level.Slots[0].Id);
            HeroUnit unit = session.Heroes.Single();
            int gold = session.Gold, spent = session.GoldSpent;
            CaptureThree(scene, "preparation-visible-back");
            back.onClick.Invoke();
            Require(HasText(scene.Root, "离开布阵"), "可见返回没有复用备战离开确认");
            CaptureThree(scene, "preparation-exit");
            Click(scene.Root, "继续布阵");
            Require(scene.Flow.CurrentSession == session && session.State == BattleState.Preparation &&
                session.Heroes.Single() == unit && session.Gold == gold && session.GoldSpent == spent && session.Time == 0,
                "继续布阵丢失了原守将、军饷或备战状态");
            NamedButton(scene.Root, "战场返回").onClick.Invoke();
            Click(scene.Root, "重新选将");
            Require(scene.Flow.PageKind == "Roster" && scene.Flow.CurrentSession == null, "返回后的重新选将未结束备战");
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            NamedButton(scene.Root, "战场返回").onClick.Invoke();
            Click(scene.Root, "返回关卡");
            Require(scene.Flow.PageKind == "Levels" && scene.Flow.CurrentSession == null, "备战返回关卡残留会话");
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            Click(scene.Root, "开战");
            scene.Flow.Tick(.5f);
            session = scene.Flow.CurrentSession;
            double elapsed = session.ElapsedSeconds;
            NamedButton(scene.Root, "战场返回").onClick.Invoke();
            Require(session.State == BattleState.Paused && HasText(scene.Root, "推演暂停"), "运行时返回应进入暂停菜单");
            scene.Flow.Tick(2);
            Require(session.ElapsedSeconds == elapsed, "返回菜单仍累计通关时间");
            CaptureThree(scene, "running-back-paused");
            Click(scene.Root, "继续守关");
            Require(scene.Flow.CurrentSession == session && session.State == BattleState.Running, "继续守关没有恢复原会话");
            Require(scene.Values.Count == 0, "导航回归误写了玩家通关纪录");
        }
    }

    private static void VerifyLethalHit(GameDatabase original)
    {
        var database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        level.Id = "feedback_fixture";
        level.Name = "命中反馈验证";
        level.InitialGold = 1000;
        level.Routes = new List<RouteDefinition>
        {
            new RouteDefinition { Id = "near", Points = new List<Point2> { new Point2(810, 510), new Point2(1220, 510) } },
            new RouteDefinition { Id = "far", Points = new List<Point2> { new Point2(1600, 680), new Point2(1800, 680) } }
        };
        level.Slots = new List<SlotDefinition> { new SlotDefinition { Id = 0, Position = new Point2(700, 500) } };
        level.Waves = new List<WaveDefinition>
        {
            new WaveDefinition { Name = "命中验证", StartDelay = 0, Groups = new List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "infantry", RouteId = "near", Count = 1, Interval = 1 },
                new SpawnGroup { EnemyId = "infantry", RouteId = "far", Count = 1, Interval = 1 }
            } }
        };
        database.FindEnemy("infantry").MaxHealth = 1;
        database.FindEnemy("infantry").Speed = .01f;
        using (var scene = new Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "huang_zhong" });
            Click(scene.Root, "开战");
            scene.Flow.Tick(.1f);
            Transform units = scene.Root.GetComponentsInChildren<RectTransform>().First(t => t.name == "棋兵");
            Transform target = units.Cast<Transform>().First(t => t.name == "敌军" && ((RectTransform)t).anchoredPosition.x < 1000);
            Image body = target.Find("人物").GetComponent<Image>();
            Require(body.sprite != null && body.color.b > .99f, "命中夹具缺少正常敌军形象");
            SelectHero(scene.Root, "黄忠");
            ClickSlot(scene.Root, level, 0);
            scene.Flow.Tick(1f / 30f);
            Require(scene.Flow.CurrentSession.DefeatedCount == 1 && scene.Flow.CurrentSession.State == BattleState.Running,
                "命中夹具未自然击退目标，或远处敌军未维持运行");
            Require(target.gameObject.activeSelf && body.color.b < .9f && target.GetComponent<CanvasGroup>().alpha > .99f,
                "致命命中被退场同步吞掉，敌军未先显示受击亮色");
            CaptureThree(scene, "lethal-hit-flash-fixture");
            scene.Flow.Tick(.1f);
            Require(target.GetComponent<CanvasGroup>().alpha > 0 && target.GetComponent<CanvasGroup>().alpha < 1,
                "致命命中没有进入短暂淡出");
            float alpha = target.GetComponent<CanvasGroup>().alpha;
            NamedButton(scene.Root, "战场返回").onClick.Invoke();
            scene.Flow.Tick(2);
            Require(target.GetComponent<CanvasGroup>().alpha == alpha, "暂停时退场动画仍推进");
            Click(scene.Root, "继续守关");
            scene.Flow.Tick(.2f);
            Require(!target.gameObject.activeSelf, "退场结束后敌军未回池隐藏");
        }
    }

    private static Button NamedButton(RectTransform root, string name)
    { return root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == name); }

    private static bool HasText(RectTransform root, string text)
    { return root.GetComponentsInChildren<Text>().Any(label => label.text == text); }

    private static void Click(RectTransform root, string prefix)
    {
        Button button = root.GetComponentsInChildren<Button>().First(item => item.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal));
        Require(button.interactable, "测试按钮不可用：" + prefix);
        button.onClick.Invoke();
    }

    private static void ClickSlot(RectTransform root, LevelDefinition level, int slotId)
    {
        var ground = root.GetComponentsInChildren<RectTransform>().First(t => t.name == "可交互阵位");
        int index = level.Slots.FindIndex(slot => slot.Id == slotId);
        Require(index >= 0, "没有找到目标阵位：" + slotId);
        InvokeRebuilding(ground.GetChild(index).GetComponent<Button>(), root);
    }

    private static void SelectHero(RectTransform root, string name)
    {
        Button card = root.GetComponentsInChildren<Button>().First(button => button.name == "战斗武将卡" &&
            button.GetComponentsInChildren<Text>().Any(label => label.text == name));
        InvokeRebuilding(card, root);
    }

    private static void InvokeRebuilding(Button button, RectTransform root)
    {
        var callback = button.onClick;
        Transform cards = root.GetComponentsInChildren<RectTransform>().First(t => t.name == "五张武将卡");
        for (int i = cards.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(cards.GetChild(i).gameObject);
        callback.Invoke();
    }

    private static void CaptureThree(Scene scene, string name)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
        {
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene.Camera.targetTexture = target;
                scene.Camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases();
                foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                    if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "玩家反馈" + label.name);
                scene.Camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + "-" + size.x + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                scene.Camera.targetTexture = null;
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Scene : IDisposable
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public readonly Camera Camera;
        public readonly RectTransform Root;
        public readonly GameFlow Flow;
        private readonly GameObject canvasObject;

        public Scene(GameDatabase database)
        {
            canvasObject = new GameObject("玩家反馈验证", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvasObject.transform;
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(1920, 1080);
            Camera = new GameObject("玩家反馈相机", typeof(Camera)).GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = 540;
            Camera.transform.position = new Vector3(960, -540, -100);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = UiFactory.Deep;
            Camera.nearClipPlane = .1f;
            Camera.farClipPlane = 200;
            canvas.worldCamera = Camera;
            var ui = new UiFactory();
            Root = ui.Rect(rect, "玩家反馈逻辑画布", 0, 0, 1920, 1080);
            var progress = new GameProgress((key, fallback) => { int value; return Values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => Values[key] = value, () => { });
            Flow = new GameFlow(ui, Root, database, progress);
        }

        public void Dispose()
        {
            Flow.Dispose();
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(Camera.gameObject);
        }
    }
}
