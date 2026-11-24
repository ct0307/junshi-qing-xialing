using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 只读取终局快照；保存及终局去重由GameFlow负责。
    public static class RaceResultView
    {
        private sealed class SummaryLine
        {
            public double Time;
            public int Order;
            public string Text;
        }

        public static RectTransform Create(UiFactory ui, RectTransform parent, GameDatabase database,
            LevelDefinition level, BattleSession session, Action retry, Action adjust, Action next,
            Action levels, RaceRecordResult result, FastestRecord historical, string ordinaryRecordIssue = "")
        {
            RaceSnapshot snapshot = result.Snapshot;
            bool won = session.State == BattleState.Victory;
            RectTransform overlay = ui.Rect(parent, "竞速结算遮罩", 0, 0, 1920, 1080);
            Image shade = AppTheme.Box(ui, overlay, "结算背景遮罩", 0, 0, 1920, 1080, new Color(.025f, .085f, .065f, .72f), false);
            shade.raycastTarget = true;
            RectTransform panel = AppTheme.Box(ui, overlay, "竞速结算纸面", 340, 80, 1240, 920, BattlePalette.Silk).rectTransform;
            AppTheme.Box(ui, panel, "结算令印", 40, 36, 70, 75, won ? BattlePalette.Jade : BattlePalette.Danger);
            AppTheme.Title(ui, panel, won ? "捷" : "失", 40, 36, 70, 75, 38, BattlePalette.Silk, TextAnchor.MiddleCenter);
            AppTheme.Title(ui, panel, won ? "守关成功" : "此局失守", 140, 30, 790, 75, 46, BattlePalette.Jade);
            AppTheme.Label(ui, panel, level.Name + " · " + (won ? "全部波次完成，城防仍存。" : (session.DefeatReason ?? "城防归零。")), 42, 118, 1156, 52, 26);
            AppTheme.Box(ui, panel, "分隔", 40, 185, 1160, 2, BattlePalette.Line, false);

            RectTransform main = ui.Rect(panel, "竞速结果摘要", 40, 204, 1160, 590);
            Stat(ui, main, "本次用时", BattleResultView.FormatRecordTime(snapshot.ElapsedSeconds), 0);
            Stat(ui, main, "此前竞速最快", result.PreviousRecord == null ? "—" : BattleResultView.FormatRecordTime(result.PreviousRecord.ElapsedMilliseconds / 1000.0), 394);
            Stat(ui, main, "快慢差值", Difference(result), 788);
            AppTheme.Box(ui, main, "资格底", 0, 135, 1160, 140, BattlePalette.Mist);
            AppTheme.Label(ui, main, Feedback(won, snapshot, result), 24, 148, 1112, 112, 29, snapshot.IsEligible ? BattlePalette.Jade : BattlePalette.Ink);
            AppTheme.Label(ui, main, "城防 " + session.CastleHealth + " / " + level.CastleMaxHealth + "    漏兵 " + snapshot.LeakedCount +
                "    敌将击退 " + snapshot.DefeatedBossCount + " / " + snapshot.ExpectedBossCount + "    敌将突破 " + snapshot.BossBreachCount,
                0, 300, 1160, 68, 28);
            AppTheme.Label(ui, main, "守关成功，且全部敌将击退；普通漏兵允许。\n1×、2×、3×动态切换同榜，只有更快的合格用时才刷新。", 0, 387, 1160, 108, 25, BattlePalette.Muted);
            string old = historical == null ? "—" : BattleResultView.FormatRecordTime(historical.ElapsedMilliseconds / 1000.0) + " · " + historical.SpeedSummary;
            AppTheme.Label(ui, main, (string.IsNullOrEmpty(ordinaryRecordIssue) ? "普通通关历史参考 " + old : ordinaryRecordIssue) + "\n" + (next == null && won ? "本战役已到末关；可调整阵容挑战自己的竞速最快。" : "调整阵容沿用本局名单；新局从初始军饷重新招募。"),
                0, 505, 1160, 82, 23, BattlePalette.Muted);

            RectTransform details = ui.Rect(panel, "竞速复盘详情", 40, 204, 1160, 590);
            AppTheme.Label(ui, details, "倍率时长（有效运行实秒）  1× " + Seconds(snapshot.GetSpeedSeconds(1)) +
                "    2× " + Seconds(snapshot.GetSpeedSeconds(2)) + "    3× " + Seconds(snapshot.GetSpeedSeconds(3)), 0, 0, 1160, 57, 26, BattlePalette.Jade);
            AppTheme.Label(ui, details, FirstBreach(level, session.FirstBreach), 0, 72, 1160, 100, 24);
            var lines = BuildSummary(database, session, snapshot);
            var rows = new List<Text>();
            AppTheme.Label(ui, details, "真实部署、升级与有效倍率切换" + (snapshot.SummaryTruncated ? " · 轻量摘要已达上限，后续操作未保留" : ""), 0, 184, 1160, 48, 25, BattlePalette.Jade);
            for (int i = 0; i < 6; i++) rows.Add(AppTheme.Label(ui, details, "", 0, 244 + i * 43, 1160, 42, 23));
            int page = 0;
            Text pageText = AppTheme.Label(ui, details, "", 410, 518, 340, 66, 24, BattlePalette.Muted, TextAnchor.MiddleCenter);
            UiButton previous = null, forward = null;
            Action refresh = () => {
                int count = Math.Max(1, (lines.Count + 5) / 6);
                for (int i = 0; i < rows.Count; i++) rows[i].text = page * 6 + i < lines.Count ? lines[page * 6 + i].Text : "";
                pageText.text = "摘要 " + (page + 1) + " / " + count;
                if (previous != null) previous.Button.interactable = page > 0;
                if (forward != null) forward.Button.interactable = page + 1 < count;
            };
            previous = AppTheme.Button(ui, details, "上一页", 0, 518, 260, 66, () => { page--; refresh(); }, BattlePalette.Mist, 25);
            previous.Rect.name = "竞速摘要上一页";
            forward = AppTheme.Button(ui, details, "下一页", 900, 518, 260, 66, () => { page++; refresh(); }, BattlePalette.Mist, 25);
            forward.Rect.name = "竞速摘要下一页";
            refresh(); details.gameObject.SetActive(false);

            UiButton toggle = null;
            toggle = AppTheme.Button(ui, panel, "查看详情", 970, 28, 230, 76, () => {
                bool expanded = !details.gameObject.activeSelf;
                details.gameObject.SetActive(expanded); main.gameObject.SetActive(!expanded);
                toggle.Label.text = expanded ? "收起详情" : "查看详情";
            }, BattlePalette.Mist, 26);
            toggle.Rect.name = "竞速详情开关";
            ResultButton(ui, panel, "重新推演", 40, retry, BattlePalette.Jade);
            ResultButton(ui, panel, "调整阵容", 338, adjust, BattlePalette.Mist);
            ResultButton(ui, panel, !won ? "下一关需先通关" : next == null ? "战役已结束" : "下一关", 636, next, BattlePalette.Mist);
            ResultButton(ui, panel, "返回关卡", 934, levels, BattlePalette.Mist);
            return overlay;
        }

        private static void ResultButton(UiFactory ui, RectTransform panel, string text, float x, Action callback, Color color)
        {
            UiButton button = AppTheme.Button(ui, panel, text, x, 826, 266, 70, callback, color, text.Length > 6 ? 24 : 28);
            button.Button.interactable = callback != null;
        }
        private static void Stat(UiFactory ui, RectTransform parent, string title, string value, float x)
        {
            AppTheme.Box(ui, parent, title, x, 0, 372, 112, BattlePalette.Mist);
            AppTheme.Label(ui, parent, title, x + 18, 4, 336, 40, 22, BattlePalette.Muted);
            AppTheme.Label(ui, parent, value, x + 18, 44, 336, 64, 35, BattlePalette.Ink);
        }
        private static string Difference(RaceRecordResult result)
        {
            if (!result.HasValidTime || result.PreviousRecord == null) return "—";
            long difference = result.CurrentRecord.ElapsedMilliseconds - result.PreviousRecord.ElapsedMilliseconds;
            return difference == 0 ? "相同用时" : (difference < 0 ? "快 " : "慢 ") + (Math.Abs(difference) / 1000.0).ToString("0.000") + " 秒";
        }
        private static string Feedback(bool won, RaceSnapshot snapshot, RaceRecordResult result)
        {
            if (!snapshot.IsEligible) return (won ? "守关成功；未满足竞速资格" : "未满足竞速资格") + "\n" + snapshot.IneligibilityReason;
            if (!string.IsNullOrEmpty(result.IneligibilityReason)) return "本局满足竞速资格，但竞速纪录未保存\n" + result.IneligibilityReason;
            if (result.IsFirstRecord) return "首次竞速纪录\n已保存本关首个全部敌将击退的合格成绩。";
            if (result.IsNewRecord) return "刷新竞速纪录 · " + Difference(result) + "\n已保存新的竞速最快。";
            if (result.IsEqualRecord) return "与竞速最快相同 · 未刷新\n保留此前纪录与策略摘要。";
            return "慢于竞速最快 · " + Difference(result) + "\n本局已完成合格通关，可调整阵容再试。";
        }
        private static string Seconds(double value) { return value.ToString("0.00") + "秒"; }
        private static string FirstBreach(LevelDefinition level, BreachRecord breach)
        {
            if (breach == null) return "首次突破：本局没有敌军突破记录。";
            string route = "未知路线";
            for (int i = 0; i < level.Routes.Count; i++) if (level.Routes[i].Id == breach.RouteId) route = "路线" + (i + 1);
            return "首次突破：" + BattleResultView.FormatRecordTime(breach.ElapsedSeconds) + " · 第" + breach.Wave + "波 · " + route + " · " + breach.EnemyName +
                (breach.IsBoss ? "（敌将）" : "") + "\n剩余生命 " + breach.Health.ToString("0.#") + "/" + breach.MaxHealth.ToString("0.#") + " · 当时军饷 " + breach.Gold + " · " + breach.Speed.ToString("0") + "×";
        }
        private static List<SummaryLine> BuildSummary(GameDatabase database, BattleSession session, RaceSnapshot snapshot)
        {
            var lines = new List<SummaryLine>();
            foreach (RaceAction item in snapshot.Actions)
            {
                HeroDefinition hero = database.FindHero(item.HeroId);
                string kind = item.Kind == "recruit" ? "招募" : item.Kind == "upgrade" ? "升阶" : item.Kind == "move" ? "调位" : item.Kind;
                lines.Add(new SummaryLine { Time = item.ElapsedSeconds, Order = lines.Count, Text = BattleResultView.FormatRecordTime(item.ElapsedSeconds) + "  " + kind + " " + (hero == null ? item.HeroId : hero.Name) +
                    " · 阵位" + item.SlotId + " · " + item.Level + "阶 · 余饷" + item.Gold });
            }
            foreach (RaceSpeedChange item in snapshot.SpeedChanges)
                lines.Add(new SummaryLine { Time = item.ElapsedSeconds, Order = lines.Count, Text = BattleResultView.FormatRecordTime(item.ElapsedSeconds) + "  有效进军 " + item.Speed + "×" });
            // 同时刻的备战操作保持采集顺序，不由不稳定排序重排招募先后。
            lines.Sort((a, b) => { int time = a.Time.CompareTo(b.Time); return time == 0 ? a.Order.CompareTo(b.Order) : time; });
            if (lines.Count == 0) lines.Add(new SummaryLine { Time = 0, Text = "本局未记录部署、升级或有效倍率切换。" });
            return lines;
        }
    }
}
