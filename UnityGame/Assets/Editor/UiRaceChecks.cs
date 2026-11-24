using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 全部存档均由Scene注入内存字典；不读写玩家自己的纪录或教学标记。
public static class UiRaceChecks
{
    public static string Output { get; private set; } = Path.GetFullPath("../docs/ui/runtime-race-20261119");
    private static int assertions;
    private static readonly HashSet<string> captured = new HashSet<string>();
    [Serializable] private sealed class Report
    {
        public string Status, StartedAtUtc, CompletedAtUtc;
        public int AssertionCount, CheckCount;
        public string[] Failures, Screenshots;
        public bool RealDeviceChecked = false, HumanFunChecked = false;
    }

    public static void Run()
    { RunTo(Path.GetFullPath("../docs/ui/runtime-race-20261119")); }

    public static void RunTo(string output)
    {
        Output = Path.GetFullPath(output);
        Directory.CreateDirectory(Output);
        var report = new Report { Status = "Running", StartedAtUtc = DateTime.UtcNow.ToString("o") };
        SaveReport(report);
        YanyiBuild.VerifyProject();
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        assertions = 0; captured.Clear();
        var failures = new List<string>();
        Check(failures, "首次倍率说明与确认持久化", () => VerifyIntro(database));
        Check(failures, "六向直选与状态输入保护", () => VerifySpeedSelection(database));
        Check(failures, "说明期间系统中断与返回键", () => VerifySystemInterrupt(database));
        Check(failures, "调整阵容保留名单且重新付费", () => VerifyRoster(database));
        Check(failures, "军令台与真实升级收益", () => VerifyBattleReadability(database));
        Check(failures, "敌我形状辨识", () => VerifyUnitMarkers(database));
        Check(failures, "合格与不合格结算及回调隔离", () => VerifyResults(database));
        Check(failures, "首次慢局相同刷新与列表翻页", () => VerifyComparisons(database));
        Check(failures, "保存失败仍可结算与重新阅读", () => VerifyStorageFailures(database));
        Check(failures, "缺钱满阶与密集Boss失败状态", () => VerifyMoreStates(database));
        bool attemptedCapture = failures.Count == 0;
        if (attemptedCapture) Check(failures, "三尺寸与完整状态截图", () => CaptureStates(database));
        report.Status = failures.Count == 0 ? "Passed" : "Failed";
        report.CompletedAtUtc = DateTime.UtcNow.ToString("o");
        report.AssertionCount = assertions; report.CheckCount = 10 + (attemptedCapture ? 1 : 0);
        report.Failures = failures.ToArray();
        report.Screenshots = captured.OrderBy(p => p).ToArray();
        SaveReport(report);
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures.ToArray()));
        Debug.Log("竞速UI验收通过：" + assertions + "项断言，" + report.Screenshots.Length + "张独立截图");
    }

    // 独立无截图入口供先失败后实现检查使用，不写历史验收目录。
    public static void RunSpeedSelectionContract()
    {
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        assertions = 0; VerifySpeedSelection(database);
        Debug.Log("倍率直选契约通过：" + assertions + "项断言");
    }

    private static void VerifySpeedSelection(GameDatabase database)
    {
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.Progress.MarkMarchSpeedIntroRead(); scene.Begin();
            BattleSession b = scene.Flow.CurrentSession;
            for (int from = 1; from <= 3; from++)
            for (int target = 1; target <= 3; target++)
            {
                ButtonNamed(scene.Root, "进军倍率" + from).onClick.Invoke();
                string before = TextNamed(scene.Root, "军令通知").text;
                ButtonNamed(scene.Root, "进军倍率" + target).onClick.Invoke();
                Require(b.Speed == target && b.State == BattleState.Preparation, "直选必须一次到达且不替代开战");
                if (from == target) Require(TextNamed(scene.Root, "军令通知").text == before, "当前档重复点击不得通知");
                for (int speed = 1; speed <= 3; speed++)
                {
                    Button button = ButtonNamed(scene.Root, "进军倍率" + speed);
                    Require(((RectTransform)button.transform).rect.width == 72, "三档独立热区宽度应为72");
                    Require(button.transform.Find("倍率选中标记").gameObject.activeSelf == (target == speed), "选中标记必须来自真实会话");
                }
            }
            Invoke(scene.Flow, "ChangeMarchSpeed", 0); Invoke(scene.Flow, "ChangeMarchSpeed", 4);
            Require(b.Speed == 3, "非法目标必须拒绝");
            ButtonNamed(scene.Root, "进军倍率1").onClick.Invoke();
            b.Start(); scene.Flow.Tick(.01f);
            ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke();
            ButtonNamed(scene.Root, "进军倍率1").onClick.Invoke();
            scene.Flow.Tick(.03f);
            Require(b.Speed == 1 && b.GetRaceSnapshot().GetSpeedSeconds(3) == 0, "2到1不得插入3倍时间片");
            scene.Flow.PauseBattle(); double elapsed = b.RaceElapsedSeconds; int gold = b.Gold;
            ButtonNamed(scene.Root, "进军倍率3").onClick.Invoke(); Require(b.Speed == 1, "暂停遮罩必须拦截顶栏入口");
            ButtonNamed(scene.Root, "暂停进军倍率3").onClick.Invoke(); scene.Flow.Tick(10);
            Require(b.Speed == 3 && b.State == BattleState.Paused && b.RaceElapsedSeconds == elapsed && b.Gold == gold, "暂停菜单选档不得恢复或推进");
            scene.Flow.ShowAudioSettings(); ButtonNamed(scene.Root, "暂停进军倍率1").onClick.Invoke();
            Require(b.Speed == 3, "音量模态中不得穿透选档"); scene.Flow.HandleBack();
            Invoke(scene.Flow, "SetApplicationFocused", false);
            ButtonNamed(scene.Root, "暂停进军倍率1").onClick.Invoke(); scene.Flow.HandleBack();
            Require(b.Speed == 3 && b.State == BattleState.Paused, "后台禁止输入或恢复");
            Invoke(scene.Flow, "SetApplicationFocused", true);
            ButtonNamed(scene.Root, "暂停进军倍率1").onClick.Invoke(); Require(b.Speed == 1 && b.State == BattleState.Paused, "前台明确暂停可选档但不得恢复");
            Invoke(scene.Flow, "SetApplicationPaused", true); Invoke(scene.Flow, "SetApplicationFocused", false);
            Invoke(scene.Flow, "SetApplicationFocused", true); ButtonNamed(scene.Root, "暂停进军倍率2").onClick.Invoke();
            Require(b.Speed == 1 && b.State == BattleState.Paused, "恢复焦点不得绕过应用挂起");
            Invoke(scene.Flow, "SetApplicationPaused", false);
            scene.Flow.HandleBack(); Require(b.State == BattleState.Running, "前台须明确继续");
            foreach (int target in new[] { 2, 1, 3, 1 })
            { ButtonNamed(scene.Root, "进军倍率" + target).onClick.Invoke(); Require(b.Speed == target && b.State == BattleState.Running, "快速目标序列须按输入顺序处理"); }
            scene.Flow.ShowMarchSpeedRules(); Invoke(scene.Flow, "ChangeMarchSpeed", 3);
            Require(b.Speed == 1 && b.State == BattleState.Paused, "规则模态必须拒绝目标请求");
            scene.Flow.HandleBack();
        }
        foreach (int target in new[] { 2, 3 })
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); BattleSession b = scene.Flow.CurrentSession; b.Start();
            double elapsed = b.RaceElapsedSeconds; float march = b.MarchTime; int gold = b.Gold;
            ButtonNamed(scene.Root, "进军倍率" + target).onClick.Invoke();
            Require(b.Speed == target && b.State == BattleState.Paused && b.RaceElapsedSeconds == elapsed && b.MarchTime == march && b.Gold == gold,
                "首次高档必须在回调内暂停且无中间推进");
            Require(HasText(scene.Root, "当前选择 " + target + "×"), "说明须显示实际目标倍率");
            ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(b.Speed == target && b.State == BattleState.Running, "确认只恢复状态不得再次切档");
        }
        foreach (bool paused in new[] { false, true })
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); BattleSession b = scene.Flow.CurrentSession;
            if (paused) { b.Start(); scene.Flow.PauseBattle(); }
            ButtonNamed(scene.Root, paused ? "暂停进军倍率3" : "进军倍率3").onClick.Invoke();
            ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(b.Speed == 3 && b.State == (paused ? BattleState.Paused : BattleState.Preparation), "首次说明确认必须保持备战或普通暂停");
        }
        foreach (bool loseFocus in new[] { true, false })
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); BattleSession b = scene.Flow.CurrentSession; b.Start();
            ButtonNamed(scene.Root, "进军倍率3").onClick.Invoke();
            string systemEntry = loseFocus ? "SetApplicationFocused" : "SetApplicationPaused";
            Invoke(scene.Flow, systemEntry, !loseFocus);
            Button confirm = ButtonNamed(scene.Root, "倍率说明确认"); confirm.onClick.Invoke();
            Require(confirm != null && confirm.gameObject.activeInHierarchy && !scene.Flow.Progress.HasReadMarchSpeedIntro,
                "后台确认必须拒绝关闭说明或写入已读");
            scene.Flow.HandleBack();
            Require(confirm != null && confirm.gameObject.activeInHierarchy && !scene.Flow.Progress.HasReadMarchSpeedIntro,
                "后台返回不得绕过说明输入保护");
            Require(b.State == BattleState.Paused && b.Speed == 3, "后台确认不得恢复或重新切档");
            Invoke(scene.Flow, systemEntry, loseFocus); confirm.onClick.Invoke();
            Require(scene.Flow.Progress.HasReadMarchSpeedIntro && b.State == BattleState.Paused && b.Speed == 3,
                "恢复激活后确认才能保存说明且仍须明确继续");
            ButtonNamed(scene.Root, "暂停进军倍率1").onClick.Invoke(); Require(b.State == BattleState.Paused, "系统恢复后选档不能替代继续");
            scene.Flow.HandleBack(); Require(b.State == BattleState.Running && b.Speed == 1, "系统说明中断后仅明确继续才运行");
        }
        GameDatabase resultDatabase = ResultFixture(database, false);
        using (var scene = new UiRefinementChecks.Scene(resultDatabase))
        {
            LevelDefinition level = resultDatabase.FindLevel("huangjin");
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu" }); scene.Flow.CurrentSession.TryRecruit("lv_bu", 0); Finish(scene);
            BattleSession b = scene.Flow.CurrentSession; double elapsed = b.RaceElapsedSeconds;
            Require(b.State == BattleState.Victory, "终局夹具没有结束");
            for (int speed = 1; speed <= 3; speed++)
            {
                Button button = ButtonNamed(scene.Root, "进军倍率" + speed);
                Require(!button.interactable, "终局必须禁用三档"); button.onClick.Invoke();
            }
            Invoke(scene.Flow, "ChangeMarchSpeed", 3); Require(b.Speed == 1 && b.RaceElapsedSeconds == elapsed, "终局请求不得改终点或倍率");
        }
        using (var scene = new UiRefinementChecks.Scene(database))
        using (var view = new BattleView(null, new UiFactory(), scene.Root, database, database.FindLevel("huangjin"), new List<string>(database.FindLevel("huangjin").RecommendedRoster)))
        {
            foreach (int speed in new[] { 3, 1, 2, 1 })
            { ButtonNamed(scene.Root, "进军倍率" + speed).onClick.Invoke(); Require(view.Session.Speed == speed && view.Session.State == BattleState.Preparation, "无流程回退必须接收目标且不开战"); }
        }
        VerifySpeedHitTargets(database);
    }

    private static void VerifySpeedHitTargets(GameDatabase database)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2340, 1080) })
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            var eventObject = new GameObject("倍率射线事件系统", typeof(EventSystem));
            EventSystem events = eventObject.GetComponent<EventSystem>();
            // 编辑器非播放夹具不触发BaseRaycaster.OnEnable，显式注册后走真实EventSystem排序。
            RaycasterManager.GetRaycasters().Add(scene.Raycaster);
            RenderTexture previous = scene.Camera.targetTexture, target = RenderTexture.GetTemporary(size.x, size.y, 24);
            try
            {
                scene.Flow.Progress.MarkMarchSpeedIntroRead(); scene.Begin();
                ((RectTransform)scene.Root.parent).sizeDelta = new Vector2(size.x, size.y);
                scene.Camera.orthographicSize = size.y / 2f;
                scene.Camera.transform.position = new Vector3(size.x / 2f, -size.y / 2f, -100);
                scene.Camera.targetTexture = target; scene.Camera.aspect = (float)size.x / size.y;
                Rect safe = size.x == 2340 ? new Rect(96, 42, 2160, 1008) : new Rect(0, 0, size.x, size.y);
                SafeAreaLayout.Apply(scene.Root, size.x, size.y, safe);
                BattleSession b = scene.Flow.CurrentSession;
                for (int speed = 1; speed <= 3; speed++)
                {
                    RaycastClick(scene, events, ButtonNamed(scene.Root, "进军倍率" + speed), safe);
                    Require(b.Speed == speed && b.State == BattleState.Preparation, size.x + "射线点击必须命中指定档且不开战");
                }
                Button primary = ButtonNamed(scene.Root, "战场主操作");
                RaycastClick(scene, events, primary, safe); Require(b.State == BattleState.Running, size.x + "主操作射线没有开战");
                RaycastClick(scene, events, primary, safe); Require(b.State == BattleState.Paused, size.x + "主操作射线没有打开暂停");
                for (int speed = 1; speed <= 3; speed++)
                {
                    RaycastClick(scene, events, ButtonNamed(scene.Root, "暂停进军倍率" + speed), safe);
                    Require(b.Speed == speed && b.State == BattleState.Paused, size.x + "暂停菜单射线选档不得恢复");
                    RequireRaycastBlocked(scene, events, ButtonNamed(scene.Root, "进军倍率" + speed));
                }
                RequireRaycastBlocked(scene, events, primary);
                scene.Flow.ShowAudioSettings();
                for (int speed = 1; speed <= 3; speed++)
                {
                    RequireRaycastBlocked(scene, events, ButtonNamed(scene.Root, "暂停进军倍率" + speed));
                    RequireRaycastBlocked(scene, events, ButtonNamed(scene.Root, "进军倍率" + speed));
                }
                scene.Flow.HandleBack(); scene.Flow.ShowMarchSpeedRules();
                for (int speed = 1; speed <= 3; speed++) RequireRaycastBlocked(scene, events, ButtonNamed(scene.Root, "进军倍率" + speed));
                RequireRaycastBlocked(scene, events, primary);
                RaycastClick(scene, events, ButtonNamed(scene.Root, "倍率说明确认"), safe);
                Require(b.State == BattleState.Paused && b.Speed == 3, size.x + "规则实际点击关闭后应保留暂停与档位");
            }
            finally
            {
                scene.Camera.targetTexture = previous; RenderTexture.ReleaseTemporary(target);
                RaycasterManager.GetRaycasters().Remove(scene.Raycaster);
                UnityEngine.Object.DestroyImmediate(eventObject);
            }
        }
    }

    private static PointerEventData ButtonPointer(UiRefinementChecks.Scene scene, EventSystem events, Button button)
    {
        Canvas.ForceUpdateCanvases(); scene.Camera.Render();
        RectTransform rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(scene.Camera, rect.TransformPoint(rect.rect.center));
        return new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
    }

    private static void RaycastClick(UiRefinementChecks.Scene scene, EventSystem events, Button button, Rect safe)
    {
        PointerEventData pointer = ButtonPointer(scene, events, button);
        Require(safe.Contains(pointer.position), button.name + "中心命中点超出安全区");
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, button.name + "实际EventSystem射线没有命中目标，位置" + pointer.position +
            "，命中" + string.Join(" / ", hits.Select(hit => hit.gameObject.name).ToArray()));
        Require(button.interactable, button.name + "目标应可交互");
        ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void RequireRaycastBlocked(UiRefinementChecks.Scene scene, EventSystem events, Button button)
    {
        PointerEventData pointer = ButtonPointer(scene, events, button);
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject != button.gameObject &&
            !hits[0].gameObject.transform.IsChildOf(button.transform), button.name + "被模态覆盖时仍可由背景射线命中");
    }

    private static void VerifyIntro(GameDatabase database)
    {
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); BattleSession b = scene.Flow.CurrentSession;
            ButtonNamed(scene.Root, "战场主操作").onClick.Invoke(); scene.Flow.Tick(.2f);
            double before = b.RaceElapsedSeconds; int gold = b.Gold;
            ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke();
            Require(b.Speed == 2 && b.State == BattleState.Paused, "首次切2倍必须只切一次并暂停阅读");
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("我方攻击与技能速度不变")), "说明缺少压力差异");
            RequireClickable(scene, ButtonNamed(scene.Root, "倍率说明确认"));
            scene.Flow.Tick(10); Require(b.RaceElapsedSeconds == before && b.Gold == gold, "阅读推进了用时或收入");
            ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(b.Speed == 2 && b.State == BattleState.Running, "确认后倍率或原运行状态错误");
            Require(Read<bool>(scene.Flow.Progress, "HasReadMarchSpeedIntro"), "确认未保存独立已读");
            ButtonNamed(scene.Root, "进军倍率3").onClick.Invoke(); Require(b.Speed == 3, "后续切3倍失败");
            ButtonNamed(scene.Root, "进军倍率1").onClick.Invoke(); Require(b.Speed == 1, "后续回1倍失败");
            scene.Flow.RetryBattle(); ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke();
            Require(scene.Flow.CurrentSession.Speed == 2 && scene.Flow.CurrentSession.State == BattleState.Preparation,
                "重试后不应重复首读或擅自开战");
        }
    }

    private static void VerifySystemInterrupt(GameDatabase database)
    {
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); scene.Flow.CurrentSession.Start();
            ButtonNamed(scene.Root, "进军倍率3").onClick.Invoke(); scene.Flow.PauseForSystem();
            Require(!Read<bool>(scene.Flow.Progress, "HasReadMarchSpeedIntro"), "系统中断误标已读");
            ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(scene.Flow.CurrentSession.State == BattleState.Paused, "系统中断后确认不得自动恢复");
            scene.Flow.HandleBack();
            Require(scene.Flow.CurrentSession.State == BattleState.Running, "明确继续后才能恢复");
        }
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke(); scene.Flow.HandleBack();
            Require(!Read<bool>(scene.Flow.Progress, "HasReadMarchSpeedIntro"), "未确认返回误标已读");
            Require(scene.Flow.CurrentSession.Speed == 2 && scene.Flow.CurrentSession.State == BattleState.Preparation,
                "返回关闭说明不得再次切倍率或擅自开战");
        }
    }

    private static void VerifyRoster(GameDatabase database)
    {
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            LevelDefinition level = database.FindLevel("huangjin");
            var roster = new List<string> { "guan_yu", "zhuge_liang" };
            scene.Flow.BeginBattle(level, roster);
            Invoke(scene.Flow, "AdjustRoster");
            Require(scene.Flow.PageKind == "Roster", "调整入口没有进入选将");
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 2 / 10"), "调整丢失上一局名单");
            ButtonNamed(scene.Root, "选将成长3").onClick.Invoke();
            Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 2 / 10"), "查看成长改变入队");
            ButtonLabel(scene.Root, "进入沙盘").onClick.Invoke();
            Require(scene.Flow.CurrentSession.Heroes.Count == 0 && scene.Flow.CurrentSession.Gold == level.InitialGold,
                "调整后新局不得免费带入旧部署");
            Require(scene.Flow.CurrentSession.IsInRoster("guan_yu") && scene.Flow.CurrentSession.IsInRoster("zhuge_liang"), "名单未传入新会话");
        }
    }

    private static void VerifyBattleReadability(GameDatabase source)
    {
        GameDatabase database = Clone(source); LevelDefinition level = database.FindLevel("huangjin"); level.InitialGold = 5000;
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "ma_chao" }); BattleSession b = scene.Flow.CurrentSession;
            Require(b.TryRecruit("ma_chao", level.Slots[0].Id) == null, "升级夹具招募失败"); scene.Flow.Tick(0);
            Button card = scene.Root.GetComponentsInChildren<Button>().Single(t => t.name == "战斗武将卡"); card.onClick.Invoke();
            // 选择会重建卡栏；断言始终读取当前实例。
            card = scene.Root.GetComponentsInChildren<Button>().Single(t => t.name == "战斗武将卡");
            Require(!card.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("已部署 ·")), "军令台重复部署与阶位");
            Text change = TextNamed(scene.Root, "成长变化");
            Require(change.text.Contains("枪线推退目标") && change.fontSize >= 22, "真实变化应优先且可读");
            string range = TextNamed(scene.Root, "成长状态").text;
            Require(!range.Contains("210 → 210") && !range.Contains("225 → 225"), "不变射程不能抢占升级变化");
            foreach (Button faction in scene.Root.GetComponentsInChildren<Button>().Where(t => t.name.StartsWith("军势_")))
            {
                Require(((RectTransform)faction.transform).rect.height >= 52 && faction.GetComponentInChildren<Text>().fontSize >= 20,
                    "军势入口热区或文字仍过小");
                RequireClickable(scene, faction);
            }
            UiBattleHeaderChecks.VerifyLayout(scene.Root, "竞速战斗顶栏");
        }
    }

    private static void VerifyUnitMarkers(GameDatabase database)
    {
        var root = new GameObject("辨识夹具", typeof(RectTransform));
        try
        {
            var ui = new UiFactory(); var enemy = new UnitView(ui, (RectTransform)root.transform, true);
            enemy.SetDefinition("Game/Enemies/infantry", "步兵", 112, false); enemy.Place(new Point2(100, 100), 0, 1, false, false, 0);
            Require(enemy.Root.Find("敌军识别") != null && enemy.Root.Find("敌军识别").gameObject.activeSelf, "敌军缺少独立形状识别");
            Require(!enemy.Root.GetComponentsInChildren<Text>().Any(t => t.text == "步兵"), "不恢复大姓名牌");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void VerifyResults(GameDatabase source)
    {
        foreach (bool breach in new[] { false, true })
        {
            GameDatabase database = ResultFixture(source, breach);
            using (var scene = new UiRefinementChecks.Scene(database))
            {
                LevelDefinition level = database.FindLevel("huangjin"); scene.Flow.BeginBattle(level, new List<string> { "lv_bu" });
                BattleSession b = scene.Flow.CurrentSession;
                if (!breach) Require(b.TryRecruit("lv_bu", 0) == null, "结算夹具招募失败");
                b.Start(); for (int i = 0; i < 1000 && b.State == BattleState.Running; i++) scene.Flow.Tick(.05f);
                Require(b.State == BattleState.Victory, "两种结果都应保留普通胜利");
                Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains(breach ? "未满足竞速资格" : "首次竞速纪录")), "结算资格反馈缺失");
                object record = Invoke(scene.Flow.Progress, "GetRaceRecord", level.Id, b.RuleVersion);
                Require((record != null) == !breach, "普通突破胜利污染竞速榜或合格成绩未保存");
                scene.Flow.CompleteBattle(b); Require(ReferenceEquals(b, scene.Flow.CurrentSession), "重复回调改变会话");
                ButtonLabel(scene.Root, "查看详情").onClick.Invoke();
                Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("倍率时长")), "详情缺真实倍率时长");
                if (breach) Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("首次突破")), "复盘缺首次失守事实");
                ButtonLabel(scene.Root, "调整阵容").onClick.Invoke();
                Require(scene.Flow.PageKind == "Roster" && scene.Root.GetComponentsInChildren<Text>().Any(t => t.text == "已选 1 / 10"), "结算调整未沿用名单");
                scene.Flow.BeginBattle(level, new List<string> { "lv_bu" }); scene.Flow.CompleteBattle(b);
                Require(scene.Flow.CurrentSession.State == BattleState.Preparation, "旧回调污染新局");
            }
        }
    }

    private static void CaptureStates(GameDatabase database)
    {
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            CaptureThree(scene, "main"); scene.Flow.ShowLevels(); CaptureThree(scene, "levels-empty-record");
            scene.Flow.ShowRoster(database.FindLevel("huangjin")); CaptureThree(scene, "roster-empty");
            ButtonLabel(scene.Root, "推荐阵容").onClick.Invoke(); CaptureThree(scene, "roster-recommended");
            scene.Begin(); CaptureThree(scene, "battle-empty");
            ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke(); CaptureThree(scene, "speed-intro-preparation");
            ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            ButtonNamed(scene.Root, "战场主操作").onClick.Invoke(); scene.Flow.Tick(.5f); CaptureThree(scene, "battle-running");
            scene.Flow.PauseBattle(); CaptureThree(scene, "battle-paused");
        }
        foreach (bool breach in new[] { false, true })
        {
            GameDatabase fixture = ResultFixture(database, breach);
            using (var scene = new UiRefinementChecks.Scene(fixture))
            {
                scene.Flow.BeginBattle(fixture.FindLevel("huangjin"), new List<string> { "lv_bu" }); BattleSession b = scene.Flow.CurrentSession;
                if (!breach) b.TryRecruit("lv_bu", 0);
                b.Start(); for (int i = 0; i < 1000 && b.State == BattleState.Running; i++) scene.Flow.Tick(.05f);
                string prefix = breach ? "boss-breach-victory" : "race-first-record";
                CaptureThree(scene, prefix); ButtonLabel(scene.Root, "查看详情").onClick.Invoke(); CaptureThree(scene, prefix + "-details");
            }
        }
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); CaptureSafeArea(scene, "safe-battle-asymmetric", new Rect(132, 0, 2100, 1080));
        }
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke();
            CaptureSafeArea(scene, "safe-intro-right-notch", new Rect(0, 0, 2220, 1080));
        }
        using (var scene = new UiRefinementChecks.Scene(ResultFixture(database, false)))
        {
            LevelDefinition level = scene.Flow.CampaignLevels.First(l => l.Id == "huangjin");
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu" }); scene.Flow.CurrentSession.TryRecruit("lv_bu", 0); Finish(scene);
            CaptureSafeArea(scene, "safe-result-bottom", new Rect(0, 84, 2400, 996));
        }
    }

    private static void CaptureSafeArea(UiRefinementChecks.Scene scene, string name, Rect safe)
    {
        const int width = 2400, height = 1080;
        ((RectTransform)scene.Root.parent).sizeDelta = new Vector2(width, height);
        scene.Camera.transform.position = new Vector3(width / 2f, -height / 2f, -100);
        SafeAreaLayout.Apply(scene.Root, width, height, safe);
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active, previousCamera = scene.Camera.targetTexture; Texture2D pixels = null;
        try
        {
            scene.Camera.targetTexture = target; scene.Camera.aspect = (float)width / height; Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; scene.Root.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(scene.Camera, corner);
                Require(screen.x >= safe.xMin - .1f && screen.x <= safe.xMax + .1f &&
                    screen.y >= safe.yMin - .1f && screen.y <= safe.yMax + .1f, "实际竞速页面超出模拟安全区");
            }
            foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "竞速安全区" + label.name);
            scene.Camera.Render(); RenderTexture.active = target; pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            string file = name + "-2400.png"; File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToPNG()); captured.Add(file);
        }
        finally
        {
            scene.Camera.targetTexture = previousCamera; RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Finish(UiRefinementChecks.Scene scene)
    {
        scene.Flow.CurrentSession.Start();
        for (int i = 0; i < 3000 && scene.Flow.CurrentSession.State == BattleState.Running; i++) scene.Flow.Tick(.05f);
    }

    private static bool HasText(RectTransform root, string text)
    { return root.GetComponentsInChildren<Text>().Any(t => t.text.Contains(text)); }

    private static void VerifyComparisons(GameDatabase source)
    {
        GameDatabase database = ResultFixture(source, false); LevelDefinition level = database.FindLevel("huangjin");
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            float[] delays = { 3, 6, 3, 1 }; string[] expected = { "首次竞速纪录", "慢于竞速最快", "与竞速最快相同", "刷新竞速纪录" };
            string[] names = { "first", "slower", "equal", "improved" };
            for (int run = 0; run < delays.Length; run++)
            {
                level.Waves[0].StartDelay = delays[run]; scene.Flow.BeginBattle(level, new List<string> { "lv_bu" });
                Require(scene.Flow.CurrentSession.TryRecruit("lv_bu", 0) == null, "比较夹具部署失败"); Finish(scene);
                Require(scene.Flow.CurrentSession.IsRaceEligible && HasText(scene.Root, expected[run]), "结算没有正确区分" + names[run]);
                if (run > 0) Require(HasText(scene.Root, "00:03."), "此前最快未保留原值");
                RequireClickable(scene, ButtonLabel(scene.Root, "调整阵容"));
                RequireClickable(scene, ButtonLabel(scene.Root, "查看详情"));
                CaptureThree(scene, "record-" + names[run]);
            }
            scene.Flow.ShowLevels(); Require(HasText(scene.Root, "竞速最快 00:01."), "选关没有读取新竞速最快");
            CaptureThree(scene, "levels-race-record");
        }
        // 在备战中真实部署、调位制造多页摘要；不伪造日志。
        database = ResultFixture(source, false); level = database.FindLevel("huangjin");
        level.Slots.Add(new SlotDefinition { Id = 1, Position = new Point2(650, 550) });
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu" }); BattleSession b = scene.Flow.CurrentSession;
            Require(b.TryRecruit("lv_bu", 0) == null, "摘要夹具部署失败"); int slot = 0;
            for (int i = 0; i < 9; i++) { Require(b.TryMoveHero(slot, 1 - slot) == null, "摘要调位失败"); slot = 1 - slot; }
            Finish(scene); ButtonLabel(scene.Root, "查看详情").onClick.Invoke();
            Require(ButtonNamed(scene.Root, "竞速摘要下一页").interactable, "长摘要没有翻页");
            ButtonNamed(scene.Root, "竞速摘要下一页").onClick.Invoke(); Require(HasText(scene.Root, "摘要 2 /"), "下一页未前进");
            RequireClickable(scene, ButtonNamed(scene.Root, "竞速摘要上一页"));
            CaptureThree(scene, "summary-next-page");
            ButtonNamed(scene.Root, "竞速摘要上一页").onClick.Invoke(); Require(HasText(scene.Root, "摘要 1 /"), "上一页未回退");
        }
    }

    private static void VerifyStorageFailures(GameDatabase source)
    {
        var values = new Dictionary<string, int>(); bool rejectSave = true;
        var progress = new GameProgress((key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
            (key, value) => values[key] = value, () => { if (rejectSave) throw new IOException("验收注入保存失败"); });
        GameDatabase database = ResultFixture(source, false); LevelDefinition level = database.FindLevel("huangjin");
        using (var scene = new UiRefinementChecks.Scene(database, progress))
        {
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu" });
            ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke(); ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(!progress.HasReadMarchSpeedIntro, "说明保存失败仍标已读");
            Require(HasText(scene.Root, "倍率说明未能保存"), "说明保存失败必须轻量提示");
            ButtonNamed(scene.Root, "进军倍率2").onClick.Invoke(); Require(!HasText(scene.Root, "以压力换时间"), "重复当前档不得重入首次说明");
            ButtonNamed(scene.Root, "进军倍率1").onClick.Invoke();
            ButtonNamed(scene.Root, "进军倍率3").onClick.Invoke(); Require(HasText(scene.Root, "以压力换时间"), "未保存说明无法重读");
            ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(scene.Flow.CurrentSession.TryRecruit("lv_bu", 0) == null, "保存异常夹具部署失败"); Finish(scene);
            Require(HasText(scene.Root, "竞速纪录未保存") && !HasText(scene.Root, "首次竞速纪录"), "保存失败结算中断或误报新纪录");
            Require(progress.GetRaceRecord(level.Id, scene.Flow.CurrentSession.RuleVersion) == null, "保存失败留下已提交新榜");
            CaptureThree(scene, "storage-save-failed"); rejectSave = false;
            ButtonLabel(scene.Root, "重新推演").onClick.Invoke(); Require(scene.Flow.CurrentSession.State == BattleState.Preparation, "异常后重试不可用");
        }
    }

    private static void VerifyMoreStates(GameDatabase source)
    {
        using (var scene = new UiRefinementChecks.Scene(source))
        {
            scene.Begin(); BattleSession b = scene.Flow.CurrentSession;
            // 用真实按钮部署、选中与升级，检查缺钱时没有误扣款。
            ButtonNamed(scene.Root, "战斗武将卡").onClick.Invoke();
            RectTransform slots = scene.Root.GetComponentsInChildren<RectTransform>().First(t => t.name == "可交互阵位");
            slots.GetChild(0).GetComponent<Button>().onClick.Invoke();
            Button upgrade = ButtonLabel(scene.Root, "升级 ");
            while (upgrade.interactable && b.Heroes[0].Level < 3)
            { upgrade.onClick.Invoke(); upgrade = ButtonLabel(scene.Root, "升级 "); }
            CaptureThree(scene, "battle-unaffordable-or-max");
        }
        GameDatabase database = Clone(source); LevelDefinition level = database.FindLevel("huangjin"); level.InitialGold = 5000;
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "ma_chao" }); BattleSession b = scene.Flow.CurrentSession;
            b.TryRecruit("ma_chao", level.Slots[0].Id); b.TryUpgrade(level.Slots[0].Id); b.TryUpgrade(level.Slots[0].Id); scene.Flow.Tick(0);
            ButtonNamed(scene.Root, "战斗武将卡").onClick.Invoke(); Require(HasText(scene.Root, "已满级"), "满阶状态不明确");
            CaptureThree(scene, "battle-max-tier");
        }
        database = ResultFixture(source, true); level = database.FindLevel("huangjin");
        level.Waves[0].Groups[0].Count = 2; level.Waves[0].Groups[0].Interval = .03f;
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu" }); Finish(scene);
            Require(scene.Flow.CurrentSession.State == BattleState.Defeat && HasText(scene.Root, "此局失守"), "失败结算缺失");
            Require(!HasText(scene.Root, "战役已结束") && !ButtonLabel(scene.Root, "下一关需先通关").interactable,
                "非末关失败不能显示战役结束或解锁下一关");
            Require(scene.Flow.Progress.GetRaceRecord(level.Id, scene.Flow.CurrentSession.RuleVersion) == null, "失败污染竞速最快");
            CaptureThree(scene, "battle-defeat");
        }
        database = ResultFixture(source, false, "finale"); level = database.FindLevel("finale");
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "lv_bu" });
            scene.Flow.CurrentSession.TryRecruit("lv_bu", 0); Finish(scene);
            Require(scene.Flow.CurrentSession.IsRaceEligible && !ButtonLabel(scene.Root, "战役已结束").interactable,
                "末关胜利导航没有明确结束状态");
            CaptureThree(scene, "finale-victory");
        }
        database = Clone(source); level = database.FindLevel("huangjin");
        // 高生命仅用于画面压力夹具，正式database.json和策略模拟均未修改。
        database.Enemies.First(e => !e.IsBoss).MaxHealth = 100000;
        level.InitialGold = 10000;
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "密集与敌将显示夹具", StartDelay = .1f,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = database.Enemies.First(e => !e.IsBoss).Id, RouteId = level.Routes[0].Id, Count = 45, Interval = .2f },
                new SpawnGroup { EnemyId = "boss_zhang_liao", RouteId = level.Routes[0].Id, Count = 1, Interval = .1f } } } };
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Begin(); BattleSession b = scene.Flow.CurrentSession;
            for (int i = 0; i < level.RecommendedRoster.Count; i++) Require(b.TryRecruit(level.RecommendedRoster[i], level.Slots[i].Id) == null, "满阵夹具部署失败");
            b.Start(); for (int i = 0; i < 180; i++) scene.Flow.Tick(.05f);
            Require(scene.Flow.CurrentSession.Enemies.Count >= 20, "密集敌军夹具未实际生成");
            Require(b.Heroes.Count == 10, "未覆盖满10将");
            CaptureThree(scene, "battle-dense-boss");
            ButtonNamed(scene.Root, "战斗规则").onClick.Invoke();
            Require(scene.Flow.CurrentSession.State == BattleState.Paused && HasText(scene.Root, "全部敌将击退"), "运行中规则没有暂停或资格说明");
            CaptureThree(scene, "rules-running-paused"); ButtonNamed(scene.Root, "倍率说明确认").onClick.Invoke();
            Require(scene.Flow.CurrentSession.State == BattleState.Running, "主动查看规则确认后未回战斗");
        }
    }

    private static void RequireClickable(UiRefinementChecks.Scene scene, Button button)
    {
        Canvas.ForceUpdateCanvases();
        // WorldSpace批处理夹具先渲染，Graphic深度才可用于真实射线排序。
        scene.Camera.Render();
        RectTransform rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(scene.Camera, rect.TransformPoint(rect.rect.center));
        var hits = new List<RaycastResult>();
        scene.Raycaster.Raycast(new PointerEventData(null) { position = point }, hits);
        Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, button.name + "真实射线被其他界面遮挡：" +
            (hits.Count == 0 ? "无命中" : hits[0].gameObject.name));
    }

    internal static void CaptureThree(UiRefinementChecks.Scene scene, string name)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
        {
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
            RenderTexture previous = RenderTexture.active, previousCamera = scene.Camera.targetTexture; Texture2D pixels = null;
            try
            {
                scene.Camera.targetTexture = target; scene.Camera.aspect = (float)size.x / size.y; Canvas.ForceUpdateCanvases();
                foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                    if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "竞速" + label.name);
                scene.Camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                string file = name + "-" + size.x + ".png";
                File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToPNG()); captured.Add(file);
            }
            finally
            {
                scene.Camera.targetTexture = previousCamera; RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    private static GameDatabase ResultFixture(GameDatabase source, bool breach, string levelId = "huangjin")
    {
        GameDatabase database = Clone(source); LevelDefinition level = database.FindLevel(levelId);
        level.InitialGold = 5000; level.IncomePerSecond = 0; level.BossHeroIds.Clear();
        level.Routes = new List<RouteDefinition> { new RouteDefinition { Id = "race_fixture", Points = new List<Point2> { new Point2(550, 500), new Point2(950, 500) } } };
        level.Slots = new List<SlotDefinition> { new SlotDefinition { Id = 0, Position = new Point2(500, 500) } };
        level.Waves = new List<WaveDefinition> { new WaveDefinition { Name = "竞速资格隔离夹具", StartDelay = .1f,
            Groups = new List<SpawnGroup> { new SpawnGroup { EnemyId = "boss_zhang_liao", RouteId = "race_fixture", Count = 1 } } } };
        EnemyDefinition boss = database.FindEnemy("boss_zhang_liao"); boss.MaxHealth = breach ? 100000 : 1; boss.Speed = breach ? 5000 : 0;
        HeroDefinition hero = database.FindHero("lv_bu"); hero.Damage = 10000; hero.Range = 2000;
        return database;
    }
    private static GameDatabase Clone(GameDatabase database) { return JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(database)); }
    private static void SaveReport(Report report) { File.WriteAllText(Path.Combine(Output, "verification.json"), JsonUtility.ToJson(report, true)); }
    private static void Check(List<string> failures, string name, Action action)
    { try { action(); Debug.Log("竞速检查通过：" + name); } catch (Exception e) { failures.Add(name + "：" + e.Message); Debug.LogWarning("竞速检查失败：" + name + "：" + e); } }
    private static void Require(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static T Read<T>(object instance, string property)
    { PropertyInfo member = instance.GetType().GetProperty(property); Require(member != null, "缺少属性" + property); return (T)member.GetValue(instance, null); }
    private static object Invoke(object instance, string method, params object[] arguments)
    { MethodInfo member = instance.GetType().GetMethod(method); Require(member != null, "缺少入口" + method); return member.Invoke(instance, arguments); }
    private static Button ButtonNamed(RectTransform root, string name)
    { Button result = root.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name); Require(result != null, "缺少按钮" + name); return result; }
    private static Button ButtonLabel(RectTransform root, string prefix)
    { Button result = root.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal)); Require(result != null, "缺少操作" + prefix); return result; }
    private static Text TextNamed(RectTransform root, string name) { return root.GetComponentsInChildren<Text>().Single(t => t.name == name); }
}
