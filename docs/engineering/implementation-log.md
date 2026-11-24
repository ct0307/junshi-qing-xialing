# 实施日志：Unity首版

## 本轮Boss突破容错（2026-11-06）

- 全关城防30，8种Boss抵城扣配置15城防后离场；仅城防归零失败，末波结束且城防仍存可胜利。突破只计漏兵，不计击退或发击退赏金；Boss耐久／技能、小兵属性、原严格全Boss击退参考保持。
- 主任务重新编译执行81项规则，0失败，4项波次及内容检查通过。通用模拟五关均Victory，其中虎牢484.6游戏秒、城防23、漏张辽；终局385.7游戏秒、城防15、漏司马懿。这些是容错结果，不代表全Boss击退或人工难度验收。
- 真实Unity展示红测失败于缺少损失／继续规则提示（Logs/ui-boss-breach-red.log），修正后完整Ui退出0（Logs/ui-boss-breach.log）。新增33张三尺寸图，与此前73张共106张；覆盖真实暂停／恢复／招募／升级、末波容错胜利、二次及低城防归零失败、LastBreach复盘与五关情报，字形／裁切检查及小屏／宽屏抽查通过。隔离短路线夹具不代表实际关卡难度。
- Windows及ARM64 IL2CPP Android构建均退出0，日志Logs/build-windows-boss-breach.log、build-android-boss-breach.log。英文副本同步8个新增或变化文件，362个Assets、合计385个工程文件构建前后SHA-256一致，无额外文件。
- 当前APK位于UnityGame/Builds/Android/YanyiSandpan.apk，61,677,259字节（58.82MiB），SHA256 c9897e5920a53d67a7b1eb512166f0db61f2ea0d44a9078cba38e065078eef10；复制哈希、566项ZIP CRC及v2签名通过，aapt确认军师请下令、com.course.yanyisandpan、0.1.0、min26／target36、ARM64。比击破版少360字节；相较流程版44,601,460字节累计增17,075,799字节（约16.28MiB）。旧击破APK与两份验收JSON已保留History。
- Windows构建目录265,688,431字节（不含验收JSON），5关键文件哈希匹配。已验证1280×720启动、标题与有效句柄，Logs/windows-player-boss-breach.log未见托管异常；D3D12 info queue查询0x80004002后完成图形初始化。收尾时进程已关闭且日志含Shutdown，未取得退出码。
- 独立需求及质量审查未发现确定P1/P2。本轮未重录音频DSP；人工乐趣、五关试玩、听感、手机触控／性能及实际玩家偏好迁移仍待独立验收。

## 历史布阵与击破体验（2026-11-06）

