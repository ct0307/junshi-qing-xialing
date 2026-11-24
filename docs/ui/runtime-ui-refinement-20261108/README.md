# 界面精修：真实 UI 验收图

日期：2026-11-08。已核对本目录实际文件，共 **20 种状态 × 3 个尺寸 = 60 张 PNG**。截图来自运行时 uGUI 离屏渲染；交互检查调用真实按钮，并使用真实 `GraphicRaycaster` 验证热区。

本轮最终完整日志稳定留档为 [ui-refinement-final.log](../../../UnityGame/Logs/ui-refinement-final.log)，原始入口日志为 [build-ui.log](../../../UnityGame/Logs/build-ui.log)。日志包含战斗、菜单、结算精修专项通过，以及完整 UI 自动渲染通过；末尾明确为 `return code 0`。Unity 版本为 `6000.3.28f1`。Windows 与 Android 新包均已构建成功，签名、复制哈希、源码及截图核验结果已写入 [acceptance.json](../../../UnityGame/Builds/UIRefinement-20261108/acceptance.json)，`AllChecksPassed`为`true`。

## 尺寸与截图清单

| 文件后缀 | 实际像素 | 数量 |
| --- | --- | ---: |
| `-1280.png` | 1280 × 720 | 20 |
| `-1920.png` | 1920 × 1080 | 20 |
| `-2400.png` | 2400 × 1080 | 20 |

截图共用 1920 × 1080 逻辑画布，按输出尺寸调整相机与渲染目标。每张截图生成前检查可见文字字形和文本框高度；选将正文另检查单行宽度、卡片边界及文字矩形互斥。宽屏输出用于复核画布居中与两侧留白。

| 状态 | 画面内容 | 1280 | 1920 | 2400 |
| --- | --- | --- | --- | --- |
| `main` | 主菜单、既有人物与台座接地阴影 | [图](main-1280.png) | [图](main-1920.png) | [图](main-2400.png) |
| `levels` | 选关地图预览、本版最快与最近通关记录行 | [图](levels-1280.png) | [图](levels-1920.png) | [图](levels-2400.png) |
| `roster-empty` | 空阵容、独立加入按钮与默认成长阶数 | [图](roster-empty-1280.png) | [图](roster-empty-1920.png) | [图](roster-empty-2400.png) |
| `roster-focus` | 推荐阵容、赵云查看描边与二阶激活 | [图](roster-focus-1280.png) | [图](roster-focus-1920.png) | [图](roster-focus-2400.png) |
| `battle-empty` | 未部署状态、固定右上开战与右下操作提示 | [图](battle-empty-1280.png) | [图](battle-empty-1920.png) | [图](battle-empty-2400.png) |
| `battle-placement` | 待部署、阵位候选与次级取消选择 | [图](battle-placement-1280.png) | [图](battle-placement-1920.png) | [图](battle-placement-2400.png) |
| `battle-deployed` | 已部署守将、成长区与固定开战 | [图](battle-deployed-1280.png) | [图](battle-deployed-1920.png) | [图](battle-deployed-2400.png) |
| `battle-unaffordable` | 军饷不足时的升级状态 | [图](battle-unaffordable-1280.png) | [图](battle-unaffordable-1920.png) | [图](battle-unaffordable-2400.png) |
| `battle-running` | 运行中信息层次、实际用时与主操作 | [图](battle-running-1280.png) | [图](battle-running-1920.png) | [图](battle-running-2400.png) |
| `battle-rules` | 运行中打开规则，真实会话已暂停 | [图](battle-rules-1280.png) | [图](battle-rules-1920.png) | [图](battle-rules-2400.png) |
| `result-first-default` | 首次胜利、当前成绩与收起的详情 | [图](result-first-default-1280.png) | [图](result-first-default-1920.png) | [图](result-first-default-2400.png) |
| `result-first-details` | 首次胜利详情、最佳快照与突破复盘 | [图](result-first-details-1280.png) | [图](result-first-details-1920.png) | [图](result-first-details-2400.png) |
| `result-slower-default` | 较慢胜利、不刷新最佳并保存最近通关 | [图](result-slower-default-1280.png) | [图](result-slower-default-1920.png) | [图](result-slower-default-2400.png) |
| `result-slower-details` | 较慢局详情与保留的历史最佳快照 | [图](result-slower-details-1280.png) | [图](result-slower-details-1920.png) | [图](result-slower-details-2400.png) |
| `result-equal-default` | 相同用时、不刷新最佳并保存最近通关 | [图](result-equal-default-1280.png) | [图](result-equal-default-1920.png) | [图](result-equal-default-2400.png) |
| `result-equal-details` | 相同用时局的完整详情 | [图](result-equal-details-1280.png) | [图](result-equal-details-1920.png) | [图](result-equal-details-2400.png) |
| `result-improved-default` | 更快胜利、刷新差额与下一关主按钮 | [图](result-improved-default-1280.png) | [图](result-improved-default-1920.png) | [图](result-improved-default-2400.png) |
| `result-improved-details` | 刷新后的完整最佳快照与突破复盘 | [图](result-improved-details-1280.png) | [图](result-improved-details-1920.png) | [图](result-improved-details-2400.png) |
| `result-defeat-default` | 失败、重新推演主按钮与已有最佳用时 | [图](result-defeat-default-1280.png) | [图](result-defeat-default-1920.png) | [图](result-defeat-default-2400.png) |
| `result-defeat-details` | 失败详情仍可读取历史最佳快照 | [图](result-defeat-details-1280.png) | [图](result-defeat-details-1920.png) | [图](result-defeat-details-2400.png) |

