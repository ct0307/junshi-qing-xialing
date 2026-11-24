using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 离线美术实验的网格与材质工具，不参与正式游戏运行。
public static class MiniatureArt
{
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    public const float MapPitch = 50;

    public static void ResetMaterials() { materials.Clear(); }

    public static Vector3 At(float x, float y, float height = 0)
    {
        return new Vector3((x - 960) / 100, height, (540 - y) / (100 * Mathf.Sin(MapPitch * Mathf.Deg2Rad)));
    }

    public static Material Mat(string hex, float metallic = 0, float smoothness = .25f)
    {
        string key = hex + ":" + metallic + ":" + smoothness;
        Material value;
        // 切换离线场景会销毁非资产材质，不能把Unity的失效引用复用于下一位武将。
        if (materials.TryGetValue(key, out value) && value != null && value.shader != null) return value;
        Color color;
        if (!ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out color))
            throw new ArgumentException("材质颜色格式错误：" + hex);
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("缺少Unity内置Standard着色器");
        value = new Material(shader) { name = "微缩材质" + hex, color = color };
        value.SetFloat("_Metallic", metallic);
        value.SetFloat("_Glossiness", smoothness);
        materials[key] = value;
        return value;
    }

    public static Transform Group(Transform parent, string name, Vector3 position)
    {
        var result = new GameObject(name).transform;
        result.SetParent(parent, false);
        result.localPosition = position;
        return result;
    }

    public static GameObject Patch(Transform parent, string name, Vector3[] vertices, int[] tris, Material material)
    {
        var mesh = new Mesh { name = name };
        if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return MeshObject(parent, name, mesh, material);
    }

    private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var result = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        result.transform.SetParent(parent, false);
        result.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = result.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return result;
    }

    public static GameObject Ellipsoid(Transform parent, string name, Vector3 position, Vector3 scale,
        Material material, int longitude = 32, int latitude = 20)
    {
        var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new List<int>();
        for (int row = 0; row <= latitude; row++)
        {
            float phi = Mathf.PI * row / latitude;
            for (int column = 0; column <= longitude; column++)
            {
                float theta = 2 * Mathf.PI * column / longitude;
                Vector3 normal = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                int index = row * (longitude + 1) + column;
                vertices[index] = Vector3.Scale(normal * .5f, scale);
                normals[index] = new Vector3(normal.x / Mathf.Max(.001f, scale.x), normal.y / Mathf.Max(.001f, scale.y),
                    normal.z / Mathf.Max(.001f, scale.z)).normalized;
                uv[index] = new Vector2((float)column / longitude, (float)row / latitude);
                if (row == latitude || column == longitude) continue;
                int below = index + longitude + 1;
                if (row > 0) triangles.AddRange(new[] { index, index + 1, below });
                if (row < latitude - 1) triangles.AddRange(new[] { index + 1, below + 1, below });
            }
        }
        var mesh = new Mesh { name = name, vertices = vertices, normals = normals, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateBounds();
        var result = MeshObject(parent, name, mesh, material);
        result.transform.localPosition = position;
        return result;
    }

    public static GameObject Lathe(Transform parent, string name, Vector2[] radiusHeight, Vector3 position,
        Material material, int sides = 32)
    {
        if (radiusHeight.Length < 2) throw new ArgumentException("旋转体至少需要两个截面");
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        for (int row = 0; row < radiusHeight.Length; row++)
        {
            Vector2 section = radiusHeight[row];
            Vector2 before = radiusHeight[Mathf.Max(0, row - 1)];
            Vector2 after = radiusHeight[Mathf.Min(radiusHeight.Length - 1, row + 1)];
            float deltaRadius = after.x - before.x;
            float deltaHeight = after.y - before.y;
            for (int column = 0; column <= sides; column++)
            {
                float theta = Mathf.PI * 2 * column / sides;
                vertices.Add(new Vector3(Mathf.Cos(theta) * section.x, section.y, Mathf.Sin(theta) * section.x));
                Vector3 normal = new Vector3(Mathf.Cos(theta) * deltaHeight, -deltaRadius, Mathf.Sin(theta) * deltaHeight);
                normals.Add(normal.sqrMagnitude > .000001f ? normal.normalized : new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)));
                if (row == radiusHeight.Length - 1 || column == sides) continue;
                int a = row * (sides + 1) + column, b = a + sides + 1;
                triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }
        // 独立封口法线保留侧面与端面的明确转折。
        for (int end = 0; end < 2; end++)
        {
            Vector2 section = radiusHeight[end == 0 ? 0 : radiusHeight.Length - 1];
            Vector3 normal = end == 0 ? Vector3.down : Vector3.up;
            int center = vertices.Count;
            vertices.Add(new Vector3(0, section.y, 0)); normals.Add(normal);
            for (int i = 0; i <= sides; i++)
            {
                float angle = Mathf.PI * 2 * i / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * section.x, section.y, Mathf.Sin(angle) * section.x));
                normals.Add(normal);
                if (i == sides) continue;
                int a = center + i + 1, b = a + 1;
                triangles.AddRange(end == 0 ? new[] { center, a, b } : new[] { center, b, a });
            }
        }
        var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray(), normals = normals.ToArray() };
        mesh.RecalculateBounds();
        var result = MeshObject(parent, name, mesh, material);
        result.transform.localPosition = position;
        return result;
    }

    public static GameObject Cylinder(Transform parent, string name, Vector3 a, Vector3 b, float radius,
        Material material, int sides = 16)
    {
        float length = Vector3.Distance(a, b);
        if (length < .0001f) return Ellipsoid(parent, name, a, Vector3.one * radius * 2, material);
        var result = Lathe(parent, name, new[] { new Vector2(radius, 0), new Vector2(radius, length) }, a, material, sides);
        result.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        return result;
    }

    public static GameObject Tube(Transform parent, string name, Vector3[] points, float radius, Material material, int sides = 12)
    {
        Transform group = Group(parent, name, Vector3.zero);
        for (int i = 1; i < points.Length; i++) Cylinder(group, name + "段", points[i - 1], points[i], radius, material, sides);
        for (int i = 1; i < points.Length - 1; i++) Ellipsoid(group, name + "接点", points[i], Vector3.one * radius * 2, material, sides, 10);
        return group.gameObject;
    }

    public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, float bevel = .04f)
    {
        Vector3 half = size * .5f;
        float radius = Mathf.Clamp(bevel, .001f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .7f);
        var vertices = new List<Vector3>();
        var tris = new List<int>();
        for (int ring = 0; ring < 4; ring++)
        {
            bool end = ring == 0 || ring == 3;
            float x = half.x - (end ? radius : 0), z = half.z - (end ? radius : 0);
            float y = ring == 0 ? -half.y : ring == 1 ? -half.y + radius : ring == 2 ? half.y - radius : half.y;
            float cut = end ? radius * .25f : radius;
            var outline = new[] { new Vector3(x - cut, y, z), new Vector3(x, y, z - cut),
                new Vector3(x, y, -z + cut), new Vector3(x - cut, y, -z), new Vector3(-x + cut, y, -z),
                new Vector3(-x, y, -z + cut), new Vector3(-x, y, z - cut), new Vector3(-x + cut, y, z) };
            vertices.AddRange(outline);
            if (ring == 0) continue;
            for (int i = 0; i < 8; i++)
            {
                int a = (ring - 1) * 8 + i, b = (ring - 1) * 8 + (i + 1) % 8;
                int c = ring * 8 + (i + 1) % 8, d = ring * 8 + i;
                tris.AddRange(new[] { a, b, c, a, c, d });
            }
        }
        for (int end = 0; end < 2; end++)
        {
            int center = vertices.Count, offset = end * 24;
            vertices.Add(new Vector3(0, end == 0 ? -half.y : half.y, 0));
            for (int i = 0; i < 8; i++)
            {
                int a = offset + i, b = offset + (i + 1) % 8;
                tris.AddRange(end == 0 ? new[] { center, b, a } : new[] { center, a, b });
            }
        }
        var result = Patch(parent, name, vertices.ToArray(), tris.ToArray(), material);
        result.transform.localPosition = position;
        return result;
    }
}
