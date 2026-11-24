# 玩法入门、信息与布阵纠错实施计划

> 按 subagent-driven-development 分工执行，先写行为检查，再实现并由主任务完成独立审阅和真实Unity验收。

**目标：** 让玩家先从黄巾学会布阵，可靠地理解武将攻击节奏，失败后看见真实突破信息，并在开战前调整站位。

**已确认设计：** 用户在玩法评审后回复“好”，接受“黄巾新手流程 → 修正武将信息与失败复盘 → 开战前调位”的实施顺序。沿用现有布局、数值与每局一次招募规则；本轮不扩展主动技能或额外养成。

**技术：** Unity 6000.3.28f1、纯C# Domain、uGUI；不增加依赖。主工程仍在UnityGame，Android副本使用已核验的F盘英文路径。

## 接口与文件分工

| 任务 | 文件 | 接口及行为 |
|---|---|---|
| Domain规则及行为测试 | BattleSession.cs、CombatSystem.cs、RuntimeTypes.cs、Tests/DomainTests.cs | `TryMoveHero(int fromSlotId, int toSlotId)`仅准备阶段移动同一实例；`GetAttackTimingDescription(HeroDefinition)`来自真实冷却与蓄力规则；`LastBreach`记录最后突破者 |
| 菜单与结算展示 | MenuViews.cs、BattleResultView.cs | 读取`GameFlow.CampaignLevels`，黄巾标为推荐首战；显示统一攻击说明；用真实突破记录展示敌军、路线及剩余血量 |
| 战斗调位与入门提示 | BattleView.cs；必要时UnitView.cs | 备战点守将再点空位移动；选中、视图键、覆盖预览同步。黄巾未通关时在既有提示区分阶段教学，不强制暂停或替玩家操作 |
| 流程、真实uGUI验证及产物 | GameFlow.cs、Editor/UiGameFlowChecks.cs、Editor/UiBattleRenderChecks.cs、工程说明与验收JSON | 菜单及胜利“下一关”共用黄巾优先顺序；覆盖真实按钮、三尺寸画面与Windows／Android构建 |

`LastBreach`为新`BreachRecord`，字段固定为`EnemyName`、`RouteId`、`Health`、`MaxHealth`、`IsBoss`，只保留一次突破当时的值。初始为空，新局重新清空。

## 任务1：规则事实与准备阶段调位

- [x] 先补失败检查，覆盖原实例、等级、军饷、消费及已招募标记保持；占位、无效阵位、开战后／暂停／结算不能移动。
- [x] 实现移动并发送独立move事件，不重复招募。
- [x] 从实际战斗代码提取冷却／蓄力计算供说明复用。张飞至少4秒；吕布注明蓄力和出手后冷却；荀彧与吕蒙显示触发规则，不能伪装成普通攻速。
- [x] 通过自然战斗检查普通／Boss突破记录，事件被Drain后仍保留；新会话为空。
- [x] 运行`./UnityGame/Tests/Run-Tests.ps1`，76项通过，既有正常经济参考不变。

关键验收形态：

```csharp
HeroUnit original = session.Heroes[0];
int gold = session.Gold;
Equal(null, session.TryMoveHero(original.SlotId, emptySlotId));
Check(object.ReferenceEquals(original, session.Heroes[0]));
Equal(gold, session.Gold);
Check(session.IsRecruited(original.Definition.Id));
```

## 任务2：新手顺序、准确信息与真实复盘

- [x] 选关与下一关共用黄巾优先顺序，数据库、地图背景引用不重排；其它关依原顺序自由进入。
- [x] 武将详情使用统一攻击说明；基础威力不得标成实际单次伤害或DPS。
- [x] 失败页给出最后突破者、路线序号和剩余生命比例，建议以事实为依据；没有突破记录时维持安全回退。
- [x] 入门提示覆盖准备、第一波覆盖、第二波升级、第三波控制；通关后停止初学者提示。选择武将时仍可查看技能，不增加遮挡。

## 任务3：按钮、边界及画面验收

- [x] 用隔离GameProgress验证新手／已通关状态、黄巾→虎牢下一关、25人及Boss禁选保留。
- [x] 真实点击部署→移动→旧位可用→升级，验证已招募实例仅一个；战斗中不能移动。
- [x] 失败记录经过真实自然突破，渲染敌将名称、路线及血量；不注入虚假的复盘统计。
- [x] `./tools/Build-Game.ps1 -Target Ui`，输出`docs/ui/runtime-gameplay/`，检查1920×1080、1280×720、2400×1080及所有新文字无裁切。
- [x] 主任务依次做需求审阅、代码质量审阅，修正发现的问题后再构建。

## 任务4：构建与交付

- [x] Windows构建前确认旧试玩进程已关闭；如仍在运行，使用独立构建目录，避免强制关闭用户试玩。
- [x] 主工程Assets／Packages／ProjectSettings仅同步新增变化到英文副本，逐文件指纹核对，Unity串行构建。
- [x] 构建Windows与Android；APK核对大小、SHA256、ZIP CRC、包名、显示名、ARM64和v2签名。
- [x] 保留更名版APK与验收记录为历史包，更新README、运行时契约、当前产物记录及规划日志。
- [x] 明确Unity离屏检查与电脑启动、玩家实际试玩、手机触控／性能的证据边界。

最终证据：76项规则0失败；`Logs/ui-gameplay.log`退出0、22张玩法图（抽查）；两平台构建退出0。Android包61,669,575字节，SHA256 `0eed887d1feb23cdcd0b43a9de1c9386653c6acb1cf2f1c8f50a3484ab5aa7dc`，379个工程文件前后匹配，566项CRC与v2签名通过；Windows新版窗口已启动。日志路径相对UnityGame，人工五关试玩、听感、手机安装触控与性能仍待独立验收。

仓库当前无初始提交，不把用户现有资料纳入新提交；本轮不执行清理、重置、安装或依赖变更。
