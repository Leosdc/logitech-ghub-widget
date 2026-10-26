using System;

namespace GHubCli.Models
{
    public class CliDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = "Dispositivo Logitech";
        public string DeviceType { get; set; } = "UNKNOWN";
        public string ConnectionType { get; set; } = "WIRELESS";
        public int BatteryPercentage { get; set; } = 0;
        public bool IsCharging { get; set; }
        public double Mileage { get; set; }
        public double MaxLifespan { get; set; }
        public double ConsumptionMw { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public string CategoryLabel
        {
            get
            {
                var upperType = DeviceType.ToUpperInvariant();
                var upperName = Name.ToUpperInvariant();

                if (upperType == "MOUSE" || upperName.Contains("MOUSE") || upperName.Contains("SUPERLIGHT") || upperName.Contains("G305") || upperName.Contains("G502") || upperName.Contains("G PRO"))
                    return "Mouse";

                if (upperType == "KEYBOARD" || upperName.Contains("KEYBOARD") || upperName.Contains("TECLADO") || upperName.Contains("G515") || upperName.Contains("G915") || upperName.Contains("G Pro X 60"))
                    return "Teclado";

                if (upperType == "HEADSET" || upperName.Contains("HEADSET") || upperName.Contains("FONE") || upperName.Contains("G325") || upperName.Contains("G733") || upperName.Contains("G PRO X WIRELESS"))
                    return "Headset";

                return "Periférico";
            }
        }

        public string CategoryIcon
        {
            get => CategoryLabel switch
            {
                "Mouse" => "\U0001F5B1",
                "Teclado" => "\u2328",
                "Headset" => "\U0001F3A7",
                _ => "\U0001F3AE"
            };
        }

        public string StatusText
        {
            get
            {
                if (!IsActive) return "Inativo";
                if (IsCharging) return "Carregando";
                return "Ativo";
            }
        }

        public string FormattedPercentage
        {
            get
            {
                if (!IsActive && BatteryPercentage == 0) return "--%";
                return $"{BatteryPercentage}%";
            }
        }

        public string FormattedMileage
        {
            get
            {
                if (!IsActive) return "Desconectado";
                if (IsCharging) return "Carregando...";
                if (Mileage > 0)
                {
                    int hours = (int)Math.Round(Mileage);
                    return hours > 0 ? $"~{hours}h restantes" : "< 1h restante";
                }
                return "Calculando...";
            }
        }

        public string FormattedConsumption
        {
            get
            {
                if (!IsActive) return "--";
                return ConsumptionMw > 0 ? $"{ConsumptionMw:F1} mW" : "Normal";
            }
        }
    }
}
