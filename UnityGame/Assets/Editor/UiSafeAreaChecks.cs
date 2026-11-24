using System;
using UnityEngine;
using YanyiSandpan.Presentation;

// 验证实际RectTransform四角，不用另一份布局算法计算期望缩放。
public static class UiSafeAreaChecks
{
    public static void Verify()
    {
        var screenObject = new GameObject("安全区验证屏幕", typeof(RectTransform));
        var rootObject = new GameObject("安全区验证逻辑画布", typeof(RectTransform));
        int count = 0;
        try
        {
            RectTransform screen = (RectTransform)screenObject.transform;
            screen.pivot = Vector2.zero;
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(screen, false);
            // 从错误锚点、尺寸和非等比缩放开始，避免测试只依赖默认状态。
            root.anchorMin = new Vector2(.1f, .2f);
            root.anchorMax = new Vector2(.7f, .9f);
            root.pivot = new Vector2(0, 1);
            root.sizeDelta = new Vector2(800, 600);
            root.localScale = new Vector3(3, 2, 1);
            root.anchoredPosition = new Vector2(-30, 500);

            Action<string, int, int, Rect, Rect> check = (name, width, height, input, expected) =>
            {
                screen.sizeDelta = new Vector2(Mathf.Max(1, width), Mathf.Max(1, height));
                SafeAreaLayout.Apply(root, width, height, input);
                CheckLayout(root, screen, expected, name);
                count++;
            };

            check("16:9屏幕", 1920, 1080, new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1080));
            check("小屏幕", 1280, 720, new Rect(0, 0, 1280, 720), new Rect(0, 0, 1280, 720));
            check("宽屏幕", 2400, 1080, new Rect(0, 0, 2400, 1080), new Rect(0, 0, 2400, 1080));
            check("左侧刘海", 2400, 1080, new Rect(132, 0, 2268, 1080), new Rect(132, 0, 2268, 1080));
            check("右侧刘海", 2400, 1080, new Rect(0, 0, 2220, 1080), new Rect(0, 0, 2220, 1080));
            check("左右不对称预留", 2400, 1080, new Rect(132, 0, 2100, 1080), new Rect(132, 0, 2100, 1080));
            check("底部预留", 1920, 1080, new Rect(0, 84, 1920, 996), new Rect(0, 84, 1920, 996));
            check("区域与屏幕取交集", 1920, 1080, new Rect(-100, 80, 1920, 1200), new Rect(0, 80, 1820, 1000));
            check("区域完全在屏幕外", 1920, 1080, new Rect(2500, 0, 200, 600), new Rect(0, 0, 1920, 1080));
            check("零区域", 1920, 1080, new Rect(100, 100, 0, 20), new Rect(0, 0, 1920, 1080));
            check("负区域", 1920, 1080, new Rect(100, 100, -80, 50), new Rect(0, 0, 1920, 1080));
            check("NaN位置", 1920, 1080, new Rect(float.NaN, 0, 100, 100), new Rect(0, 0, 1920, 1080));
            check("NaN尺寸", 1920, 1080, new Rect(0, 0, float.NaN, 100), new Rect(0, 0, 1920, 1080));
            check("正无穷位置", 1920, 1080, new Rect(0, float.PositiveInfinity, 100, 100), new Rect(0, 0, 1920, 1080));
            check("负无穷尺寸", 1920, 1080, new Rect(0, 0, 100, float.NegativeInfinity), new Rect(0, 0, 1920, 1080));
            check("区域边界溢出", 1920, 1080, new Rect(float.MaxValue, 0, float.MaxValue, 100), new Rect(0, 0, 1920, 1080));
            check("极小区域缩放下溢", 1920, 1080, new Rect(0, 0, float.Epsilon, 100), new Rect(0, 0, 1920, 1080));
            check("屏幕零尺寸", 0, 0, Rect.zero, new Rect(0, 0, 1, 1));
            check("屏幕负尺寸", -1920, -1080, Rect.zero, new Rect(0, 0, 1, 1));
            check("屏幕单边零尺寸", 0, 1080, new Rect(0, 0, 1, 1080), new Rect(0, 0, 1, 1080));
            // 同一对象连续换屏后，锚点、偏移和缩放不得残留上一项状态。
            check("连续切换回标准屏", 1920, 1080, new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1080));
            check("连续切换回左侧刘海", 2400, 1080, new Rect(132, 0, 2268, 1080), new Rect(132, 0, 2268, 1080));
            Debug.Log("安全区布局验证通过：" + count + "项，实际四角、等比缩放、居中、异常输入与连续切换");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(screenObject);
            if (rootObject != null) UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    private static void CheckLayout(RectTransform root, RectTransform screen, Rect expectedSafe, string name)
    {
        Vector2 center = new Vector2(.5f, .5f);
        Require(root.anchorMin == center && root.anchorMax == center && root.pivot == center,
            name + "未恢复居中锚点与轴心");
        Require(Mathf.Abs(root.rect.width - 1920) < .01f && Mathf.Abs(root.rect.height - 1080) < .01f,
            name + "未保持1920×1080逻辑尺寸");
        Vector3 scale = root.localScale;
        Require(Finite(scale.x) && Finite(scale.y) && Finite(scale.z) && scale.x > 0 && scale.y > 0,
            name + "出现无效或非正缩放");
        Require(Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.z, 1), name + "缩放不等比");

        var corners = new Vector3[4];
        root.GetWorldCorners(corners);
        float xMin = float.PositiveInfinity, yMin = float.PositiveInfinity;
        float xMax = float.NegativeInfinity, yMax = float.NegativeInfinity;
        float tolerance = Mathf.Max(.01f, Mathf.Max(expectedSafe.width, expectedSafe.height) * .00001f);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = screen.InverseTransformPoint(corner);
            Require(Finite(point.x) && Finite(point.y) && Finite(point.z), name + "四角含NaN或无穷值");
            Require(point.x >= expectedSafe.xMin - tolerance && point.x <= expectedSafe.xMax + tolerance &&
                point.y >= expectedSafe.yMin - tolerance && point.y <= expectedSafe.yMax + tolerance,
                name + "四角越出有效安全区：" + point);
            xMin = Mathf.Min(xMin, point.x);
            yMin = Mathf.Min(yMin, point.y);
            xMax = Mathf.Max(xMax, point.x);
            yMax = Mathf.Max(yMax, point.y);
        }
        float width = xMax - xMin, height = yMax - yMin;
        Require(width > 0 && height > 0 && Mathf.Abs(width / height - 1920f / 1080f) < .0001f,
            name + "实际画布比例不是16:9");
        Require(Mathf.Abs((xMin + xMax) * .5f - expectedSafe.center.x) <= tolerance &&
            Mathf.Abs((yMin + yMax) * .5f - expectedSafe.center.y) <= tolerance, name + "未在有效安全区居中");
        Require(Mathf.Abs(width - expectedSafe.width) <= tolerance || Mathf.Abs(height - expectedSafe.height) <= tolerance,
            name + "未最大化利用安全区");
    }

    private static bool Finite(float value)
    { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
