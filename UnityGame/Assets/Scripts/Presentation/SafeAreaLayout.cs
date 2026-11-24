using System;
using UnityEngine;

namespace YanyiSandpan.Presentation
{
    public static class SafeAreaLayout
    {
        private const float LogicalWidth = 1920;
        private const float LogicalHeight = 1080;

        public static void Apply(RectTransform logicalRoot, int screenWidth, int screenHeight, Rect safeArea)
        {
            if (logicalRoot == null) throw new ArgumentNullException("logicalRoot");
            int width = Math.Max(1, screenWidth), height = Math.Max(1, screenHeight);
            Rect fullScreen = new Rect(0, 0, width, height);
            Rect safe = ValidIntersection(safeArea, fullScreen);
            float scale = Mathf.Min(safe.width / LogicalWidth, safe.height / LogicalHeight);
            // 极小的正面积也可能在浮点除法后下溢为0，此时采用完整屏幕。
            if (!Finite(scale) || scale <= 0)
            {
                safe = fullScreen;
                scale = Mathf.Min(safe.width / LogicalWidth, safe.height / LogicalHeight);
            }
            Vector2 center = new Vector2(.5f, .5f);
            logicalRoot.anchorMin = logicalRoot.anchorMax = center;
            logicalRoot.pivot = center;
            logicalRoot.sizeDelta = new Vector2(LogicalWidth, LogicalHeight);
            logicalRoot.localScale = new Vector3(scale, scale, 1);
            logicalRoot.anchoredPosition3D = new Vector3(safe.center.x - width * .5f,
                safe.center.y - height * .5f, 0);
        }

        private static Rect ValidIntersection(Rect candidate, Rect fullScreen)
        {
            if (!Finite(candidate.x) || !Finite(candidate.y) || !Finite(candidate.width) || !Finite(candidate.height) ||
                candidate.width <= 0 || candidate.height <= 0 || !Finite(candidate.xMax) || !Finite(candidate.yMax))
                return fullScreen;
            float left = Mathf.Max(fullScreen.xMin, candidate.xMin);
            float bottom = Mathf.Max(fullScreen.yMin, candidate.yMin);
            float right = Mathf.Min(fullScreen.xMax, candidate.xMax);
            float top = Mathf.Min(fullScreen.yMax, candidate.yMax);
            return right > left && top > bottom ? Rect.MinMaxRect(left, bottom, right, top) : fullScreen;
        }

        private static bool Finite(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
