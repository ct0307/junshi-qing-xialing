using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 全流程真实页面检查；截图使用隔离进度，不写入玩家存档。
public static class UiAppStyleChecks
{
    public static void Verify(GameDatabase database, string output)
    {
        Directory.CreateDirectory(output);
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            Capture(scene, output, "main", "军师请下令");
            scene.Flow.ShowLevels();
            Capture(scene, output, "levels", "选择战役");
            foreach (LevelDefinition item in database.Levels)
            {
                scene.Flow.ShowIntel(item);
                Capture(scene, output, "intel-" + item.Id, item.Name + " · 敌将情报");
            }
            LevelDefinition level = database.FindLevel("huangjin");
            scene.Flow.ShowRoster(level);
            Require(!FindButton(scene.Root, "进入沙盘").interactable, "空阵容应禁用进入沙盘");
            Capture(scene, output, "roster-empty", "战前选将");
            Click(scene.Root, "推荐阵容");
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "选将卡_zhao_yun").onClick.Invoke();
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "选将成长3").onClick.Invoke();
            Require(FindButton(scene.Root, "进入沙盘").interactable, "推荐阵容无法进入沙盘");
            Capture(scene, output, "roster-selected", "战前选将");
            Click(scene.Root, "进入沙盘");
            UiBattleHeaderChecks.VerifyLayout(scene.Root, "全流程进入沙盘");
            scene.Flow.HandleBack();
            Capture(scene, output, "preparation-exit", "离开布阵");
            Click(scene.Root, "继续布阵");
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "战场主操作").onClick.Invoke();
            scene.Flow.Tick(.5f);
            scene.Flow.PauseBattle();
            double before = scene.Flow.CurrentSession.ElapsedSeconds;
            Capture(scene, output, "pause", "推演暂停");
            scene.Flow.ShowAudioSettings();
            Capture(scene, output, "audio-paused", "声音设置");
            scene.Flow.Tick(3);
            Require(scene.Flow.CurrentSession.ElapsedSeconds == before, "设置页推进了战斗时钟");
            Click(scene.Root, "完成");
            Click(scene.Root, "继续守关");
            Require(scene.Flow.CurrentSession.State == BattleState.Running, "设置关闭后无法继续同一会话");
            scene.Flow.ShowMainMenu();
            scene.Flow.ShowAudioSettings();
            Capture(scene, output, "audio-menu", "声音设置");
            Click(scene.Root, "完成");
            scene.Flow.Dispose();
            int retries = 0;
            ContentErrorView.Create(new UiFactory(), scene.Root, "未找到关卡内容，请重试加载。", () => retries++);
            Capture(scene, output, "content-error", "内容加载失败");
            Click(scene.Root, "重试加载");
            Require(retries == 1, "错误页没有调用重试入口");
        }
        VerifyLongestIntel(database, output);
        Debug.Log("全流程主题检查通过：宋体标题、浅绢面板、直边按钮、五关情报、空阵容、三阶查看、暂停设置恢复与错误重试；最长混合敌情三尺寸实图");
    }

    private static void VerifyLongestIntel(GameDatabase original, string output)
    {
        // 只在深拷贝中把末波放到备战预告，不改变正式关卡数据或战斗难度。
        GameDatabase database = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(original));
        LevelDefinition level = database.FindLevel("finale");
        WaveDefinition lastWave = level.Waves[level.Waves.Count - 1];
        level.Waves = new List<WaveDefinition> { lastWave };
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            UiBattleHeaderChecks.VerifyLayout(scene.Root, "群雄末波长敌情隔离夹具");
            Text brief = scene.Root.GetComponentsInChildren<Text>().First(text => text.name == "战况摘要");
            Require(brief.text.Contains("执矛步兵") && brief.text.Contains("司马懿"), "未捕获末波完整混合敌情");
            Capture(scene, output, "long-intel-fixture", "群雄终局");
        }
    }

    private static void Capture(UiRefinementChecks.Scene scene, string output, string name, string title)
    {
        // 弹窗后面的战场阵位使用独立命中图形，不属于本轮菜单按钮形状约束。
        Transform styleRoot = scene.Root;
        RectTransform[] containers = scene.Root.GetComponentsInChildren<RectTransform>();
        RectTransform modal = containers.FirstOrDefault(rect => rect.name == "声音设置面板") ??
            containers.FirstOrDefault(rect => rect.name == "弹窗遮罩");
        if (modal != null) styleRoot = modal;
        Text heading = styleRoot.GetComponentsInChildren<Text>().FirstOrDefault(label => label.text == title && label.font == BattleTypography.DisplayFont);
        Require(heading != null, name + "标题没有使用已批准宋体");
        foreach (Button button in styleRoot.GetComponentsInChildren<Button>())
        {
            Image background = button.GetComponent<Image>();
            if (button.GetComponent<SlotHitArea>() != null) continue;
            Require(background != null && background.sprite == null, name + "按钮仍使用旧圆角：" + button.name);
        }
        foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2400, 1080) })
        {
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousCamera = scene.Camera.targetTexture;
            float previousAspect = scene.Camera.aspect;
            Texture2D pixels = null;
            try
            {
                scene.Camera.targetTexture = target;
                scene.Camera.aspect = (float)size.x / size.y;
                Canvas.ForceUpdateCanvases();
                foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                    if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, name + "/" + label.text);
                scene.Camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + "-" + size.x + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                scene.Camera.targetTexture = previousCamera;
                scene.Camera.aspect = previousAspect;
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    private static Button FindButton(RectTransform root, string prefix)
    { return root.GetComponentsInChildren<Button>().First(button => button.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal)); }
    private static void Click(RectTransform root, string prefix) { FindButton(root, prefix).onClick.Invoke(); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
