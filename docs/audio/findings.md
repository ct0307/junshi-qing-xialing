# 音频制作发现

## 依赖

系统 Python 3.13.1；已有 NumPy、SciPy、requests；无 soundfile、PATH 未发现 ffmpeg。使用 SciPy/WAV 输出，不安装依赖。

## 授权

VSCO 2 CE 官方仓库：`https://github.com/sgossner/VSCO-2-CE`。
固定提交：`440300901dfe9275fd84e0b7763af1f8443ae62e`。
仓库 LICENSE 原文为 CC0 1.0 Universal，允许复制、修改、再分发及商业使用；Readme 同时保留作者署名请求，本项目自愿保留署名。

旧官网 HTTPS 证书过期，Kenney 音效页面返回 403；没有关闭证书验证或绕过访问限制。GitHub API 可用，用同一固定版本获取许可、来源和采样。

## 制作边界

曲目为基于 CC0 实体乐器采样的原创五声音阶编配，不采用成曲或知名旋律。实际主奏是竖琴和西洋长笛，不能标为古琴、古筝或箫笛实录。低鼓、木鼓、锣等真实打击采样支撑沙盘风格。

技术验收可证明文件完整性、格式、峰值与循环边界；不能替代人工审美听测和手机外放验证。

## 已定位问题

Windows 默认 GBK 标准输出无法显示来源 README 中的西文字符；用 Python `-X utf8` 读取及输出，文件统一 UTF-8。
raw.githubusercontent.com 连接重置；采用官方 GitHub contents/blob API 获取同一固定提交，校验 Git blob SHA-1 与 SHA-256，不更改安全策略。

来源采样存在八度编号差异：竖琴 E3 实采约164 Hz，对应 MIDI52；长笛 C4 实采约524 Hz，对应 MIDI72；大提琴 C3 实采约262 Hz，对应 MIDI60。先加入音高回归，确认旧映射输出1046 Hz后校正根音；新增真实低音大提琴 C2 采样，避免仅通过大幅降低高音采样来构造低音。最终25个原始采样均登记。
