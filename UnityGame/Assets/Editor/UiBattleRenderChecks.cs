using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 真实uGUI离屏渲染与按钮回调检查；只在编辑器编译，不进入游戏安装包。
public static class UiBattleRenderChecks
{
    // A 方案独立入口：只把本轮图像写入新目录，不调用会重写历史图的完整 Run。
    public static void RunJade()
    {
        RunJadeChecks(Path.GetFullPath("../docs/ui/runtime-jade-a-20261112"), false);
    }

    public static void RunHarmony()
    {
        RunJadeChecks(Path.GetFullPath("../docs/ui/runtime-jade-harmony-20261112"), true);
    }

    public static void RunHeader()
    {
        RunJadeChecks(Path.GetFullPath("../docs/ui/runtime-header-clarity-20261112"), true, true);
    }

    public static void RunApp()
    {
        RunJadeChecks(Path.GetFullPath("../docs/ui/runtime-jade-app-20261112"), true, true, true);
    }

    public static void RunExperience()
    {
        RunJadeChecks(Path.GetFullPath("../docs/ui/runtime-combat-experience-20261112"), true, true, true, true);
    }

    public static void RunFactionArmy()
    {
        RunJadeChecks(Path.GetFullPath("../docs/ui/runtime-faction-army-20261112/regression"), true, true, true, true);
    }

    private static void RunJadeChecks(string output, bool verifyDisplayFont, bool verifyHeader = false, bool verifyApp = false, bool verifyExperience = false)
    {
        Directory.CreateDirectory(output);
        string startedAt = DateTime.UtcNow.ToString("o");
        // 先使上次成功记录失效；即使配置验证或后续检查抛错，也不能留下过期的通过状态。
        File.WriteAllLines(Path.Combine(output, "verification.txt"), new[]
        {
            "A「青绿行军」Unity 验收进行中，尚未通过",
            "本次开始 UTC：" + startedAt
        });
        File.WriteAllText(Path.Combine(output, "verification.json"),
            "{\"Status\":\"Running\",\"StartedAtUtc\":\"" + startedAt + "\"}");
        YanyiBuild.VerifyProject();
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        if (verifyDisplayFont) VerifyDisplayTypography(database);
        if (verifyHeader) UiBattleHeaderChecks.Verify(database);
        if (verifyApp)
        {
            UiRefinementMenuChecks.Verify(database);
            UiRefinementResultChecks.Capture(database, Path.Combine(output, "results"));
            UiAppStyleChecks.Verify(database, Path.Combine(output, "app"));
        }
        UiRefinementChecks.VerifyBattle(database, output);
        foreach (var level in database.Levels)
            VerifyLevel(database, level, output);
        VerifyHuangjinBattle(database, output, false, true);
        VerifyHuangjinBattle(database, output, true, true);
        UiGameFlowChecks.VerifyIsolated(database, output);
        UiBossBreachChecks.Verify(database, Path.Combine(output, "boss-breach"));
        if (verifyDisplayFont) UiGrowthChecks.Verify(database, Path.Combine(output, "growth"));
        if (verifyExperience)
        {
            UiBattleImpactChecks.Verify(database, Path.Combine(output, "impact"));
            UiCombatExperienceChecks.Verify(database, Path.Combine(output, "experience"));
        }
        string[] screenshots = Directory.GetFiles(output, "*.png", SearchOption.AllDirectories)
            .Select(path => path.Substring(output.Length + 1).Replace('\\', '/')).OrderBy(path => path).ToArray();
        File.WriteAllLines(Path.Combine(output, "verification.txt"), new[]
        {
            "A「青绿行军」Unity 验收通过",
            "Unity：" + Application.unityVersion,
            "UTC：" + DateTime.UtcNow.ToString("o"),
            "已执行：配置、字形、五关人物边界与投影、阵位射线、规则暂停恢复、五关招募升级分页、黄巾真实胜败、完整导航与隔离存档、Boss突破继续及低城防失败",
            verifyExperience ? "战斗体验专项：真实群攻击退、3/6/10连破、风眼与圆火区/道路火带、连环传伤与补入、协射来源/改射/双箭、旧事件寿命、共享预算、终局清理及音频门控" : "沿用既有界面专项",
            "截图数量：" + screenshots.Length,
            "以下为本轮输出："
        }.Concat(screenshots));
        File.WriteAllText(Path.Combine(output, "verification.json"),
            "{\"Status\":\"Passed\",\"StartedAtUtc\":\"" + startedAt +
            "\",\"CompletedAtUtc\":\"" + DateTime.UtcNow.ToString("o") + "\"}");
        Debug.Log("A方案UI验收通过：五关真实交互、黄巾完整胜败、三尺寸截图及完整导航；输出 " + output);
    }

