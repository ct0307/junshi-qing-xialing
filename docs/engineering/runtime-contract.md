# 首版运行时接口与并行分工

用户已批准开始实施Unity方案；预计还有一个多月。独立新工程位于UnityGame，已在codex/unity-game分支开发。仓库没有初始提交，现有设计均为未跟踪文件，因此不为创建worktree擅自提交用户资料；以新子目录和分支隔离实现。

## 数据契约

UnityGame/Assets/Scripts/Domain/Definitions.cs是配置字段唯一依据。JSON使用完全相同的PascalCase字段。坐标为1920×1080逻辑画布，左上角原点、Y向下；地图安全区域约(30,133)-(1890,849)。角色/敌军/地图资源均通过Resources相对路径加载，不含扩展名。

LevelDefinition.CombatRuleVersion为可选字段。空值保留旧army/solo规则，非空由BattleSession.ResolveRuleVersion(level,factionEnabled)追加-army/-solo；普通最快/最近及竞速均使用实际版本，不能只隔离竞速。黄巾样板为sensation-hj-mn-v1，其余关卡保持缺省兼容。

开发诊断通过EnableCombatDiagnostics(capacity)在备战显式启用，默认Diagnostics=null；最多16384条，只读索引、Truncated和DroppedCount。CombatDiagnosticEntry为值类型，含原序列/EffectId/实际生命前后；只累加Kind=Damage的ActualDamage，Kind=Kill不重复计伤。ExportCombatDiagnostics仅终局复制导出，禁止逐击写盘。首批可信归因为吕布/周瑜/诸葛直接、周瑜火区/余烬和诸葛迟滞，其他伤害保留未归因或传伤标签。

25个角色ID与Skill对应：guan_yu/focus，huang_zhong/sniper，xu_chu/duelist，taishi_ci/twin，lv_bu/cleave，zhao_yun/pierce，zhou_yu/fire，lu_xun/spread_fire，yuan_shao/rain，guo_jia/mark，zhang_fei/stun，zhuge_liang/slow，ma_chao/knockback，zhang_jiao/chain，lv_meng/ambush，cao_cao/buff，diao_chan/vulnerable，pang_tong/link，xun_yu/assist，sima_yi/disrupt，sun_shangxiang/execute，zhang_liao/splash，jiang_wei/adaptive，hua_tuo/heal，lu_su/supply。

## 战斗接口（Domain，纯C#5兼容，可用已有.NET Framework编译）

