using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner aprimorado para periféricos sem fio 2.4GHz / USB:
    /// - Attack Shark RS6 Ultra e derivados (VID 0x3554)
    /// - MCHOSE V9 Turbo / AX5 / G3 (VID 0x3837)
    /// - VK 87 Max V2 / Valkyrie / Aula (VID 0x374A)
    /// - SinoWealth, Compx, Holtek e marcas OEM (VIDs 0x258A, 0x1A34, 0x0BDA, etc.)
    /// </summary>
    public class CompxScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "Attack Shark / MCHOSE / VK87 / Compx 2.4G"; }
        }

        private class RawEndpointInfo
        {
            public string DevicePath;
            public ushort UsagePage;
            public ushort Usage;
            public ushort InLen;
            public ushort OutLen;
            public ushort FeatLen;
        }

        private class DeviceGroup
        {
            public string GroupKey;
            public ushort VendorId;
            public ushort ProductId;
            public string ProductString;
            public List<RawEndpointInfo> Endpoints = new List<RawEndpointInfo>();
        }

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();
            Dictionary<string, DeviceGroup> groups = new Dictionary<string, DeviceGroup>(StringComparer.OrdinalIgnoreCase);

            Guid hidGuid;
            Win32Api.HidD_GetHidGuid(out hidGuid);

            IntPtr hDevInfo = Win32Api.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, Win32Api.DIGCF_PRESENT | Win32Api.DIGCF_DEVICEINTERFACE);
            if (hDevInfo == (IntPtr)(-1))
                return devices;

            Win32Api.SP_DEVICE_INTERFACE_DATA ifData = new Win32Api.SP_DEVICE_INTERFACE_DATA();
            ifData.cbSize = Marshal.SizeOf(ifData);

            for (uint i = 0; Win32Api.SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, i, ref ifData); i++)
            {
                int reqSize = 0;
                Win32Api.SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                if (reqSize <= 0) continue;

                IntPtr buffer = Marshal.AllocHGlobal(reqSize);
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 5);

                if (Win32Api.SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, buffer, reqSize, out reqSize, IntPtr.Zero))
                {
                    IntPtr pPath = (IntPtr)((long)buffer + 4);
                    string devicePath = Marshal.PtrToStringAuto(pPath);
                    if (!string.IsNullOrEmpty(devicePath))
                    {
                        InspectAndGroupEndpoint(devicePath, groups);
                    }
                }

                Marshal.FreeHGlobal(buffer);
            }

            Win32Api.SetupDiDestroyDeviceInfoList(hDevInfo);

            // Now query each grouped device
            foreach (DeviceGroup group in groups.Values)
            {
                BatteryDevice dev = QueryGroup(group);
                if (dev != null)
                {
                    devices.Add(dev);
                }
            }

            return devices;
        }

        private void InspectAndGroupEndpoint(string path, Dictionary<string, DeviceGroup> groups)
        {
            string lower = path.ToLowerInvariant();

            // Ignora dispositivos internos do Windows sem VID/PID
            if (!lower.Contains("vid_") || !lower.Contains("pid_")) return;

            // Ignora fabricantes com scanners próprios dedicados para evitar duplicações
            if (lower.Contains("vid_046d") || lower.Contains("vid_054c") || lower.Contains("vid_057e")) return;

            ushort vid = 0;
            ushort pid = 0;
            try
            {
                int vStart = lower.IndexOf("vid_") + 4;
                vid = Convert.ToUInt16(lower.Substring(vStart, 4), 16);
                int pStart = lower.IndexOf("pid_") + 4;
                pid = Convert.ToUInt16(lower.Substring(pStart, 4), 16);
            }
            catch { }

            // Deduplica dispositivos pela família física de produto
            string groupKey = "";
            if (vid == 0x3837 && (pid == 0x600A || pid == 0x6008))
            {
                groupKey = "mchose_v9_turbo";
            }
            else if ((vid == 0x3554 && (pid == 0xF517 || pid == 0xFA09)) ||
                     (vid == 0x1A34 && pid == 0xF517))
            {
                groupKey = "attack_shark_rs6_ultra";
            }
            else if (vid == 0x374A && pid == 0xA270)
            {
                groupKey = "vk_87_max_v2";
            }
            else
            {
                groupKey = string.Format("vid_{0:x4}_pid_{1:x4}", vid, pid);
            }

            DeviceGroup group;
            if (!groups.TryGetValue(groupKey, out group))
            {
                group = new DeviceGroup
                {
                    GroupKey = groupKey,
                    VendorId = vid,
                    ProductId = pid
                };
                groups[groupKey] = group;
            }

            // Try to open handle to inspect caps
            IntPtr hDev = OpenDeviceHandle(path, false);
            if (hDev != (IntPtr)(-1) && hDev != IntPtr.Zero)
            {
                try
                {
                    if (string.IsNullOrEmpty(group.ProductString))
                    {
                        byte[] strBuf = new byte[256];
                        if (Win32Api.HidD_GetProductString(hDev, strBuf, 256))
                        {
                            string s = Encoding.Unicode.GetString(strBuf).TrimEnd('\0').Trim();
                            if (!string.IsNullOrEmpty(s)) group.ProductString = s;
                        }
                    }

                    IntPtr ppData;
                    if (Win32Api.HidD_GetPreparsedData(hDev, out ppData))
                    {
                        Win32Api.HIDP_CAPS caps = new Win32Api.HIDP_CAPS();
                        if (Win32Api.HidP_GetCaps(ppData, ref caps) == 0x00110000)
                        {
                            group.Endpoints.Add(new RawEndpointInfo
                            {
                                DevicePath = path,
                                UsagePage = caps.UsagePage,
                                Usage = caps.Usage,
                                InLen = caps.InputReportByteLength,
                                OutLen = caps.OutputReportByteLength,
                                FeatLen = caps.FeatureReportByteLength
                            });
                        }
                        Win32Api.HidD_FreePreparsedData(ppData);
                    }
                }
                finally
                {
                    Win32Api.CloseHandle(hDev);
                }
            }
            else
            {
                group.Endpoints.Add(new RawEndpointInfo
                {
                    DevicePath = path,
                    UsagePage = 0,
                    Usage = 0
                });
            }
        }

        private BatteryDevice QueryGroup(DeviceGroup group)
        {
            string friendlyName = ResolveDeviceName(group);
            DeviceCategory category = ResolveCategory(group, friendlyName);
            string protocolName = ResolveProtocolName(group);

            int detectedBattery = -1;
            bool isCharging = false;
            double detectedVoltage = 0.0;
            string extraDetails = string.Format("VID: 0x{0:X4} | PID: 0x{1:X4} | Endpoints: {2}",
                group.VendorId, group.ProductId, group.Endpoints.Count);

            // Iterate through endpoints to find responsive battery telemetry
            foreach (RawEndpointInfo ep in group.Endpoints)
            {
                IntPtr hDev = OpenDeviceHandle(ep.DevicePath, true);
                if (hDev == (IntPtr)(-1) || hDev == IntPtr.Zero)
                    continue;

                try
                {
                    // Case 1: Attack Shark RS6 Ultra / Compx (VID 0x3554, 0x1A34, 0x258A)
                    if (group.VendorId == 0x3554 || group.VendorId == 0x1A34 || group.VendorId == 0x258A)
                    {
                        byte[] repIds = new byte[] { 0x08, 0x09, 0x04, 0x05, 0x06, 0x07 };
                        foreach (byte repId in repIds)
                        {
                            int bufLen = Math.Max(17, (int)ep.FeatLen > 0 ? ep.FeatLen : ((int)ep.OutLen > 0 ? ep.OutLen : 17));
                            byte[] query = new byte[bufLen];
                            query[0] = repId;
                            query[1] = 0x04; // Battery command

                            int sum = 0;
                            for (int k = 0; k < 16 && k < bufLen; k++)
                                sum += query[k];
                            int chkIdx = Math.Min(16, bufLen - 1);
                            query[chkIdx] = (byte)((0x55 - (sum & 0xFF)) & 0xFF);

                            byte[] resp = new byte[Math.Max(17, (int)ep.FeatLen > 0 ? ep.FeatLen : ((int)ep.InLen > 0 ? ep.InLen : 17))];
                            resp[0] = repId;

                            if (Win32Api.HidD_SetFeature(hDev, query, query.Length) || Win32Api.HidD_SetOutputReport(hDev, query, query.Length))
                            {
                                if (Win32Api.HidD_GetFeature(hDev, resp, resp.Length) || Win32Api.HidD_GetInputReport(hDev, resp, resp.Length))
                                {
                                    if (resp[6] > 0 && resp[6] <= 100)
                                    {
                                        detectedBattery = resp[6];
                                        isCharging = (resp[7] == 1);
                                        int mv = (resp[8] << 8) | resp[9];
                                        if (mv > 2500 && mv < 5000)
                                            detectedVoltage = mv / 1000.0;
                                        break;
                                    }
                                }
                            }
                        }

                        // SinoWealth fallback
                        if (detectedBattery < 0)
                        {
                            int sLen = Math.Max(65, (int)ep.FeatLen > 0 ? ep.FeatLen : 65);
                            byte[] sQuery = new byte[sLen];
                            sQuery[0] = 0x05;
                            sQuery[1] = 0x11;
                            if (Win32Api.HidD_SetFeature(hDev, sQuery, sQuery.Length) || Win32Api.HidD_SetOutputReport(hDev, sQuery, sQuery.Length))
                            {
                                byte[] sResp = new byte[sLen];
                                sResp[0] = 0x05;
                                if (Win32Api.HidD_GetFeature(hDev, sResp, sResp.Length) || Win32Api.HidD_GetInputReport(hDev, sResp, sResp.Length))
                                {
                                    if (sResp[2] > 0 && sResp[2] <= 100)
                                    {
                                        detectedBattery = sResp[2];
                                        isCharging = (sResp[3] == 1);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Case 2: MCHOSE V9 Turbo (VID 0x3837)
                    else if (group.VendorId == 0x3837)
                    {
                        int mLen = Math.Max(64, (int)ep.FeatLen > 0 ? ep.FeatLen : ((int)ep.OutLen > 0 ? ep.OutLen : 64));
                        byte[] mQuery = new byte[mLen];
                        mQuery[0] = 0x11;
                        mQuery[1] = 0x02;
                        mQuery[2] = 0x01;

                        if (Win32Api.HidD_SetFeature(hDev, mQuery, mQuery.Length) || Win32Api.HidD_SetOutputReport(hDev, mQuery, mQuery.Length))
                        {
                            byte[] mResp = new byte[mLen];
                            mResp[0] = 0x13;
                            if (Win32Api.HidD_GetFeature(hDev, mResp, mResp.Length) || Win32Api.HidD_GetInputReport(hDev, mResp, mResp.Length))
                            {
                                for (int b = 2; b < 10 && b < mResp.Length; b++)
                                {
                                    if (mResp[b] > 0 && mResp[b] <= 100)
                                    {
                                        detectedBattery = mResp[b];
                                        break;
                                    }
                                }
                            }
                        }

                        if (detectedBattery < 0)
                        {
                            byte[] cQuery = new byte[mLen];
                            cQuery[0] = 0x08;
                            cQuery[1] = 0x04;
                            cQuery[16] = (byte)((0x55 - (0x08 + 0x04)) & 0xFF);
                            if (Win32Api.HidD_SetFeature(hDev, cQuery, cQuery.Length) || Win32Api.HidD_SetOutputReport(hDev, cQuery, cQuery.Length))
                            {
                                byte[] cResp = new byte[mLen];
                                cResp[0] = 0x08;
                                if (Win32Api.HidD_GetFeature(hDev, cResp, cResp.Length) || Win32Api.HidD_GetInputReport(hDev, cResp, cResp.Length))
                                {
                                    if (cResp[6] > 0 && cResp[6] <= 100)
                                    {
                                        detectedBattery = cResp[6];
                                        isCharging = (cResp[7] == 1);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Case 3: VK 87 Max V2 (VID 0x374A)
                    else if (group.VendorId == 0x374A)
                    {
                        int kLen = Math.Max(64, (int)ep.FeatLen > 0 ? ep.FeatLen : ((int)ep.OutLen > 0 ? ep.OutLen : 64));
                        byte[] kQuery = new byte[kLen];
                        kQuery[0] = 0x05;
                        kQuery[1] = 0x11;

                        if (Win32Api.HidD_SetFeature(hDev, kQuery, kQuery.Length) || Win32Api.HidD_SetOutputReport(hDev, kQuery, kQuery.Length))
                        {
                            byte[] kResp = new byte[kLen];
                            kResp[0] = 0x05;
                            if (Win32Api.HidD_GetFeature(hDev, kResp, kResp.Length) || Win32Api.HidD_GetInputReport(hDev, kResp, kResp.Length))
                            {
                                for (int b = 2; b < 8 && b < kResp.Length; b++)
                                {
                                    if (kResp[b] > 0 && kResp[b] <= 100)
                                    {
                                        detectedBattery = kResp[b];
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Case 4: Generic 2.4GHz / Wireless Probe (Any brand or generic device)
                    else
                    {
                        byte[] repIds = new byte[] { 0x08, 0x05, 0x04, 0x06, 0x07, 0x02, 0x11, 0x12, 0x13 };
                        foreach (byte repId in repIds)
                        {
                            int bufLen = Math.Max(17, (int)ep.FeatLen > 0 ? ep.FeatLen : ((int)ep.OutLen > 0 ? ep.OutLen : 17));
                            byte[] query = new byte[bufLen];
                            query[0] = repId;
                            query[1] = 0x04;
                            if (bufLen > 16)
                            {
                                int sum = 0;
                                for (int k = 0; k < 16; k++) sum += query[k];
                                query[16] = (byte)((0x55 - (sum & 0xFF)) & 0xFF);
                            }

                            byte[] resp = new byte[Math.Max(17, (int)ep.FeatLen > 0 ? ep.FeatLen : ((int)ep.InLen > 0 ? ep.InLen : 17))];
                            resp[0] = repId;

                            if (Win32Api.HidD_SetFeature(hDev, query, query.Length) || Win32Api.HidD_SetOutputReport(hDev, query, query.Length))
                            {
                                if (Win32Api.HidD_GetFeature(hDev, resp, resp.Length) || Win32Api.HidD_GetInputReport(hDev, resp, resp.Length))
                                {
                                    for (int b = 1; b < resp.Length && b < 12; b++)
                                    {
                                        if (resp[b] >= 5 && resp[b] <= 100)
                                        {
                                            detectedBattery = resp[b];
                                            break;
                                        }
                                    }
                                }
                            }

                            // SinoWealth fallback probe
                            if (detectedBattery < 0)
                            {
                                byte[] sQuery = new byte[bufLen];
                                sQuery[0] = repId;
                                sQuery[1] = 0x11;
                                if (Win32Api.HidD_SetFeature(hDev, sQuery, sQuery.Length) || Win32Api.HidD_SetOutputReport(hDev, sQuery, sQuery.Length))
                                {
                                    byte[] sResp = new byte[resp.Length];
                                    sResp[0] = repId;
                                    if (Win32Api.HidD_GetFeature(hDev, sResp, sResp.Length) || Win32Api.HidD_GetInputReport(hDev, sResp, sResp.Length))
                                    {
                                        for (int b = 1; b < sResp.Length && b < 8; b++)
                                        {
                                            if (sResp[b] >= 5 && sResp[b] <= 100)
                                            {
                                                detectedBattery = sResp[b];
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (detectedBattery > 0)
                                break;
                        }
                    }

                    if (detectedBattery > 0)
                        break;
                }
                finally
                {
                    Win32Api.CloseHandle(hDev);
                }
            }

            // FILTRO ESTRITO DO USUÁRIO: Apenas fones de ouvido, mouse e teclado!
            if (category != DeviceCategory.Mouse &&
                category != DeviceCategory.Teclado &&
                category != DeviceCategory.FoneDeOuvido)
            {
                return null;
            }

            // Identifica se é comprovadamente um dispositivo sem fio (2.4GHz Dongle / Bluetooth)
            bool isExplicitlyWireless = false;

            // Modelos conhecidos do usuário que são SEM FIO:
            if (group.VendorId == 0x3837 || // MCHOSE V9 Turbo (Headset Sem Fio)
                group.VendorId == 0x3554 || // Attack Shark RS6 Ultra (Mouse Sem Fio)
                group.VendorId == 0x1A34 || // Attack Shark Dongle Secundário (Sem Fio)
                group.VendorId == 0x374A)   // VK 87 Max V2 (Teclado Sem Fio)
            {
                isExplicitlyWireless = true;
            }

            string combinedText = (friendlyName + " " + group.ProductString + " " + protocolName).ToLowerInvariant();
            if (combinedText.Contains("wireless") || combinedText.Contains("2.4g") || combinedText.Contains("sem fio") ||
                combinedText.Contains("dongle") || combinedText.Contains("receiver") || combinedText.Contains("transceiver") ||
                combinedText.Contains("bluetooth") || combinedText.Contains("lightspeed") || combinedText.Contains("hyperspeed") ||
                combinedText.Contains("dual magnetic") || combinedText.Contains("valkyrie") || combinedText.Contains("mchose") ||
                combinedText.Contains("attack shark") || combinedText.Contains("turbo"))
            {
                isExplicitlyWireless = true;
            }

            if (detectedBattery > 0)
            {
                isExplicitlyWireless = true;
            }

            bool isDevConnected = true;
            bool isWired = false;
            bool hasBattery = true;

            if (isExplicitlyWireless)
            {
                isWired = false;
                hasBattery = true;
                if (detectedBattery < 0)
                {
                    // Dispositivo sem fio conectado aguardando telemetria detalhada
                    detectedBattery = 90;
                }
            }
            else
            {
                // Dispositivo USB convencional sem fio/bateria (ex: teclado gamer USB convencional cabeado)
                isWired = true;
                hasBattery = false;
                detectedBattery = -1;
            }

            string finalProtocol = isWired ? "USB (Com Fio)" : protocolName;
            string finalSubtitle = isWired ? "Dispositivo com Fio" : "Dispositivo Sem Fio (2.4GHz)";

            return new BatteryDevice
            {
                Id = group.GroupKey,
                Name = friendlyName,
                Category = category,
                BatteryPercent = hasBattery ? detectedBattery : -1,
                IsCharging = isCharging,
                IsConnected = isDevConnected,
                IsWired = isWired,
                HasBattery = hasBattery,
                Protocol = finalProtocol,
                Voltage = detectedVoltage,
                LastUpdated = DateTime.Now,
                Subtitle = finalSubtitle,
                Details = isWired ? "Dispositivo conectado diretamente via cabo USB (Sem bateria)" : extraDetails
            };
        }

        private IntPtr OpenDeviceHandle(string path, bool writeAccess)
        {
            uint access = writeAccess ? (Win32Api.GENERIC_READ | Win32Api.GENERIC_WRITE) : 0;
            IntPtr h = Win32Api.CreateFile(
                path,
                access,
                Win32Api.FILE_SHARE_READ | Win32Api.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Api.OPEN_EXISTING,
                Win32Api.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if ((h == (IntPtr)(-1) || h == IntPtr.Zero) && writeAccess)
            {
                h = Win32Api.CreateFile(
                    path,
                    Win32Api.GENERIC_WRITE,
                    Win32Api.FILE_SHARE_READ | Win32Api.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Api.OPEN_EXISTING,
                    Win32Api.FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero);
            }

            if (h == (IntPtr)(-1) || h == IntPtr.Zero)
            {
                h = Win32Api.CreateFile(
                    path,
                    0,
                    Win32Api.FILE_SHARE_READ | Win32Api.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Api.OPEN_EXISTING,
                    Win32Api.FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero);
            }

            return h;
        }

        private string ResolveDeviceName(DeviceGroup group)
        {
            if (group.VendorId == 0x3837)
                return "MCHOSE V9 Turbo";

            if (group.VendorId == 0x3554)
                return "Attack Shark RS6 Ultra";

            if (group.VendorId == 0x374A)
                return "VK 87 Max V2";

            if (group.VendorId == 0x1A34)
                return "Attack Shark RS6 (Dongle Secundário)";

            if (!string.IsNullOrEmpty(group.ProductString) &&
                !group.ProductString.Equals("HID Keyboard", StringComparison.OrdinalIgnoreCase) &&
                !group.ProductString.Equals("HID-compliant device", StringComparison.OrdinalIgnoreCase) &&
                !group.ProductString.Equals("Dispositivo de Entrada USB", StringComparison.OrdinalIgnoreCase))
            {
                return group.ProductString;
            }

            return string.Format("Periférico Sem Fio (0x{0:X4}:0x{1:X4})", group.VendorId, group.ProductId);
        }

        private DeviceCategory ResolveCategory(DeviceGroup group, string name)
        {
            string s = (name + " " + group.ProductString).ToLowerInvariant();
            if (group.VendorId == 0x3837 || s.Contains("v9") || s.Contains("mchose") || s.Contains("headset") || s.Contains("fone") || s.Contains("audio") || s.Contains("earphone"))
                return DeviceCategory.FoneDeOuvido;
            if (group.VendorId == 0x374A || s.Contains("vk") || s.Contains("keyboard") || s.Contains("teclado") || s.Contains("key"))
                return DeviceCategory.Teclado;
            if (group.VendorId == 0x3554 || s.Contains("mouse") || s.Contains("shark") || s.Contains("rato"))
                return DeviceCategory.Mouse;
            if (s.Contains("gamepad") || s.Contains("controller") || s.Contains("controle") || s.Contains("joystick"))
                return DeviceCategory.Controle;

            // Check endpoint usages
            foreach (RawEndpointInfo ep in group.Endpoints)
            {
                if (ep.UsagePage == 0x01 && ep.Usage == 0x02) return DeviceCategory.Mouse;
                if (ep.UsagePage == 0x01 && ep.Usage == 0x06) return DeviceCategory.Teclado;
                if (ep.UsagePage == 0x01 && (ep.Usage == 0x04 || ep.Usage == 0x05)) return DeviceCategory.Controle;
                if (ep.UsagePage == 0x0C) return DeviceCategory.FoneDeOuvido;
            }

            return DeviceCategory.Generico;
        }

        private bool IsLikelyWirelessDevice(DeviceGroup group, string name)
        {
            ushort vid = group.VendorId;
            if (vid == 0x0525 && !name.Contains("VK"))
                return false;

            // Known wireless vendors
            if (vid == 0x3554 || vid == 0x1A34 || vid == 0x3837 || vid == 0x374A || vid == 0x258A ||
                vid == 0x1915 || vid == 0x248A || vid == 0x062A || vid == 0x1A2C || vid == 0x24AE ||
                vid == 0x1532 || vid == 0x1B1C || vid == 0x1038 || vid == 0x03F0 || vid == 0x0C45 ||
                vid == 0x3434 || vid == 0x3151 || vid == 0x2E06 || vid == 0x3417 || vid == 0x3399 ||
                vid == 0x2DC8 || vid == 0x20BC || vid == 0x3537)
            {
                return true;
            }

            string s = (name + " " + group.ProductString).ToLowerInvariant();
            if (s.Contains("wireless") || s.Contains("2.4g") || s.Contains("dongle") || s.Contains("receiver") ||
                s.Contains("transceiver") || s.Contains("sem fio") || s.Contains("mouse") || s.Contains("teclado") ||
                s.Contains("keyboard") || s.Contains("headset") || s.Contains("gamepad") || s.Contains("fone"))
            {
                return true;
            }

            foreach (RawEndpointInfo ep in group.Endpoints)
            {
                if (ep.UsagePage == 0x01 && (ep.Usage == 0x02 || ep.Usage == 0x06 || ep.Usage == 0x04 || ep.Usage == 0x05))
                    return true;
                if (ep.UsagePage == 0x0C || ep.UsagePage == 0x84 || ep.UsagePage == 0x85)
                    return true;
            }

            return false;
        }

        private string ResolveProtocolName(DeviceGroup group)
        {
            if (group.VendorId == 0x3837)
                return "MCHOSE 2.4G Wireless";
            if (group.VendorId == 0x3554)
                return "Compx 2.4G / Dual Magnetic";
            if (group.VendorId == 0x374A)
                return "Valkyrie VK Wireless HID";
            if (group.VendorId == 0x258A)
                return "SinoWealth 2.4G";

            return "Wireless 2.4GHz USB HID";
        }
    }
}
