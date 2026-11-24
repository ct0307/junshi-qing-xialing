# 军师请下令

**三国名将 · 布阵守城**  
军师一声令，名将守山河。

项目已由“演义沙盘”更名为“军师请下令”，主菜单、Android应用显示名和Windows产品名已同步，沿用原有包名与工程路径。

移动应用开发课程的横屏三国塔防项目，使用 Unity 6.3 LTS（6000.3.28f1）、C# 与 uGUI，目标为 Android 离线运行。

## 当前试玩：战斗体验样板（2026-11-19）

新版本在[Builds/Sensation-20261119](Builds/Sensation-20261119/README.md)，包含倍率直选、三将技能表现、黄巾30槽布局与第4波16人。成绩隔离为sensation-hj-mn-v1-army，原Race包保留。复验使用Sensation/WindowsSensation/AndroidSensation三个独立构建入口；详情见[实施验收](../docs/engineering/combat-sensation-acceptance-20261119.md)。

## 历史试玩：布阵竞速版（2026-11-19）

最新交付在`Builds/Race-20261119/`：[试玩说明](Builds/Race-20261119/README.md)。Windows运行完整目录内的`Windows/YanyiSandpan.exe`；Android使用`Android/YanyiSandpan.apk`（约68.69MiB，API26起、ARM64）。

本版落地已认可P1、P2：全Boss真实击退主竞速、独立固定步终点计时与最快、首次倍率说明／后台暂停保护、此次与此前最快差值、战斗UI精修、沿用名单调整和真实倍率／操作复盘。战斗数值保持faction-army-v1，新资格all-boss-v1；普通胜利仍容错，普通漏兵不妨碍合格，Boss突破不计新竞速。旧普通最快、最近和城防记录保留，不清档或自动认证。

```powershell
./UnityGame/Tests/Run-RaceTests.ps1
./UnityGame/Tests/Run-RaceSimulation.ps1 -Output .planning/race-implementation-20261119/race-simulation-results.json
./tools/Build-Game.ps1 -Target Race
./tools/Build-Game.ps1 -Target WindowsRace
# APK在新的纯英文工程副本中构建
./tools/Build-Game.ps1 -Target AndroidRace -ProjectDirectory F:/Development/Unity/BuildProjects/Race-20261119
```

本轮177项UI断言／78张图、两平台构建与APK验签通过，139项旧规则等回归通过。80局策略69合格，五关均找到比常速更快且复跑一致的动态提速参考；不宣称全程3倍必须获胜或更好玩。Windows仅隐藏启动检查，未进行可见完整人工操作；真机、真人、听感及实际PlayerPrefs跨进程待验。见[实施验收](../docs/engineering/race-implementation-acceptance-20261119.md)与[Word计划](../docs/engineering/race-implementation-plan-20261119.docx)。

## 历史试玩：军势与反馈版（2026-11-12）

历史交付在 `Builds/FactionArmy-20261112/`：[试玩与安装说明](Builds/FactionArmy-20261112/README.md)。Windows运行 `Windows/YanyiSandpan.exe`，需保留旁边数据文件；Android使用 `Android/YanyiSandpan.apk`（API26起、ARM64）。旧包保留。

本版清理大姓名牌，姓名／阶位移至将册；强化吕布、袁绍、周瑜真实出手和升阶首次施放。战前标注阵营与名单预计档，战中按实际招募2/4人激活军势，群雄用220逻辑距离判断孤阵。军势版伤害改变，成绩独立为 `faction-army-v1`，不复制或删除旧版最快。

本轮命令：`./UnityGame/Tests/Run-FactionTests.ps1`、`./tools/Build-Game.ps1 -Target FactionArmy`、`-Target WindowsFactionArmy`。APK用新ASCII副本加 `-Target AndroidFactionArmy`。完整结果与证据见[验收](../docs/engineering/faction-army-acceptance-20261112.md)；以下保留既有工程说明和历史版本资料。

## 历史Windows对照：战斗体验表现版 CombatExperience-20261112

