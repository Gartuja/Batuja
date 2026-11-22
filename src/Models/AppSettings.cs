using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace OmniBattery.Models
{
    public enum TrayIconStyle
    {
        Moderno,     // default – rounded rect with accent border
        AeroGlass,   // translucent glass look
        Retro,       // flat pixel-style with classic gradient
        Minimalista  // just the number, no background box
    }

    public class AppSettings
    {
        public int RefreshIntervalSeconds { get; set; }
        public int LowBatteryThreshold { get; set; }
        public bool EnableNotifications { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public bool ShowSystemBattery { get; set; }
        public string BackgroundImagePath { get; set; }
        public int BackgroundOpacity { get; set; } // 0 to 100%
        public Dictionary<string, string> DeviceColors { get; set; }
        public Dictionary<string, string> DeviceIcons { get; set; }
        public HashSet<string> HiddenDevices { get; set; }
        public HashSet<string> DisconnectedDevices { get; set; }
        public HashSet<string> DisabledTrayDevices { get; set; }
        public TrayIconStyle TrayStyle { get; set; }
        public bool OverlayEnabled { get; set; }
        public int OverlayX { get; set; }
        public int OverlayY { get; set; }
        public bool OverlayVertical { get; set; }
        public int OverlayOpacity { get; set; }

        private static string ConfigPath
        {
            get
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(appDir, "settings.ini");
            }
        }

        public AppSettings()
        {
            RefreshIntervalSeconds = 30;
            LowBatteryThreshold = 20;
            EnableNotifications = true;
            StartWithWindows = false;
            MinimizeToTrayOnClose = true;
            ShowSystemBattery = false;
            BackgroundImagePath = "";
            BackgroundOpacity = 75; // 75% dark tint by default
            DeviceColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DeviceIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HiddenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DisconnectedDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DisabledTrayDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TrayStyle = TrayIconStyle.Moderno;
            OverlayEnabled = false;
            OverlayX = -1;
            OverlayY = -1;
            OverlayVertical = false;
            OverlayOpacity = 90;
        }

        public Color? GetCustomColor(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return null;

            if (DeviceColors.ContainsKey(deviceKey))
            {
                string hex = DeviceColors[deviceKey];
                try
                {
                    if (hex.StartsWith("#") && hex.Length == 7)
                    {
                        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
                        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
                        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
                        return Color.FromArgb(r, g, b);
                    }
                }
                catch { }
            }

            return null;
        }

        public void SetCustomColor(string deviceKey, Color color)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;

            string hex = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            DeviceColors[deviceKey] = hex;
            Save();
        }

        public void RemoveCustomColor(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;

            if (DeviceColors.ContainsKey(deviceKey))
            {
                DeviceColors.Remove(deviceKey);
                Save();
            }
        }

        public string GetCustomIcon(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return null;
            string val;
            if (DeviceIcons.TryGetValue(deviceKey, out val))
                return val;
            return null;
        }

        public void SetCustomIcon(string deviceKey, string iconVal)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;
            DeviceIcons[deviceKey] = iconVal;
            Save();
        }

        public void RemoveCustomIcon(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;
            if (DeviceIcons.ContainsKey(deviceKey))
            {
                DeviceIcons.Remove(deviceKey);
                Save();
            }
        }

        public bool IsDeviceHidden(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return false;
            return HiddenDevices.Contains(deviceKey);
        }

        public void SetDeviceHidden(string deviceKey, bool hidden)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;
            if (hidden)
                HiddenDevices.Add(deviceKey);
            else
                HiddenDevices.Remove(deviceKey);
            Save();
        }

        public bool IsDeviceManualDisconnected(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return false;
            return DisconnectedDevices.Contains(deviceKey);
        }

        public void SetDeviceManualDisconnected(string deviceKey, bool disconnected)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;
            if (disconnected)
                DisconnectedDevices.Add(deviceKey);
            else
                DisconnectedDevices.Remove(deviceKey);
            Save();
        }

        public bool IsTrayIconEnabled(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return true;
            return !DisabledTrayDevices.Contains(deviceKey);
        }

        public void SetTrayIconEnabled(string deviceKey, bool enabled)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;
            if (enabled)
                DisabledTrayDevices.Remove(deviceKey);
            else
                DisabledTrayDevices.Add(deviceKey);
            Save();
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path))
                {
                    settings.Save();
                    return settings;
                }

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    int eqIdx = line.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = line.Substring(0, eqIdx).Trim();
                    string val = line.Substring(eqIdx + 1).Trim();

                    if (key.StartsWith("Color_", StringComparison.OrdinalIgnoreCase))
                    {
                        string devKey = key.Substring(6).Trim();
                        if (!string.IsNullOrEmpty(devKey) && !string.IsNullOrEmpty(val))
                        {
                            settings.DeviceColors[devKey] = val;
                        }
                        continue;
                    }

                    if (key.StartsWith("Icon_", StringComparison.OrdinalIgnoreCase))
                    {
                        string devKey = key.Substring(5).Trim();
                        if (!string.IsNullOrEmpty(devKey) && !string.IsNullOrEmpty(val))
                        {
                            settings.DeviceIcons[devKey] = val;
                        }
                        continue;
                    }

                    if (key.StartsWith("Hidden_", StringComparison.OrdinalIgnoreCase))
                    {
                        string devKey = key.Substring(7).Trim();
                        bool hid;
                        if (bool.TryParse(val, out hid) && hid)
                        {
                            settings.HiddenDevices.Add(devKey);
                        }
                        continue;
                    }

                    if (key.StartsWith("Off_", StringComparison.OrdinalIgnoreCase))
                    {
                        string devKey = key.Substring(4).Trim();
                        bool isOff;
                        if (bool.TryParse(val, out isOff) && isOff)
                        {
                            settings.DisconnectedDevices.Add(devKey);
                        }
                        continue;
                    }

                    if (key.StartsWith("TrayOff_", StringComparison.OrdinalIgnoreCase))
                    {
                        string devKey = key.Substring(8).Trim();
                        bool isOff;
                        if (bool.TryParse(val, out isOff) && isOff)
                        {
                            settings.DisabledTrayDevices.Add(devKey);
                        }
                        continue;
                    }

                    switch (key.ToLowerInvariant())
                    {
                        case "refreshintervalseconds":
                            int ri;
                            if (int.TryParse(val, out ri) && ri >= 5 && ri <= 3600)
                                settings.RefreshIntervalSeconds = ri;
                            break;
                        case "lowbatterythreshold":
                            int lbt;
                            if (int.TryParse(val, out lbt) && lbt >= 5 && lbt <= 50)
                                settings.LowBatteryThreshold = lbt;
                            break;
                        case "enablenotifications":
                            bool en;
                            if (bool.TryParse(val, out en))
                                settings.EnableNotifications = en;
                            break;
                        case "startwithwindows":
                            bool sw;
                            if (bool.TryParse(val, out sw))
                                settings.StartWithWindows = sw;
                            break;
                        case "minimizetotrayonclose":
                            bool mc;
                            if (bool.TryParse(val, out mc))
                                settings.MinimizeToTrayOnClose = mc;
                            break;
                        case "showsystembattery":
                            bool ssb;
                            if (bool.TryParse(val, out ssb))
                                settings.ShowSystemBattery = ssb;
                            break;
                        case "backgroundimagepath":
                            settings.BackgroundImagePath = val;
                            break;
                        case "backgroundopacity":
                            int bo;
                            if (int.TryParse(val, out bo) && bo >= 0 && bo <= 100)
                                settings.BackgroundOpacity = bo;
                            break;
                        case "trayiconstyle":
                            TrayIconStyle ts;
                            if (Enum.TryParse(val, true, out ts))
                                settings.TrayStyle = ts;
                            break;
                        case "overlayenabled":
                            bool oe;
                            if (bool.TryParse(val, out oe))
                                settings.OverlayEnabled = oe;
                            break;
                        case "overlayx":
                            int ox;
                            if (int.TryParse(val, out ox))
                                settings.OverlayX = ox;
                            break;
                        case "overlayy":
                            int oy;
                            if (int.TryParse(val, out oy))
                                settings.OverlayY = oy;
                            break;
                        case "overlayvertical":
                            bool ov;
                            if (bool.TryParse(val, out ov))
                                settings.OverlayVertical = ov;
                            break;
                        case "overlayopacity":
                            int op;
                            if (int.TryParse(val, out op) && op >= 20 && op <= 100)
                                settings.OverlayOpacity = op;
                            break;
                    }
                }
            }
            catch
            {
                // Fallback to defaults
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Batuja - Monitor de Dispositivos sem Fio - Configurações");
                sb.AppendLine(string.Format("RefreshIntervalSeconds={0}", RefreshIntervalSeconds));
                sb.AppendLine(string.Format("LowBatteryThreshold={0}", LowBatteryThreshold));
                sb.AppendLine(string.Format("EnableNotifications={0}", EnableNotifications));
                sb.AppendLine(string.Format("StartWithWindows={0}", StartWithWindows));
                sb.AppendLine(string.Format("MinimizeToTrayOnClose={0}", MinimizeToTrayOnClose));
                sb.AppendLine(string.Format("ShowSystemBattery={0}", ShowSystemBattery));
                sb.AppendLine(string.Format("BackgroundImagePath={0}", BackgroundImagePath));
                sb.AppendLine(string.Format("BackgroundOpacity={0}", BackgroundOpacity));
                sb.AppendLine(string.Format("TrayIconStyle={0}", TrayStyle));
                sb.AppendLine(string.Format("OverlayEnabled={0}", OverlayEnabled));
                sb.AppendLine(string.Format("OverlayX={0}", OverlayX));
                sb.AppendLine(string.Format("OverlayY={0}", OverlayY));
                sb.AppendLine(string.Format("OverlayVertical={0}", OverlayVertical));
                sb.AppendLine(string.Format("OverlayOpacity={0}", OverlayOpacity));

                sb.AppendLine();
                sb.AppendLine("# Cores Personalizadas dos Dispositivos");
                foreach (KeyValuePair<string, string> kvp in DeviceColors)
                {
                    sb.AppendLine(string.Format("Color_{0}={1}", kvp.Key, kvp.Value));
                }

                sb.AppendLine();
                sb.AppendLine("# Ícones Personalizados dos Dispositivos");
                foreach (KeyValuePair<string, string> kvp in DeviceIcons)
                {
                    sb.AppendLine(string.Format("Icon_{0}={1}", kvp.Key, kvp.Value));
                }

                sb.AppendLine();
                sb.AppendLine("# Dispositivos Ocultos");
                foreach (string hd in HiddenDevices)
                {
                    sb.AppendLine(string.Format("Hidden_{0}=True", hd));
                }

                sb.AppendLine();
                sb.AppendLine("# Dispositivos Marcados como Desligados / Inativos");
                foreach (string dd in DisconnectedDevices)
                {
                    sb.AppendLine(string.Format("Off_{0}=True", dd));
                }

                sb.AppendLine();
                sb.AppendLine("# Dispositivos com Ícone da Barra de Tarefas Desativado");
                foreach (string td in DisabledTrayDevices)
                {
                    sb.AppendLine(string.Format("TrayOff_{0}=True", td));
                }

                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
