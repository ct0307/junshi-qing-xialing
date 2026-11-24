using System;
using System.Collections.Generic;
using System.Reflection;
using YanyiSandpan.Domain;

internal static class FeedbackStateTests
{
    private static int passed, failed;
    private static int Main()
    {
        Run("唯一真实击退达到3/6/10档", delegate {
            object tracker = Tracker(.7f);
            for (int id = 1; id <= 10; id++)
            {
                object result = Observe(tracker, Kill(id, .1f));
                Equal(id, Value<int>(result, "Count"));
                Equal(id >= 10 ? 3 : id >= 6 ? 2 : id >= 3 ? 1 : 0, Value<int>(result, "Tier"));
                Equal(id == 3 || id == 6 || id == 10, Value<bool>(result, "CrossedTier"));
            }
            Equal(false, Value<bool>(Observe(tracker, Kill(10, .2f)), "Accepted"));
        });
        Run("固定窗口到期不因后续击退续时", delegate {
            object tracker = Tracker(.7f);
            Observe(tracker, Kill(1, 0)); Observe(tracker, Kill(2, .69f));
            object result = Observe(tracker, Kill(3, .7f));
            Equal(1, Value<int>(result, "Count")); Equal(0, Value<int>(result, "Tier"));
            Equal(false, Value<bool>(Observe(tracker, Kill(1, .8f)), "Accepted"));
        });
        Run("非法ID命中与漏兵不计数", delegate {
            object tracker = Tracker(.7f);
            foreach (BattleEvent item in new[] { Kill(0, 0), Kill(-1, 0), new BattleEvent { Kind = "hit", TargetEnemyId = 20 }, new BattleEvent { Kind = "leak", TargetEnemyId = 21 } })
                Equal(false, Value<bool>(Observe(tracker, item), "Accepted"));
            Equal(1, Value<int>(Observe(tracker, Kill(22, .1f)), "Count"));
        });
        Run("批量消费遵守发生时刻并可比较0.4与0.7", delegate {
            object shortWindow = Tracker(.4f), longWindow = Tracker(.7f);
            int shortPeak = 0, longPeak = 0;
            foreach (BattleEvent item in new[] { Kill(1, 0), Kill(2, .25f), Kill(3, .5f), Kill(4, .72f) })
            {
                shortPeak = Math.Max(shortPeak, Value<int>(Observe(shortWindow, item), "Count"));
                longPeak = Math.Max(longPeak, Value<int>(Observe(longWindow, item), "Count"));
            }
            Equal(2, shortPeak); Equal(3, longPeak);
        });
        Run("Clear清空本局身份与档位", delegate {
            object tracker = Tracker(.7f); Observe(tracker, Kill(1, 0));
            tracker.GetType().GetMethod("Clear").Invoke(tracker, null);
            Equal(1, Value<int>(Observe(tracker, Kill(1, 0)), "Count"));
        });
        Run("无效时刻和乱序不污染身份", delegate {
            object tracker = Tracker(.7f);
            Equal(false, Value<bool>(Observe(tracker, Kill(1, float.NaN)), "Accepted"));
            Equal(false, Value<bool>(Observe(tracker, Kill(1, float.PositiveInfinity)), "Accepted"));
            Observe(tracker, Kill(2, 1));
            Equal(false, Value<bool>(Observe(tracker, Kill(1, .5f)), "Accepted"));
            Equal(true, Value<bool>(Observe(tracker, Kill(1, 1)), "Accepted"));
        });
        Console.WriteLine("反馈状态测试：通过{0}，失败{1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }
    private static object Tracker(float window)
    {
        Type type = typeof(BattleEvent).Assembly.GetType("YanyiSandpan.Presentation.BattleFeedbackState");
        if (type == null) throw new Exception("缺少独立连破聚合状态");
        return Activator.CreateInstance(type, new object[] { window });
    }
    private static BattleEvent Kill(int id, float at)
    {
        var item = new BattleEvent { Kind = "kill", TargetEnemyId = id, Position = new Point2(id * 10, 500) };
        FieldInfo field = typeof(BattleEvent).GetField("OccurredAt");
        if (field == null) throw new Exception("事件缺少真实发生时间");
        field.SetValue(item, at); return item;
    }
    private static object Observe(object tracker, BattleEvent item) { return tracker.GetType().GetMethod("Observe").Invoke(tracker, new object[] { item }); }
    private static T Value<T>(object item, string name) { return (T)item.GetType().GetField(name).GetValue(item); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("预期" + expected + "，实际" + actual); }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("通过：" + name); } catch (Exception error) { failed++; Console.WriteLine("失败：" + name + "：" + (error.InnerException ?? error).Message); } }
}
