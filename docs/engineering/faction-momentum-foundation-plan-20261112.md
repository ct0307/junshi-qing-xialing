# 阵营军势：领域与配置先行实施计划

> 历史先行计划，现已完成P2-A及后续P2-B，正式实现与证据见[验收](faction-army-acceptance-20261112.md)。下方代码块和清单保留计划时状态，不替代当前源码；后续完整实施记录位于 `.planning/faction-army-runtime-20261112/`。

**目标：** 为25将补齐固定阵营和可独立测试的军势查询，为下一批真实战斗接入提供可信输入。

**架构：** `HeroDefinition.Faction`承载归属；生成脚本为唯一来源；`FactionRules`只读取实际上阵武将和伤害上下文，不修改生命、经济、冷却或事件。通过独立规则测试验证计数、距离、伤害类型和数据契约，本批次不在`CombatSystem`调用军势乘数。

**技术：** Unity 6000.3.28f1、纯C#5、现有.NET Framework编译器、现有Python与Pillow，无依赖安装。

依据：[阵营军势完整规则](faction-momentum-design-20261112.md)。本计划仅覆盖P2-A，P1遮挡／攻击反馈仍按[已有方案](combat-readability-upgrade-design-20261112.md)优先执行；P2-B的伤害、快照、UI和成绩接入须按完整规则另拆执行计划。本计划完成不能称军势战斗版已上线。

## 文件职责

| 文件 | 变更 |
| --- | --- |
| `UnityGame/Assets/Scripts/Domain/Definitions.cs` | 在`HeroDefinition`增加`Faction`字段 |
| `UnityGame/Assets/Scripts/Domain/FactionRules.cs` | 新增纯查询：合法归属、人数、档位、强敌、孤立、乘数 |
| `UnityGame/Assets/Scripts/Domain/CatalogValidator.cs` | 拒绝非空未知阵营，允许旧配置缺省 |
| `tools/build_game_content.py` | 唯一归属映射及正式生成配置校验 |
| `tools/update_faction_data.py` | 新增只更新database.json的受控入口，避免重生成美术 |
| `UnityGame/Assets/Resources/Game/database.json` | 只增加25个Faction值，其余内容逐字段保持 |
| `UnityGame/Tests/FactionRuleTests.cs` | 新增独立领域／数据检查 |
| `UnityGame/Tests/Run-FactionTests.ps1` | 现有编译器下的独立测试入口 |
| `docs/engineering/runtime-contract.md` | 在验收后记录可选旧字段／完整正式配置的契约 |

`.meta`由Unity按项目既有方式生成并纳入最终输入清单，不手造GUID。当前仓库无HEAD且全工程未跟踪，不执行默认的`git add .`或提交步骤。

## 任务1：先写行为测试并确认失败

- [ ] 新增`UnityGame/Tests/FactionRuleTests.cs`，内容如下。目标是验证用户规则与数据，不是逐行镜像实现。

<!-- sample: faction-tests -->
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using YanyiSandpan.Domain;

