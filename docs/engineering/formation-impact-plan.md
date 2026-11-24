# 布阵与击破体验实施计划

> 使用 subagent-driven-development 分工执行；先取得行为失败证据，再实现，统一由主任务串行执行Unity、独立审阅和产物核验。

**目标：** 布阵带来成片击破的清楚反馈，黄巾有张弛且至少两套阵容仍有合法通关解法。

**架构：** 波次由现有Python生成器分组排程；表现消费既有Domain真实事件，不改变伤害、经济和胜败规则。配置、表现各自保持独立所有权；主任务负责集成、审阅和交付。

**技术：** Unity 6000.3.28f1、C#、uGUI、现有Python；不增加依赖。无初始提交，不把用户资料提交或复制成工作树。

## 任务1：波内节奏与两套正常经济参考

文件：tools/build_game_content.py；新增tools/test_wave_pacing.py；UnityGame/Assets/Resources/Game/database.json；docs/engineering/game-content-manifest.json；必要时Tests/DomainTests.cs与Tests/README.md。

- [x] 保存旧配置测试快照，并先写失败检查：黄巾普通组出现更短的批内间隔和更长的批间空隙；每波按EnemyId／RouteId／HealthScale／SpeedScale汇总Count与旧版一致；Boss完整排程保持；其余四关完整相同。
- [x] 运行现有Python执行新测试，确认缺少成组排程而失败。
- [x] 仅拆分黄巾普通SpawnGroup。以`Delay + batchIndex * batchPeriod`和`Interval`表达批次；尾批保留剩余人数；将最后一兵出场时刻约束在原组范围内。通过`--data-only`生成配置及实际清单，不重绘素材。
- [x] 执行内容检查与76项既有规则；主推、替代阵容均通关并记录实际经济与节奏。只在必要时更新参考部署，不能削弱成功、军饷、招募唯一性或账目断言。

## 任务2：真实攻击轨迹与连破表现

文件：BattleEffects.cs；必要时新增同目录自定义Graphic；UiBattleFeedbackChecks.cs。主任务保留BattleView、UiBattleRenderChecks及新截图入口所有权。

- [x] 先补真实uGUI失败检查，观察扇形／贯穿几何、重复群攻合并、不同武将互不抑制，以及至少3个唯一kill才显示“连破 ×N”。重复ID、leak及普通hit不得计入。
- [x] 主任务以`./tools/Build-Game.ps1 -Target Verify`取得红日志后，再允许实现。
- [x] 实现有方向的吕布扇面与赵云长线轨迹；同次短窗口主轨迹合并，保留各目标命中与独立弹道。连破仅聚合真实kill事件，短时窗口、有界对象池，清空时重置；不增加战斗奖励。
- [x] 复验暂停、零时间推进、清空、到期、对象回池、重要提示及90／14容量上限；不改现有Domain伤害与音频源。

## 任务3：集成、画面与审查

文件：UiBattleRenderChecks.cs；新UiBattleImpactChecks.cs及.meta；必要时BattleView.cs；docs/ui/runtime-impact/。

- [x] 真实黄巾按钮部署与升级推进，保留前三波教学、调位及自然失败复盘，采集真实密集清场；另以隔离夹具展示吕布扇形、赵云穿透和连破边界。
- [x] 三尺寸截图前检查文字字形／裁切；抽查轨迹、道路、单位、预警和底栏可读性；记录局部反馈峰值，不能将其当手机帧率。
- [x] 需求审查后再进行质量审查；修复确定问题并复验。

## 任务4：重建与交付

- [x] 确认无项目编辑器或旧试玩进程占用；Unity串行构建Windows和英文副本Android，不能强制关用户应用。
- [x] 保留玩法入门版APK与验收JSON为历史；同步变化文件，构建前后逐文件SHA核对，无额外源码。
- [x] 记录真实APK尺寸／SHA256／ZIP CRC／包名／显示名／ARM64／v2签名，复制回主工程再次核对。Windows核对关键产物，并以窗口标题和启动日志确认可启动。
- [x] 更新README、契约、实施日志、当前JSON与规划文件；保留手机、人工听感和五关人工体验待验边界。

交付：Windows构建目录265,687,919字节（不含验收JSON），新版窗口已确认标题及句柄。Android 61,677,619字节，SHA256 `48d2f53bc9291e8bc98a96fd72f0db0cb5c43a54fdaef4c98102f74269483842`；383工程文件／360 Assets前后匹配，566项ZIP CRC与v2签名通过。最终Ui日志ui-impact.log、构建日志build-windows-impact.log／build-android-impact.log，均位于UnityGame/Logs；人工解压体验及手机验收继续待验。

验证命令：现有Python执行`tools/test_wave_pacing.py`与`tools/build_game_content.py --verify-only`；`./UnityGame/Tests/Run-Tests.ps1`；`./tools/Build-Game.ps1 -Target Verify/Ui/Windows`（分别调用）；Android使用`-Target Android -ProjectDirectory F:/Development/Unity/BuildProjects/YanyiSandpan-6000.3.28f1`。
