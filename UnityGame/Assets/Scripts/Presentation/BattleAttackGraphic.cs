using UnityEngine;
using UnityEngine.UI;

namespace YanyiSandpan.Presentation
{
    // 仅由真实攻击事件短暂显示，与布阵射程预览分离。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleAttackGraphic : Graphic
    {
        private bool cone;
        private float range;
        private Vector2 direction;
        private float coneAngle = Mathf.Acos(.3f);
        private float lineWidth = 38;
        private bool edgeOnly;

        public void Configure(bool isCone, float distance, Vector2 aim, float halfAngle = -1, float width = 38, bool outlineOnly = false)
        {
            cone = isCone;
            edgeOnly = outlineOnly;
            range = Mathf.Max(0, distance);
            coneAngle = halfAngle < 0 ? Mathf.Acos(.3f) : halfAngle;
            lineWidth = Mathf.Max(0, width);
            direction = aim.sqrMagnitude > .001f ? aim.normalized : Vector2.right;
            raycastTarget = false;
            rectTransform.pivot = new Vector2(.5f, .5f);
            rectTransform.sizeDelta = new Vector2(range * 2, range * 2);
            rectTransform.anchoredPosition = Vector2.zero;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (range <= 0) return;
            if (edgeOnly && cone)
            {
                float facingAngle = Mathf.Atan2(direction.y, direction.x);
                for (int i = 0; i <= 64; i++)
                {
                    float angle = facingAngle + Mathf.Lerp(-coneAngle, coneAngle, i / 64f);
                    Vector2 ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    helper.AddVert(ray * Mathf.Max(0, range - 6), color, Vector2.zero);
                    helper.AddVert(ray * range, color, Vector2.zero);
                    if (i > 0) { int index = i * 2; helper.AddTriangle(index - 2, index - 1, index); helper.AddTriangle(index - 1, index + 1, index); }
                }
                return;
            }
            helper.AddVert(Vector3.zero, color, Vector2.zero);
            float halfAngle = cone ? coneAngle : Mathf.Asin(Mathf.Min(lineWidth, range) / range);
            float facing = Mathf.Atan2(direction.y, direction.x);
            int segments = cone ? 40 : 10;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            if (!cone) helper.AddVert(-normal * Mathf.Min(lineWidth, range), color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = facing + Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments);
                helper.AddVert(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * range, color, Vector2.zero);
            }
            if (!cone) helper.AddVert(normal * Mathf.Min(lineWidth, range), color, Vector2.zero);
            for (int i = 1; i < helper.currentVertCount - 1; i++) helper.AddTriangle(0, i, i + 1);
        }
    }
}
