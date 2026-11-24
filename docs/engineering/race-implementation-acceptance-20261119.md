# 布阵竞速实施验收

日期：2026-11-19。状态：P1、P2代码与独立两平台试玩包已交付，自动验收通过；真人与真机项目保留待验。独立版本为Race-20261119，战斗规则faction-army-v1，竞速资格all-boss-v1。

## 已验证范围

| 优先级 | 范围 | 结果与证据 |
| --- | --- | --- |
| P1 | Boss实例对账与竞速资格 | Domain15组通过，含同身份多实例、普通漏兵合格、Boss突破不合格、事件排空、未终局／未生成完 |
| P1 | 新竞速计时 | 固定规则步终点、30／60／120帧及长帧对照、暂停与终局冻结通过；旧ElapsedSeconds继续保留 |
| P1 | 独立最快与异常保护 | 存档20组及旧记录118断言通过；更快／相同／更慢、旧数据隔离、跨实例读取、损坏／缺键／部分提交、读写／保存异常 |
| P1 | 逐关提速策略 | 80局69合格；五关两套常速参考均合格，每关动态更快且复跑一致，不改配置数值 |
| P1 | 旧规则与内容 | 139项规则（含五关模拟）、22组军势（含40局）、6项反馈、9项Python、25将5关内容校验通过 |
| P1 | 首次倍率说明与UI | Unity实际编译；10组177断言、78张图通过，含三尺寸、3张模拟安全区、真实射线、满10将密集Boss、缺钱／满阶／暂停／失败／末关 |
| P2 | 名单调整、首次失守和轻量摘要 | 沿用合法名单、新局重新付费；首次失守事实、各倍率实际时长／切换、部署／调位／升级节点与分页通过 |
| P1／P2 | 独立Windows／Android | 两平台构建通过，同一474文件输入构建前后均一致；147文件Windows包隐藏启动8秒无异常；APK590项CRC及v2验签通过 |

## 试玩入口

- Windows：[YanyiSandpan.exe](../../UnityGame/Builds/Race-20261119/Windows/YanyiSandpan.exe)，保留整个Windows目录。
- Android：[YanyiSandpan.apk](../../UnityGame/Builds/Race-20261119/Android/YanyiSandpan.apk)，72028802字节，约68.69MiB；API26起、ARM64，Unity开发签名。
- [试玩说明](../../UnityGame/Builds/Race-20261119/README.md)、[机器验收与边界](../../UnityGame/Builds/Race-20261119/acceptance.json)、[78张界面图](../ui/runtime-race-20261119/README.md)。
- [Word计划](race-implementation-plan-20261119.docx)与[Word验收](race-implementation-acceptance-20261119.docx)。

## 规则与数据边界

普通胜利保持城防仍存即可，Boss突破仍扣15；新主竞速另要求全部配置Boss实际生成并真实击退、Boss突破为0，允许普通漏兵。同关同faction-army-v1同all-boss-v1动态1／2／3倍同榜，严格更快刷新。没有修改攻击、经济或出兵数值。

新RaceElapsedSeconds只累计实际完成规则步，终局帧多余输入不记入主竞速。旧ElapsedSeconds、SpeedMask、普通最快／最近／通关／城防接口保留，历史成绩不自动认证成竞速成绩。

独立键前缀为yanyi.race，按关卡、实际战斗规则和资格版本隔离；标识符含分隔符也不会串榜。32KB有界二进制摘要、校验及双槽活动指针降低部分写入风险；这是本机损坏检测，不是防作弊或跨进程事务系统。

品质审查发现过一次真实问题：旧纪录瞬时读取失败可能误认为缺档，导致较慢值覆盖最快。已先用红测复现，再修复为读取失败时禁止写入、不报首次／刷新；说明标记保存异常也会安全回滚。独立质量复核通过。

