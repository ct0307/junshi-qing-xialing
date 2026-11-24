# Windows实玩反馈：返回与命中表现

本目录为Unity真实uGUI离屏渲染，共4种状态、每种1280×720／1920×1080／2400×1080，共12张PNG。入口为 `UiFeedbackChecks.Verify`，由完整 `-Target Ui` 调用；最新日志 `UnityGame/Logs/ui-feedback-final.log` 退出0。

| 文件前缀 | 验证内容 |
| --- | --- |
| preparation-visible-back | 备战可见返回按钮，真实按钮部署一名武将后截图 |
| preparation-exit | 点击返回显示退出确认，继续时保留同一守将、等级、军饷及支出 |
| running-back-paused | 运行时返回打开暂停菜单，通关时间冻结，继续后恢复原会话 |
| lethal-hit-flash-fixture | 隔离短路线由真实按钮招募并自然击退目标，致命命中先亮起再淡出 |

返回沿用GameFlow.HandleBack，覆盖继续布阵、重新选将、返回关卡及运行暂停。命中夹具保留远处敌军以防提前终局，检查受击、淡出、暂停冻结及结束回池；终局仍立即清理反馈，因此不代表最后一名敌人的闪亮也会在结算之上显示。夹具敌军生命、路线仅用于重现表现问题，不代表正式关卡平衡。

检查全部使用内存存档，不写玩家偏好，不发送桌面输入。两项实际问题均先取得失败证据，再修复通过：`ui-feedback-back-red.log`、`ui-feedback-hit-red.log` → `ui-feedback-targeted.log`。截图会检查字形及文字裁切，并由根抽查1280布局。没有完成手机触控、真实窗口人工操作或主观解压感验收。
