# 战斗体验表现版 · 本轮运行时画面

使用Unity6000.3.28f1真实uGUI离屏渲染，`Experience`完整退出0。301张PNG均在本次通过的运行时间内写入，清单与PNG完整性已核对；旧Jade图保留。

| 尺寸 | 数量 |
| --- | ---: |
| 1280×720 | 94 |
| 1920×1080 | 118 |
| 2400×1080 | 89 |

本轮包含全流程菜单／正常战斗／空阵失败／错误页／暂停／胜败／安全区与五关招募升级回归；`experience/`的42张为三组配合、连破分档、Boss预警与终局隔离夹具；`impact/`的12张为真实按钮招募后的群攻击退夹具。隔离夹具的高军饷、敌军参数和短路线只服务展示契约，不表示正式关卡强度或触发概率。

| 代表场景 | 画面 |
| --- | --- |
| 三阶风眼和道路火带，小屏 | [1280](experience/fixture-wind-fire-stage3-1280.png) |
| 连环建立和真实传伤 | [1920](experience/fixture-link-created-hit-1920.png) |
| 连环补入，小屏 | [1280](experience/fixture-link-replaced-1280.png) |
| 荀彧来源与双箭，宽屏 | [2400](experience/fixture-assist-stage3-two-arrows-2400.png) |
| 10档真实击退夹具 | [1920](experience/fixture-burst-10-1920.png) |
| 预算压力下Boss预警 | [1280](experience/fixture-boss-warning-with-fields-1280.png) |
| 败局状态清理 | [1920](experience/fixture-defeat-clear-status-1920.png) |

自动检查覆盖字形、文本裁切、实际圆半径／道路折点、来源与真实目标、原到期、共享90特效／14数字预算、暂停、旧事件与终局清理。主代理抽看上表三种尺寸代表图，确认边界与连线可见、10档文字无裁切、预警条保留；无法从静态截图证明人工听感或操作手感。

原始结果：[verification.json](verification.json)、[文字清单](verification.txt)、[带哈希的截图清单](screenshot-files.json)、[夹具清单](experience/experience-fixtures.json)。夹具JSON仅保存场景名称；事件与快照在运行时断言，未导出明细。试玩与边界见[交付说明](../../../UnityGame/Builds/CombatExperience-20261112/README.md)。
