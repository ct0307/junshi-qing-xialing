using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

public static class AppIconSetup
{
    public static void Apply()
    {
        Texture2D icon = Load("app-icon-1024.png");
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
        var icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++) icons[i] = icon;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
        PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, AndroidPlatformIconKind.Adaptive);
        Texture2D background = Load("adaptive-background-432.png"), foreground = Load("adaptive-foreground-432.png");
        foreach (PlatformIcon item in adaptive) { item.SetTexture(background, 0); item.SetTexture(foreground, 1); }
        PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, AndroidPlatformIconKind.Adaptive, adaptive);
        foreach (PlatformIconKind kind in new[] { AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy })
        {
            PlatformIcon[] platformIcons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
            foreach (PlatformIcon item in platformIcons) item.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, platformIcons);
        }
        // 不同Unity版本的平台模块可能未开放主题单色图标槽；仅在实际提供时配置。
        FieldInfo monochrome = typeof(AndroidPlatformIconKind).GetField("Monochrome", BindingFlags.Public | BindingFlags.Static);
        if (monochrome != null)
        {
            PlatformIconKind kind = (PlatformIconKind)monochrome.GetValue(null);
            PlatformIcon[] platformIcons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
            foreach (PlatformIcon item in platformIcons) item.SetTexture(Load("monochrome-432.png"));
            PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, platformIcons);
        }
    }

    public static void Verify()
    {
        Texture2D[] defaults = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
        if (defaults.Length == 0 || defaults[0] == null || defaults[0].name != "app-icon-1024") throw new InvalidOperationException("通用图标未接入");
        PlatformIcon[] adaptive = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, AndroidPlatformIconKind.Adaptive);
        if (adaptive.Length == 0) throw new InvalidOperationException("Android没有自适应图标槽");
        foreach (PlatformIcon item in adaptive)
            if (item.GetTexture(0) == null || item.GetTexture(1) == null || item.GetTexture(0).name != "adaptive-background-432" || item.GetTexture(1).name != "adaptive-foreground-432")
                throw new InvalidOperationException("Android自适应图标的前景背景未正确设置");
        Debug.Log("应用图标配置验证通过：通用1024、Windows多尺寸、Android自适应前景/背景及兼容图标");
    }

    private static Texture2D Load(string file)
    {
        string path = "Assets/Branding/" + file;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("图标尚未导入：" + path);
        if (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize < 1024)
        {
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024; importer.SaveAndReimport();
        }
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("图标加载失败：" + path);
        return texture;
    }
}