- 用户批准“需要布阵，但打起来很爽”的首批改进。黄巾第4～8波主兵改为每波4个短批，每批3～4人、批内0.55秒，保持原组首末出场时刻。前三波、弓手、Boss排程、波间等待与其余四关保持，敌军总量、属性、赏金和战斗规则不变；仅以生成器data-only更新配置及清单，未重绘历史美术。
- 新增BattleAttackGraphic，吕布按真实扇形、赵云按射程裁切后的贯穿带绘制方向攻击。仅共享群攻主轨迹，太史慈双矢、袁绍箭雨等不同目标保留独立弹道。0.4游戏秒内至少3个唯一有效kill显示局部“连破 ×N”；不计hit、leak、重复或无效ID，不归因某招／某将，不增加奖励。原90反馈／14数字及音频声部预算、暂停冻结与终局清理保持。
- 主任务重新执行76项规则，0失败，日志Logs/domain-impact-root.log；4项波次检查与内容检查通过，25角色／5关／43波／178阵位／73资源及历史美术指纹有效。新推荐／替代／提前升阶参考分别405.5／376.7／405.4游戏秒，均城防30、零漏兵，原部署与成功断言不变；推荐支出2723、结余15，替代支出2457、结余122。
- 红→绿证据覆盖旧版缺少方向图形、连破文字框过矮、无效敌军ID计数、太史慈双目标弹道被合并。连破标签改48高并加深色细描边，回池数字禁用描边；限缩合并键后，真实Domain双目标事件及推进后的两条箭位置检查通过。失败日志verify-impact-red.log、ui-impact-text-red.log、verify-impact-id-red.log、verify-impact-multitarget-red.log均保留；独立需求及质量复审无剩余确定P1/P2。
- 最终完整Unity Ui退出0，日志Logs/ui-impact.log；原教学、调位、复盘、导航、预警和音频设置流程保持。docs/ui/runtime-impact含15张PNG，覆盖1920×1080、1280×720、2400×1080：3张真实正常经济黄巾战斗、12张明确标注的吕布／赵云隔离自然群攻夹具。每图前检查字形／裁切，主任务抽查小屏及宽屏；同帧截图不视为独立状态，弱化敌军夹具不证明实关平衡。
- Windows及ARM64 IL2CPP Android构建均退出0，日志Logs/build-windows-impact.log、build-android-impact.log。英文副本同步8个新增或变化文件，360个Assets、合计383个工程文件构建前后SHA-256一致，无额外文件。
- 该阶段APK现保留于UnityGame/Builds/Android/History/YanyiSandpan-impact-48d2f53bc929.apk，61,677,619字节（58.82MiB），SHA256 48d2f53bc9291e8bc98a96fd72f0db0cb5c43a54fdaef4c98102f74269483842；复制后哈希、566项ZIP CRC及v2签名通过。aapt确认军师请下令、com.course.yanyisandpan、0.1.0、min26／target36、ARM64。比玩法入门版增加8,044字节；对应apk-verification-impact.json、source-sync-verification-impact.json同目录保留。
- Windows构建目录265,687,919字节（不含验收JSON），5个关键文件哈希匹配。新版已按1280×720窗口启动，启动时核对标题“军师请下令”与有效句柄，Logs/windows-player-impact.log未见托管异常。D3D12 info queue查询0x80004002后完成图形初始化；收尾时进程已关闭，日志含清理退出，未取得退出码，未重新打开或强制关闭程序。
- 密集击退采样仅替代阵容增加（12→18、单采样峰值2→4），推荐12→8、提前14→7，三套峰值存活均17；不能直接据此声称“更爽”已验证。adb无设备，人工解压感、五关玩法、手机触控／性能及听感、实际玩家偏好迁移仍待独立验收。本轮音频DSP沿用历史阶段证据，未重新录制。

## 历史玩法入门与布阵纠错（2026-11-06）

- 用户接受黄巾入门、可靠武将信息与失败复盘、开战前调位。本轮沿用现有界面和数值；选关与下一关共用只读黄巾优先顺序，其余关卡仍可自由选择。未通关黄巾显示准备及前三波提示，第四波与通关后关闭教学。
- Domain新增准备阶段TryMoveHero，移动同一HeroUnit并发move事件，保持等级、军饷、消费和已招募标记；展示层同步棋兵、名签、选中、覆盖和升级目标。武将说明复用真实冷却／蓄力计算，协射与伏击显示触发条件。自然抵城前保留LastBreach快照，失败页显示敌军、路线、余血及事实建议。
- 原65项规则加11项新行为检查；主任务重新编译执行76项，0失败，日志Logs/domain-gameplay-root.log。内容检查25角色／5关／43波／178阵位／73资源通过，历史美术与既有合法经济参考保持。
- 独立需求和质量审查发现普通notice会盖Boss预警。曹操普通增伤及马超以Boss为目标的蓄力分别取得失败证据；修正为结合HeroId判定真正敌军来源，普通技能不抢有效预警或前三波教学。最终两次回归及真实UI退出0，日志Logs/ui-gameplay.log；独立复审无剩余P1/P2。
- 22张玩法PNG位于docs/ui/runtime-gameplay，覆盖1920×1080、1280×720、2400×1080；每图前检查非空文字字形与裁切。主任务抽看关卡、调位、失败、吕布／荀彧详情与教学页面。黄巾正常经济胜利408.0游戏秒、空阵失败237.1游戏秒保持；离屏检查不等于人工玩法或手机触控验收。
- Windows与ARM64 IL2CPP Android构建均退出0，成功日志Logs/build-windows-gameplay.log及build-android-gameplay.log。英文副本同步8个变化文件，356个Assets、合计379个工程文件构建前后SHA-256一致，无额外文件。
- 该阶段APK现保留于UnityGame/Builds/Android/History/YanyiSandpan-gameplay-0eed887d1feb.apk，61,669,575字节（58.81MiB），SHA256 0eed887d1feb23cdcd0b43a9de1c9386653c6acb1cf2f1c8f50a3484ab5aa7dc；复制哈希、566项ZIP CRC、v2签名通过。aapt确认军师请下令、com.course.yanyisandpan、0.1.0、min26／target36、ARM64。比更名版增加136,992字节，更名版APK及记录保留History。
- 新版Windows已按1280×720窗口启动，启动时确认窗口标题“军师请下令”与有效句柄，日志Logs/windows-player-gameplay.log未见托管异常；存在D3D12 info queue查询0x80004002诊断行，后续图形初始化成功。交付检查时该进程已退出，日志含引擎清理和Shutdown，未取得退出码。尚未人工完整试玩，实际玩家偏好迁移未单独验收。adb无设备；手机安装触控、性能、五关人工平衡及人工听感仍待独立证据。本轮未重做音频DSP采样。