- BattleSession(GameDatabase database, LevelDefinition level, IEnumerable<string> roster)
- BattleState State，枚举Preparation/Running/Paused/Victory/Defeat
- int Gold、CastleHealth、CurrentWave、TotalWaves；float Time、MarchTime、Speed；double ElapsedSeconds；int SpeedMask、BossBreachCount
- Time为固定实秒规则时钟，武将普攻、独立特色技能、蓄力、灼烧、控制与Boss技能使用它；MarchTime按每段实际运行的1／2／3倍进军增量推进，只影响移动、出兵、波间和计时军饷。切速不追溯重算旧增量。
- ElapsedSeconds从Start后累计有效前台运行实秒，不乘倍率；备战、暂停、后台和终局冻结，固定步欠账不缩短计时。SpeedMask仅实际运行过的1／2／3倍分别置位1／2／4，暂停空切不计。BossBreachCount累计实际突破Boss数，包含于LeakedCount。
- 抵城统一扣EnemyDefinition.CastleDamage并移除敌军，仅CastleHealth归零时失败；当前Boss扣15、普通敌军扣1～2，总城防30。Boss突破不发kill或击退赏金；城防仍在时继续处理存活／待出兵与后续波次，最后一波清空后可胜利。
- bool IsWaveActive；float NextWaveCountdown；int RemainingEnemies：只读波次情报。准备阶段和波间倒计时为当前进军倍率下的预计实秒，切速立即换算；活跃波和终局倒计时为0。余敌包含本波待出兵，不含未来波。
- int DefeatedCount、LeakedCount、GoldSpent；string DefeatReason：累计击退、漏兵、成功消费与实际失败原因；取出事件不清空统计，非失败状态原因为空字符串。
- List<HeroUnit> Heroes；List<EnemyUnit> Enemies
- string TryRecruit(string heroId,int slotId)，string TryUpgrade(int slotId)：成功返回null，失败返回中文原因，不产生副作用
- int AvailableSupplyAdvance、bool CanAffordTransaction(int cost)、int GetTransactionShortfall(int cost)：无副作用查询本波三阶鲁肃合法补给余额、含余额的可支付性及真实差额；预支与波末使用同一等级预算及100全局上限。UI不能只以Gold禁用本来可预支的升级。
- string TryMoveHero(int fromSlotId,int toSlotId)：仅Preparation可以移动同一HeroUnit；成功返回null，原等级、军饷、GoldSpent及已招募集合保持；占位或无效阵位失败无副作用。开战后含暂停与终局均拒绝。
- static string GetAttackTimingDescription(HeroDefinition definition,int heroLevel)：与真实战斗共用独立普攻、技能冷却／蓄力计算；协射与伏击显示触发规则。保留省略等级的一阶重载，不能直接用旧AttackInterval推断实际节奏。
- static float GetMaximumRange(HeroDefinition definition,int heroLevel)、static string GetGrowthDescription(HeroDefinition definition,int heroLevel)与GetUpgradeDescription(slotId)共用HeroGrowth解析；覆盖和升阶预览使用真实当前等级，升级不重置普攻或技能冷却。
- HeroDefinition新增BasicAttackInterval、BasicDamageScale、SkillCooldown、SkillDamageScale四个显式预算；旧配置为0时按旧基准兼容。25人正式预算由内容生成器写入；Damage及AttackInterval保留基准语义，二三阶的主要收益由各自机制提供，基础威力每阶增加12%。试玩反馈v2仅把25项BasicAttackInterval缩为v1的70%，正式配置0.420～0.770秒，固定步持续目标实测约0.433～0.800实秒；单次伤害、技能、Boss、经济和波次不变。v1原0.60～1.10秒保留为历史规格，不用于当前验收。
- BreachRecord LastBreach：最近一名敌军抵城移除前的名称、路线ID、生命／最大生命和Boss标记快照；DrainEvents后仍保留，新会话初始为空。
- void Start()，void SetPaused(bool paused)，void SetSpeed(int speed)，void Tick(float realDeltaTime)
- List<BattleEvent> DrainEvents()：取出并清空事件
- HeroUnit：public HeroDefinition Definition；public int SlotId、Level；public Point2 Position
- EnemyUnit：public int Id；public EnemyDefinition Definition；public Point2 Position；public float Health、MaxHealth、Progress、SlowUntil、StunUntil、WarningUntil；public bool Enraged
- BattleEvent：public string Kind、HeroId、Text；public Point2 Position、Target；public float Amount；public int HeroLevel；public float SkillRange。等级与范围为发动时快照，避免延迟技能被后续升级改画。OccurredAt统一为事件发出时的会话有效实秒，延迟消费不能以收件时刻重新开始动作或短反馈寿命。
- 反馈附加字段：TriggerHeroId为协射触发友军或连环原攻击者；SourceEnemyId为传伤初始敌军；EffectId为稳定组ID；ExpiresAt为原到期；ReplacedEnemyId为退出成员；RelatedEnemyIds与RelatedPositions为事件发出时有可信位置的存活成员副本，按索引对应。嵌套死亡中已经移除的成员不输出默认坐标，不更改玩法组集合。
- BattleEvent.TargetEnemyId：basic_attack/attack/hit/fire/stun/slow/kill/leak及boss_spawn/boss_warning/boss_phase填写真实敌军实例编号；协同攻击保留实际目标。无具体敌人的事件为0。展示层按编号查找存活或当帧退场对象接收受击，不按相邻位置猜测目标；敌军移除后的hit/kill/leak仍保留该编号。
- Kind采用recruit/upgrade/move/basic_attack/attack/hit/fire/stun/slow/boss_spawn/boss_warning/boss_phase/kill/leak/wave/victory/defeat/notice，新增纯展示语义link_created/link_replaced/link_hit。link_hit.Amount是接收者按原公式结算的伤害计算值，包含接收者减伤，原目标未按剩余生命封顶；不代表剩余生命实际扣除值。它不能再次触发伤害或重复命中数字。basic_attack只代表快普攻，attack保留特色技能／协射等语义，不能每次普攻都绘制重斩扇形；boss_spawn与原notice同时发出，保留原文字提示。move的HeroId为移动武将，Position为起点、Target为终点、Amount为0、TargetEnemyId为0。
- `ReadCombatFeedback(List<CombatFieldSnapshot>,List<EnemyFeedbackSnapshot>)`清空并复用调用者容器，复制真实区域形状、来源、位置／半径／路线距离区间／Until及敌军晕／缓／燃／链状态与截止；`ReadLinkFeedback(List<LinkFeedbackSnapshot>)`按组输出当前存活成员ID和位置副本。查询不推进时钟、不消费事件、不暴露内部可写引用；终局返回空。
- CatalogValidator.Validate(GameDatabase database)返回List<string>配置问题

