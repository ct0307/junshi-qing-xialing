# Boss突破容错设计与实施计划

> 使用 writing-plans、planning-with-files-zh、subagent-driven-development 和测试先行；用户已选择直接扣较多城防后离场，继续在当前会话执行。无初始Git提交，不提交用户资料或新建资料工作树。

## 已确认设计

- Boss抵城按EnemyDefinition.CastleDamage扣城防并离场，城防归零才失败；仍有城防时继续处理本波及后续波次，末波全部已出兵且无存活敌军时可胜利。
- 本轮8种Boss的配置扣城防统一由10调整为15，总城防仍30；普通兵仍1～2。Boss生命、护甲、速度、技能、波次、人数及武将数值保持，先解决失误直接判负。
- 突破只发真实leak并累计漏兵，不发kill、不发击退赏金；暂停、终局、快照和华佗既有上限保持。已有“全部Boss击退”的正常经济成功参考不得放宽。
- 展示准确说明突破损失、继续守城条件和结算，不能再暗示必须全歼或Boss突破必败。故意承受一次突破允许作为城防交换，满城防且期间未恢复时两次15点突破归零；保住城防仍反映在已有最佳城防记录中。
- 小兵保留易于成片击退的解压表现，不在本轮统一加血；主观强弱与解压感继续由人工试玩确认。

## 任务1：规则红→绿

文件：UnityGame/Assets/Scripts/Domain/BattleSession.cs、UnityGame/Tests/DomainTests.cs。

- [x] 替换旧的Boss立即失败断言，补自然抵城后继续、末波容错胜利、零城防失败、两Boss累计、未出兵／下一波不跳过、不发击退赏金与华佗恢复上限测试；先执行并保存预期失败日志。
- [x] 抵城仅以`CastleHealth == 0`调用失败；保留Boss造成最后一击的明确原因，继续沿用配置扣血和真实快照／事件。重新编译执行全部规则与原严格正常经济参考。

## 任务2：配置与预算

文件：tools/build_game_content.py、tools/test_wave_pacing.py、UnityGame/Assets/Resources/Game/database.json、docs/engineering/game-content-manifest.json。

- [x] 先在快照对照检查中明确允许且断言唯一目录变化为8种Boss CastleDamage由10到15；其它目录字段、5关波次与黄巾短批预算保持，取得旧配置失败证据。
- [x] 生成器Boss定义仅改`CastleDamage=15`，使用`--data-only`生成，执行波次与内容检查，确认v3/v4及运行时美术未变化。

## 任务3：真实UI与审查

文件：Presentation下BattleView.cs、BattleResultView.cs、MenuViews.cs，必要时BattleIntelView.cs；新增Editor/UiBossBreachChecks.cs及.meta，接入UiBattleRenderChecks.cs；docs/ui/runtime-boss-breach。

- [x] 以真实BattleView自然抵城夹具验证Boss突破后运行、正确扣血／提示和末波胜利／城防耗尽失败；隔离数据不当作实际难度结论，三尺寸检查字形与裁切并截图。
- [x] 同步战前、开战、Boss提示与结算文案，完整UI回归原教学、调位、复盘、波次、音频与轨迹。独立需求审查后再做质量审查，修复确定问题。

## 任务4：重建与交付

- [x] 确认用户游戏未运行；保存当前击破版APK及验收JSON到History，仅同步变化文件至英文副本，构建前后全部工程文件SHA一致且无额外文件；Unity串行执行Windows／Android。
- [x] 核对Windows关键文件、启动与日志；Android实际大小／SHA／CRC／aapt包信息／签名及复制后哈希，记录无连接设备时的真机边界。
- [x] 更新README、运行契约、Tests说明、实施日志、两平台验收JSON和三份规划记录；重读UTF-8与数字一致性后交付试玩链接。

验证命令：`./UnityGame/Tests/Run-Tests.ps1`；现有Python执行`tools/test_wave_pacing.py`及`tools/build_game_content.py --verify-only`；`./tools/Build-Game.ps1 -Target Ui`／Windows；Android使用`-ProjectDirectory F:/Development/Unity/BuildProjects/YanyiSandpan-6000.3.28f1`。不安装依赖，不强制关闭应用，不以自动规则／离屏UI代替人工体验或手机验收。

当前验证结果：81项规则0失败，4项波次及内容检查通过，完整Ui及两平台构建退出0；33张新图已抽查，需求与质量审查无确定P1/P2。385工程文件／362 Assets构建前后SHA一致，8个变化文件；APK61,677,259字节（58.82MiB），SHA256 `c9897e5920a53d67a7b1eb512166f0db61f2ea0d44a9078cba38e065078eef10`，566项CRC与v2签名通过。Windows265,688,431字节（不含验收JSON）与5关键哈希有效，已验证启动；手机与人工体验仍待验。
