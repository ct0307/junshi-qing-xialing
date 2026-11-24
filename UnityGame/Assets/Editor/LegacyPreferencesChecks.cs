using System;
using System.Collections.Generic;
using YanyiSandpan.Presentation;

public static class LegacyPreferencesChecks
{
    public static void Verify()
    {
        var current = new Dictionary<string, int> { { "yanyi.audio.music.volume", 750 } };
        var legacy = new Dictionary<string, object>
        {
            { "yanyi.audio.enabled_h2682062960", 0 },
            { "yanyi.audio.music.volume_h4294967295", 200 },
            { "yanyi.audio.sound.volume", 650 },
            { "yanyi.clear.huangjin_h0", 1 },
            { "yanyi.best.huangjin_h4294967295", 18 },
            { "yanyi.audio.string", "100" },
            { "yanyi.best.long", 42L },
            { "yanyi.other.value", 1 },
            { "other.clear.huangjin", 1 },
            { "yanyi.clear.", 1 },
            { "yanyi.audio.invalid_h4294967296", 1 },
            { "yanyi.audio.invalid_h-1", 1 }
        };
        int reads = 0, saves = 0;
        Func<IEnumerable<KeyValuePair<string, object>>> read = () => { reads++; return legacy; };
        Action<string, int> write = (key, value) => current[key] = value;
        LegacyPreferences.Migrate(read, current.ContainsKey, write, () => saves++);
        Require(current.ContainsKey("yanyi.audio.enabled") && current["yanyi.audio.enabled"] == 0, "未迁移Unity哈希键");
        Require(current["yanyi.audio.music.volume"] == 750, "旧偏好覆盖了当前设置");
        Require(current.ContainsKey("yanyi.audio.sound.volume") && current["yanyi.audio.sound.volume"] == 650, "未迁移普通逻辑键");
        Require(current.ContainsKey("yanyi.clear.huangjin") && current["yanyi.clear.huangjin"] == 1 &&
            current.ContainsKey("yanyi.best.huangjin") && current["yanyi.best.huangjin"] == 18, "通关记录未迁移");
        Require(current.Count == 6 && current.ContainsKey(LegacyPreferences.MigrationKey) &&
            current[LegacyPreferences.MigrationKey] == 1, "迁移了非游戏整数、非法哈希或遗漏迁移标记");
        Require(reads == 1 && saves == 1 && legacy.Count == 12, "迁移次数、保存次数或旧数据保护错误");
        legacy["yanyi.best.hulao"] = 20;
        LegacyPreferences.Migrate(read, current.ContainsKey, write, () => saves++);
        Require(reads == 1 && saves == 1 && !current.ContainsKey("yanyi.best.hulao"), "迁移标记未阻止再次读取旧数据");

        var empty = new Dictionary<string, int>();
        int emptySaves = 0;
        LegacyPreferences.Migrate(() => new Dictionary<string, object>(), empty.ContainsKey,
            (key, value) => empty[key] = value, () => emptySaves++);
        Require(empty.Count == 1 && empty.ContainsKey(LegacyPreferences.MigrationKey) && emptySaves == 1,
            "没有旧数据时未记录迁移完成");
        var failed = new Dictionary<string, int>();
        LegacyPreferences.Migrate(() => { throw new InvalidOperationException("模拟拒绝访问"); }, failed.ContainsKey,
            (key, value) => failed[key] = value, () => { throw new InvalidOperationException("不应保存"); });
        Require(failed.Count == 0, "读取异常后错误地标记迁移成功");
#if UNITY_EDITOR
        UnityEngine.Debug.Log("旧版Windows偏好检查通过：哈希与普通键、现值保护、整数白名单、一次性标记及异常隔离");
#endif
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
