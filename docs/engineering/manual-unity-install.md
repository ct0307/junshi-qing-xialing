# Unity人工安装指引

本项目采用Unity 6.3 LTS 6000.3.28f1，工具安装根目录为 `F:\Development\Unity`。先前自动执行安装脚本被工具审批拒绝，只返回 `blocked by policy`；下载和文件校验仍可继续，安装执行需要用户手动完成。

最新状态：用户已手动完成Hub、正确版本Editor及Android支持EXE安装。11个已校验的SDK/NDK/JDK等ZIP已提取到本指引的对应目录；Java17实际启动、NDKr27c版本及关键文件检查通过。主工程Windows构建及操作已实测。Android要求纯英文工程路径，使用F:/Development/Unity/BuildProjects/YanyiSandpan-6000.3.28f1构建副本已成功生成APK，完整性、包信息及签名检查通过，安装包已复制至UnityGame/Builds/Android/YanyiSandpan.apk；真机尚待验证。下文安装步骤保留用于重新配置环境。

安装文件统一放在 `F:\Development\Unity\Downloads`。14个正式包现已全部下载并验收，共约6.83GiB：4项官方哈希、11个ZIP CRC、3个EXE有效签名通过。没有官方哈希的10项在报告中明确标记NotProvided。完整结果见同目录 `download-verification-6000.3.28f1.json`。只有下载完成、验证通过且去掉 `.partial` 后缀的文件才是最终包；本机安装已完成。

## 路线选择

| 路线 | 操作 | 适用情况 |
|---|---|---|
| 由Hub安装 | 手动打开Hub，在安装6000.3.28f1时勾选Android Build Support、SDK & NDK Tools、OpenJDK | 希望由Hub管理编辑器及后续模块；Hub可能重新下载文件 |
| 独立包安装 | 手动运行已下载的Editor及Android支持EXE，再按下表放置独立ZIP内容 | 希望复用本次已准备的完整下载文件 |

两种路线择一使用。官方说明Add modules仅支持由Hub安装的Editor；Hub的Locate能识别独立安装的Editor，但不会使其获得Add modules功能。[官方依据](https://docs.unity.com/en-us/hub/add-modules)。目前未查到当前Hub可直接复用这些预下载文件的官方离线参数。

## 共同步骤：Hub和许可

1. 双击 `UnityHubSetup-3.22.5-x64.exe`，安装目录选择 `F:\Development\Unity\Hub`。
2. 打开Hub，登录Unity账号，并按界面提示选择适用许可、完成激活。
3. 将Hub设置中的编辑器安装目录设为 `F:\Development\Unity\Editors`。Hub少量账号、许可及系统临时配置仍可能位于用户目录。

## 独立安装包路线

1. 等待编辑器安装文件最终校验通过，双击 `UnitySetup64-6000.3.28f1.exe`。安装目录设为 `F:\Development\Unity\Editors\6000.3.28f1`，安装结果应包含 `Editor\Unity.exe`。
2. 双击 `UnitySetup-Android-6000.3.28f1.exe`，将目标设为相同Unity安装根目录。该EXE只是Android Build Support，**不包含全部SDK、NDK和JDK**。
3. 按下一节将11个ZIP内容放到指定目录。用资源管理器先解压到F盘独立临时目录，再按表复制或改名到目标位置；遇到已有同名目录先核对，不直接整目录覆盖。
4. 在Hub中通过Locate添加 `F:\Development\Unity\Editors\6000.3.28f1\Editor\Unity.exe`。
5. 添加本项目的 `UnityGame` 文件夹。首次打开后检查“Preferences → External Tools”中的Android SDK、NDK和JDK路径，运行项目README中的Verify步骤。

## Android ZIP目录表

以下用 `A` 代表：

```text
F:\Development\Unity\Editors\6000.3.28f1\Editor\Data\PlaybackEngines\AndroidPlayer
```

源ZIP均可在下载目录中按下表文件名开头识别，完整URL与大小见 `download-manifest-6000.3.28f1.json`。

| ZIP文件名开头 | 已确认的归档顶层 | 内容应放到的位置 | 关键文件检查 |
|---|---|---|---|
| `jdk17.0.18-8_` | `bin`、`conf`、`lib`等直接位于归档根部 | 将归档内容直接放入 `A\OpenJDK` | `A\OpenJDK\bin\java.exe` |
| `1_5312bb398aff` | 只有空的 `tools` 目录 | `A\SDK` | `A\SDK\tools`；此包没有实际工具 |
| `android-ndk-r27c-windows` | `android-ndk-r27c` | 将该目录改名为 `NDK`，放入 `A` | `A\NDK\source.properties` |
| `cmake-3.22.1-windows` | `bin`、`doc`、`share`直接位于归档根部 | 将归档内容直接放入 `A\SDK\cmake\3.22.1` | `bin\cmake.exe` |
| `build-tools_r36_windows` | `android-16` | 将该目录改名为 `36.0.0`，放入 `A\SDK\build-tools` | `A\SDK\build-tools\36.0.0\aapt2.exe` |
| `platform-tools_r37.0.1-win` | `platform-tools` | 将该目录放入 `A\SDK` | `A\SDK\platform-tools\adb.exe` |
| `platform-34-ext7_r02` | `android-34` | 将该目录放入 `A\SDK\platforms` | `android-34\android.jar` |
| `platform-35_r01` | `android-35` | 同上 | `android-35\android.jar` |
| `platform-36_r02` | `android-36` | 同上 | `android-36\android.jar` |
| `platform-37.0_r02` | `android-37.0` | 同上，保留这个实际目录名 | `android-37.0\android.jar` |
| `commandlinetools-win-12266719` | `cmdline-tools` | 将该目录改名为 `16.0`，放入 `A\SDK\cmdline-tools` | `A\SDK\cmdline-tools\16.0\bin\sdkmanager.bat` |

NDK还应存在 `A\NDK\toolchains\llvm\prebuilt\windows-x86_64\bin\clang.exe`。JDK、CMake没有外层包装目录；NDK则有一层，放置时按上表检查最终文件层级。

## 安装后验收

在仓库根目录运行：

```powershell
$env:UNITY_EDITOR_PATH = 'F:\Development\Unity\Editors\6000.3.28f1\Editor\Unity.exe'
./tools/Build-Game.ps1 -Target Verify
```

Verify成功后，再打开Bootstrap场景运行、检查界面和交互；随后执行Android构建并连接手机实测。只完成下载或目录放置，不等于Unity编译、Android构建或真机运行已通过。

目录表依据官方发布元数据和11个ZIP的中央目录只读核对，共读取约7.1MB元数据；没有因此执行工具、解压或证明工具可运行。文件完整性与实际安装结果分别验收。
