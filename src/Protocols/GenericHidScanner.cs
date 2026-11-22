using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner para bateria do sistema (Laptop / UPS) e dispositivos HID Power Device genéricos (Usage Page 0x84/0x85).
    /// </summary>
    public class GenericHidScanner : IProtocolScanner
    {
        private readonly bool _includeSystemBattery;

        public GenericHidScanner(bool includeSystemBattery)
        {
            _includeSystemBattery = includeSystemBattery;
        }

        public string Name
        {
            get { return "Sistema / HID Power Genérico"; }
        }

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();

            if (_includeSystemBattery)
            {
                Win32Api.SYSTEM_POWER_STATUS status;
                if (Win32Api.GetSystemPowerStatus(out status))
                {
                    // BatteryFlag 128 = No system battery (Desktop)
                    if (status.BatteryFlag != 128 && status.BatteryLifePercent <= 100)
                    {
                        bool isCharging = (status.ACLineStatus == 1);
                        devices.Add(new BatteryDevice
                        {
                            Id = "System_Laptop_Battery",
                            Name = "Bateria do Notebook / Sistema",
                            Category = DeviceCategory.Laptop,
                            BatteryPercent = status.BatteryLifePercent,
                            IsCharging = isCharging,
                            IsConnected = true,
                            Protocol = "Windows Power Subsystem",
                            LastUpdated = DateTime.Now,
                            Details = string.Format("Status AC: {0} | Flags: 0x{1:X2}",
                                status.ACLineStatus == 1 ? "Conectado à Tomada" : "Na Bateria", status.BatteryFlag)
                        });
                    }
                }
            }

            return devices;
        }
    }
}
