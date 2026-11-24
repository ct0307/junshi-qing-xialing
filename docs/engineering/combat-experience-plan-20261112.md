# 战斗体验表现版实施计划

首轮实现、复审、验证与独立Windows交付已完成，见[验收记录](combat-experience-acceptance-20261112.md)。用户已授权实施；关联[设计](combat-feedback-design-20261112.md)。按任务拆解、测试先行和独立复审推进，不在任务间重复申请确认。

## 范围与验收

目标：不改变玩法轨迹，增加3/6/10连破分档和三组真实配合反馈，交付独立Windows试玩包。0.7秒为表现试验值；原Jade包保留0.4秒作试玩对照，自动计数同时比较两个窗口。音效先合并/限频，显式三档音高后置。

技术：Unity6000.3.28f1、C#5兼容Domain、uGUI，无外部依赖。工作区无提交，不创建空基线worktree或提交其他未跟踪文件。

## 任务1：保护与行为基准

- [x] 复制当前Assets/Scripts、Assets/Editor、Tests、Packages、ProjectSettings和工具源到本轮source-backup；不复制缓存或旧包。
- [x] 保留Jade和1192历史文件指纹，确认新目录不存在后使用。
- [x] 新增纯C# `UnityGame/Tests/CombatTrace.cs`：同一驱动编译旧/新Domain；A/B/C合法阵容在1/2/3倍率记录每个0.25秒状态、原有事件及逐次招募/升级消费；新语义事件忽略于玩法对比。
- [x] 先编译运行旧版，保存9条轨迹与击退发生时刻。

## 任务2：规则反馈契约

- [x] 先在Domain测试新增事件时间、协射来源、连环生命周期/真实传伤、快照独立性断言，观察旧版失败。
- [x] `RuntimeTypes.cs`给BattleEvent加OccurredAt、TriggerHeroId、SourceEnemyId及连环副本字段；`BattleSession.Emit`统一填时刻。
- [x] `CombatSystem.cs`仅在已有施法/传伤/协射处附加语义，不变更任何战斗判定；新`CombatFeedbackSnapshot.cs`提供区域/连环/敌军状态只读副本。
- [x] `Run-Tests.ps1 -Simulate`通过；用相同CombatTrace编译新Domain，与冻结版所有原事件/状态逐行相等。

## 任务3：连破与声音

- [x] 新`Presentation/BattleFeedbackState.cs`负责唯一ID、OccurredAt固定窗口、3/6/10跨档、批次最高档；`Clear()`清空全局击退集合。
- [x] 新纯C# `Tests/FeedbackStateTests.cs`与脚本验证2/3、5/6、9/10、0.7边界、不续时、重复ID、批处理、暂停时钟和0.4对照；先观察失败再实现。
- [x] `BattleEffects.cs`保留默认Emit兼容现有测试，运行时从BattleView共享聚合结果；连破字号29/33/37，局部冲击及一个复用标签；旧事件扣减寿命。
- [x] `GameAudio.cs`增加批次入口：同批最高档替代普通击退声、跨批0.2秒限频，沿用普通预算/关键声部和暂停终局门禁；不改AudioVoicePool。
- [x] 更新`UiBattleFeedbackChecks.cs`窗口边界与分档；音频加合并/去重/静音/终局验证。

## 任务4：真实配合表现

- [x] 新`Presentation/BattleSynergyEffects.cs`使用同一个BattleEffects预算池绘制区域/线，不另建无上限对象池。
- [x] 在BattleView同步快照；UnitView复用状态位，最多两状态，回收/暂停/终局无残留。
- [x] 周瑜圆区/路线火带及诸葛亮实际风眼边界；连环建立/补入/真实传伤线；荀彧真实来源与独立箭迹。
- [x] 新`Editor/UiCombatExperienceChecks.cs`覆盖三组真实规则夹具、3/6/10档、三尺寸、预警共存、生命周期和预算，所有夹具明确标注。

## 任务5：独立入口与交付

- [x] `UiBattleRenderChecks.RunExperience()`复用现有骨架，专项在枚举截图和写Passed之前执行，输出新目录。
- [x] `YanyiBuild.BuildWindowsExperience()`输出新Builds目录；`Build-Game.ps1`增加Experience、WindowsExperience映射，Experience保留图形环境。
- [x] 群攻夹具增加输出重载；旧入口保留，不执行会覆盖历史图的Run。
- [x] 执行规则/反馈测试、内容检查、Experience渲染、WindowsExperience构建；检查字形/裁切并抽看三尺寸代表截图。
- [x] 独立规格符合性和代码质量复审，处理P1/P2后复验相关范围。
- [x] 新包记录acceptance.json、windows-files.json、日志/源码指纹、旧证据保护核验；更新AGENTS、工程README和路线图的最新状态。

## 命令

```powershell
./UnityGame/Tests/Run-Tests.ps1 -Simulate
./UnityGame/Tests/Run-FeedbackTests.ps1
python -X utf8 tools/build_game_content.py --verify-only
./tools/Build-Game.ps1 -Target Experience
./tools/Build-Game.ps1 -Target WindowsExperience
```

以上独立入口已实现并执行通过。规则、UI、构建、Windows启动及人工乐趣分别记录；本轮不承诺手机或人工听感通过。