已落地0.7实秒固定窗口、3／6／10连破分档与合并音；补充诸葛亮风眼、周瑜圆火区／道路火带、庞统真实传伤／补入线和荀彧协射来源。共享90特效／14数字预算，暂停、旧事件和终局清理已验证。玩法数值、军饷、进军倍率和存档规则保持。

运行 [新Windows试玩](Builds/CombatExperience-20261112/Windows/YanyiSandpan.exe)，分享时复制完整Windows文件夹。详见[试玩说明](Builds/CombatExperience-20261112/README.md)、[验收记录](Builds/CombatExperience-20261112/acceptance.json)及[301张新图](../docs/ui/runtime-combat-experience-20261112/README.md)。本轮139项规则、6项聚合、五关模拟、内容检查、Unity专项和Windows构建通过，9条固定玩法轨迹与冻结版逐字节一致。隐藏进程启动持续响应，未做可见窗口人工交互；解压感和听感仍由用户对比试玩判断。

```powershell
./UnityGame/Tests/Run-FeedbackTests.ps1
./tools/Build-Game.ps1 -Target Experience
./tools/Build-Game.ps1 -Target WindowsExperience
```

新入口输出独立目录，避免使用下方历史入口重写Jade。Android仍为Jade版本，本轮未重打APK。

## 保留对照：青绿行军全流程 JadeApp-20261112

菜单、选关、情报、选将、暂停、结算、声音设置和错误页已延续认可的浅绢／青玉风格，统一宋体标题与黑体正文。用户已试玩本轮Windows，反馈整体还行、解压感仍需提升。

| 平台／资料 | 保留入口 | 使用说明 |
| --- | --- | --- |
| Windows | [YanyiSandpan.exe](Builds/JadeApp-20261112/Windows/YanyiSandpan.exe) | 分享时必须复制完整 `Windows` 文件夹。 |
| Android | [YanyiSandpan.apk](Builds/JadeApp-20261112/Android/YanyiSandpan.apk) | Android 8.0及以上、ARM64；手机尚未实测。 |
| 界面截图 | [247张运行时图及验收边界](../docs/ui/runtime-jade-app-20261112/README.md) | 覆盖正常、空阵容、错误、暂停、胜败及三种尺寸。 |
| 交付记录 | [acceptance.json](Builds/JadeApp-20261112/acceptance.json) | 构建、源码与历史指纹、产物、签名和检查边界。 |

Jade交付时124项规则、五关自动模拟及内容检查通过；Unity App、Windows、Android均退出0，422份构建源码与1192份历史文件核对通过，APK71,761,217字节、569项ZIP CRC及v2签名通过。这些是旧版证据。后续表现版修改了授权源码，不能再用Jade源码指纹证明当前源码；旧包和旧图保留不变。手机触控／性能、实际听感、五关人工和跨进程存档仍待专项验证。

## 后续改进方向：解压感与武将配合

首轮连破反馈和三组已有配合可读性已完成，详见[战斗体验改进路线图](../docs/engineering/combat-experience-roadmap-20261112.md)。下一步先对比试玩，再决定普通兵生命、局部出兵间隔或升级表现实验；这些数值候选尚未实施。

## 历史界面精修：UIRefinement-20261108

已按批准的界面评审完成精修，保留青绿沙盘、纸色卡片和暖金按钮。战斗页固定右上“开战”，精简收入信息，按需展开规则；阵位扩大有效热区，重叠处按屏幕最近阵位分配，选中候选更清楚。选将用独立描边标识正在查看的武将，加入阵容与成长阶数各有明确状态；结算默认展示六项统计和本次／个人最佳，详情可展开历史快照与突破复盘。失败仍显示已有最佳，且不写入纪录。

