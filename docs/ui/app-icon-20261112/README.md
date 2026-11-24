# 军师请下令 · 青玉军令图标

## 设计

主符号为暖金军令牌，中央使用原创轮廓绘制的单字“令”，与应用名称“军师请下令”直接呼应。青玉色延续项目现有视觉，朱砂小印提供少量暖色焦点。减少人物、兵器和文字堆叠，使桌面小图标保留明确轮廓。

本稿使用矢量路径直接设计与导出，未使用图像生成API、外部图片或新安装依赖。图标中的“令”不依赖字体。展示板文字使用项目既有中文字体，不属于图标源稿。

| 用途 | 文件 | 说明 |
| --- | --- | --- |
| 原创源稿 | `master.svg` | 1024×1024，满方形不透明背景 |
| 通用图标 | `app-icon-1024.png`、`app-icon-512.png`、`app-icon-192.png`、`app-icon-96.png`、`app-icon-64.png`、`app-icon-48.png` | 满方形PNG，不预烘焙圆角 |
| Android自适应前景 | `adaptive-foreground.svg`、`adaptive-foreground-432.png` | 透明画布432×432，对应108dp图层 |
| Android自适应背景 | `adaptive-background.svg`、`adaptive-background-432.png` | 不透明青玉底色，432×432 |
| Android单色候选 | `monochrome.svg`、`monochrome-432.png` | 白色轮廓及字形，透明底，待平台接入 |
| Android组合预览 | `adaptive-launcher-composite-preview.png` | 前景与背景合成后，取中央72/108可视区；仅用于观察图层组合 |
| Windows图标 | `app-icon-windows.ico` | 透明圆角；16、24、32、48、64、128、256七种尺寸 |
| 展示板 | `preview-board.png` | 大图、48/64/96像素实尺、浅深背景圆形与圆角裁切 |
| 校验记录 | `verification.json` | 尺寸、透明度、安全区、ICO尺寸、SHA-256 |

## 安全区和验证

- Android前景画布432×432；中央安全圆半径为432×33/108＝132像素。
- 当前所有非透明前景像素距中心最大约120.169像素，保留约11.831像素余量；包括抗锯齿边缘。
- 通用PNG及Android背景均检查为全不透明；前景检查包含全透明与全不透明像素。
- 已查看展示板，检查48/64/96像素轮廓、单字“令”、圆形与圆角裁切。16～32像素以军令牌轮廓为主，不承诺字形完整阅读。
- 已检查渲染背景为青玉色，避免渲染器对渐变支持不足造成黑底。
- 本交付尚未修改Unity图标设置，未打Android包、未做启动器或手机实机验证。平台接入后需分别验收图标掩模、动态主题与包内资源。

## 重新导出

在仓库根目录使用项目现有Node.js、Python、PyMuPDF与Pillow执行，不安装依赖：

```powershell
node .planning/combat-readability-icon-20261112/icon-build.cjs
python .planning/combat-readability-icon-20261112/icon-render.py
```

上述脚本只更新本轮图标设计目录，不读取或修改Unity设置、运行时素材、构建包及历史v3/v4稿件。展示板圆角仅用于预览，通用PNG始终保持完整方形。
