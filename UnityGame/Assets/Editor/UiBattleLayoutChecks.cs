using System;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

public static class UiBattleLayoutChecks
{
    public static void Verify(GameDatabase database)
    {
        var canvas = new GameObject("战场布局验证画布", typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        int count = 0;
        try
        {
            var ui = new UiFactory();
            RectTransform root = ui.Rect(canvas.transform, "逻辑画布", 0, 0, 1920, 1080);
            foreach (var level in database.Levels)
            {
                RectTransform page = ui.Rect(root, level.Id, 0, 0, 1920, 1080);
                RectTransform ground = BattleLayout.CreateGround(ui, page, level.MapResource);
                RawImage map = ground.GetComponentInChildren<RawImage>();
                if (map == null || map.texture == null)
                    throw new InvalidOperationException(level.Name + "缺少真实地图地面");
                RectTransform units = ui.Rect(ground, "棋兵", 0, 0, 1920, 1080);
                var unit = new UnitView(ui, units, false, BattleLayout.GroundScale);
                unit.SetDefinition(database.FindHero("zhuge_liang").SpriteId, "诸葛亮", BattleLayout.HeroSize, false);
                RectTransform body = (RectTransform)unit.Root.Find("人物");
                Text name = unit.Root.Find("名签底").GetComponentInChildren<Text>(true);
                Text rank = unit.Root.Find("等级徽标").GetComponentInChildren<Text>(true);

                foreach (var slot in level.Slots)
                {
                    for (int tier = 1; tier <= 3; tier++)
                    {
                        unit.Place(slot.Position, tier, 1, false, false, 0);
                        unit.Attack(0);
                        // 调用真实攻击峰值动画，包含左上角缩放及浮动，不重写动作计算。
                        unit.Animate(.11f);
                        CheckBounds(root, body, level.Name + "阵位" + slot.Id + "人物");
                        if (name.gameObject.activeInHierarchy) CheckBounds(root, name.rectTransform, level.Name + "阵位" + slot.Id + "姓名");
                        if (rank.gameObject.activeInHierarchy) CheckBounds(root, rank.rectTransform, level.Name + "阵位" + slot.Id + "阶位");
                        if (name.gameObject.activeInHierarchy) throw new InvalidOperationException("战场不应常驻大姓名牌");
                        UiTypographyChecks.Check(name, "真实诸葛亮名签", true);
                        UiTypographyChecks.Check(rank, "真实阶位" + tier, true);
                        Vector3 mapped = MapPoint(map, slot.Position);
                        if (Vector3.Distance(mapped, unit.Root.position) > .1f)
                            throw new InvalidOperationException(level.Name + "阵位" + slot.Id + "未对齐原地图坐标");
                    }
                    // 抵消地面纵缩放后，人物像素横纵长度应相同。
                    var corners = new Vector3[4];
                    body.GetWorldCorners(corners);
                    if (Mathf.Abs(Vector3.Distance(corners[0], corners[1]) - Vector3.Distance(corners[1], corners[2])) > .1f)
                        throw new InvalidOperationException(level.Name + "人物受到非等比压缩");
                    count++;
                }
                foreach (var route in level.Routes)
                    foreach (var point in route.Points)
                    {
                        // 用路径顶点验证地图裁切与地面变换，避免只验证某个阵位。
                        Vector3 expected = ground.TransformPoint(new Vector3(point.X, -point.Y, 0));
                        if (Vector3.Distance(MapPoint(map, point), expected) > .1f)
                            throw new InvalidOperationException(level.Name + "道路未对齐原地图坐标");
                    }
                UnityEngine.Object.DestroyImmediate(page.gameObject);
            }
            Debug.Log("战场布局验证通过：" + database.Levels.Count + "关、" + count + "阵位，人物攻击峰值、三阶名签、道路与地图坐标均符合边界");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    private static Vector3 MapPoint(RawImage map, Point2 source)
    {
        Rect rect = map.rectTransform.rect;
        Rect uv = map.uvRect;
        float x = ((source.X / map.texture.width - uv.x) / uv.width) * rect.width + rect.xMin;
        float y = ((1 - source.Y / map.texture.height - uv.y) / uv.height) * rect.height + rect.yMin;
        return map.rectTransform.TransformPoint(new Vector3(x, y, 0));
    }

    private static void CheckBounds(RectTransform root, RectTransform subject, string description)
    {
        var corners = new Vector3[4];
        subject.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            float y = -root.InverseTransformPoint(corner).y;
            if (y < BattleLayout.HeaderBottom || y > BattleLayout.DockTop)
                throw new InvalidOperationException(description + "被信息栏遮挡：逻辑Y=" + y.ToString("0.0"));
        }
    }
}