| 平台／资料 | 最新入口 | 使用说明 |
| --- | --- | --- |
| Windows | [YanyiSandpan.exe](Builds/UIRefinement-20261108/Windows/YanyiSandpan.exe) | 从此路径启动；传给他人时必须复制完整 Windows 文件夹。 |
| Android | [YanyiSandpan.apk](Builds/UIRefinement-20261108/Android/YanyiSandpan.apk) | Android 8.0及以上、ARM64；真实手机安装与运行待验收。 |
| 验收图 | [60张三尺寸截图及检查边界](../docs/ui/runtime-ui-refinement-20261108/README.md) | 包含空阵容、备战、缺钱、运行、规则、胜败与纪录状态。 |
| 实施计划 | [界面精修计划](../docs/engineering/ui-refinement-plan-20261108.md) | 包含实现范围、验证命令和交付路径。 |

本轮124项规则及五关自动模拟通过、内容校验通过，完整 Unity UI 验收退出0；新增60张截图覆盖1280×720、1920×1080和2400×1080，并抽看代表状态。Windows／Android均构建成功，构建前后407个源码及配置文件指纹一致。UI证据见 `Logs/ui-refinement-final.log`，新包独立验收记录见 [acceptance.json](Builds/UIRefinement-20261108/acceptance.json)。自动渲染、按钮回调与射线检查不代表手机触控、性能、实际听感、跨进程存档或正式五关人工试玩通过。

本轮不改变 Domain、数值、存档键、包名或原美术。下方 `Windows-Feedback`、`Builds/Android` 和根目录 `delivery/军师请下令-v2-20261108` 为保留的历史包。

## 既有玩法与上一轮反馈

已接入25将专属二、三阶成长、独立普攻与特色技能、1／2／3倍进军、实际通关计时及五卡两页军令台。上一轮按Windows实玩反馈补上“最近通关”、可见返回按钮与非终局致命命中闪亮，并将普攻间隔缩至原值70%，实际约0.43～0.80实秒，单击伤害、特色技能、Boss与经济不变。124项规则（含全关模拟）、118项纪录内存断言、4项成长内容、4项波次检查和完整Unity UI通过；五关严格正常经济参考均击退全部Boss。9段音乐与34个音效沿用，人工解压感、听感与手机验收仍待完成。

## 上一轮安装试玩（历史包）

2026-11-08：用户确认Windows已试玩，暂时没有手机，先整理交付。交付目录为仓库根目录下的 `delivery/军师请下令-v2-20261108/`，包含APK、完整Windows压缩包、去缓存Unity源码包、Word说明和校验清单。手机及五关、听感、跨进程存档的专项验收仍保留待完成。

Android安装包为 [Builds/Android/YanyiSandpan.apk](Builds/Android/YanyiSandpan.apk)，版本0.1.0，含完整音乐与音效。备战左上角“返回”可选择继续布阵、重新选将或返回关卡；战斗中返回先暂停，恢复后继续原会话。将APK传到Android 8.0及以上的64位手机，用文件管理器打开并按系统提示安装，再打开“军师请下令”。当前尚未完成真机安装与运行验收。

当前APK61,735,507字节（58.88MiB），SHA-256为 `f332f9a896178fff1e6e9187a215dca50488d51177214b585c43be31925d9298`，566项ZIP CRC及v2签名通过。对应源码证据见 `Builds/Android/apk-verification.json`；旧Boss容错包与成长版包保存在 `Builds/Android/History`。历史日志中的旧尺寸、测试数和成绩不能代表本版。

Android包名和存档键保持。Windows更名会改变PlayerPrefs路径，因此保留原公司名“演义沙盘课程项目”，首次启动时只读取旧路径中的游戏整数偏好，补齐新路径缺失项；不覆盖已有值或删除旧数据。隔离迁移检查及Windows实际编译通过，真实玩家偏好迁移尚未人工实测。

本轮电脑版打开 [Builds/Windows-Feedback/YanyiSandpan.exe](Builds/Windows-Feedback/YanyiSandpan.exe)，需要旁边的完整数据目录。原 `Builds/Windows` 保留用户正在运行的成长版；旧窗口不会自动加载本轮改动，退出后从新版路径启动即可沿用同一玩家存档。当前Windows构建与复制文件指纹已验证，界面使用Unity真实uGUI离屏按钮及三尺寸渲染验证；没有操控或关闭用户正在运行的窗口。详情见 `Logs/build-windows-feedback.log`、`Logs/ui-feedback-final.log` 和 `Builds/Windows-Feedback/build-verification.json`，不把自动检查当作五关人工试玩。

