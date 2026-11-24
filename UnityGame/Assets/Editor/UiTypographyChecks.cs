using System;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

public static class UiTypographyChecks
{
    public static void Verify(GameDatabase database)
    {
        var root = new GameObject("文字验证画布", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        try
        {
            var ui = new UiFactory();
            var seal = ui.Label(root.transform, "令", 0, 0, 70, 88, 45);
            Check(seal, "窄题签");
            if (seal.rectTransform.rect.height > 88)
                throw new InvalidOperationException("窄题签不应因测量样本换行而被拉高");
            Check(ui.Label(root.transform, "张辽", 0, 0, 500, 48, 34), "敌将姓名");
            Check(ui.Label(root.transform, "330", 0, 0, 130, 45, 36), "军饷数字");
            Check(ui.Label(root.transform, "本关敌将", 0, 0, 145, 28, 20), "卡片状态");
            Check(ui.Label(root.transform, "关羽", 0, 0, 145, 42, 28), "卡片姓名");
            Check(ui.Label(root.transform, "守关\n军饷", 0, 0, 350, 105, 25), "多行说明");
            foreach (var hero in database.Heroes)
            {
                Check(ui.Label(root.transform, hero.Name, 0, 0, 96, 40, 26), hero.Name + "卡片姓名", true);
                Check(ui.Label(root.transform, hero.Role, 0, 0, 96, 30, 20), hero.Name + "卡片定位", true);
                Check(ui.Label(root.transform, "军饷 " + hero.Cost, 0, 0, 96, 33, 22), hero.Name + "招募费用", true);
                Check(ui.Label(root.transform, hero.Description, 0, 0, 405, 58, 18), hero.Name + "技能说明");
                Check(ui.Label(root.transform, hero.Name, 0, 0, 112, 30, 20), hero.Name + "战场名签", true);
            }
            foreach (string value in new[] { "已出阵", "可招募", "待部署", "还差 145" })
                Check(ui.Label(root.transform, value, 0, 0, 99, 30, 19), "卡片状态" + value, true);
            foreach (string value in new[] { "1", "2", "3" })
                Check(ui.Label(root.transform, value, 0, 0, 30, 30, 20), "阶位徽标" + value, true);
            foreach (var level in database.Levels)
            {
                foreach (var wave in level.Waves)
                {
                    Check(ui.Label(root.transform, "下一波 · " + wave.Name, 0, 0, 322, 43, 29), level.Name + "波次情报", true);
                }
                Check(ui.Label(root.transform, "开战后12秒", 0, 0, 185, 43, 28), level.Name + "倒计时", true);
                Check(ui.Label(root.transform, "10000/10000", 0, 0, 185, 43, 28), level.Name + "敌将生命", true);
                Check(ui.Label(root.transform, "第一阶段 · 蓄势 1.5秒 · 即将召唤援军", 0, 0, 517, 31, 18), level.Name + "敌将预警", true);
                var session = new BattleSession(database, level, level.RecommendedRoster);
                var sample = ui.Rect(root.transform, "结算文字样本", 0, 0, 1920, 1080);
                BattleResultView.Create(ui, sample, level, session, () => { }, () => { }, null, () => { });
                foreach (Text label in sample.GetComponentsInChildren<Text>()) Check(label, "结算" + label.gameObject.name);
                UnityEngine.Object.DestroyImmediate(sample.gameObject);
            }
            Debug.Log("中文文字验证通过：25位武将卡片、完整技能说明、三阶徽标及五关结算均无裁切");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    internal static void Check(Text label, string description, bool singleLine = false)
    {
        var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
        var generator = label.cachedTextGenerator;
        generator.Populate(label.text, settings);
        int glyphCount = 0;
        // 空格、换行等空白不生成字形顶点，不能计入可见字形数量。
        foreach (char character in label.text)
            if (!char.IsWhiteSpace(character)) glyphCount++;
        if (generator.vertexCount < glyphCount * 4)
            throw new InvalidOperationException(description + "发生文字裁切：文本框高度" +
                label.rectTransform.rect.height + "，实际需要" + label.preferredHeight);
        if (label.preferredHeight > label.rectTransform.rect.height + .5f)
            throw new InvalidOperationException(description + "超出文本框高度：" + label.text);
        if (singleLine && label.preferredWidth > label.rectTransform.rect.width + .5f)
            throw new InvalidOperationException(description + "不能完整显示为单行：" + label.text);
    }
}
