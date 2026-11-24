using System;
using UnityEngine;
using UnityEngine.UI;

namespace YanyiSandpan.Presentation
{
    // 非战斗页面共用已确认的浅绢与青玉风格，不改变工厂默认值和战斗布局。
    public static class AppTheme
    {
        public static Image Box(UiFactory ui, Transform parent, string name, float x, float y,
            float width, float height, Color color, bool round = false)
        {
            return ui.Box(parent, name, x, y, width, height, color, false);
        }

        public static Text Label(UiFactory ui, Transform parent, string value, float x, float y,
            float width, float height, int size = 26, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            return ui.Label(parent, value, x, y, width, height, size, color ?? BattlePalette.Ink, align);
        }

        public static Text Title(UiFactory ui, Transform parent, string value, float x, float y,
            float width, float height, int size, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            Text title = Label(ui, parent, value, x, y, width, height, size, color, align);
            BattleTypography.Display(title, size);
            return title;
        }

        public static UiButton Button(UiFactory ui, Transform parent, string value, float x, float y,
            float width, float height, Action action, Color? color = null, int fontSize = 28, bool playSound = true)
        {
            Color background = color ?? BattlePalette.Jade;
            UiButton button = ui.Button(parent, value, x, y, width, height, action, background, fontSize, playSound);
            BattleTypography.Flat(button.Background);
            ui.Border(button.Background, BattlePalette.Line, 1);
            button.Label.color = background == BattlePalette.Jade || background == BattlePalette.Danger ?
                BattlePalette.Silk : BattlePalette.Ink;
            return button;
        }
    }
}
