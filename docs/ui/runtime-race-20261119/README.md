# 布阵竞速UI验收图

Unity 6000.3.28f1实际编译和uGUI渲染，10组检查、177项断言通过；本次运行产出78张图。清单以[verification.json](verification.json)为准，脚本只列本次实际生成文件，避免旧运行遗留图混入通过数。

| 范围 | 对应文件前缀 |
| --- | --- |
| 主菜单、选关无纪录／有竞速纪录 | main、levels-empty-record、levels-race-record |
| 空名单／推荐名单 | roster-empty、roster-recommended |
| 首次倍率说明与运行中随时规则 | speed-intro-preparation、rules-running-paused |
| 战斗空部署／运行／暂停／缺钱／满阶 | battle-empty、battle-running、battle-paused、battle-unaffordable-or-max、battle-max-tier |
| 满10将、密集普通兵与Boss | battle-dense-boss |
| 首次、慢于、相同、刷新 | record-first、record-slower、record-equal、record-improved |
| 首次及Boss突破普通胜利的资格／复盘 | race-first-record、boss-breach-victory及其-details |
| 保存失败、摘要翻页、失败、末关胜利 | storage-save-failed、summary-next-page、battle-defeat、finale-victory |
| 实际页面套用模拟安全区 | safe-battle-asymmetric、safe-intro-right-notch、safe-result-bottom |

前25类状态按1280×720、1920×1080、2400×1080生成75张；另3张为2400×1080的安全区模拟：左右不对称预留、右侧刘海、底部84像素预留。安全区图真实调用SafeAreaLayout，并核对实际页面四角；仍不能替代设备显示和握持验收。

检查包括实际按钮回调与GraphicRaycaster顶层命中、文字无裁切、暂停冻结、后台不自动恢复、说明确认已读与失败重读、沿用名单重新付费、旧会话／重复回调隔离、严格更快比较、保存失败不报刷新、分页以及失败／末关禁用导航。旧普通记录118断言和原BattleResultView兼容也在专项／VerifyProject内验证。

**夹具边界：**0.033～6秒结算来自短路线／少量Boss隔离夹具，用于验证记录逻辑，并非正式关卡通关时间。密集图的10000开局资金与普通兵高生命仅存在于内存压力夹具，用于同时展示10将、45兵与Boss；正式database.json及真实经济策略模拟没有改数值。正式五关竞速参考见[策略报告](../../engineering/race-strategy-reference-20261119.md)。

本批没有进行真人乐趣、真实听感、Android触控／帧率／发热或实际PlayerPrefs跨进程验证。完整结果见[实施验收](../../engineering/race-implementation-acceptance-20261119.md)。
