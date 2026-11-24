# 最近通关与分版最快纪录验收

日期：2026-11-06。本目录包含 **11 种状态 × 3 个尺寸 = 33 张 PNG**，由真实运行时 uGUI 离屏渲染生成。交互检查通过真实开战、结算返回关卡按钮推进流程；记录存储全部使用隔离内存字典。

完整日志：[ui-feedback-final.log](../../../UnityGame/Logs/ui-feedback-final.log)。日志包含“最快纪录内存检查通过：118项断言”“最近通关真实流程检查通过”和完整 UI 自动渲染通过记录，末尾为 `return code 0`。Unity 版本为 `6000.3.28f1`。

## 纪录口径

| 字段 | 保存与显示规则 |
| --- | --- |
| 本版最快 | 当前规则 `growth-tempo-v2` 下的最短合法胜利用时。严格更快才替换；相同或较慢成绩保留原有完整快照。 |
| 最近通关 | 当前规则下最近一次合法胜利。较慢或相同成绩也更新，用时、进军倍率、城防、漏兵及 Boss 突破数来自同一局。 |
| 旧版最快 | 独立读取 `growth-tempo-v1`，在有旧纪录时显示“旧版最快”。不迁为 v2 最快，也不据此推算最近通关。 |
| 通关与最佳城防 | 沿用原有 `yanyi.clear.*` 与 `yanyi.best.*`；不因规则版本升级清空，最佳城防与最快局快照分别维护。 |

v2 对应本轮普攻间隔调整后的规则。不同版本的最快成绩分开显示，避免新旧规则混比。时间为有效战斗实秒，显示到整数毫秒；备战与暂停不计入，进军 1／2／3 倍的实际使用情况保存在倍率摘要中。

本版最快与最近通关分别存于 `yanyi.fastest.growth-tempo-v2.<level>.` 和 `yanyi.recent.growth-tempo-v2.<level>.`。二者均保存毫秒高低位、倍率、城防、漏兵、Boss 突破数、完整标记与校验值。失败、非法时间或非法快照不覆盖这两种时间记录；重复终局、旧会话不能再次写入。缺字段或校验失败的快照按无有效纪录处理。旧两参数通关入口仍按原口径维护通关与最佳城防，不虚构时间记录。

结算同时显示本次与本版个人最佳。首次胜利标明首次纪录；更快时标明刷新及改善秒数；较慢或相同时明确“未刷新纪录”，并说明已保存为最近通关。失败标明“本局未通关”。

## 尺寸与场景

| 文件后缀 | 实际像素 | 数量 |
| --- | --- | ---: |
| `-1280.png` | 1280 × 720 | 11 |
| `-1920.png` | 1920 × 1080 | 11 |
| `-2400.png` | 2400 × 1080 | 11 |

每张图生成前均检查文字字形、文本框高度；选关纪录行另检查单行宽度。截图相机使用同一份 1920 × 1080 世界空间画布，调整渲染目标与相机宽高比。这些尺寸覆盖离屏布局，不代表实际设备安全区和触控验收。

