# A「青绿行军」Unity 接入验收

用户已选择 A 方案。本目录是正式 Unity 6000.3.28f1 运行界面的离屏渲染与真实操作验收，不是浏览器提案截图。

## 已接入

| 优先级 | 内容 | 结果 |
| --- | --- | --- |
| P1 | 浅绢顶底、墨青信息、青玉主操作 | 已接入真实战斗页 |
| P1 | 五张将牌、独立状态签、选中细边与记号 | 招募、已出阵、差额和预支保留真实经济判定 |
| P1 | 浅玉成长区与完整战况 | 升级、满阶、Boss预警、阶段、余敌和入口均保留 |
| P1 | 地图与人物 | 原投影、阵位及等比人物保留，普通空位降至12%竹叶色 |
| P2 | 视觉收口 | 顶底铜线连续；两项视觉复审问题均已关闭 |
| P2 | 验收记录 | 每次运行先标记尚未通过，全部检查完成后才记录通过 |

为保留真实完整战况，顶栏为176画布像素，比网页提案的120画布像素略高；未为匹配网页稿缩小人物或改动规则坐标。规则、技能详情与暂停弹层沿用现有深色样式。

## 运行证据

- 规则基线：`./UnityGame/Tests/Run-Tests.ps1 -Simulate`，124通过、0失败，全关自动布阵五关获胜。
- 最终专项：`./tools/Build-Game.ps1 -Target Jade`，Unity退出0。
- 覆盖配置与字形、五关178阵位三阶人物边界和地图投影、真实射线及相邻阵位、招募升级分页、规则暂停恢复、黄巾完整合法经济胜利与空阵失败、完整导航与隔离存档、Boss突破与低城防。
- 输出118张截图，涵盖1280×720、1920×1080、2400×1080；交付脚本确认截图均来自最终这一轮运行。
- SHA256核对：534份既有UI资料、174份玩法及资源文件均未改变。
- 独立规格/视觉审查与代码质量审查均通过，无未关闭P1/P2。

完整记录见 [verification.json](verification.json)、[verification.txt](verification.txt)、[Unity日志](unity-jade.log)。复核交付可运行 `python docs/ui/runtime-jade-a-20261112/finalize_evidence.py`。

## 重点截图

| 状态 | 文件 |
| --- | --- |
| 空阵与操作引导 | [battle-empty-1280.png](battle-empty-1280.png) |
| 选将与阵位候选 | [battle-placement-1280.png](battle-placement-1280.png) |
| 军饷不足与升级差额 | [battle-unaffordable-1280.png](battle-unaffordable-1280.png) |
| 密集真实战斗 | [huangjin-density-1280.png](huangjin-density-1280.png) |
| 宽屏战斗 | [huangjin-density-2400.png](huangjin-density-2400.png) |
| Boss蓄势 | [huangjin-boss-warning.png](huangjin-boss-warning.png) |
| 规则阅读暂停 | [battle-rules-1280.png](battle-rules-1280.png) |

本轮完成UI接入，地图与人物沿用正式素材，没有重绘。未生成新的Android安装包，未进行手机触控、真机性能或发热测试；这些不包含在上述通过结论中。
