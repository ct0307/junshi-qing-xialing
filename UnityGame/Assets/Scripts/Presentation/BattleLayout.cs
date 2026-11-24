using UnityEngine;
using UnityEngine.UI;

namespace YanyiSandpan.Presentation
{
    // 仅压缩地面的纵深，规则坐标不变；人物子层抵消缩放，保留棋兵比例。
    public static class BattleLayout
    {
        public const float GroundScale = .67f;
        public const float GroundOffset = 218;
        public const float HeaderBottom = 176;
        public const float DockTop = 832;
        public const float HeroSize = 170;

        public static float ScreenY(float worldY) { return GroundOffset + worldY * GroundScale; }

        public static RectTransform CreateGround(UiFactory ui, RectTransform parent, string mapResource)
        {
            Image frame = ui.Box(parent, "独立沙盘边框", 24, 176, 1872, 648, BattlePalette.Silk, false);
            ui.Border(frame, new Color(BattlePalette.Copper.r, BattlePalette.Copper.g, BattlePalette.Copper.b, .6f), 1.5f);
            ui.Box(parent, "沙盘内侧", 32, 184, 1856, 640, UiFactory.Hex("94A47B"));
            RectTransform viewport = ui.Rect(parent, "沙盘视口", 32, 184, 1856, 640);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform ground = ui.Rect(viewport, "战场坐标层", -32, GroundOffset - 184, 1920, 1080);
            ground.localScale = new Vector3(1, GroundScale, 1);
            Sprite map = ui.LoadSprite(mapResource);
            if (map != null)
            {
                // 原图内侧为地形，道路与建筑仍与原始坐标一致；独立边框为上排角色留白。
                RawImage image = ui.Rect(ground, "地图内侧", 38, 144, 1844, 698).gameObject.AddComponent<RawImage>();
                image.texture = map.texture;
                image.uvRect = new Rect(38f / 1920, 238f / 1080, 1844f / 1920, 698f / 1080);
                image.raycastTarget = false;
                image.gameObject.AddComponent<GroundEdgeFade>();
            }
            return ground;
        }
    }

    // 上下边缘使用短距离过渡，避免裁剪后的地图与沙盘留白出现硬接缝。
    public sealed class GroundEdgeFade : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            Rect rect = graphic.rectTransform.rect;
            helper.Clear();
            float[] rows = { 0, .1f, .96f, 1 };
            byte[] alpha = { 0, 255, 255, 0 };
            var image = (RawImage)graphic;
            Rect uv = image.uvRect;
            for (int i = 0; i < rows.Length; i++)
            {
                float y = rect.yMax - rows[i] * rect.height;
                float v = uv.yMax - rows[i] * uv.height;
                Color32 color = graphic.color;
                color.a = alpha[i];
                helper.AddVert(new Vector3(rect.xMin, y), color, new Vector2(uv.xMin, v));
                helper.AddVert(new Vector3(rect.xMax, y), color, new Vector2(uv.xMax, v));
                if (i == 0) continue;
                int start = (i - 1) * 2;
                helper.AddTriangle(start, start + 1, start + 2);
                helper.AddTriangle(start + 1, start + 3, start + 2);
            }
        }
    }
}
