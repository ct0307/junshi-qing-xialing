using System.Collections.Generic;

namespace YanyiSandpan.Presentation
{
    // 只记录等待中的真实阶位；不改技能冷却，不制造规则事件。
    public sealed class UpgradeFeedbackState
    {
        private readonly Dictionary<string, int> pending = new Dictionary<string, int>();
        private readonly Dictionary<string, int> consumed = new Dictionary<string, int>();
        public void Upgrade(string id, int level) { if (!string.IsNullOrEmpty(id)) pending[id] = level; }
        public int WaitingLevel(string id) { int value; return id != null && pending.TryGetValue(id, out value) ? value : 0; }
        public bool Consume(string id, int level, int sequence)
        {
            int expected, last;
            if (id == null || sequence <= 0 || !pending.TryGetValue(id, out expected) || expected != level ||
                (consumed.TryGetValue(id, out last) && last == sequence)) return false;
            pending.Remove(id); consumed[id] = sequence; return true;
        }
        public void Clear() { pending.Clear(); consumed.Clear(); }
    }
}
