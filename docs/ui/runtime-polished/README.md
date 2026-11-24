# 黄巾样板关运行时验收图

这些图片由Unity 6000.3.28f1真实uGUI离屏渲染生成，包含实际文字、按钮、单位、状态与规则数据。生成命令：在仓库根目录运行`./tools/Build-Game.ps1 -Target Ui`。全程无需Computer Use，不写玩家通关记录。

| 内容 | 图片 |
|---|---|
| 空战场与初始情报 | huangjin-empty.png、huangjin-empty-army.png |
| 选将覆盖与选中底座 | huangjin-placement-preview.png |
| 五关部署与缺钱/升阶 | hulao-deployed.png、huangjin-deployed.png、guandu-deployed.png、chibi-deployed.png、finale-deployed.png |
| 小屏与宽屏 | huangjin-mobile.png（1280×720）、huangjin-wide.png（2400×1080） |
| 满级状态 | huangjin-max-level.png |
| 战斗与波间情报 | huangjin-combat.png、huangjin-between-waves.png |
| Boss与蓄势 | huangjin-boss.png、huangjin-boss-warning.png |
| 胜败结算 | huangjin-victory-result.png、huangjin-victory-mobile.png、huangjin-defeat-result.png |

默认1920×1080，共18张。程序通过真实按钮回调完成五关基础交互、黄巾合法经济胜利与空阵失败；结算核对真实六项统计与三个按钮。正常参考为408.0游戏秒、城防30、击退124、0漏兵、支出2723；空阵237.1游戏秒失守、漏兵26、支出0。游戏秒数不是脚本的实际执行耗时。

已复核文字、名签层级、覆盖预览、Boss预警和结算。图中金色圈与道路表示技能最大覆盖，实际攻击或触发条件由角色规则决定。地景沿用青绿沙盘，仅黄巾增加静态郊野层次；v3/v4源稿未改。

| 优先级 | 尚待验收 | 原因 |
|---|---|---|
| P1 | 手机实际安装、触控与可点性 | 当前adb无连接设备 |
| P1 | 五关人工玩法与难度容错 | 自动解法不等于玩家体验 |
| P2 | 安全区、后台恢复、帧率/内存/发热、听感 | 离屏图和桌面构建无法给出设备证据 |
