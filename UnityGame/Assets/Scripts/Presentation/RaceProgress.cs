using System;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class GameProgress
    {
        private const string MarchIntroKey = "yanyi.help.march-pressure-v1";
        private bool marchIntroSaveFailed;
        public bool HasReadMarchSpeedIntro
        {
            get { try { return !marchIntroSaveFailed && readInt(MarchIntroKey, 0) == 1; } catch (Exception) { return false; } }
        }
        public bool MarkMarchSpeedIntroRead()
        {
            int previous;
            // 读取异常不能当作未读，否则可能覆盖无法确认的已有状态。
            try { previous = readInt(MarchIntroKey, 0); if (previous == 1 && !marchIntroSaveFailed) return true; }
            catch (Exception) { return false; }
            try { writeInt(MarchIntroKey, 1); save(); marchIntroSaveFailed = false; return true; }
            catch (Exception) {
                // 即使回滚也失败，本实例仍不得将未持久保存的内存值宣称为已读。
                marchIntroSaveFailed = true;
                try { writeInt(MarchIntroKey, previous); } catch (Exception) { }
                return false;
            }
        }
        public RaceRecord GetRaceRecord(string levelId, string ruleVersion)
        {
            try { return ReadRaceRecord(levelId, ruleVersion); } catch (Exception) { return null; }
        }
        // UI读取吞掉异常；写入口必须区分存储异常与可正常识别的缺档/损坏。
        private RaceRecord ReadRaceRecord(string levelId, string ruleVersion)
        {
            if (!RaceRecordCodec.ValidId(levelId) || !RaceRecordCodec.ValidId(ruleVersion)) return null;
            string prefix = RacePrefix(levelId, ruleVersion); int active;
            if (!TryRead(prefix + "active", out active) || (active != 0 && active != 1)) return null;
            string slot = prefix + "slot" + active + "."; int ready, length, checksum;
            if (!TryRead(slot + "ready", out ready) || ready != 1 || !TryRead(slot + "length", out length) ||
                length <= 0 || length > RaceRecordCodec.MaxBytes || !TryRead(slot + "checksum", out checksum)) return null;
            byte[] payload = new byte[length];
            for (int i = 0; i < (length + 3) / 4; i++) {
                int block; if (!TryRead(slot + "data" + i, out block)) return null;
                for (int j = 0; j < 4 && i * 4 + j < length; j++) payload[i * 4 + j] = (byte)((uint)block >> (8 * j));
            }
            if (RaceRecordCodec.Checksum(payload) != checksum) return null;
            RaceRecord record = RaceRecordCodec.Decode(payload);
            return record != null && record.LevelId == levelId && record.RuleVersion == ruleVersion ? record : null;
        }
        public RaceRecordResult RecordRaceVictory(RaceSnapshot snapshot)
        {
            RaceRecord previous = null; bool readFailed = false;
            try { if (snapshot != null) previous = ReadRaceRecord(snapshot.LevelId, snapshot.RuleVersion); }
            catch (Exception) { readFailed = true; }
            string reason; RaceRecord current = RaceRecordCodec.FromSnapshot(snapshot, out reason);
            if (readFailed) return new RaceRecordResult(previous, current, snapshot, false, "旧竞速记录读取失败，本次未保存，请重试");
            bool isNew = current != null && (previous == null || current.ElapsedMilliseconds < previous.ElapsedMilliseconds);
            if (isNew && !TryWriteRace(current)) { isNew = false; reason = "竞速记录保存失败，请重试"; }
            return new RaceRecordResult(previous, current, snapshot, isNew, reason);
        }
        private bool TryWriteRace(RaceRecord record)
        {
            string prefix = RacePrefix(record.LevelId, record.RuleVersion); int active = -1; bool switched = false;
            try {
                byte[] payload = RaceRecordCodec.Encode(record); if (payload == null) return false;
                int stored; if (TryRead(prefix + "active", out stored) && (stored == 0 || stored == 1)) active = stored;
                int next = active == 0 ? 1 : 0; string slot = prefix + "slot" + next + ".";
                writeInt(slot + "ready", 0); writeInt(slot + "length", payload.Length);
                for (int i = 0; i < (payload.Length + 3) / 4; i++) {
                    uint block = 0;
                    for (int j = 0; j < 4 && i * 4 + j < payload.Length; j++) block |= (uint)payload[i * 4 + j] << (8 * j);
                    writeInt(slot + "data" + i, unchecked((int)block));
                }
                writeInt(slot + "checksum", RaceRecordCodec.Checksum(payload)); writeInt(slot + "ready", 1);
                // 只有活动指针代表提交；读取绝不猜测或恢复未提交候选槽。
                switched = true; writeInt(prefix + "active", next); save(); return true;
            } catch (Exception) {
                if (switched) { try { writeInt(prefix + "active", active); } catch (Exception) { } }
                return false;
            }
        }
        private static string RacePrefix(string levelId, string ruleVersion)
        {
            // 转义点号以避免任意标识符中的分隔符造成键冲突。
            return "yanyi.race." + Uri.EscapeDataString(ruleVersion).Replace(".", "%2E") + ".all-boss-v1." + Uri.EscapeDataString(levelId).Replace(".", "%2E") + ".";
        }
    }
}
