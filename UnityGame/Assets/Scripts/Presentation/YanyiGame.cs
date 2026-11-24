using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 平台入口只负责屏幕、资源与系统事件；页面和会话交给 GameFlow。
    public sealed class YanyiGame : MonoBehaviour
    {
        private UiFactory ui;
        private RectTransform logicalRoot;
        private RectTransform errorPage;
        private GameFlow flow;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            // 自动播放检查使用隔离服务，避免创建真实玩家流程和偏好存储。
            if (UnityEditor.SessionState.GetBool("yanyi.audio.playback.active", false)) return;
#endif
            if (UnityEngine.Object.FindAnyObjectByType<YanyiGame>() != null) return;
            new GameObject("演义沙盘").AddComponent<YanyiGame>();
        }

        private void Awake()
        {
            LegacyPreferences.Migrate();
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameAudio.Initialize(gameObject);
            ui = new UiFactory();
            Canvas canvas = new GameObject("界面画布", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Image background = ui.Box(canvas.transform, "安全区背景", 0, 0, 1, 1, UiFactory.Deep, false);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            logicalRoot = ui.Rect(canvas.transform, "1920逻辑画布", 0, 0, 1920, 1080);
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("触控事件", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform);
            FitSafeArea();
            LoadContent();
        }

        private void LoadContent()
        {
            if (flow != null) { flow.Dispose(); flow = null; }
            if (errorPage != null)
            {
                errorPage.gameObject.SetActive(false);
                Destroy(errorPage.gameObject);
                errorPage = null;
            }
            try
            {
                TextAsset json = Resources.Load<TextAsset>("Game/database");
                if (json == null) throw new InvalidOperationException("未找到关卡配置 Game/database，请先生成内容。");
                GameDatabase database = JsonUtility.FromJson<GameDatabase>(json.text);
                if (database == null) throw new InvalidOperationException("关卡配置读取失败。");
                List<string> issues = CatalogValidator.Validate(database);
                if (issues.Count > 0) throw new InvalidOperationException(string.Join("\n", issues.ToArray()));
                GameProgress progress = new GameProgress(
                    (key, fallback) => PlayerPrefs.GetInt(key, fallback),
                    (key, value) => PlayerPrefs.SetInt(key, value), PlayerPrefs.Save);
                flow = new GameFlow(ui, logicalRoot, database, progress);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                errorPage = ContentErrorView.Create(ui, logicalRoot, exception.Message, LoadContent);
            }
        }

        private void FitSafeArea()
        {
            SafeAreaLayout.Apply(logicalRoot, Screen.width, Screen.height, Screen.safeArea);
            lastSafeArea = Screen.safeArea;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }

        private void Update()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastSafeArea != Screen.safeArea) FitSafeArea();
            if (flow != null) flow.Tick(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        public void HandleBack() { if (flow != null) flow.HandleBack(); }
        public void HandleApplicationPause(bool paused)
        {
            if (flow != null) flow.SetApplicationPaused(paused);
            GameAudio.SetApplicationPaused(paused);
        }
        public void HandleApplicationFocus(bool focused)
        {
            if (flow != null) flow.SetApplicationFocused(focused);
            GameAudio.SetApplicationFocused(focused);
        }
        private void OnApplicationPause(bool paused) { HandleApplicationPause(paused); }
        private void OnApplicationFocus(bool focused) { HandleApplicationFocus(focused); }
        private void OnDestroy() { if (flow != null) { flow.Dispose(); flow = null; } }
    }
}
