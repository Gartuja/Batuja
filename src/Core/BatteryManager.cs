using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using OmniBattery.Models;
using OmniBattery.Protocols;

namespace OmniBattery.Core
{
    public class BatteryManager
    {
        private readonly List<IProtocolScanner> _scanners;
        private readonly AppSettings _settings;
        private readonly Timer _pollTimer;
        private readonly object _lock = new object();
        private readonly Dictionary<string, DateTime> _notifiedDevices = new Dictionary<string, DateTime>();

        private List<BatteryDevice> _cachedDevices = new List<BatteryDevice>();
        private bool _isScanning = false;

        public event Action<List<BatteryDevice>> DevicesUpdated;
        public event Action<BatteryDevice, string> LowBatteryAlert;
        public event Action<bool> ScanStateChanged;

        public List<BatteryDevice> CurrentDevices
        {
            get
            {
                lock (_lock)
                {
                    return new List<BatteryDevice>(_cachedDevices);
                }
            }
        }

        public AppSettings Settings
        {
            get { return _settings; }
        }

        private readonly DeviceHistoryManager _history = new DeviceHistoryManager();

        public DeviceHistoryManager History
        {
            get { return _history; }
        }

        public BatteryManager(AppSettings settings)
        {
            _settings = settings;
            _scanners = new List<IProtocolScanner>
            {
                new CompxScanner(),        // Attack Shark RS6 Ultra, MCHOSE V9 Turbo, VK 87 Max V2, SinoWealth
                new LogitechScanner(),     // Logitech Lightspeed, Unifying, Bluetooth
                new XInputScanner(),       // Xbox Wireless, Anbernic, 8BitDo
                new BluetoothPnPScanner(), // Bluetooth BAS / HFP (JBL, AirPods, Sony, etc.)
                new GamepadHidScanner(),   // DualSense, DualShock 4, Switch Pro
                new GenericHidScanner(_settings.ShowSystemBattery) // Laptop/UPS
            };

            int intervalMs = Math.Max(5, _settings.RefreshIntervalSeconds) * 1000;
            _pollTimer = new Timer(OnPollTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            TriggerScanAsync();
            int intervalMs = Math.Max(5, _settings.RefreshIntervalSeconds) * 1000;
            _pollTimer.Change(intervalMs, intervalMs);
        }

        public void Stop()
        {
            _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void TriggerScanAsync()
        {
            if (_isScanning) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                PerformScan();
            });
        }

        private void OnPollTimer(object state)
        {
            PerformScan();
        }

        public void SetCustomDeviceColor(BatteryDevice device, Color color)
        {
            if (device == null) return;

            device.CustomColor = color;
            _settings.SetCustomColor(device.DeviceKey, color);
            _settings.SetCustomColor(device.Name, color);

            lock (_lock)
            {
                foreach (var d in _cachedDevices)
                {
                    if (d.Id == device.Id || d.Name == device.Name)
                    {
                        d.CustomColor = color;
                    }
                }
            }

            if (DevicesUpdated != null)
                DevicesUpdated(CurrentDevices);
        }

        public void ResetCustomDeviceColor(BatteryDevice device)
        {
            if (device == null) return;

            device.CustomColor = null;
            _settings.RemoveCustomColor(device.DeviceKey);
            _settings.RemoveCustomColor(device.Name);

            lock (_lock)
            {
                foreach (var d in _cachedDevices)
                {
                    if (d.Id == device.Id || d.Name == device.Name)
                    {
                        d.CustomColor = null;
                    }
                }
            }

            if (DevicesUpdated != null)
                DevicesUpdated(CurrentDevices);
        }

        public void SetDeviceIcon(BatteryDevice device, string iconVal)
        {
            if (device == null) return;

            _settings.SetCustomIcon(device.DeviceKey, iconVal);
            _settings.SetCustomIcon(device.Name, iconVal);

            ApplyCustomIconToDevice(device, iconVal);

            lock (_lock)
            {
                foreach (var d in _cachedDevices)
                {
                    if (d.Id == device.Id || d.Name == device.Name)
                    {
                        ApplyCustomIconToDevice(d, iconVal);
                    }
                }
            }

            if (DevicesUpdated != null)
                DevicesUpdated(CurrentDevices);
        }

        public void ResetDeviceIcon(BatteryDevice device)
        {
            if (device == null) return;

            _settings.RemoveCustomIcon(device.DeviceKey);
            _settings.RemoveCustomIcon(device.Name);

            device.CustomCategory = null;
            device.CustomIconPath = null;

            lock (_lock)
            {
                foreach (var d in _cachedDevices)
                {
                    if (d.Id == device.Id || d.Name == device.Name)
                    {
                        d.CustomCategory = null;
                        d.CustomIconPath = null;
                    }
                }
            }

            if (DevicesUpdated != null)
                DevicesUpdated(CurrentDevices);
        }

