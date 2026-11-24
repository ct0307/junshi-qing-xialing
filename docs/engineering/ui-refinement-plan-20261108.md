# 界面精修实施计划

采用subagent-driven-development执行，根任务统一运行Unity、构建、图像复核与集成。

**目标：** 落实已批准的UI评审，生成可复核画面及新的Windows／Android试玩包。

**架构：** Presentation保持规则坐标与存档契约。菜单、结算为独立文件所有者，根任务负责战斗与通用交互；编辑器检查只在UI验收链生成图片。

**技术：** Unity 6000.3.28f1、C#、uGUI、系统PowerShell；无新依赖。

## 任务与文件

1. 基线与失败证据
   - 保存本轮修改前源码到忽略的.tools/ui-refinement-before-20261108，核对已有进程不终止玩家窗口。
   - 新增UiRefinementChecks.cs及菜单／结算专项检查，以真实按钮与RectTransform断言当前缺口。先运行UiRefinementChecks.Run取得预期失败。
2. 战斗与热区（根任务）
   - 修改BattleView.cs、UiFactory.cs、必要的UnitView.cs；不改Domain。
   - `incomeText.text = "收入 +" + (level.IncomePerSecond * Session.Speed).ToString("0.##") + "/实秒";`
   - 固定右上开战，右下取消次级色；新增规则阅读弹窗及真实暂停／关闭恢复。
   - 阵位的交互背景高度抵消地面纵缩，视觉子层仍按原尺寸；关键小控件扩大热区，覆盖矩形射线与相邻间隙检查。
3. 菜单（独立代理）
   - 修改MenuViews.cs；新增UiRefinementMenuChecks.cs。
   - 查看用独立Outline更新，成长按钮按inspectedLevel更新颜色；加入状态仍由selected决定。
   - 加入按钮88×88，保持5×5网格与清晰文字；关卡有效地图预览和主菜单阴影在既有资源上调整。
4. 结算（独立代理）
   - 修改BattleResultView.cs；新增UiRefinementResultChecks.cs，按新展开行为调整必要的旧纪录检查。
   - `ResultButton(..., hasNext ? UiFactory.Panel : UiFactory.Gold)`用于重试；下一关胜利使用Gold。
   - 本局／记录状态保留默认可见，历史最佳和突破复盘由详情开关展示；真实回调不改变Session或记录。
5. 集成与渲染
   - 在UiBattleRenderChecks.Run接入专项检查；更新旧断言中有意改变的展示契约，规则断言不放宽。
   - `./UnityGame/Tests/Run-Tests.ps1 -Simulate`期望124通过、0失败。
   - 现有Python执行`tools/build_game_content.py --verify-only`及成长／波次保护，期望通过。
   - `./tools/Build-Game.ps1 -Target Ui`期望退出0；抽看三尺寸及状态图片，修复字形、裁切、热区遮挡。
6. 审查、构建与交付
   - 独立规格／质量审查，修复后对受影响检查复验。
   - 新建英文构建副本，同步Assets、Packages、ProjectSettings并逐项核对SHA256；Windows／Android串行构建。
   - 产物放UnityGame/Builds/UIRefinement-20261108/，记录新包与源码哈希；原包及delivery不覆盖。
   - 更新验收README、主工程说明与本轮规划状态，列明真机尚待验证。

## 状态
- [x] 修改前基线与红证据。
- [x] 战斗、热区、菜单、结算实现。
- [x] 完整回归、三尺寸代表截图视觉抽查与独立审查。
- [x] 新两平台包、指纹与验收说明。

## 实际交付与证据

- 修改前98份Presentation／Editor源码及meta保存在忽略的 `.tools/ui-refinement-before-20261108/`；战斗、菜单与结算专项先取得真实失败，再实现与回归。实际未修改UiFactory、Domain、配置或美术源稿。
- 124项规则、五关通用模拟及严格全Boss击退参考通过；内容校验确认25人、5关、43波、178阵位、73资源与历史美术未变，4项成长及4项波次检查通过。
- 完整Unity UI最终退出0，日志为 `UnityGame/Logs/ui-refinement-final.log`。新增20种状态×3尺寸，共60张PNG；根任务使用图像查看工具对代表状态视觉抽查，并非60张逐一检查或真实玩家试玩。独立复审的规则入口高度与失败历史最佳两项P2均关闭，无新增确定P1／P2。
- 在新英文副本构建Windows与Android，两者均退出0。407个源码／配置文件构建前后及最终重算均一致；Windows147个文件逐一复制核对，共265,739,055字节。
- 新APK61,741,959字节，SHA-256为 `775a22be6d83404fa72debcdf44c501c293c68a146d69e93f487f52c23714793`，566项ZIP CRC及v2验签通过，包名、应用名和ARM64符合既有契约。aapt使用已确认SHA相同的英文路径副本，输入与日志指纹已登记。
- 最新试玩入口和验收报告位于 [UIRefinement-20261108](../../UnityGame/Builds/UIRefinement-20261108/README.md)，逐项证据见 [acceptance.json](../../UnityGame/Builds/UIRefinement-20261108/acceptance.json)，截图与边界见 [界面验收说明](../ui/runtime-ui-refinement-20261108/README.md)。旧APK及旧Windows六项关键指纹仍匹配原记录，历史delivery保留。
- 待验收：新Windows包真实启动与玩家操作、手机安装触控与安全区、设备性能和听感、五关人工难度体验、真实玩家档跨进程持久化。