| 状态名称 | 断言与画面 | 1280 | 1920 | 2400 |
| --- | --- | --- | --- | --- |
| `levels-previous-rule` | 仅有旧 v1 最快 04:50.035；本版最快、最近通关均为破折号，读取不写入 | [图](levels-previous-rule-1280.png) | [图](levels-previous-rule-1920.png) | [图](levels-previous-rule-2400.png) |
| `result-first` | 本版首次合法胜利，显示本次、个人最佳、首次纪录和最近通关已保存 | [图](result-first-1280.png) | [图](result-first-1920.png) | [图](result-first-2400.png) |
| `levels-first` | 点击结算返回关卡后，本版最快与最近相同，旧版仍可见 | [图](levels-first-1280.png) | [图](levels-first-1920.png) | [图](levels-first-2400.png) |
| `result-slower` | 较慢胜利明确未刷新，最近通关更新而最快保持 | [图](result-slower-1280.png) | [图](result-slower-1920.png) | [图](result-slower-2400.png) |
| `levels-slower` | 返回选关后，最近显示较慢局，本版最快仍显示首局 | [图](levels-slower-1280.png) | [图](levels-slower-1920.png) | [图](levels-slower-2400.png) |
| `result-equal` | 相同毫秒成绩明确“未刷新纪录（与最佳相同）”，最近通关仍保存 | [图](result-equal-1280.png) | [图](result-equal-1920.png) | [图](result-equal-2400.png) |
| `levels-equal` | 返回选关后，相同成绩可读，旧版纪录与最佳城防保持 | [图](levels-equal-1280.png) | [图](levels-equal-1920.png) | [图](levels-equal-2400.png) |
| `result-improved` | 严格更快胜利明确刷新及改善秒数，同时更新最近与最快 | [图](result-improved-1280.png) | [图](result-improved-1920.png) | [图](result-improved-2400.png) |
| `levels-improved` | 返回选关后，最近与最快均显示更快局，旧版继续独立显示 | [图](levels-improved-1280.png) | [图](levels-improved-1920.png) | [图](levels-improved-2400.png) |
| `levels-reopened` | 销毁并重建 GameFlow 后，从同一内存字典读回最近、最快与旧版，不产生新写入 | [图](levels-reopened-1280.png) | [图](levels-reopened-1920.png) | [图](levels-reopened-2400.png) |
| `result-defeat-unchanged` | 两次 Boss 突破产生真实失败，最近与最快保持，结算标明本局未通关 | [图](result-defeat-unchanged-1280.png) | [图](result-defeat-unchanged-1920.png) | [图](result-defeat-unchanged-2400.png) |

## 数据来源与隔离边界

旧版 04:50.035 来自此前只读排查时核验的 Windows 黄巾快照。检查代码将相同的整数值复制到内存：290035 毫秒、倍率掩码 7、城防 20、漏兵 6、Boss 突破 0，校验值为 -526637781；通关标记为 1，独立最佳城防为 30。验收运行时不读取注册表或玩家 PlayerPrefs，也不修改玩家档。不存在的本版最近纪录保持为空。

首张旧版选关图使用正式关卡信息。之后先深复制数据库，将黄巾副本命名为“最近通关验收”，改为 10 单位短路线、城防 30、关闭计时军饷收入，使用一个扣 15 城防的正式 Boss 和 6 秒出兵等待。按 2／1／2／3 倍顺序推进四次真实会话，分别得到首次、较慢、相同、更快的合法胜利；最后将 Boss 数量改为两个以产生失败。配置副本与记录均不回写正式数据。

这些数秒成绩和简化波次只证明记录与导航行为，不代表真实黄巾时长、阵容、收益、难度或解压体验。`levels-first` 与 `levels-equal`、`levels-improved` 与 `levels-reopened` 的画面可能相同，它们分别验证不同的流程状态，不能算作不同玩法内容。

`levels-reopened` 只重建同一进程内的 GameFlow，仍读取同一个内存字典。118 项存储检查另覆盖复制字典后重建 GameProgress 的读取、非法值及损坏字段。**两者均不是独立进程测试，也没有证明真实 PlayerPrefs 落盘、退出后重启或跨设备读取。**

额外无截图检查包括：省略可选结算纪录参数时不误报合法时间无效；重复终局不重复保存；旧会话不写入新一局；本版胜利不改变旧版快照和独立最佳城防。

## 入口与复核

检查实现：[UiRecentRecordChecks.cs](../../../UnityGame/Assets/Editor/UiRecentRecordChecks.cs)。仅由有图形渲染的 [UiBattleRenderChecks.Run](../../../UnityGame/Assets/Editor/UiBattleRenderChecks.cs) 在基础 VerifyProject 之后调用；不挂入 Verify／Windows／Android 的无图形验证链，避免无图形构建覆盖验收图。

在仓库根目录使用已安装并激活的 Unity 编辑器运行：

```powershell
./tools/Build-Game.ps1 -Target Ui
```

该命令会重新生成多组运行时验收图。当前这轮完整证据以本页链接的 `ui-feedback-final.log` 为准，不能用历史日志替代新运行的结果。

| 优先级 | 尚待独立验证 | 所需证据 |
| --- | --- | --- |
| P1 | Windows 真实存档跨进程保留 | 本版实际首次／慢局／同局／快局后退出并重开，核对最近、最快及旧版成绩 |
| P1 | Android 安装与记录持久化 | 当前版本实际安装运行、合法通关、退出重启及记录页面 |
| P2 | 实机显示与输入 | 真实分辨率、安全区、返回按钮、触控及后台恢复 |
