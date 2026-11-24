# 布阵竞速实施计划

> 执行方式：使用subagent-driven-development按独立模块实施与复核，依赖接口顺序接入。用户已授权本计划所依据的竞速设计及落地，不再次等待同一设计确认。

目标：将已认可方案的P1、P2落地为独立竞速规则、可理解的提速与结算、便捷重试及可信策略摘要，生成独立验证与试玩产物。

执行结果：P1、P2代码、自动验收与独立Windows／Android包已完成。15组竞速、20组存档及旧118断言通过；80局正常经济策略69合格，五关动态更快且复跑一致；Unity177断言78图、两平台构建与APK v2验签通过。真人与真机待验项见[验收报告](race-implementation-acceptance-20261119.md)，不能用勾选的实施工作替代体验验证。

架构：Domain保留原战斗推进，新增不可变竞速快照；GameProgress独立保存竞速成绩；GameFlow只从当前终局会话提交。uGUI继续使用青绿纸面与现有构图，按实际数据精修，不重写v3/v4视觉。

技术：Unity 6000.3.28f1、C#／uGUI；规则测试使用系统.NET Framework的C#5编译器；不安装任何依赖。

## 范围与验收契约

普通通关仍允许Boss突破后以剩余城防获胜；新主竞速要求胜利、预期Boss全部生成并真实击退、Boss突破为0，允许普通漏兵。预期数来自Waves.Groups中的IsBoss与Count，不来自BossHeroIds。

同关同战斗规则同资格版本all-boss-v1，动态1／2／3倍同榜，严格更快才刷新。新竞速采用固定步终点计时，暂停与后台不推进；旧ElapsedSeconds及旧普通记录继续保留，避免把新计时写回历史普通基线。没有改变攻击、伤害或经济规则时保留实际战斗RuleVersion。

新增源码文件与meta由Unity正常导入；所有修改先备份到.planning/race-implementation-20261119/before，保存哈希。历史包与截图只读。原仓库无可用提交，不自动建立Git提交或清理目录。

## 任务1：领域竞速与复盘数据

文件：修改UnityGame/Assets/Scripts/Domain/BattleSession.cs、CombatSystem.cs、RuntimeTypes.cs；新增Domain/RaceSnapshot.cs、Domain/BattleRace.cs；新增UnityGame/Tests/RaceDomainTests.cs及Run-RaceTests.ps1。

- [x] 先写行为测试：同Boss多实例、普通漏兵合格、Boss突破普通胜利不合格、未终局／未生成完不合格、事件排空不改变资格。
- [x] 用同一预定脚本对比30／60／120帧及长帧，断言竞速终点相差不超过一个1/30步；暂停／终局、空切倍率、收入与冷却不补算。
- [x] 运行专项确认失败来自缺少新竞速能力，再实现并运行专项与旧规则。

冻结入口：

```csharp
public double RaceElapsedSeconds { get; }
public int ExpectedBossCount { get; }
public int SpawnedBossCount { get; }
public int DefeatedBossCount { get; }
public bool IsRaceEligible { get; }
public BreachRecord FirstBreach { get; }
public RaceSnapshot GetRaceSnapshot();
```

RaceSnapshot只读属性：LevelId、RuleVersion、QualificationVersion、ElapsedSeconds、SpeedMask、CastleHealth、LeakedCount、ExpectedBossCount、SpawnedBossCount、DefeatedBossCount、BossBreachCount、IsEligible、IneligibilityReason；GetSpeedSeconds(int)返回真实处理时长，SpeedChanges与Actions只读，SummaryTruncated说明摘要截断。RaceSpeedChange保存Speed、ElapsedSeconds；RaceAction保存Kind、HeroId、SlotId、Level、Gold、ElapsedSeconds。

Boss击退在真实伤害死亡入口按实例去重。时长由原输入片段队列实际消费，终局帧未推进部分不进入竞速。FirstBreach与LastBreach保存复制事实；BreachRecord补Wave、Gold、Speed、ElapsedSeconds用于复盘。轻量记录有上限，截断后显示提示，不声称完整回放。

运行：`./UnityGame/Tests/Run-RaceTests.ps1`、`./UnityGame/Tests/Run-Tests.ps1 -Simulate`。预期新专项及139项旧规则通过，保存真实日志与失败修正记录。

## 任务2：独立竞速纪录与兼容

实际文件：新增Presentation/RaceRecord.cs、RaceProgress.cs、RaceRecordCodec.cs，GameProgress.cs仅改partial声明；新增UnityGame/Tests/RaceRecordTests.cs并接专项。

