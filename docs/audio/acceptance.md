# 音频素材验收记录

日期：2026-11-06。此记录只覆盖音频制作与文件验收；Unity接入、构建和真机由工程验收另行记录。

| 优先级 | 验收项 | 证据与结果 |
|---|---|---|
| P1 | Cue与文件覆盖 | 28 Cue、9音乐＋34短音，共43不同SHA-256；无39文件保底复用 |
| P1 | 工程资源路径 | 与CSV前缀及catalog契约一致；Music/Sfx运行时仅最终文件 |
| P1 | 许可与源工程再分发 | VSCO 2 CE固定提交的CC0原文、25原始采样具体URL与双哈希；见LICENSES.md和source/source_manifest.json |
| P1 | 格式与时长 | runtime为48kHz/16位PCM；43制作母版为48kHz/24位PCM；音乐实际立体声、短音单声道；时长符合Cue建议范围 |
| P1 | 母版真实精度 | 全部43母版低8位包含真实量化内容；菜单母版逐采样与量化前浮点重渲染的24位量化结果完全一致，不是16位补零升位 |
| P1 | 拍号与循环区间 | manifest与ledger含TimeSignature及48kHz整数帧区间；循环为4/4与[0,Frames)，非循环明确null |
| P1 | Cue逻辑优先级 | 结果100、预警90、漏兵75、操作/身份/波次65、普通战斗30、UI/常规音乐50；其他目录字段不改 |
| P1 | 电平与非空 | 峰值-7.3222～-3.5001dBFS；RMS-22.4786～-17.0003dBFS；0削波样本 |
| P1 | 采样接缝 | 七个音乐循环首尾采样差为0；该结果不代表主观循环听感通过 |
| P1 | 变体与五关区别 | 43文件哈希各不相同；五关有独立声部、节奏和音区安排 |
| P1 | 实际音高 | 长笛目标MIDI72约524Hz；已通过独立回归检查，修正源库八度命名差异 |
| P1 | 可重复性 | 同机相同脚本重复生成43/43WAV SHA-256一致，见reproducibility-check.json |
| P1 | 只读验收 | --verify-only返回0、Failures为空；独立测试确认不修改输入或报告 |
| P1 | 独立检查 | docs/audio/test_audio_content.py：7/7通过；包括原runtime43文件SHA-256保持 |
| P1 | 试听板 | audio-preview-60s.wav：60秒、2,880,000帧、2声道；映射见preview-timeline.json |
| P2 | 主观听感、辨识与疲劳 | 未进行人工听测；需用户播放试听板及实际战斗审听 |
| P2 | 民族乐器真实性 | 本轮采用真实竖琴与西洋长笛承担角色，不宣称古琴、古筝、箫笛实录 |
| P2 | 真机外放和延迟 | 本素材任务未进行真机验收 |

复核命令：

```powershell
python -X utf8 tools/build_audio_content.py --verify-only
python -X utf8 docs/audio/test_audio_content.py
```
