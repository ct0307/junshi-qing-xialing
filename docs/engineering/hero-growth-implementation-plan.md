# 武将成长与进军体验实施计划

> 执行方式：使用subagent-driven-development分工实现，规则、记录、UI拥有互不重叠的文件范围；根任务统一内容配置、Unity运行、审查与构建。用户2026-11-06回复“好”，已批准hero-growth-tempo-design.md整体方案，无需再次请求执行许可。

**目标：** 25将快普攻与专属二三阶成长、敌军进军1／2／3倍与独立攻击时钟、每关最快实秒记录，以及上下军令台UI。

**架构：** 继续采用纯C# Domain与Unity/uGUI展示分离。规则固定实秒步进，另维护MarchTime推进移动、出兵、波间与军饷。成长由共享解析结果驱动规则及说明，GameProgress保存版本化最快记录。

**技术：** Unity6000.3.28f1、C#5规则、uGUI、现有Python/Pillow。无新增依赖，无安装，无Git初始提交；先将本轮相关源文件保存到忽略目录.tools/growth-before。

## 公共契约

规则任务提供：

```csharp
// Time继续用于攻击／技能／状态和展示，不随进军倍率改变。
public float Time { get; private set; }
public float MarchTime { get; private set; }
public double ElapsedSeconds { get; private set; }
public int SpeedMask { get; private set; }
public int BossBreachCount { get; private set; }
// 既有NextWaveCountdown改为当前倍率下预计实秒。
// 当前用法保留，并增加等级明确的说明和覆盖接口。
public static string GetAttackTimingDescription(HeroDefinition definition, int heroLevel);
public static float GetMaximumRange(HeroDefinition definition, int heroLevel);
public static string GetGrowthDescription(HeroDefinition definition, int heroLevel);
```

- SpeedMask：实际运行过1／2／3倍分别置1／2／4，暂停中切换不置位。
- ElapsedSeconds：仅Running累计传入的有效实秒，不乘倍率；固定步处理限额不能让累计实秒变短；终局后冻结。
- 新增basic_attack事件只代表普通攻击；attack保留特色技能／协射等原始语义。普通攻击不绘制吕布扇形等技能几何。
- 成长配置字段由规则任务定义后立即通知根任务，根任务同步生成器；保留旧字段默认兼容测试夹具，不用复制25套战斗引擎。

记录任务提供以下可注入存储接口，保留原RecordVictory重载：

```csharp
public FastestRecord GetFastestRecord(string levelId);
public VictoryRecordResult RecordVictory(string levelId, int castleHealth,
    double elapsedSeconds, int speedMask, int leakedCount, int bossBreachCount);
// 返回比较前纪录、有效本次纪录、首次／刷新标志，结算不能先保存再猜旧值。
```

FastestRecord包含毫秒数、倍率摘要、城防、漏兵、Boss突破数；新存储键带规则版本，旧通关／最佳城防不清理。GameFlow向BattleResultView传VictoryRecordResult；两个任务实现前直接协商最终类型名称和可选参数兼容。

## 任务1：快普攻、25人成长与双时钟