选关同时显示“本版最快”“最近通关”，有旧成长版成绩时另显示“旧版最快”。每次合法胜利都会保存最近成绩，只有严格更快才刷新本版最快；结算明确说明首次、刷新、相同或未刷新。数值调整使用 `growth-tempo-v2`，原 `growth-tempo-v1` 最快与通关／城防档保持，不推算以前未保存的最近成绩。

## 首版内容

- 25 名武将，战前选择 1～10 名；本关中场及最终 Boss 自动禁选。
- 每名武将每局最多招募一次；二、三阶改变转火、覆盖、控制、追击或协同等能力。军饷来自开局配置、计时收入和击退奖励。
- 5 张地图，共 178 个固定阵位、43 波敌军；普通敌军具有速度、护甲和控制抗性，Boss 具有预告及半血阶段。
- 主菜单、关卡情报、推荐阵容、战斗、暂停、重开、结算和本地通关记录。
- 1×／2×／3×进军只加快敌军移动、出兵、波间等待与计时军饷；武将普攻、技能与实际计时按有效实秒。备战、暂停及后台不计通关时间。
- 中文字体、人物与地图、攻击与连破反馈；9段配乐、34个音效及独立音量设置，保留关键短音缺失回退。

| 关卡 | 阵位 | 波次 |
|---|---:|---:|
| 虎牢合战 | 34 | 10 |
| 黄巾演武 | 32 | 8 |
| 官渡沙盘 | 36 | 9 |
| 赤壁两岸 | 36 | 8 |
| 群雄终局 | 40 | 8 |

## 第一次打开

