using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 短路线隔离夹具经过真实GameFlow、BattleView和uGUI，不代表实际关卡难度。
public static class UiBossBreachChecks
{
    private static string Output = Path.GetFullPath("../docs/ui/runtime-boss-breach");

    public static void Verify(GameDatabase database, string output)
    {
        string previous = Output;
        try
        {
            Output = Path.GetFullPath(output);
            Verify(database);
        }
        finally { Output = previous; }
    }

    public static void Verify(GameDatabase original)
    {
        Directory.CreateDirectory(Output);
        VerifyContinuedBattle(original);
        VerifyFinalBossVictory(original);
        VerifyFatalBreach(original, false);
        VerifyFatalBreach(original, true);
        VerifyIntel(original);
        Debug.Log("Boss突破UI验证通过：首次抵城继续、真实暂停与补阵、末波容错胜利、第二次及低城防归零失败；三尺寸字形与裁切检查完成。隔离夹具不代表实际关卡难度。");
    }

    private static void VerifyContinuedBattle(GameDatabase original)
    {
        GameDatabase database = Fixture(original, "continue", 30, false);
        LevelDefinition level = database.FindLevel("huangjin");
        using (var scene = new BreachScene(database))
        {
            scene.Begin(level);
            AdvanceToLeak(scene, 1);
            BattleSession session = scene.Flow.CurrentSession;
            Require(session.State == BattleState.Running, "首次Boss突破后应继续运行，实际为" + session.State);
            Require(session.CastleHealth == 15, "满城防30首次Boss突破应剩15，实际为" + session.CastleHealth);
            Require(HasText(scene.Root, "15 / 30") && HasText(scene.Root, "低城防"), "自然突破后城防HUD未即时显示真实余量或低城防提示");
            VerifyNoKillReward(session, level);
            VerifyRecord(session, database);
            Require(HasText(scene.Root, "城防 -15") && HasText(scene.Root, "继续守关"), "突破军令缺少真实损失或继续守关提示");
            Require(!scene.Root.GetComponentsInChildren<Text>().Any(t => t.name == "结算标题"), "非致命突破错误弹出结算");
            CaptureThree(scene, "first-breach-continues");

            Button pause = scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "战场主操作");
            Require(pause.interactable, "突破后真实暂停按钮不可操作");
            pause.onClick.Invoke();
            Require(session.State == BattleState.Paused, "突破后真实暂停按钮不可用");
            float pausedTime = session.Time;
            int pausedGold = session.Gold;
            scene.Flow.Tick(2);
            Require(session.Time == pausedTime && session.Gold == pausedGold, "突破后暂停未冻结战斗与经济");
            CaptureThree(scene, "after-breach-paused");
            Click(scene.Root, "继续守关");
            Require(session.State == BattleState.Running, "突破后无法通过真实按钮继续守关");

            RectTransform cards = (RectTransform)scene.Root.Find("页面/五张武将卡");
            Button card = cards.GetComponentsInChildren<Button>().First(b => b.GetComponentsInChildren<Text>().Any(t => t.text == "吕布"));
            InvokeRebuilding(card, cards);
            Button slot = scene.Root.Find("页面/沙盘视口/战场坐标层/可交互阵位").GetChild(0).GetComponent<Button>();
            InvokeRebuilding(slot, cards);
            Require(session.IsRecruited("lv_bu") && session.GoldSpent == database.FindHero("lv_bu").Cost,
                "突破后无法通过真实卡片与阵位继续招募");
            int before = session.Gold;
            int cost = session.GetUpgradeCost(0);
            Click(scene.Root, "升级 ");
            Require(session.Heroes[0].Level == 2 && session.Gold == before - cost, "突破后真实升级操作失败");
            CaptureThree(scene, "after-breach-reinforced");
        }
    }

    private static void VerifyFinalBossVictory(GameDatabase original)
    {
        GameDatabase database = Fixture(original, "final", 30, false);
        LevelDefinition level = database.FindLevel("huangjin");
        level.Waves.RemoveAt(1);
        using (var scene = new BreachScene(database))
        {
            scene.Begin(level);
            AdvanceToLeak(scene, 1);
            BattleSession session = scene.Flow.CurrentSession;
            Require(session.State == BattleState.Victory && session.CastleHealth == 15, "末波Boss突破且城防存活应胜利");
            VerifyNoKillReward(session, level);
            VerifyRecord(session, database);
            Require(HasText(scene.Root, "守关大捷") && HasText(scene.Root, "城防仍存"), "容错胜利结算应说明城防仍存");
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "结算详情开关").onClick.Invoke();
            Require(HasText(scene.Root, "最后突破：张辽") && HasText(scene.Root, "剩余生命"), "胜利后仍须保留真实突破复盘");
            CaptureThree(scene, "final-breach-victory");
        }
    }

    private static void VerifyFatalBreach(GameDatabase original, bool lowCastle)
    {
        GameDatabase database = Fixture(original, lowCastle ? "low" : "second", lowCastle ? 13 : 30, true);
        LevelDefinition level = database.FindLevel("huangjin");
        if (lowCastle) level.Waves[0].Groups[0].Count = 1;
        using (var scene = new BreachScene(database))
        {
            scene.Begin(level);
            AdvanceToLeak(scene, lowCastle ? 1 : 2);
            BattleSession session = scene.Flow.CurrentSession;
            Require(session.State == BattleState.Defeat && session.CastleHealth == 0,
                (lowCastle ? "低城防" : "第二次") + "Boss突破应将城防归零并失败");
            VerifyNoKillReward(session, level);
            VerifyRecord(session, database);
            Require(HasText(scene.Root, "此局失守") && HasText(scene.Root, session.DefeatReason), "归零失败结算缺少真实原因");
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "结算详情开关").onClick.Invoke();
            Require(HasText(scene.Root, "最后突破：张辽") && HasText(scene.Root, "剩余生命"), "失败结算缺少真实突破复盘");
            CaptureThree(scene, lowCastle ? "low-castle-defeat" : "second-breach-defeat");
        }
    }

    private static void VerifyIntel(GameDatabase database)
    {
        using (var scene = new BreachScene(database))
        {
            foreach (LevelDefinition level in database.Levels)
            {
                scene.Flow.ShowIntel(level);
                foreach (string heroId in level.BossHeroIds)
                {
                    EnemyDefinition enemy = database.Enemies.Find(e => e.IsBoss && e.CharacterId == heroId);
                    if (enemy != null)
                        Require(HasText(scene.Root, "抵城损失 " + enemy.CastleDamage + " 城防"), level.Name + "敌将情报缺少配置抵城损失");
                }
                Require(HasText(scene.Root, "城防归零才失败") && HasText(scene.Root, "全部波次结束"), level.Name + "情报缺少继续与胜利规则");
                CaptureThree(scene, level.Id + "-intel");
            }
        }
    }

    private static GameDatabase Fixture(GameDatabase original, string scenario, int castleHealth, bool doubleBreach)
    {
        // 深拷贝后仅安排路线、波次和起始经济；Boss原始属性不作调整。
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        level.Name = "Boss突破规则验收";
        level.Subtitle = "隔离夹具 · " + scenario;
        level.CastleMaxHealth = castleHealth;
        level.InitialGold = 1000;
        level.IncomePerSecond = 0;
        level.BossHeroIds = new List<string> { "zhang_liao" };
        level.Routes = new List<RouteDefinition>
        {
            new RouteDefinition { Id = "breach", Points = new List<Point2> { new Point2(1280, 580), new Point2(1322, 580) } }
        };
        level.Slots = new List<SlotDefinition> { new SlotDefinition { Id = 0, Position = new Point2(400, 400) } };
        level.Waves = new List<WaveDefinition>
        {
            new WaveDefinition { Name = "自然抵城", StartDelay = 0, Groups = new List<SpawnGroup>
            { new SpawnGroup { EnemyId = "boss_zhang_liao", RouteId = "breach", Count = doubleBreach ? 2 : 1, Interval = 4 } } }
        };
        if (!doubleBreach)
            level.Waves.Add(new WaveDefinition { Name = "继续守关验证", StartDelay = 8, Groups = new List<SpawnGroup>
            { new SpawnGroup { EnemyId = "infantry", RouteId = "breach", Count = 1, Interval = 1 } } });
        return database;
    }

    private static void AdvanceToLeak(BreachScene scene, int count)
    {
        BattleSession session = scene.Flow.CurrentSession;
        for (int i = 0; i < 600 && session.LeakedCount < count && session.State == BattleState.Running; i++) scene.Flow.Tick(1f / 30f);
        Require(session.LeakedCount == count, "短路线未自然产生预期突破次数：" + count);
    }

    private static void VerifyNoKillReward(BattleSession session, LevelDefinition level)
    {
        Require(session.DefeatedCount == 0 && session.Gold == level.InitialGold && session.GoldSpent == 0,
            "自然突破不应计入击退或发放赏金");
        Require(session.Enemies.Count == 0, "抵城Boss仍留在活动敌军中");
    }

    private static void VerifyRecord(BattleSession session, GameDatabase database)
    {
        BreachRecord breach = session.LastBreach;
        EnemyDefinition boss = database.FindEnemy("boss_zhang_liao");
        Require(breach != null && breach.IsBoss && breach.EnemyName == boss.Name && breach.RouteId == "breach" &&
            breach.Health == boss.MaxHealth && breach.MaxHealth == boss.MaxHealth, "突破记录未保留真实敌将、路线与剩余生命");
    }

    private static bool HasText(RectTransform root, string value)
    { return root.GetComponentsInChildren<Text>().Any(t => t.text.Contains(value)); }

    private static void Click(RectTransform root, string prefix)
    {
        Button button = root.GetComponentsInChildren<Button>().First(b => b.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith(prefix, StringComparison.Ordinal)));
        Require(button.interactable, "按钮不可操作：" + prefix);
        button.onClick.Invoke();
    }

    private static void InvokeRebuilding(Button button, RectTransform cards)
    {
        var callback = button.onClick;
        for (int i = cards.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(cards.GetChild(i).gameObject);
        callback.Invoke();
    }

    private static void CaptureThree(BreachScene scene, string name)
    {
        Capture(scene, name + "-1920", 1920, 1080);
        Capture(scene, name + "-1280", 1280, 720);
        Capture(scene, name + "-2400", 2400, 1080);
    }

    private static void Capture(BreachScene scene, string name, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            scene.Camera.targetTexture = target;
            scene.Camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            foreach (Text text in scene.Root.GetComponentsInChildren<Text>())
                if (!string.IsNullOrWhiteSpace(text.text)) UiTypographyChecks.Check(text, "Boss突破" + text.name);
            scene.Camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            scene.Camera.targetTexture = null;
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private sealed class BreachScene : IDisposable
    {
        private readonly GameObject canvasObject;
        public readonly Camera Camera;
        public readonly RectTransform Root;
        public readonly UiFactory Ui;
        public readonly GameFlow Flow;

        public BreachScene(GameDatabase database)
        {
            canvasObject = new GameObject("Boss突破规则验收", typeof(RectTransform), typeof(Canvas));
            Camera = new GameObject("Boss突破截图相机", typeof(Camera)).GetComponent<Camera>();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.pivot = new Vector2(0, 1);
            canvasRect.sizeDelta = new Vector2(1920, 1080);
            Camera.orthographic = true;
            Camera.orthographicSize = 540;
            Camera.transform.position = new Vector3(960, -540, -100);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = UiFactory.Deep;
            Camera.nearClipPlane = .1f;
            Camera.farClipPlane = 200;
            canvas.worldCamera = Camera;
            Ui = new UiFactory();
            Root = Ui.Rect(canvasRect, "Boss突破逻辑画布", 0, 0, 1920, 1080);
            var values = new Dictionary<string, int>();
            var progress = new GameProgress((key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => values[key] = value, () => { });
            Flow = new GameFlow(Ui, Root, database, progress);
        }

        public void Begin(LevelDefinition level)
        {
            Flow.BeginBattle(level, new List<string> { "lv_bu" });
            Ui.Label(Root.Find("页面"), "隔离规则验收：短路线与受控波次，不代表实际关卡难度", 340, 174, 1240, 34, 21, UiFactory.Gold, TextAnchor.MiddleCenter);
            Click(Root, "开战");
            Require(HasText(Root, "城防归零才失败") && !HasText(Root, "击退全部敌军"), "开战说明仍暗示必须全数击退");
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

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