    private static void VerifyDisplayTypography(GameDatabase database)
    {
        Font display = Resources.Load<Font>("Game/Fonts/ChineseDisplay");
        Font body = Resources.Load<Font>("Game/Fonts/Chinese");
        Require(display != null, "协调精修缺少可分发的宋体字体资源 ChineseDisplay");
        Require(body != null, "协调精修缺少原有正文黑体字体资源 Chinese");
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin();
            Require(NamedText(scene.Root, "关卡标题").font == display && NamedText(scene.Root, "成长标题").font == display,
                "关卡标题或成长标题未实际使用宋体资源");
            Button[] cards = scene.Root.GetComponentsInChildren<Button>().Where(button => button.name == "战斗武将卡").ToArray();
            Require(cards.Length == 5, "协调精修字体验收应覆盖五张真实武将卡");
            foreach (Button card in cards)
            {
                Text name = card.GetComponentsInChildren<Text>().Single(label => database.Heroes.Any(hero => hero.Name == label.text));
                Require(name.font == display, "武将卡姓名未实际使用宋体资源：" + name.text);
            }
            Require(NamedText(scene.Root, "军饷实秒收入").font == body && NamedText(scene.Root, "成长变化").font == body,
                "正文应继续使用原有黑体资源");
        }
        Debug.Log("协调精修字体检查通过：宋体资源可加载，关卡标题、成长标题及五卡姓名使用宋体，正文保留黑体");
    }

    public static void Run()
    {
        YanyiBuild.VerifyProject();
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        UiRefinementChecks.Run();
        UiRefinementResultChecks.Capture(database, UiRefinementChecks.Output);
        UiRecentRecordChecks.Run(database);
        UiFeedbackChecks.Verify(database);
        UiGrowthChecks.Verify(database);
        UiBossBreachChecks.Verify(database);
        string output = Path.GetFullPath("../docs/ui/runtime-flow");
        Directory.CreateDirectory(output);
        foreach (var level in database.Levels)
            VerifyLevel(database, level, output);
        VerifyHuangjinBattle(database, output, false);
        VerifyHuangjinBattle(database, output, true);
        UiGameFlowChecks.Verify(database, output);
        UiAudioIntegrationChecks.Verify(database);
        UiBattleImpactChecks.Verify(database);
        Debug.Log("UI自动渲染通过：五关基础交互、黄巾完整合法经济胜利、空阵失败、情报与结算；输出 " + output);
    }

    private static void VerifyLevel(GameDatabase database, LevelDefinition level, string output)
    {
        var canvasObject = new GameObject("UI自动渲染", typeof(RectTransform), typeof(Canvas));
        var cameraObject = new GameObject("UI截图相机", typeof(Camera));
        BattleView battle = null;
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.pivot = new Vector2(0, 1);
            canvasRect.sizeDelta = new Vector2(1920, 1080);
            canvasRect.position = Vector3.zero;
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.transform.position = new Vector3(960, -540, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Deep;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            canvas.worldCamera = camera;
            var ui = new UiFactory();
            RectTransform root = ui.Rect(canvasRect, "真实战斗页", 0, 0, 1920, 1080);
            var roster = new List<string>(level.RecommendedRoster);
            battle = new BattleView(null, ui, root, database, level, roster);
            if (level.Id == "huangjin") Capture(camera, output, "huangjin-empty", 1920, 1080);

            var positions = level.Slots.OrderBy(s => s.Position.Y).ThenBy(s => s.Position.X).ToList();
            SlotDefinition first = positions[0];
            SlotDefinition second = positions.First(s => s.Id != first.Id && Mathf.Abs(s.Position.X - first.Position.X) > 400);
            HeroDefinition firstHero = database.FindHero(roster.First(id => id != "zhuge_liang"));
            SelectHero(root, firstHero.Name);
            VerifyPlacementPreview(root, level, first.Id);
            ClickSlot(root, level, first.Id);
            Require(battle.Session.Heroes.Count == 1, level.Name + "顶部招募失败");
            Require(FindGround(root).Find("选中阵位").gameObject.activeSelf, "部署后缺少选中阵位标识");
            SelectHero(root, "诸葛亮");
            ClickSlot(root, level, second.Id);
            Require(battle.Session.Heroes.Count == 2, level.Name + "三字姓名武将招募失败");
            HeroUnit selected = battle.Session.Heroes.Find(h => h.Definition.Id == "zhuge_liang");
            Button upgrade = FindButton(root, "升级 ");
            if (upgrade.interactable) upgrade.onClick.Invoke();
            Require(!upgrade.interactable, level.Name + "军饷不足时升级仍可用");
            Require(upgrade.GetComponentInChildren<Text>().text.Contains("还差 "), "升级缺少差额");
            Require(root.GetComponentsInChildren<Text>().Any(t => t.text == "诸葛亮"), "三字名签缺失");
            FindButton(root, "开战").onClick.Invoke();
            for (int i = 0; i < 12; i++) battle.Tick(.1f);
            RectTransform cards = (RectTransform)root.Find("五张武将卡");
            Button initialPage = cards.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.Contains("/"));
            if (!initialPage.GetComponentInChildren<Text>().text.Contains("1/")) InvokeRebuilding(initialPage, cards);
            Capture(camera, output, level.Id + "-deployed", 1920, 1080);
            if (level.Id == "huangjin")
            {
                Capture(camera, output, "huangjin-mobile", 1280, 720);
                Capture(camera, output, "huangjin-wide", 2400, 1080);
            }

            // 通过真实会话推进收入与战斗，再点击相同升级按钮。
            for (int i = 0; i < 1200 && selected.Level < BattleSession.MaxHeroLevel; i++)
            {
                battle.Tick(.1f);
                if (upgrade.interactable) upgrade.onClick.Invoke();
            }
            Require(selected.Level == BattleSession.MaxHeroLevel, level.Name + "真实收入与升级流程未到三级");
            Require(!upgrade.interactable && upgrade.GetComponentInChildren<Text>().text == "已满级", "满级状态错误");
            for (int i = 0; i < 12; i++) battle.Tick(.1f);
            if (level.Id == "huangjin") Capture(camera, output, "huangjin-max-level", 1920, 1080);
            Button page = cards.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.Contains("/"));
            if (roster.Count > 6)
            {
                string before = page.GetComponentInChildren<Text>().text;
                InvokeRebuilding(page, cards);
                Button after = cards.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.Contains("/"));
                Require(after.GetComponentInChildren<Text>().text != before, "武将翻页失败");
            }
            Debug.Log("UI流程验证通过：" + level.Name + "，剩余军饷" + battle.Session.Gold + "，诸葛亮3阶");
        }
        finally
        {
            if (battle != null) battle.Dispose();
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void VerifyHuangjinBattle(GameDatabase database, string output, bool empty, bool isolateOutput = false)
    {
        LevelDefinition level = database.FindLevel("huangjin");
        using (var scene = new RenderScene(database, level))
        {
            BattleSession session = scene.Battle.Session;
            string[] roster = level.RecommendedRoster.ToArray();
            int[] slotIds = { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 };
            bool sawInitialCountdown = false, sawActive = false, sawBetween = false;
            bool capturedCombat = false, capturedBoss = false, capturedWarning = false, capturedBetween = false;
            bool capturedImpactDensity = false;
            int spent = 0;
            VerifyTempo(scene.Root, session);
            VerifyIntel(scene.Root, session, database, level);
            if (empty) Capture(scene.Camera, output, "huangjin-empty-army", 1920, 1080);

            for (int step = 0; step < 3200 && (session.State == BattleState.Preparation || session.State == BattleState.Running); step++)
            {
                if (!empty)
                {
                    for (int i = 0; i < roster.Length; i++)
                    {
                        HeroDefinition hero = database.FindHero(roster[i]);
                        if (session.IsRecruited(hero.Id) || session.Gold < hero.Cost) continue;
                        int before = session.Gold;
                        SelectHero(scene.Root, hero.Name);
                        VerifyPlacementPreview(scene.Root, level, slotIds[i]);
                        if (step == 0 && session.Heroes.Count == 0)
                            Capture(scene.Camera, output, "huangjin-placement-preview", 1920, 1080);
                        ClickSlot(scene.Root, level, slotIds[i]);
                        Require(session.IsRecruited(hero.Id) && session.Gold == before - hero.Cost,
                            "黄巾UI招募未按真实费用完成：" + hero.Name);
                        spent += hero.Cost;
                    }
                    if (session.Heroes.Count == roster.Length)
                    {
                        foreach (HeroUnit hero in session.Heroes.OrderBy(h => h.Level).ToList())
                        {
                            int cost = session.GetUpgradeCost(hero.SlotId);
                            if (hero.Level >= BattleSession.MaxHeroLevel || session.Gold < cost) continue;
                            int before = session.Gold, beforeLevel = hero.Level;
                            ClickSlot(scene.Root, level, hero.SlotId);
                            Button upgrade = FindButton(scene.Root, "升级 ");
                            Require(upgrade.interactable, "真实收入已足额但升级按钮不可用");
                            upgrade.onClick.Invoke();
                            Require(hero.Level == beforeLevel + 1 && session.Gold == before - cost,
                                "黄巾UI升级未按真实费用完成：" + hero.Definition.Name);
                            spent += cost;
                        }
                    }
                }
                Require(session.GoldSpent == spent && session.Gold >= 0, "UI累计消费与真实会话不符");
                if (session.State == BattleState.Preparation) FindButton(scene.Root, "开战").onClick.Invoke();
                if (step == 0)
                {
                    VerifyTempo(scene.Root, session);
                    sawInitialCountdown = !session.IsWaveActive && session.CurrentWave == 0;
                    VerifyPausedBattle(scene.Battle, scene.Root);
                }
                scene.Battle.Tick(.25f);
                VerifyTempo(scene.Root, session);
                VerifyIntel(scene.Root, session, database, level);
                if (session.IsWaveActive) sawActive = true;
                if (session.State == BattleState.Running && !session.IsWaveActive && session.CurrentWave > 0)
                {
                    sawBetween = true;
                    if (!empty && !capturedBetween)
                    {
                        Capture(scene.Camera, output, "huangjin-between-waves", 1920, 1080);
                        capturedBetween = true;
                    }
                }
                if (empty) continue;
                if (!capturedImpactDensity && session.CurrentWave >= 4 && session.Enemies.Count >= 6)
                {
                    if (isolateOutput)
                    {
                        foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                            if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "A方案密集战斗" + label.name);
                        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
                            Capture(scene.Camera, output, "huangjin-density-" + size.x, size.x, size.y);
                    }
                    else UiBattleImpactChecks.CaptureNatural(scene.Camera, scene.Root);
                    capturedImpactDensity = true;
                }
                if (!capturedCombat && session.Enemies.Count >= 5 && session.Heroes.Count >= 3)
                {
                    Capture(scene.Camera, output, "huangjin-combat", 1920, 1080);
                    capturedCombat = true;
                }
                EnemyUnit boss = session.Enemies.FirstOrDefault(e => e.Definition.IsBoss);
                if (boss != null && !capturedBoss && boss.Progress > .08f)
                {
                    Capture(scene.Camera, output, "huangjin-boss", 1920, 1080);
                    capturedBoss = true;
                }
                if (boss != null && boss.WarningUntil > session.Time && !capturedWarning)
                {
                    Capture(scene.Camera, output, "huangjin-boss-warning", 1920, 1080);
                    capturedWarning = true;
                }
            }
            Require(sawInitialCountdown && sawActive, "未覆盖首波倒计时及活跃波次");
            Require(session.State == (empty ? BattleState.Defeat : BattleState.Victory),
                "黄巾" + (empty ? "空阵" : "参考布阵") + "结果不符，实际" + session.State);
            if (empty)
            {
                Require(session.Heroes.Count == 0 && session.GoldSpent == 0 && session.LeakedCount > 0,
                    "空阵失败统计不符");
            }
            else
            {
                Require(sawBetween && capturedCombat && capturedBoss && capturedWarning, "完整胜利未覆盖波间、战斗、敌将或蓄势画面");
                Require(capturedImpactDensity, "完整黄巾流程未覆盖后五波实际密集战斗画面");
                Require(session.Heroes.Count == roster.Length && session.CurrentWave == session.TotalWaves && session.RemainingEnemies == 0,
                    "参考布阵胜利时部署、波次或余敌不符");
                Require(session.DefeatedCount > 0 && session.GoldSpent == spent, "参考布阵未累计真实击退或消费");
            }
            VerifyResult(scene, level, output, empty);
            Debug.Log("黄巾完整UI验证：" + session.State + "，游戏时长" + session.Time.ToString("0.0") + "秒（非真实墙钟耗时），击退" +
                session.DefeatedCount + "，漏兵" + session.LeakedCount + "，消费" + session.GoldSpent);
        }
    }

    private static void VerifyPausedBattle(BattleView battle, RectTransform root)
    {
        BattleSession session = battle.Session;
        session.SetPaused(true);
        battle.Tick(0);
        float beforeTime = session.Time, countdown = session.NextWaveCountdown;
        int gold = session.Gold, remaining = session.RemainingEnemies;
        string[] beforeTexts = root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
        for (int i = 0; i < 20; i++) battle.Tick(.25f);
        Require(session.Time == beforeTime && session.Gold == gold && session.NextWaveCountdown == countdown && session.RemainingEnemies == remaining,
            "暂停时战斗时钟、军饷、倒计时或余敌发生变化");
        Require(beforeTexts.SequenceEqual(root.GetComponentsInChildren<Text>().Select(t => t.text)), "暂停时战场信息发生变化");
        session.SetPaused(false);
        battle.Tick(0);
    }

    private static void VerifyTempo(RectTransform root, BattleSession session)
    {
        Text title = NamedText(root, "战况标题"), count = NamedText(root, "战况计数");
        if (session.State == BattleState.Victory || session.State == BattleState.Defeat)
        {
            Require(title.text == (session.State == BattleState.Victory ? "守关成功" : "守关失利"),
                "终局稳定情报标题未刷新，实际为" + title.text);
            Require(count.text == "击退 " + session.DefeatedCount, "终局稳定情报未显示真实击退数");
            if (session.State == BattleState.Defeat)
                Require(NamedText(root, "战况摘要").text == session.DefeatReason, "终局稳定情报未显示真实失败原因");
            RectTransform ground = FindGround(root);
            Require(ground.Find("战斗反馈").GetComponentsInChildren<Text>().Length == 0,
                "终局仍残留活动战斗飘字");
            int visibleEnemies = 0;
            foreach (Transform unit in ground.Find("棋兵"))
                if (unit.gameObject.activeSelf && unit.name == "敌军") visibleEnemies++;
            Require(visibleEnemies == session.Enemies.Count,
                "终局活动敌军数量与真实会话不符，可能残留退场影像");
            return;
        }
        EnemyUnit boss = session.Enemies.FirstOrDefault(e => e.Definition.IsBoss && e.Health > 0);
        if (boss != null && session.State == BattleState.Running)
        {
            Require(title.text.Contains(boss.Definition.Name) && count.text == Mathf.CeilToInt(boss.Health) + "/" + Mathf.CeilToInt(boss.MaxHealth),
                "敌将情报姓名或真实生命值不符");
            if (boss.WarningUntil > session.Time)
                Require(NamedText(root, "战况摘要").text.Contains("蓄势"), "敌将施法前缺少稳定蓄势情报");
        }
        else if (session.State == BattleState.Preparation)
        {
            Require(title.text.Contains("下一波"), "准备阶段缺少下一波情报");
            Require(count.text.Contains("开战后") && count.text.Contains(Mathf.CeilToInt(session.NextWaveCountdown).ToString()),
                "准备阶段未显示首波真实延迟");
        }
        else if (session.IsWaveActive)
            Require(title.text.Contains("本波") && count.text.Contains("余敌 " + session.RemainingEnemies),
                "活跃波次未显示真实余敌，实际为" + count.text);
        else if (session.State == BattleState.Running)
            Require(title.text.Contains("下一波") && count.text.Contains("秒后") && count.text.Contains(Mathf.CeilToInt(session.NextWaveCountdown).ToString()),
                "波间未显示真实下一波倒计时，实际为" + count.text);
    }

    private static void VerifyIntel(RectTransform root, BattleSession session, GameDatabase database, LevelDefinition level)
    {
        Text title = NamedText(root, "战况标题"), summary = NamedText(root, "战况摘要"), count = NamedText(root, "战况计数");
        Require(!string.IsNullOrEmpty(title.text) && !string.IsNullOrEmpty(summary.text), "稳定战况情报标题或摘要为空");
        Require(!string.IsNullOrEmpty(count.text), "稳定战况情报计数为空");
        if (session.State == BattleState.Victory || session.State == BattleState.Defeat || session.Enemies.Any(e => e.Definition.IsBoss)) return;
        int waveIndex = session.IsWaveActive ? session.CurrentWave - 1 : session.CurrentWave;
        if (waveIndex >= 0 && waveIndex < level.Waves.Count)
        {
            WaveDefinition wave = level.Waves[waveIndex];
            Require(title.text.Contains(wave.Name), "稳定情报未指向当前或下一波真实名称");
            Require(summary.text.Contains("敌型：") && summary.text.Contains("入口：") &&
                summary.text.Contains(database.FindEnemy(wave.Groups[0].EnemyId).Name), "稳定摘要缺少真实敌型或入口");
        }
    }

    private static void VerifyResult(RenderScene scene, LevelDefinition level, string output, bool empty)
    {
        int retry = 0, choose = 0, next = 0, levels = 0;
        RectTransform result = BattleResultView.Create(scene.Ui, scene.Root, level, scene.Battle.Session,
            () => retry++, () => choose++, empty ? (Action)null : () => next++, () => levels++);
        BattleSession session = scene.Battle.Session;
        Require(NamedText(result, "结算标题").text == (empty ? "此局失守" : "守关大捷"), "结算标题不符");
        if (empty) Require(NamedText(result, "结算说明").text.Contains(session.DefeatReason), "失败结算缺少真实失败原因");
        int seconds = (int)Math.Floor(session.ElapsedSeconds);
        CheckStat(result, "城防", session.CastleHealth + " / " + level.CastleMaxHealth);
        CheckStat(result, "用时", seconds / 60 + "分" + (seconds % 60).ToString("00") + "秒");
        CheckStat(result, "击退", session.DefeatedCount + " 名敌军");
        CheckStat(result, "漏兵", session.LeakedCount + " 名敌军");
        CheckStat(result, "军饷支出", session.GoldSpent.ToString());
        CheckStat(result, "波次", session.CurrentWave + " / " + session.TotalWaves);
        Capture(scene.Camera, output, empty ? "huangjin-defeat-result" : "huangjin-victory-result", 1920, 1080);
        if (!empty) Capture(scene.Camera, output, "huangjin-victory-mobile", 1280, 720);
        FindButton(result, "重新推演").onClick.Invoke();
        FindButton(result, empty ? "重新选将" : "下一关").onClick.Invoke();
        FindButton(result, "返回关卡").onClick.Invoke();
        Require(retry == 1 && levels == 1 && (empty ? choose == 1 && next == 0 : next == 1 && choose == 0),
            "结算按钮未调用正确回调");
    }

    private static void CheckStat(RectTransform result, string name, string expected)
    {
        Transform box = result.Find("结算弹窗/统计" + name + "/统计数值");
        Require(box != null && box.GetComponent<Text>().text == expected, "结算统计不符：" + name);
    }

    private static Text NamedText(RectTransform root, string name)
    {
        Text text = root.GetComponentsInChildren<Text>().FirstOrDefault(t => t.gameObject.name == name);
        Require(text != null, "未找到界面文字：" + name);
        return text;
    }

    private static RectTransform FindGround(RectTransform root)
    { return (RectTransform)root.Find("沙盘视口/战场坐标层"); }

    private static void VerifyPlacementPreview(RectTransform root, LevelDefinition level, int slotId)
    {
        RectTransform ground = FindGround(root);
        Require(!ground.Find("战场名签").GetComponentsInChildren<RectTransform>().Any(t => t.name == "名签底"),
            "常态战场仍有会遮挡其他人物的大姓名牌");
        Transform slot = ground.Find("可交互阵位").GetChild(level.Slots.FindIndex(s => s.Id == slotId));
        EventTrigger trigger = slot.GetComponent<EventTrigger>();
        EventTrigger.Entry enter = trigger.triggers.First(e => e.eventID == EventTriggerType.PointerEnter);
        enter.callback.Invoke(new PointerEventData(null));
        Require(ground.Find("道路覆盖预览").gameObject.activeSelf, "选将悬停阵位时道路覆盖预览未显示");
    }

    private sealed class RenderScene : IDisposable
    {
        public readonly Camera Camera;
        public readonly UiFactory Ui;
        public readonly RectTransform Root;
        public readonly BattleView Battle;
        private readonly GameObject canvasObject;

        public RenderScene(GameDatabase database, LevelDefinition level)
        {
            canvasObject = new GameObject("完整UI验证", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("完整UI截图相机", typeof(Camera));
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRect = (RectTransform)canvas.transform;
                canvasRect.pivot = new Vector2(0, 1);
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                Camera = cameraObject.GetComponent<Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = 540;
                Camera.transform.position = new Vector3(960, -540, -100);
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = UiFactory.Deep;
                Camera.nearClipPlane = .1f;
                Camera.farClipPlane = 200;
                canvas.worldCamera = Camera;
                Ui = new UiFactory();
                Root = Ui.Rect(canvasRect, "完整战斗页", 0, 0, 1920, 1080);
                Battle = new BattleView(null, Ui, Root, database, level, new List<string>(level.RecommendedRoster));
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                throw;
            }
        }

        public void Dispose()
        {
            try { Battle.Dispose(); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(Camera.gameObject);
            }
        }
    }

    private static void SelectHero(RectTransform root, string name)
    {
        RectTransform cards = (RectTransform)root.Find("五张武将卡");
        for (int i = 0; i < 2; i++)
        {
            Button card = cards.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentsInChildren<Text>().Any(t => t.text == name));
            if (card != null) { InvokeRebuilding(card, cards); return; }
            Button page = cards.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.Contains("/"));
            InvokeRebuilding(page, cards);
        }
        throw new InvalidOperationException("推荐阵容中没有" + name);
    }

    private static void ClickSlot(RectTransform root, LevelDefinition level, int id)
    {
        Button slot = root.Find("沙盘视口/战场坐标层/可交互阵位").GetChild(level.Slots.FindIndex(s => s.Id == id)).GetComponent<Button>();
        InvokeRebuilding(slot, (RectTransform)root.Find("五张武将卡"));
    }

    private static void InvokeRebuilding(Button button, RectTransform cards)
    {
        // 编辑器不运行帧末销毁队列；仅提前释放即将由真实回调重建的卡片。
        var callback = button.onClick;
        for (int i = cards.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(cards.GetChild(i).gameObject);
        callback.Invoke();
    }

    private static Button FindButton(RectTransform root, string prefix)
    {
        return root.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void Capture(Camera camera, string output, string name, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