## 历史游戏更名（2026-11-06）

- 用户确认游戏名“军师请下令”、副标题“三国名将 · 布阵守城”、宣传语“军师一声令，名将守山河。”。主菜单题签改“令”，产品显示名、编辑器菜单和当前说明同步；包名com.course.yanyisandpan、命名空间、构建路径与存档键保持。
- Windows产品名会改变PlayerPrefs路径，保留公司名“演义沙盘课程项目”，增加仅Windows Player运行的旧整数偏好迁移。使用Windows自带advapi32只读接口，不增加程序集或依赖；仅补缺失游戏键、不覆盖现值、不删除旧数据。内存隔离检查通过；未实际运行真实玩家偏好迁移。
- 首次Windows真实构建发现默认NetStandard2.1引用缺少Registry程序集，失败日志保留build-windows-rename-registry-failed.log；改用原生只读接口后，Windows实际构建退出0，产物app.info确认新名称。成功日志build-windows-rename.log，构建目录265,676,079字节（不含验收JSON）。
- Unity Ui退出0，既有流程、文字、音频设置与布局检查保持；1920×1080、1280×720、2400×1080三张新主菜单图已逐张查看，标题、副标题、宣传语和题签无裁切。图片位于docs/ui/runtime-name。
- 更名同步10个新增或变化文件到英文构建副本。356个Assets、合计379个工程文件构建前后SHA-256全部相同，无额外文件；Android ARM64 IL2CPP构建退出0，成功日志build-android-rename.log。
- 该阶段APK现保留于UnityGame/Builds/Android/History/YanyiSandpan-rename-958fda73beee.apk，61,532,583字节（58.68MiB），SHA256 958fda73beee7f6753def07d36ffb4fc70957e6c7024f0fe9cf2afc5122301a7；复制后哈希一致。566条ZIP CRC、APK v2签名通过，aapt确认应用名军师请下令，包名、0.1.0、min26／target36、ARM64保持。对应验收JSON同样保留History。
- 未安装依赖、未改历史美术。adb无设备，真实手机触控、人工听感、性能及实际玩家偏好迁移仍待独立验收。

## 音频制作与接入（更名前，2026-11-06）

