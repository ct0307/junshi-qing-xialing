using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 黄巾郊野的静态景物合成到一个网格，始终位于阵位、人物和战斗反馈之后。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HuangjinScenery : MaskableGraphic
    {
        private LevelDefinition level;

        public static void Create(UiFactory ui, RectTransform ground, LevelDefinition level)
        {
            if (level.Id != "huangjin") return;
            RectTransform rect = ui.Rect(ground, "黄巾郊野层次", 0, 0, 1920, 1080);
            HuangjinScenery scenery = rect.gameObject.AddComponent<HuangjinScenery>();
            scenery.raycastTarget = false;
            scenery.level = level;
            scenery.SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (level == null) return;

            // 山脊下缘透明衔接原有地面，让顶部留白成为同一片郊野。
            Ridge(mesh, Shade("7D9273", .42f), Shade("7D9273", 0), 130,
                38, 16, 205, 0, 364, 25, 582, -9, 786, 23, 1000, 4,
                1218, 26, 1450, -3, 1667, 18, 1882, 7);
            Ridge(mesh, Shade("708C6A", .35f), Shade("829875", 0), 190,
                38, 95, 238, 76, 427, 97, 648, 61, 830, 89, 1065, 70,
                1277, 99, 1509, 63, 1690, 89, 1882, 74);
            Crest(mesh, Shade("B9C29B", .23f), 1.5f,
                49, 96, 238, 77, 426, 98, 648, 62, 830, 90, 1065, 71);

            // 远处营寨使用较低对比，黄旗成为地点标识，避免与己方姓名牌争抢注意。
            TreeGroup(mesh, 80, 90, .72f);
            TreeGroup(mesh, 575, 57, .7f);
            TreeGroup(mesh, 1120, 79, .8f);
            TreeGroup(mesh, 1788, 90, .72f);
            Camp(mesh, 1565, 105, .86f);
            Camp(mesh, 1673, 116, .65f);
            Flag(mesh, 1628, 112, 94, Shade("B2A060", .85f));

            // 回转路中间保留成片负空间，用两组田埂提供郊野的疏密与方向。
            Field(mesh, 430, 391, 300, 54);
            Field(mesh, 925, 394, 336, 49);
            Ridge(mesh, Shade("728A63", .19f), Shade("728A63", 0), 594,
                380, 552, 510, 538, 648, 553, 787, 547);
            Ridge(mesh, Shade("728A63", .17f), Shade("728A63", 0), 600,
                935, 563, 1088, 547, 1239, 560, 1392, 547);

            // 小景物逐一避开可点阵位和真实路线，只放在非交互空隙。
            Grass(mesh, 465, 408, 1);
            Grass(mesh, 755, 406, .85f);
            Grass(mesh, 1008, 419, .9f);
            Grass(mesh, 1304, 415, 1);
            Grass(mesh, 1452, 483, .75f);
            Grass(mesh, 734, 647, .85f);
            Grass(mesh, 1146, 642, .8f);
            Grass(mesh, 1716, 816, .95f);
            Stone(mesh, 75, 824, 22, 11);
            Stone(mesh, 1818, 794, 27, 14);
            Stone(mesh, 1515, 850, 20, 10);
            Stone(mesh, 82, 615, 17, 9);
            // 城门外侧压低的垒石与旗杆，与原有建筑合成小型守关营地。
            Stone(mesh, 71, 750, 15, 8);
            Stone(mesh, 94, 756, 13, 7);
            Flag(mesh, 83, 801, 41, Shade("759D8A", .64f));
        }

        private void Field(VertexHelper mesh, float x, float y, float width, float height)
        {
            Polygon(mesh, Shade("A4AB78", .17f), x, y + 11, x + width - 28, y,
                x + width, y + height - 10, x + 19, y + height);
            for (int i = 0; i < 4; i++)
            {
                float offset = i * 10;
                Line(mesh, x + 24, y + 15 + offset, x + width - 25, y + 5 + offset,
                    1.6f, Shade("75885D", .25f));
                Line(mesh, x + 24, y + 17 + offset, x + width - 25, y + 7 + offset,
                    1, Shade("C0BA87", .2f));
            }
        }

        private void TreeGroup(VertexHelper mesh, float x, float y, float scale)
        {
            Ellipse(mesh, x + 3, y + 12 * scale, 44 * scale, 9 * scale, Shade("677F62", .16f));
            Ellipse(mesh, x - 16 * scale, y - 7 * scale, 22 * scale, 21 * scale, Shade("70896C", .28f));
            Ellipse(mesh, x + 13 * scale, y - 15 * scale, 25 * scale, 27 * scale, Shade("6D876B", .28f));
            Ellipse(mesh, x + 35 * scale, y - 5 * scale, 17 * scale, 18 * scale, Shade("7D9472", .3f));
            Line(mesh, x + 12 * scale, y + 3 * scale, x + 12 * scale, y + 18 * scale,
                2 * scale, Shade("758365", .35f));
        }

        private void Camp(VertexHelper mesh, float x, float y, float scale)
        {
            Ellipse(mesh, x + 3, y + 7, 45 * scale, 11 * scale, Shade("526E53", .22f));
            Polygon(mesh, Shade("ACA576", .75f), x - 42 * scale, y,
                x + 2 * scale, y - 62 * scale, x + 45 * scale, y + 2 * scale);
            Polygon(mesh, Shade("D3C394", .7f), x - 42 * scale, y,
                x + 2 * scale, y - 62 * scale, x + 3 * scale, y + 3 * scale);
            Polygon(mesh, Shade("5D7356", .65f), x - 9 * scale, y + 1,
                x + 2 * scale, y - 29 * scale, x + 14 * scale, y + 2);
            Line(mesh, x + 2 * scale, y - 61 * scale, x + 44 * scale, y + 2 * scale,
                1.8f, Shade("E0CFA1", .55f));
            Line(mesh, x - 50 * scale, y + 5 * scale, x - 35 * scale, y - 13 * scale,
                1, Shade("7B825C", .55f));
            Line(mesh, x + 54 * scale, y + 5 * scale, x + 37 * scale, y - 12 * scale,
                1, Shade("7B825C", .55f));
        }

        private void Flag(VertexHelper mesh, float x, float baseY, float height, Color cloth)
        {
            Line(mesh, x, baseY, x, baseY - height, 2.2f, Shade("867D55", .7f));
            float top = baseY - height + 4;
            Polygon(mesh, cloth, x + 2, top, x + height * .48f, top + 5,
                x + height * .42f, top + height * .32f, x + 2, top + height * .27f);
            Line(mesh, x + 4, top + 2, x + height * .46f, top + 7, 1, Shade("E3D29E", .55f));
        }

        private void Grass(VertexHelper mesh, float x, float y, float scale)
        {
            if (!IsClear(x, y, 25)) return;
            Ellipse(mesh, x + 2, y + 2, 25 * scale, 7 * scale, Shade("6E8256", .14f));
            for (int i = 0; i < 5; i++)
            {
                float stemX = x + (i - 2) * 7 * scale;
                float height = (i % 2 == 0 ? 14 : 20) * scale;
                Color stem = Shade(i % 2 == 0 ? "B4AC70" : "8B9B67", .48f);
                Line(mesh, stemX, y, stemX + 2 * scale, y - height, 1.3f * scale, stem);
                Line(mesh, stemX + scale, y - height * .55f, stemX - 4 * scale, y - height * .85f,
                    1.1f * scale, stem);
            }
        }

        private void Stone(VertexHelper mesh, float x, float y, float width, float height)
        {
            if (!IsClear(x, y, width + 4)) return;
            Ellipse(mesh, x + 2, y + height * .5f, width * .62f, height * .42f, Shade("59724F", .2f));
            Polygon(mesh, Shade("899478", .65f), x - width * .5f, y + height * .2f,
                x - width * .34f, y - height * .5f, x + width * .22f, y - height * .6f,
                x + width * .5f, y + height * .14f, x + width * .16f, y + height * .47f);
            Line(mesh, x - width * .32f, y - height * .45f, x + width * .2f, y - height * .55f,
                1.2f, Shade("BBC0A2", .6f));
        }

        private bool IsClear(float x, float y, float radius)
        {
            foreach (SlotDefinition slot in level.Slots)
            {
                float dx = (x - slot.Position.X) / (62 + radius);
                float dy = (y - slot.Position.Y) / (36 + radius);
                if (dx * dx + dy * dy < 1) return false;
            }
            Vector2 point = new Vector2(x, y);
            foreach (RouteDefinition route in level.Routes)
                for (int i = 1; i < route.Points.Count; i++)
                {
                    Vector2 a = new Vector2(route.Points[i - 1].X, route.Points[i - 1].Y);
                    Vector2 b = new Vector2(route.Points[i].X, route.Points[i].Y);
                    Vector2 delta = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                    if ((point - a - delta * t).sqrMagnitude < (50 + radius) * (50 + radius)) return false;
                }
            return true;
        }

        private static Color Shade(string hex, float alpha)
        { Color value = UiFactory.Hex(hex); value.a = alpha; return value; }

        private static void Ridge(VertexHelper mesh, Color top, Color bottom, float floor, params float[] points)
        {
            for (int i = 0; i < points.Length - 2; i += 2)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(points[i], -points[i + 1]), top, Vector2.zero);
                mesh.AddVert(new Vector3(points[i + 2], -points[i + 3]), top, Vector2.zero);
                mesh.AddVert(new Vector3(points[i + 2], -floor), bottom, Vector2.zero);
                mesh.AddVert(new Vector3(points[i], -floor), bottom, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static void Crest(VertexHelper mesh, Color color, float width, params float[] points)
        {
            for (int i = 0; i < points.Length - 2; i += 2)
                Line(mesh, points[i], points[i + 1], points[i + 2], points[i + 3], width, color);
        }

        private static void Line(VertexHelper mesh, float x1, float y1, float x2, float y2, float width, Color color)
        {
            Vector2 direction = new Vector2(x2 - x1, y2 - y1).normalized;
            Vector2 edge = new Vector2(-direction.y, direction.x) * width * .5f;
            Polygon(mesh, color, x1 + edge.x, y1 + edge.y, x2 + edge.x, y2 + edge.y,
                x2 - edge.x, y2 - edge.y, x1 - edge.x, y1 - edge.y);
        }

        private static void Ellipse(VertexHelper mesh, float x, float y, float radiusX, float radiusY, Color color)
        {
            const int segments = 16;
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, -y), color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(new Vector3(x + Mathf.Cos(angle) * radiusX, -y - Mathf.Sin(angle) * radiusY), color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void Polygon(VertexHelper mesh, Color color, params float[] points)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i += 2)
                mesh.AddVert(new Vector3(points[i], -points[i + 1]), color, Vector2.zero);
            for (int i = 1; i < points.Length / 2 - 1; i++)
                mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
