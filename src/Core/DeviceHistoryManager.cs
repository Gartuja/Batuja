using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OmniBattery.Models;

namespace OmniBattery.Core
{
    public class DeviceHistoryEntry
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public DeviceCategory Category { get; set; }
        public int LastBatteryPercent { get; set; }
        public string Protocol { get; set; }
        public string Subtitle { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsCurrentlyConnected { get; set; }
        public double Voltage { get; set; }
    }

    public class DeviceHistoryManager
    {
        private readonly Dictionary<string, DeviceHistoryEntry> _history =
            new Dictionary<string, DeviceHistoryEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lock = new object();

        private static string HistoryFilePath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                try
                {
                    string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                        dir = exeDir;
                }
                catch { }
                return Path.Combine(dir, "history.ini");
            }
        }

        public DeviceHistoryManager()
        {
            Load();
        }

        public void RecordDevice(BatteryDevice dev, bool isConnected)
        {
            if (dev == null) return;

            string key = dev.DeviceKey;
            if (string.IsNullOrEmpty(key)) key = dev.Name;
            if (string.IsNullOrEmpty(key)) return;

            lock (_lock)
            {
                DeviceHistoryEntry entry;
                if (!_history.TryGetValue(key, out entry))
                {
                    entry = new DeviceHistoryEntry
                    {
                        Key = key,
                        Name = dev.Name,
                        Category = dev.EffectiveCategory,
                        FirstSeen = DateTime.Now
                    };
                    _history[key] = entry;
                }

                entry.Name = dev.Name;
                entry.Category = dev.EffectiveCategory;
                entry.Protocol = dev.Protocol;
                entry.Subtitle = dev.Subtitle;
                entry.LastSeen = DateTime.Now;
                entry.IsCurrentlyConnected = isConnected;

                if (dev.BatteryPercent >= 0)
                {
                    entry.LastBatteryPercent = dev.BatteryPercent;
                }
                if (dev.Voltage > 0)
                {
                    entry.Voltage = dev.Voltage;
                }

                Save();
            }
        }

        public void MarkDisconnected(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;

            lock (_lock)
            {
                DeviceHistoryEntry entry;
                if (_history.TryGetValue(deviceKey, out entry))
                {
                    entry.IsCurrentlyConnected = false;
                    Save();
                }
            }
        }

        public List<DeviceHistoryEntry> GetAll()
        {
            lock (_lock)
            {
                List<DeviceHistoryEntry> list = new List<DeviceHistoryEntry>(_history.Values);
                list.Sort(delegate(DeviceHistoryEntry a, DeviceHistoryEntry b)
                {
                    // Connected first, then by LastSeen descending
                    if (a.IsCurrentlyConnected != b.IsCurrentlyConnected)
                        return b.IsCurrentlyConnected.CompareTo(a.IsCurrentlyConnected);
                    return b.LastSeen.CompareTo(a.LastSeen);
                });
                return list;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _history.Clear();
                Save();
            }
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    string path = HistoryFilePath;
                    if (!File.Exists(path)) return;

                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    foreach (string rawLine in lines)
                    {
                        string line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                            continue;

                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();

                        // Format: Key = Name|Category|Battery|Protocol|FirstSeen|LastSeen|Voltage
                        string[] parts = val.Split('|');
                        if (parts.Length >= 6)
                        {
                            DeviceHistoryEntry entry = new DeviceHistoryEntry();
                            entry.Key = key;
                            entry.Name = parts[0];

                            DeviceCategory cat;
                            if (Enum.TryParse(parts[1], true, out cat))
                                entry.Category = cat;

                            int bat;
                            if (int.TryParse(parts[2], out bat))
                                entry.LastBatteryPercent = bat;

                            entry.Protocol = parts[3];

                            DateTime dtFirst;
                            if (DateTime.TryParse(parts[4], out dtFirst))
                                entry.FirstSeen = dtFirst;

                            DateTime dtLast;
                            if (DateTime.TryParse(parts[5], out dtLast))
                                entry.LastSeen = dtLast;

                            if (parts.Length >= 7)
                            {
                                double v;
                                if (double.TryParse(parts[6], out v))
                                    entry.Voltage = v;
                            }

                            entry.IsCurrentlyConnected = false;
                            _history[key] = entry;
                        }
                    }
                }
                catch { }
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# MoniTuja - Histórico de Dispositivos Conectados");
                    sb.AppendLine(string.Format("# Atualizado em {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
                    sb.AppendLine();

                    foreach (KeyValuePair<string, DeviceHistoryEntry> kvp in _history)
                    {
                        DeviceHistoryEntry e = kvp.Value;
                        sb.AppendLine(string.Format("{0}={1}|{2}|{3}|{4}|{5:yyyy-MM-dd HH:mm:ss}|{6:yyyy-MM-dd HH:mm:ss}|{7:F2}",
                            kvp.Key,
                            e.Name != null ? e.Name.Replace('|', '_') : "",
                            e.Category,
                            e.LastBatteryPercent,
                            e.Protocol != null ? e.Protocol.Replace('|', '_') : "",
                            e.FirstSeen,
                            e.LastSeen,
                            e.Voltage));
                    }

                    File.WriteAllText(HistoryFilePath, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
