using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// 仅供离线美术样稿使用；不生成阵位、武将、文字或运行时脚本。
public static class MiniatureMap
{
    private const float RoadWidth = 85;
    private const float HeightToPixels = 64.27876f;
    private const float DepthToPixels = 76.60444f;

    [Serializable] private sealed class DatabaseData { public LevelData[] Levels; }
    [Serializable] private sealed class LevelData
    {
        public string Id;
        public RouteData[] Routes;
        public SlotData[] Slots;
    }
    [Serializable] private sealed class RouteData { public string Id; public PointData[] Points; }
    [Serializable] private sealed class SlotData { public int Id; public PointData Position; }
    [Serializable] private sealed class PointData { public float X; public float Y; }

    private sealed class MapContext
    {
        public readonly Transform Root;
        public readonly LevelData Level;
        public readonly List<Rect> Occupied = new List<Rect>();
        public int Placed;
        public int Rejected;

        public MapContext(Transform root, LevelData level) { Root = root; Level = level; }

        // 高度投影朝画面上方延伸，不能只检查树根或房屋地基。
        public bool Reserve(float x, float y, float halfWidth, float halfDepth, float height,
            bool allowRoad = false, bool occupy = true)
        {
            Rect bounds = new Rect(x - halfWidth, y - halfDepth - height * HeightToPixels,
                halfWidth * 2, halfDepth * 2 + height * HeightToPixels);
            if (x < 42 || x > 1878 || y < 150 || y > 830) return Reject();
            foreach (SlotData slot in Level.Slots)
            {
                Rect clear = new Rect(slot.Position.X - 68, slot.Position.Y - 52, 136, 104);
                if (bounds.Overlaps(clear)) return Reject();
            }
            if (!allowRoad)
            {
                // 对道路中心线做矩形扩张，包含半幅路面和八像素留白。
                Rect roadBounds = Grow(bounds, RoadWidth * .5f + 8);
                foreach (RouteData route in Level.Routes)
                    for (int i = 1; i < route.Points.Length; i++)
                        if (SegmentHits(roadBounds, Point(route.Points[i - 1]), Point(route.Points[i])))
                            return Reject();
            }
            foreach (Rect other in Occupied)
                if (bounds.Overlaps(other)) return Reject();
            if (occupy) { Occupied.Add(bounds); Placed++; }
            return true;
        }

        private bool Reject() { Rejected++; return false; }
    }

