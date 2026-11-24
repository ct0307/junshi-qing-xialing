# 战斗体验专项实施与验收

本轮按已认可的战斗体验设计v3实施，黄巾作为首批样板。真人解压感、听感和真机触控/性能仍需试玩确认；自动化结果只证明对应机制、流程和构建。

| 优先级 | 实际交付 | 验收依据 |
| --- | --- | --- |
| P1 | 顶栏与暂停菜单1×/2×/3×直接选择；2→1一步完成，当前档幂等 | 六方向时片/收入测试，首次1→3、系统中断、保存失败、真实按钮射线 |
| P1 | 吕布收束蓄力与一次主斩；周瑜一次点火与持续火带；诸葛施法与真实风眼 | 主效果来自skill_cast，多目标不重播主效果/主声；一阶不虚构风眼 |
| P1 | 黄巾32槽改为30槽，内外分带重排，合并两个旧位；道路覆盖与火/风形状预览 | 2160机制单元，39局正常资源策略，空场/十将三尺寸180次阵位中心射线 |
| P2 | 黄巾第4波12→16人，四批各四人，间隔仍0.55进军秒，首末0/17.6不变 | 8份独立候选、30局公共节点实验、逐步账本对账，其他波/关/敌军/武将未改 |
| P2 | 三将有界开发诊断，默认关闭，真实生命差与原施放/区域来源 | 专项测试、开关行为对照、容量/截断与终局导出 |
| P1 | 普通最快/最近与竞速均按新关卡规则隔离 | 新旧存档并存回归；选关/情报/暂停/结算使用真实版本 |

## 参数取舍与实际实验

旧地图第4波，吕布一阶旧#4与黄忠一阶旧#27，正常开局推进至第3波清空，再选择实验倍率；本波不招募、不升阶。1倍原版吕布每次真实施放唯一命中均值1.6；T（0.40间隔）仍1.6；N（16人、0.55间隔）为2.4，敌群峰值9→12，击退12→16，均无漏兵。N本波击退赏金84→112，不能称经济不变。

最终选MN：30槽完整布局＋16人，保留0.55间隔。新地图的黄忠对应选择#25（1210,590），避免错误地沿用已经迁位的旧#27编号作几何对照。MN在该1倍局部用例同样为均值2.4、峰值12、16击退且无漏兵；3倍漏兵增多，是必须应对的压力。

周瑜/诸葛站位对照保留正常开局，诸葛此前不招募，在第3波清空相同固定步再正常招募与双二阶升级。MN两局共同节点126.7秒、军饷681；周瑜#5，诸葛分别#4/#6。第4波清空用时35.067/34.700秒，周瑜区域与余烬实际扣血742.256/1051.183，其中已有诸葛有效迟滞的目标扣血531.545/583.250。数值只描述该完整阵容与站位，不推导专属伤害加成，不把状态共存播成“联合击杀”，不宣称上游必优。

## 阵位与策略验证边界

机制矩阵为黄巾合法24将×3阶×30槽，共2160单元，读取实际成长、最大射界、道路相交长度、真实出手、友军覆盖与220孤阵关系。矩阵为机制夹具，使用独立资源额度，不冒充正常经济策略。

39局正常资源策略单列：三套合法阵容各1/2/3倍，共9局；30槽分别由赵云正常首发，等待第一批进入射界后再正常补防，共30局。所有30槽均实际出手且最终全Boss击退；后段用途为截击，不能据此断言与前段收益相同或常规满编时必有目标。三套阵容1/2倍均合格，固定3倍均失败。五关竞速原策略回归80局、69合格，5/5关找到动态更快且复跑一致的参考。

## UI、诊断与存档

- 完整Unity入口Sensation通过591项竞速/交互断言、78张流程截图，另12张正常经济技能/十将实景；补充180次空场/十将阵位射线。截图1280×720、1920×1080、2400×1080；新射线另含2340×1080不对称安全区，不能当作手机dp实测。
- 90个普通效果及90个重要区域的满池测试均实际执行；真实区域优先于普通反馈，Boss危险优先于区域。90效果/14普通数字/11音源/普通音16次每实秒预算保留。主声由真实序列尝试一次，无全屏白闪、强制慢放或停顿。
- 诊断最多16384条值类型记录；结构实测72字节，含一次完整导出约2.36MiB。超限明确Truncated/DroppedCount，实验明细截断即拒绝验收。关闭时不新增逐击日志对象/字符串/IO；Player分配量与设备性能未实测。
- 黄巾战斗版本为sensation-hj-mn-v1-army；竞速资格继续all-boss-v1，其他关仍沿用既有版本。旧普通最快、最近及旧竞速不迁移、不覆盖；通关和城防成就保留兼容。

## 复现与产物

最终回归为138项基础规则、22组军势与40局、6组反馈、16组竞速领域、22组存档及118项旧断言，均通过。诊断专项15组通过，包含36个开关行为对照场景，覆盖三将各阶、2/3倍、六向切换、变步长及暂停恢复；事件、状态和时钟逐步一致。Python共14项测试及73份内容资源验证通过。

Unity UI验收及Windows、Android最终构建均退出0。APK大小72,104,098字节，590条ZIP内容CRC通过，apksigner验签通过且v2=true。根工程及Android ASCII副本的482份构建输入SHA256一致，原509份历史包与截图指纹保持。包内149份文件指纹记录于package-verification.json。Windows Player可见操作、真机与真实PlayerPrefs跨进程未实测。

```powershell
./UnityGame/Tests/Run-Tests.ps1
./UnityGame/Tests/Run-FactionTests.ps1
./UnityGame/Tests/Run-FeedbackTests.ps1
./UnityGame/Tests/Run-RaceTests.ps1
./UnityGame/Tests/Run-CombatDiagnosticsTests.ps1
./UnityGame/Tests/Run-SensationExperiments.ps1
./UnityGame/Tests/Run-SensationFormation.ps1
./UnityGame/Tests/Run-RaceSimulation.ps1 -Output .planning/combat-sensation-implementation-20261119/race-simulation-results.json
./tools/Build-Game.ps1 -Target Sensation
./tools/Build-Game.ps1 -Target WindowsSensation
./tools/Build-Game.ps1 -Target AndroidSensation -ProjectDirectory F:/Development/Unity/BuildProjects/Sensation-20261119
```

Python使用本机已有运行时运行tools/test_sensation_experiments.py、test_sensation_content.py、test_wave_pacing.py、test_hero_growth_content.py及build_game_content.py --verify-only，无新增依赖。正式专项再生入口为--huangjin-sensation，只更新配置、黄巾地图草丛避让和内容清单。

源码备份、TDD红绿日志、独立复核结论、实验/矩阵、构建指纹位于[本轮实施记录](../../.planning/combat-sensation-implementation-20261119/task_plan.md)。UI结果见[流程报告](../ui/runtime-sensation-20261119/verification.json)及[补充报告](../ui/runtime-sensation-20261119/sensation-verification.json)。最新构建及最终指纹状态以[交付说明](../../UnityGame/Builds/Sensation-20261119/README.md)和本轮package-verification.json为准。