        public void SetDeviceTrayIconEnabled(BatteryDevice device, bool enabled)
        {
            if (device == null) return;

            device.IsTrayIconEnabled = enabled;
            _settings.SetTrayIconEnabled(device.DeviceKey, enabled);
            _settings.SetTrayIconEnabled(device.Name, enabled);

            lock (_lock)
            {
                foreach (var d in _cachedDevices)
                {
                    if (d.Id == device.Id || d.Name == device.Name)
                    {
                        d.IsTrayIconEnabled = enabled;
                    }
                }
            }

            if (DevicesUpdated != null)
                DevicesUpdated(CurrentDevices);
        }

        public void DisconnectDevice(BatteryDevice device)
        {
            if (device == null) return;

            _settings.SetDeviceManualDisconnected(device.DeviceKey, true);
            _settings.SetDeviceManualDisconnected(device.Name, true);
            _history.MarkDisconnected(device.DeviceKey);

            PerformScan();
        }

        public void ReconnectDevice(string deviceKey)
        {
            if (string.IsNullOrEmpty(deviceKey)) return;

            _settings.SetDeviceManualDisconnected(deviceKey, false);
            _settings.SetDeviceHidden(deviceKey, false);

            PerformScan();
        }

        public void SetDeviceHidden(BatteryDevice device, bool hidden)
        {
            if (device == null) return;

            device.IsHidden = hidden;
            _settings.SetDeviceHidden(device.DeviceKey, hidden);
            _settings.SetDeviceHidden(device.Name, hidden);

            List<BatteryDevice> updatedList;
            lock (_lock)
            {
                if (hidden)
                {
                    _cachedDevices.RemoveAll(d => (d.DeviceKey == device.DeviceKey || d.Name == device.Name));
                }
                updatedList = new List<BatteryDevice>(_cachedDevices);
            }

            if (DevicesUpdated != null)
                DevicesUpdated(updatedList);

            TriggerScanAsync();
        }

        public void SetDeviceConnected(BatteryDevice device, bool connected)
        {
            if (device == null) return;

            device.IsConnected = connected;
            // Se desconectado manualmente, salva em DisconnectedDevices
            _settings.SetDeviceManualDisconnected(device.DeviceKey, !connected);
            _settings.SetDeviceManualDisconnected(device.Name, !connected);

            lock (_lock)
            {
                foreach (var d in _cachedDevices)
                {
                    if (d.Id == device.Id || d.Name == device.Name)
                    {
                        d.IsConnected = connected;
                    }
                }
            }

            if (DevicesUpdated != null)
                DevicesUpdated(CurrentDevices);
        }

        private void ApplyCustomIconToDevice(BatteryDevice dev, string iconVal)
        {
            if (string.IsNullOrEmpty(iconVal))
            {
                dev.CustomCategory = null;
                dev.CustomIconPath = null;
                return;
            }

            if (File.Exists(iconVal))
            {
                dev.CustomIconPath = iconVal;
                dev.CustomCategory = null;
                return;
            }

            switch (iconVal.ToLowerInvariant())
            {
                case "mouse":
                    dev.CustomCategory = DeviceCategory.Mouse;
                    dev.CustomIconPath = null;
                    break;
                case "teclado":
                case "keyboard":
                    dev.CustomCategory = DeviceCategory.Teclado;
                    dev.CustomIconPath = null;
                    break;
                case "fone":
                case "headset":
                case "fonedeouvido":
                    dev.CustomCategory = DeviceCategory.FoneDeOuvido;
                    dev.CustomIconPath = null;
                    break;
                case "controle":
                case "gamepad":
                    dev.CustomCategory = DeviceCategory.Controle;
                    dev.CustomIconPath = null;
                    break;
                case "laptop":
                case "notebook":
                    dev.CustomCategory = DeviceCategory.Laptop;
                    dev.CustomIconPath = null;
                    break;
                case "bateria":
                case "battery":
                case "generico":
                    dev.CustomCategory = DeviceCategory.Generico;
                    dev.CustomIconPath = null;
                    break;
            }
        }

