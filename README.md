# 军师请下令

移动应用开发课程项目，一款三国题材的横屏塔防游戏。使用 Unity 6.3 LTS、C# 和 uGUI 开发，面向 Android，支持离线游玩。

战前从 25 名武将中选择最多 10 名，战中招募、布阵和升阶，守住城防。本关的 Boss 不可选为己方武将，每名武将每局只能招募一次。

当前版本包含战役、竞速和战斗体验样板。手机性能、实际听感和游玩体验还需要真机测试。

## 打开项目

在 Unity Hub 中添加 `UnityGame` 目录，使用 **Unity 6000.3.28f1** 打开。构建 Android 版本需要安装对应的 Android Build Support。

## 目录

| 目录 | 内容 |
| --- | --- |
| `UnityGame/` | Unity 工程、游戏资源和规则测试 |
| `docs/ui/` | 界面设计、角色素材和画面验收记录 |
| `docs/engineering/` | 数据契约、实现说明和验收记录 |
| `docs/audio/` | 音频素材、来源和使用许可 |
| `tools/` | 内容生成、检查和构建脚本 |

## 规则测试

在仓库根目录使用 PowerShell 运行：

```powershell
./UnityGame/Tests/Run-Tests.ps1
```

竞速与战斗体验的专项命令见 [工程说明](UnityGame/README.md)。

仓库不包含 Unity 缓存、安装包和本地临时验收目录。历史文档中指向这些目录的链接，需要本地对应文件才能打开。

## 素材来源

音频来源与许可见 [docs/audio/LICENSES.md](docs/audio/LICENSES.md)，其他素材的说明随对应目录保存。