    public static GameObject Build(string databasePath)
    {
        if (string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
            throw new FileNotFoundException("找不到正式游戏数据库", databasePath);
        DatabaseData database = JsonUtility.FromJson<DatabaseData>(File.ReadAllText(databasePath, Encoding.UTF8));
        LevelData level = null;
        if (database != null && database.Levels != null)
            foreach (LevelData candidate in database.Levels)
                if (candidate.Id == "huangjin") { level = candidate; break; }
        if (level == null || level.Routes == null || level.Routes.Length == 0 || level.Slots == null)
            throw new InvalidDataException("正式数据库缺少黄巾关卡、道路或阵位");
        foreach (RouteData route in level.Routes)
            if (route.Points == null || route.Points.Length < 2)
                throw new InvalidDataException("黄巾道路至少需要两个正式折点");
        foreach (SlotData slot in level.Slots)
            if (slot.Position == null) throw new InvalidDataException("黄巾阵位缺少位置");

        var result = new GameObject("黄巾惊野_青绿微缩样稿");
        var context = new MapContext(result.transform, level);
        BuildTerrain(context);
        foreach (RouteData route in level.Routes) BuildRoad(context.Root, route);

        // 先保留终点，再布村舍，避免房屋高处与城楼屋檐穿插。
        BuildGate(context, level.Routes[0].Points[level.Routes[0].Points.Length - 1]);
        BuildHouse(context, "村舍_主屋", 115, 442, .80f, .48f, .57f, false);
        BuildHouse(context, "村舍_侧屋", 230, 475, .68f, .41f, .50f, true);
        BuildHouse(context, "村舍_小粮仓", 340, 421, .55f, .20f, .31f, false);

        BuildGrove(context, "田间榆簇", 430, 423, .74f, 1);
        BuildGrove(context, "田间柏簇", 745, 433, .80f, 2);
        BuildGrove(context, "东田榆簇", 1390, 435, .76f, 3);
        BuildField(context, 575, 408, 1.42f, .58f, 0);
        BuildField(context, 940, 410, 1.55f, .57f, 1);
        BuildField(context, 1180, 409, 1.40f, .56f, 2);

        // 上沿仅在没有部署位的东北空隙安排矮林，顶端保留裁剪余量。
        BuildGrove(context, "东北矮林", 1585, 222, .64f, 4);
        BuildRocks(context, "东北层岩", 1680, 198, .80f, .32f);
        BuildGrove(context, "东南林簇", 1794, 790, .70f, 5);
        BuildGrove(context, "西南小树", 80, 794, .54f, 6);
        BuildRocks(context, "东南石组", 1710, 814, .62f, .23f);
        BuildRocks(context, "西北石组", 72, 205, .40f, .23f);

        Vector2[] grasses = {
            new Vector2(72, 380), new Vector2(266, 393), new Vector2(310, 421),
            new Vector2(685, 410), new Vector2(824, 414), new Vector2(1061, 404),
            new Vector2(1311, 408), new Vector2(1466, 472), new Vector2(1510, 475),
            new Vector2(72, 622), new Vector2(92, 748), new Vector2(1865, 580),
            new Vector2(1844, 731), new Vector2(1667, 750), new Vector2(1690, 257)
        };
        for (int i = 0; i < grasses.Length; i++)
            BuildGrass(context, grasses[i].x, grasses[i].y, i);

        int pointCount = 0;
        foreach (RouteData route in level.Routes) pointCount += route.Points.Length;
        Debug.Log("黄巾微缩地图：原样读取" + level.Routes.Length + "条道路、" + pointCount +
            "个折点、" + level.Slots.Length + "个阵位；布景" + context.Placed +
            "组，避让跳过" + context.Rejected + "组。未烘焙阵位与文字。");
        return result;
    }

    private static void BuildTerrain(MapContext context)
    {
        Transform root = context.Root;
        // 四层轮廓保留厚土剖面与圆润草皮边，台面保持世界高度零。
        Vector3[][] rings = {
            RoundedRing(34, 140, 1886, 850, 49, -.40f),
            RoundedRing(24, 132, 1896, 858, 57, -.28f),
            RoundedRing(24, 128, 1896, 854, 57, -.085f),
            RoundedRing(36, 140, 1884, 842, 47, 0)
        };
        AddRingBand(root, "厚土下切面", rings[0], rings[1], MiniatureArt.Mat("#696A4A"));
        AddRingBand(root, "暖土剖面", rings[1], rings[2], MiniatureArt.Mat("#9B956E"));
        AddRingBand(root, "草皮圆角", rings[2], rings[3], MiniatureArt.Mat("#84946B"));
        var vertices = new List<Vector3> { MiniatureArt.At(960, 491) };
        vertices.AddRange(rings[3]);
        var triangles = new List<int>();
        for (int i = 0; i < rings[3].Length; i++)
            triangles.AddRange(new[] { 0, i + 1, (i + 1) % rings[3].Length + 1 });
        GameObject surface = MiniatureArt.Patch(root, "柔绿草土地表", vertices.ToArray(), triangles.ToArray(),
            BuildGroundMaterial(context.Level));
        Vector2[] uv = new Vector2[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
            uv[i] = new Vector2((vertices[i].x * 100 + 960 - 36) / 1848,
                (540 - vertices[i].z * DepthToPixels - 140) / 702);
        surface.GetComponent<MeshFilter>().sharedMesh.uv = uv;

        // 侧面短层线只在台地外缘出现，不进入游戏路径与阵位。
        Vector3[] seam = {
            MiniatureArt.At(95, 854, -.20f), MiniatureArt.At(510, 854, -.20f),
            MiniatureArt.At(1030, 854, -.20f), MiniatureArt.At(1530, 854, -.20f),
            MiniatureArt.At(1842, 854, -.20f)
        };
        MiniatureArt.Tube(root, "土层细脉", seam, .013f, MiniatureArt.Mat("#B3AA7E"), 8);
    }

    private static Material BuildGroundMaterial(LevelData level)
    {
        // 低频色差来自原生程序纹理，地表仍为严格的零高度平面。
        const int width = 512, height = 256;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "草土低频色过渡_无阵位标记";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[width * height];
        Color baseColor = Hex("#98A17A"), dryColor = Hex("#ABA581");
        Color greenColor = Hex("#849974"), mossColor = Hex("#8F9D79");
        for (int row = 0; row < height; row++)
            for (int column = 0; column < width; column++)
            {
                float u = (float)column / (width - 1), v = (float)row / (height - 1);
                Vector2 p = new Vector2(36 + u * 1848, 140 + v * 702);
                float clear = 1;
                foreach (SlotData slot in level.Slots)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(p.x - slot.Position.X) - 68);
                    float dy = Mathf.Max(0, Mathf.Abs(p.y - slot.Position.Y) - 52);
                    clear = Mathf.Min(clear, Mathf.SmoothStep(0, 1, Mathf.Sqrt(dx * dx + dy * dy) / 34));
                }
                float routeDistance = float.MaxValue;
                foreach (RouteData route in level.Routes)
                    for (int i = 1; i < route.Points.Length; i++)
                        routeDistance = Mathf.Min(routeDistance,
                            DistanceToSegment(p, Point(route.Points[i - 1]), Point(route.Points[i])));
                clear *= Mathf.SmoothStep(0, 1, (routeDistance - 44) / 28);
                float low = Mathf.PerlinNoise(u * 5.3f + 17.2f, v * 3.4f + 8.1f) - .5f;
                float medium = Mathf.PerlinNoise(u * 12.5f + 4.2f, v * 8.0f + 21.7f) - .5f;
                Color color = baseColor;
                color = Color.Lerp(color, dryColor, GroundBlend(p, 290, 437, 245, 108, 2.1f) * .62f * clear);
                color = Color.Lerp(color, greenColor, GroundBlend(p, 795, 443, 300, 105, 8.7f) * .56f * clear);
                color = Color.Lerp(color, dryColor, GroundBlend(p, 1290, 438, 275, 120, 13.3f) * .36f * clear);
                color = Color.Lerp(color, mossColor, GroundBlend(p, 1765, 763, 180, 112, 19.2f) * .65f * clear);
                float shade = (low * .075f + medium * .018f) * clear;
                pixels[row * width + column] = new Color(color.r + shade, color.g + shade, color.b + shade * .78f, 1);
            }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        var material = new Material(MiniatureArt.Mat("#FFFFFF", 0, .12f));
        material.name = "哑光草土_低频自然过渡";
        material.mainTexture = texture;
        return material;
    }

