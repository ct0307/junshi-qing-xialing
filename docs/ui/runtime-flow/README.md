# 完整流程离屏验收

在仓库根目录执行 `./tools/Build-Game.ps1 -Target Ui`，使用Unity 6000.3.28f1的真实uGUI与按钮回调，不使用Computer Use。

本轮27张PNG包括此前18张战斗验收场景，以及9张完整流程画面：

| 图片 | 内容 |
|---|---|
| flow-main.png | 主菜单 |
| flow-levels.png | 五关选择 |
| flow-intel.png | 黄巾敌将情报 |
| flow-roster.png | 25人选将与推荐阵容 |
| flow-preparation-exit.png | 离开布阵确认 |
| flow-pause.png | 平台暂停入口后的真实弹窗 |
| flow-defeat.png | 空部署自然失败结算 |
| flow-victory.png | 正常经济完整胜利结算 |
| flow-content-error.png | 内容错误页与重试按钮 |

真实流程检查由 `UnityGame/Assets/Editor/UiGameFlowChecks.cs` 执行，覆盖页面返回、空阵容禁入、10人上限、Boss禁选、推荐/清空、备战保留、暂停冻结与恢复、重开、自然胜败、下一关、末关结算组件、存档去重与最佳城防。截图前逐个检查当前非空Text字形与文本框高度。

GameProgress使用本次检查的内存字典，不写入玩家通关记录；不点击音效切换，主菜单会读取音效开关现值。错误页检查验证按钮回调和空流程返回保护，未制造真实资源损坏或验证重新加载成功恢复。平台检查直接调用YanyiGame公开入口，不代表实体设备投递过系统事件。末关无下一关分支为结算组件检查，不代表本轮UI自动流程完整打通终局。

安全区另由22项实际RectTransform四角检查验证。离屏图覆盖1920×1080、1280×720和2400×1080；手机触控、实际刘海区域、后台、性能与听感仍待真机。
