# 音频素材制作计划

已批准路线：授权素材＋原创编辑。实施总计划见 `../engineering/audio-implementation-plan.md`。

- [x] 读取 28 Cue 与精确资源契约；确认 Python 3.13、NumPy、SciPy 可用。
- [x] 固定 VSCO 2 CE 的 CC0 许可与提交版本，拒绝无法核验的来源。
- [x] 保存许可原文、来源快照、原始采样与下载校验。
- [x] 生成 9 个音乐、34 个音效和 60 秒试听板。
- [x] 运行独立验收与脚本只读验收，保存逐文件技术台账。
- [x] 向主任务报告可复现命令、限制和人工听测边界。
- [x] 审查补齐43个真正24位母版、48kHz整数循环帧与拍号元数据，并统一已认可的优先级；7/7独立验收通过，原runtime音频哈希不变。

范围仅限 `tools/build_audio_content.py`、`docs/audio/**`、`Resources/Audio/Music/**`、`Sfx/**` 与 `catalog.json`。不安装包、不删除用户文件、不改既有 Game 素材。
