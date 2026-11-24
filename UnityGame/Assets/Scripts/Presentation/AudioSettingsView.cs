using System;
using UnityEngine;
using UnityEngine.UI;

namespace YanyiSandpan.Presentation
{
    // 主菜单与暂停页共用设置，音量步进适合手机触控。
    public static class AudioSettingsView
    {
        public static RectTransform Create(UiFactory ui, Transform parent, Action close)
        {
            RectTransform root = ui.Rect(parent, "声音设置面板", 0, 0, 1920, 1080);
            Image shade = AppTheme.Box(ui, root, "声音设置遮罩", 0, 0, 1920, 1080,
                new Color(.02f, .09f, .07f, .62f), false);
            shade.raycastTarget = true;
            Image panel = AppTheme.Box(ui, root, "声音设置内容", 480, 220, 960, 640, BattlePalette.Silk);
            ui.Border(panel, BattlePalette.Copper, 1);
            AppTheme.Title(ui, panel.transform, "声音设置", 55, 28, 850, 70, 44, BattlePalette.Ink);
            AddRow(ui, panel.transform, "音乐", 145,
                () => GameAudio.MusicEnabled, value => GameAudio.MusicEnabled = value,
                () => GameAudio.MusicVolume, value => GameAudio.MusicVolume = value);
            AddRow(ui, panel.transform, "音效", 300,
                () => GameAudio.SoundEnabled, value => GameAudio.SoundEnabled = value,
                () => GameAudio.SoundVolume, value => GameAudio.SoundVolume = value);
            AppTheme.Label(ui, panel.transform, "音乐与音效分别设置，自动保存。", 60, 465, 840, 45, 24, BattlePalette.Muted);
            AppTheme.Button(ui, panel.transform, "完成", 60, 535, 840, 76, close, null, 30);
            return root;
        }

        private static void AddRow(UiFactory ui, Transform parent, string title, float y,
            Func<bool> enabled, Action<bool> setEnabled, Func<float> volume, Action<float> setVolume)
        {
            RectTransform row = ui.Rect(parent, title + "设置", 55, y, 850, 130);
            AppTheme.Label(ui, row, title, 0, 0, 200, 60, 34, BattlePalette.Ink);
            AppTheme.Label(ui, row, "音量", 0, 65, 170, 48, 24, BattlePalette.Muted);
            Text amount = AppTheme.Label(ui, row, "", 340, 45, 140, 65, 32, BattlePalette.Jade, TextAnchor.MiddleCenter);
            UiButton toggle = null;
            Action refresh = () =>
            {
                amount.text = Mathf.RoundToInt(volume() * 100) + "%";
                if (toggle != null)
                {
                    toggle.Label.text = enabled() ? "已开启" : "已关闭";
                    toggle.Background.color = enabled() ? BattlePalette.Jade : BattlePalette.Mist;
                    toggle.Label.color = enabled() ? BattlePalette.Silk : BattlePalette.Ink;
                }
            };
            AppTheme.Button(ui, row, "−", 220, 45, 95, 65, () => { setVolume(volume() - .1f); refresh(); }, BattlePalette.Mist, 34);
            AppTheme.Button(ui, row, "+", 505, 45, 95, 65, () => { setVolume(volume() + .1f); refresh(); }, BattlePalette.Mist, 34);
            toggle = AppTheme.Button(ui, row, "", 650, 45, 190, 65,
                () => { setEnabled(!enabled()); refresh(); }, BattlePalette.Mist, 26);
            refresh();
        }
    }
}