Preparation允许部署和升级，Start后以1/30实秒固定步推进，并保存各段倍率用于进军增量。暂停/结算禁止部署和升级。UI不直接修改Gold/Health/Level。路径使用配置中的采样中心线，所有路线应含通往城门的完整点列。

## 精品样板展示接口

- `BattleIntelView(ui,parent,database,level)`及`Refresh(session)`读取真实波次、敌型、入口、生命与蓄势时间，不消费事件。
- `BattlePlacementGuide.Show(level,position,radius)`只高亮射程圆内的真实道路，`Clear()`清空；手机选将后显示参考位置，点击阵位才招募。圈线表示技能最大覆盖，特殊技能的实际触发仍由规则层决定。
- `HeroSkillPreview.Show(definition,level,origin,target)`从HeroGrowth读取当前及下一阶的枪线宽度、扇面／半圆／整圈、横扫与震慑范围；金色为当前、青色为下一阶。方向示意按参考目标绘制，实际技能朝真实敌军；外围圆仅表示最大射界，不表示每次整圈命中。
- `HuangjinScenery.Create(ui,ground,level)`仅增加黄巾静态郊野层，单网格、不接收触控，不修改道路或阵位。
- `BattleEffects.Emit(event,hero)`按技能选择反馈，普通数字同时最多14个，总反馈池最多90个。`Advance(gameDelta)`使用会话时间差，暂停为0。
- `BattleAttackGraphic`仅在真实cleave／pierce技能事件短暂显示扇形／贯穿带，按HeroLevel和SkillRange快照解析半圆／整圈及枪线宽度；普攻使用独立弹道。共享主轨迹仅按武将合并；双矢、箭雨、链电等独立目标按武将及TargetEnemyId分别显示。
- `BattleFeedbackState(.7f)`按kill.OccurredAt聚合0.7实秒固定窗口，3／6／10档，只计本局唯一有效TargetEnemyId；不续窗、不归因武将、不增加收益、不用hit／leak补数。每批结果共用于图形与音频。标签字号29／33／37、文本框210×64及深色细描边，Clear清去重和窗口，回池数字恢复普通文字参数。旧Jade包保留0.4秒。
- `SyncCombatFeedback`按规则快照绘制人物下方真实圆区／道路火带，与短线共用90对象池；`GameAudio.EventBatch`按最高跨档合并，同批替代普通击退音，0.2秒门限与原Cue冷却并行。超过0.25秒的普通攻击／命中／火／控制短音在占用冷却前丢弃；Boss与终局沿用关键门控。
- 胜败确认后立即清理退场单位、区域、短暂反馈和连破状态；失败背景的存活敌军同时清空状态文字及蓄势提示，下一局重建会话与展示对象。
- `UnitView.Hit(time)`、`Retire(time)`、`IsRetiring`、`IsRetired`由同一游戏时钟推进；敌人规则移除后继续展示0.24秒退场，结束后回池。BattleView保留当帧退场索引，致命hit仍可传给该对象；Hit允许退场中记录受击，Animate先计算0.12秒闪亮再推进退场，暂停冻结。`SetDefinition`重置池复用状态；终局仍立即清理，不延迟计时或成绩保存。
- `BattleResultView.Create(ui,parent,level,session,retry,chooseRoster,next,returnLevels,recordResult)`展示真实统计和原因并转交回调；最后的VictoryRecordResult为可选参数。`GameFlow.CompleteBattle`仅接受当前会话的首次终局，胜利时交由`GameProgress.RecordVictory`保存并传入比较结果。

## 页面流程与平台入口

