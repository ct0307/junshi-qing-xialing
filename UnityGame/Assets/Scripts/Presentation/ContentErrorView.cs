using System;
using UnityEngine;

namespace YanyiSandpan.Presentation
{
    public static class ContentErrorView
    {
        public static RectTransform Create(UiFactory ui, RectTransform parent, string message, Action retry)
        {
            RectTransform root = ui.Rect(parent, "内容错误页面", 0, 0, 1920, 1080);
            AppTheme.Box(ui, root, "错误背景", 0, 0, 1920, 1080, BattlePalette.Silk, false);
            AppTheme.Title(ui, root, "内容加载失败", 150, 180, 1600, 100, 56, BattlePalette.Danger);
            AppTheme.Label(ui, root, "请重试加载。若问题持续，请重新安装完整游戏包。", 150, 310, 1600, 80, 30);
            AppTheme.Label(ui, root, message, 150, 420, 1600, 350, 26, BattlePalette.Muted);
            AppTheme.Button(ui, root, "重试加载", 150, 840, 420, 90, retry, null, 32);
            return root;
        }
    }
}
