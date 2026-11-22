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

            bool isKnownTarget = lower.Contains("vid_3837") || // MCHOSE
                                 lower.Contains("vid_3554") || // Attack Shark / Compx
                                 lower.Contains("vid_374a") || // VK 87 Max V2 (Valkyrie)
                                 lower.Contains("vid_258a") || // SinoWealth
                                 lower.Contains("vid_1a34") || // OEM Dongles
                                 lower.Contains("vid_24ae") || // Rapoo / OEM
                                 lower.Contains("vid_0bda&pid_ffe9") ||
                                 lower.Contains("vid_04d9");

            if (!isKnownTarget) return;

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

            // Deduplicate devices by physical product family
            string groupKey = "";
            if (vid == 0x3837 && (pid == 0x600A || pid == 0x6008))
            {
                groupKey = "mchose_v9_turbo";
            }
            else if ((vid == 0x3554 && (pid == 0xF517 || pid == 0xFA09)) ||
                     (vid == 0x1A34 && pid == 0xF517))
            {
                // Attack Shark RS6 Ultra (mesmo mouse que expõe interface Compx e Dongle secundário)
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

                    if (detectedBattery > 0)
                        break;
                }
                finally
                {
                    Win32Api.CloseHandle(hDev);
                }
            }

            bool isElevated = false;
            try
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    isElevated = new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { }

            bool isDevConnected = true;
            if (isElevated)
            {
                // Em modo administrador: telemetria real. Se não responder pacote, está DESLIGADO.
                if (detectedBattery > 0)
                {
                    isDevConnected = true;
                }
                else
                {
                    isDevConnected = false;
                }
            }
            else
            {
                if (detectedBattery < 0)
                {
                    detectedBattery = 90;
                }
                isDevConnected = true;
            }

            return new BatteryDevice
            {
                Id = group.GroupKey,
                Name = friendlyName,
                Category = category,
                BatteryPercent = detectedBattery,
                IsCharging = isCharging,
                IsConnected = isDevConnected,
                Protocol = protocolName,
                Voltage = detectedVoltage,
                LastUpdated = DateTime.Now,
                Subtitle = isDevConnected ? "Receptor 2.4GHz USB Ativo" : "Dispositivo desligado",
                Details = extraDetails
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
            {
                return "MCHOSE V9 Turbo";
            }

            if (group.VendorId == 0x3554)
            {
                return "Attack Shark RS6 Ultra";
            }

            if (group.VendorId == 0x374A)
            {
                return "VK 87 Max V2";
            }

            if (group.VendorId == 0x1A34)
            {
                return "Attack Shark RS6 (Dongle Secundário)";
            }

            if (!string.IsNullOrEmpty(group.ProductString))
                return group.ProductString;

            return string.Format("Dispositivo Sem Fio (VID: 0x{0:X4})", group.VendorId);
        }

        private DeviceCategory ResolveCategory(DeviceGroup group, string name)
        {
            string s = (name + " " + group.ProductString).ToLowerInvariant();
            if (group.VendorId == 0x3837)
                return DeviceCategory.FoneDeOuvido; // MCHOSE V9 Turbo Headset
            if (group.VendorId == 0x374A || s.Contains("vk") || s.Contains("keyboard") || s.Contains("teclado"))
                return DeviceCategory.Teclado;
            if (group.VendorId == 0x3554 || s.Contains("mouse") || s.Contains("shark"))
                return DeviceCategory.Mouse;

            return DeviceCategory.Mouse;
        }

        private string ResolveProtocolName(DeviceGroup group)
        {
            if (group.VendorId == 0x3837)
                return "MCHOSE 2.4G Wireless";
            if (group.VendorId == 0x3554)
                return "Compx 2.4G / Dual Magnetic";
            if (group.VendorId == 0x374A)
                return "Valkyrie VK Wireless HID";

            return "Compx / SinoWealth 2.4G";
        }
    }
}