- 交付28 Cue、9音乐＋34短音，共43个不同运行时WAV及43个48kHz/24位制作母版；母版从量化前浮点编曲直接导出。25个VSCO 2 CE实体乐器采样固定到Git提交，许可原文、源文件及SHA台账保留；使用竖琴与西洋长笛等音色，不宣称民族乐器实录。60秒试听位于docs/audio/audio-preview-60s.wav。
- GameAudio拆分偏好、Cue目录、音乐双源与固定音效池；原生Mixer含Music与SFX/UI/Battle/Critical，最多11个AudioSource。音乐按需异步加载，短音预载；开战、波间、Boss集合及终局读取真实Session；2倍速不改变BGM音高，危险提示压低背景。主菜单和暂停页可分别调音乐／音效开关及音量，旧总静音仅迁移一次。
- Domain只补事件的真实TargetEnemyId和独立boss_spawn，保留原notice；不改数值、经济或技能。根重新编译执行65项规则测试，0失败；25角色／5关／43波／178阵位／73资源内容检查通过，v3/v4指纹不变。音频verify-only与独立素材7项测试通过。
- 独立审查指出两处资产缺口和四处运行边界。资产补真正24位母版与循环帧／拍号；根在Unity复现取消加载重试导致菜单重启、20ms暂停窗内绑定新曲后无声、结算后台返回补播旧短句、满阵容仍响确认声，再修复并复验。红日志与report-red.json保留，未依据代理完成通知直接认定通过。
- Unity Audio／Ui检查退出0，覆盖独立设置、11声部路由、预警／阶段去重、限频、结果接管及挂起门。缺失路径在隔离目录模拟，关键预警回退且每Cue仅一次警告，长音乐缺失保留当前曲；不删除真实资源。Ui真实按钮验证菜单／暂停设置、满阵容／禁选拒绝声、返回层级、平台入口；6张设置图逐张查看无明显裁切，既有27张流程图及黄巾正常胜利／空阵失败保持。
- 两组PlayMode实际DSP采样取得10＋5段WAV，48kHz双声道；非静音场景存在实际输出，后台与持续暂停为零，快速操作三项回归通过。该检查证明实际输出与门控，不代表人工听感、LUFS、真峰值或手机测试。
- Windows构建退出0。Android第一次新增18.67MiB略超预算，保留中间日志／英文副本产物后将Vorbis质量0.8调至0.7，重新导入并复验音频逻辑、实际输出及Windows／Android构建。原始WAV及制作母版不变。最终源副本352个Assets、合计375文件构建前后SHA全部相同；首次同步127项，压缩修订45项。
- 该阶段APK现备份于UnityGame/Builds/Android/History/YanyiSandpan-audio-a3b684365a7a.apk：61,529,803字节（58.68MiB），比流程版增16.14MiB，SHA256 a3b684365a7a817b459d39cdfe949c9565df0aeb5a0ac45559139db5a57aa6c9。566 ZIP条目CRC通过，APK v2签名通过；包名com.course.yanyisandpan、0.1.0、min26／target36、ARM64。旧流程APK保留History，成功日志为Logs/build-android-audio.log。
- 未安装额外框架或工具，未使用Computer Use、未改玩家偏好。当时无adb设备；手机触控、真实系统音频中断与回调投递、跨进程偏好、三次循环人工试听、10将混音、耳机切换、音频CPU／内存及发热仍未验收。详见docs/audio/runtime-acceptance.md及Builds/Android/History/apk-verification-audio.json。

以下继续保留更早的阶段记录；当前APK以本页顶部Boss突破容错节及apk-verification.json为准，音频制作与播放证据见上述音频节。

## 本轮完整流程与移动稳定性验收
- YanyiGame拆出GameFlow与GameProgress：真实导航、战斗会话、暂停和结算统一管理。备战返回显示离开确认，继续保留部署；错误页保留提示并提供重试按钮；当前会话终局仅结算一次，旧会话不会影响新一局。未改Domain规则、关卡数值或v3/v4源稿，未安装依赖。
- 根任务重新编译并执行61项规则测试，0失败；官渡正常经济参考427.2秒、城防26、245击退/4漏兵，赤壁479.4秒、城防17、219击退/15漏兵，消费与事件逐笔配对。黄巾三套及终局参考保留，当前4/5关有固定合法成功参考；虎牢缺口及五关人工平衡仍待补验。
- 内容verify-only退出0：25角色、5关、43波、178阵位、73资源与历史美术基准一致。Unity Verify退出0，22项安全区实际四角、居中/等比、异常区域与连续切换通过。
- 扩展Ui检查退出0：真实菜单至战斗、25人/最多10人/Boss禁选/空阵容、推荐/清空、备战保留与退出、暂停冻结、重开、自然失败、正常经济胜利、下一关和通关标记通过。GameProgress最佳值与分关记录、结算对象去重、旧会话拒绝均经实际断言。
- 27张真实uGUI图位于docs/ui/runtime-flow，含原18张战斗图及9张导航/错误态图，旧目录保留；覆盖1920×1080、1280×720与2400×1080。截图前检查当前非空Text字形和文本框高度，根任务实际查看菜单、关卡、情报、选将、备战、暂停、胜利、错误页，未见明显新增裁切或遮挡。
- 平台入口测试直接调用YanyiGame的挂起/前台/失焦方法，证明挂起暂停、连续调用幂等、前台不自动继续；失焦在已暂停后调用，未单独证明仅失焦即暂停，更没有实体OS投递证据。错误页测试证明渲染、空flow入口安全与重试按钮派发，未制造真实Resources加载失败或验证重试恢复。内存字典服务重建不等于磁盘存档或进程重启。
- Windows构建退出0。Android构建前同步17个新增/修改文件，235个Assets、合计258个工程文件构建前后SHA-256一致；IL2CPP/Gradle构建退出0。成功日志为UnityGame/Logs/build-android-flow.log。
- 新APK已复制到UnityGame/Builds/Android/YanyiSandpan.apk，44,601,460字节，SHA256为b47acb3ce4896f255ef1bda013138c7e753f3e767f644148c1bf6fe76a59c100；478项ZIP CRC通过、v2签名通过。包名com.course.yanyisandpan、版本0.1.0、minSDK26/target36、ARM64，沿用课程调试签名。
- 本轮未使用Computer Use、不改写玩家通关或音效设置；主菜单仍读取音效开关。adb未连接手机，真实触控、刘海/后台、性能、听感及五关人工玩法保留待验。

