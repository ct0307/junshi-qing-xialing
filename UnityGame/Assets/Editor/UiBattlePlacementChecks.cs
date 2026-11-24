using System;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

public static class UiBattlePlacementChecks
{
    public static void Verify()
    {
        var canvas = new GameObject("道路覆盖验证画布", typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var mesh = new Mesh();
        try
        {
            var ui = new UiFactory();
            RectTransform root = ui.Rect(canvas.transform, "道路覆盖验证", 0, 0, 1920, 1080);
            // 只添加被测组件，确保实际运行时会自动提供CanvasRenderer。
            BattlePlacementGuide guide = root.gameObject.AddComponent<BattlePlacementGuide>();
            Require(root.GetComponent<CanvasRenderer>() != null, "覆盖预览缺少自动CanvasRenderer");
            Require(!guide.raycastTarget, "覆盖预览不应拦截触控");

            guide.Show(Level(100, 200, 500, 200), new Point2(300, 400), 30);
            Read(guide, mesh);
            int baseVertices = mesh.vertexCount;
            Require(baseVertices > 0, "选中底座标记未生成");

            guide.Show(Level(100, 400, 500, 400), new Point2(300, 400), 30);
            Read(guide, mesh);
            Require(mesh.vertexCount == baseVertices + 4, "穿过射程的道路应只生成一个有效子段");
            CheckEnds(mesh, 270, 330, -400);
            Require(Mathf.Abs(guide.CoverageLength - 60) < .01f, "覆盖长度未按真实裁剪区间累计");

            guide.Show(Level(290, 400, 310, 400), new Point2(300, 400), 30);
            Read(guide, mesh);
            Require(mesh.vertexCount == baseVertices + 4, "圆内道路未完整生成");
            CheckEnds(mesh, 290, 310, -400);

            guide.Show(Level(100, 370, 500, 370), new Point2(300, 400), 30);
            Read(guide, mesh);
            Require(mesh.vertexCount == baseVertices, "相切道路不应生成有长度的覆盖段");
            Require(guide.CoverageLength == 0, "相切道路不应报告有效覆盖");
            guide.Show(Level(100, 360, 500, 360), new Point2(300, 400), 30);
            Read(guide, mesh);
            Require(mesh.vertexCount == baseVertices, "圆外道路不应被高亮");

            guide.Clear();
            Read(guide, mesh);
            Require(mesh.vertexCount == 0, "取消选择后覆盖网格未清空");
            Debug.Log("道路覆盖验证通过：自动渲染组件、圆内外裁剪、相切、清空及触控穿透");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    private static LevelDefinition Level(float x1, float y1, float x2, float y2)
    {
        var level = new LevelDefinition();
        var route = new RouteDefinition();
        route.Points.Add(new Point2(x1, y1));
        route.Points.Add(new Point2(x2, y2));
        level.Routes.Add(route);
        return level;
    }

    private static void Read(BattlePlacementGuide guide, Mesh mesh)
    {
        Canvas.ForceUpdateCanvases();
        mesh.Clear();
            Mesh rendered = guide.canvasRenderer.GetMesh();
            if (rendered == null) return;
            mesh.vertices = rendered.vertices;
            UnityEngine.Object.DestroyImmediate(rendered);
    }

    private static void CheckEnds(Mesh mesh, float fromX, float toX, float centerY)
    {
        Vector3[] vertices = mesh.vertices;
        Require(Mathf.Abs(vertices[0].x - fromX) < .01f && Mathf.Abs(vertices[3].x - fromX) < .01f,
            "道路覆盖起点未按真实圆边界裁剪");
        Require(Mathf.Abs(vertices[1].x - toX) < .01f && Mathf.Abs(vertices[2].x - toX) < .01f,
            "道路覆盖终点未按真实圆边界裁剪");
        Require(Mathf.Abs((vertices[0].y + vertices[3].y) * .5f - centerY) < .01f,
            "道路覆盖Y坐标未转换为画布坐标");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
