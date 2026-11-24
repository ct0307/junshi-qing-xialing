using System;
using System.Collections.Generic;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 只聚合真实事件，不依赖渲染帧、音频或对象池，也不改变战斗规则。
    public sealed class BattleFeedbackState
    {
        private readonly HashSet<int> defeated = new HashSet<int>();
        private readonly float window;
        private float until, lastAt = -1;
        private int count, windowId;
        private Point2 sum;

        public BattleFeedbackState(float window)
        {
            if (float.IsNaN(window) || float.IsInfinity(window) || window <= 0) throw new ArgumentOutOfRangeException("window");
            this.window = window;
        }

        public BattleFeedbackResult Observe(BattleEvent item)
        {
            if (item == null || item.Kind != "kill" || item.TargetEnemyId <= 0 ||
                float.IsNaN(item.OccurredAt) || float.IsInfinity(item.OccurredAt) || item.OccurredAt < 0 ||
                item.OccurredAt < lastAt || !defeated.Add(item.TargetEnemyId)) return new BattleFeedbackResult();
            if (count == 0 || item.OccurredAt >= until)
            {
                count = 0; sum = new Point2(); until = item.OccurredAt + window; windowId++;
            }
            int previousTier = Tier(count);
            count++; lastAt = item.OccurredAt;
            sum.X += item.Position.X; sum.Y += item.Position.Y;
            return new BattleFeedbackResult {
                Accepted = true, Count = count, Tier = Tier(count), CrossedTier = Tier(count) > previousTier,
                WindowId = windowId, At = item.OccurredAt, Center = new Point2(sum.X / count, sum.Y / count)
            };
        }

        private static int Tier(int value) { return value >= 10 ? 3 : value >= 6 ? 2 : value >= 3 ? 1 : 0; }

        public void Clear()
        {
            defeated.Clear(); until = 0; lastAt = -1; count = windowId = 0; sum = new Point2();
        }
    }

    public struct BattleFeedbackResult
    {
        public bool Accepted, CrossedTier;
        public int Count, Tier, WindowId;
        public float At;
        public Point2 Center;
    }
}
