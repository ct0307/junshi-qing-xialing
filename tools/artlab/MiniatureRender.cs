using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 仅在独立ArtLab工程中执行，不打开或改写玩家游戏工程。
public static class MiniatureRender
{
    public static void Run()
    {
        string output = Environment.GetEnvironmentVariable("ARTLAB_OUTPUT");
        string database = Environment.GetEnvironmentVariable("ARTLAB_DATABASE");
        if (string.IsNullOrEmpty(output) || !File.Exists(database)) throw new InvalidOperationException("缺少美术实验路径参数");
        if (Directory.Exists(output)) throw new InvalidOperationException("样稿版本目录已存在，禁止覆盖：" + output);
        Directory.CreateDirectory(output);
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 60;
        QualitySettings.shadowCascades = 2;
        QualitySettings.pixelLightCount = 4;
        RenderCharacter(output, "guan_yu");
        RenderCharacter(output, "zhao_yun");
        RenderMap(output, database);
        Debug.Log("微缩美术样稿导出通过：" + output);
    }

    private static void NewScene(bool character = false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MiniatureArt.ResetMaterials();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = character ? new Color(.57f, .64f, .60f) : new Color(.40f, .47f, .43f);
        RenderSettings.ambientEquatorColor = character ? new Color(.32f, .39f, .36f) : new Color(.18f, .24f, .22f);
        RenderSettings.ambientGroundColor = character ? new Color(.13f, .18f, .15f) : new Color(.08f, .11f, .10f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.reflectionIntensity = .28f;
        var sun = new GameObject("左上暖光", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1, .91f, .77f);
        sun.intensity = character ? .92f : .78f;
        sun.transform.rotation = Quaternion.Euler(55, 35, 0);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .65f;
        sun.shadowBias = .008f;
        sun.shadowNormalBias = .05f;
        var fill = new GameObject("柔和冷补光", typeof(Light)).GetComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(.68f, .80f, .91f);
        fill.intensity = character ? .28f : .18f;
        fill.transform.rotation = Quaternion.Euler(25, -40, 0);
    }

    private static Camera CameraFor(int width, int height)
    {
        var camera = new GameObject("样稿正交相机", typeof(Camera)).GetComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = (float)width / height;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100;
        camera.allowHDR = false;
        camera.allowMSAA = true;
        return camera;
    }

    private static void RenderCharacter(string output, string heroId)
    {
        NewScene(true);
        GameObject model = MiniatureCharacters.Build(heroId);
        if (model == null) throw new InvalidOperationException("人物构建失败：" + heroId);
        Camera camera = CameraFor(768, 768);
        camera.orthographicSize = 2.05f;
        camera.transform.position = new Vector3(4, 5, -10);
        camera.transform.LookAt(new Vector3(0, 1.5f, 0));
        // 地面足点投影严格对齐既有台座锚点，不依赖包围盒或Sprite pivot。
        Vector3 screen = camera.WorldToViewportPoint(Vector3.zero);
        Vector2 desired = new Vector2(360f / 768, 1 - 660f / 768);
        camera.transform.position -= camera.transform.right * ((desired.x - screen.x) * 2 * camera.orthographicSize * camera.aspect)
            + camera.transform.up * ((desired.y - screen.y) * 2 * camera.orthographicSize);
        Vector3 aligned = camera.WorldToViewportPoint(Vector3.zero);
        if (Mathf.Abs(aligned.x * 768 - 360) > .01f || Mathf.Abs((1 - aligned.y) * 768 - 660) > .01f)
            throw new InvalidOperationException("人物台座锚点投影不一致");
        Save(camera, 768, 768, Path.Combine(output, heroId + ".png"));
        Debug.Log("人物样稿通过：" + heroId + "；台座锚点(360,660)，透明768方图");
    }

    private static void RenderMap(string output, string database)
    {
        NewScene();
        MiniatureMap.Build(database);
        Camera camera = CameraFor(1920, 1080);
        camera.backgroundColor = new Color(25f / 255, 62f / 255, 55f / 255, 1);
        camera.orthographicSize = 5.4f;
        float angle = MiniatureArt.MapPitch * Mathf.Deg2Rad;
        camera.transform.position = new Vector3(0, 25 * Mathf.Sin(angle), -25 * Mathf.Cos(angle));
        camera.transform.LookAt(Vector3.zero);
        // 地面到图片的投影保持既有逻辑坐标，装饰高度不影响规则坐标。
        foreach (Vector2 point in new[] { new Vector2(70, 300), new Vector2(1700, 415), new Vector2(180, 700), new Vector2(340, 215) })
        {
            Vector3 projected = camera.WorldToViewportPoint(MiniatureArt.At(point.x, point.y));
            if (Mathf.Abs(projected.x * 1920 - point.x) > .02f || Mathf.Abs((1 - projected.y) * 1080 - point.y) > .02f)
                throw new InvalidOperationException("地图投影偏离规则坐标：" + point);
        }
        Save(camera, 1920, 1080, Path.Combine(output, "huangjin.png"));
        Debug.Log("地图样稿通过：1920×1080，四个参考坐标投影误差小于0.02像素");
    }

    private static void Save(Camera camera, int width, int height, string path)
    {
        var target = new RenderTexture(width * 2, height * 2, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        target.Create();
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        var sampled = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        sampled.Create();
        Graphics.Blit(target, sampled);
        RenderTexture.active = sampled;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        // 透明背景的MSAA覆盖率同时衰减RGB与Alpha；PNG需保存直通Alpha以避免再次合成黑边。
        if (camera.backgroundColor.a == 0)
        {
            Color32[] pixels = image.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a == 0) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
                if (pixel.a == 255) continue;
                float unpremultiply = 255f / pixel.a;
                pixel.r = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * unpremultiply), 0, 255);
                pixel.g = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.g * unpremultiply), 0, 255);
                pixel.b = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.b * unpremultiply), 0, 255);
                pixels[i] = pixel;
            }
            image.SetPixels32(pixels);
            image.Apply();
        }
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        sampled.Release();
        UnityEngine.Object.DestroyImmediate(sampled);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }
}
