# 演义沙盘音频实施计划

> 执行流程：采用writing-plans与subagent-driven-development；以失败检查、最小实现、独立需求审查和质量审查逐项推进。用户已授权本轮实施，不在任务间重复请求确认。

日期：2026-11-06。

目标：落实audio-design-plan.md的授权素材＋原创编辑路线，交付可运行音频、Unity接入、试听样条、授权台账及新构建。

架构：GameAudio保留静态表现入口，分离配置、偏好、音乐双源与固定短音池。音乐与音效资源置于Resources/Audio，不改变现有Game内容生成或数值；Domain仅补充已有事件的目标身份。使用Unity原生音频组件，不增加框架。

当前分支codex/unity-game；仓库仍无初始提交且资料未跟踪，本轮不为隔离工作树提交用户文件。文件所有权分开，Unity进程由主任务串行执行。

## 任务1：可分发素材与原创编配

文件：tools/build_audio_content.py、docs/audio/**、UnityGame/Assets/Resources/Audio/{Music,Sfx,catalog.json}。

- [x] 核验真实乐器／音效样本许可，保存来源、许可原文及SHA-256，不采用无法核对工程源文件分发权的素材。
- [x] 按28条Cue制作9个音乐与34个音效文件，必要时按已批准的39文件保底绑定；导出60秒试听板。
- [x] 脚本提供--verify-only，检查时长、声道、采样、非空、峰值、接缝、文件数与授权清单。

资源契约：catalog.json使用Version=1和Cues数组；每项为Id、Paths（Resources相对路径，无扩展名）、Gain、Cooldown、Priority、Loop。示例：{"Id":"music.menu","Paths":["Audio/Music/mus_menu_an_shang_shan_he_v01"],"Gain":1.0,"Cooldown":0.0,"Priority":50,"Loop":true}。

## 任务2：音频服务与测试

文件：Presentation/GameAudio.cs与新Audio*.cs、Editor/UiAudioChecks.cs、Editor/Audio*.cs、原生混音器。

- [x] 先编写反射式UiAudioChecks，当前缺少独立设置与音乐状态时运行失败，记录红阶段。
- [x] 实现音乐A/B、1个UI、6普通、2关键音源；短音限流、变体、预警压低背景音，关键缺失素材回退。
- [x] 音乐与音效开关／音量独立保存，旧键仅迁移一次；测试注入隔离存储，禁止写玩家偏好。
- [x] 实现菜单、备战、开战、波间、Boss与终局切换，暂停、系统挂起与前台恢复，不随2倍速变调。
- [x] 使用Unity执行音频行为检查并记录实际音源、资源导入与混音器状态。

接口：Initialize(GameObject)、ShowMenu()、BeginBattle(string levelId)、SyncBattle(BattleSession)、SetBattlePaused(bool)、SetApplicationPaused(bool)、SetApplicationFocused(bool)、Event(BattleEvent)、Button()、Reject()；属性MusicEnabled、SoundEnabled、MusicVolume、SoundVolume及旧Enabled兼容入口。

## 任务3：事件身份与规则回归

文件：Domain/BattleSession.cs、CombatSystem.cs、Tests/DomainTests.cs。

- [x] 先用真实战斗测试暴露attack、kill、leak、Boss预警／阶段等事件缺少TargetEnemyId及boss_spawn的问题。
- [x] 填写既有目标字段，Boss登场增加结构化事件并保留原notice；不改任何战斗规则和数值。
- [x] 执行./UnityGame/Tests/Run-Tests.ps1，确保既有61项和4关成功参考保留。

## 任务4：导航、设置与平台接入

文件：Presentation/GameFlow.cs、BattleView.cs、MenuViews.cs、UiFactory.cs、YanyiGame.cs；Editor/UiAudioIntegrationChecks.cs、YanyiBuild.cs。

- [x] 先检查真实菜单／暂停页缺少独立音量入口，以及后台在非战斗页没有全局音频门的问题。
- [x] 页面使用ShowMenu；备战BeginBattle(level.Id)；Tick、开战和恢复后同步真实Session；两个系统回调同时通知音频服务。
- [x] 添加音乐／音效设置面板，在主菜单与暂停页可调，成功／拒绝避免通用点击重复叠加。
- [x] 执行Unity真实按钮检查，覆盖设置、空资源、暂停、系统入口及反复重试。

## 任务5：审查、构建与交付证据

文件：UnityGame/README.md、docs/engineering/implementation-log.md、音频验收记录，以及Builds与Logs中的本轮产物证据。

- [x] 独立需求审查后进行质量审查，修复确定问题并复核。
- [x] 主任务执行规则、内容--verify-only、音频--verify-only、Unity Verify/Ui/音频检查。
- [x] 构建Windows；将本轮变更精确同步至已授权英文Android副本，逐文件核对后构建与验签，保留旧APK历史证据。
- [x] 记录音频播放／试听与真机证据边界，交付试听样条、新APK及授权说明；真实手机外放、系统投递、帧率／发热无设备时保留未验收。

计划中的API为本轮统一协作契约，尚未实现的条目不得写成完成状态；每次Unity运行前先确认代理已结束该批源码写入。