    private static float GroundBlend(Vector2 p, float x, float y, float radiusX, float radiusY, float seed)
    {
        float dx = (p.x - x) / radiusX, dy = (p.y - y) / radiusY;
        float edge = Mathf.PerlinNoise(p.x / 170 + seed, p.y / 125 + seed) * .30f;
        return 1 - Mathf.SmoothStep(.10f, 1, Mathf.Sqrt(dx * dx + dy * dy) + edge);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, segment) / Mathf.Max(.001f, segment.sqrMagnitude));
        return Vector2.Distance(p, a + segment * t);
    }

    private static Color Hex(string value)
    {
        Color result;
        if (!ColorUtility.TryParseHtmlString(value, out result)) throw new ArgumentException("草土颜色无效");
        return result;
    }

    private static Vector3[] RoundedRing(float left, float top, float right, float bottom, float radius, float height)
    {
        var result = new List<Vector3>();
        Vector2[] centers = {
            new Vector2(right - radius, top + radius), new Vector2(right - radius, bottom - radius),
            new Vector2(left + radius, bottom - radius), new Vector2(left + radius, top + radius)
        };
        for (int corner = 0; corner < 4; corner++)
            for (int step = 0; step <= 10; step++)
            {
                float angle = (-90 + corner * 90 + step * 9) * Mathf.Deg2Rad;
                Vector2 p = centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                result.Add(MiniatureArt.At(p.x, p.y, height));
            }
        return result.ToArray();
    }

    private static void AddRingBand(Transform root, string name, Vector3[] lower, Vector3[] upper, Material material)
    {
        var vertices = new List<Vector3>();
        var tris = new List<int>();
        // 每段独立顶点，草皮与厚土保持干净的层次转折。
        for (int i = 0; i < lower.Length; i++)
        {
            int next = (i + 1) % lower.Length;
            int index = vertices.Count;
            vertices.AddRange(new[] { upper[i], lower[i], lower[next], upper[next] });
            tris.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
        }
        MiniatureArt.Patch(root, name, vertices.ToArray(), tris.ToArray(), material);
    }

    private static void BuildRoad(Transform root, RouteData route)
    {
        Transform group = MiniatureArt.Group(root, "正式土路_" + route.Id, Vector3.zero);
        // 带宽先在逻辑像素坐标内计算；不在世界空间重画或平滑中心线。
        RoadStrip(group, "浅色路肩", route.Points, RoadWidth, 0, .003f, MiniatureArt.Mat("#B2AD83"));
        RoadStrip(group, "压实土路", route.Points, 77, 0, .007f, MiniatureArt.Mat("#CEBF95"));
        RoadStrip(group, "细土路心", route.Points, 68, 0, .010f, MiniatureArt.Mat("#D6C69E"));
        RoadStrip(group, "左侧淡车辙", route.Points, 1.9f, -19, .012f, MiniatureArt.Mat("#C9B991"));
        RoadStrip(group, "右侧淡车辙", route.Points, 1.9f, 19, .012f, MiniatureArt.Mat("#C9B991"));
    }

    private static void RoadStrip(Transform root, string name, PointData[] points, float width, float offset,
        float height, Material material)
    {
        var vertices = new Vector3[points.Length * 2];
        var triangles = new int[(points.Length - 1) * 6];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 before = Point(points[Mathf.Max(0, i - 1)]);
            Vector2 current = Point(points[i]);
            Vector2 after = Point(points[Mathf.Min(points.Length - 1, i + 1)]);
            Vector2 incoming = (current - before).normalized;
            Vector2 outgoing = (after - current).normalized;
            if (i == 0) incoming = outgoing;
            if (i == points.Length - 1) outgoing = incoming;
            Vector2 normal = new Vector2(-incoming.y - outgoing.y, incoming.x + outgoing.x).normalized;
            if (normal.sqrMagnitude < .01f) normal = new Vector2(-outgoing.y, outgoing.x);
            float denominator = Mathf.Max(.5f, Vector2.Dot(normal, new Vector2(-outgoing.y, outgoing.x)));
            Vector2 left = current + normal * ((offset + width * .5f) / denominator);
            Vector2 right = current + normal * ((offset - width * .5f) / denominator);
            vertices[i * 2] = MiniatureArt.At(left.x, left.y, height);
            vertices[i * 2 + 1] = MiniatureArt.At(right.x, right.y, height);
            if (i == points.Length - 1) continue;
            int t = i * 6, a = i * 2;
            triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
            triangles[t + 3] = a + 1; triangles[t + 4] = a + 3; triangles[t + 5] = a + 2;
        }
        MiniatureArt.Patch(root, name, vertices, triangles, material);
    }

    private static void BuildHouse(MapContext context, string name, float x, float y,
        float width, float wallHeight, float depth, bool darkRoof)
    {
        float height = wallHeight + .35f;
        float halfWidth = (width + .20f) * 51 + 2.5f;
        float halfDepth = (depth * .5f + .18f) * DepthToPixels;
        if (!context.Reserve(x, y, halfWidth, halfDepth, height)) return;
        Transform group = MiniatureArt.Group(context.Root, name, MiniatureArt.At(x, y));
        Material stone = MiniatureArt.Mat("#969B87");
        Material plaster = MiniatureArt.Mat(darkRoof ? "#CFCAA9" : "#E0D7B6");
        Material wood = MiniatureArt.Mat("#766D51");
        Material roof = MiniatureArt.Mat(darkRoof ? "#5C756F" : "#71867A");
        MiniatureArt.Box(group, "石基", new Vector3(0, .04f, 0), new Vector3(width + .09f, .08f, depth + .07f), stone);
        MiniatureArt.Box(group, "灰泥墙", new Vector3(0, wallHeight * .5f + .06f, 0),
            new Vector3(width, wallHeight, depth), plaster, .028f);
        MiniatureArt.Box(group, "前檐木梁", new Vector3(0, wallHeight + .035f, -depth * .5f - .012f),
            new Vector3(width + .03f, .055f, .05f), wood, .006f);
        MiniatureArt.Box(group, "木门", new Vector3(-width * .16f, wallHeight * .33f + .06f, -depth * .5f - .014f),
            new Vector3(width * .24f, wallHeight * .65f, .028f), wood, .007f);
        MiniatureArt.Box(group, "窗格底", new Vector3(width * .25f, wallHeight * .59f + .06f, -depth * .5f - .018f),
            new Vector3(width * .21f, wallHeight * .30f, .035f), MiniatureArt.Mat("#8B927B"), .006f);
        if (wallHeight > .3f)
        {
            MiniatureArt.Box(group, "窗竖格", new Vector3(width * .25f, wallHeight * .59f + .06f, -depth * .5f - .039f),
                new Vector3(.022f, wallHeight * .32f, .014f), wood, .002f);
            MiniatureArt.Box(group, "窗横格", new Vector3(width * .25f, wallHeight * .59f + .06f, -depth * .5f - .04f),
                new Vector3(width * .22f, .022f, .014f), wood, .002f);
        }
        Roof(group, "青瓦双坡顶", width + .20f, depth + .18f, wallHeight + .09f, .17f, roof);
        MiniatureArt.Box(group, "门前踏石", new Vector3(-width * .16f, .025f, -depth * .5f - .10f),
            new Vector3(width * .36f, .05f, .16f), stone, .024f);
    }

    private static void Roof(Transform root, string name, float width, float depth, float baseHeight, float rise, Material material)
    {
        Transform group = MiniatureArt.Group(root, name, Vector3.zero);
        float[] zs = { -depth * .5f, -depth * .41f, 0, depth * .41f, depth * .5f };
        float[] ys = { baseHeight + .043f, baseHeight, baseHeight + rise, baseHeight, baseHeight + .043f };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < zs.Length - 1; i++)
        {
            int index = vertices.Count;
            vertices.AddRange(new[] {
                new Vector3(-width * .5f, ys[i], zs[i]), new Vector3(width * .5f, ys[i], zs[i]),
                new Vector3(-width * .5f, ys[i + 1], zs[i + 1]), new Vector3(width * .5f, ys[i + 1], zs[i + 1])
            });
            triangles.AddRange(new[] { index, index + 2, index + 1, index + 1, index + 2, index + 3 });
        }
        MiniatureArt.Patch(group, "有折面瓦坡", vertices.ToArray(), triangles.ToArray(), material);
        Material edge = MiniatureArt.Mat("#4F6862");
        for (int side = -1; side <= 1; side += 2)
        {
            MiniatureArt.Cylinder(group, "卷边檐口", new Vector3(-width * .5f, ys[0], side * depth * .5f),
                new Vector3(width * .5f, ys[0], side * depth * .5f), .027f, edge, 10);
            // 山墙遮住瓦坡下的空洞，避免房屋像只有一张屋顶纸片。
            float end = side * width * .5f;
            Vector3[] endVertices = {
                new Vector3(end, baseHeight - .022f, -depth * .44f),
                new Vector3(end, baseHeight - .022f, depth * .44f),
                new Vector3(end, baseHeight + rise, 0)
            };
            MiniatureArt.Patch(group, "山墙封檐", endVertices,
                side > 0 ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 }, edge);
        }
        MiniatureArt.Tube(group, "轻翘瓦脊", new[] {
            new Vector3(-width * .51f, baseHeight + rise + .055f, 0),
            new Vector3(-width * .40f, baseHeight + rise + .015f, 0),
            new Vector3(width * .40f, baseHeight + rise + .015f, 0),
            new Vector3(width * .51f, baseHeight + rise + .055f, 0)
        }, .025f, edge, 10);
        // 少量瓦肋强化缩小后的体积，不逐片堆高频纹理。
        int ribs = Mathf.Clamp(Mathf.RoundToInt(width / .16f), 4, 8);
        for (int rib = 1; rib < ribs; rib++)
        {
            float ridgeX = Mathf.Lerp(-width * .46f, width * .46f, (float)rib / ribs);
            for (int side = -1; side <= 1; side += 2)
                MiniatureArt.Tube(group, "疏瓦肋", new[] {
                    new Vector3(ridgeX, baseHeight + rise + .006f, 0),
                    new Vector3(ridgeX, baseHeight + .01f, side * depth * .41f),
                    new Vector3(ridgeX, baseHeight + .049f, side * depth * .5f)
                }, .008f, material, 6);
        }
    }

    private static void BuildGate(MapContext context, PointData endpoint)
    {
        float x = endpoint.X - 8, y = endpoint.Y - 12;
        // 终点建筑是唯一允许接触道路的布景；部署位仍无例外地保留。
        if (!context.Reserve(x, y, 62, 42, 1.43f, true))
            throw new InvalidOperationException("黄巾终点城楼与正式阵位冲突，需调整离线样稿布景");
        Transform group = MiniatureArt.Group(context.Root, "终点_青瓦城楼", MiniatureArt.At(x, y));
        Material stone = MiniatureArt.Mat("#A5AA91");
        Material lightStone = MiniatureArt.Mat("#C0C2A7");
        Material wood = MiniatureArt.Mat("#6B725B");
        Material roof = MiniatureArt.Mat("#53776D");
        for (int side = -1; side <= 1; side += 2)
        {
            MiniatureArt.Box(group, "门洞石墩", new Vector3(0, .32f, side * .405f),
                new Vector3(.86f, .64f, .21f), stone, .045f);
            MiniatureArt.Box(group, "石墩压顶", new Vector3(0, .66f, side * .405f),
                new Vector3(.94f, .09f, .25f), lightStone, .026f);
            for (int row = 0; row < 3; row++)
                MiniatureArt.Box(group, "浅石缝", new Vector3(.434f, .14f + row * .17f, side * .405f),
                    new Vector3(.012f, .012f, .19f), MiniatureArt.Mat("#939C86"), .002f);
        }
        // 沿世界X贯通的门洞与路线终段方向一致。
        MiniatureArt.Box(group, "门洞过梁", new Vector3(0, .68f, 0), new Vector3(.91f, .16f, .87f), stone, .028f);
        Roof(group, "城楼下重檐", 1.08f, 1.03f, .76f, .16f, roof);
        MiniatureArt.Box(group, "楼阁墙芯", new Vector3(0, 1.00f, 0), new Vector3(.66f, .28f, .65f),
            MiniatureArt.Mat("#D4D0AC"), .02f);
        foreach (float pillarX in new[] { -.32f, .32f })
            foreach (float pillarZ in new[] { -.31f, .31f })
                MiniatureArt.Cylinder(group, "楼阁木柱", new Vector3(pillarX, .88f, pillarZ),
                    new Vector3(pillarX, 1.17f, pillarZ), .034f, wood, 10);
        MiniatureArt.Box(group, "楼阁窗", new Vector3(0, 1.025f, -.336f), new Vector3(.38f, .13f, .021f), wood, .005f);
        for (int i = -1; i <= 1; i++)
            MiniatureArt.Box(group, "窗棂", new Vector3(i * .11f, 1.025f, -.353f),
                new Vector3(.022f, .13f, .012f), MiniatureArt.Mat("#A3AB8F"), .002f);
        Roof(group, "城楼上重檐", 1.14f, .98f, 1.15f, .19f, roof);
    }

    private static void BuildField(MapContext context, float x, float y, float width, float depth, int variation)
    {
        if (!context.Reserve(x, y, (width + .10f) * 50, (depth + .10f) * DepthToPixels * .5f, .16f)) return;
        Transform group = MiniatureArt.Group(context.Root, "低田埂_" + variation, MiniatureArt.At(x, y));
        Material bank = MiniatureArt.Mat("#A09F79");
        Material earth = MiniatureArt.Mat(variation == 1 ? "#99996F" : "#A39C76");
        Material crop = MiniatureArt.Mat(variation == 2 ? "#AAAE7B" : "#98A473");
        FieldPatch(group, "不规则浅田界", width, depth, .012f, bank, variation);
        FieldPatch(group, "成片熟土", width * .94f, depth * .86f, .016f, earth, variation);
        int rowCount = variation == 1 ? 4 : 3;
        for (int row = 0; row < rowCount; row++)
        {
            float z = Mathf.Lerp(-depth * .28f, depth * .28f, (float)row / (rowCount - 1));
            float reach = width * (.37f + .025f * ((row + variation) % 3));
            MiniatureArt.Tube(group, "柔缓作物垄", new[] {
                new Vector3(-reach, .034f, z - .012f), new Vector3(-reach * .48f, .034f, z + .010f),
                new Vector3(0, .034f, z), new Vector3(reach * .53f, .034f, z + .015f),
                new Vector3(reach, .034f, z - .006f)
            }, .014f, crop, 8);
            for (int clump = 0; clump < 2; clump++)
            {
                float stemX = (clump == 0 ? -.23f : .22f) * width + (row % 2) * .07f;
                MiniatureArt.Ellipsoid(group, "稀疏穗团", new Vector3(stemX, .075f, z),
                    new Vector3(.035f, .085f, .030f), crop, 10, 8);
            }
        }
    }

    private static void FieldPatch(Transform root, string name, float width, float depth,
        float height, Material material, int variation)
    {
        Vector2[] outline = {
            new Vector2(-.50f, -.34f), new Vector2(-.34f, -.50f), new Vector2(.32f, -.46f),
            new Vector2(.49f, -.27f), new Vector2(.47f, .35f), new Vector2(.21f, .49f),
            new Vector2(-.36f, .44f), new Vector2(-.49f, .17f)
        };
        var vertices = new Vector3[outline.Length + 1];
        var tris = new int[outline.Length * 3];
        vertices[0] = new Vector3(0, height, 0);
        for (int i = 0; i < outline.Length; i++)
        {
            float irregular = 1 + Mathf.Sin(i * 2.7f + variation * 1.4f) * .035f;
            vertices[i + 1] = new Vector3(outline[i].x * width * irregular, height, outline[i].y * depth);
            tris[i * 3] = 0;
            tris[i * 3 + 1] = (i + 1) % outline.Length + 1;
            tris[i * 3 + 2] = i + 1;
        }
        MiniatureArt.Patch(root, name, vertices, tris, material);
    }

    private static void BuildGrove(MapContext context, string name, float x, float y, float height, int variation)
    {
        float scale = height / .78f;
        if (!context.Reserve(x, y, 45 * scale, 24 * scale, height)) return;
        Transform group = MiniatureArt.Group(context.Root, name, MiniatureArt.At(x, y));
        bool cypress = variation == 2;
        BuildTree(group, new Vector3(-.19f * scale, 0, .06f * scale), height * .85f, cypress, 0);
        BuildTree(group, new Vector3(.13f * scale, 0, .10f * scale), height, cypress, 1);
        BuildTree(group, new Vector3(.25f * scale, 0, -.10f * scale), height * .74f, cypress, 2);
        MiniatureArt.Ellipsoid(group, "树下苔台", new Vector3(.03f * scale, -.025f, 0),
            new Vector3(.73f * scale, .07f, .49f * scale), MiniatureArt.Mat("#83976E"), 24, 10);
    }

    private static void BuildTree(Transform root, Vector3 position, float height, bool cypress, int variation)
    {
        Transform group = MiniatureArt.Group(root, "圆冠小乔木_" + variation, position);
        float scale = height / .78f;
        Material trunk = MiniatureArt.Mat("#78785B");
        Material foliage = MiniatureArt.Mat(variation == 0 ? "#547C68" : variation == 1 ? "#668D70" : "#789777");
        MiniatureArt.Cylinder(group, "树干", Vector3.zero, new Vector3(.025f * scale, height * .64f, 0),
            .037f * scale, trunk, 10);
        MiniatureArt.Cylinder(group, "斜枝", new Vector3(0, height * .34f, 0),
            new Vector3(-.10f * scale, height * .59f, .015f), .019f * scale, trunk, 8);
        if (cypress)
        {
            MiniatureArt.Lathe(group, "收尖柏冠", new[] {
                new Vector2(.04f * scale, height * .27f), new Vector2(.16f * scale, height * .38f),
                new Vector2(.145f * scale, height * .62f), new Vector2(.10f * scale, height * .80f),
                new Vector2(.012f * scale, height)
            }, Vector3.zero, foliage, 20);
        }
        else
        {
            MiniatureArt.Ellipsoid(group, "主树冠", new Vector3(.01f * scale, height * .72f, 0),
                new Vector3(.35f * scale, height * .56f, .32f * scale), foliage, 24, 16);
            MiniatureArt.Ellipsoid(group, "左侧冠团", new Vector3(-.105f * scale, height * .60f, -.025f * scale),
                new Vector3(.23f * scale, height * .34f, .25f * scale), foliage, 20, 12);
            MiniatureArt.Ellipsoid(group, "右侧冠团", new Vector3(.14f * scale, height * .67f, .055f * scale),
                new Vector3(.24f * scale, height * .36f, .24f * scale), foliage, 20, 12);
        }
    }

    private static void BuildRocks(MapContext context, string name, float x, float y, float width, float height)
    {
        if (!context.Reserve(x, y, width * 55, width * 29, height)) return;
        Transform group = MiniatureArt.Group(context.Root, name, MiniatureArt.At(x, y));
        Material lower = MiniatureArt.Mat("#84958A");
        Material upper = MiniatureArt.Mat("#A9B4A1");
        MiniatureArt.Box(group, "下层页岩", new Vector3(-width * .08f, height * .20f, 0),
            new Vector3(width * .84f, height * .40f, width * .53f), lower, height * .15f);
        GameObject cap = MiniatureArt.Box(group, "偏移上层石", new Vector3(width * .05f, height * .56f, width * .035f),
            new Vector3(width * .61f, height * .36f, width * .43f), upper, height * .12f);
        cap.transform.localRotation = Quaternion.Euler(0, -13, 0);
        MiniatureArt.Ellipsoid(group, "侧卧卵石", new Vector3(width * .37f, height * .13f, -width * .17f),
            new Vector3(width * .31f, height * .26f, width * .28f), lower, 16, 10);
    }

    private static void BuildGrass(MapContext context, float x, float y, int variation)
    {
        if (!context.Reserve(x, y, 10, 9, .15f)) return;
        Transform group = MiniatureArt.Group(context.Root, "疏草_" + variation, MiniatureArt.At(x, y));
        Material grass = MiniatureArt.Mat(variation % 3 == 0 ? "#7F956E" : "#8C9D72");
        for (int i = 0; i < 3; i++)
        {
            float side = i - 1;
            MiniatureArt.Cylinder(group, "草叶", new Vector3(side * .035f, 0, 0),
                new Vector3(side * .07f, .10f + (i == 1 ? .035f : 0), .025f), .013f, grass, 6);
        }
    }

    private static Vector2 Point(PointData value) { return new Vector2(value.X, value.Y); }
    private static Rect Grow(Rect value, float amount)
    {
        return new Rect(value.xMin - amount, value.yMin - amount, value.width + amount * 2, value.height + amount * 2);
    }

    // Liang-Barsky线段裁剪；同时覆盖短折线、竖线和矩形内部的路线。
    private static bool SegmentHits(Rect rect, Vector2 a, Vector2 b)
    {
        float t0 = 0, t1 = 1;
        Vector2 delta = b - a;
        return Clip(-delta.x, a.x - rect.xMin, ref t0, ref t1) &&
            Clip(delta.x, rect.xMax - a.x, ref t0, ref t1) &&
            Clip(-delta.y, a.y - rect.yMin, ref t0, ref t1) &&
            Clip(delta.y, rect.yMax - a.y, ref t0, ref t1);
    }

    private static bool Clip(float direction, float distance, ref float first, ref float last)
    {
        if (Mathf.Abs(direction) < .00001f) return distance >= 0;
        float t = distance / direction;
        if (direction < 0) { if (t > last) return false; first = Mathf.Max(first, t); }
        else { if (t < first) return false; last = Mathf.Min(last, t); }
        return true;
    }
}
