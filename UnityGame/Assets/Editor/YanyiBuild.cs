using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

public static class YanyiBuild
{
    private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        if (!File.Exists(ScenePath)) EditorApplication.delayCall += PrepareProject;
    }

    [MenuItem("军师请下令/准备工程")]
    public static void PrepareProject()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += PrepareProject;
            return;
        }
        Directory.CreateDirectory("Assets/Scenes");
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(25f / 255f, 62f / 255f, 55f / 255f);
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "演义沙盘课程项目";
        PlayerSettings.productName = "军师请下令";
        AppIconSetup.Apply();
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.course.yanyisandpan");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
        // JsonUtility读取纯字段配置；此阶段保守保留规则程序集。
        AssetDatabase.SaveAssets();
        Debug.Log("军师请下令工程设置完成");
    }

    [MenuItem("军师请下令/验证配置")]
    public static void VerifyProject()
    {
        PrepareProject();
        LegacyPreferencesChecks.Verify();
        UiAudioChecks.Verify();
        var source = Resources.Load<TextAsset>("Game/database");
        if (source == null) throw new InvalidOperationException("未找到Game/database.json，请先生成内容");
        var database = JsonUtility.FromJson<GameDatabase>(source.text);
        var errors = CatalogValidator.Validate(database);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors.ToArray()));
        if (Resources.Load<Font>("Game/Fonts/Chinese") == null)
            throw new InvalidOperationException("未找到可分发的中文字体");
        foreach (var hero in database.Heroes)
            if (Resources.Load<Texture2D>(hero.SpriteId) == null)
                throw new InvalidOperationException("人物素材缺失：" + hero.Id);
        foreach (var level in database.Levels)
            if (Resources.Load<Texture2D>(level.MapResource) == null)
                throw new InvalidOperationException("地图素材缺失：" + level.Id);
        UiTypographyChecks.Verify(database);
        UiBattleLayoutChecks.Verify(database);
        UiBattleFeedbackChecks.Verify();
        UiBattlePlacementChecks.Verify();
        UiSafeAreaChecks.Verify();
        var recordChecks = new GameObject("最快记录流程验证", typeof(RectTransform), typeof(Canvas));
        try
        {
            RectTransform checkRoot = (RectTransform)recordChecks.transform;
            checkRoot.pivot = new Vector2(0, 1); checkRoot.sizeDelta = new Vector2(1920, 1080);
            UiFastestRecordChecks.Run(new UiFactory(), checkRoot, database);
        }
        finally { UnityEngine.Object.DestroyImmediate(recordChecks); }
        Debug.Log("配置验证通过：" + database.Heroes.Count + "位人物，" + database.Levels.Count + "张地图");
    }

    [MenuItem("军师请下令/构建Android APK")]
    public static void BuildAndroid()
    { BuildAndroidTo("Builds/Android"); }

    public static void BuildAndroidFactionArmy()
    { BuildAndroidTo("Builds/FactionArmy-20261112/Android"); }

    public static void BuildAndroidRace()
    { BuildAndroidTo("Builds/Race-20261119/Android"); }

    public static void BuildAndroidSensation()
    { BuildAndroidTo("Builds/Sensation-20261119/Android"); }

    private static void BuildAndroidTo(string output)
    {
        VerifyProject();
        var androidRoot = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
        var sdk = Path.Combine(androidRoot, "SDK");
        var ndk = Path.Combine(androidRoot, "NDK");
        var jdk = Path.Combine(androidRoot, "OpenJDK");
        if (Directory.Exists(sdk)) EditorPrefs.SetString("AndroidSdkRoot", sdk);
        if (Directory.Exists(ndk)) EditorPrefs.SetString("AndroidNdkRootR16b", ndk);
        if (Directory.Exists(jdk)) EditorPrefs.SetString("JdkPath", jdk);
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        Directory.CreateDirectory(output);
        BuildAndCheck(Path.Combine(output, "YanyiSandpan.apk"), BuildTarget.Android);
    }

    [MenuItem("军师请下令/构建Windows试玩版")]
    public static void BuildWindows()
    {
        VerifyProject();
        Directory.CreateDirectory("Builds/Windows");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        BuildAndCheck("Builds/Windows/YanyiSandpan.exe", BuildTarget.StandaloneWindows64);
    }

    public static void BuildWindowsJade()
    {
        VerifyProject();
        Directory.CreateDirectory("Builds/JadeApp-20261112/Windows");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        BuildAndCheck("Builds/JadeApp-20261112/Windows/YanyiSandpan.exe", BuildTarget.StandaloneWindows64);
    }

    public static void BuildWindowsExperience()
    {
        VerifyProject();
        Directory.CreateDirectory("Builds/CombatExperience-20261112/Windows");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        BuildAndCheck("Builds/CombatExperience-20261112/Windows/YanyiSandpan.exe", BuildTarget.StandaloneWindows64);
    }

    private static void BuildAndCheck(string output, BuildTarget target)
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = output,
            target = target, options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("构建失败：" + report.summary.result + "，错误" + report.summary.totalErrors);
        Debug.Log("构建成功：" + Path.GetFullPath(output) + "，大小" + report.summary.totalSize);
    }

    public static void BuildWindowsFactionArmy()
    {
        VerifyProject();
        Directory.CreateDirectory("Builds/FactionArmy-20261112/Windows");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        BuildAndCheck("Builds/FactionArmy-20261112/Windows/YanyiSandpan.exe", BuildTarget.StandaloneWindows64);
    }

    public static void BuildWindowsRace()
    {
        VerifyProject();
        Directory.CreateDirectory("Builds/Race-20261119/Windows");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        BuildAndCheck("Builds/Race-20261119/Windows/YanyiSandpan.exe", BuildTarget.StandaloneWindows64);
    }

    public static void BuildWindowsSensation()
    {
        VerifyProject();
        Directory.CreateDirectory("Builds/Sensation-20261119/Windows");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        BuildAndCheck("Builds/Sensation-20261119/Windows/YanyiSandpan.exe", BuildTarget.StandaloneWindows64);
    }
}