文件：Assets/Scripts/Domain/*.cs、Tests/DomainTests.cs，以及新增纯规则测试文件。规则任务所有；不改生成器、正式database、Presentation或Editor。

- [x] 先增加旧版失败的双时钟行为检查：固定范围内耐久目标，1／2／3倍相同实秒的普攻频率一致；移动、出兵、波间和军饷增量随倍率。
- [x] 用现有测试入口取得失败日志，后实现实秒步进、MarchTime、ElapsedSeconds、SpeedMask和BossBreachCount。
- [x] 加入独立普攻与技能就绪时间、技能快照参数及二三阶机制，完整覆盖设计表25人，不改变同关禁选和一次招募。
- [x] 成长预览、攻击节奏、实际最大覆盖共享规则结果，升级不重置冷却。
- [x] 检查Boss硬控、干扰、传播、协射与追击去重；华佗2／波、8／局；鲁肃100／局及原子预支。
- [x] 适配旧测试中已批准改变的节奏／升级断言，保留招募、扣费、胜败、事件身份等原行为检查；不通过删除严格成功断言掩盖失败。

双时钟行为示例：

```csharp
session.Start(); session.SetSpeed(1); session.Tick(2f);
session.SetSpeed(3); session.Tick(3f);
Near(5f, session.Time); Near(11f, session.MarchTime);
Check(Math.Abs(session.ElapsedSeconds - 5) < .001);
Equal(5, session.SpeedMask);
```

运行：`./UnityGame/Tests/Run-Tests.ps1 -Simulate`。先保存预期失败，再要求0失败；通用模拟必须在失败时影响退出码。五关正常经济参考由根任务集成校准，明确容错与全Boss击退差别。

## 任务2：最快记录与流程接入

文件：Presentation/GameProgress.cs、GameFlow.cs、新增记录类型；Editor/UiFastestRecordChecks.cs。记录任务所有；不改UI视图、Domain、生成器。

- [x] 内存字典测试先覆盖无旧时间、严格更快、相同／更慢、非法／溢出、倍率摘要与旧城防保留，取得旧版失败证据。
- [x] 存储整数毫秒和同次记录快照，验证全部字段，损坏值回退为空；失败和无效时间不阻止已有通关／城防逻辑。
- [x] GameFlow仅当前会话首次胜利记录；将比较结果传入结算；暂停页可查看当前关最快。
- [x] 真正计时从Start开始，备战／暂停／后台不累计，冻结后不改变；使用规则提供ElapsedSeconds，验证低帧处理上限不产生较短成绩。
- [x] 独立Unity检查使用内存存储，不写玩家偏好；记录首次与刷新文案数据，旧会话与重复结算拒绝。

原最快期望序列：首次100000ms→保存；110000ms→不更新最快；100000ms→不更新最快；90000ms→保存并返回旧100000ms；失败／NaN／0→不刷新；最佳城防独立更新。v2追加：每次合法胜利均保存最近通关，较慢／相同局不能因未破纪录而丢失。

## 任务3：上下军令台UI与真实交互

文件：Presentation/MenuViews.cs、BattleView.cs、BattleResultView.cs、BattleIntelView.cs、BattleEffects.cs、UnitView.cs、UiFactory.cs、BattleLayout.cs／PlacementGuide，以及相关Editor/Ui*Checks.cs（UiFastestRecordChecks除外）。UI任务所有；不改GameFlow／GameProgress／Domain／生成器。

- [x] 先用真实按钮检查旧版查看卡片会改变阵容、没有进军3倍／计时／成长预览等缺失，保存红证据。
- [x] 选将主体打开详情，独立勾选区改变选择；五卡两页，招募／已部署／不足／满级状态准确。
- [x] 顶部整合军饷、城防、用时、波次、余敌和进军1／2／3倍；底部固定升级当前→下一阶变化、费用、差额。
- [x] 成长后的实际射程／形状覆盖使用当前等级；基础攻击与技能表现、音频派发区分。
- [x] 胜利页显示首次纪录／破纪录差额／本次与最佳；关卡卡片显示最快，失败不出现纪录刷新。
- [x] 保留v3方向与安全区，改善损失警示及密集单位展示，覆盖1280／1920／2400三个尺寸。
- [x] 更新原UI夹具中已批准改变的分页、攻击间隔与升级文案断言，保留字形、裁切、原子扣费、Boss容错及流程边界。

检查：根任务统一运行 `./tools/Build-Game.ps1 -Target Ui`，独立代理不得同时启动Unity。截图输出docs/ui/runtime-growth并注明正式内容与隔离夹具边界。

## 任务4：内容配置、集成与审查

根任务所有：tools/build_game_content.py、tools/test_wave_pacing.py与fixtures、Resources/Game/database.json和内容清单、GameAudio.cs、工程文档／计划、构建产物记录。

- [x] 获取规则字段契约，给25人配置普攻间隔、特色冷却与平衡参数，生成器与正式配置一致。运行data-only，保持道路、阵位、地图与历史美术指纹。
- [x] 用既有Python执行内容与波次检查；旧波次基线只针对本轮未改变的内容保留，不把批准的武将成长变更误判为波次篡改。
- [x] 接入basic_attack音频映射，保持普通／关键声部与限频预算；音频素材不重新制作。
- [x] 根任务重新运行规则、五关严格成功参考、暂停切速与经济检查；必要时调整每将基础输出／费用，记录每次调参依据。
- [x] 两阶段独立审查：先核对25项需求及机制边界，再审查正确性／性能／持久化；修复明确问题后复审。
- [x] 更新runtime-contract与README，记录新行为与未完成的手机人工验收，不把自动关卡胜利当作体验已通过。

运行命令：

```powershell
./UnityGame/Tests/Run-Tests.ps1 -Simulate
& 'C:/Users/chentao/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -X utf8 -B tools/build_game_content.py --verify-only
& 'C:/Users/chentao/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -X utf8 -B tools/test_wave_pacing.py
./tools/Build-Game.ps1 -Target Verify
./tools/Build-Game.ps1 -Target Ui
```

## 任务5：产物与最终验收

- [x] 保留历史产物，按既有英文构建副本同步本轮源文件；主／英文副本399个工程文件在构建前后SHA一致。
- [x] 串行Windows／Android构建退出0；Windows完整147文件复制核对，Android大小／SHA、566项ZIP CRC、应用名、ARM64／API26／36及APK v2签名通过，详见本轮证据。
- [x] Windows人工试玩：2026-11-08用户确认“已经试玩过了”；本次不重复启动或操作用户窗口，未逐项反馈的日志与专项结果不推定通过。
- [ ] 如有已连接手机执行目标设备验收；没有设备时明确保留手机触控、性能、系统回调、五关人工体验与听感待验。
- [ ] 所有文档与规划按当前产物、测试次数和实际证据更新，最终给出安装包与代表画面。

## 本轮进度

- [x] 方案获用户批准，明确执行授权与文件范围。
- [x] 建立实施分工及接口契约。
- [x] 规则／记录／UI实现与集成。
- [x] 两阶段审查与自动验证。
- [ ] 构建交付及证据整理。

## 2026-11-06试玩反馈v2修正与验收

原成长设计与25人成长机制保留在hero-growth-tempo-design.md；首版0.60～1.10秒规格是历史，不回写成v2。当前完成状态如下：

- [x] 25项显式普攻间隔逐项缩至首版70%，正式配置0.420～0.770秒，固定步实测约0.433～0.800实秒。单次普攻伤害、技能、Boss、经济、波次、路线与v3/v4源稿不变；data-only仅更新数据库及内容指纹。
- [x] 正式25人耐久目标检查先在旧配置失败，再验证15秒至少19次普攻、实际间隔0.4～0.8秒、三倍率攻击时点一致；抑制普攻后技能／蓄力时点不变。普通123项，根-Simulate为124项通过／0失败，日志UnityGame/Logs/domain-feedback-root.log。
- [x] 内容测试采用明确0.420～0.770秒范围，4项内容与4项波次检查通过，日志UnityGame/Logs/content-feedback.log；不改生产参数迎合旧断言。
- [x] 最快键升级growth-tempo-v2；所有有效胜利保存最近通关，慢局／相同也存；保留v1旧版最快单独展示，不参与v2比较。首次／慢局／相同／快局、重建流程、重复终局／旧会话／失败不误写均有自动证据。
- [x] 可见返回复用HandleBack，继续布阵保留会话与费用，重新选将／返回关卡结束备战，运行时返回暂停。致命命中传给当帧退场对象，先闪亮再短退场，暂停冻结，终局仍立即清理。
- [x] 完整Unity UI日志UnityGame/Logs/ui-feedback-final.log退出0；最近记录11状态×3尺寸=33张、反馈4×3=12张、原成长18×3=54张。目录为docs/ui/runtime-recent-records、runtime-feedback、runtime-growth；不把夹具图当正式难度。
- [x] 当前v2两平台构建退出0：日志build-windows-feedback.log、build-android-feedback.log。Windows独立Builds/Windows-Feedback整目录147文件、265734255字节；Android61735507字节，SHA-256为f332f9a896178fff1e6e9187a215dca50488d51177214b585c43be31925d9298，566项CRC及APK v2签名通过。主／英文副本399工程文件前后SHA一致。
- [x] 2026-11-08用户确认已完成Windows试玩；助手未操控用户进程。五关、听感与跨进程存档仍按专项证据验收。
- [ ] 手机安装／触控／安全区／性能、五关人工玩法及解压感、听感、真实跨进程PlayerPrefs读写／迁移仍待设备或人工验收。

根日志严格正常经济参考：虎牢420.1、黄巾推荐371.7／替代349.3／提前升阶373.8、官渡397.0、赤壁432.3、终局353.2实秒，均全Boss击退且城防30；赤壁有1名普通漏兵，城防30可包含医护收益。原v1参考447.1／383.8／359.7／386.2／412.8／450.1／359.3保留为历史，不与v2成绩混排。
