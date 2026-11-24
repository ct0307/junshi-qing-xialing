using UnityEngine;
using UnityEngine.UI;

namespace YanyiSandpan.Presentation
{
    // 战斗页用宋体承载标题和姓名，操作说明继续使用现有黑体。
    public static class BattleTypography
    {
        private static Font displayFont;

        public static Font DisplayFont
        {
            get
            {
                if (displayFont == null) displayFont = Resources.Load<Font>("Game/Fonts/ChineseDisplay");
                return displayFont;
            }
        }

        public static void Display(Text label, int size)
        {
            if (DisplayFont != null) label.font = DisplayFont;
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            // 更换字体后重新按字形行高留白，不能沿用黑体的行框估计。
            float width = label.rectTransform.rect.width;
            var settings = label.GetGenerationSettings(new Vector2(width, 0));
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float height = label.cachedTextGeneratorForLayout.GetPreferredHeight("守关", settings) / label.pixelsPerUnit;
            label.rectTransform.sizeDelta = new Vector2(width, Mathf.Max(label.rectTransform.rect.height, Mathf.Ceil(height) + 2));
        }

        public static void Flat(Image surface)
        {
            surface.sprite = null;
            surface.type = Image.Type.Simple;
        }
    }
}
