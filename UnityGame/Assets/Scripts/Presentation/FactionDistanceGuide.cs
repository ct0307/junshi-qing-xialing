using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 逻辑220距离在地面投影中绘制，不使用压缩后的屏幕距离判断资格。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FactionDistanceGuide : MaskableGraphic
    {
        private readonly List<Point2> ends = new List<Point2>();
        private Point2 origin;
        private bool showCircle;
        public void Show(Point2 point, IList<HeroUnit> heroes, HeroUnit selected, bool qun)
        {
            raycastTarget = false; origin = point; showCircle = qun; ends.Clear();
            HeroUnit closest = null; float best = float.MaxValue;
            foreach (HeroUnit hero in heroes)
            {
                if (hero == selected) continue;
                float distance = Point2.Distance(hero.Position, point);
                if (qun && distance < best) { closest = hero; best = distance; }
                if (!qun && hero.Definition.Faction == "qun" && distance < FactionRules.IsolationDistance) ends.Add(hero.Position);
            }
            if (closest != null) ends.Add(closest.Position);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 a = new Vector2(origin.X, -origin.Y);
            foreach (Point2 end in ends)
                Line(mesh, a, new Vector2(end.X, -end.Y), new Color(.69f, .38f, .23f, .55f));
            if (showCircle)
                for (int i = 0; i < 64; i++)
                {
                    if (i % 2 != 0) continue;
                    float angle = i * Mathf.PI / 32, next = (i + 1) * Mathf.PI / 32;
                    Line(mesh, a + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 220,
                        a + new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * 220, new Color(.18f, .42f, .34f, .4f));
                }
        }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color color)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * 1.5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero); mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero); mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
