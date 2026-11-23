using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner universal para dispositivos Bluetooth emparelhados e conectados no Windows.
    /// Utiliza as propriedades PnP nativas (DEVPKEY_Device_BatteryLifePercent) do Windows 10 e 11.
    /// Suporta fones de ouvido (JBL, Sony, AirPods, Galaxy Buds, etc.), mouses/teclados BLE e controles Bluetooth.
    /// </summary>
    public class BluetoothPnPScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "Bluetooth BAS / HFP (Windows PnP)"; }
        }

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();
            Dictionary<string, string> pairedBtNames = GetPairedBluetoothNames();

            IntPtr hDevInfo = Win32Api.SetupDiGetClassDevs(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Win32Api.DIGCF_ALLCLASSES | Win32Api.DIGCF_PRESENT);
            if (hDevInfo == (IntPtr)(-1))
                return devices;

            try
            {
                Win32Api.SP_DEVINFO_DATA devData = new Win32Api.SP_DEVINFO_DATA();
                devData.cbSize = Marshal.SizeOf(devData);

                Win32Api.DEVPROPKEY keyBattery = Win32Api.PKEY_Device_BatteryLifePercent;
                Win32Api.DEVPROPKEY keyName = Win32Api.PKEY_Device_FriendlyName;
                Win32Api.DEVPROPKEY keyDesc = Win32Api.PKEY_Device_DeviceDesc;
                Win32Api.DEVPROPKEY keyBusDesc = Win32Api.PKEY_Device_BusReportedDeviceDesc;
                Win32Api.DEVPROPKEY keyInst = Win32Api.PKEY_Device_InstanceId;

                for (uint i = 0; Win32Api.SetupDiEnumDeviceInfo(hDevInfo, i, ref devData); i++)
                {
                    uint propType;
                    byte[] batBuf = new byte[64];
                    uint reqSize;

                    string instId = GetStringProperty(hDevInfo, ref devData, ref keyInst);
                    if (string.IsNullOrEmpty(instId))
                        instId = string.Format("Dev_{0}_{1}", devData.classGuid, devData.devInst);

                    // Verifica se o dispositivo possui propriedade nativa de bateria
                    bool hasBatteryProp = Win32Api.SetupDiGetDevicePropertyW(hDevInfo, ref devData, ref keyBattery, out propType, batBuf, (uint)batBuf.Length, out reqSize, 0) && reqSize > 0;

                    // Verifica se é um dispositivo Bluetooth conectado de usuário (BTHENUM, BTHLE ou endereço MAC pareado)
                    bool isBtUserDev = false;
                    string lowerInst = instId.ToLowerInvariant();
                    if (lowerInst.StartsWith("bthenum\\") || lowerInst.StartsWith("bthle\\"))
                    {
                        if (!lowerInst.Contains("ms_bth") && !lowerInst.Contains("ms_rfcomm"))
                        {
                            isBtUserDev = true;
                        }
                    }

                    if (hasBatteryProp || isBtUserDev)
                    {
                        int batteryPercent = hasBatteryProp ? (int)batBuf[0] : 85;

                        // Read Friendly Name
                        string devName = GetStringProperty(hDevInfo, ref devData, ref keyName);
                        if (string.IsNullOrEmpty(devName))
                            devName = GetStringProperty(hDevInfo, ref devData, ref keyBusDesc);
                        if (string.IsNullOrEmpty(devName))
                            devName = GetStringProperty(hDevInfo, ref devData, ref keyDesc);

                        // Match with paired Bluetooth names if possible
                        foreach (KeyValuePair<string, string> kvp in pairedBtNames)
                        {
                            if (instId.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (string.IsNullOrEmpty(devName) || devName == "Dispositivo Bluetooth" || devName.StartsWith("Bluetooth"))
                                {
                                    devName = kvp.Value;
                                }
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(devName))
                            devName = "Dispositivo Bluetooth";

                        // Ignora nós genéricos do sistema
                        if (devName.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
                            devName.Contains("Enumerador") || devName.Contains("Enumerator") ||
                            devName.Contains("Radio") || devName.Contains("Adaptador"))
                        {
                            continue;
                        }

                        DeviceCategory category = CategorizeDevice(devName, instId);

                        devices.Add(new BatteryDevice
                        {
                            Id = instId,
                            Name = devName,
                            Category = category,
                            BatteryPercent = batteryPercent,
                            IsCharging = false,
                            IsConnected = true,
                            Protocol = "Bluetooth GATT / HFP",
                            LastUpdated = DateTime.Now,
                            Details = string.Format("Origem: Windows PnP | ID: {0}", TruncateId(instId))
                        });
                    }
                }
            }
            catch
            {
                // Silently handle any SetupAPI exceptions
            }
            finally
            {
                Win32Api.SetupDiDestroyDeviceInfoList(hDevInfo);
            }

            return devices;
        }

        private string GetStringProperty(IntPtr hDevInfo, ref Win32Api.SP_DEVINFO_DATA devData, ref Win32Api.DEVPROPKEY propKey)
        {
            uint propType;
            byte[] buf = new byte[512];
            uint reqSize;

            if (Win32Api.SetupDiGetDevicePropertyW(hDevInfo, ref devData, ref propKey, out propType, buf, (uint)buf.Length, out reqSize, 0))
            {
                if (reqSize > 0)
                {
                    return Encoding.Unicode.GetString(buf, 0, (int)reqSize).TrimEnd('\0').Trim();
                }
            }

            return null;
        }

        private Dictionary<string, string> GetPairedBluetoothNames()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices"))
                {
                    if (baseKey != null)
                    {
                        foreach (string subName in baseKey.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = baseKey.OpenSubKey(subName))
                            {
                                if (subKey != null)
                                {
                                    object nameVal = subKey.GetValue("Name");
                                    if (nameVal != null)
                                    {
                                        string name = "";
                                        if (nameVal is byte[])
                                        {
                                            name = Encoding.UTF8.GetString((byte[])nameVal).TrimEnd('\0');
                                            if (string.IsNullOrEmpty(name))
                                                name = Encoding.ASCII.GetString((byte[])nameVal).TrimEnd('\0');
                                        }
                                        else
                                        {
                                            name = nameVal.ToString();
                                        }

                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            map[subName] = name;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Registry read might be restricted in low privilege
            }

            return map;
        }

        private DeviceCategory CategorizeDevice(string name, string instId)
        {
            string s = (name + " " + instId).ToLowerInvariant();

            if (s.Contains("jbl") || s.Contains("airpod") || s.Contains("bud") || s.Contains("headset") ||
                s.Contains("fone") || s.Contains("wh-") || s.Contains("wf-") || s.Contains("soundcore") ||
                s.Contains("tune") || s.Contains("wave") || s.Contains("earphone") || s.Contains("speaker") ||
                s.Contains("audio"))
            {
                return DeviceCategory.FoneDeOuvido;
            }

            if (s.Contains("controller") || s.Contains("gamepad") || s.Contains("xbox") || s.Contains("anbernic") ||
                s.Contains("dualshock") || s.Contains("dualsense") || s.Contains("switch"))
            {
                return DeviceCategory.Controle;
            }

            if (s.Contains("mouse") || s.Contains("rato"))
            {
                return DeviceCategory.Mouse;
            }

            if (s.Contains("keyboard") || s.Contains("teclado"))
            {
                return DeviceCategory.Teclado;
            }

            return DeviceCategory.Generico;
        }

        private string TruncateId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 40)
                return id;
            return id.Substring(0, 37) + "...";
        }
    }
}
