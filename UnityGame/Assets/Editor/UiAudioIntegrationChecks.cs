using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 使用真实菜单组件验证声音设置入口；不读取或写入玩家音量偏好。
public static class UiAudioIntegrationChecks
{
    public static void RunFromCommandLine()
    {
        var source = Resources.Load<TextAsset>("Game/database");
        Require(source != null, "音频界面检查缺少游戏配置");
        Verify(JsonUtility.FromJson<GameDatabase>(source.text));
    }

    public static void Verify(GameDatabase database)
    {
        GameObject holder = new GameObject("音频设置界面检查", typeof(RectTransform), typeof(Canvas));
        GameObject audioOwner = new GameObject("隔离音频设置服务");
        GameObject cameraOwner = new GameObject("音频设置截图相机", typeof(Camera));
        GameObject platformOwner = new GameObject("隔离音频平台入口");
        GameFlow flow = null;
        try
        {
            var values = new Dictionary<string, int>();
            var preferences = new AudioPreferences(
                (key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => values[key] = value, () => { });
            double audioTime = 100;
            GameAudio.Initialize(audioOwner, preferences, () => audioTime);
            RectTransform root = holder.GetComponent<RectTransform>();
            root.pivot = new Vector2(0, 1);
            root.sizeDelta = new Vector2(1920, 1080);
            Canvas canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            Camera camera = cameraOwner.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 540;
            camera.transform.position = new Vector3(960, -540, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Deep;
            canvas.worldCamera = camera;
            flow = new GameFlow(new UiFactory(), root, database,
                new GameProgress((key, fallback) => fallback, (key, value) => { }, () => { }));
            Require(Find(root, "声音设置") != null, "主菜单缺少独立声音设置入口");
            Click(root, "声音设置");
            Transform musicRow = Named(root, "音乐设置");
            Transform soundRow = Named(root, "音效设置");
            Click(musicRow, "已开启");
            Require(!GameAudio.MusicEnabled && GameAudio.SoundEnabled, "音乐静音联动修改了音效开关");
            Click(musicRow, "−");
            Click(soundRow, "+");
            Require(Math.Abs(GameAudio.MusicVolume - .4f) < .002f && Math.Abs(GameAudio.SoundVolume - .9f) < .002f,
                "真实按钮未分别调整两类音量");
            var reopened = new AudioPreferences(
                (key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => values[key] = value, () => { });
            Require(!reopened.MusicEnabled && reopened.SoundEnabled && Math.Abs(reopened.MusicVolume - .4f) < .002f,
                "重新读取隔离存储未保留真实按钮设置");
            Capture(root, camera, "settings-main");
            Click(root, "完成");
            Require(root.GetComponentsInChildren<RectTransform>().All(item => item.name != "声音设置面板"), "完成后设置面板未关闭");

            LevelDefinition level = database.FindLevel("huangjin");
            flow.ShowRoster(level);
            Click(root, "推荐阵容");
            HeroDefinition overflow = database.Heroes.First(hero => !level.RecommendedRoster.Contains(hero.Id) && !level.BossHeroIds.Contains(hero.Id));
            audioTime += 1;
            Click(root, overflow.Name);
            Require(GameAudio.LastSoundCue == "ui.click" && root.GetComponentsInChildren<Text>().Any(text => text.text == "已选 10 / 10"),
                "已满阵容查看卡片应保持阵容并播放普通查看反馈");
            audioTime += 1;
            long beforeReject = GameAudio.SoundSequence;
            ChooseHero(root, overflow.Id);
            Require(GameAudio.LastSoundCue == "ui.reject" && GameAudio.SoundSequence == beforeReject + 1,
                "选满10人后仍播放成功点击声，或点击和拒绝声重复叠播");
            audioTime += 1;
            ChooseHero(root, level.BossHeroIds[0]);
            Require(GameAudio.LastSoundCue == "ui.reject", "点击本关禁选敌将未播放拒绝声");
            audioTime += 1;
            ChooseHero(root, level.RecommendedRoster[0]);
            Require(GameAudio.LastSoundCue == "ui.click", "合法选将操作未播放确认点击声");
            flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            flow.CurrentSession.Start();
            flow.Tick(.01f);
            flow.PauseBattle();
            Click(root, "声音设置");
            Click(Named(root, "音效设置"), "已开启");
            Require(!GameAudio.SoundEnabled && flow.CurrentSession.State == BattleState.Paused,
                "暂停页调整声音时恢复了战斗或修改未生效");
            Capture(root, camera, "settings-pause");
            flow.HandleBack();
            Require(flow.CurrentSession.State == BattleState.Paused && Find(root, "继续守关") != null,
                "设置页返回错误地关闭暂停或恢复战斗");
            flow.HandleBack();
            Require(flow.CurrentSession.State == BattleState.Running, "离开设置后不能正常继续战斗");

            platformOwner.SetActive(false);
            YanyiGame platform = platformOwner.AddComponent<YanyiGame>();
            typeof(YanyiGame).GetField("flow", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(platform, flow);
            platform.HandleApplicationPause(true);
            platform.HandleApplicationFocus(false);
            platform.HandleApplicationPause(false);
            Require(GameAudio.SystemSuspended && flow.CurrentSession.State == BattleState.Paused,
                "两个平台挂起原因未独立保持");
            platform.HandleApplicationFocus(true);
            Require(!GameAudio.SystemSuspended && flow.CurrentSession.State == BattleState.Paused,
                "前台回调自动恢复了战斗");
            typeof(YanyiGame).GetField("flow", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(platform, null);
            Debug.Log("音频设置界面检查通过：主菜单/暂停真实按钮、独立开关与音量、隔离存储重读、返回层级及平台入口；不是磁盘或手机听测");
        }
        finally
        {
            if (flow != null) flow.Dispose();
            UnityEngine.Object.DestroyImmediate(platformOwner);
            UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.DestroyImmediate(cameraOwner);
            UnityEngine.Object.DestroyImmediate(audioOwner);
        }
    }

    private static Transform Named(Transform root, string name)
    {
        Transform found = root.GetComponentsInChildren<Transform>().FirstOrDefault(item => item.name == name);
        Require(found != null, "声音设置缺少节点：" + name);
        return found;
    }

    private static void Click(Transform root, string label)
    {
        Button button = Find(root, label);
        Require(button != null && button.interactable, "未找到可操作的声音按钮：" + label);
        button.onClick.Invoke();
    }

    private static void ChooseHero(Transform root, string heroId)
    {
        Button button = Named(root, "选择_" + heroId).GetComponent<Button>();
        Require(button != null && button.interactable, "选将勾选按钮不可用：" + heroId);
        button.onClick.Invoke();
    }

    private static void Capture(RectTransform root, Camera camera, string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Canvas.ForceUpdateCanvases();
        foreach (Text text in root.GetComponentsInChildren<Text>())
        {
            if (string.IsNullOrEmpty(text.text)) continue;
            Require(text.text.All(character => char.IsWhiteSpace(character) || text.font.HasCharacter(character)), "声音设置缺少字形：" + text.text);
            Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "声音设置文字高度不足：" + text.text);
        }
        string directory = Path.GetFullPath("../docs/ui/runtime-audio");
        Directory.CreateDirectory(directory);
        int[,] sizes = { { 1920, 1080 }, { 1280, 720 }, { 2400, 1080 } };
        for (int i = 0; i < sizes.GetLength(0); i++)
        {
            int width = sizes[i, 0], height = sizes[i, 1];
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + "-" + width + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    private static Button Find(Transform root, string label)
    {
        return root.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
            button.gameObject.activeInHierarchy && button.GetComponentsInChildren<Text>(true).Any(text => text.text == label));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