        private void PerformScan()
        {
            lock (_lock)
            {
                if (_isScanning) return;
                _isScanning = true;
            }

            if (ScanStateChanged != null)
                ScanStateChanged(true);

            Dictionary<string, BatteryDevice> detected = new Dictionary<string, BatteryDevice>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (IProtocolScanner scanner in _scanners)
                {
                    try
                    {
                        List<BatteryDevice> results = scanner.Scan();
                        if (results != null)
                        {
                            foreach (BatteryDevice dev in results)
                            {
                                if (dev == null) continue;

                                // Filtro estrito do usuario: apenas fones de ouvido, mouse e teclado
                                if (dev.Category != DeviceCategory.Mouse &&
                                    dev.Category != DeviceCategory.Teclado &&
                                    dev.Category != DeviceCategory.FoneDeOuvido)
                                {
                                    continue;
                                }

                                // REGRA DO USUÁRIO: MoniTuja monitora exclusivamente dispositivos SEM FIO!
                                // Dispositivos conectados diretamente por cabo USB (IsWired) ou sem bateria não entram
                                if (dev.IsWired || !dev.HasBattery)
                                {
                                    continue;
                                }

                                // Se o dispositivo sem fio não retornou telemetria instantânea, recupera a última carga conhecida do histórico
                                if (dev.BatteryPercent < 0)
                                {
                                    var hist = _history.GetAll().Find(h => h.Key == dev.DeviceKey || h.Name == dev.Name);
                                    if (hist != null && hist.LastBatteryPercent >= 0)
                                    {
                                        dev.BatteryPercent = hist.LastBatteryPercent;
                                    }
                                }

                                // Check if user hid or turned off this device
                                if (_settings.IsDeviceHidden(dev.DeviceKey) || _settings.IsDeviceHidden(dev.Name) ||
                                    _settings.IsDeviceManualDisconnected(dev.DeviceKey) || _settings.IsDeviceManualDisconnected(dev.Name))
                                {
                                    _history.RecordDevice(dev, false);
                                    continue; // Não aparece mais nos dispositivos conectados
                                }

                                // Se o dispositivo estiver desligado ou desconectado, não exibe na tela principal nem na bandeja
                                if (!dev.IsConnected)
                                {
                                    _history.RecordDevice(dev, false);
                                    continue;
                                }

                                // Attach tray icon state
                                dev.IsTrayIconEnabled = _settings.IsTrayIconEnabled(dev.DeviceKey) && _settings.IsTrayIconEnabled(dev.Name);

                                // Record in history
                                _history.RecordDevice(dev, dev.IsConnected);

                                // Attach custom color if configured
                                Color? custom = _settings.GetCustomColor(dev.DeviceKey);
                                if (!custom.HasValue)
                                    custom = _settings.GetCustomColor(dev.Name);
                                if (custom.HasValue)
                                    dev.CustomColor = custom.Value;

                                // Attach custom icon if configured
                                string customIcon = _settings.GetCustomIcon(dev.DeviceKey);
                                if (string.IsNullOrEmpty(customIcon))
                                    customIcon = _settings.GetCustomIcon(dev.Name);
                                if (!string.IsNullOrEmpty(customIcon))
                                    ApplyCustomIconToDevice(dev, customIcon);

                                string key = !string.IsNullOrEmpty(dev.Id) ? dev.Id : dev.Name;

                                if (!detected.ContainsKey(key))
                                {
                                    detected[key] = dev;
                                }
                                else
                                {
                                    if (dev.BatteryPercent >= 0)
                                    {
                                        detected[key].BatteryPercent = dev.BatteryPercent;
                                        detected[key].IsCharging = dev.IsCharging;
                                        detected[key].LastUpdated = dev.LastUpdated;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                List<BatteryDevice> updatedList = new List<BatteryDevice>(detected.Values);

                lock (_lock)
                {
                    _cachedDevices = updatedList;
                    _isScanning = false;
                }

                if (ScanStateChanged != null)
                    ScanStateChanged(false);

                if (DevicesUpdated != null)
                    DevicesUpdated(updatedList);

                CheckLowBatteryAlerts(updatedList);
            }
        }

        private void CheckLowBatteryAlerts(List<BatteryDevice> devices)
        {
            if (!_settings.EnableNotifications) return;

            int threshold = _settings.LowBatteryThreshold;
            DateTime now = DateTime.Now;

            foreach (BatteryDevice dev in devices)
            {
                if (dev.IsConnected && !dev.IsCharging && dev.BatteryPercent > 0 && dev.BatteryPercent <= threshold)
                {
                    string alertKey = dev.Name + "_" + dev.Id;
                    bool canNotify = true;

                    if (_notifiedDevices.ContainsKey(alertKey))
                    {
                        TimeSpan diff = now - _notifiedDevices[alertKey];
                        if (diff.TotalMinutes < 30)
                        {
                            canNotify = false;
                        }
                    }

                    if (canNotify)
                    {
                        _notifiedDevices[alertKey] = now;
                        string msg = string.Format("O dispositivo '{0}' está com bateria fraca ({1}%). Conecte ao carregador!",
                            dev.Name, dev.BatteryPercent);

                        if (LowBatteryAlert != null)
                            LowBatteryAlert(dev, msg);
                    }
                }
            }
        }

        public void UpdateRefreshInterval(int seconds)
        {
            _settings.RefreshIntervalSeconds = seconds;
            _settings.Save();
            int ms = Math.Max(5, seconds) * 1000;
            _pollTimer.Change(ms, ms);
        }
    }
}
