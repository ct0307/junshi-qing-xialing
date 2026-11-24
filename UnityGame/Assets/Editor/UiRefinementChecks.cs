using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 使用真实界面、射线和隔离记录验收，避免把按钮尺寸当作实际输入已通过。
public static class UiRefinementChecks
{
    public static string Output { get; private set; } = Path.GetFullPath("../docs/ui/runtime-ui-refinement-20261108");

    // 专项验收临时切换输出目录，结束后恢复默认路径，避免覆盖历史截图。
    public static void VerifyBattle(GameDatabase database, string output)
    {
        string previous = Output;
        try
        {
            Output = Path.GetFullPath(output);
            VerifyBattle(database);
        }
        finally { Output = previous; }
    }

    public static void Run()
    {
        var database = JsonUtility.FromJson<GameDatabase>(Resources.Load<TextAsset>("Game/database").text);
        var failures = new List<string>();
        Check(failures, "战斗专项", () => VerifyBattle(database));
        Check(failures, "菜单专项", () => UiRefinementMenuChecks.Verify(database));
        Check(failures, "结算专项", () => UiRefinementResultChecks.Verify(database));
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures.ToArray()));
        Debug.Log("本轮界面精修专项通过");
    }

    public static void VerifyBattle(GameDatabase database)
    {
        Directory.CreateDirectory(Output);
        var failures = new List<string>();
        Check(failures, "战斗信息分层", () =>
        {
            using (var scene = new Scene(database))
            {
                scene.Begin();
                string income = NamedText(scene.Root, "军饷实秒收入").text;
                Require(income.Contains("/实秒") && !income.Contains("基础"), "顶栏应只常驻当前实秒收入，基础口径移至规则");
            }
        });
        Check(failures, "稳定开战与取消层级", () =>
        {
            using (var scene = new Scene(database))
            {
                scene.Begin();
                Button start = NamedButton(scene.Root, "战场主操作");
                Require(start != null && start.interactable, "备战缺少固定命名的开战入口");
                Require(Same(start.image.color, BattlePalette.Jade), "备战开战入口应为青玉主按钮");
                SelectHero(scene.Root, "关羽");
                Require(start.interactable && start.GetComponentInChildren<Text>().text == "开战", "选择武将不应改变开战入口");
                Button cancel = FindButton(scene.Root, "取消选择");
                Require(Same(cancel.image.color, BattlePalette.Mist), "取消选择应为浅玉次操作");
                cancel.onClick.Invoke();
                SelectHero(scene.Root, "关羽");
                ClickSlot(scene.Root, 0);
                Require(start.interactable && start.GetComponentInChildren<Text>().text == "开战", "已部署守将仍应能从固定入口开战");
                start.onClick.Invoke();
                Require(scene.Flow.CurrentSession.State == BattleState.Running, "固定开战未推进真实会话");
                start.onClick.Invoke();
                Require(scene.Flow.CurrentSession.State == BattleState.Paused, "固定入口未切换为暂停");
            }
        });
        Check(failures, "热区与相邻阵位射线", () => VerifyTargets(database));
        Check(failures, "规则阅读与返回恢复", () =>
        {
            using (var scene = new Scene(database))
            {
                scene.Begin();
                Button rules = NamedButton(scene.Root, "战斗规则");
                Require(rules != null, "战斗缺少按需查看的规则入口");
                Require(((RectTransform)rules.transform).rect.height >= 88, "规则入口热区高度不足88");
                rules.onClick.Invoke();
                Require(scene.Root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("我方攻速") && t.text.Contains("进军")), "规则弹窗缺少倍率边界");
                Require(scene.Flow.CurrentSession.State == BattleState.Preparation, "备战阅读不应开战");
                scene.Flow.HandleBack();
                Require(!scene.Root.GetComponentsInChildren<RectTransform>().Any(t => t.name == "战斗规则弹窗"), "返回未关闭规则");
                NamedButton(scene.Root, "战场主操作").onClick.Invoke();
                scene.Flow.Tick(.5f);
                BattleSession session = scene.Flow.CurrentSession;
                double elapsed = session.ElapsedSeconds;
                rules.onClick.Invoke();
                Require(session.State == BattleState.Paused, "运行中规则阅读未暂停");
                scene.Flow.Tick(3);
                Require(session.ElapsedSeconds == elapsed, "阅读规则推进了真实计时");
                CaptureThree(scene, "battle-rules");
                scene.Flow.HandleBack();
                Require(ReferenceEquals(scene.Flow.CurrentSession, session) && session.State == BattleState.Running, "关闭规则未恢复同一会话");
            }
        });
        if (failures.Count > 0) throw new InvalidOperationException("界面精修专项失败：\n" + string.Join("\n", failures.ToArray()));
        CaptureStates(database);
    }

    private static void VerifyTargets(GameDatabase database)
    {
        foreach (LevelDefinition level in database.Levels)
        using (var scene = new Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            Transform slots = scene.Root.GetComponentsInChildren<RectTransform>().First(t => t.name == "可交互阵位");
            Canvas.ForceUpdateCanvases();
            scene.Camera.Render();
            foreach (Transform slot in slots)
            {
                RectTransform rect = (RectTransform)slot;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Require(Vector3.Distance(corners[0], corners[1]) >= 87.9f, level.Name + "阵位热区被地面纵缩");
                foreach (Vector2 fraction in new[] { new Vector2(.5f, .5f), new Vector2(.1f, .1f),
                    new Vector2(.9f, .1f), new Vector2(.1f, .9f), new Vector2(.9f, .9f) })
                {
                    Vector3 world = rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, fraction.x),
                        Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, fraction.y), 0));
                    // 重叠点应交给屏幕上更近的有效阵位，而非固定由最后绘制者拿走。
                    Transform expected = slots.Cast<Transform>().Where(candidate =>
                        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)candidate,
                            RectTransformUtility.WorldToScreenPoint(scene.Camera, world), scene.Camera))
                        .OrderBy(candidate => Vector3.SqrMagnitude(((RectTransform)candidate).TransformPoint(((RectTransform)candidate).rect.center) - world))
                        .ThenBy(candidate => level.Slots[candidate.GetSiblingIndex()].Id).First();
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(scene.Camera, world);
                    var results = new List<RaycastResult>();
                    scene.Raycaster.Raycast(new PointerEventData(null) { position = point }, results);
                    RectTransform viewport = scene.Root.GetComponentsInChildren<RectTransform>().First(t => t.name == "沙盘视口");
                    if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, point, scene.Camera))
                    {
                        Require(!results.Any(hit => hit.gameObject.transform.IsChildOf(slots)), "沙盘外被裁剪的热区不应接收阵位输入");
                        continue;
                    }
                    Require(results.Count > 0 && results[0].gameObject == expected.gameObject, level.Name + "阵位" + slot.GetSiblingIndex() + "矩形命中分配错误，采样" + fraction +
                        "，预期索引" + expected.GetSiblingIndex() + "，实际" + (results.Count == 0 ? "无" : results[0].gameObject.transform.GetSiblingIndex().ToString()) +
                        "，世界点" + world);
                }
            }
            Button detail = NamedButton(scene.Root, "武将详情");
            Require(detail != null && ((RectTransform)detail.transform).rect.height >= 88, "武将详情热区高度不足");
            foreach (string name in new[] { "武将详情", "战斗规则", "战场主操作" })
            {
                Button button = NamedButton(scene.Root, name);
                RectTransform rect = (RectTransform)button.transform;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(scene.Camera, rect.TransformPoint(rect.rect.center));
                var hits = new List<RaycastResult>();
                scene.Raycaster.Raycast(new PointerEventData(null) { position = point }, hits);
                Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, name + "的真实射线被其他控件遮挡");
            }
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "战斗武将卡").onClick.Invoke();
            for (int index = 0; index < 2; index++)
            {
                EventTrigger trigger = slots.GetChild(index).GetComponent<EventTrigger>();
                trigger.triggers.First(entry => entry.eventID == EventTriggerType.PointerEnter).callback.Invoke(new PointerEventData(null));
            }
            Require(slots.GetChild(0).Find("阵位底座").GetComponent<Image>().color.a <
                slots.GetChild(1).Find("阵位底座").GetComponent<Image>().color.a, "切换候选后旧阵位仍保留同等强度的高亮");
        }
    }

    private static void CaptureStates(GameDatabase database)
    {
        using (var scene = new Scene(database))
        {
            CaptureThree(scene, "main");
            scene.Flow.ShowLevels(); CaptureThree(scene, "levels");
            scene.Flow.ShowRoster(database.FindLevel("huangjin")); CaptureThree(scene, "roster-empty");
            FindButton(scene.Root, "推荐阵容").onClick.Invoke();
            NamedButton(scene.Root, "选将卡_zhao_yun").onClick.Invoke();
            NamedButton(scene.Root, "选将成长2").onClick.Invoke();
            CaptureThree(scene, "roster-focus");
            scene.Begin(); CaptureThree(scene, "battle-empty");
            SelectHero(scene.Root, "关羽"); CaptureThree(scene, "battle-placement");
            ClickSlot(scene.Root, 0); CaptureThree(scene, "battle-deployed");
            SelectHero(scene.Root, "黄忠"); ClickSlot(scene.Root, 1);
            FindButton(scene.Root, "升级 ").onClick.Invoke();
            CaptureThree(scene, "battle-unaffordable");
            NamedButton(scene.Root, "战场主操作").onClick.Invoke();
            scene.Flow.Tick(1); CaptureThree(scene, "battle-running");
        }
    }

    private static void Check(List<string> failures, string label, Action check)
    {
        try { check(); Debug.Log("精修检查通过：" + label); }
        catch (Exception error) { failures.Add(label + "：" + error.Message); Debug.LogWarning("精修预期检查：" + label + "：" + error.Message); }
    }
    private static bool Same(Color a, Color b) { return Vector4.Distance(a, b) < .01f; }
    private static Button NamedButton(RectTransform root, string name)
    { return root.GetComponentsInChildren<Button>().FirstOrDefault(t => t.name == name); }
    private static Text NamedText(RectTransform root, string name)
    { return root.GetComponentsInChildren<Text>().First(t => t.name == name); }
    private static Button FindButton(RectTransform root, string prefix)
    { return root.GetComponentsInChildren<Button>().First(t => t.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal)); }
    private static void SelectHero(RectTransform root, string name)
    {
        Button card = root.GetComponentsInChildren<Button>().First(t => t.name == "战斗武将卡" && t.GetComponentsInChildren<Text>().Any(label => label.text == name));
        card.onClick.Invoke();
    }
    private static void ClickSlot(RectTransform root, int index)
    { root.GetComponentsInChildren<RectTransform>().First(t => t.name == "可交互阵位").GetChild(index).GetComponent<Button>().onClick.Invoke(); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void CaptureThree(Scene scene, string name)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
        {
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousCamera = scene.Camera.targetTexture;
            Texture2D pixels = null;
            try
            {
                scene.Camera.targetTexture = target; scene.Camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases();
                foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                    if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "本轮精修" + label.name);
                scene.Camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + "-" + size.x + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                scene.Camera.targetTexture = previousCamera; RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    public sealed class Scene : IDisposable
    {
        public readonly Camera Camera;
        public readonly RectTransform Root;
        public readonly GameFlow Flow;
        public readonly GraphicRaycaster Raycaster;
        private readonly GameObject canvasObject;
        private readonly GameDatabase database;
        private readonly RenderTexture rayTarget;
        public Scene(GameDatabase database, GameProgress progress = null)
        {
            this.database = database;
            canvasObject = new GameObject("本轮精修验收", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvas.transform; rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(1920, 1080);
            Camera = new GameObject("精修截图相机", typeof(Camera)).GetComponent<Camera>();
            Camera.orthographic = true; Camera.orthographicSize = 540; Camera.transform.position = new Vector3(960, -540, -100);
            rayTarget = RenderTexture.GetTemporary(1920, 1080, 24); Camera.targetTexture = rayTarget; Camera.aspect = 16f / 9f;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = UiFactory.Deep;
            Camera.nearClipPlane = .1f; Camera.farClipPlane = 200; canvas.worldCamera = Camera;
            Raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            var ui = new UiFactory(); Root = ui.Rect(rect, "精修逻辑画布", 0, 0, 1920, 1080);
            var values = new Dictionary<string, int>();
            if (progress == null) progress = new GameProgress((key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => values[key] = value, () => { });
            Flow = new GameFlow(ui, Root, database, progress);
        }
        public void Begin()
        { LevelDefinition level = database.FindLevel("huangjin"); Flow.BeginBattle(level, new List<string>(level.RecommendedRoster)); }
        public void Dispose()
        { Flow.Dispose(); UnityEngine.Object.DestroyImmediate(canvasObject); Camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(Camera.gameObject); RenderTexture.ReleaseTemporary(rayTarget); }
    }
}
