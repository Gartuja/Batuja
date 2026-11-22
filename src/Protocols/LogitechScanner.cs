using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner para dispositivos Logitech (Lightspeed, Unifying, Bolt e Bluetooth).
    /// Suporta protocolo HID++ 1.0 e HID++ 2.0 / 4.5.
    /// Suporta mouses (G Pro, MX Master, G502, G305, etc.), teclados (G915, MX Keys, etc.) e headsets.
    /// </summary>
    public class LogitechScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "Logitech HID++ (Lightspeed & Unifying)"; }
        }

        private const ushort LOGITECH_VID = 0x046D;

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();
            HashSet<string> scannedPaths = new HashSet<string>();

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
                    if (!string.IsNullOrEmpty(devicePath) && !scannedPaths.Contains(devicePath))
                    {
                        scannedPaths.Add(devicePath);
                        List<BatteryDevice> found = ScanLogitechDevice(devicePath);
                        if (found != null && found.Count > 0)
                        {
                            devices.AddRange(found);
                        }
                    }
                }

                Marshal.FreeHGlobal(buffer);
            }

            Win32Api.SetupDiDestroyDeviceInfoList(hDevInfo);
            return devices;
        }

        private List<BatteryDevice> ScanLogitechDevice(string path)
        {
            List<BatteryDevice> result = new List<BatteryDevice>();

            IntPtr hDev = Win32Api.CreateFile(
                path,
                Win32Api.GENERIC_READ | Win32Api.GENERIC_WRITE,
                Win32Api.FILE_SHARE_READ | Win32Api.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Api.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDev == (IntPtr)(-1) || hDev == IntPtr.Zero)
                return result;

            try
            {
                Win32Api.HIDD_ATTRIBUTES attr = new Win32Api.HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(attr);
                if (!Win32Api.HidD_GetAttributes(hDev, ref attr) || attr.VendorID != LOGITECH_VID)
                    return result;

                IntPtr ppData;
                if (!Win32Api.HidD_GetPreparsedData(hDev, out ppData))
                    return result;

                Win32Api.HIDP_CAPS caps = new Win32Api.HIDP_CAPS();
                int capsRes = Win32Api.HidP_GetCaps(ppData, ref caps);
                Win32Api.HidD_FreePreparsedData(ppData);

                if (capsRes != 0x00110000)
                    return result;

                // Logitech HID++ communicates on collections with UsagePage 0xFF00, 0xFF43 or report length >= 7
                if (caps.OutputReportByteLength < 7 && caps.FeatureReportByteLength < 7)
                    return result;

                string receiverName = GetDeviceString(hDev);
                if (string.IsNullOrEmpty(receiverName))
                    receiverName = "Logitech Wireless Receiver";

                // Scan paired device slots: 0xFF (receiver/direct), 0x01 through 0x06 (wireless devices)
                byte[] targetIndices = new byte[] { 0xFF, 0x01, 0x02, 0x03, 0x04 };

                foreach (byte devIdx in targetIndices)
                {
                    BatteryDevice dev = QueryHidPlusPlus(hDev, path, devIdx, receiverName);
                    if (dev != null)
                    {
                        result.Add(dev);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            finally
            {
                Win32Api.CloseHandle(hDev);
            }

            return result;
        }

        private BatteryDevice QueryHidPlusPlus(IntPtr hDev, string basePath, byte devIndex, string receiverName)
        {
            try
            {
                // Step 1: Query Feature 0x1000 (BATTERY_LEVEL_STATUS)
                byte feature1000Index = GetFeatureIndex(hDev, devIndex, 0x1000);
                if (feature1000Index > 0)
                {
                    // Call function 0: getBatteryLevelStatus
                    byte[] req = new byte[20];
                    req[0] = 0x11; // Long report
                    req[1] = devIndex;
                    req[2] = feature1000Index;
                    req[3] = 0x00; // Function 0

                    byte[] resp = SendAndReceiveLong(hDev, req);
                    if (resp != null && resp[0] == 0x11 && resp[1] == devIndex && resp[2] == feature1000Index)
                    {
                        int battery = resp[4];
                        int status = resp[6]; // 0: discharging, 1: recharging, 2: almost full, 3: full
                        bool isCharging = (status == 1 || status == 2 || status == 3);

                        string devName = GetDeviceNameHidPP(hDev, devIndex);
                        if (string.IsNullOrEmpty(devName))
                            devName = (devIndex == 0xFF) ? receiverName : string.Format("Logitech Device #{0}", devIndex);

                        return new BatteryDevice
                        {
                            Id = string.Format("{0}#Slot{1}", basePath, devIndex),
                            Name = devName,
                            Category = GuessCategory(devName),
                            BatteryPercent = battery,
                            IsCharging = isCharging,
                            IsConnected = true,
                            Protocol = "Logitech HID++ 2.0",
                            LastUpdated = DateTime.Now,
                            Details = string.Format("Feature: 0x1000 | Slot: {0} | Status: {1}", devIndex, GetStatusDescription(status))
                        };
                    }
                }

                // Step 2: Query Feature 0x1004 (UNIFIED_BATTERY)
                byte feature1004Index = GetFeatureIndex(hDev, devIndex, 0x1004);
                if (feature1004Index > 0)
                {
                    byte[] req = new byte[20];
                    req[0] = 0x11;
                    req[1] = devIndex;
                    req[2] = feature1004Index;
                    req[3] = 0x10; // Function 1: getBatteryStatus

                    byte[] resp = SendAndReceiveLong(hDev, req);
                    if (resp != null && resp[0] == 0x11 && resp[1] == devIndex && resp[2] == feature1004Index)
                    {
                        int battery = resp[4];
                        int flags = resp[5];
                        bool isCharging = (flags & 0x01) != 0;

                        string devName = GetDeviceNameHidPP(hDev, devIndex);
                        if (string.IsNullOrEmpty(devName))
                            devName = (devIndex == 0xFF) ? receiverName : string.Format("Logitech Device #{0}", devIndex);

                        return new BatteryDevice
                        {
                            Id = string.Format("{0}#Slot{1}", basePath, devIndex),
                            Name = devName,
                            Category = GuessCategory(devName),
                            BatteryPercent = battery,
                            IsCharging = isCharging,
                            IsConnected = true,
                            Protocol = "Logitech HID++ 2.0 (Unified)",
                            LastUpdated = DateTime.Now,
                            Details = string.Format("Feature: 0x1004 | Slot: {0}", devIndex)
                        };
                    }
                }

                // Step 3: HID++ 1.0 Fallback (Register 0x0D - Battery Status)
                byte[] shortReq = new byte[7];
                shortReq[0] = 0x10; // Short report
                shortReq[1] = devIndex;
                shortReq[2] = 0x81; // SubID: read register
                shortReq[3] = 0x0D; // Register 0x0D: Battery

                byte[] shortResp = SendAndReceiveShort(hDev, shortReq);
                if (shortResp != null && shortResp[0] == 0x10 && shortResp[1] == devIndex && shortResp[2] == 0x81)
                {
                    int batVal = shortResp[3];
                    // 0x01: <5%, 0x03: ~20%, 0x05: ~50%, 0x07: ~90%
                    int estimatedPercent = 50;
                    if (batVal <= 0x01) estimatedPercent = 5;
                    else if (batVal <= 0x03) estimatedPercent = 20;
                    else if (batVal <= 0x05) estimatedPercent = 55;
                    else estimatedPercent = 90;

                    return new BatteryDevice
                    {
                        Id = string.Format("{0}#Slot{1}", basePath, devIndex),
                        Name = (devIndex == 0xFF) ? receiverName : string.Format("Logitech Device #{0}", devIndex),
                        Category = DeviceCategory.Mouse,
                        BatteryPercent = estimatedPercent,
                        IsCharging = (shortResp[4] == 1),
                        IsConnected = true,
                        Protocol = "Logitech HID++ 1.0",
                        LastUpdated = DateTime.Now,
                        Details = string.Format("Reg 0x0D: 0x{0:X2} | Slot: {1}", batVal, devIndex)
                    };
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        private byte GetFeatureIndex(IntPtr hDev, byte devIndex, ushort featureId)
        {
            byte[] req = new byte[20];
            req[0] = 0x11; // Long report
            req[1] = devIndex;
            req[2] = 0x00; // Root feature
            req[3] = 0x00; // Function 0: getFeature
            req[4] = (byte)(featureId >> 8);
            req[5] = (byte)(featureId & 0xFF);

            byte[] resp = SendAndReceiveLong(hDev, req);
            if (resp != null && resp[0] == 0x11 && resp[1] == devIndex && resp[2] == 0x00 && resp[3] == 0x00)
            {
                return resp[4]; // featureIndex
            }

            return 0;
        }

        private string GetDeviceNameHidPP(IntPtr hDev, byte devIndex)
        {
            try
            {
                byte nameFeatureIdx = GetFeatureIndex(hDev, devIndex, 0x0005); // FEATURE_DEVICE_NAME_TYPE
                if (nameFeatureIdx == 0)
                    nameFeatureIdx = GetFeatureIndex(hDev, devIndex, 0x0001);

                if (nameFeatureIdx > 0)
                {
                    // Function 1: getDeviceName
                    byte[] req = new byte[20];
                    req[0] = 0x11;
                    req[1] = devIndex;
                    req[2] = nameFeatureIdx;
                    req[3] = 0x10; // Function 1
                    req[4] = 0x00;

                    byte[] resp = SendAndReceiveLong(hDev, req);
                    if (resp != null && resp[0] == 0x11 && resp[1] == devIndex && resp[2] == nameFeatureIdx)
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int k = 5; k < resp.Length && resp[k] != 0; k++)
                        {
                            sb.Append((char)resp[k]);
                        }
                        string n = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(n)) return n;
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        private byte[] SendAndReceiveLong(IntPtr hDev, byte[] req)
        {
            byte[] resp = new byte[20];
            bool sent = Win32Api.HidD_SetOutputReport(hDev, req, req.Length);
            if (!sent)
            {
                sent = Win32Api.HidD_SetFeature(hDev, req, req.Length);
            }

            if (sent)
            {
                resp[0] = 0x11;
                bool recvd = Win32Api.HidD_GetInputReport(hDev, resp, resp.Length);
                if (!recvd)
                {
                    recvd = Win32Api.HidD_GetFeature(hDev, resp, resp.Length);
                }

                if (recvd) return resp;
            }

            return null;
        }

        private byte[] SendAndReceiveShort(IntPtr hDev, byte[] req)
        {
            byte[] resp = new byte[7];
            bool sent = Win32Api.HidD_SetOutputReport(hDev, req, req.Length);
            if (!sent)
            {
                sent = Win32Api.HidD_SetFeature(hDev, req, req.Length);
            }

            if (sent)
            {
                resp[0] = 0x10;
                bool recvd = Win32Api.HidD_GetInputReport(hDev, resp, resp.Length);
                if (!recvd)
                {
                    recvd = Win32Api.HidD_GetFeature(hDev, resp, resp.Length);
                }

                if (recvd) return resp;
            }

            return null;
        }

        private string GetDeviceString(IntPtr hDev)
        {
            byte[] buf = new byte[256];
            if (Win32Api.HidD_GetProductString(hDev, buf, buf.Length))
            {
                string s = Encoding.Unicode.GetString(buf).TrimEnd('\0').Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        private string GetStatusDescription(int status)
        {
            switch (status)
            {
                case 0: return "Descarregando";
                case 1: return "Recarregando";
                case 2: return "Quase cheia";
                case 3: return "Totalmente carregada";
                case 4: return "Carga lenta";
                default: return "Normal";
            }
        }

        private DeviceCategory GuessCategory(string name)
        {
            if (string.IsNullOrEmpty(name)) return DeviceCategory.Mouse;
            string l = name.ToLowerInvariant();
            if (l.Contains("keyboard") || l.Contains("teclado") || l.Contains("g915") || l.Contains("mx keys") || l.Contains("craft") || l.Contains("k380") || l.Contains("k780") || l.Contains("pop keys"))
                return DeviceCategory.Teclado;
            if (l.Contains("headset") || l.Contains("fone") || l.Contains("g733") || l.Contains("g935") || l.Contains("g535") || l.Contains("pro x wireless"))
                return DeviceCategory.FoneDeOuvido;
            return DeviceCategory.Mouse;
        }
    }
}
