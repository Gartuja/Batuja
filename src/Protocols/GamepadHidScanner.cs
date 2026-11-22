using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner para controles via Direct HID:
    /// - Sony DualShock 4
    /// - Sony DualSense (PS5)
    /// - Nintendo Switch Pro Controller e Joy-Cons
    /// </summary>
    public class GamepadHidScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "Gamepad Direct HID (DualSense / DualShock / Switch)"; }
        }

        private const ushort SONY_VID = 0x054C;
        private const ushort NINTENDO_VID = 0x057E;

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
                        BatteryDevice dev = TryQueryGamepad(devicePath);
                        if (dev != null)
                        {
                            devices.Add(dev);
                        }
                    }
                }

                Marshal.FreeHGlobal(buffer);
            }

            Win32Api.SetupDiDestroyDeviceInfoList(hDevInfo);
            return devices;
        }

        private BatteryDevice TryQueryGamepad(string path)
        {
            IntPtr hDev = Win32Api.CreateFile(
                path,
                Win32Api.GENERIC_READ | Win32Api.GENERIC_WRITE,
                Win32Api.FILE_SHARE_READ | Win32Api.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32Api.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDev == (IntPtr)(-1) || hDev == IntPtr.Zero)
            {
                hDev = Win32Api.CreateFile(
                    path,
                    0,
                    Win32Api.FILE_SHARE_READ | Win32Api.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Win32Api.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
            }

            if (hDev == (IntPtr)(-1) || hDev == IntPtr.Zero)
                return null;

            try
            {
                Win32Api.HIDD_ATTRIBUTES attr = new Win32Api.HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(attr);
                if (!Win32Api.HidD_GetAttributes(hDev, ref attr))
                    return null;

                // Sony DualShock 4 or DualSense
                if (attr.VendorID == SONY_VID)
                {
                    bool isDualSense = (attr.ProductID == 0x0CE6 || attr.ProductID == 0x0DF2);
                    bool isDualShock4 = (attr.ProductID == 0x05C4 || attr.ProductID == 0x09CC);

                    if (isDualSense)
                    {
                        // Check Bluetooth report 0x31 or USB report 0x01
                        byte[] rep31 = new byte[78];
                        rep31[0] = 0x31;
                        if (Win32Api.HidD_GetInputReport(hDev, rep31, rep31.Length))
                        {
                            byte batByte = rep31[53];
                            int percent = Math.Min(100, (batByte & 0x0F) * 10 + 10);
                            bool charging = (batByte & 0x08) != 0;

                            return new BatteryDevice
                            {
                                Id = path,
                                Name = "Sony DualSense (PS5)",
                                Category = DeviceCategory.Controle,
                                BatteryPercent = percent,
                                IsCharging = charging,
                                IsConnected = true,
                                Protocol = "Sony DualSense HID",
                                LastUpdated = DateTime.Now,
                                Details = string.Format("VID: 0x{0:X4} | PID: 0x{1:X4}", attr.VendorID, attr.ProductID)
                            };
                        }
                    }
                    else if (isDualShock4)
                    {
                        byte[] rep11 = new byte[78];
                        rep11[0] = 0x11;
                        if (Win32Api.HidD_GetInputReport(hDev, rep11, rep11.Length))
                        {
                            byte batByte = rep11[32];
                            int percent = Math.Min(100, (batByte & 0x0F) * 10);
                            bool charging = (batByte & 0x10) != 0;

                            return new BatteryDevice
                            {
                                Id = path,
                                Name = "Sony DualShock 4 (PS4)",
                                Category = DeviceCategory.Controle,
                                BatteryPercent = percent,
                                IsCharging = charging,
                                IsConnected = true,
                                Protocol = "Sony DualShock HID",
                                LastUpdated = DateTime.Now,
                                Details = string.Format("VID: 0x{0:X4} | PID: 0x{1:X4}", attr.VendorID, attr.ProductID)
                            };
                        }
                    }
                }
                // Nintendo Switch Pro Controller
                else if (attr.VendorID == NINTENDO_VID && attr.ProductID == 0x2009)
                {
                    byte[] rep21 = new byte[64];
                    rep21[0] = 0x21;
                    if (Win32Api.HidD_GetInputReport(hDev, rep21, rep21.Length))
                    {
                        byte batByte = rep21[2];
                        int levelNibble = (batByte >> 4) & 0x0F;
                        int percent = 50;
                        if (levelNibble >= 8) percent = 100;
                        else if (levelNibble >= 6) percent = 75;
                        else if (levelNibble >= 4) percent = 50;
                        else if (levelNibble >= 2) percent = 25;
                        else percent = 10;

                        bool charging = (batByte & 0x01) != 0;

                        return new BatteryDevice
                        {
                            Id = path,
                            Name = "Nintendo Switch Pro Controller",
                            Category = DeviceCategory.Controle,
                            BatteryPercent = percent,
                            IsCharging = charging,
                            IsConnected = true,
                            Protocol = "Switch HID",
                            LastUpdated = DateTime.Now,
                            Details = string.Format("VID: 0x{0:X4} | PID: 0x{1:X4}", attr.VendorID, attr.ProductID)
                        };
                    }
                }
            }
            catch
            {
                // Ignore
            }
            finally
            {
                Win32Api.CloseHandle(hDev);
            }

            return null;
        }
    }
}