下文为历史阶段记录；当前产物以本页顶部Boss突破容错节与apk-verification.json为准。
## 此前黄巾样板关与共用体验验收
- 已接入真实下一波/本波情报、敌型/入口、Boss生命与阶段/蓄势时间，选将参考覆盖、选中底座、升阶收益和详细胜败结算；共用交互覆盖五关，黄巾新增低对比丘陵、田野、营寨与旗帜。未改经济、敌军数值、道路、阵位或v3/v4源稿。
- 攻击反馈按技能区分斩击、穿透、箭、火与控制；普通数字最多14个，总池最多90个。命中按TargetEnemyId准确匹配，退场0.24游戏秒，暂停和倍速沿会话时间。密集战斗名签独立显示，同深度Boss优先显示。
- 实际修复并复验：Unity6.3自定义Graphic需显式要求CanvasRenderer；终局不足0.12秒的最后帧也强制刷新HUD；终局清理退场残影与短暂反馈。道路圆内/外、相切、清空和触控穿透的真实网格检查通过。
- 根任务独立执行59项规则测试，0失败；内容verify-only通过：25角色、5关、43波、178阵位、73资源，历史美术基准未变。
- `-Target Ui`退出0：五关招募、差额、升级、满级与翻页；黄巾完整合法经济胜利、空阵失败、倒计时/余敌、暂停、Boss预警、终局统计及三个结算按钮均通过。18张真实uGUI图在docs/ui/runtime-polished，覆盖1920×1080、1280×720及2400×1080，已目视复核。
- 黄巾完整UI参考为408.0游戏秒、城防30、击退124、0漏兵、支出2723；空阵为237.1游戏秒、城防0、击退0、漏兵26、支出0。纯规则另有替代阵容376.9秒、提前升黄忠二阶407.4秒两套满城防合法解法；这些数据不等于人工乐趣或胜率验收。
- Windows构建退出0；Android构建前后223个Assets文件、合计246个工程文件SHA-256一致，仅同步22个新增或修改文件。Android IL2CPP/Gradle构建退出0，成功日志保存为UnityGame/Logs/build-android-polished.log。
- 新APK已复制回UnityGame/Builds/Android/YanyiSandpan.apk：44,597,980字节，SHA256为342fce75f7de4be39bef5eb24c76c343b28066ef887db3d2ddec210302643309。478项ZIP CRC通过、v2签名通过；com.course.yanyisandpan、0.1.0、API26起、目标36、ARM64。
- 全程未使用Computer Use。adb未连接手机；触控、安全区、后台恢复、真机性能/听感及五关人工玩法仍待设备与人工验收。批量离屏模拟的D3D12上传缓存提示不作为手机性能数据。

下文保留此前版本的实施与产物记录，当前结果以上述本轮验收及apk-verification.json为准。

## 此前战斗UI精修验收
- 根据用户运行截图修复顶部人物与名签裁切、长说明覆盖战场；六卡一屏增加费用及差额标签，阵位按选择状态显示，升级缺钱与满级状态明确。v3/v4源稿及Domain配置不变。
- 用户要求尽量不用Computer Use；中止首次桌面启动操作后，完全使用Unity内部自动检查。`-Target Ui`退出0，实际检查五关178阵位×三阶及25名武将字形，真实按钮回调完成招募、收入刷新、升级、满级和翻页。
- 输出9张真实uGUI离屏渲染图，覆盖五关、空状态、满级、1280×720及2400×1080。已目视复核，修正了细线射程、地图边缘和缩放按钮接缝。
- 50项规则测试、内容verify-only通过。最新Windows构建退出0；Android英文副本211个Assets文件逐一SHA256一致，IL2CPP/Gradle构建退出0。
- 当前APK为44,567,196字节，SHA256为351e568f6d827039e5137bdf9fcb79a8d43e781751dd7dc6939feaccf725a6ad；478项ZIP CRC通过、v2签名通过，包信息保持com.course.yanyisandpan、0.1.0、API26起、ARM64。
- 成功日志为UnityGame/Logs/build-android-ui.log，APK已回写UnityGame/Builds/Android/YanyiSandpan.apk；adb未连接设备。自动渲染及按钮回调不能替代手机触控、安全区、系统返回/后台与性能实测。
- aapt无法读取中文APK路径，已在同指纹英文源文件读取包信息；初次Python内联引号被PowerShell消耗，改用临时脚本完成只读CRC检查，最终均退出0。

