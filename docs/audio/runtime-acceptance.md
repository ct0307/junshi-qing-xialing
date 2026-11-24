# Unity音频接入验收

版本：Unity 6000.3.28f1；素材版本见audio_manifest.json。素材检查与实际播放证据分开记录，以下为更名前音频阶段实际执行结果。游戏现已更名《军师请下令》；当前安装包与更名验收见 `../../UnityGame/README.md`，音频素材保持。

| 优先级 | 项目 | 结果与证据 |
|---|---|---|
| P1 | 规则与事件身份 | 65项纯C#测试通过、0失败；只补真实敌军身份与boss_spawn，保留原notice。正常经济通关参考不变 |
| P1 | 素材及许可 | --verify-only通过；28 Cue、43个不同运行时WAV、43个24位母版、25个CC0采样；独立素材测试7/7通过 |
| P1 | 音频服务与导入 | Unity逻辑检查通过；原生Mixer路由、固定11音源、独立设置迁移、真实时间限频与去重、音乐状态、终局清理 |
| P1 | 实际DSP输出 | AudioPlayback通过，48kHz双声道，采得10段WAV；菜单、交战、Boss、预警、暂停UI、恢复2倍速、结果与重试有非零输出；后台和持续暂停两段输出为零 |
| P1 | 快速换曲和暂停回归 | AudioRegressions通过，5段WAV；取消未接入的战斗曲保留原菜单源及位置；20毫秒暂停窗中新曲绑定后可恢复播放；结算挂起即清除旧短句，前台不补播 |
| P1 | 设置界面 | Ui通过；主菜单与暂停页共用设置、真实按钮、独立音量与开关、隔离存储重读、返回层级与平台入口；6张设置图已逐张查看 |
| P1 | 拒绝反馈 | 选满10人、点击本关禁选敌将使用reject，合法选将使用click；单操作不叠播通用点击和拒绝 |
| P1 | 缺失素材 | 修改隔离服务的内存目录以模拟缺失路径；关键预警回退合成音、音乐缺失保留当前曲、每Cue仅一条缺失日志；未删除真实资源 |
| P1 | 既有内容 | 内容检查通过25角色、5关、43波、178阵位、73资源，v3/v4基准未变；黄巾UI完整胜利及空部署失败保持 |
| P1 | 音频阶段构建与体积 | Windows与ARM64 IL2CPP Android构建退出0；375个工程文件构建前后指纹一致。该阶段APK61,529,803字节（58.68MiB），相较流程版增16.14MiB；566 ZIP条目CRC与APK v2签名通过；此包现保留在History |
| P2 | 制作差异 | 实际使用竖琴、西洋长笛等真实采样编配，未采用民族乐器实录；全部五关为独立预混版本 |
| P1 | 人工与真机听测 | 未测。需连续听3次循环、满10将一局、手机低／中音量与耳机辨识、设备音频中断与切换；一局疲劳与20次重试内存检查也待测 |
| P2 | 声学及性能预算 | 未测LUFS-I、dBTP、设备CPU和内存峰值。文件RMS/采样峰值、固定声部与DSP能量检查不能替代上述测量 |

## 问题复现与修复

独立审查发现两项资产缺口和四项运行边界。已补真实24位制作母版及拍号／循环帧；播放问题先在Unity实际复现失败，再修复并复跑。红阶段保留于`UnityGame/Logs/audio-regressions-red.log`、`audio-roster-red.log`及`audio-playback-regressions/report-red.json`，绿色结果见同目录最新report.json。

运行时修复包括：返回当前曲时取消异步请求并保留源；为暂停淡出窗内绑定的未启动片段记录启动状态；系统挂起结算时清除旧结果和排程；选将操作按实际结果决定声音。未增加第三方框架。首轮APK音乐增量略超预算后，将Unity Vorbis质量从0.8调至0.7并重新导入，随后复验音频逻辑、实际DSP输出与Windows构建；制作母版和运行时原始WAV哈希不变。压缩档位仍须手机人工听测，不据此保证音质。

## 复核入口

从仓库根目录运行，Unity检查须串行执行，先关闭同一工程的编辑器：

```powershell
./UnityGame/Tests/Run-Tests.ps1
python -X utf8 tools/build_game_content.py --verify-only
python -X utf8 tools/build_audio_content.py --verify-only
python -X utf8 docs/audio/test_audio_content.py
./tools/Build-Game.ps1 -Target Audio
./tools/Build-Game.ps1 -Target Ui
./tools/Build-Game.ps1 -Target AudioPlayback
./tools/Build-Game.ps1 -Target AudioRegressions
```

实际输出位于`UnityGame/Logs/audio-playback`和`audio-playback-regressions`；界面证据位于`docs/ui/runtime-audio`。PlayMode检查使用-force-audio，自动进入隔离空场景并退出，不改玩家偏好。此处系统行为通过调用真实入口验证，尚无手机操作系统投递回调证据。

音频阶段历史APK：`UnityGame/Builds/Android/History/YanyiSandpan-audio-a3b684365a7a.apk`，SHA256 `a3b684365a7a817b459d39cdfe949c9565df0aeb5a0ac45559139db5a57aa6c9`。对应记录为History下的`apk-verification-audio.json`、`source-sync-verification-audio.json`。

更名阶段历史包已保留在 `UnityGame/Builds/Android/History/YanyiSandpan-rename-958fda73beee.apk`：61,532,583字节，SHA256 `958fda73beee7f6753def07d36ffb4fc70957e6c7024f0fe9cf2afc5122301a7`。

玩法入门阶段历史包保留于 `UnityGame/Builds/Android/History/YanyiSandpan-gameplay-0eed887d1feb.apk`：61,669,575字节，SHA256 `0eed887d1feb23cdcd0b43a9de1c9386653c6acb1cf2f1c8f50a3484ab5aa7dc`。

布阵与击破阶段历史包保留于 `UnityGame/Builds/Android/History/YanyiSandpan-impact-48d2f53bc929.apk`：61,677,619字节，SHA256 `48d2f53bc9291e8bc98a96fd72f0db0cb5c43a54fdaef4c98102f74269483842`；对应 `apk-verification-impact.json`、`source-sync-verification-impact.json` 同目录保留。

当前为Boss突破容错版，产物以 `UnityGame/README.md` 及Android目录验收JSON为准：61,677,259字节（58.82MiB），SHA256 `c9897e5920a53d67a7b1eb512166f0db61f2ea0d44a9078cba38e065078eef10`，比击破版少360字节，相较流程版累计增17,075,799字节（约16.28MiB）。81项规则、4项波次、完整Unity UI及Windows／Android构建通过，362个Assets、385个工程文件构建前后匹配，APK 566条CRC与v2签名通过；日志为 `ui-boss-breach.log`、`build-windows-boss-breach.log`、`build-android-boss-breach.log`。33张新图连同此前73张共106张运行时图；电脑版已验证1280×720启动、标题与句柄，未见托管异常，收尾时已关闭，未取得退出码。本轮未重做音频制作、DSP采样或人工听测，上述音频阶段证据按原报告记录；人工乐趣、手机触控、听感、性能及实际玩家偏好迁移仍待验。
