using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 在编辑器中检查真实导航与会话；通关记录仅保存在本次检查的内存字典中。
public static class UiGameFlowChecks
{
    private static string auxiliaryOutput;

    // 本轮专项把名称与玩法附图也收在同一根目录，旧入口保持原来的输出位置。
    public static void VerifyIsolated(GameDatabase database, string output)
    {
        string previous = auxiliaryOutput;
        try
        {
            auxiliaryOutput = Path.GetFullPath(output);
            Verify(database, output);
        }
        finally { auxiliaryOutput = previous; }
    }

    public static void Verify(GameDatabase database, string output)
    {
        Directory.CreateDirectory(output);
        VerifyProgress();
        LevelDefinition level = database.FindLevel("huangjin");
        Require(level != null, "完整流程检查缺少黄巾关卡");
        VerifyWarningPriority(database);
        VerifyGuideStages(database, level);
        VerifyBattleTimingDetails(database, level);
        using (var scene = new FlowScene(database))
        {
            VerifyMenus(scene, database, level, output);
            VerifyPreparation(scene, database, level, output);
            VerifyPauseAndDefeat(scene, level, output);
            BattleSession victory = VerifyVictory(scene, database, level, output);
            VerifyLastResult(scene, database.Levels[database.Levels.Count - 1], victory);
            VerifyErrorPage(scene, output);
        }
        Debug.Log("完整导航验证通过：菜单、25人选将、备战退出、平台暂停入口、重开、真实胜败、下一关、隔离存档去重与错误页；9张导航截图");
    }

    private static void VerifyProgress()
    {
        var values = new Dictionary<string, int>();
        int saves = 0;
        Func<GameProgress> reopen = () => new GameProgress(
            (key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
            (key, value) => values[key] = value, () => saves++);
        GameProgress progress = reopen();
        Require(!progress.IsCleared("sample") && progress.GetBestCastle("sample") == 0, "空记录不应已通关");
        progress.RecordVictory("sample", 18);
        Require(saves == 1 && progress.IsCleared("sample") && progress.GetBestCastle("sample") == 18, "首次胜利未保存正确记录");
        progress.RecordVictory("sample", 18);
        progress.RecordVictory("sample", 12);
        Require(saves == 1 && progress.GetBestCastle("sample") == 18, "重复或较低城防覆盖最佳记录");
        progress.RecordVictory("sample", 28);
        progress = reopen();
        Require(saves == 2 && progress.IsCleared("sample") && progress.GetBestCastle("sample") == 28, "重建记录服务后丢失最佳成绩");
        progress.RecordVictory("other", 10);
        Require(saves == 3 && progress.GetBestCastle("sample") == 28 && progress.GetBestCastle("other") == 10, "不同关卡存档相互覆盖");
    }

    private static void VerifyMenus(FlowScene scene, GameDatabase database, LevelDefinition level, string output)
    {
        scene.Flow.ShowMainMenu();
        CheckPage(scene, "Main");
        Capture(scene, output, "flow-main");
        string nameOutput = auxiliaryOutput == null ? Path.GetFullPath("../docs/ui/runtime-name") : Path.Combine(auxiliaryOutput, "name");
        Directory.CreateDirectory(nameOutput);
        Capture(scene, nameOutput, "main-1920");
        Capture(scene, nameOutput, "main-1280", 1280, 720);
        Capture(scene, nameOutput, "main-2400", 2400, 1080);
        Click(scene.Root, "开始推演");
        CheckPage(scene, "Levels");
        Require(database.Levels.All(item => LevelButton(scene.Root, item) != null), "关卡菜单缺少真实关卡");
        LevelDefinition firstBattle = database.FindLevel("huangjin");
        Button firstBattleButton = LevelButton(scene.Root, firstBattle);
        float firstBattleX = ((RectTransform)firstBattleButton.transform).anchoredPosition.x;
        Require(database.Levels.All(item => ((RectTransform)LevelButton(scene.Root, item).transform).anchoredPosition.x >= firstBattleX),
            "黄巾应作为推荐首战显示在第一张关卡卡片");
        Require(firstBattleButton.GetComponentsInChildren<Text>().Any(text => text.text.Contains("推荐首战")), "黄巾缺少推荐首战标记");
        CaptureGameplay(scene, "levels-1280", 1280, 720);
        Capture(scene, output, "flow-levels");
        scene.Flow.HandleBack();
        CheckPage(scene, "Main");
        Click(scene.Root, "开始推演");
        Click(LevelButton(scene.Root, level));
        CheckPage(scene, "Intel");
        Require(scene.Flow.CurrentLevel == level, "关卡情报未使用所选关卡");
        foreach (string id in level.BossHeroIds)
            Require(HasText(scene.Root, database.FindHero(id).Name), "情报缺少本关敌将：" + id);
        Capture(scene, output, "flow-intel");
        scene.Flow.HandleBack();
        CheckPage(scene, "Levels");
        OpenRoster(scene, level);
        scene.Flow.HandleBack();
        CheckPage(scene, "Intel");
        Click(scene.Root, "前往选将");

        Require(database.Heroes.Count == 25, "战前选将应提供25名武将");
        foreach (HeroDefinition hero in database.Heroes) HeroCard(scene.Root, hero.Name);
        Click(HeroCard(scene.Root, "张飞"));
        Require(scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains(BattleSession.GetAttackTimingDescription(database.FindHero("zhang_fei"), 1))),
            "张飞详情必须显示实际配置的普攻与特色技能节奏");
        Click(HeroCard(scene.Root, "张飞"));
        CheckSelected(scene, 0);
        foreach (string name in new[] { "张飞", "吕布", "荀彧", "吕蒙" })
        {
            Click(HeroCard(scene.Root, name));
            Require(scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("基础威力")), "武将面板未区分基础威力与实际输出");
            CaptureGameplay(scene, "hero-" + database.Heroes.First(hero => hero.Name == name).Id);
            Click(HeroCard(scene.Root, name));
        }
        CheckSelected(scene, 0);
        Require(!FindButton(scene.Root, "进入沙盘").interactable, "空阵容仍可进入沙盘");
        foreach (string id in level.BossHeroIds)
        {
            ChooseHero(HeroCard(scene.Root, database.FindHero(id).Name));
            CheckSelected(scene, 0);
        }
        Click(scene.Root, "推荐阵容");
        CheckSelected(scene, level.RecommendedRoster.Count);
        foreach (string id in level.RecommendedRoster)
            Require(IsSelected(HeroCard(scene.Root, database.FindHero(id).Name)), "推荐阵容漏选：" + id);
        Click(scene.Root, "清空阵容");
        CheckSelected(scene, 0);