下文保留此前首版实施过程；旧APK指纹与205文件计数属于历史记录。

## 已授权内容
- 用户批准按工程方案开始实施；同意安装Unity与Android构建组件，并要求优先避免占用C盘。
- A盘虽有约101GB空余，但当前用户仅有读取权限；F盘固定NTFS磁盘约66GB空余且有修改权限，工具安装、下载和构建组件放F:/Development/Unity。
- 技术锁定Unity 6.3 LTS 6000.3.28f1，官方API发布记录核验；C#规则可先用系统自带.NET Framework编译器验证。

## 执行分工
- [x] 工具链下载：14个官方文件、约6.83GiB，清单尺寸、4项官方哈希、11个ZIP CRC及3个EXE签名验收通过。
- [x] 环境安装：用户手动安装Hub、编辑器及Android模块；补齐已授权官方ZIP，许可、编译及实际构建通过。
- [x] 规则：纯C#战斗、25种技能、时钟、波次、Boss、招募升级与自动化测试；根任务最终独立运行50项通过。
- [x] 内容：25人物、5关、43波、178阵位与73张运行时PNG，配置及资源检查通过。
- [x] 展示源码：菜单、情报、选将、战斗、暂停、结算与本地记录，另有动画及音效；桌面主要操作已实测，结算仍待补验。
- [x] 集成：Unity编译、桌面运行画面与Windows、Android构建通过。
- [ ] 实际设备：手机安装启动、触控、适配及性能验证。

## 接口审查
| 任务 | 共享接口 | 处理 |
|---|---|---|
| 规则与展示 | runtime-contract.md中的BattleSession与运行态 | 数据字段由根固定，运行态接口由规则任务实现；展示不直接修改战斗数值 |
| 内容与规则 | Definitions.cs的PascalCase字段与Skill字符串 | 内容、规则代理直接同步，JSON不引入Unity外依赖 |
| 内容与展示 | Resources路径与人物台座锚点 | 人物Game/Characters、地图Game/Maps、特效Game/Effects；字体由根任务准备 |
| 规则任务内部 | C#5与现有系统编译器 | 避免较新语法，控制台测试不依赖NuGet安装 |
| 展示任务内部 | uGUI与中文字体 | Unity官方uGUI包，字体使用Noto开源字库；不使用额外UI框架 |

## 工程决策
- 仓库无初始提交，已有资料均未跟踪；不擅自提交资料以制造worktree，改在codex/unity-game分支的新UnityGame目录实施。
- 此轮按已批准方案的5关示例制作可运行初版；后续反馈可调整内容，不将样板关六人限制作为最终规模。
- 普通角色与Boss共用身份，战斗配置分开；静态图中四个已部署位置按空阵位初始化。
- 素材重新生成独立运行时资源，已认可v3/v4不覆盖；字体来源及许可随工程保留。
- 安装目录按磁盘ACL检查由A盘调整到F盘，沿用用户已授权的非系统盘安装范围；不改变磁盘权限。

## 验证边界
当时主工程状态：F盘6000.3.28f1_8f082d2fe476已安装，真实导入、C#编译及25人5图资源Verify均退出0。启动环境缺少ALLUSERSPROFILE导致UPM错误，已通过英文副本、空工程及单变量补齐定位；Build-Game.ps1仅补齐缺失的子进程变量，原命令复验通过。规则测试当时50通过、0失败；正确版本Windows构建及主要操作已实测。Android模块、SDK、NDK、JDK就绪，APK已成功构建并验签；真机尚未验收。下文保留历史过程，当前结论以本页顶部Boss突破容错节为准。

### Android安装包验收

