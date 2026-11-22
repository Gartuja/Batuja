using System;
using System.Collections.Generic;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner para controles sem fio baseados na API XInput.
    /// Suporta: Anbernic (quando conectado no modo PC/XInput), controles Xbox (Series, One, 360 Wireless),
    /// 8BitDo (Ultimate, Pro 2, SN30), Flydigi, GameSir, EasySMX e clones compatíveis.
    /// </summary>
    public class XInputScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "XInput (Xbox / Anbernic / 8BitDo)"; }
        }

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();

            for (uint slot = 0; slot < 4; slot++)
            {
                Win32Api.XINPUT_BATTERY_INFORMATION info;
                if (Win32Api.TryGetXInputBattery(slot, out info))
                {
                    // 0 = BATTERY_TYPE_DISCONNECTED
                    if (info.BatteryType != 0)
                    {
                        bool isWired = (info.BatteryType == 1);
                        int percent = 0;

                        switch (info.BatteryLevel)
                        {
                            case 0: // EMPTY
                                percent = 10;
                                break;
                            case 1: // LOW
                                percent = 25;
                                break;
                            case 2: // MEDIUM
                                percent = 65;
                                break;
                            case 3: // FULL
                                percent = 100;
                                break;
                            default:
                                percent = 50;
                                break;
                        }

                        if (isWired)
                        {
                            percent = 100;
                        }

                        string typeDesc = GetBatteryTypeName(info.BatteryType);
                        string slotName = string.Format("Controle Sem Fio #{0} (Xbox / Anbernic)", slot + 1);

                        devices.Add(new BatteryDevice
                        {
                            Id = string.Format("XInput_Gamepad_Slot_{0}", slot),
                            Name = slotName,
                            Category = DeviceCategory.Controle,
                            BatteryPercent = percent,
                            IsCharging = isWired,
                            IsConnected = true,
                            Protocol = "XInput Wireless Gamepad",
                            LastUpdated = DateTime.Now,
                            Details = string.Format("Slot: {0} | Tipo: {1} | Nível: {2}", slot + 1, typeDesc, GetLevelName(info.BatteryLevel))
                        });
                    }
                }
            }

            return devices;
        }

        private string GetBatteryTypeName(byte type)
        {
            switch (type)
            {
                case 1: return "Com Fio (Alimentado)";
                case 2: return "Pilhas Alcalinas";
                case 3: return "Pilhas Recarregáveis (NiMH)";
                case 0xFF: return "Bateria Interna de Lítio (Anbernic/Xbox Pack)";
                default: return "Padrão";
            }
        }

        private string GetLevelName(byte level)
        {
            switch (level)
            {
                case 0: return "Crítico (< 15%)";
                case 1: return "Baixo (~25%)";
                case 2: return "Médio (~65%)";
                case 3: return "Cheio (~100%)";
                default: return "Normal";
            }
        }
    }
}
