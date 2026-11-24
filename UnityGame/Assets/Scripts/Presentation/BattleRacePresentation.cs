using System;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class BattleView
    {
        // 模态由流程统一管理，刷新只同步真实会话，不再切换倍率。
        public void RefreshRaceHud() { RefreshHud(); }
        public bool HasDetailsOpen { get { return detailOverlay != null; } }
        public void ShowMarchSpeedNotice()
        { SetNotice("进军 " + Session.Speed.ToString("0") + "× · 来敌与军饷加快，我方攻速不变。"); }
        public void ShowMarchSpeedSaveFailure()
        { SetNotice("倍率说明未能保存，本局选择仍有效；下次可重新确认。", true); }

        private void ChangeMarchSpeed(int targetSpeed)
        {
            if (game != null) { game.ChangeMarchSpeed(targetSpeed); return; }
            if (detailOverlay != null || targetSpeed < 1 || targetSpeed > 3 || Session.Speed == targetSpeed ||
                (Session.State != BattleState.Preparation && Session.State != BattleState.Running && Session.State != BattleState.Paused)) return;
            Session.SetSpeed(targetSpeed); ShowMarchSpeedNotice(); RefreshHud();
        }
    }

    // 顶栏与暂停菜单共用同一组目标控件，显示只读取会话值。
    internal sealed class MarchSpeedControls
    {
        private readonly UiFactory ui;
        private readonly UiButton[] buttons = new UiButton[3];
        private readonly GameObject[] markers = new GameObject[3];
        public MarchSpeedControls(UiFactory ui, Transform parent, float x, float y, string prefix, Action<int> select)
        {
            this.ui = ui;
            for (int index = 0; index < 3; index++)
            {
                int targetSpeed = index + 1;
                UiButton button = ui.Button(parent, targetSpeed + "×", x + index * 76, y, 72, 80,
                    () => select(targetSpeed), BattlePalette.Mist, 26, false);
                button.Rect.name = prefix + targetSpeed;
                BattleTypography.Flat(button.Background);
                buttons[index] = button;
                markers[index] = ui.Box(button.Rect, "倍率选中标记", 26, 67, 20, 4, BattlePalette.Silk, false).gameObject;
            }
        }
        public void Refresh(int speed, bool interactable)
        {
            for (int index = 0; index < buttons.Length; index++)
            {
                UiButton button = buttons[index]; if (button.Rect == null) continue;
                bool selected = speed == index + 1;
                button.Background.color = selected ? BattlePalette.Jade : BattlePalette.Mist;
                button.Label.color = selected ? BattlePalette.Silk : BattlePalette.Ink;
                button.Button.interactable = interactable;
                ui.Border(button.Background, selected ? BattlePalette.Ink : BattlePalette.Line, selected ? 1.5f : 1);
                markers[index].SetActive(selected);
            }
        }
    }
}