- 在F:/Development/Unity/BuildProjects/YanyiSandpan-6000.3.28f1英文路径副本构建，规避Android工具对中文工程路径的明确限制。205个Assets文件再次逐一SHA-256核对一致，后续修改主工程须重新同步后构建。
- Build-Game.ps1 -Target Android -ProjectDirectory指定上述目录，ARM64 IL2CPP及Gradle打包完成，进程退出0、Unity日志Build Finished, Result: Success。
- 产物已复制到UnityGame/Builds/Android/YanyiSandpan.apk，实际44,561,380字节（42.50MiB），478个ZIP条目CRC全部通过。Unity构建报告totalSize为资源报告数值，不作为APK文件实际大小。
- aapt dump badging确认应用名演义沙盘，包名com.course.yanyisandpan，版本0.1.0，minSDK26、targetSDK36、arm64-v8a。apksigner verify退出0，APK v2签名有效；使用默认课程调试签名。
- 源与目标SHA-256均为8db583c94f19729d8095bfc9984ed4b66bee9ad34c8ce1a761138282db56b0db。成功日志另存UnityGame/Logs/build-android-success.log，未覆盖原中文路径失败日志；验收记录为UnityGame/Builds/Android/apk-verification.json。
- adb设备列表为空，未宣称真机安装、触控、安全区、帧率、内存或发热通过。五关人工难度及结算完整流程仍待验收。

最新补充：Hub已安装到F盘，现有编辑器为C盘6000.6.8f1且无Android模块；用户选择继续手动安装已下载的6000.3.28f1到F盘。主工程保持6000.3.28f1。F盘独立6000.6.8f1副本已完成真实导入、C#编译、配置验证和Windows构建，均退出0；已启动并查看主菜单及五关选择。日志分别为副本Logs/verify-6000.6.log及Logs/build-windows.log。下文“尚未执行安装”等为此前阶段状态，Android和真机仍未验证。

纯C#测试、Unity编译、编辑器运行、Windows试玩、APK构建、Android真机分别记录，不互相替代。当前adb列表为空，未发现已连接手机。

## 根任务独立复跑结果

| 检查 | 结果 |
|---|---|
| `./UnityGame/Tests/Run-Tests.ps1 -Simulate` | 最终编译与执行退出0；50项通过、0失败，含终局参考布阵；通用自动布阵结果见下表 |
| `tools/build_game_content.py --verify-only` | 退出0：25角色、5关、43波、178阵位、73资源，历史美术基准未变 |
| PowerShell语法解析 | Build-Game、Install-Unity及Run-Tests无解析错误，未执行安装器 |
| 静态素材目视 | 已查看content-roster.png和maps-review.png，不是运行截图 |
| 独立代码与需求审查 | 修正一项确定P1：Presentation错误引用Unity.ugui，改为UnityEngine.UI；未发现其余确定P1/P2代码问题 |

