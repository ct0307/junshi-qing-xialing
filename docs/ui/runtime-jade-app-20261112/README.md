# 青绿行军全流程：Unity运行时验收

本轮将已认可的战斗顶栏风格延伸到主菜单、选关、情报、25将选择、暂停、结算、声音设置和错误页；真实文字、按钮、卡片选中／加入状态和结算记录保持可交互。

Unity `6000.3.28f1` 的App验收于UTC `2026-11-12T03:11:55.8909684Z` 开始，`2026-11-12T03:12:15.9219761Z` 完成，退出0。原始起止时间保留在 [verification.json](verification.json)，图片路径完整清单为 [verification.txt](verification.txt)，稳定日志见 [build-app.log](../../../UnityGame/Builds/JadeApp-20261112/Logs/build-app.log)。

| 尺寸 | PNG数量 |
| --- | ---: |
| 1280×720 | 76 |
| 1920×1080 | 100 |
| 2400×1080 | 71 |
| 合计 | 247 |

247张PNG逐张完整性、尺寸、清单及生成时间核对通过，全部生成于本轮Unity验收起止时间内。此数量包含原有流程与战斗回归，不是每种界面各拍三张；本目录为独立输出，旧历史图未覆盖。

自动检查覆盖配置、中文字体、五关人物边界与投影、阵位射线、规则暂停恢复、招募升级分页、黄巾真实胜败、完整导航与隔离存档、Boss突破继续／低城防失败，以及25将热区、结算纪录与多尺寸主题检查。

| 代表状态 | 图片入口 |
| --- | --- |
| 主菜单 | [flow-main.png](flow-main.png) |
| 关卡情报 | [flow-intel.png](flow-intel.png) |
| 选将焦点 | [roster-focus-1280.png](roster-focus-1280.png) |
| 空阵容 | [roster-empty-1280.png](roster-empty-1280.png) |
| 暂停 | [flow-pause.png](flow-pause.png) |
| 较慢胜利与历史详情 | [result-slower-details-1280.png](results/result-slower-details-1280.png) |
| 错误页 | [flow-content-error.png](flow-content-error.png) |

用户已试玩本轮Windows并反馈解压感不足；这不等于五关、听感、手机或跨进程存档专项通过。助手GUI操作检查因实体Esc中止。部分短路线／数秒结算截图来自隔离夹具，不能代表正式关卡时长和难度。手机安装、触控、安全区、帧率／发热以及实际听感仍待验证。

当前试玩包见 [JadeApp-20261112](../../../UnityGame/Builds/JadeApp-20261112/README.md)。
