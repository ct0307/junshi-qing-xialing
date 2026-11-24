using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 根据真实道路和规则射程绘制覆盖部分，不改变阵位或战斗距离。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattlePlacementGuide : MaskableGraphic
    {
        private LevelDefinition level;
        private Point2 position;
        private float radius;
        private bool showing;
        public float CoverageLength { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        public void Show(LevelDefinition level, Point2 position, float radius)
        {
            if (level == null || radius <= 0) { Clear(); return; }
            if (showing && this.level == level && Mathf.Approximately(this.position.X, position.X) &&
                Mathf.Approximately(this.position.Y, position.Y) && Mathf.Approximately(this.radius, radius)) return;
            this.level = level;
            this.position = position;
            this.radius = radius;
            CoverageLength = 0;
            foreach (RouteDefinition route in level.Routes)
                for (int i = 1; i < route.Points.Count; i++)
                {
                    Vector2 a = new Vector2(route.Points[i - 1].X, route.Points[i - 1].Y), b = new Vector2(route.Points[i].X, route.Points[i].Y);
                    Vector2 start, end;
                    if (Clip(a, b, new Vector2(position.X, position.Y), radius, out start, out end)) CoverageLength += Vector2.Distance(start, end);
                }
            showing = true;
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (!showing) return;
            showing = false;
            CoverageLength = 0;
            level = null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!showing || level == null) return;
            Color pathColor = UiFactory.Gold;
            pathColor.a = .48f;
            Vector2 center = new Vector2(position.X, position.Y);
            foreach (RouteDefinition route in level.Routes)
                for (int i = 1; i < route.Points.Count; i++)
                {
                    Vector2 a = new Vector2(route.Points[i - 1].X, route.Points[i - 1].Y);
                    Vector2 b = new Vector2(route.Points[i].X, route.Points[i].Y);
                    Vector2 start, end;
                    if (Clip(a, b, center, radius, out start, out end))
                        Line(mesh, start, end, 12, pathColor);
                }

            Color baseColor = UiFactory.Gold;
            baseColor.a = .17f;
            Ellipse(mesh, center, 66, 31, baseColor);
            Color outer = UiFactory.Gold;
            outer.a = .9f;
            Ring(mesh, center, 66, 31, 3, outer);
            Color inner = UiFactory.Paper;
            inner.a = .7f;
            Ring(mesh, center, 56, 24, 1.5f, inner);
        }

        private static bool Clip(Vector2 a, Vector2 b, Vector2 center, float radius,
            out Vector2 start, out Vector2 end)
        {
            start = end = Vector2.zero;
            Vector2 delta = b - a;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared < .0001f) return false;
            // 将圆心投影到直线，以投影距离求交点，避免长路线二次判别式的精度损失。
            float closest = Vector2.Dot(center - a, delta) / lengthSquared;
            float distanceSquared = (a + delta * closest - center).sqrMagnitude;
            float remaining = radius * radius - distanceSquared;
            if (remaining <= 0) return false;
            float extent = Mathf.Sqrt(remaining / lengthSquared);
            float from = Mathf.Max(0, closest - extent);
            float to = Mathf.Min(1, closest + extent);
            if (to <= from) return false;
            start = a + delta * from;
            end = a + delta * to;
            return true;
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 edge = new Vector2(-direction.y, direction.x) * width * .5f;
            int start = mesh.currentVertCount;
            Vertex(mesh, a + edge, color);
            Vertex(mesh, b + edge, color);
            Vertex(mesh, b - edge, color);
            Vertex(mesh, a - edge, color);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Ellipse(VertexHelper mesh, Vector2 center, float radiusX, float radiusY, Color color)
        {
            const int segments = 48;
            int start = mesh.currentVertCount;
            Vertex(mesh, center, color);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vertex(mesh, center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY), color);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float radiusX, float radiusY,
            float width, Color color)
        {
            const int segments = 48;
            int start = mesh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vertex(mesh, center + new Vector2(unit.x * radiusX, unit.y * radiusY), color);
                Vertex(mesh, center + new Vector2(unit.x * (radiusX - width), unit.y * (radiusY - width)), color);
                if (i == 0) continue;
                int previous = start + (i - 1) * 2;
                mesh.AddTriangle(previous, previous + 2, previous + 1);
                mesh.AddTriangle(previous + 1, previous + 2, previous + 3);
            }
        }

        private static void Vertex(VertexHelper mesh, Vector2 point, Color color)
        { mesh.AddVert(new Vector3(point.x, -point.y), color, Vector2.zero); }
    }
}
