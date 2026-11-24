using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 有界二进制摘要；校验只识别损坏，不提供防作弊认证。
    internal static class RaceRecordCodec
    {
        internal const int MaxBytes = 32768;
        private const int MaxTextBytes = 256, MaxActions = 64, MaxChanges = 256;
        private const int FormatVersion = 1;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static bool ValidId(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextBytes) return false;
            foreach (char c in text) if (char.IsControl(c)) return false;
            try { return StrictUtf8.GetByteCount(text) <= MaxTextBytes; } catch (ArgumentException) { return false; }
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static bool Milliseconds(double seconds, bool allowZero, out long result)
        {
            result = 0;
            if (!Finite(seconds) || seconds < 0) return false;
            double rounded = Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
            if (!Finite(rounded) || rounded >= (double)long.MaxValue || (!allowZero && rounded <= 0)) return false;
            result = (long)rounded; return true;
        }
        internal static RaceRecord FromSnapshot(RaceSnapshot snapshot, out string reason)
        {
            reason = "本次快照不符合竞速记录要求";
            if (snapshot == null) return null;
            if (!snapshot.IsEligible) { if (!string.IsNullOrEmpty(snapshot.IneligibilityReason)) reason = snapshot.IneligibilityReason; return null; }
            if (snapshot.ExpectedBossCount < 0 || snapshot.ExpectedBossCount != snapshot.SpawnedBossCount ||
                snapshot.SpawnedBossCount != snapshot.DefeatedBossCount || snapshot.BossBreachCount != 0 ||
                snapshot.QualificationVersion != "all-boss-v1") return null;
            long elapsed;
            if (!Milliseconds(snapshot.ElapsedSeconds, false, out elapsed)) { reason = "本次实秒无效，未计入竞速记录"; return null; }
            if (!ValidId(snapshot.LevelId) || !ValidId(snapshot.RuleVersion) || snapshot.SpeedMask <= 0 ||
                (snapshot.SpeedMask & ~7) != 0 || snapshot.CastleHealth <= 0 || snapshot.LeakedCount < 0) return null;
            try {
                var durations = new long[3]; double sum = 0; long roundedSum = 0; int largest = 0, actualMask = 0;
                for (int i = 0; i < 3; i++) {
                    double seconds = snapshot.GetSpeedSeconds(i + 1);
                    if (!Milliseconds(seconds, true, out durations[i])) return null;
                    if (seconds > 0) actualMask |= 1 << i;
                    sum += seconds; roundedSum = checked(roundedSum + durations[i]);
                    if (durations[i] > durations[largest]) largest = i;
                }
                if (actualMask != snapshot.SpeedMask || !Finite(sum) || Math.Abs(sum - snapshot.ElapsedSeconds) > Math.Max(.000001, snapshot.ElapsedSeconds * 1e-12)) return null;
                long adjustment = elapsed - roundedSum;
                if (adjustment < -2 || adjustment > 2 || durations[largest] + adjustment < 0) return null;
                durations[largest] += adjustment;
                if (snapshot.Actions == null || snapshot.SpeedChanges == null || snapshot.Actions.Count > MaxActions || snapshot.SpeedChanges.Count > MaxChanges) return null;
                var actions = new List<RaceRecordAction>(); var changes = new List<RaceRecordSpeedChange>();
                foreach (RaceAction action in snapshot.Actions) {
                    long time;
                    if (action == null || !Milliseconds(action.ElapsedSeconds, true, out time) || action.ElapsedSeconds > snapshot.ElapsedSeconds + .000001) return null;
                    actions.Add(new RaceRecordAction(action.Kind, action.HeroId, action.SlotId, action.Level, action.Gold, time));
                }
                foreach (RaceSpeedChange change in snapshot.SpeedChanges) {
                    long time;
                    if (change == null || !Milliseconds(change.ElapsedSeconds, true, out time) || change.ElapsedSeconds > snapshot.ElapsedSeconds + .000001) return null;
                    changes.Add(new RaceRecordSpeedChange(change.Speed, time));
                }
                var record = new RaceRecord(snapshot.LevelId, snapshot.RuleVersion, elapsed, snapshot.SpeedMask, snapshot.CastleHealth,
                    snapshot.LeakedCount, snapshot.DefeatedBossCount, durations, actions, changes, snapshot.SummaryTruncated);
                if (!ValidRecord(record) || Encode(record) == null) return null;
                reason = ""; return record;
            } catch (Exception) { return null; }
        }
        private static bool ValidRecord(RaceRecord record)
        {
            if (record == null || !ValidId(record.LevelId) || !ValidId(record.RuleVersion) || record.QualificationVersion != "all-boss-v1" ||
                record.ElapsedMilliseconds <= 0 || record.SpeedMask <= 0 || (record.SpeedMask & ~7) != 0 || record.CastleHp <= 0 ||
                record.LeakedCount < 0 || record.BossCount < 0 || record.Actions.Count > MaxActions || record.SpeedChanges.Count < 1 || record.SpeedChanges.Count > MaxChanges) return false;
            long remaining = record.ElapsedMilliseconds;
            for (int i = 1; i <= 3; i++) {
                long duration = record.GetSpeedMilliseconds(i);
                if (duration < 0 || duration > remaining || ((record.SpeedMask & (1 << (i - 1))) == 0 && duration != 0)) return false;
                remaining -= duration;
            }
            if (remaining != 0) return false;
            long previous = 0;
            foreach (RaceRecordAction action in record.Actions) {
                if (action == null || (action.Kind != "recruit" && action.Kind != "move" && action.Kind != "upgrade") || !ValidId(action.HeroId) ||
                    action.SlotId < 0 || action.Level < 1 || action.Level > 3 || action.Gold < 0 ||
                    action.ElapsedMilliseconds < previous || action.ElapsedMilliseconds > record.ElapsedMilliseconds) return false;
                previous = action.ElapsedMilliseconds;
            }
            previous = 0; int lastSpeed = 0, changeMask = 0;
            foreach (RaceRecordSpeedChange change in record.SpeedChanges) {
                if (change == null || change.Speed < 1 || change.Speed > 3 || change.Speed == lastSpeed ||
                    change.ElapsedMilliseconds < previous || change.ElapsedMilliseconds > record.ElapsedMilliseconds ||
                    (lastSpeed == 0 && change.ElapsedMilliseconds != 0)) return false;
                previous = change.ElapsedMilliseconds; lastSpeed = change.Speed; changeMask |= 1 << (change.Speed - 1);
            }
            return (changeMask & ~record.SpeedMask) == 0 && (record.SummaryTruncated || changeMask == record.SpeedMask);
        }
        internal static byte[] Encode(RaceRecord record)
        {
            if (!ValidRecord(record)) return null;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
                writer.Write(FormatVersion); WriteText(writer, record.LevelId); WriteText(writer, record.RuleVersion); WriteText(writer, record.QualificationVersion);
                writer.Write(record.ElapsedMilliseconds); writer.Write(record.SpeedMask); writer.Write(record.CastleHp); writer.Write(record.LeakedCount); writer.Write(record.BossCount);
                for (int i = 1; i <= 3; i++) writer.Write(record.GetSpeedMilliseconds(i));
                writer.Write(record.SummaryTruncated ? 1 : 0); writer.Write(record.Actions.Count);
                foreach (RaceRecordAction action in record.Actions) {
                    WriteText(writer, action.Kind); WriteText(writer, action.HeroId); writer.Write(action.SlotId); writer.Write(action.Level); writer.Write(action.Gold); writer.Write(action.ElapsedMilliseconds);
                }
                writer.Write(record.SpeedChanges.Count);
                foreach (RaceRecordSpeedChange change in record.SpeedChanges) { writer.Write(change.Speed); writer.Write(change.ElapsedMilliseconds); }
                writer.Flush(); return stream.Length <= MaxBytes ? stream.ToArray() : null;
            }
        }
        internal static RaceRecord Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxBytes) return null;
            try {
                using (var stream = new MemoryStream(payload, false)) using (var reader = new BinaryReader(stream)) {
                    if (reader.ReadInt32() != FormatVersion) return null;
                    string level = ReadText(reader), rule = ReadText(reader), qualification = ReadText(reader);
                    if (qualification != "all-boss-v1") return null;
                    long elapsed = reader.ReadInt64(); int mask = reader.ReadInt32(), castle = reader.ReadInt32(), leaks = reader.ReadInt32(), bosses = reader.ReadInt32();
                    var durations = new[] { reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64() };
                    int truncated = reader.ReadInt32(); if (truncated != 0 && truncated != 1) return null;
                    int count = reader.ReadInt32(); if (count < 0 || count > MaxActions) return null;
                    var actions = new List<RaceRecordAction>();
                    for (int i = 0; i < count; i++) actions.Add(new RaceRecordAction(ReadText(reader), ReadText(reader), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64()));
                    count = reader.ReadInt32(); if (count < 1 || count > MaxChanges) return null;
                    var changes = new List<RaceRecordSpeedChange>();
                    for (int i = 0; i < count; i++) changes.Add(new RaceRecordSpeedChange(reader.ReadInt32(), reader.ReadInt64()));
                    if (stream.Position != stream.Length) return null;
                    var record = new RaceRecord(level, rule, elapsed, mask, castle, leaks, bosses, durations, actions, changes, truncated == 1);
                    return ValidRecord(record) ? record : null;
                }
            } catch (Exception) { return null; }
        }
        private static void WriteText(BinaryWriter writer, string value)
        {
            byte[] bytes = StrictUtf8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes);
        }
        private static string ReadText(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 1 || length > MaxTextBytes || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("竞速摘要字符串长度无效");
            return StrictUtf8.GetString(reader.ReadBytes(length));
        }
        internal static int Checksum(byte[] payload)
        {
            unchecked { uint value = 2166136261; foreach (byte b in payload) value = (value ^ b) * 16777619; return (int)value; }
        }
    }
}