UI还修正了非末关失败误显示战役已结束，现为禁用的“下一关需先通关”；只有末关胜利显示战役结束。普通存储异常不会中断竞速结果页。各模块均经过独立规格与质量审查，最终集成审查未发现剩余P1／P2。

## 策略证据

完整名单、阵位、升级时点、倍率轨迹、Boss与经济账本见[策略参考](race-strategy-reference-20261119.md)。五关选中动态参考比两套常速中较快值缩短11.2%～22.2%；只证明这些脚本成立，不声称最优或更好玩。

初版“第四同营优先”仅换标签、真实操作未变，经规格审查指出后补红测并修为真实留钱凑四将，断言前4次招募同阵营且轨迹不同。最新80局69合格，包含如实保留的终局第四人优先普通胜利／Boss未全击退；旧80局70合格数已失效。

## 界面与构建证据

75张图覆盖25类状态的1280×720、1920×1080、2400×1080，另3张实际套用SafeAreaLayout并核对页面四角，模拟不对称预留／右侧刘海／底部预留。首次／快慢等短时间结算用隔离夹具，压力图用内存高资金／高生命夹具，均不修改正式配置。截图目录与机器清单按本次产出记数。

Windows隐藏启动只证明进程8秒存活及日志无异常，没有自动操作用户桌面或进行可见完整人工流程。Android在F:/Development/Unity/BuildProjects/Race-20261119新副本构建，仅复制Assets／Packages／ProjectSettings，构建前后输入与Windows474份快照一致。

APK SHA-256：077F67245B79C16EF30A8DF3AA18B0F8894992CEAC3004BD634E5F31927D2568。590项ZIP CRC有效，v2签名有效；包名com.course.yanyisandpan，中文名称军师请下令，minSdk26／targetSdk36，arm64-v8a及IL2CPP库完整。资源表反查自适应图标XML，前景／背景节点存在；资源路径经AAPT缩名，不能按ZIP文件名推断图标缺失。

509份旧军势包／旧军势截图指纹全部一致，未调用历史App／FactionArmy截图入口。未安装依赖、清档、重置分支或提交构建缓存。完整构建输入指纹与Windows文件指纹随包保留。

## 验证入口与日志

```powershell
./UnityGame/Tests/Run-RaceTests.ps1
./UnityGame/Tests/Run-RaceSimulation.ps1 -Output .planning/race-implementation-20261119/race-simulation-results.json
./UnityGame/Tests/Run-Tests.ps1 -Simulate
./UnityGame/Tests/Run-FactionTests.ps1
./UnityGame/Tests/Run-FeedbackTests.ps1
./tools/Build-Game.ps1 -Target Race
./tools/Build-Game.ps1 -Target WindowsRace
./tools/Build-Game.ps1 -Target AndroidRace -ProjectDirectory <新的ASCII工程副本>
```

主要日志在.planning/race-implementation-20261119：domain-root-verification.log、records-root-review-verification.log、race-simulation.log、final-domain-regression.log、final-faction-regression.log、final-feedback-regression.log、final-python-tests.log、final-content-verify.log、ui-safearea-final.log、build-windows-race.log、windows-player-smoke.log、build-android-race.log、apk-badging.log、apk-signature.log。Python9项结果为OK；Windows PowerShell将unittest标准错误流包为NativeCommandError显示，进程实际退出0。

## 尚未验证

5～8名新玩家首玩理解、人工乐趣、真实听感，以及Android真机的触控、性能、发热、后台恢复与真实PlayerPrefs跨进程／设备存档均未验。自动按钮、离屏截图、规则模拟、包构建和APK验签分别提供有限证据，不能替代这些体验验证。

本环境缺少LibreOffice；技能完整Schema校验器也因缺少defusedxml无法运行，未安装依赖。Word版改用本地ZIP完整性、全部XML解析、表格总宽／列宽及Markdown逐段覆盖检查，不宣称已完整Schema验证或逐页渲染。P3附加挑战、更多角色、长期养成、更高倍率及联网榜保持后置。
