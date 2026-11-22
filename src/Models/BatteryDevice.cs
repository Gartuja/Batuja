using System;
using System.Drawing;

namespace OmniBattery.Models
{
    public enum DeviceCategory
    {
        Mouse,
        Teclado,
        Controle,
        FoneDeOuvido,
        Laptop,
        Generico
    }

    public enum DeviceConnectionStatus
    {
        Conectado,
        Desconectado,
        Carregando,
        Dormindo
    }

    public class BatteryDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DeviceCategory Category { get; set; }
        public DeviceCategory? CustomCategory { get; set; }
        public string CustomIconPath { get; set; }
        public bool IsHidden { get; set; }
        public bool IsTrayIconEnabled { get; set; }

        public int BatteryPercent { get; set; } // 0 to 100, or -1 if unknown
        public bool IsCharging { get; set; }
        public bool IsConnected { get; set; }
        public string Protocol { get; set; }
        public double Voltage { get; set; } // Volts, 0 if not available
        public DateTime LastUpdated { get; set; }
        public string Details { get; set; }
        public string Subtitle { get; set; }
        public Color? CustomColor { get; set; }

        public DeviceCategory EffectiveCategory
        {
            get
            {
                if (CustomCategory.HasValue)
                    return CustomCategory.Value;
                return Category;
            }
        }

        public string DeviceKey
        {
            get
            {
                if (!string.IsNullOrEmpty(Name))
                {
                    return Name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
                }
                return !string.IsNullOrEmpty(Id) ? Id : "UnknownDevice";
            }
        }

        public BatteryDevice()
        {
            BatteryPercent = -1;
            IsConnected = true;
            LastUpdated = DateTime.Now;
            Category = DeviceCategory.Generico;
            Protocol = "Desconhecido";
            Details = "";
            Subtitle = "Sem fio";
            CustomColor = null;
            CustomIconPath = null;
            IsHidden = false;
            IsTrayIconEnabled = true;
        }

        public Color GetStatusColor()
        {
            if (!IsConnected)
                return Color.FromArgb(120, 120, 140); // Cinza

            if (IsCharging)
                return Color.FromArgb(0, 210, 211); // Ciano / Azul Elétrico

            if (BatteryPercent < 0)
                return Color.FromArgb(88, 101, 242); // Roxo / Azul padrão

            if (BatteryPercent <= 20)
                return Color.FromArgb(231, 76, 60); // Vermelho

            if (BatteryPercent <= 50)
                return Color.FromArgb(243, 156, 18); // Laranja / Âmbar

            return Color.FromArgb(46, 204, 113); // Verde
        }

        public Color GetEffectiveColor()
        {
            if (!IsConnected)
                return Color.FromArgb(120, 120, 140);

            if (CustomColor.HasValue)
                return CustomColor.Value;

            return GetStatusColor();
        }

        public string GetFormattedPercentage()
        {
            if (!IsConnected)
                return "Desligado";

            if (BatteryPercent < 0)
                return "Ativo";

            return BatteryPercent + "%";
        }
    }
}