- `GameFlow(ui,root,database,progress)`持有菜单、当前战斗和弹窗。`Tick`推进展示；`HandleBack`逐页返回，备战询问是否离开，运行时暂停，暂停时恢复，终局返回关卡。
- BattleView顶部提供可见“返回”按钮并调用同一HandleBack；备战确认中的“继续布阵”保留会话、守将、军饷与消费，可选重新选将或返回关卡。返回暂停不累计ElapsedSeconds。
- `CampaignLevels`是黄巾优先、其余维持原顺序的只读关卡集合，选关卡片和胜利后下一关共用它；不修改database.Levels。黄巾未通关时BattleView在既有提示区讲解前三波，第四波及首次通关后关闭教学。
- BattleView按武将ID保持UnitView，调位同步阵位索引、位置、名签、选中与覆盖；真实敌军来源的Boss notice与普通武将以Boss为目标的notice依据HeroId区分，普通技能不覆盖有效预警或前三波教学。
- 底部五卡两页，固定成长预览使用当前／下一阶机制与同源资金查询；HUD显示有效实秒、进军倍率与当前每实秒收入。通知保护使用Session.Time，暂停不消耗预警展示时长。
- 武将详情仅在自行暂停的Running会话关闭时恢复；返回优先关闭详情，系统暂停先取消详情自动恢复权并进入暂停页，重开／终局／销毁不恢复旧会话。最快记录显示mm:ss.fff，实时HUD保持mm:ss。
- `PauseForSystem()`只暂停运行中的会话，重复调用不叠框；恢复前台不自动继续战斗。
- `GameProgress(readInt,writeInt,save)`提供`IsCleared`、`GetBestCastle`、`GetFastestRecord(levelId)`、`GetFastestRecord(levelId,ruleVersion)`、`GetRecentRecord(levelId)`和`RecordVictory`。运行使用PlayerPrefs，自动检查使用内存字典。
- `RecordVictory(levelId,castleHealth,elapsedSeconds,speedMask,leakedCount,bossBreachCount)`返回VictoryRecordResult，先捕获PreviousRecord再比较本次CurrentRecord；只有严格更快更新BestRecord。每次有效胜利都更新本版最近通关完整快照，较慢或相同也保存。原两参重载继续保留通关／最佳城防，不能从旧城防推算通关时间。
- `FastestRecord`保存long整数毫秒、SpeedMask、CastleHp、LeakedCount、BossBreachCount和SpeedSummary。`VictoryRecordResult`提供HasValidTime、IsFirstRecord、IsNewRecord、IsEqualRecord；结算分别显示首次、刷新差额、未刷新／与最佳相同，并说明已保存最近通关。
- 当前独立军势版为`faction-army-v1`；最快键`yanyi.fastest.faction-army-v1.<level>.`，最近键`yanyi.recent.faction-army-v1.<level>.`。每份快照包含ready/msHigh/msLow/mask/castle/leaks/bosses/checksum，最后提交完整标记，非法或损坏记录按无有效时间处理。growth-tempo-v2最快以“旧版最快”展示；v1/v2全部原键、旧`yanyi.clear.*`与`yanyi.best.*`保留，不复制为新版记录。所有本版合法胜利均可记录，包含Boss突破后城防仍在的胜利。
- 选关卡片分别显示“本版最快”“最近通关”，有有效growth-tempo-v2记录时另显示“旧版最快”；重建GameFlow继续读同源存储，不把旧版或较慢最近局覆盖成本版最快。纯规则旧夹具默认关闭军势，版本仍为growth-tempo-v2；产品BattleView四参数构造显式启用军势，本版不提供运行时切换。
- `YanyiGame`负责加载内容、系统输入、`OnApplicationPause`/`OnApplicationFocus`和屏幕适配。内容失败保留`ContentErrorView`及重试按钮；没有流程时返回键和系统回调不导航。
- `SafeAreaLayout.Apply(root,width,height,safeArea)`把1920×1080逻辑画布等比居中在有效安全区；异常区域回退为完整屏幕。自动检查使用实际RectTransform四角，实际设备的系统安全区仍需真机验收。

## 军势与升阶表现契约（2026-11-12）