        List<HeroDefinition> available = database.Heroes.Where(hero => !level.BossHeroIds.Contains(hero.Id)).ToList();
        Require(available.Count > 10, "上限检查至少需要11名可选武将");
        for (int i = 0; i < 10; i++) ChooseHero(HeroCard(scene.Root, available[i].Name));
        CheckSelected(scene, 10);
        ChooseHero(HeroCard(scene.Root, available[10].Name));
        CheckSelected(scene, 10);
        Require(!IsSelected(HeroCard(scene.Root, available[10].Name)), "第11名武将突破阵容上限");
        foreach (string id in level.BossHeroIds)
        {
            Button card = HeroCard(scene.Root, database.FindHero(id).Name);
            ChooseHero(card);
            Require(!IsSelected(card), "本关敌将进入了己方阵容：" + id);
            CheckSelected(scene, 10);
        }
        ChooseHero(HeroCard(scene.Root, available[0].Name));
        CheckSelected(scene, 9);
        ChooseHero(HeroCard(scene.Root, available[10].Name));
        CheckSelected(scene, 10);
        Click(scene.Root, "清空阵容");
        CheckSelected(scene, 0);
        Require(!FindButton(scene.Root, "进入沙盘").interactable, "清空后仍可进入沙盘");
        Click(scene.Root, "推荐阵容");
        Capture(scene, output, "flow-roster");
        Click(scene.Root, "进入沙盘");
        CheckFreshBattle(scene, level);
        foreach (HeroDefinition hero in database.Heroes)
            Require(scene.Flow.CurrentSession.IsInRoster(hero.Id) == level.RecommendedRoster.Contains(hero.Id), "实际会话阵容与战前选择不符：" + hero.Id);
    }

    private static void VerifyPreparation(FlowScene scene, GameDatabase database, LevelDefinition level, string output)
    {
        BattleSession session = scene.Flow.CurrentSession;
        HeroDefinition hero = database.FindHero(level.RecommendedRoster[0]);
        SelectHero(scene.Root, hero.Name);
        ClickSlot(scene.Root, level, 10);
        Require(session.IsRecruited(hero.Id) && session.Heroes.Count == 1, "备战检查未通过真实按钮部署武将");
        HeroUnit original = session.Heroes[0];
        RectTransform units = FindNamed(scene.Root, "棋兵");
        Transform originalView = units.GetChild(0);
        int beforeMoveGold = session.Gold, beforeMoveSpent = session.GoldSpent;
        int destination = level.Slots.First(slot => slot.Id != 10).Id;
        ClickSlot(scene.Root, level, destination);
        Require(object.ReferenceEquals(original, session.Heroes[0]) && original.SlotId == destination,
            "开战前点击空阵位未移动同一个守将");
        Require(session.Gold == beforeMoveGold && session.GoldSpent == beforeMoveSpent && session.IsRecruited(hero.Id),
            "备战调位改变了消费或招募唯一性");
        Require(units.childCount == 1 && units.GetChild(0) == originalView &&
            FindNamed(scene.Root, "战场名签").childCount == 1, "备战调位复制了棋兵或名签");
        CaptureGameplay(scene, "move-1920");
        CaptureGameplay(scene, "move-1280", 1280, 720);
        CaptureGameplay(scene, "move-2400", 2400, 1080);
        int rankBefore = original.Level;
        Click(scene.Root, "升级 ");
        Require(original.SlotId == destination && original.Level == rankBefore + 1, "调位后的升级按钮未指向同一武将的新阵位");
        ClickSlot(scene.Root, level, 10);
        Require(original.SlotId == 10 && session.Heroes.Count == 1, "备战移回原阵位失败或产生重复守将");
        int gold = session.Gold;
        scene.Flow.PauseForSystem();
        Require(session.State == BattleState.Preparation && !HasText(scene.Root, "推演暂停"), "备战失焦错误地启动或暂停了会话");
        scene.Flow.HandleBack();
        Require(HasText(scene.Root, "离开布阵"), "备战返回未显示离开确认");
        Capture(scene, output, "flow-preparation-exit");
        Click(scene.Root, "继续布阵");
        Require(scene.Flow.CurrentSession == session && session.State == BattleState.Preparation && session.Heroes.Count == 1 && session.Gold == gold && session.Time == 0,
            "继续布阵丢失会话、部署或军饷");
        Require(!HasText(scene.Root, "离开布阵"), "继续布阵后确认框未关闭");
        scene.Flow.HandleBack();
        Click(scene.Root, "重新选将");
        CheckPage(scene, "Roster");
        Require(scene.Flow.CurrentSession == null, "重新选将后仍持有上一局会话");
        EnterRecommended(scene, level);
        scene.Flow.HandleBack();
        Click(scene.Root, "返回关卡");
        CheckPage(scene, "Levels");
        Require(scene.Flow.CurrentSession == null, "退出备战后仍持有上一局会话");
        OpenRoster(scene, level);
        EnterRecommended(scene, level);
        Require(scene.SaveCount == 0 && scene.Values.Count == 0, "菜单或备战流程修改了通关记录");
    }

    private static void VerifyPauseAndDefeat(FlowScene scene, LevelDefinition level, string output)
    {
        Click(scene.Root, "开战");
        scene.Flow.Tick(.5f);
        BattleSession session = scene.Flow.CurrentSession;
        Require(session.State == BattleState.Running, "空部署未能正常开战");
        VerifyPlatformPause(scene);
        Require(session.State == BattleState.Paused, "系统失焦未暂停真实会话");
        Require(scene.Root.GetComponentsInChildren<Text>().Count(text => text.text == "推演暂停") == 1, "重复系统暂停创建了多个暂停框");
        Capture(scene, output, "flow-pause");
        float time = session.Time, countdown = session.NextWaveCountdown;
        int gold = session.Gold, health = session.CastleHealth, wave = session.CurrentWave, enemies = session.RemainingEnemies;
        for (int i = 0; i < 20; i++) scene.Flow.Tick(.25f);
        Require(session.Time == time && session.Gold == gold && session.CastleHealth == health && session.CurrentWave == wave &&
            session.RemainingEnemies == enemies && session.NextWaveCountdown == countdown, "暂停期间战斗、收入或波次继续推进");
        scene.Flow.HandleBack();
        Require(session.State == BattleState.Running && !HasText(scene.Root, "推演暂停"), "返回键未关闭暂停并恢复原会话");
        Click(scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "战场主操作"));
        Click(scene.Root, "重新推演");
        CheckFreshBattle(scene, level);
        Require(scene.Flow.CurrentSession != session, "暂停重开复用了上一局会话");
        Require(!HasText(scene.Root, "推演暂停"), "重开后残留暂停框");
        Click(scene.Root, "开战");
        session = scene.Flow.CurrentSession;
        for (int step = 0; step < 3200 && session.State == BattleState.Running; step++) scene.Flow.Tick(.25f);
        Require(session.State == BattleState.Defeat && session.Heroes.Count == 0 && session.GoldSpent == 0 && session.LeakedCount > 0,
            "真实空部署未自然产生预期失败");
        Require(HasText(scene.Root, "此局失守") && scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains(session.DefeatReason)),
            "真实失败未自动进入包含失败原因的结算");
        CheckResultStats(scene.Root, level, session);
        BreachRecord breach = session.LastBreach;
        Require(breach != null && breach.Health > 0 && breach.MaxHealth >= breach.Health, "自然失败缺少真实突破快照");
        session.DrainEvents();
        Require(object.ReferenceEquals(breach, session.LastBreach), "消费事件清除了复盘记录");
        scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "结算详情开关").onClick.Invoke();
        Require(scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("最后突破") &&
            text.text.Contains(breach.EnemyName) && text.text.Contains("路线1") && text.text.Contains("剩余生命")),
            "失败复盘未显示真实敌军、路线和剩余生命");
        CaptureGameplay(scene, "defeat-1920");
        CaptureGameplay(scene, "defeat-1280", 1280, 720);
        CaptureGameplay(scene, "defeat-2400", 2400, 1080);
        Capture(scene, output, "flow-defeat");
        Require(scene.SaveCount == 0 && scene.Values.Count == 0, "失败错误地写入了通关记录");
        Click(scene.Root, "重新推演");
        CheckFreshBattle(scene, level);
        Require(scene.Flow.CurrentSession != session && !HasText(scene.Root, "此局失守"), "失败重开未替换会话或清理结算");
        scene.Flow.HandleBack();
        Click(scene.Root, "重新选将");
        CheckPage(scene, "Roster");
        EnterRecommended(scene, level);
    }

    private static BattleSession VerifyVictory(FlowScene scene, GameDatabase database, LevelDefinition level, string output)
    {
        BattleSession session = scene.Flow.CurrentSession;
        int[] slots = { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 };
        string[] roster = level.RecommendedRoster.ToArray();
        Require(roster.Length == slots.Length, "黄巾参考阵容与参考阵位数量不符");
        int spent = 0;
        for (int step = 0; step < 3200 && (session.State == BattleState.Preparation || session.State == BattleState.Running); step++)
        {
            for (int i = 0; i < roster.Length; i++)
            {
                HeroDefinition hero = database.FindHero(roster[i]);
                if (session.IsRecruited(hero.Id) || session.Gold < hero.Cost) continue;
                int before = session.Gold;
                SelectHero(scene.Root, hero.Name);
                ClickSlot(scene.Root, level, slots[i]);
                Require(session.IsRecruited(hero.Id) && session.Gold == before - hero.Cost, "完整流程招募与真实费用不符：" + hero.Name);
                spent += hero.Cost;
            }
            if (session.Heroes.Count == roster.Length)
                foreach (HeroUnit hero in session.Heroes.OrderBy(unit => unit.Level).ToList())
                {
                    int cost = session.GetUpgradeCost(hero.SlotId);
                    if (hero.Level >= BattleSession.MaxHeroLevel || session.Gold < cost) continue;
                    int before = session.Gold, rank = hero.Level;
                    ClickSlot(scene.Root, level, hero.SlotId);
                    Click(scene.Root, "升级 ");
                    Require(hero.Level == rank + 1 && session.Gold == before - cost, "完整流程升级与真实费用不符：" + hero.Definition.Name);
                    spent += cost;
                }
            Require(session.GoldSpent == spent && session.Gold >= 0, "完整流程累计消费或军饷错误");
            if (session.State == BattleState.Preparation) Click(scene.Root, "开战");
            scene.Flow.Tick(.25f);
        }
        Require(session.State == BattleState.Victory && session.Heroes.Count == roster.Length && session.RemainingEnemies == 0 &&
            session.CurrentWave == session.TotalWaves && session.DefeatedCount > 0, "真实黄巾参考部署未自然通关");
        Require(HasText(scene.Root, "守关大捷"), "胜利未通过真实流程自动显示结算");
        CheckResultStats(scene.Root, level, session);
        Require(scene.Progress.IsCleared(level.Id) && scene.Progress.GetBestCastle(level.Id) == session.CastleHealth && scene.SaveCount == 1,
            "真实胜利未正确保存一次通关记录");
        RectTransform resultBefore = FindNamed(scene.Root, "战斗结算遮罩");
        scene.Flow.CompleteBattle(session);
        Require(FindNamed(scene.Root, "战斗结算遮罩") == resultBefore && scene.SaveCount == 1, "重复终局重建结算或再次保存");
        for (int i = 0; i < 20; i++) scene.Flow.Tick(.25f);
        Require(scene.SaveCount == 1, "终局后重复更新再次写入通关记录");
        Capture(scene, output, "flow-victory");
        int next = scene.Flow.CampaignLevels.IndexOf(level) + 1;
        Require(next < scene.Flow.CampaignLevels.Count && scene.Flow.CampaignLevels[next].Id == "hulao", "黄巾下一关应为虎牢");
        Click(scene.Root, "下一关");
        CheckPage(scene, "Intel");
        Require(scene.Flow.CurrentLevel == scene.Flow.CampaignLevels[next] && scene.Flow.CurrentSession == null && !HasText(scene.Root, "守关大捷"),
            "下一关未切换真实情报或仍残留结算会话");
        scene.Flow.HandleBack();
        CheckPage(scene, "Levels");
        Require(HasText(scene.Root, "已通关  城防最佳 " + session.CastleHealth), "返回关卡后未显示内存通关记录");
        OpenRoster(scene, level);
        EnterRecommended(scene, level);
        scene.Flow.CompleteBattle(session);
        Require(!HasText(scene.Root, "守关大捷") && scene.SaveCount == 1, "上一局终局污染新会话");
        Require(scene.Progress.IsCleared(level.Id) && scene.SaveCount == 1, "再次进入已通关关卡改写了通关记录");
        Require(!scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("入门 ")), "已通关仍显示初学者教学");
        Click(scene.Root, "开战");
        Click(scene.Root.GetComponentsInChildren<Button>().First(button => button.name == "战场主操作"));
        Click(scene.Root, "返回关卡");
        CheckPage(scene, "Levels");
        Require(scene.Flow.CurrentSession == null && !HasText(scene.Root, "推演暂停"), "暂停返回关卡残留会话或弹窗");
        return session;
    }

    private static void VerifyLastResult(FlowScene scene, LevelDefinition last, BattleSession victory)
    {
        // 只补结算组件的末关分支；真实胜利状态来自上面的合法经济完整战斗。
        int retry = 0, roster = 0, levels = 0;
        RectTransform result = BattleResultView.Create(scene.Ui, scene.Root, last, victory,
            () => retry++, () => roster++, null, () => levels++);
        try
        {
            Require(!result.GetComponentsInChildren<Button>().Any(button => button.GetComponentInChildren<Text>().text == "下一关"), "末关结算仍显示下一关");
            Click(result, "重新选将");
            Click(result, "重新推演");
            Click(result, "返回关卡");
            Require(roster == 1 && retry == 1 && levels == 1, "末关结算未提供正确的可用操作");
        }
        finally { UnityEngine.Object.DestroyImmediate(result.gameObject); }
    }

    private static void VerifyPlatformPause(FlowScene scene)
    {
        // 只调用平台回调入口，不把此检查当成真实操作系统事件投递。
        var owner = new GameObject("平台回调入口检查");
        YanyiGame wrapper = owner.AddComponent<YanyiGame>();
        var field = typeof(YanyiGame).GetField("flow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        try
        {
            Require(field != null && field.GetValue(wrapper) == null, "平台检查不应启动真实资源加载");
            field.SetValue(wrapper, scene.Flow);
            wrapper.HandleApplicationPause(false);
            wrapper.HandleApplicationFocus(true);
            Require(scene.Flow.CurrentSession.State == BattleState.Running, "正常前台事件改变了运行状态");
            wrapper.HandleApplicationPause(true);
            wrapper.HandleApplicationFocus(false);
            wrapper.HandleApplicationPause(true);
            Require(scene.Flow.CurrentSession.State == BattleState.Paused, "平台失焦与挂起入口未暂停");
            wrapper.HandleApplicationPause(false);
            wrapper.HandleApplicationFocus(true);
            Require(scene.Flow.CurrentSession.State == BattleState.Paused, "恢复前台自动继续了战斗");
        }
        finally
        {
            if (field != null) field.SetValue(wrapper, null);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void VerifyErrorPage(FlowScene scene, string output)
    {
        scene.Flow.ShowMainMenu();
        int retries = 0;
        RectTransform error = ContentErrorView.Create(scene.Ui, scene.Root, "未找到关卡配置 Game/database，请先生成内容。", () => retries++);
        var owner = new GameObject("错误页平台入口检查");
        try
        {
            YanyiGame wrapper = owner.AddComponent<YanyiGame>();
            wrapper.HandleBack();
            wrapper.HandleApplicationPause(true);
            wrapper.HandleApplicationFocus(false);
            Require(error != null && error.gameObject.activeInHierarchy && HasText(error, "内容加载失败"), "空流程返回或系统事件清除了错误页");
            Capture(scene, output, "flow-content-error");
            Click(error, "重试加载");
            Require(retries == 1, "重试加载按钮未触发回调");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(error.gameObject);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void OpenRoster(FlowScene scene, LevelDefinition level)
    {
        CheckPage(scene, "Levels");
        Click(LevelButton(scene.Root, level));
        CheckPage(scene, "Intel");
        Click(scene.Root, "前往选将");
        CheckPage(scene, "Roster");
    }

    private static void EnterRecommended(FlowScene scene, LevelDefinition level)
    {
        CheckPage(scene, "Roster");
        Click(scene.Root, "推荐阵容");
        Click(scene.Root, "进入沙盘");
        CheckFreshBattle(scene, level);
    }

    private static void CheckFreshBattle(FlowScene scene, LevelDefinition level)
    {
        CheckPage(scene, "Battle");
        BattleSession session = scene.Flow.CurrentSession;
        Require(session != null && scene.Flow.CurrentLevel == level && session.State == BattleState.Preparation && session.Time == 0 &&
            session.Heroes.Count == 0 && session.Enemies.Count == 0 && session.Gold == level.InitialGold && session.CastleHealth == level.CastleMaxHealth &&
            session.DefeatedCount == 0 && session.LeakedCount == 0 && session.GoldSpent == 0 && session.Speed == 1, "新一局未重置战斗与经济状态");
    }

    private static void CheckPage(FlowScene scene, string expected)
    { Require(scene.Flow.PageKind == expected, "页面应为" + expected + "，实际为" + scene.Flow.PageKind); }

    private static void CheckSelected(FlowScene scene, int count)
    { Require(HasText(scene.Root, "已选 " + count + " / 10"), "战前阵容计数应为" + count); }

    private static bool IsSelected(Button card)
    { return card.GetComponentsInChildren<Text>().Any(text => text.text == "已入阵容"); }

    private static void ChooseHero(Button card)
    {
        Button choice = card.GetComponentsInChildren<Button>().FirstOrDefault(button => button != card);
        Require(choice != null, "选将卡缺少独立加入或移出按钮");
        Click(choice);
    }

    private static bool HasText(RectTransform root, string value)
    { return root.GetComponentsInChildren<Text>().Any(text => text.text == value || text.text.StartsWith(value + "？", StringComparison.Ordinal)); }

    private static Button HeroCard(RectTransform root, string name)
    {
        Button card = root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.GetComponentsInChildren<Text>().Any(text => text.text == name));
        Require(card != null, "未找到武将按钮：" + name);
        return card;
    }

    private static Button LevelButton(RectTransform root, LevelDefinition level)
    { return root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.GetComponentsInChildren<Text>().Any(text => text.text == level.Name)); }

    private static Button FindButton(RectTransform root, string prefix)
    {
        Button button = root.GetComponentsInChildren<Button>().FirstOrDefault(item => item.GetComponentInChildren<Text>().text.StartsWith(prefix, StringComparison.Ordinal));
        Require(button != null, "未找到按钮：" + prefix);
        return button;
    }

    private static void Click(RectTransform root, string prefix) { Click(FindButton(root, prefix)); }

    private static void Click(Button button)
    {
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable, "尝试点击不存在、隐藏或禁用的按钮");
        button.onClick.Invoke();
    }

    private static RectTransform FindNamed(RectTransform root, string name)
    {
        RectTransform result = root.GetComponentsInChildren<RectTransform>().FirstOrDefault(item => item.name == name);
        Require(result != null, "缺少活动界面节点：" + name);
        return result;
    }

    private static void SelectHero(RectTransform root, string name)
    {
        RectTransform cards = FindNamed(root, "五张武将卡");
        for (int page = 0; page < 2; page++)
        {
            Button card = cards.GetComponentsInChildren<Button>().FirstOrDefault(button => button.GetComponentsInChildren<Text>().Any(text => text.text == name));
            if (card != null) { InvokeRebuilding(card, cards); return; }
            Button next = cards.GetComponentsInChildren<Button>().First(button => button.GetComponentInChildren<Text>().text.Contains("/"));
            InvokeRebuilding(next, cards);
        }
        throw new InvalidOperationException("实际战斗阵容中未找到" + name);
    }

    private static void ClickSlot(RectTransform root, LevelDefinition level, int id)
    {
        RectTransform layer = FindNamed(root, "可交互阵位");
        int index = level.Slots.FindIndex(slot => slot.Id == id);
        Require(index >= 0, "参考阵位不存在：" + id);
        InvokeRebuilding(layer.GetChild(index).GetComponent<Button>(), FindNamed(root, "五张武将卡"));
    }

    private static void InvokeRebuilding(Button button, RectTransform cards)
    {
        Require(button != null && button.interactable, "战斗卡片操作不可用");
        // 编辑器没有帧末销毁队列，只提前释放真实回调本来就会重建的卡片。
        var callback = button.onClick;
        for (int i = cards.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(cards.GetChild(i).gameObject);
        callback.Invoke();
    }

    private static void CheckResultStats(RectTransform root, LevelDefinition level, BattleSession session)
    {
        RectTransform result = FindNamed(root, "战斗结算遮罩");
        int seconds = (int)Math.Floor(session.ElapsedSeconds);
        string[] names = { "城防", "用时", "击退", "漏兵", "军饷支出", "波次" };
        string[] values = { session.CastleHealth + " / " + level.CastleMaxHealth, seconds / 60 + "分" + (seconds % 60).ToString("00") + "秒",
            session.DefeatedCount + " 名敌军", session.LeakedCount + " 名敌军", session.GoldSpent.ToString(), session.CurrentWave + " / " + session.TotalWaves };
        for (int i = 0; i < names.Length; i++)
        {
            Transform value = result.Find("结算弹窗/统计" + names[i] + "/统计数值");
            Require(value != null && value.GetComponent<Text>().text == values[i], "真实结算统计与会话不符：" + names[i]);
        }
    }

    private static void VerifyWarningPriority(GameDatabase database)
    {
        // 独立夹具把Boss留在射程内，用真实事件顺序验证预警不会被普通增伤提示盖掉。
        GameDatabase fixture = JsonUtility.FromJson<GameDatabase>(JsonUtility.ToJson(database));
        EnemyDefinition boss = fixture.Enemies.First(enemy => enemy.IsBoss && enemy.CharacterId != "cao_cao");
        boss.MaxHealth = 100000;
        boss.Speed = 1;
        var level = new LevelDefinition { Id = "warning-check", Name = "预警检查", MapResource = database.FindLevel("huangjin").MapResource,
            InitialGold = 400, CastleMaxHealth = 30 };
        level.Routes.Add(new RouteDefinition { Id = "test-route", Points = new List<Point2> { new Point2(300, 300), new Point2(1300, 300) } });
        level.Slots.Add(new SlotDefinition { Id = 1, Position = new Point2(300, 330) });
        var wave = new WaveDefinition { StartDelay = 0, Name = "预警夹具" };
        wave.Groups.Add(new SpawnGroup { EnemyId = boss.Id, RouteId = "test-route", Count = 1, Interval = 1 });
        level.Waves.Add(wave);
        foreach (string heroId in new[] { "cao_cao", "ma_chao" })
        using (var scene = new FlowScene(fixture))
        {
            scene.Flow.BeginBattle(level, new List<string> { heroId });
            Click(scene.Root, "开战");
            // 延迟半秒招募，让马超对Boss蓄力的普通notice落在真实预警窗口内。
            scene.Flow.Tick(.5f);
            SelectHero(scene.Root, fixture.FindHero(heroId).Name);
            ClickSlot(scene.Root, level, 1);
            Text notice = scene.Root.GetComponentsInChildren<Text>().Single(text => text.name == "军令通知");
            bool sawWarning = false;
            for (int i = 0; i < 100; i++)
            {
                scene.Flow.Tick(.1f);
                if (!notice.text.Contains("即将施展技能")) continue;
                sawWarning = true;
                string warning = notice.text;
                scene.Flow.CurrentSession.SetPaused(true);
                float frozen = scene.Flow.CurrentSession.Time;
                scene.Flow.Tick(8);
                Require(scene.Flow.CurrentSession.Time == frozen && notice.text == warning, "暂停超过提示保护时长后Boss预警消失");
                var deadline = typeof(BattleView).GetField("noticeUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var viewField = typeof(GameFlow).GetField("battle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                float until = (float)deadline.GetValue(viewField.GetValue(scene.Flow));
                Require(Math.Abs(until - (frozen + 4)) < .15f, "Boss预警保护时钟未绑定真实规则实秒");
                scene.Flow.CurrentSession.SetPaused(false);
                for (int j = 0; j < 12; j++) scene.Flow.Tick(.1f);
                Require(notice.text == warning, "Boss预警被随后普通武将技能提示覆盖：" + heroId);
                break;
            }
            Require(sawWarning, "真实事件夹具未观察到Boss预警");
        }
    }

    private static void VerifyGuideStages(GameDatabase database, LevelDefinition level)
    {
        using (var scene = new FlowScene(database))
        {
            scene.Flow.BeginBattle(level, level.RecommendedRoster);
            Require(scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("入门 1/3")), "黄巾准备阶段缺少首战教学");
            CaptureGameplay(scene, "guide-preparation-1280", 1280, 720);
            CaptureGameplay(scene, "guide-preparation-2400", 2400, 1080);
            Click(scene.Root, "开战");
            var captured = new HashSet<int>();
            int[] referenceSlots = { 10, 28, 5, 6, 7, 4, 20, 2, 12, 3 };
            for (int i = 0; i < 2000 && scene.Flow.CurrentSession.State == BattleState.Running && captured.Count < 4; i++)
            {
                // 用既有合法经济参考存活到第四波，教学本身不替玩家布阵或修改规则。
                BattleSession session = scene.Flow.CurrentSession;
                for (int heroIndex = 0; heroIndex < level.RecommendedRoster.Count; heroIndex++)
                {
                    string heroId = level.RecommendedRoster[heroIndex];
                    if (!session.IsRecruited(heroId) && session.Gold >= database.FindHero(heroId).Cost)
                    {
                        SelectHero(scene.Root, database.FindHero(heroId).Name);
                        ClickSlot(scene.Root, level, referenceSlots[heroIndex]);
                        Require(session.IsRecruited(heroId), "入门验收的真实按钮参考招募失败");
                    }
                }
                scene.Flow.Tick(.25f);
                int wave = scene.Flow.CurrentSession.CurrentWave;
                if (wave < 1 || wave > 4 || captured.Contains(wave)) continue;
                string waveHint = wave == 1 ? "第一波：" : wave == 2 ? "第二波：" : "第三波：";
                bool lesson = scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("入门 " + wave + "/3") || text.text.StartsWith(waveHint, StringComparison.Ordinal));
                Require(wave <= 3 ? lesson : !scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("入门 ")),
                    "黄巾入门教学未随前三波切换或第四波未结束");
                CaptureGameplay(scene, "guide-wave-" + wave);
                captured.Add(wave);
            }
            Require(captured.Count == 4, "合法参考部署自然流程未覆盖入门前三波及第四波退出");
            Require(scene.SaveCount == 0, "入门提示检查写入了玩家通关记录");
            scene.Flow.BeginBattle(database.FindLevel("hulao"), database.FindLevel("hulao").RecommendedRoster);
            Require(!scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains("入门 ")), "其它关卡显示了黄巾入门教学");
        }
    }

    private static void VerifyBattleTimingDetails(GameDatabase database, LevelDefinition level)
    {
        using (var scene = new FlowScene(database))
        {
            scene.Flow.BeginBattle(level, new List<string> { "zhang_fei", "lv_bu", "xun_yu", "lv_meng" });
            foreach (string id in new[] { "zhang_fei", "lv_bu", "xun_yu", "lv_meng" })
            {
                HeroDefinition hero = database.FindHero(id);
                SelectHero(scene.Root, hero.Name);
                Click(scene.Root, "详情");
                string timing = BattleSession.GetAttackTimingDescription(hero, 1);
                Require(scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains(timing)),
                    "战斗待部署详情缺少真实攻击说明：" + hero.Name);
                CaptureGameplay(scene, "battle-info-" + id, 1280, 720);
                Click(scene.Root, "关闭");
            }
            SelectHero(scene.Root, "吕布");
            ClickSlot(scene.Root, level, level.Slots[0].Id);
            Click(scene.Root, "详情");
            Require(scene.Root.GetComponentsInChildren<Text>().Any(text => text.text.Contains(BattleSession.GetAttackTimingDescription(database.FindHero("lv_bu")))),
                "已部署武将详情缺少真实攻击说明");
            CaptureGameplay(scene, "battle-info-deployed", 2400, 1080);
            Click(scene.Root, "关闭");
        }
    }

    private static void CaptureGameplay(FlowScene scene, string name, int width = 1920, int height = 1080)
    {
        string output = auxiliaryOutput == null ? Path.GetFullPath("../docs/ui/runtime-gameplay") : Path.Combine(auxiliaryOutput, "gameplay");
        Directory.CreateDirectory(output);
        Capture(scene, output, name, width, height);
    }

    private static void Capture(FlowScene scene, string output, string name, int width = 1920, int height = 1080)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            scene.Camera.targetTexture = target;
            scene.Camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            foreach (Text label in scene.Root.GetComponentsInChildren<Text>())
                if (!string.IsNullOrWhiteSpace(label.text)) UiTypographyChecks.Check(label, "完整流程" + label.gameObject.name);
            scene.Camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            scene.Camera.targetTexture = null;
            RenderTexture.active = previous;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private sealed class FlowScene : IDisposable
    {
        private readonly GameObject canvasObject;
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public readonly Camera Camera;
        public readonly UiFactory Ui;
        public readonly RectTransform Root;
        public readonly GameProgress Progress;
        public readonly GameFlow Flow;
        public int SaveCount;

        public FlowScene(GameDatabase database)
        {
            canvasObject = new GameObject("完整流程验证画布", typeof(RectTransform), typeof(Canvas));
            Camera = new GameObject("完整流程截图相机", typeof(Camera)).GetComponent<Camera>();
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRect = (RectTransform)canvas.transform;
                canvasRect.pivot = new Vector2(0, 1);
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                Camera.orthographic = true;
                Camera.orthographicSize = 540;
                Camera.transform.position = new Vector3(960, -540, -100);
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = UiFactory.Deep;
                Camera.nearClipPlane = .1f;
                Camera.farClipPlane = 200;
                canvas.worldCamera = Camera;
                Ui = new UiFactory();
                Root = Ui.Rect(canvasRect, "完整流程逻辑画布", 0, 0, 1920, 1080);
                Progress = new GameProgress((key, fallback) => { int value; return Values.TryGetValue(key, out value) ? value : fallback; },
                    (key, value) => Values[key] = value, () => SaveCount++);
                Flow = new GameFlow(Ui, Root, database, Progress);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(Camera.gameObject);
                throw;
            }
        }

        public void Dispose()
        {
            try { Flow.Dispose(); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(Camera.gameObject);
            }
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