- [x] 先写首次、刷新、相同、较慢、不合格胜利、损坏、缺键、部分提交、旧成绩隔离与重新读入测试，运行确认失败。
- [x] 增加GetRaceRecord(levelId, ruleVersion)与RecordRaceVictory(RaceSnapshot)；键按关卡、战斗RuleVersion、资格all-boss-v1隔离，写入前清提交标记，字段和校验完成后最后提交。
- [x] RaceRecordResult先持有PreviousRecord再写本次，区分首次／刷新／相同／未刷新／不合格；旧RecordVictory、FastestRuleVersion及普通最近接口保持语义。

```csharp
public RaceRecord GetRaceRecord(string levelId, string ruleVersion);
public RaceRecordResult RecordRaceVictory(RaceSnapshot snapshot);
```

运行专项与旧记录编辑器检查；旧记录0突破不能自动写入新前缀。新读写不扫描或重写旧存档。

## 任务3：提速、关键UI与低成本重试

实际文件：修改Presentation/GameFlow.cs、BattleView.cs、BattleArmyPresentation.cs、MenuViews.cs、UnitView.cs；新增RaceResultView.cs，旧BattleResultView.Create兼容保留；新增Editor/UiRaceChecks.cs，UiRefinementChecks.Scene增加可注入内存存档用于异常检查。

- [x] 先增加真实按钮和生命周期断言：首次1→2仅切一次、确认后已读、阅读暂停、后台不自动恢复、重试不再弹、规则随时可读、旧会话不写记录。
- [x] 倍率说明通过GameFlow统一模态生命周期，暂停中空切不计摘要；首战高倍率只解释压力，不强制解锁。
- [x] 顶栏稳定呈现军饷、城防、倍率与用时；军令台去重复阶位，升级突出真实变化项与费用，保留真实首次施放及孤阵预览。
- [x] 选将查看焦点、加入名单和成长预览分离；新“调整阵容”入口沿用上一局合法名单，重新进入仍正常付费、从新会话开始。
- [x] 结算展示竞速资格、此次／此前最快／差值，普通胜利但Boss突破保留结果并说明不合格；显示首次失守与真实倍率时长／操作摘要，空记录和末关导航都有明确状态。
- [x] 辨识采用小形状／底座辅助，不恢复大姓名牌；军势名单预览与实际收益分开，青绿纸面和令印章沿用。

运行新的Race编辑器入口，覆盖正常、空部署、缺钱、满阶、暂停、说明、密集战斗、Boss、合格／不合格／失败与无记录；1280×720、1920×1080、2400×1080及安全区分别取图。截图是布局证据，手机握持与触控须真人真机确认。

## 任务4：五关可重复策略与回归

文件：新增UnityGame/Tests/RaceSimulationTests.cs；增补专项入口；按当前配置补tools/test_wave_pacing.py的Faction映射口径。

- [x] 每关两条有意义差异的1×全Boss参考，对比先招第四同营、早升核心与功能补位，正常账本不加钱、不越过名单规则。
- [x] 用固定时刻或可观察战斗条件切换倍率；寻找每关至少一条比其1×参考更快的合格策略。固定2／3倍保留压力结果，不强求全胜。
- [x] 输出阵容、阵位、升级／招募节点、倍率时长、Boss对账、城防、账本和有效实秒，失败如实记录，未达标先诊断；只在证据支持时做单变量平衡实验。
- [x] 波次旧断言先补精确Faction映射验证，再纳入比较放行，保留所有其他字段与波次保护。

运行Run-Tests.ps1 -Simulate、Run-FactionTests.ps1、Run-FeedbackTests.ps1、Run-RaceTests.ps1及现有Python成长／波次／内容检查；所有失败先区分旧基线与本轮回归。

## 任务5：独立输出与交付

文件：修改tools/Build-Game.ps1、UnityGame/Assets/Editor/YanyiBuild.cs；新增独立Race验收／Windows／Android入口；更新docs/engineering中的专项验收、主路线图、运行契约及项目说明。

- [x] 独立输出到UnityGame/Builds/Race-20261119和docs/ui/runtime-race-20261119；构建输入清单与旧包／旧图哈希分别保护。
- [x] Unity验证与新按钮／三尺寸验收通过后构建Windows。Android新建ASCII工程副本，只复制Assets／Packages／ProjectSettings，构建前对照输入指纹，输出新APK并单独校验与验签。
- [x] 不调用旧App／FactionArmy截图入口覆盖证据；如复用旧检查代码，将输出参数化为新目录。
- [x] 计划及验收报告同步Word，记录具体通过项和不能完成的环境／设备限制。缺少LibreOffice时完成内容／结构检查并披露未逐页渲染。
- [x] 最后逐项核对方案覆盖，人工乐趣、真机触控／性能／听感与5～8人首玩如无外部人员或设备则明确未验，不能用模拟替代。

## 当前计划复核

P1竞速／说明／纪录／军饷取舍／逐关提速／战斗UI分别对应任务1～4；P2重试、复盘与摘要对应任务1～3。P3内容扩展保持后置。实际实现中允许按真实接口补充小范围文件，须先记录范围与测试。