- `HeroDefinition.Faction`固定为wei/shu/wu/qun；正式25将完整，旧空值兼容且不计，非空未知值校验拒绝。`BattleSession(..., bool factionEnabled=false)`提供只读`FactionEnabled`、`RuleVersion`、`GetFactionCount`、`GetFactionTier`、`IsFactionIsolated`；人数只按已成功招募的不同HeroId统计，阶位为0/2/4。
- 四人档替代两人档；魏仅普攻（含号令追加、不含协射），蜀仅对IsBoss或elite的直击，吴特色直击和持续，群仅距任意其他友军>=220的直击。魏蜀吴6%/12%，群8%/16%。军势不改控制、时长、箭数、经济、价格或冷却。
- 每次特色施放固定阵营、档位、孤立资格及序列，吕布在蓄力开始固定，后轮箭雨沿用。直击结算前只乘一次，火区／燃烧创建时增强且tick／传播／余烬不重乘；连环仍继承真实结算30%，不递归；协射独立判断自身资格。
- `BattleEvent`新增`AttackSequenceId`、`HeroLevel`、`FactionTier`、`FactionMultiplier`（默认1）。`skill_charge`含真实结束时间ExpiresAt；`skill_cast`每次特色释放一次；attack附同序列，后轮不重发cast。`faction_hit`仅说明实际增强直击，不再次伤害。
- 展示消费真实施放及等级；升级成功首帧同步军饷、卡牌和阶位，最新待强调阶位覆盖旧档，首次匹配真实cast后消费一次。不重置冷却，不制造技能。备战／运行的短提示用展示实秒，暂停冻结；Boss提示保持优先，终局清空。
- 常态无大姓名牌；卡栏常驻真实姓名／阶位，小阶徽遇其他人物或道路折叠。选中射界战斗短显，备战与暂停可查看；满血普通敌兵血条隐藏。群的距离与招募影响预览使用逻辑坐标，装饰不拦截阵位点击。
- 仍共用90特效／14数字／11声源及16次普通音效预算。普攻较轻，特色、升阶、军势分层；军势限频并丢弃过期短音，关键Boss预警优先。

## 音频表现接口

- `GameAudio.Initialize(owner)`建立服务与固定11音源；`AudioPreferences`可注入读写与保存委托，自动检查使用内存字典，运行使用PlayerPrefs。
- `MusicEnabled`／`SoundEnabled`、`MusicVolume`／`SoundVolume`分别保存。音乐默认50%、音效80%；旧`yanyi.audio.enabled`只迁移一次。
- `ShowMenu()`延续菜单曲；`BeginBattle(level.Id)`重置会话音频记录并进入备战；`SyncBattle(session)`读取真实准备、波间、Boss存活集合及胜败，不消费事件或改变规则。
- `Event(item)`由BattleView原有DrainEvents循环分发。短音按真实时间限频，普通6声、关键2声、UI1声；结算短句使用音乐双源并受音乐设置控制。
- `SetBattlePaused`保留音乐位置并停战斗短音；`SetApplicationPaused`与`SetApplicationFocused`独立维护系统挂起原因。YanyiGame在所有页面通知音频，不仅限运行中战斗。回前台不自动恢复战斗。
- 主菜单与暂停页共用`AudioSettingsView`；返回先关闭声音设置，再按原暂停层级处理。操作类按钮禁用通用声音，由实际成功或拒绝结果决定反馈。
- 资源契约：`Resources/Audio/catalog.json`含Version与28个Cue，音乐按需加载、34短音预载；`Audio/GameMixer`为Unity原生混音器。Domain与音频配置无依赖。

## 布阵竞速接口（2026-11-19）

普通通关和原ElapsedSeconds、SpeedMask、普通最快／最近接口继续保留。新主竞速使用独立RaceElapsedSeconds，累计实际完成的1/30实秒规则步，终局输入帧未消费部分不计；备战／暂停／说明／后台／终局不推进。显示毫秒是格式，不代表规则达到1毫秒精度。资格版本all-boss-v1固定本计时口径，战斗数值未变仍为faction-army-v1。

