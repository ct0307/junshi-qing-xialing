using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 使用规则实际圆心/半径或道路距离区间构造边界，不用武将射程代替伤害区。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleFieldGraphic : Graphic
    {
        private readonly List<Vector2> path = new List<Vector2>();
        private Vector2 center;
        private float radius;
        private bool wind;

        public void Configure(CombatFieldSnapshot field, RouteDefinition route)
        {
            raycastTarget = false;
            wind = field.Kind == "wind";
            color = wind ? new Color(.52f, .88f, .76f, .72f) : new Color(1, .66f, .28f, .72f);
            path.Clear(); radius = field.Radius;
            center = new Vector2(field.Position.X, -field.Position.Y);
            if (route != null)
            {
                radius = 0;
                float distance = 0;
                for (int index = 1; index < route.Points.Count; index++)
                {
                    Point2 a = route.Points[index - 1], b = route.Points[index];
                    float length = Point2.Distance(a, b);
                    float start = Mathf.Max(field.Start, distance), end = Mathf.Min(field.End, distance + length);
                    if (end > start && length > .001f)
                    {
                        Vector2 origin = new Vector2(a.X, -a.Y), finish = new Vector2(b.X, -b.Y);
                        path.Add(Vector2.Lerp(origin, finish, (start - distance) / length));
                        path.Add(Vector2.Lerp(origin, finish, (end - distance) / length));
                    }
                    distance += length;
                }
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (radius > 0)
            {
                for (int index = 0; index < 64; index++)
                {
                    float a = index * Mathf.PI * 2 / 64, b = (index + 1) * Mathf.PI * 2 / 64;
                    Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), w = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    Quad(helper, center + v * (radius - 3), center + v * radius, center + w * radius, center + w * (radius - 3), color);
                    Color fill = color; fill.a = wind ? .055f : .10f;
                    int start = helper.currentVertCount;
                    helper.AddVert(center, fill, Vector2.zero); helper.AddVert(center + v * radius, fill, Vector2.zero);
                    helper.AddVert(center + w * radius, fill, Vector2.zero); helper.AddTriangle(start, start + 1, start + 2);
                    if (wind && index % 16 < 9)
                        Quad(helper, center + v * radius * .55f, center + v * (radius * .55f + 2), center + w * (radius * .55f + 2), center + w * radius * .55f, color);
                }
            }
            for (int index = 0; index + 1 < path.Count; index += 2)
            {
                Vector2 start = path[index], end = path[index + 1], delta = (end - start).normalized;
                Vector2 normal = new Vector2(-delta.y, delta.x);
                Color fill = color; fill.a *= .22f;
                Quad(helper, start - normal * 17, start + normal * 17, end + normal * 17, end - normal * 17, fill);
                Quad(helper, start + normal * 15, start + normal * 18, end + normal * 18, end + normal * 15, color);
                Quad(helper, start - normal * 18, start - normal * 15, end - normal * 15, end - normal * 18, color);
            }
        }

        private static void Quad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color shade)
        {
            int start = helper.currentVertCount;
            helper.AddVert(a, shade, Vector2.zero); helper.AddVert(b, shade, Vector2.zero);
            helper.AddVert(c, shade, Vector2.zero); helper.AddVert(d, shade, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
