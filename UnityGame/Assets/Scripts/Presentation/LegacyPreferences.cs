using System;
using System.Collections.Generic;
using System.Globalization;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace YanyiSandpan.Presentation
{
    public static class LegacyPreferences
    {
        public const string MigrationKey = "yanyi.legacy.windows.migrated";

        public static void Migrate()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Migrate(ReadLegacy, UnityEngine.PlayerPrefs.HasKey, UnityEngine.PlayerPrefs.SetInt,
                UnityEngine.PlayerPrefs.Save);
#endif
        }

        // 委托入口供隔离检查使用，避免编辑器测试接触真实偏好。
        public static void Migrate(Func<IEnumerable<KeyValuePair<string, object>>> readLegacy,
            Func<string, bool> hasKey, Action<string, int> write, Action save)
        {
            try
            {
                if (hasKey(MigrationKey)) return;
                foreach (var entry in readLegacy())
                {
                    string key = LogicalKey(entry.Key);
                    if (key != null && entry.Value is int && !hasKey(key)) write(key, (int)entry.Value);
                }
                write(MigrationKey, 1);
                save();
            }
            catch (Exception)
            {
                // 权限或偏好写入异常不得阻止游戏启动；旧注册表始终保留。
            }
        }

        private static string LogicalKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int suffix = name.LastIndexOf("_h", StringComparison.Ordinal);
            if (suffix >= 0)
            {
                uint hash;
                if (!uint.TryParse(name.Substring(suffix + 2), NumberStyles.None, CultureInfo.InvariantCulture, out hash)) return null;
                name = name.Substring(0, suffix);
            }
            foreach (string prefix in new[] { "yanyi.audio.", "yanyi.clear.", "yanyi.best." })
                if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length) return name;
            return null;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static IEnumerable<KeyValuePair<string, object>> ReadLegacy()
        {
            var values = new List<KeyValuePair<string, object>>();
            IntPtr key;
            // 预定义句柄必须保留符号扩展，查询权限不允许改写旧注册表。
            int error = RegOpenKeyExW(new IntPtr(unchecked((int)0x80000001)),
                @"Software\演义沙盘课程项目\演义沙盘", 0, 0x0001, out key);
            if (error == 2) return values;
            if (error != 0) throw new InvalidOperationException("旧偏好注册表打开失败：" + error);
            try
            {
                var name = new StringBuilder(16384);
                var data = new byte[4];
                for (uint index = 0; ; index++)
                {
                    name.Clear();
                    uint nameLength = (uint)name.Capacity, dataLength = 4, type;
                    error = RegEnumValueW(key, index, name, ref nameLength, IntPtr.Zero, out type, data, ref dataLength);
                    if (error == 259) break;
                    // 缓冲区只接受DWORD；非整数或超过四字节的数据不参与迁移。
                    if (error == 234) continue;
                    if (error != 0) throw new InvalidOperationException("旧偏好注册表枚举失败：" + error);
                    string valueName = name.ToString();
                    if (type == 4 && dataLength == 4 && LogicalKey(valueName) != null)
                        values.Add(new KeyValuePair<string, object>(valueName, BitConverter.ToInt32(data, 0)));
                }
            }
            finally
            {
                error = RegCloseKey(key);
                if (error != 0) throw new InvalidOperationException("旧偏好注册表关闭失败：" + error);
            }
            return values;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int RegOpenKeyExW(IntPtr key, string subKey, uint options, uint access, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int RegEnumValueW(IntPtr key, uint index, StringBuilder name, ref uint nameLength,
            IntPtr reserved, out uint type, [Out] byte[] data, ref uint dataLength);

        [DllImport("advapi32.dll", ExactSpelling = true)]
        private static extern int RegCloseKey(IntPtr key);
#endif
    }
}
