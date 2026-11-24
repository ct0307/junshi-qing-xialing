using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 隔离夹具通过真实战斗产生攻击与击退；实际关卡的密集画面由完整黄巾流程单独采集。
public static class UiBattleImpactChecks
{
    private static readonly string Output = Path.GetFullPath("../docs/ui/runtime-impact");

    public static void Verify(GameDatabase database)
    { Verify(database, Output); }

    public static void Verify(GameDatabase database, string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        VerifyAttack(database, "lv_bu", output);
        VerifyAttack(database, "zhao_yun", output);
        Debug.Log("击破表现集成检查通过：真实按钮招募、自然群攻击退、连破计数边界及三尺寸截图；隔离敌军参数不代表实际关卡难度");
    }

    public static void CaptureNatural(Camera camera, RectTransform root)
    {
        CaptureThree(camera, root, "huangjin-density", Output);
    }

    private static void VerifyAttack(GameDatabase original, string heroId, string output)
    {
        // 只复制夹具，绝不改写实际资源或用于通关回归的database。
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("huangjin");
        level.Id = "impact_fixture";
        level.Name = "攻击表现验证";
        level.Subtitle = "隔离夹具 · 非实际关卡";
        level.InitialGold = 1000;
        level.Routes = new List<RouteDefinition>
        {
            new RouteDefinition { Id = "near", Points = new List<Point2> { new Point2(810, 510), new Point2(1220, 510) } },
            new RouteDefinition { Id = "far", Points = new List<Point2> { new Point2(1520, 680), new Point2(1700, 680) } }
        };
        level.Slots = new List<SlotDefinition> { new SlotDefinition { Id = 0, Position = new Point2(700, 500) } };
        level.Waves = new List<WaveDefinition>
        {
            new WaveDefinition { Name = "局部群攻验证", StartDelay = 0, Groups = new List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "infantry", RouteId = "near", Count = 6, Interval = .02f },
                new SpawnGroup { EnemyId = "infantry", RouteId = "far", Count = 1, Interval = 1 }
            } }
        };
        database.FindEnemy("infantry").MaxHealth = 10;
        database.FindEnemy("infantry").Speed = .01f;

        GameObject canvasObject = new GameObject("击破集成验证", typeof(RectTransform), typeof(Canvas));
        Camera camera = new GameObject("击破截图相机", typeof(Camera)).GetComponent<Camera>();
        BattleView battle = null;
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.pivot = new Vector2(0, 1);
            canvasRect.sizeDelta = new Vector2(1920, 1080);
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.transform.position = new Vector3(960, -540, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Deep;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 200;
            canvas.worldCamera = camera;
            var ui = new UiFactory();
            RectTransform root = ui.Rect(canvasRect, "击破验证逻辑画布", 0, 0, 1920, 1080);
            battle = new BattleView(null, ui, root, database, level, new List<string> { heroId });
            ui.Label(root, "隔离表现验证：敌军参数仅用于展示，不代表实际关卡难度", 340, 174, 1240, 34, 21, UiFactory.Gold, TextAnchor.MiddleCenter);

            // 先自然刷齐目标，再招募；避免更快普攻提前逐个清空群攻测试的敌群。
            root.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text.StartsWith("开战", StringComparison.Ordinal)).onClick.Invoke();
            battle.Tick(.3f);
            Require(battle.Session.Enemies.Count == 7, "群攻夹具应先形成六名近敌与一名远敌");

            RectTransform cards = (RectTransform)root.Find("五张武将卡");
            Button card = cards.GetComponentsInChildren<Button>().First(b => b.GetComponentsInChildren<Text>().Any(t => t.text == database.FindHero(heroId).Name));
            InvokeRebuilding(card, cards);
            Button slot = root.Find("沙盘视口/战场坐标层/可交互阵位").GetChild(0).GetComponent<Button>();
            InvokeRebuilding(slot, cards);
            Require(battle.Session.Heroes.Count == 1 && battle.Session.GoldSpent == database.FindHero(heroId).Cost,
                "表现夹具未通过真实按钮按正常费用招募");

            bool capturedAttack = false, capturedCombo = false;
            for (int step = 0; step < 150 && !(capturedAttack && capturedCombo); step++)
            {
                battle.Tick(1f / 30f);
                if (!capturedAttack && battle.Session.DefeatedCount >= 3)
                {
                    CaptureThree(camera, root, heroId + "-attack", output);
                    capturedAttack = true;
                }
                Text combo = root.GetComponentsInChildren<Text>().FirstOrDefault(t => t.text.StartsWith("连破 ×", StringComparison.Ordinal));
                if (!capturedCombo && combo != null)
                {
                    int count;
                    Require(int.TryParse(combo.text.Substring("连破 ×".Length), out count) && count >= 3 && count <= battle.Session.DefeatedCount,
                        "连破数量必须来自实际击退，不能超过会话累计击退数");
                    CaptureThree(camera, root, heroId + "-combo", output);
                    capturedCombo = true;
                }
            }
            Require(capturedAttack && capturedCombo, heroId + "未自然产生群攻击退及连破画面");
            Require(battle.Session.State == BattleState.Running && battle.Session.CastleHealth == level.CastleMaxHealth,
                "远处守候敌军应使表现夹具保持运行，不能靠终局后注入反馈截图");
            Debug.Log("自然群攻击退验证：" + heroId + "，实际击退" + battle.Session.DefeatedCount + "，消费" + battle.Session.GoldSpent);
        }
        finally
        {
            if (battle != null) battle.Dispose();
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    private static void InvokeRebuilding(Button button, RectTransform cards)
    {
        var callback = button.onClick;
        for (int i = cards.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(cards.GetChild(i).gameObject);
        callback.Invoke();
    }

    private static void CaptureThree(Camera camera, RectTransform root, string name, string output)
    {
        Directory.CreateDirectory(output);
        Capture(camera, root, name + "-1920", 1920, 1080, output);
        Capture(camera, root, name + "-1280", 1280, 720, output);
        Capture(camera, root, name + "-2400", 2400, 1080, output);
    }

    private static void Capture(Camera camera, RectTransform root, string name, int width, int height, string output)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            foreach (Text label in root.GetComponentsInChildren<Text>())
                if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "击破表现" + label.gameObject.name);
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
