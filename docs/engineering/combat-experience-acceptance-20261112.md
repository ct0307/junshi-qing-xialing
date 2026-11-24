# 战斗体验表现版验收

日期2026-11-12。首轮已实现并交付独立[Windows试玩](../../UnityGame/Builds/CombatExperience-20261112/README.md)，对应[设计](combat-feedback-design-20261112.md)和[实施计划](combat-experience-plan-20261112.md)。机器记录见[acceptance.json](../../UnityGame/Builds/CombatExperience-20261112/acceptance.json)。

## 实现与边界

0.7实秒固定窗口、3／6／10档连破及合并音已接入；风眼／火区／道路火带依据只读规则快照，连环与协射依据真实来源、目标、时刻和原到期。保持原有事件顺序，新增语义只供展示；不改敌军、伤害、经济、成长、波次、倍率、规则版本或存档键。固定三档音高后置，不修改AudioVoicePool。

## 验证

| 优先级 | 范围 | 本轮证据 |
| --- | --- | --- |
| P1 | 规则／模拟 | [domain-final.log](../../.planning/combat-experience-20261112/domain-final.log)：139通过、0失败；五关自动布阵全胜 |
| P1 | 连破 | [feedback-final.log](../../.planning/combat-experience-20261112/feedback-final.log)：6通过，固定边界／唯一ID／分档／批次／Clear |
| P1 | 内容 | [content-final.log](../../.planning/combat-experience-20261112/content-final.log)：25角色、5关、43波、178阵位、73资源及历史美术校验通过 |
| P1 | 玩法／经济 | [trace-comparison.json](../../.planning/combat-experience-20261112/trace-comparison.json)：9条原有事件、状态和逐笔招募／升级轨迹SHA完全一致 |
| P1 | Unity | [build-experience.log](../../UnityGame/Builds/CombatExperience-20261112/Logs/build-experience.log)：退出0；[301张新图](../ui/runtime-combat-experience-20261112/README.md)，三尺寸字形／裁切及新专项通过 |
| P1 | 声音逻辑 | 同批最高档替代普通击退、响度档、去重／冷却／预算／Boss门控、六种过期短音与新鲜事件检查通过；没有人工听测 |
| P1 | 构建 | [Windows日志](../../UnityGame/Builds/CombatExperience-20261112/Logs/build-windowsexperience.log)：退出0；147文件、277,496,863字节，433项构建输入文件（Assets／Packages／ProjectSettings）构建前后指纹一致，含68个C#文件，不包含Tests／tools |
| P1 | 保护 | [protection-verification.json](../../.planning/combat-experience-20261112/protection-verification.json)：608基线文件无非授权变化；1192历史清单仅3项授权Domain源码变化；247张旧Jade图与147旧Windows文件未变 |
| P2 | 启动 | [windows-startup.json](../../.planning/combat-experience-20261112/windows-startup.json)：隐藏进程8秒持续响应，主窗口句柄为0，没有可见窗口交互 |

窗口统计见[window-comparison.json](../../.planning/combat-experience-20261112/window-comparison.json)。A1倍0.4／0.7秒分别三连破3／5次；B1倍均3次。6档较少，10档由真实击退隔离夹具验证，不能推导正式关卡触发率。

## 固定黄巾参考

同一驱动、合法阵容、正常军饷、同阵位／同升级策略、每0.25实秒取样；新纯反馈语义排除于原玩法比较。A／B／C的完整阵容在[设计](combat-feedback-design-20261112.md)，详细结果见[trace-after-final.log](../../.planning/combat-experience-20261112/trace-after-final.log)。

| 阵容 | 1倍结果／实秒／城防 | 2倍结果／实秒／城防 | 3倍结果／实秒／城防 |
| --- | --- | --- | --- |
| A推荐 | 胜／371.7／30 | 胜／273.8／26 | 败／215.0／0 |
| B替代 | 胜／349.3／30 | 胜／216.3／30 | 败／196.7／0 |
| C三组组合 | 胜／391.3／30 | 胜／279.0／2 | 败／217.8／0 |

这些3倍败局在冻结版中同样存在；本轮保留原结果，不为表现修改平衡。五关自动布阵全胜是独立的原有1倍策略，不能混为上述九条全胜。

## 复审与修正

Domain与展示分别做规格和质量复审，最终无未解决P1／P2。修正了旧事件占冷却、旧动作／闪亮／退场续命、过期普通短音补播、败局活敌状态残留，以及连环嵌套死亡输出默认坐标。新增嵌套死亡回归先失败后通过，Domain139项与9条行为轨迹重新验证。

Unity首次新专项因反射未指定OnPopulateMesh重载而中止，这是测试代码问题，已指定VertexHelper签名。随后真实10档字体需54行高而原框仅48导致裁切，已将连破框调至64并重新完整通过。失败日志保留在本轮规划目录，不以编译或测试夹具错误冒充功能红灯。

## 待人工验证

用户对比试玩的解压感、配合理解、实际听感、五关人工体验、跨进程存档，以及手机安装／触控／帧率／发热仍待验证。本轮仅Windows，Android沿用Jade。隐藏进程检查与离屏渲染不作为可见窗口交互或真机证据。
