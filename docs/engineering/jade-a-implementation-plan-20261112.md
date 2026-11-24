# A「青绿行军」接入实施计划

> 执行方式：使用 subagent-driven-development 分工实现与独立审查；以现有真实规则和 Unity 验收为主。

**目标：** 将用户选择的 A 方案接入正式战斗页，并提供真实运行截图。

**架构：** 只调整 Presentation 与 Editor 验收代码。战斗专属色板隔离菜单和结算，保留地图坐标及会话操作。新增独立验收入口输出本轮目录，避免覆盖历史证据。

**技术栈：** Unity 6000.3.28f1、C#、uGUI、PowerShell；无需外部依赖。

**执行结果：** 已完成。最终 `-Target Jade` 退出0，118张截图；独立视觉与代码复审通过。证据见 `docs/ui/runtime-jade-a-20261112/README.md`。未进行Android/手机验证。

## 1. 基线与备份
- [x] 备份 BattleView.cs、BattleIntelView.cs、BattleLayout.cs、相关 Editor 检查及 Build-Game.ps1 到本轮规划目录。
- [x] 执行 `./UnityGame/Tests/Run-Tests.ps1 -Simulate`，确认规则与五关模拟基线。
- [x] 执行 `./tools/Build-Game.ps1 -Target Verify`，记录当前配置、字形、人物边界和射线检查。

## 2. 展示接入
- [x] BattleView.cs：浅绢顶底、墨青文字、青玉主操作；五张浅色将牌、独立状态签、选中细边、成长区。
- [x] 战斗专属色板：墨青183E37、青玉3A7861、竹叶8BA779、浅绢F4F0DD、暖铜B28B52、玉雾E0EBDE。
- [x] BattleIntelView.cs：所有动态分支适应浅底，保留敌型、入口、余敌、Boss 预警与阶段。
- [x] BattleLayout.cs：调整外框与浅色留白；保持 GroundScale=.67、GroundOffset=218 与地图 UV 投影。
- [x] 保留真实经济/预支判定、每将一次、五卡分页、升级/满级、教程、规则暂停及恢复；保留已有控件名和关键操作标签。

## 3. 独立验收
- [x] UiRefinementChecks.cs：为截图输出增加可恢复的目录参数，更新 A 视觉断言，保留全部行为断言。
- [x] UiBattleRenderChecks.cs：新增公开 A 验收方法，复用参数化五关、黄巾真实胜败、流程检查。
- [x] Build-Game.ps1：新增有图形的 A 验收目标，使用现有 Unity 路径与环境处理。
- [x] 执行 Unity Verify 与 A 验收，检查 1280×720、1920×1080、2400×1080 截图、五关热区、字形与人物边界。

## 4. 审查与交付
- [x] 独立检查设计符合度，再审查实现质量；发现问题修复后复验。
- [x] 重读本轮文档并检查编码，记录运行命令、退出状态和截图文件。
- [x] 交付真实 Unity 截图，明确 Android/真机验证是否执行；不把网页提案截图作为 Unity 证据。
