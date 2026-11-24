# 成长与进军改版：真实 UI 验收图

日期：2026-11-06。以本目录实际文件核对，共 **18 个场景 × 3 个尺寸 = 54 张 PNG**。画面由运行时 uGUI 的文字、按钮、阵位和单位离屏渲染生成，交互检查调用真实按钮回调。

验收入口为 [UiBattleRenderChecks.cs](../../../UnityGame/Assets/Editor/UiBattleRenderChecks.cs)，其中调用 [UiGrowthChecks.cs](../../../UnityGame/Assets/Editor/UiGrowthChecks.cs)。本轮完整 UI 日志为 [ui-growth-final.log](../../../UnityGame/Logs/ui-growth-final.log)：包含“成长UI真实交互检查通过”与完整 UI 自动渲染通过记录，末尾为 `return code 0`。Unity 版本为 `6000.3.28f1`。

## 尺寸与呈现方式

| 文件后缀 | 实际像素 | 数量 | 核查范围 |
| --- | --- | ---: | --- |
| `-1280.png` | 1280 × 720 | 18 | 小尺寸文字、固定成长区、按钮及结算内容 |
| `-1920.png` | 1920 × 1080 | 18 | 1920 × 1080 逻辑画布的完整布局 |
| `-2400.png` | 2400 × 1080 | 18 | 宽横屏下画布居中及两侧留白 |

截图相机使用同一份 1920 × 1080 世界空间画布，调整渲染目标与相机宽高比。每次截图前均运行中文文字及裁切检查。上述尺寸覆盖离屏布局，不代替手机安全区、触控命中和系统实际分辨率验收。

## 场景清单

每行对应三张实际存在的图片。

| 场景 | 内容及断言 | 数据来源 | 1280 | 1920 | 2400 |
| --- | --- | --- | --- | --- | --- |
| `menu` | “军师请下令”主菜单 | 正式配置 | [图](menu-1280.png) | [图](menu-1920.png) | [图](menu-2400.png) |
| `levels-empty-records` | 无本版有效纪录时，全部关卡显示“最快 —” | 正式配置、空内存纪录 | [图](levels-empty-records-1280.png) | [图](levels-empty-records-1920.png) | [图](levels-empty-records-2400.png) |
| `roster-details-do-not-select` | 卡片主体查看详情；独立勾选加入／移出；逐一查看25将三阶后阵容不变 | 正式配置 | [图](roster-details-do-not-select-1280.png) | [图](roster-details-do-not-select-1920.png) | [图](roster-details-do-not-select-2400.png) |
| `paused-clock` | 五卡两页可切换；进军1／2／3倍收入即时换算；累计3实秒仍显示00:03；暂停后计时冻结 | 正式黄巾关卡 | [图](paused-clock-1280.png) | [图](paused-clock-1920.png) | [图](paused-clock-2400.png) |
| `upgrade-tier-1` | 黄忠一阶当前覆盖、下一阶变化及升级费用 | 成长预览夹具 | [图](upgrade-tier-1-1280.png) | [图](upgrade-tier-1-1920.png) | [图](upgrade-tier-1-2400.png) |
| `upgrade-tier-2` | 真实升级后显示二阶覆盖与二→三阶预览 | 成长预览夹具 | [图](upgrade-tier-2-1280.png) | [图](upgrade-tier-2-1920.png) | [图](upgrade-tier-2-2400.png) |
| `upgrade-tier-3` | 三阶满级按钮禁用，移除不存在的下一阶范围 | 成长预览夹具 | [图](upgrade-tier-3-1280.png) | [图](upgrade-tier-3-1920.png) | [图](upgrade-tier-3-2400.png) |
| `growth-full-details` | 满级技能详情与完整成长说明 | 成长预览夹具 | [图](growth-full-details-1280.png) | [图](growth-full-details-1920.png) | [图](growth-full-details-2400.png) |
| `shape-zhao-yun-tier-1-to-2` | 赵云当前／下一阶定向技能形状；注明“方向示意，实际朝敌军” | 成长预览夹具 | [图](shape-zhao-yun-tier-1-to-2-1280.png) | [图](shape-zhao-yun-tier-1-to-2-1920.png) | [图](shape-zhao-yun-tier-1-to-2-2400.png) |
| `shape-lv-bu-tier-1-to-2` | 吕布一阶扇面与二阶半圆比较 | 成长预览夹具 | [图](shape-lv-bu-tier-1-to-2-1280.png) | [图](shape-lv-bu-tier-1-to-2-1920.png) | [图](shape-lv-bu-tier-1-to-2-2400.png) |
| `shape-lv-bu-tier-2-to-3` | 吕布二阶半圆与三阶回身整圈比较 | 成长预览夹具 | [图](shape-lv-bu-tier-2-to-3-1280.png) | [图](shape-lv-bu-tier-2-to-3-1920.png) | [图](shape-lv-bu-tier-2-to-3-2400.png) |
| `upgrade-unaffordable` | 现金与可用预支不足时，升级禁用并显示固定差额 | 成长预览夹具 | [图](upgrade-unaffordable-1280.png) | [图](upgrade-unaffordable-1920.png) | [图](upgrade-unaffordable-2400.png) |
| `upgrade-supply-advance` | 现金不足但鲁肃合法余款足够时，按钮可用且显示预支；随后真实点击完成原子扣费 | 预支夹具 | [图](upgrade-supply-advance-1280.png) | [图](upgrade-supply-advance-1920.png) | [图](upgrade-supply-advance-2400.png) |
| `record-first` | 真实胜利首次保存纪录，显示本次、个人最佳及同局快照 | 纪录／突破夹具 | [图](record-first-1280.png) | [图](record-first-1920.png) | [图](record-first-2400.png) |
| `record-improved` | 3倍进军取得严格更快成绩，显示刷新差额 | 纪录／突破夹具 | [图](record-improved-1280.png) | [图](record-improved-1920.png) | [图](record-improved-2400.png) |
| `record-slower-keeps-best` | 较慢胜利不刷新；本局城防14、漏兵2，最佳快照仍为城防15、漏兵1、Boss突破1 | 纪录／突破夹具 | [图](record-slower-keeps-best-1280.png) | [图](record-slower-keeps-best-1920.png) | [图](record-slower-keeps-best-2400.png) |
| `battle-loss-continues` | Boss真实突破扣15城防，显示“-15”及低城防提示，城防15时继续守关 | 纪录／突破夹具 | [图](battle-loss-continues-1280.png) | [图](battle-loss-continues-1920.png) | [图](battle-loss-continues-2400.png) |
| `record-defeat-unchanged` | 第二次突破导致真实失败，最快纪录保持并显示“未刷新纪录” | 纪录／突破夹具 | [图](record-defeat-unchanged-1280.png) | [图](record-defeat-unchanged-1920.png) | [图](record-defeat-unchanged-2400.png) |

