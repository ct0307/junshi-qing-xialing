using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 只绘制技能当前朝向的几何示意；普攻射程和条件追击仍由独立提示说明。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HeroSkillPreview : MaskableGraphic
    {
        public static readonly Color CurrentColor = new Color(216f / 255, 188f / 255, 122f / 255, .94f);
        public static readonly Color NextColor = new Color(122f / 255, 222f / 255, 209f / 255, .94f);

        private const int CircleSegments = 72;
        private HeroDefinition definition;
        private int currentLevel;
        private Vector2 origin;
        private Vector2 target;
        private bool showing;
        private LevelDefinition map;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        public void Show(HeroDefinition definition, int currentLevel, Point2 origin, Point2 target)
        { Show(definition, currentLevel, origin, target, null); }

        public void Show(HeroDefinition definition, int currentLevel, Point2 origin, Point2 target, LevelDefinition map)
        {
            if (definition == null || definition.Range <= 0 || !Supported(definition.Skill))
            { Clear(); return; }
            int stage = Mathf.Clamp(currentLevel, 1, BattleSession.MaxHeroLevel);
            Vector2 from = new Vector2(origin.X, origin.Y);
            Vector2 to = new Vector2(target.X, target.Y);
            if (showing && this.definition == definition && this.currentLevel == stage && this.origin == from && this.target == to && this.map == map) return;
            this.definition = definition;
            this.currentLevel = stage;
            this.origin = from;
            this.target = to;
            this.map = map;
            showing = true;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (!showing) return;
            showing = false;
            definition = null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!showing || definition == null) return;
            // 下一阶先绘制，让新增覆盖留在当前金色形状外侧。
            if (currentLevel < BattleSession.MaxHeroLevel)
                DrawStage(mesh, currentLevel + 1, NextColor, .09f, 3.5f);
            DrawStage(mesh, currentLevel, CurrentColor, .13f, 2f);
        }

        private void DrawStage(VertexHelper mesh, int level, Color edgeColor, float fillAlpha, float edgeWidth)
        {
            HeroGrowthProfile growth = HeroGrowth.Resolve(definition, level);
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < .001f) direction = Vector2.right;
            else direction.Normalize();
            float range = definition.Range;
            List<Vector2> polygon;
            switch (definition.Skill)
            {
                case "pierce":
                    polygon = ForwardStrip(origin, direction, range, growth.LineWidth);
                    break;
                case "cleave":
                    polygon = growth.FullCircle ? Circle(origin, range) :
                        Sector(origin, direction, range, level >= 2 ? Mathf.PI * .5f : Mathf.Acos(.3f));
                    break;
                case "duelist":
                    // 规则先按守将射程筛敌，再以目标或自身为圆心横扫。
                    polygon = Circle(growth.FullCircle ? origin : target, growth.AreaRadius);
                    if (!growth.FullCircle) polygon = ClipToCircle(polygon, origin, range);
                    break;
                case "stun":
                    polygon = Circle(origin, growth.OuterControlRadius);
                    break;
                case "fire":
                    if (level >= 2 && map != null) { DrawFireBand(mesh, edgeColor, fillAlpha); return; }
                    polygon = Circle(target, 95);
                    break;
                case "slow":
                    // 一阶只有目标迟滞，二阶起才有持续风眼；三阶第二区需另一真实目标。
                    if (level < 2) return;
                    polygon = Circle(target, 105);
                    break;
                default:
                    return;
            }
            if (polygon.Count < 3) return;
            Color fillColor = edgeColor;
            fillColor.a = fillAlpha;
            Fill(mesh, polygon, fillColor);
            for (int i = 0; i < polygon.Count; i++)
                Line(mesh, polygon[i], polygon[(i + 1) % polygon.Count], edgeWidth, edgeColor);
        }

        private static bool Supported(string skill)
        { return skill == "pierce" || skill == "cleave" || skill == "duelist" || skill == "stun" || skill == "fire" || skill == "slow"; }

        private void DrawFireBand(VertexHelper mesh, Color edgeColor, float fillAlpha)
        {
            RouteDefinition closest = null;
            float best = float.MaxValue, travel = 0;
            foreach (RouteDefinition route in map.Routes)
            {
                float cumulative = 0;
                for (int i = 1; i < route.Points.Count; i++)
                {
                    Vector2 a = new Vector2(route.Points[i - 1].X, route.Points[i - 1].Y), b = new Vector2(route.Points[i].X, route.Points[i].Y);
                    Vector2 delta = b - a;
                    float fraction = delta.sqrMagnitude > .001f ? Mathf.Clamp01(Vector2.Dot(target - a, delta) / delta.sqrMagnitude) : 0;
                    float distance = Vector2.Distance(target, a + fraction * delta);
                    if (distance < best) { best = distance; closest = route; travel = cumulative + fraction * delta.magnitude; }
                    cumulative += delta.magnitude;
                }
            }
            if (closest == null) return;
            float travelled = 0;
            foreach (var segment in Segments(closest))
            {
                Vector2 delta = segment[1] - segment[0]; float length = delta.magnitude;
                float start = Mathf.Max(travel - 75, travelled), end = Mathf.Min(travel + 115, travelled + length);
                if (end > start && length > .001f)
                {
                    Vector2 a = segment[0] + delta * ((start - travelled) / length), b = segment[0] + delta * ((end - travelled) / length);
                    Color fill = edgeColor; fill.a = fillAlpha;
                    Line(mesh, a, b, 34, fill); Line(mesh, a, b, 3, edgeColor);
                }
                travelled += length;
            }
        }

        private static IEnumerable<Vector2[]> Segments(RouteDefinition route)
        {
            for (int i = 1; i < route.Points.Count; i++)
                yield return new[] { new Vector2(route.Points[i - 1].X, route.Points[i - 1].Y), new Vector2(route.Points[i].X, route.Points[i].Y) };
        }

        private static List<Vector2> Circle(Vector2 center, float radius)
        {
            var result = new List<Vector2>(CircleSegments);
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2 / CircleSegments;
                result.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            return result;
        }

        private static List<Vector2> Sector(Vector2 center, Vector2 direction, float radius, float halfAngle)
        {
            int segments = Mathf.Max(12, Mathf.CeilToInt(CircleSegments * halfAngle / Mathf.PI));
            float facing = Mathf.Atan2(direction.y, direction.x);
            var result = new List<Vector2>(segments + 2) { center };
            for (int i = 0; i <= segments; i++)
            {
                float angle = facing + Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
                result.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            return result;
        }

        private static List<Vector2> ForwardStrip(Vector2 center, Vector2 direction, float radius, float halfWidth)
        {
            float width = Mathf.Min(radius, Mathf.Max(0, halfWidth));
            float angle = Mathf.Asin(width / radius);
            Vector2 normal = new Vector2(-direction.y, direction.x);
            var result = new List<Vector2>(28) { center - normal * width };
            // 枪线由前向半平面、固定半宽和射程圆相交，末端不是无限长矩形。
            for (int i = 0; i <= 24; i++)
            {
                float sweep = Mathf.Lerp(-angle, angle, i / 24f);
                result.Add(center + direction * (Mathf.Cos(sweep) * radius) + normal * (Mathf.Sin(sweep) * radius));
            }
            result.Add(center + normal * width);
            return result;
        }

        private static List<Vector2> ClipToCircle(List<Vector2> polygon, Vector2 center, float radius)
        {
            List<Vector2> boundary = Circle(center, radius);
            for (int edge = 0; edge < boundary.Count && polygon.Count > 0; edge++)
            {
                Vector2 a = boundary[edge], b = boundary[(edge + 1) % boundary.Count];
                var clipped = new List<Vector2>(polygon.Count + 1);
                Vector2 previous = polygon[polygon.Count - 1];
                float previousDistance = Cross(b - a, previous - a);
                foreach (Vector2 point in polygon)
                {
                    float distance = Cross(b - a, point - a);
                    bool inside = distance >= -.001f, previousInside = previousDistance >= -.001f;
                    if (inside != previousInside)
                    {
                        float fraction = previousDistance / (previousDistance - distance);
                        clipped.Add(Vector2.Lerp(previous, point, fraction));
                    }
                    if (inside) clipped.Add(point);
                    previous = point;
                    previousDistance = distance;
                }
                polygon = clipped;
            }
            return polygon;
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        private static void Fill(VertexHelper mesh, List<Vector2> polygon, Color color)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in polygon) center += point;
            center /= polygon.Count;
            int start = mesh.currentVertCount;
            Vertex(mesh, center, color);
            foreach (Vector2 point in polygon) Vertex(mesh, point, color);
            for (int i = 0; i < polygon.Count; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % polygon.Count);
        }

        private static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return;
            Vector2 edge = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            Vertex(mesh, from + edge, color); Vertex(mesh, to + edge, color);
            Vertex(mesh, to - edge, color); Vertex(mesh, from - edge, color);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Vertex(VertexHelper mesh, Vector2 point, Color color)
        { mesh.AddVert(new Vector3(point.x, -point.y), color, Vector2.zero); }
    }
}