1. 手动安装官方 Unity Hub。国际版3.22.5已下载到 `F:\Development\Unity\Downloads\UnityHubSetup-3.22.5-x64.exe`，官方SHA512与有效签名已验证；Hub可安装到 `F:\Development\Unity\Hub`。也可从 [Unity 官方下载页](https://unity.com/download)获取。安装编辑器前，先将 Hub 中的编辑器安装路径设在 F 盘，例如 `F:\Development\Unity\Editors`。
2. 登录 Unity 账号并按资格激活许可，安装 **6000.3.28f1**。勾选 **Android Build Support、Android SDK & NDK Tools、OpenJDK**。项目锁定文件为 `ProjectSettings/ProjectVersion.txt`。下载若仍跳转后报 404，先检查已有代理的系统代理设置及官方版本列表，避免混装其他版本的 Android 模块。
3. 在 Hub 中选择“添加已有项目”，选择本 README 所在的 **UnityGame 文件夹**，不要选择上一级课程文件夹。
4. 等待首次导入和脚本编译完成。工程会生成 `Assets/Scenes/Bootstrap.unity`；也可运行顶部菜单“军师请下令 → 准备工程”。
5. 运行“军师请下令 → 验证配置”，检查 Console 错误。打开 Bootstrap 场景，在 Game 窗口选 16:9 横屏，点击 Play。
6. 若打开的是已有编辑器配置，在“Project Settings → Player → Other Settings”中将 **Active Input Handling** 设为 **Input Manager (Old)** 或 **Both**，以支持本项目的触控事件模块。

首次导入会从 Unity 官方包源解析 uGUI。项目没有 npm、后端服务、密钥或额外 UI 框架。编辑器、模块和下载缓存优先放 F 盘；Unity Hub 的部分账号配置及系统临时文件仍可能保存在用户目录。

14个官方工具链文件已全部下载到F盘，共约6.83GiB；4项官方哈希、11个ZIP CRC及3个EXE签名检查通过。报告为下载目录中的`download-verification-6000.3.28f1.json`。Hub、6000.3.28f1及Android支持EXE已由用户手动安装，SDK、NDK及JDK等11个ZIP已提取到对应目录；Java17实际运行成功。若通过独立EXE手动安装Editor，Hub可Locate该版本，但[官方Add modules功能](https://docs.unity.com/en-us/hub/add-modules)仅支持由Hub安装的Editor。Android支持EXE不包含全部SDK、NDK、JDK子模块。

### 恢复工具链下载

本次官方包清单位于 `F:\Development\Unity\Downloads\download-manifest-6000.3.28f1.json`。若下载中断，可在仓库根目录用现有Python重新执行：

```powershell
& 'C:/Users/chentao/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -B tools/download_unity_packages.py --manifest 'F:/Development/Unity/Downloads/download-manifest-6000.3.28f1.json' --chunk-mib 8 --backend curl --workers 8
```

工具使用系统代理，默认4并发、每段8MiB；上述现场命令采用8并发及Windows原生curl传输，保留分段以便续传，合并后比对清单尺寸及已提供的官方哈希。SDK部分ZIP无官方哈希，只记录尺寸和Range检查。该工具不解压或执行安装器，不修改系统设置。curl每次尝试设90秒总时限，逐跳检查官方HTTPS地址，并限制最大响应体；每段最多尝试3次。备用urllib后端为60秒网络操作超时，连接及重定向可能累计超过该时限。实际16MiB请求曾被截断，因此缩小分段；旧16MiB缓存另行保留。

独立安装包的人工操作和Android目录表见[人工安装指引](../docs/engineering/manual-unity-install.md)。

## 操作方法

1. 从置顶的“黄巾演武 · 推荐首战”开始，阅读进军路线、敌将及技能情报。首次通关前，准备阶段与前三波有覆盖、升级和控制提示。
2. 使用推荐阵容或自行选将，最多10人。点人物卡查看详情，用卡片独立勾选加入或移出；禁选的Boss会显示原因。
3. 进入沙盘后，先点底部人物卡，再点空阵位招募；选将后自动显示参考覆盖，金色路段为技能最大覆盖，实际触发由技能决定。底部可翻页，点击已部署人物查看技能、真实攻击节奏、升阶收益及费用。开战前选中守将，再点空阵位可免费调位；开战后位置固定。
4. 准备妥当后点击“开始守关”。武将自动攻击，策略主要来自阵容、位置、招募顺序和升级时机。
5. 左上“返回”、暂停按钮或系统返回键打开对应退出／暂停菜单。进军可切1／2／3倍，攻速保持不变；切到后台后需手动继续。普通漏兵扣1～2城防，Boss扣15并离场；城防耗尽才失败，全部波次结束且城防仍在即可胜利。Boss突破不发击退赏金，结算保留真实突破记录。
6. 主菜单或暂停页点击“声音设置”，分别开启／关闭音乐与音效，按±每次调整10%；设置自动保存。默认音乐50%、音效80%。

推荐先试玩黄巾演武，再试双路线的虎牢合战。多路线地图需要分配守将，不能只守一条路线。

黄巾已有推荐、替代及提前升阶三组合法通关参考。开局可先布关羽与黄忠，再给黄忠升二阶；完整阵位与收支见 `Tests/README.md`。自动参考证明存在解法，仍需人工试玩评估乐趣与容错。

黄巾第4～8波主兵保留每波4个短批，批内0.55进军秒，敌军总量与赏金保持。本版1倍进军推荐／替代参考为371.7／349.3有效实秒，详见 `Tests/README.md`。短时间真实击退至少3名敌军出现“连破 ×N”，没有额外奖励或断连惩罚；群攻与方向表现边界见 `../docs/ui/runtime-impact/README.md`。

Boss突破仍扣15城防，总城防30，突破没有击退赏金。本轮五关严格全Boss击退参考和通用五关模拟都通关；这些自动参考不能替代人工难度验收。Boss容错历史验收及隔离夹具说明见 `../docs/ui/runtime-boss-breach/README.md`。

## 测试与构建

以下命令均从课程文件夹，也就是仓库根目录运行。

```powershell
# 无需 Unity，编译并执行纯 C# 规则测试
./UnityGame/Tests/Run-Tests.ps1

# 推荐阵容的全关自动布阵模拟
./UnityGame/Tests/Run-Tests.ps1 -Simulate

# 以下命令需要已经安装、激活的 Unity 编辑器
$env:UNITY_EDITOR_PATH = 'F:\Development\Unity\Editors\6000.3.28f1\Editor\Unity.exe'
./tools/Build-Game.ps1 -Target Verify
# 自动验证五关UI按钮流程并生成真实uGUI离屏渲染图；无需Computer Use
./tools/Build-Game.ps1 -Target Ui
# 素材与授权台账、音频服务和实际DSP输出验证
python -X utf8 tools/build_audio_content.py --verify-only
python -X utf8 docs/audio/test_audio_content.py
./tools/Build-Game.ps1 -Target Audio
./tools/Build-Game.ps1 -Target AudioPlayback
./tools/Build-Game.ps1 -Target AudioRegressions
./tools/Build-Game.ps1 -Target Windows
# Android工具要求纯英文工程路径；此目录为本次核验过的构建副本
./tools/Build-Game.ps1 -Target Android -ProjectDirectory 'F:\Development\Unity\BuildProjects\YanyiSandpan-6000.3.28f1'
```

可使用 `-UnityEditor '实际路径\Editor\Unity.exe'` 指定路径；已设置的 `UNITY_EDITOR_PATH` 优先。执行批处理构建前先关闭该项目的编辑器，避免工程锁冲突。

课程目录含中文，Android工具会直接拒绝。主工程保留在课程目录，将 `Assets`、`Packages`、`ProjectSettings` 同步到纯英文目录，再用 `-ProjectDirectory` 构建。**主工程修改后须核对构建副本，避免打包旧代码。** 本轮399个工程文件在构建前后核对SHA-256，详见 `Builds/Android/source-sync-verification.json`。指定工程目录后日志和产物也在该目录；APK复制回课程目录后再次核对哈希，Windows整目录复制到 `Builds/Windows-Feedback` 后逐文件核对。

| 输出 | 路径（相对 UnityGame） |
|---|---|
| 规则测试程序 | `Tests/bin/DomainTests.exe` |
| Unity 日志 | `Logs/build-verify.log`、`build-windows.log`、`build-android.log` |
| UI 自动检查 | `Logs/build-ui.log`；27张图片输出到 `../docs/ui/runtime-flow/`，此前18张保留于runtime-polished |
| 当前反馈修复构建日志及验收记录 | `Logs/build-android-feedback.log`、`build-windows-feedback.log`；`Builds/Android/apk-verification.json`、`Builds/Windows-Feedback/build-verification.json` |
| 音频逻辑与实际输出 | `Logs/build-audio.log`、`audio-playback/report.json`、`audio-playback-regressions/report.json`（后两者相对Logs） |
| 声音设置截图 | `../docs/ui/runtime-audio/`，6张PNG |
| 更名主菜单截图 | `../docs/ui/runtime-name/`，1920×1080、1280×720、2400×1080共3张PNG |
| 玩法入门及纠错截图 | `../docs/ui/runtime-gameplay/`，22张PNG：真实按钮调位、前三波教学、武将节奏与失败复盘；本轮回归日志`Logs/ui-boss-breach.log` |
| 布阵与击破表现 | `../docs/ui/runtime-impact/`，15张PNG；波次检查`../tools/test_wave_pacing.py`，规则`Logs/domain-impact-root.log`，最终UI `Logs/ui-impact.log` |
| Boss突破容错 | `../docs/ui/runtime-boss-breach/`，33张新PNG；完整UI `Logs/ui-boss-breach.log` 退出0，连同此前73张共106张运行时图 |
| 历史样板关APK日志 | `Logs/build-android-polished.log` |
| 特色成长界面 | `../docs/ui/runtime-growth/`，18场景×3尺寸；沿用v3视觉与v4源稿 |
| 最近通关与版本纪录 | `../docs/ui/runtime-recent-records/`，11状态×3尺寸；内存隔离夹具，不是跨进程PlayerPrefs验收 |
| 返回与末击反馈 | `../docs/ui/runtime-feedback/`，4状态×3尺寸；完整UI `Logs/ui-feedback-final.log` |
| Windows 试玩 | `Builds/Windows-Feedback/YanyiSandpan.exe`，须连同旁边数据目录一起分发 |
| Android APK | `Builds/Android/YanyiSandpan.apk` |

Android 配置为 API 26 起、ARM64、IL2CPP，适合常见的 64 位 Android 手机。课程本地演示采用 APK；使用 Unity 默认调试签名，正式发布前需单独配置签名。构建成功后再通过手机 USB 调试安装，例如 `adb install -r UnityGame/Builds/Android/YanyiSandpan.apk`。目前没有已连接设备，不能据此宣称手机运行通过。

自动模拟只检查推荐阵容是否能按程序策略走完配置。它不能证明手机手感、游戏乐趣、所有阵容都可胜或所有屏幕适配正常。

UI验证优先使用`-Target Ui`：检查五关178阵位、25人字形、真实按钮回调的选将/部署/差额/升级/满级/翻页，完成黄巾正常经济胜利与空阵失败、菜单返回、备战退出、暂停冻结、重开、下一关与隔离存档，并输出1920×1080、1280×720及2400×1080画面。它不发送桌面输入，不操作其他应用；音量与通关检查使用隔离存储。新增错误页与平台入口检查的具体边界见 `../docs/ui/runtime-flow/README.md`；音频自动检查和15段DSP采样边界见 `../docs/audio/runtime-acceptance.md`。离屏渲染和声音能量检查不等于手机触控、系统投递、人工听感或真机性能实测。

## 文件位置

| 目录 | 内容 |
|---|---|
| `Assets/Scripts/Domain` | 与 Unity 无关的配置、战斗规则和目录验证 |
| `Assets/Scripts/Presentation` | uGUI 页面、单位显示、动画及音效 |
| `Assets/Editor` | 工程初始化、纹理导入和构建入口 |
| `Assets/Resources/Game/database.json` | 人物、敌军、地图、阵位和波次配置 |
| `Assets/Resources/Game/Characters`、`Bosses` | 己方浅色台座、敌将红色台座 |
| `Assets/Resources/Game/Fonts` | Noto 中文字体及 OFL 许可 |
| `Assets/Resources/Audio` | 9段音乐、34个音效、Cue目录与原生混音器 |
| `../docs/audio` | 60秒试听、24位母版、源采样、CC0许可与逐文件台账 |
| `Tests` | 可独立执行的规则测试及自动模拟 |
| `../docs/ui` | 已认可的 v3 验收基准及 v4 精修源素材 |
| `../docs/engineering` | 接口、素材清单、人物与地图检视图、实施记录 |

运行时内容由 `../tools/build_game_content.py` 生成，需要现有 Python 和 Pillow；`--verify-only` 只核验资源、配置与清单。切勿将整个战斗概念图作为界面截图贴入游戏：当前按钮、文字、阵位和单位由运行时代码分别创建。

## 仍需完成的验收

| 优先级 | 项目 | 判定依据 |
|---|---|---|
| P1 | 手机运行界面与交互 | 共用流程与黄巾胜败结算已自动验证；手机触控仍需实测 |
| P1 | Android 真机安装及运行 | APK构建及验签已通过；仍需手机实际安装启动 |
| P1 | 五关人工试玩 | 分路、Boss 预告、难度与单局时长符合课程演示目标 |
| P1 | 循环与手机听测 | 实际导入后连续3次循环、满10将及低／中音量外放／耳机的关键预警辨识 |
| P2 | 不同屏幕与后台恢复 | 安全区无遮挡、小屏可读可点、后台暂停不丢进度 |
| P2 | 性能与听感 | 真实手机帧率、内存、发热和音量反馈记录 |

静态素材检视及纯 C# 测试与这些验收分别记录。