- `BattleSession.ExpectedBossCount`从Waves.Groups对应IsBoss与Count建立预期；`SpawnedBossCount`与`DefeatedBossCount`按真实实例去重。`IsRaceEligible`要求Victory、预期全部生成／真实击退且BossBreachCount=0；允许普通漏兵，不使用BossHeroIds数量或已消费事件反推。
- `GetRaceSnapshot()`返回不可变RaceSnapshot：LevelId、RuleVersion、QualificationVersion、ElapsedSeconds、SpeedMask、CastleHealth、LeakedCount、ExpectedBossCount、SpawnedBossCount、DefeatedBossCount、BossBreachCount、IsEligible、IneligibilityReason。`GetSpeedSeconds(int)`为各倍率已消费的有效实秒，暂停空切不记入；SpeedChanges为只读有效倍率切换，Actions为只读真实招募／升级／调位节点。
- 摘要最多保留256次倍率变化、64次操作，之后SummaryTruncated为true，时长仍完整累计；不是完整回放。动作保存Kind、HeroId、SlotId、Level、Gold及ElapsedSeconds，倍率变化保存Speed及ElapsedSeconds。
- `FirstBreach`保存第一名突破敌军的事实，`LastBreach`继续保存最近一名，二者使用不同副本；BreachRecord新增Wave、Gold、Speed、ElapsedSeconds。UI只陈述当时路线、剩余生命、军饷、倍率，不推断失败因果。
- `GameProgress.GetRaceRecord(levelId,ruleVersion)`与`RecordRaceVictory(RaceSnapshot)`使用独立yanyi.race前缀，关卡／战斗规则／资格版本隔离；旧数据不清除、不重写、不自动认证。标识符按分隔符安全转义，当前资格为all-boss-v1。
- `RaceRecord`不可变，含ElapsedMilliseconds、SpeedMask、CastleHp、LeakedCount、BossCount、版本／关卡ID、SpeedSummary及各倍率毫秒、轻量Actions／SpeedChanges与截断标记。`RaceRecordResult`先保留PreviousRecord再比较CurrentRecord，返回BestRecord、Snapshot、IsNewRecord、IsFirstRecord、IsEqualRecord、HasValidTime及IneligibilityReason；严格更快才覆盖，相同时间保留原摘要。
- 存储为32KB有界二进制摘要及校验、双槽最后切活动指针；读异常时禁止新写，写／保存失败不误报刷新。公共UI读安全返回空记录；这是本机损坏检测，不是防作弊或系统级事务。旧普通保存异常由GameFlow捕获并显示，不阻断竞速结果页。
- `HasReadMarchSpeedIntro`／`MarkMarchSpeedIntroRead()`为独立说明标记。只在确认后尝试保存，读写／保存异常安全回滚，失败下次仍可阅读。GameFlow统一管理规则模态、暂停与系统中断；系统中断后确认说明也不能自动恢复战斗。
- `GameFlow.AdjustRoster()`沿用上一局仍合法的名单进入选将，不带入免费部署、等级或剩余军饷；新会话仍正常招募。选将查看焦点、名单勾选与成长预览继续独立。
- GameFlow当前终局仅提交一次，拒绝旧会话回调；原普通RecordVictory继续写真实普通结果，新竞速单独提交。`RaceResultView`展示资格、本次／此前／差值、保存失败、首次突破、各倍率时长及分页摘要；旧BattleResultView.Create直接调用兼容保留。

验证入口：`Run-RaceTests.ps1`、`Run-RaceSimulation.ps1`与`Build-Game.ps1 -Target Race`。独立产物位于Race-20261119，不调用旧App／FactionArmy入口覆盖历史截图。实际验证与设备限制见[race-implementation-acceptance-20261119.md](race-implementation-acceptance-20261119.md)。

## 分工与文件所有权（历史并行边界）

- 规则任务：Domain除Definitions.cs以外的脚本，以及UnityGame/Tests/独立控制台规则测试。
- 内容任务：tools/build_game_content.py，UnityGame/Assets/Resources/Game/**及内容清单。25人素材允许复用绘图工具，但为新增角色提供可辨形象；不修改已认可v3/v4源稿。
- 展示任务：UnityGame/Assets/Scripts/Presentation/**，运行时自动生成可交互UI；纯uGUI+Text和可分发字体，避免尚未安装的外部UI依赖。
- 主任务：Unity工程设置、Editor构建脚本、启动场景、项目说明、安装和集成验证。

## 验收

先验证同关Boss排除、招募唯一性、扣费原子性、收入/暂停/倍速、敌军抵城、胜败与重开，再进行25人5关完整性和渲染交互验证。内容数量按已批准方案的5关示例实施，可继续调整。编辑器/字体使用官方来源，安装依赖另行取得许可。不得将无Unity的C#规则测试称为Unity或APK构建通过。

2026-11-06反馈v2证据：普通规则123项，根任务含模拟124项通过／0失败（UnityGame/Logs/domain-feedback-root.log）；4项成长内容及4项波次保护通过（content-feedback.log）。完整Unity UI日志ui-feedback-final.log退出0，记录118项内存断言、最近记录11×3=33张、反馈4×3=12张、原成长18×3=54张。手机、人工解压感与真实跨进程PlayerPrefs仍未验收；原成长设计保留历史，当前规则不与v1最快混排。