internal static class FactionRuleTests
{
    private static int checks;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }

    private static void Near(float expected, float actual, string message)
    { Check(Math.Abs(expected - actual) < .0001f, message); }

    private static HeroUnit Hero(string id, string faction, float x, float y)
    {
        return new HeroUnit { Definition = new HeroDefinition { Id = id, Faction = faction },
            Position = new Point2(x, y) };
    }

    private static int Main(string[] args)
    {
        try
        {
            int[] counts = { 0, 1, 2, 3, 4, 5, 8 };
            int[] tiers = { 0, 0, 2, 2, 4, 4, 4 };
            for (int i = 0; i < counts.Length; i++)
                Check(FactionRules.GetTier(counts[i]) == tiers[i], "军势门槛不符");

            HeroUnit first = Hero("a", "shu", 0, 0);
            var deployed = new List<HeroUnit> { first, Hero("b", "shu", 300, 0),
                Hero("c", "wu", 600, 0), Hero("a", "shu", 0, 0) };
            first.Level = 3;
            Check(FactionRules.CountDeployed(deployed, "shu") == 2, "升级或重复身份不能重复计数");
            Check(FactionRules.CountDeployed(new List<HeroUnit>(), "shu") == 0, "未招募不计数");
            Check(FactionRules.CountDeployed(deployed, "unknown") == 0, "未知阵营不计数");

            HeroUnit isolated = Hero("q", "qun", 0, 0);
            HeroUnit neighbor = Hero("w", "wu", 219.99f, 0);
            var pair = new List<HeroUnit> { isolated, neighbor };
            Check(!FactionRules.IsIsolated(isolated, pair), "跨营近邻也应破坏孤立");
            neighbor.Position = new Point2(220, 0);
            Check(FactionRules.IsIsolated(isolated, pair), "220边界应合格");
            neighbor.Position = new Point2(220.01f, 0);
            Check(FactionRules.IsIsolated(isolated, pair), "超过220应合格");
            neighbor.Position = new Point2(float.NaN, 0);
            Check(!FactionRules.IsIsolated(isolated, pair), "无效位置不应获得收益");

            Check(FactionRules.IsStrongTarget(new EnemyDefinition { IsBoss = true }), "Boss应为强敌");
            Check(FactionRules.IsStrongTarget(new EnemyDefinition { Id = "elite" }), "明确精英应为强敌");
            Check(!FactionRules.IsStrongTarget(new EnemyDefinition { Id = "shield", MaxHealth = 99999 }),
                "高血量盾兵不能被推断为精英");

            Near(1.06f, FactionRules.GetMultiplier("wei", 2, FactionDamageKind.Basic, false, false), "魏2档");
            Near(1.12f, FactionRules.GetMultiplier("wei", 4, FactionDamageKind.Basic, false, false), "魏4档替换2档");
            Near(1, FactionRules.GetMultiplier("wei", 4, FactionDamageKind.SkillDirect, false, false), "协射不算普攻");
            Near(1.12f, FactionRules.GetMultiplier("shu", 4, FactionDamageKind.SkillDirect, true, false), "蜀强敌直击");
            Near(1, FactionRules.GetMultiplier("shu", 4, FactionDamageKind.SkillDirect, false, false), "蜀普通目标");
            Near(1, FactionRules.GetMultiplier("shu", 4, FactionDamageKind.SkillPeriodic, true, false), "蜀持续伤害");
            Near(1.12f, FactionRules.GetMultiplier("wu", 4, FactionDamageKind.SkillPeriodic, false, false), "吴持续伤害创建");
            Near(1, FactionRules.GetMultiplier("wu", 4, FactionDamageKind.Basic, false, false), "吴不增强普攻");
            Near(1.16f, FactionRules.GetMultiplier("qun", 4, FactionDamageKind.SkillDirect, false, true), "群孤立直击");
            Near(1, FactionRules.GetMultiplier("qun", 4, FactionDamageKind.SkillDirect, false, false), "群拥挤无收益");
            foreach (string faction in new[] { "wei", "shu", "wu", "qun" })
                Near(1, FactionRules.GetMultiplier(faction, 4, FactionDamageKind.Inherited, true, true), "继承伤害不重乘");

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            GameDatabase db = serializer.Deserialize<GameDatabase>(File.ReadAllText(args[0]));
            Check(db.Heroes.Count == 25 && db.Heroes.All(x => FactionRules.IsKnown(x.Faction)), "正式配置阵营必须齐全");
            foreach (var expected in new[] { new { Faction = "wei", Count = 6 },
                new { Faction = "shu", Count = 8 }, new { Faction = "wu", Count = 6 },
                new { Faction = "qun", Count = 5 } })
                Check(db.Heroes.Count(x => x.Faction == expected.Faction) == expected.Count, "阵营人数不符");
            foreach (LevelDefinition level in db.Levels)
                foreach (string faction in new[] { "wei", "shu", "wu", "qun" })
                    Check(db.Heroes.Count(x => x.Faction == faction && !level.BossHeroIds.Contains(x.Id)) >= 4,
                        "关卡禁选后无法达到4人档");
            Check(CatalogValidator.Validate(db).Count == 0, "完整目录应合法");
            db.Heroes[0].Faction = "unknown";
            Check(CatalogValidator.Validate(db).Any(x => x.Contains("阵营")), "未知值必须报错");
            db.Heroes[0].Faction = null;
            Check(!CatalogValidator.Validate(db).Any(x => x.Contains("阵营")), "旧配置空值应兼容");
            Console.WriteLine("阵营基础检查通过：" + checks);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
```

- [ ] 新增`UnityGame/Tests/Run-FactionTests.ps1`：

<!-- sample: faction-runner -->
```powershell
param([string]$Compiler = "C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$domainRoot = Join-Path $testRoot '../Assets/Scripts/Domain'
$outputRoot = Join-Path $testRoot 'bin'
if (-not (Test-Path -LiteralPath $Compiler)) { throw '找不到现有C#编译器。' }
[void][IO.Directory]::CreateDirectory($outputRoot)
$sources = @(Get-ChildItem -LiteralPath $domainRoot -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $testRoot 'FactionRuleTests.cs'
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$outputRoot/FactionRuleTests.exe" $sources
if ($LASTEXITCODE -ne 0) { throw '阵营基础测试编译失败。' }
& (Join-Path $outputRoot 'FactionRuleTests.exe') (Join-Path $testRoot '../Assets/Resources/Game/database.json')
if ($LASTEXITCODE -ne 0) { throw '阵营基础测试失败。' }
```

- [ ] 根目录运行`./UnityGame/Tests/Run-FactionTests.ps1`。此时应因`Faction`／`FactionRules`不存在而编译失败，保留日志；不要把依赖缺失当成已通过。

## 任务2：增加纯查询与旧配置兼容

- [ ] 在`Definitions.cs`的`HeroDefinition`中，于`Id`后插入`public string Faction;`。未填写的旧配置保持`null`，不自动推断或计数。
- [ ] 新增`UnityGame/Assets/Scripts/Domain/FactionRules.cs`：

<!-- sample: faction-rules -->
```csharp
using System;
using System.Collections.Generic;

namespace YanyiSandpan.Domain
{
    public enum FactionDamageKind { Inherited, Basic, SkillDirect, SkillPeriodic }

    public static class FactionRules
    {
        public const float IsolationDistance = 220f;

        public static bool IsKnown(string faction)
        { return faction == "wei" || faction == "shu" || faction == "wu" || faction == "qun"; }

        public static int GetTier(int count)
        { return count >= 4 ? 4 : count >= 2 ? 2 : 0; }

        public static int CountDeployed(IEnumerable<HeroUnit> heroes, string faction)
        {
            if (heroes == null || !IsKnown(faction)) return 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (HeroUnit hero in heroes)
                if (hero != null && hero.Definition != null && hero.Definition.Faction == faction &&
                    !string.IsNullOrEmpty(hero.Definition.Id)) ids.Add(hero.Definition.Id);
            return ids.Count;
        }

        public static bool IsStrongTarget(EnemyDefinition enemy)
        { return enemy != null && (enemy.IsBoss || enemy.Id == "elite"); }

        private static bool Finite(Point2 value)
        { return !float.IsNaN(value.X) && !float.IsInfinity(value.X) && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y); }

        public static bool IsIsolated(HeroUnit hero, IEnumerable<HeroUnit> heroes)
        {
            if (hero == null || heroes == null || !Finite(hero.Position)) return false;
            foreach (HeroUnit other in heroes)
            {
                if (other == null || ReferenceEquals(other, hero)) continue;
                if (!Finite(other.Position) || Point2.Distance(hero.Position, other.Position) < IsolationDistance)
                    return false;
            }
            return true;
        }

        // 持续伤害仅在创建时查询；后续tick与传伤使用Inherited，不重复放大。
        public static float GetMultiplier(string faction, int tier, FactionDamageKind kind,
            bool strongTarget, bool isolated)
        {
            if (tier != 2 && tier != 4) return 1;
            bool direct = kind == FactionDamageKind.Basic || kind == FactionDamageKind.SkillDirect;
            float standard = tier == 4 ? 1.12f : 1.06f;
            if (faction == "wei" && kind == FactionDamageKind.Basic) return standard;
            if (faction == "shu" && direct && strongTarget) return standard;
            if (faction == "wu" && (kind == FactionDamageKind.SkillDirect || kind == FactionDamageKind.SkillPeriodic))
                return standard;
            if (faction == "qun" && direct && isolated) return tier == 4 ? 1.16f : 1.08f;
            return 1;
        }
    }
}
```

- [ ] 在`CatalogValidator.Validate`角色循环的技能检查后插入：

```csharp
if (!string.IsNullOrEmpty(hero.Faction) && !FactionRules.IsKnown(hero.Faction))
    errors.Add("角色阵营未知：" + hero.Id);
```

- [ ] 再运行独立测试。行为部分应能执行，正式数据完整性应仍失败，因为database.json还没有阵营。保留这个有意义的失败，不删除断言。

## 任务3：单一归属来源与受控配置更新

- [ ] 在`tools/build_game_content.py`的`HERO_ROWS`后新增下表代码；在`make_database`的现有`for hero in heroes:`循环首行插入`hero["Faction"] = HERO_FACTIONS[hero["Id"]]`。

<!-- sample: faction-map -->
```python
FACTION_MEMBERS = {
    "wei": ("xu_chu", "guo_jia", "cao_cao", "xun_yu", "sima_yi", "zhang_liao"),
    "shu": ("guan_yu", "huang_zhong", "zhao_yun", "zhang_fei", "zhuge_liang", "ma_chao", "pang_tong", "jiang_wei"),
    "wu": ("taishi_ci", "zhou_yu", "lu_xun", "lv_meng", "sun_shangxiang", "lu_su"),
    "qun": ("lv_bu", "yuan_shao", "zhang_jiao", "diao_chan", "hua_tuo"),
}
HERO_FACTIONS = {hero_id: faction for faction, members in FACTION_MEMBERS.items() for hero_id in members}
assert len(HERO_FACTIONS) == sum(len(members) for members in FACTION_MEMBERS.values()) == 25
assert set(HERO_FACTIONS) == {row[0] for row in HERO_ROWS}
```

- [ ] 在`verify_database`的角色循环首部插入正式数据约束；现有字段集合校验会自动要求生成的`Faction`与C#字段一致。

```python
if hero.get("Faction") not in FACTION_MEMBERS or hero.get("Faction") != HERO_FACTIONS.get(hero["Id"]):
    errors.append("角色阵营缺失或归属错误：" + hero["Id"])
```

- [ ] 新增`tools/update_faction_data.py`，只更新这一份JSON，不运行全量美术生成：

<!-- sample: faction-data-update -->
```python
"""仅补齐阵营字段；其他配置若发生变化立即停止。"""
import copy
import json
from pathlib import Path
import build_game_content as content

root = Path(__file__).resolve().parents[1]
target = root / "UnityGame/Assets/Resources/Game/database.json"
before = json.loads(target.read_text(encoding="utf-8-sig"))
after = content.make_database()

def without_faction(value):
    result = copy.deepcopy(value)
    for hero in result["Heroes"]:
        hero.pop("Faction", None)
    return result

if without_faction(before) != without_faction(after):
    raise SystemExit("除阵营之外的配置不同，已停止，未写入文件。")
# 现有校验器失败时直接抛异常，成功时返回None。
content.verify_database(after)
if before != after:
    target.write_text(json.dumps(after, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("仅阵营字段已同步；其他配置逐字段相同。")
```

- [ ] 根目录运行`python tools/update_faction_data.py`，预期退出0且仅25个Faction字段变化。若工具阻止写入，核对生成器／当前配置的真实差异，不能删除保护断言或改用全量生成覆盖。
- [ ] 运行`./UnityGame/Tests/Run-FactionTests.ps1`，预期所有基础检查通过、退出0。

## 任务4：回归、契约与交接

- [ ] 运行`./UnityGame/Tests/Run-Tests.ps1 -Simulate`。旧规则测试不得因缺省阵营或元数据变化失败；五关模拟与该批次之前的冻结参考比较，不把历史139项数字当作新结果。
- [ ] 运行`python tools/build_game_content.py --verify-only`，预期退出0。运行`./UnityGame/Tests/Run-FeedbackTests.ps1`，预期原反馈检查通过。
- [ ] 比较原JSON与新JSON剔除Faction后的结构，确认完全一致；复核Runtime契约规定的合法阵容与1／2／3倍原玩法／军饷轨迹。此批次不得在`Power`、`BasicShot`、`Shot`、`Damage`中接入倍率。
- [ ] 将以下段落加入`docs/engineering/runtime-contract.md`的配置契约部分，并记录真实验证日志路径：

```text
HeroDefinition.Faction为固定游戏归属，合法值wei/shu/wu/qun。正式生成配置必须全员完整，旧配置缺省可读且不计军势；非空未知值由目录校验拒绝。FactionRules是纯查询，本批次尚不改变实际伤害、价格、冷却、记录或事件。军势乘数查询供后续独立实验接入，不能在展示层自行结算。
```

- [ ] 运行`./tools/Build-Game.ps1 -Target Verify`，确认Unity兼容新增C#5代码。该通用编译检查不能代替后续军势UI专项或平台图标验收，不运行会覆盖旧试玩证据的App／WindowsJade／Experience入口。
- [ ] 保存实际代码变更、配置差异、测试结果与构建输入清单。以“阵营基础数据与查询已完成、真实收益尚未接入”交接P2-B；保留当前`growth-tempo-v2`记录版本。

## 本计划的验收边界

所有勾选项完成后，只能说明P2-A领域基础可用。四类真实伤害、施放上下文快照、军势UI、`faction-army-v1`成绩隔离、独立试玩包和人工平衡均属于P2-B；完整要求已在设计中写明，不得据此跳过。

本计划制定阶段可在本轮规划目录临时副本中编译示例，验证C#5签名与配置映射；这类检查仅证明计划代码样例可编译，不能冒充正式工程已实现或回归通过。
