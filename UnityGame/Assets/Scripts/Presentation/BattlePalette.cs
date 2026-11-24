using UnityEngine;

namespace YanyiSandpan.Presentation
{
    // 战斗页的青绿行军色板，避免改变菜单和结算的既有风格。
    public static class BattlePalette
    {
        public static readonly Color Ink = UiFactory.Hex("183E37");
        public static readonly Color Jade = UiFactory.Hex("3A7861");
        public static readonly Color Leaf = UiFactory.Hex("8BA779");
        public static readonly Color Silk = UiFactory.Hex("F4F0DD");
        public static readonly Color Copper = UiFactory.Hex("B28B52");
        public static readonly Color Mist = UiFactory.Hex("E0EBDE");
        public static readonly Color Muted = UiFactory.Hex("607467");
        public static readonly Color Line = UiFactory.Hex("C8D0B6");
        public static readonly Color Danger = UiFactory.Hex("9C4939");
    }
}
