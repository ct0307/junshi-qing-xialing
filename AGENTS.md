# 军师请下令项目说明

- 当前战斗体验样板：UnityGame/Builds/Sensation-20261119。倍率三档直选、三将主表现、默认关闭的有界诊断、黄巾30槽及第4波16人（0.55间隔）。黄巾规则sensation-hj-mn-v1-army；普通最快/最近与竞速均隔离，其他关保留旧版本。验收见docs/engineering/combat-sensation-acceptance-20261119.md；真人乐趣/听感/真机待验。独立入口Sensation/WindowsSensation/AndroidSensation；不要调用旧入口覆盖历史输出。专项Run-CombatDiagnosticsTests.ps1、Run-SensationExperiments.ps1、Run-SensationFormation.ps1。

- 对外名称：军师请下令；副标题：三国名将 · 布阵守城；宣传语：军师一声令，名将守山河。旧名演义沙盘保留在历史稿中，包名、命名空间和构建路径保持既有标识。

- 语言：与用户交流、代码注释和文档使用简体中文；代码标识符使用英文。
- 技术：Unity 6.3 LTS 6000.3.28f1、C#、uGUI；目标Android横屏离线塔防。
- 工程目录：UnityGame；设计与验收基准：docs/ui；工程说明：docs/engineering；内容生成：tools。
- 最新独立竞速版：UnityGame/Builds/Race-20261119，含Windows与Android。落地P1/P2全Boss击退主竞速、固定步终点计时、首次倍率说明与后台保护、竞速差值结算、UI精修、名单调整及轻量真实复盘。战斗规则faction-army-v1、资格all-boss-v1；旧普通纪录与FactionArmy／CombatExperience／JadeApp包保留。验收见docs/engineering/race-implementation-acceptance-20261119.md。
- 当前竞速专项：`./UnityGame/Tests/Run-RaceTests.ps1`；策略：`./UnityGame/Tests/Run-RaceSimulation.ps1 -Output .planning/race-implementation-20261119/race-simulation-results.json`；Unity：`./tools/Build-Game.ps1 -Target Race`；独立Windows：`-Target WindowsRace`；独立APK：`-Target AndroidRace -ProjectDirectory <新的ASCII工程副本>`。15组领域、20组存档、旧118断言、80局策略（69合格，5/5动态更快复跑一致）、UI177断言78图及两平台构建／APK验签通过。474工程输入构建前后一致，509旧产物保持；真实PlayerPrefs跨进程、真机、听感及真人乐趣待验。
- 规则：25名角色，战前最多选择10名；本关全部Boss排除于己方；每名武将每局最多招募一次；大量阵位。
- v3视觉稿已认可，不重写；v4为首批精修素材。运行时界面需要真实文字、按钮和阵位状态。
- Domain为纯C#，与Unity展示分离；配置字段以Definitions.cs为准，数据契约见docs/engineering/runtime-contract.md。
- 规则测试：在仓库根目录运行 `./UnityGame/Tests/Run-Tests.ps1`；全关自动布阵模拟加 `-Simulate`。使用系统自带的.NET Framework编译器，无需安装NuGet包。
- Unity验证：`./tools/Build-Game.ps1 -Target Verify`；Android构建：`./tools/Build-Game.ps1 -Target Android`；Windows试玩构建：`./tools/Build-Game.ps1 -Target Windows`。编辑器需已安装并激活许可，详见UnityGame/README.md。
- 战斗体验专项：`./UnityGame/Tests/Run-FeedbackTests.ps1`；新图验收：`./tools/Build-Game.ps1 -Target Experience`；独立Windows：`./tools/Build-Game.ps1 -Target WindowsExperience`。不要运行App/WindowsJade覆盖历史证据。当前139项规则、6项反馈、五关模拟、301张新图及Windows构建通过；人工解压感、实际听感与手机性能待试玩验证。
- 军势规则回归：`./UnityGame/Tests/Run-FactionTests.ps1`，22组与40局通过。旧军势UI／构建入口FactionArmy／WindowsFactionArmy／AndroidFactionArmy会写入历史输出，本轮不调用覆盖；旧346图及包保留。先前桌面可见验收到战役情报，用户按Escape停止后未继续自动操作；本轮不使用Computer Use操控用户桌面。
- 内容检查：用现有Python及Pillow运行 `tools/build_game_content.py --verify-only`；完整生成会更新本项目生成的运行时素材与配置，不修改v3/v4源稿。
- 不安装第三方npm包或额外框架。安装工具、删除用户文件、重置分支、强推及批量覆盖已有资料前先确认。
- 禁止提交Library、Temp、Obj、Logs、UserSettings、构建缓存、Unity许可及个人账号数据。
- 环境变量不包含密钥；Unity编辑器路径通过UNITY_EDITOR_PATH指定，Android路径由编辑器配置管理。
- 工具安装位置优先F:/Development/Unity；当前自动执行Unity安装器曾被审批策略拒绝，不得换命令或工具绕过。Unity编译、画面、APK及真机验证须分别取得证据后记录。