满级、详情暂停生命周期、真实胜败导航等还由完整 UI 链的既有检查覆盖，其截图保存在其他运行时目录，未重复计入本目录的 60 张。

根任务已使用图像查看工具，对下列三尺寸代表状态做视觉抽查；另查看了既有 `runtime-growth/upgrade-tier-3-1920.png` 的满阶画面。该记录不代表60图逐张审阅，也不是玩家操作验收。

| 尺寸 | 已目视抽查的本目录状态 |
| --- | --- |
| 1280×720 | `main`、`levels`、`roster-focus`、`battle-rules`、`battle-deployed`、`battle-unaffordable`、`result-improved-default`、`result-defeat-details` |
| 1920×1080 | `battle-running`、`roster-focus` |
| 2400×1080 | `battle-deployed`、`result-improved-default`、`result-defeat-details` |

## 实际验证覆盖

| 范围 | 已取得的检查证据 |
| --- | --- |
| 战斗操作 | 备战时右上金色开战保持可用；选将、取消与部署不替换入口；点击后真实进入运行，再点击真实暂停。收入按当前进军倍率换算为实秒，基础口径移入规则。 |
| 规则阅读 | 备战阅读不开战；运行中打开规则暂停同一会话、冻结实际计时；返回关闭后恢复原运行会话。完整链还覆盖详情期间失焦、重开及销毁的生命周期。 |
| 热区与射线 | 五关 178 处阵位的矩形中心和四个内部角点，验证地面纵缩后的有效高度；重叠点按屏幕最近阵位分配，同距按 ID；沙盘外裁剪区不接受阵位输入。详情、规则与主操作的真实中心射线不被遮挡。 |
| 选将 | 查看描边与入队底色／勾选独立；查看、加入、移出和清空不串状态；1／2／3 阶激活色与实际成长说明一致。25 张卡保留 5×5 排列，加入区至少 88×88，五点射线命中实际按钮；文字不重叠、不越界，相邻卡间隙与卡片查看区不被加入按钮侵占。 |
| 候选与成长 | 切换阵位候选会复位旧高亮；当前与下一阶相同射程不重复叠环，成长说明仍读取真实规则。完整链验证费用、缺钱、满级、五卡翻页与三阶说明。 |
| 结算按钮 | 有下一关的胜利以“下一关”为金色主操作；失败以“重新推演”为主操作。详情真实按钮可展开／收起，保留同一会话及六项真实统计。 |
| 纪录 | 覆盖首次、较慢、同用时、更快、失败；严格更快才替换本版最佳，较慢／同用时胜利仍保存最近通关；旧版最快独立保留。失败展示已有最佳用时与完整快照，不写新纪录。 |
| 存储隔离 | 本轮专项通过字典回调注入 `GameProgress`，结算夹具使用内存 `MemoryStorage`。断言详情开合不改任意存储键、不增加 write／save 次数；重建流程仍从同一内存读回记录，重复终局与旧会话不误写。 |

最终日志同时记录：最快纪录内存 **118 项断言**通过，最近通关真实流程、五关基础 UI、黄巾正式配置的合法经济胜利及空阵失败、中文字形、安全区算法、导航与音频设置等回归通过。安全区算法和离屏射线均属于自动检查，不能据此认定手机体验通过。

## 独立复审闭环

独立规格／质量复审提出的两项 P2 均已关闭，修复先有真实失败证据，随后纳入最终全链。

| 级别 | 发现与修复 | 证据 |
| --- | --- | --- |
| P2，已关闭 | 规则入口高度不足；现为 365×88，并检查实际尺寸和真实射线。 | [规则热区红灯](../../../UnityGame/Logs/ui-refinement-review-red.log)；最终日志“规则阅读与返回恢复”“热区与相邻阵位射线”通过。 |
| P2，已关闭 | 失败结算未读取已有最佳；现通过只读历史最佳参数后备，默认可见用时，展开可读完整快照，失败不写档。 | [历史最佳红灯](../../../UnityGame/Logs/ui-refinement-history-red.log)；最终日志结算专项与最近通关真实流程通过。 |

