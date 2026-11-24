using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 使用真实字形和变换后的文本框验收，防止顶栏相互挤压或意外形成第三行。
public static class UiBattleHeaderChecks
{
    public static void Verify(GameDatabase database)
    {
        foreach (LevelDefinition level in database.Levels)
        using (var scene = new UiRefinementChecks.Scene(database))
        {
            scene.Flow.BeginBattle(level, new List<string>(level.RecommendedRoster));
            VerifyLayout(scene.Root, level.Name + "备战");
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "战场主操作").onClick.Invoke();
            scene.Flow.Tick(.5f);
            VerifyLayout(scene.Root, level.Name + "开战");
            Button speed = scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "进军倍率3");
            speed.onClick.Invoke();
            scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "倍率说明确认").onClick.Invoke();
            VerifyLayout(scene.Root, level.Name + "三倍进军");
        }
        Debug.Log("战斗顶栏检查通过：五关备战、开战与三倍进军文字完整单行，两行文本框无重叠且留有空隙");
    }

    public static void VerifyLayout(RectTransform root, string context = "战斗顶栏")
    {
        Canvas.ForceUpdateCanvases();
        Text title = root.GetComponentsInChildren<Text>().FirstOrDefault(label => label.name == "关卡标题");
        Require(title != null, context + "缺少真实关卡标题");
        Transform battleRoot = title.transform.parent;
        var labels = new List<TextBounds>();
        foreach (Transform branch in battleRoot)
        {
            // 弹窗从独立容器绘制，只验收战斗常驻顶栏，避免将弹窗内容当成第三行。
            if (branch.GetComponent<Text>() == null && branch.GetComponent<Button>() == null && branch.name != "战况情报") continue;
            foreach (Text label in branch.GetComponentsInChildren<Text>())
            {
                if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text) || label.color.a <= .01f) continue;
                Rect bounds = BoundsInRoot(label.rectTransform, root);
                if (bounds.yMin >= BattleLayout.HeaderBottom || bounds.yMax <= 0) continue;
                string description = context + "：" + label.transform.parent.name + "/" + label.name + "「" + label.text + "」";
                Require(bounds.xMin >= -.5f && bounds.xMax <= root.rect.width + .5f && bounds.yMin >= -.5f &&
                    bounds.yMax <= BattleLayout.HeaderBottom + .5f, description + "超出顶栏可用范围");
                Require(!label.text.Contains("\n") && !label.text.Contains("\r"), description + "不应包含换行");
                UiTypographyChecks.Check(label, description, true);
                Require(label.cachedTextGenerator.lineCount <= 1, description + "实际生成了多行");
                labels.Add(new TextBounds(bounds, description));
            }
        }
        Require(labels.Count > 0, context + "没有可验收的顶栏文字");
        for (int i = 0; i < labels.Count; i++)
        for (int j = i + 1; j < labels.Count; j++)
        {
            Rect a = labels[i].Bounds, b = labels[j].Bounds;
            float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            string pair = labels[i].Description + " 与 " + labels[j].Description;
            Require(overlapX <= .5f || overlapY <= .5f, pair + "文本框相交");
            if (overlapY > .5f) Require(-overlapX >= 6 - .5f, pair + "同行间隙不足6逻辑像素");
            if (overlapX > .5f) Require(-overlapY >= 12 - .5f, pair + "两行间隙不足12逻辑像素");
        }
        var bands = new List<Rect>();
        foreach (TextBounds label in labels.OrderBy(item => item.Bounds.yMin))
        {
            Rect current = label.Bounds;
            if (bands.Count == 0 || current.yMin >= bands[bands.Count - 1].yMax)
                bands.Add(current);
            else
            {
                Rect previous = bands[bands.Count - 1];
                previous.yMax = Mathf.Max(previous.yMax, current.yMax);
                bands[bands.Count - 1] = previous;
            }
        }
        Require(bands.Count == 2, context + "应保持两行信息，实际形成" + bands.Count + "行");
        Require(bands[1].yMin - bands[0].yMax >= 12 - .5f, context + "两行之间缺少明确留白");
    }

    private static Rect BoundsInRoot(RectTransform target, RectTransform root)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 first = root.InverseTransformPoint(corners[0]);
        float minX = first.x, maxX = first.x, minY = -first.y, maxY = -first.y;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 point = root.InverseTransformPoint(corners[i]);
            minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, -point.y); maxY = Mathf.Max(maxY, -point.y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class TextBounds
    {
        public readonly Rect Bounds;
        public readonly string Description;
        public TextBounds(Rect bounds, string description) { Bounds = bounds; Description = description; }
    }
}