## 正式数据与隔离夹具边界

- 正式配置来自 `Resources/Game/database`。主菜单、空纪录关卡、选将与暂停计时场景直接使用该配置；选将检查遍历25名武将的三阶说明，战斗使用黄巾推荐阵容。
- 成长预览先深复制数据库，将黄巾关卡改为 `growth-preview-fixture`、初始军饷6000并清空该夹具的Boss禁选表，以便同时展示黄忠、赵云、吕布、张飞与鲁肃。费用不足场景再将初始军饷设为赵云招募费用。英雄成长仍读取共享规则，夹具不回写正式配置；吕布在此可部署不代表正式关卡解除Boss禁选。
- 预支夹具 `supply-ui-fixture` 使用复制后的关卡，关闭计时收入并计算初始现金，使赵云升级恰差20军饷；放入耐久1000倍、移速0.001倍的步兵守候。三阶鲁肃提供合法可预支余款。截图发生在升级前，随后断言升至二阶、现金归零、支出增加完整费用且预支余额只减少差额。此状态用于验收升级按钮与交易一致性。
- 纪录夹具 `growth-record-fixture` 将城防设为30，使用极短路线与单个扣15城防的正式Boss，按真实战斗推进产生胜利、刷新、较慢胜利及失败；后续增加步兵或第二波Boss，制造快照差异和持续低城防。图中的数秒通关和简化波次只用于验证纪录及突破流程，不代表正式关卡时长、难度或通关参考。
- 全部成长场景通过 `Dictionary<string, int>` 注入 `GameProgress`；通关与最快纪录只使用隔离内存，不读写玩家持久化存档。纪录保留、失败不刷新等断言均在同一隔离场景内比较。

截图以外还检查了：赵云38／65半宽网格、吕布扇面→半圆→整圈及满级无下一阶几何；战中打开详情自动暂停，关闭详情仅恢复其持有的暂停，详情期间失焦后等待手动继续，详情期间重开不恢复旧会话；隔离毫秒纪录从100秒刷新到99.999秒时显示“快了0.001秒”。这些检查没有额外PNG，不能计入54张。

## 复核命令

从仓库根目录运行，使用已安装并激活的Unity编辑器，执行前关闭同一工程的编辑器以避免工程锁冲突。完整UI入口会重新生成本目录及其他运行时验收图。

```powershell
$env:UNITY_EDITOR_PATH = 'F:\Development\Unity\Editors\6000.3.28f1\Editor\Unity.exe'
./tools/Build-Game.ps1 -Target Ui

# 独立规则与全关模拟；不属于截图计数
./UnityGame/Tests/Run-Tests.ps1 -Simulate
```

构建脚本的默认UI日志为 `UnityGame/Logs/build-ui.log`；本轮完整结果另存为上方引用的 `UnityGame/Logs/ui-growth-final.log`。规则、内容与严格通关参考见 [成长改版验收](../../engineering/hero-growth-runtime-acceptance.md)。

## 人工与真机待验

| 优先级 | 待验项目 | 所需证据 |
| --- | --- | --- |
| P1 | Android安装、启动与真实触控 | 当前成长版安装包的独立构建／签名记录；手机实际安装启动；选将查看／勾选、五卡翻页、升阶与详情操作 |
| P1 | 五关人工玩法与节奏 | 1／2／3倍切换、升级取舍、Boss突破后补阵、单局时长、难度及解压感记录 |
| P1 | 最快纪录落盘与重启读取 | 手机实际首次纪录、刷新、较慢／失败保持、重启后最佳快照，以及旧偏好迁移 |
| P2 | 异形屏、系统返回和后台恢复 | 真实安全区、小屏文字与触区；系统返回、失焦／挂起和前台手动继续；详情暂停与系统暂停叠加 |
| P2 | 性能和听感 | 密集敌军／满阵容时帧率、内存、发热；外放及耳机下的关键预警与音量体验 |

本页的日志与离屏图只确认上述自动UI检查。Android／Windows产物交付、真机系统回调、磁盘持久化和人工体验各自需要独立证据。