独立复审未发现新的确定 P1／P2；这不替代尚未执行的人工试玩与真机验收。

## 数据与验收边界

主菜单、选关、选将与本轮战斗操作图读取正式数据库。`result-*` 来自数据库深复制后的“结算精修验收”副本：城防 30、计时收入为 0、路线仅 10 单位、出兵等待 6 秒，使用单个扣 15 城防的正式 Boss；失败场景改为两个 Boss。以 2／1／2／3 倍进军依次产生首次、较慢、相同及更快的真实终局。

这些短路线结算图验证真实会话、统计与纪录展示，数秒成绩不代表正式路线、波次、经济、时长或关卡难度。夹具仅改内存副本，不回写正式配置。本轮没有改变 Domain、25 人规则、成长／军饷／存档契约或原美术。

本轮专项没有使用真实玩家 `PlayerPrefs`。同进程内重建 `GameFlow`、复制内存后重读及 write／save 计数检查，均不证明磁盘落盘、退出重启、跨进程或跨设备存档。

| 优先级 | 尚待验证 | 所需证据 |
| --- | --- | --- |
| P1 | 新包实际启动与玩家操作 | 新Windows包真实启动、鼠标／键盘操作，以及正式关卡的玩家操作记录；本轮未做新包人工启动或人工操作。 |
| P1 | 手机安装、真实触控与安全区 | 实机安装启动；选将查看／加入、阵位相邻命中、规则阅读、系统返回与后台恢复。 |
| P1 | 人工试玩与难度体验 | 正式五关的布阵、升级取舍、1／2／3 倍节奏、失败后重试及连续游玩记录。 |
| P2 | 实机性能与音频 | 帧率、内存、发热、长时运行及手机实际听测。 |
| P2 | 真实持久化 | 实际胜利后退出进程并重启，核对最近、本版最快、旧版最快及设置。 |

## 最终构建与产物核验

新产物位于独立的 [UIRefinement-20261108](../../../UnityGame/Builds/UIRefinement-20261108/) 目录，历史包及交付目录保留。

| 平台 | 当前证据 |
| --- | --- |
| Windows | [构建日志](../../../UnityGame/Logs/build-windows-ui-refinement.log)退出0；已复制147个文件，共265739055字节，全部复制文件SHA256匹配；分发与试玩须保留完整Windows目录。 |
| Android | [构建日志](../../../UnityGame/Logs/build-android-ui-refinement.log)退出0；`YanyiSandpan.apk`为61741959字节，复制SHA256匹配；566个ZIP条目CRC全部通过，APK v2签名通过。 |
| APK SHA256 | `775a22be6d83404fa72debcdf44c501c293c68a146d69e93f487f52c23714793` |
| 源码一致性 | [source-verification.json](../../../UnityGame/Builds/UIRefinement-20261108/source-verification.json)记录407个源文件，构建前后均匹配；最终验收另重算407文件并确认一致。 |
| 最终验收 | [acceptance.json](../../../UnityGame/Builds/UIRefinement-20261108/acceptance.json)的`AllChecksPassed`为`true`，包含构建、复制哈希、ZIP／签名、源码和60张完整三尺寸PNG证据。 |

以上构建及产物核验已完成。`acceptance.json`同时保留`PlayerLaunchVerified=false`及手机／性能／听测／跨进程存档等未验证标记；构建成功不等于新包实际启动、玩家操作、手机安装或人工试玩完成。

## 复核入口

从仓库根目录使用已安装并激活的 Unity 编辑器运行：

```powershell
./tools/Build-Game.ps1 -Target Ui
./UnityGame/Tests/Run-Tests.ps1 -Simulate
python tools/build_game_content.py --verify-only
```

UI 入口会重新生成本目录及其他运行时验收图；本轮证据以稳定留档 `ui-refinement-final.log` 为准。规则／模拟日志为 [domain-ui-refinement.log](../../../UnityGame/Logs/domain-ui-refinement.log)，内容日志为 [content-ui-refinement.log](../../../UnityGame/Logs/content-ui-refinement.log)。

检查实现为 [UiRefinementChecks.cs](../../../UnityGame/Assets/Editor/UiRefinementChecks.cs)、[菜单专项](../../../UnityGame/Assets/Editor/UiRefinementMenuChecks.cs)、[结算专项](../../../UnityGame/Assets/Editor/UiRefinementResultChecks.cs)，由 [UiBattleRenderChecks.Run](../../../UnityGame/Assets/Editor/UiBattleRenderChecks.cs) 集成执行。
