using System;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 结算只展示本局数据并转交按钮回调，通关记录仍由游戏入口保存。
    public static class BattleResultView
    {
        public static RectTransform Create(UiFactory ui, RectTransform parent, LevelDefinition level,
            BattleSession session, Action retry, Action chooseRoster, Action next, Action returnLevels,
            VictoryRecordResult recordResult = null, FastestRecord historicalBest = null)
        {
            bool won = session.State == BattleState.Victory;
            bool hasNext = won && next != null;
            FastestRecord best = recordResult != null && recordResult.BestRecord != null ? recordResult.BestRecord : historicalBest;
            RectTransform overlay = ui.Rect(parent, "战斗结算遮罩", 0, 0, 1920, 1080);
            Image shade = AppTheme.Box(ui, overlay, "结算背景遮罩", 0, 0, 1920, 1080,
                new Color(.025f, .085f, .065f, .62f), false);
            shade.raycastTarget = true;
            Image shadow = AppTheme.Box(ui, overlay, "结算阴影", 490, 163, 960, 780, new Color(.01f, .045f, .03f, .4f));
            Image panel = AppTheme.Box(ui, overlay, "结算弹窗", 480, 150, 960, 780, BattlePalette.Silk);
            ui.Border(panel, BattlePalette.Copper, 2);
            RectTransform content = panel.rectTransform;

            Image stamp = AppTheme.Box(ui, content, "结算印章", 48, 39, 62, 66, won ? BattlePalette.Jade : BattlePalette.Danger);
            AppTheme.Label(ui, stamp.transform, won ? "捷" : "失", 0, 0, 62, 66, 36, BattlePalette.Silk, TextAnchor.MiddleCenter);
            NamedLabel(ui, content, "结算标题", won ? "守关大捷" : "此局失守", 135, 30, 690, 75,
                48, won ? BattlePalette.Jade : BattlePalette.Ink, TextAnchor.MiddleCenter);
            string result = won ? "全部波次结束，城防仍存。" : GetDefeatReason(session);
            NamedLabel(ui, content, "结算说明", level.Name + "  ·  " + result, 48, 111, 864, 47,
                25, BattlePalette.Ink, TextAnchor.MiddleCenter);
            AppTheme.Box(ui, content, "结算分隔", 48, 175, 864, 2, BattlePalette.Mist, false);

            int seconds = Math.Max(0, (int)Math.Floor(session.ElapsedSeconds));
            Stat(ui, content, "城防", session.CastleHealth + " / " + level.CastleMaxHealth,
                48, 194, session.CastleHealth > 0 ? BattlePalette.Jade : BattlePalette.Ink);
            Stat(ui, content, "用时", seconds / 60 + "分" + (seconds % 60).ToString("00") + "秒",
                344, 194, BattlePalette.Ink);
            Stat(ui, content, "击退", session.DefeatedCount + " 名敌军", 640, 194, BattlePalette.Ink);
            Stat(ui, content, "漏兵", session.LeakedCount + " 名敌军", 48, 321, BattlePalette.Ink);
            Stat(ui, content, "军饷支出", session.GoldSpent.ToString(), 344, 321, BattlePalette.Jade);
            Stat(ui, content, "波次", session.CurrentWave + " / " + session.TotalWaves,
                640, 321, BattlePalette.Ink);

            AppTheme.Box(ui, content, "结算建议底", 48, 440, 864, 142, BattlePalette.Mist);
            string adviceTitle = won ? (session.LastBreach == null ? "本局结果" : "突破记录与守关结果") : "突破记录与再战建议";
            double recordedSeconds = won && recordResult != null && recordResult.CurrentRecord != null ?
                recordResult.CurrentRecord.ElapsedMilliseconds / 1000.0 : session.ElapsedSeconds;
            string record = "本次 " + FormatRecordTime(recordedSeconds);
            if (best != null)
                record += "  ·  个人最佳 " + FormatRecordTime(best.ElapsedMilliseconds / 1000.0);
            if (won && recordResult != null && recordResult.IsFirstRecord) record += "\n首次纪录 · 已保存为最近通关";
            else if (won && recordResult != null && recordResult.IsNewRecord && recordResult.PreviousRecord != null)
                record += "\n刷新纪录 · 快了" + ((recordResult.PreviousRecord.ElapsedMilliseconds - recordResult.CurrentRecord.ElapsedMilliseconds) / 1000.0).ToString("0.###") + "秒 · 已保存为最近通关";
            else if (won && recordResult != null && recordResult.HasValidTime)
                record += "\n未刷新纪录" + (recordResult.IsEqualRecord ? "（与最佳相同）" : "") + " · 已保存为最近通关";
            else if (!won) record += "\n未刷新纪录 · 本局未通关";
            else record += "\n未刷新纪录 · " + (recordResult == null ? "未提供本次纪录结果" : "本次纪录无效");
            NamedLabel(ui, content, "结算时间纪录", record, 66, 452, 646, 112, 22, BattlePalette.Jade);

            string hint = won ? (hasNext ? "守关成功，可以继续下一关。" : "守关成功，可以重新选将再试。") :
                "调整阵位覆盖与招募时机，再来一局。";
            Text actionHint = NamedLabel(ui, content, "结算行动提示", hint, 66, 598, 828, 60,
                25, BattlePalette.Ink, TextAnchor.MiddleCenter);
            RectTransform details = ui.Rect(content, "结算详情区域", 48, 584, 864, 228);
            AppTheme.Box(ui, details, "结算详情底", 0, 0, 864, 228, BattlePalette.Mist);
            string snapshot = best == null ? "个人最佳：—" : "最佳快照：城防 " + best.CastleHp + " / " + level.CastleMaxHealth +
                " · 漏兵 " + best.LeakedCount + " · Boss突破 " + best.BossBreachCount + "\n" + best.SpeedSummary;
            NamedLabel(ui, details, "最佳纪录快照", snapshot, 18, 8, 828, 80, 22, BattlePalette.Ink, TextAnchor.UpperLeft);
            NamedLabel(ui, details, "结算建议标签", adviceTitle + " · 本局Boss突破 " + session.BossBreachCount, 18, 88, 828, 34,
                20, BattlePalette.Muted);
            NamedLabel(ui, details, "结算建议", GetAdvice(won, hasNext, level, session), 18, 126, 828, 94,
                21, BattlePalette.Ink, TextAnchor.UpperLeft);
            details.gameObject.SetActive(false);

            UiButton[] actions = {
                ResultButton(ui, content, "结算重新推演", "重新推演", 48, retry, hasNext ? BattlePalette.Mist : BattlePalette.Jade),
                ResultButton(ui, content, hasNext ? "结算下一关" : "结算重新选将",
                    hasNext ? "下一关" : "重新选将", 344, hasNext ? next : chooseRoster, hasNext ? BattlePalette.Jade : BattlePalette.Mist),
                ResultButton(ui, content, "结算返回关卡", "返回关卡", 640, returnLevels, BattlePalette.Mist)
            };
            UiButton toggle = null;
            toggle = AppTheme.Button(ui, content, "查看详情", 730, 462, 164, 88, () => {
                // 只切换同一结算的阅读布局，不调用导航、会话或记录服务。
                bool expanded = !details.gameObject.activeSelf;
                details.gameObject.SetActive(expanded);
                actionHint.gameObject.SetActive(!expanded);
                toggle.Label.text = expanded ? "收起详情" : "查看详情";
                float top = expanded ? 70 : 150;
                float height = expanded ? 940 : 780;
                content.anchoredPosition = new Vector2(480, -top);
                content.sizeDelta = new Vector2(960, height);
                shadow.rectTransform.anchoredPosition = new Vector2(490, -top - 13);
                shadow.rectTransform.sizeDelta = new Vector2(960, height);
                foreach (UiButton action in actions)
                    action.Rect.anchoredPosition = new Vector2(action.Rect.anchoredPosition.x, expanded ? -834 : -684);
            }, BattlePalette.Mist, 23);
            toggle.Rect.gameObject.name = "结算详情开关";
            toggle.Label.color = BattlePalette.Ink;
            return overlay;
        }

        public static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "—";
            long total = (long)Math.Min(long.MaxValue / 1000, Math.Floor(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        public static string FormatRecordTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "—";
            double rounded = Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
            if (double.IsInfinity(rounded) || rounded >= (double)long.MaxValue) return "—";
            long milliseconds = (long)rounded;
            return (milliseconds / 60000).ToString("00") + ":" + (milliseconds / 1000 % 60).ToString("00") + "." + (milliseconds % 1000).ToString("000");
        }

        private static string GetDefeatReason(BattleSession session)
        {
            return string.IsNullOrEmpty(session.DefeatReason) ? "城关未能守住。" : session.DefeatReason;
        }

        private static string GetAdvice(bool won, bool hasNext, LevelDefinition level, BattleSession session)
        {
            if (won && session.LastBreach == null) return hasNext ? "你的部署守住了城关。可以继续下一关，或再试一次。" :
                "这场战役已结束。可以重新选将，试试不同的守关阵容。";
            BreachRecord breach = session.LastBreach;
            if (breach == null) return "未记录到突破敌军。建议复查各路线的阵位覆盖，提前安排招募与升级。";
            string route = GetRouteName(level, breach.RouteId);
            string name = string.IsNullOrEmpty(breach.EnemyName) ? "未知敌军" : breach.EnemyName;
            string health = breach.MaxHealth > 0 ?
                Mathf.Max(0, breach.Health).ToString("0.#") + "/" + breach.MaxHealth.ToString("0.#") +
                "（" + Mathf.RoundToInt(Mathf.Clamp01(breach.Health / breach.MaxHealth) * 100) + "%）" : "未知";
            string record = "最后突破：" + name + " · " + route + " · 剩余生命 " + health;
            if (won) return record + "\n全部波次结束且城防仍存，守关成功。" + (hasNext ? "可继续下一关。" : "可重新选将再试。");
            return record + "\n建议复查" + route + "的阵位覆盖，提前招募或升级；技能自动触发。";
        }

        private static string GetRouteName(LevelDefinition level, string routeId)
        {
            for (int i = 0; i < level.Routes.Count; i++)
                if (level.Routes[i].Id == routeId) return "路线" + (i + 1);
            return "未知路线";
        }

        private static void Stat(UiFactory ui, RectTransform parent, string label, string value,
            float x, float y, Color valueColor)
        {
            Image box = AppTheme.Box(ui, parent, "统计" + label, x, y, 272, 108, BattlePalette.Mist);
            NamedLabel(ui, box.rectTransform, "统计标签", label, 18, 7, 236, 33, 21, BattlePalette.Muted);
            NamedLabel(ui, box.rectTransform, "统计数值", value, 18, 39, 236, 61, 34, valueColor);
        }

        private static Text NamedLabel(UiFactory ui, RectTransform parent, string name, string text,
            float x, float y, float width, float height, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            Text label = AppTheme.Label(ui, parent, text, x, y, width, height, size, color, align);
            label.gameObject.name = name;
            if (name == "结算标题") BattleTypography.Display(label, size);
            return label;
        }

        private static UiButton ResultButton(UiFactory ui, RectTransform parent, string name, string text,
            float x, Action callback, Color color)
        {
            UiButton button = AppTheme.Button(ui, parent, text, x, 684, 272, 72, callback, color, 29);
            button.Rect.gameObject.name = name;
            button.Button.interactable = callback != null;
            return button;
        }
    }
}