uGUI保留2.0.0。依据[Unity 6000.3官方程序集定义](https://raw.githubusercontent.com/Unity-Technologies/uGUI/6000.3/com.unity.ugui/Runtime/UGUI/UnityEngine.UI.asmdef)及同分支package.json；根任务已检查本地修正后的JSON。根任务重复请求raw.githubusercontent.com发生超时，官方源码证据来自独立审查任务的成功读取。

| 关卡自动布阵 | 结果 | 游戏时长 | 剩余城防 |
|---|---|---:|---:|
| 虎牢合战 | 第5/10波张辽突破，失败 | 211.6秒 | 20 |
| 黄巾演武 | 第8/8波胜利 | 408.0秒 | 30 |
| 官渡沙盘 | 第9/9波胜利 | 427.2秒 | 26 |
| 赤壁两岸 | 第8/8波胜利 | 479.4秒 | 17 |
| 群雄终局 | 第8/8波司马懿突破，失败 | 385.7秒 | 20 |

该历史策略按路线长度归一化并倾向补充覆盖较少的路线；它会改变所有角色的位置。旧策略曾完整通过虎牢，故不以单一模拟判断玩家胜率。该阶段Boss突破直接判败，已由本页顶部的Boss突破容错规则替代；当时未宣称全关平衡或玩法乐趣已经验收。

### 终局参考布阵回归

独立有界诊断尝试40组布阵与消费队列后，最佳方案已击退关羽及吕布，但司马懿剩245/2100生命。先以单变量内存试验将司马懿本场生命设1800，确认方案32正常通关，再仅将正式终局出场HealthScale从0.7改0.6；基础MaxHealth3000、技能、路线、经济、其他关卡均不变，同时将终局推荐名单同步为该方案10人及顺序。

新增真实目录回归“终局参考布阵可通关”，根任务最终重新执行并得到370.8秒Victory、城防29、三Boss均被击退；逐帧军饷账一致，未修改运行态生命或收入。具体阵位及招募升级队列见[测试说明](../../UnityGame/Tests/README.md)。通用自动布阵仍在终局末波失败，如上表保留。

最终数据库SHA256：`cec1b9f9cce664713dfbb9eaf014693f41c9beecd144e1c9995c3ca0ec8e3ce1`。内容检查再次通过，旧稿、字体及图像指纹未变。

## 安装与网络阻塞

最终下载状态：14/14文件全部下载并独立验收通过，下载任务退出0。校验报告位于`F:/Development/Unity/Downloads/download-verification-6000.3.28f1.json`，`AllPassed=true`，失败和待完成均0。总计7337847462字节；4项官方integrity匹配、11个ZIP完整CRC通过、3个EXE均为Unity Technologies SF有效签名。其余10项无官方integrity，报告明确NotProvided。根任务重读报告并逐项复核14个实际文件尺寸，总字节数匹配。尚未执行安装，F盘余约49.98GiB。

以下保留此前网络及分阶段下载情况；当前状态以上述最终结果为准。

最新网络状态：用户切换境外节点全局模式后，编辑器与Android支持包的国际官方链接均已实测HTTP206，不再地域跳转。国际Hub3.22.5已完整下载，根任务确认官方SHA512及Unity Technologies SF有效签名；旧区域Hub3.3.7保留。编辑器安装包也已完整合并，4208792576字节，官方MD5 aa4e924970ec074fe7da2cb711b3fd49匹配，独立Authenticode验签Valid、Unity Technologies SF发行。Android支持和子模块继续下载，尚未执行安装。以下404记录为此前状态。

- 官方Unity Hub安装文件在`F:/Development/Unity/Downloads/UnityHubSetup.exe`，约131MB；有效发行者签名为优三缔科技（上海）有限公司，官方SHA512匹配。
- 编辑器6000.3.28f1官方链接地域重定向至download.unitychina.cn后404；未使用非官方重打包编辑器。
- 自动运行安装准备脚本被工具审批拒绝，只返回`blocked by policy`，没有细分原因。已告知用户，不换工具、GUI或命令形式绕过。
- 用户现已开启系统代理，Windows设置及实际请求已确认；官方版本API的GET请求HTTP200，但编辑器和Android包仍被302转至国内下载节点并最终404。带查询参数的最小Range请求同样失败，尚未开始数GB下载。已请求尝试非中国大陆节点的全局模式或明确下载域名路由。
- 官方Hub清单经代理访问成功，现有安装包数字签名Valid且SHA512再次核验一致。代理不会解除独立的安装执行审批拦截，本轮未运行安装器。
- 中文字体已通过CDN下载并核对官方Git blob SHA，附OFL许可；不再阻塞内容导入。

分段工具：`tools/download_unity_packages.py`。读取F盘官方包清单，默认4并发、8MiB；现场使用`--backend curl --workers 8 --chunk-mib 8`，复用已完成缓存。只下载、验证和合并，不执行安装器。编辑器和Android支持包按官方MD5校验，Hub按SHA512，Command Line Tools按SHA384；其他子模块无官方integrity时明确仅验证尺寸。curl逐跳限制官方HTTPS、限制响应体大小，每段尝试总时限90秒，最多3次。原8项及新增6项下载回归由根任务复跑全部通过。所有分段保留，无自动清理或覆盖已有不符文件。

官方Hub文档说明：只有通过Hub安装的Editor才能使用Add modules，Locate手动安装的Editor不提供该能力（[官方说明](https://docs.unity.com/en-us/hub/add-modules)）。Android支持EXE之外还需11个独立必需ZIP；不能将运行该EXE等同完整Android环境安装。当前未查到Hub3.22.5可直接复用预下载文件的官方离线安装参数，不提供猜测命令。

## 下一轮P1验收

1. Unity安装、许可、Verify及APK构建已完成；后续代码变更后同步英文构建副本再验证构建。
2. 补验主菜单至结算的完整路径及配置缺失、后台恢复；未选阵容、军饷不足、暂停已完成桌面实测。
3. 五关人工布阵试玩、Boss预告理解与难度调整，核实单局时长和操作反馈。
4. 使用已验收APK进行手机安装、刘海安全区、小屏触控、帧率、内存及发热检查。

准确安装、操作、测试与构建步骤见[UnityGame/README.md](../../UnityGame/README.md)。
