# 军师请下令音频交付

范围为 28 个 Cue、9 个音乐文件与 34 个音效文件。Unity 运行时目录只包含最终 WAV 和 `catalog.json`；原始采样、许可和试听板留在本目录，不随 Resources 自动加载。

Unity接入与自动验证见[runtime-acceptance.md](runtime-acceptance.md)，当前安装包见[工程README](../../UnityGame/README.md)。主菜单与暂停页提供音乐、音效独立设置。长音乐导入为Vorbis／Streaming（质量0.7），短音为单声道PCM／DecompressOnLoad；原始WAV与24位制作母版均保留。

## 制作与来源

全部配乐为本项目新编写的旋律、节奏、声部与结构，使用 VSCO 2 Community Edition 的 CC0 实体乐器采样制作，没有采用现成歌曲、影视或游戏旋律。短音以相同 CC0 打击、拨弦和长笛采样进行裁剪、移调、反向、分层；火焰和气流另含固定随机种子的原创滤波噪声设计。没有把正弦提示声标为真实乐器。

本次使用的是竖琴、西洋长笛、大提琴拨奏、低鼓、木鼓、康加鼓、木击、锣、金属敲击、铃树与钹。竖琴与长笛承担方案中的拨弦和箫笛声部角色，但不是古琴、古筝、箫或笛子的实录替代品；这是当前音色库的明确差异。

许可及自愿署名见 [LICENSES.md](LICENSES.md)。逐个原始采样的来源 URL、固定提交、Git blob SHA-1、SHA-256 见 `source/source_manifest.json`，许可原文为 `source/VSCO2CE-LICENSE.txt`。下载仅使用官方 GitHub API，没有关闭 TLS 校验。

## 音乐结构

| Cue | 时长 | 速度 | 配器与结构差异 |
|---|---:|---:|---|
| music.menu | 72 秒 | 80 BPM | 24 小节；疏朗竖琴、长笛与稀疏木鼓 |
| music.battle.huangjin | 120 秒 | 96 BPM | 48 小节；木鼓、轻拨弦、长笛轮句 |
| music.battle.hulao | 120 秒 | 96 BPM | 48 小节；低鼓重拍、低音拨奏、较疏长笛 |
| music.battle.guandu | 120 秒 | 96 BPM | 48 小节；切分木击、中音拨奏与移位竖琴 |
| music.battle.chibi | 120 秒 | 96 BPM | 48 小节；高音竖琴、长笛长句与轻锣 |
| music.battle.finale | 120 秒 | 96 BPM | 48 小节；密鼓、多层拨弦与较高拨奏应答 |
| music.boss | 51.4285625 秒 | 112 BPM | 24 小节；低音长笛声部、紧密低鼓与木击 |
| music.victory | 4 秒 | 自由短句 | 原创上行竖琴与长笛结句 |
| music.defeat | 5 秒 | 自由短句 | 原创下行竖琴与低鼓结句 |

Boss 为 96 拍按 112 BPM 取整到 48 kHz 采样，时长与 51.43 秒建议值的误差小于 0.002 秒。五关均为独立编配、独立文件，没有采用 39 文件保底复用。

六类出手分别以金属短击、弓弦拨弦与气流、火焰噪声、反向铃树与长笛、木击与长笛、暖色双拨弦构成。城防、波次、Boss 预警、阶段和三名武将标识采用不同节奏和音色层次。

## 格式与技术验收

运行时音乐为 48 kHz、16 位 PCM、立体声 WAV；运行时短音为 48 kHz、16 位 PCM、单声道 WAV。全部 43 个制作母版另存于 `masters/Music` 和 `masters/Sfx`，格式为 48 kHz、24 位 PCM，声道与对应运行时音频一致。母版直接从量化前浮点编曲信号导出，没有读取16位运行时音频后补零升位；母版清单同时保存浮点信号哈希和低8位非零比例。运行时43个WAV的原SHA-256保持不变，本轮未另做有损压缩副本。各短音变体分别制作，不是重复文件改名。

`audio_manifest.json` 的 `Files` 与 `Masters` 分别记录运行时版本与制作母版。带 BOM 的 `audio-ledger.csv` 为43行逐文件台账，并列出母版路径及哈希。两者均包含拍号 `TimeSignature`、循环开始帧 `LoopStartFrame`、循环结束帧 `LoopEndFrameExclusive`；循环曲为4/4，开始为0，结束为文件总帧数 `Frames`。结算自由短句与非乐音效没有固定拍号，拍号及循环区间均明确为 `null`，CSV也使用字面量 `null`。

循环区间采用48 kHz整数PCM帧，左闭右开 `[0, Frames)`，不使用四舍五入后的秒数；1秒为48,000帧，立体声的一帧包含左右各一个采样。菜单为 `[0, 3456000)`，五关各为 `[0, 5760000)`，Boss为 `[0, 2468571)`。

台账另有逐文件SHA-256、时长、采样率、声道、峰值、RMS、采样接缝、来源采样和编配说明。`technical-report.json` 是最近一次完整生成时的技术检查记录。RMS 为均方根电平，不是 LUFS 测量。

循环通过周期混响保留尾音，并修正最后 5 毫秒的采样跳变；只读检查要求首尾采样差不超过 0.005，不能据此保证主观听感无接缝。实际循环听感、配器平衡、审美、音效辨识度、长时间疲劳、手机外放和真机延迟仍需要人工验收。

`catalog.json` 优先级遵循已认可方案：结果100、Boss预警和阶段90、城防漏兵75、招募/升级/身份/波次65、普通出手/命中/退散30、UI与常规音乐50。此修订保留原有增益、限流、路径和循环开关。

## 重现与校验

在仓库根目录执行，依赖现有 Python、NumPy 和 SciPy，无需安装 soundfile、ffmpeg、npm 包或框架：

```powershell
python -X utf8 tools/build_audio_content.py --download-only
python -X utf8 tools/build_audio_content.py
python -X utf8 tools/build_audio_content.py --verify-only
python -X utf8 docs/audio/test_audio_content.py
```

首次生成从固定提交获取已登记的 WAV 和许可文档；后续生成验证本地原始采样后离线复用。`--verify-only` 完全只读：不下载、不重写 WAV、不刷新清单或报告，文件缺失或哈希不符会返回非零退出码。完整生成只更新本轮生成的音频及本目录生成台账，不触碰既有 Game 素材。

## 试听板

`audio-preview-60s.wav` 为 60 秒立体声试听板：前 36 秒依 Cue 顺序展示九段音乐各四秒，后 24 秒依次展示全部 34 个短音变体，短音之间保留间隔。时间与文件映射见 `preview-timeline.json`。本轮未宣称已完成人工听测。
