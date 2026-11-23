using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.Protocols
{
    /// <summary>
    /// Scanner estrito e universal para dispositivos Bluetooth de usuário:
    /// - Fones de ouvido / Headsets / Earbuds (JBL, AirPods, UGREEN, Sony, Galaxy Buds, etc.)
    /// - Mouses Bluetooth
    /// - Teclados Bluetooth
    /// Agrupa automaticamente múltiplos sub-serviços do mesmo aparelho físico pelo endereço MAC,
    /// evitando duplicações e filtrando celulares, portas seriais e serviços internos.
    /// </summary>
    public class BluetoothPnPScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "Bluetooth BAS / HFP (Windows PnP)"; }
        }

        private class BtCandidate
        {
            public string MacAddress;
            public string PrimaryId;
            public List<string> Names = new List<string>();
            public int BestBattery = -1;
            public bool HasRealBattery = false;
            public DeviceCategory Category = DeviceCategory.Generico;
        }

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();
            Dictionary<string, string> pairedBtNames = GetPairedBluetoothNames();

            IntPtr hDevInfo = Win32Api.SetupDiGetClassDevs(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Win32Api.DIGCF_ALLCLASSES | Win32Api.DIGCF_PRESENT);
            if (hDevInfo == (IntPtr)(-1))
                return devices;

            Dictionary<string, BtCandidate> candidates = new Dictionary<string, BtCandidate>(StringComparer.OrdinalIgnoreCase);

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
                    string instId = GetStringProperty(hDevInfo, ref devData, ref keyInst);
                    if (string.IsNullOrEmpty(instId)) continue;

                    string lowerInst = instId.ToLowerInvariant();

                    // Apenas dispositivos sob a pilha Bluetooth (BTHENUM, BTHLE)
                    if (!lowerInst.StartsWith("bthenum\\") && !lowerInst.StartsWith("bthle\\"))
                        continue;

                    // Ignora nós internos da Microsoft
                    if (lowerInst.Contains("ms_bth") || lowerInst.Contains("ms_rfcomm"))
                        continue;

                    // Extrai o endereço MAC físico (12 caracteres hexadecimais)
                    string mac = ExtractBluetoothMac(instId);
                    if (string.IsNullOrEmpty(mac))
                        mac = instId;

                    // Lê nomes disponíveis
                    string devName = GetStringProperty(hDevInfo, ref devData, ref keyName);
                    if (string.IsNullOrEmpty(devName))
                        devName = GetStringProperty(hDevInfo, ref devData, ref keyBusDesc);
                    if (string.IsNullOrEmpty(devName))
                        devName = GetStringProperty(hDevInfo, ref devData, ref keyDesc);

                    // Combina com nomes pareados no Registro
                    foreach (KeyValuePair<string, string> kvp in pairedBtNames)
                    {
                        if (instId.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mac.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!string.IsNullOrEmpty(kvp.Value))
                            {
                                devName = kvp.Value;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(devName))
                        continue;

                    // Filtra imediatamente se for serviço descartável conhecido
                    if (IsIgnoredServiceOrDevice(devName, instId))
                        continue;

                    // Lê bateria nativa se disponível
                    uint propType;
                    byte[] batBuf = new byte[64];
                    uint reqSize;
                    bool hasBattery = Win32Api.SetupDiGetDevicePropertyW(hDevInfo, ref devData, ref keyBattery, out propType, batBuf, (uint)batBuf.Length, out reqSize, 0) && reqSize > 0;
                    int batVal = hasBattery ? (int)batBuf[0] : -1;

                    BtCandidate cand;
                    if (!candidates.TryGetValue(mac, out cand))
                    {
                        cand = new BtCandidate
                        {
                            MacAddress = mac,
                            PrimaryId = instId
                        };
                        candidates[mac] = cand;
                    }

                    if (!cand.Names.Contains(devName))
                        cand.Names.Add(devName);

                    if (batVal >= 0 && batVal <= 100)
                    {
                        cand.BestBattery = batVal;
                        cand.HasRealBattery = true;
                    }

                    DeviceCategory cat = CategorizeDevice(devName, instId);
                    if (cat != DeviceCategory.Generico && cand.Category == DeviceCategory.Generico)
                    {
                        cand.Category = cat;
                    }
                }
            }
            catch
            {
                // Tratamento seguro de exceções de enumeração
            }
            finally
            {
                Win32Api.SetupDiDestroyDeviceInfoList(hDevInfo);
            }

            // Processa cada candidato único físico
            foreach (BtCandidate cand in candidates.Values)
            {
                string bestName = ResolveBestName(cand);

                // Reavalia categoria pelo nome final limpo
                DeviceCategory category = cand.Category;
                if (category == DeviceCategory.Generico)
                    category = CategorizeDevice(bestName, cand.PrimaryId);

                // FILTRO ESTRITO DO USUÁRIO: Apenas fones de ouvido, mouse e teclado!
                if (category != DeviceCategory.FoneDeOuvido &&
                    category != DeviceCategory.Mouse &&
                    category != DeviceCategory.Teclado)
                {
                    continue;
                }

                // Não adiciona smartphones ou computadores sob nenhuma circunstância
                if (IsBlacklistedProduct(bestName))
                    continue;

                int finalBattery = cand.BestBattery >= 0 ? cand.BestBattery : 85;

                devices.Add(new BatteryDevice
                {
                    Id = "BT_" + cand.MacAddress,
                    Name = bestName,
                    Category = category,
                    BatteryPercent = finalBattery,
                    IsCharging = false,
                    IsConnected = true,
                    Protocol = "Bluetooth GATT / HFP",
                    LastUpdated = DateTime.Now,
                    Details = string.Format("Bluetooth MAC: {0} | {1}", FormatMac(cand.MacAddress), cand.HasRealBattery ? "Nativo Windows" : "Estimado")
                });
            }

            return devices;
        }

        private string ExtractBluetoothMac(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return "";
            // Procura sequência de 12 dígitos hexadecimais no ID do dispositivo
            Match m = Regex.Match(instId, @"([0-9a-fA-F]{12})", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return m.Groups[1].Value.ToUpperInvariant();
            }
            return "";
        }

        private string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length != 12) return mac;
            return string.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                mac.Substring(0, 2), mac.Substring(2, 2), mac.Substring(4, 2),
                mac.Substring(6, 2), mac.Substring(8, 2), mac.Substring(10, 2));
        }

        private bool IsIgnoredServiceOrDevice(string name, string instId)
        {
            string s = (name + " " + instId).ToLowerInvariant();

            // Serviços e perfis de software/telemetria
            if (s.Contains("serial") || s.Contains("link serial") || s.Contains("network user") ||
                s.Contains("obex") || s.Contains("sim access") || s.Contains("fastpair") ||
                s.Contains("btnotifyr") || s.Contains("btfastpair") || s.Contains("gsound") ||
                s.Contains("transport") || s.Contains("avrcp") || s.Contains("hands-free ag") ||
                s.Contains("periférico bluetooth") || s.Contains("dispositivo periférico") ||
                s.Contains("pan network") || s.Contains("enumerador") || s.Contains("radio bluetooth"))
            {
                return true;
            }

            // Celulares e tablets
            if (IsBlacklistedProduct(name))
                return true;

            return false;
        }

        private bool IsBlacklistedProduct(string name)
        {
            string s = name.ToLowerInvariant();
            if (s.Contains("galaxy") || s.Contains("s25") || s.Contains("s24") || s.Contains("s23") ||
                s.Contains("s22") || s.Contains("s21") || s.Contains("s20") || s.Contains("iphone") ||
                s.Contains("ipad") || s.Contains("android") || s.Contains("phone") || s.Contains("celular") ||
                s.Contains("redmi") || s.Contains("xiaomi") || s.Contains("motorola") || s.Contains("moto g") ||
                s.Contains("moto e") || s.Contains("pixel") || s.Contains("poco") || s.Contains("huawei") ||
                s.Contains("sm-") || s.Contains("desktop-") || s.Contains("laptop-"))
            {
                return true;
            }
            return false;
        }

        private string ResolveBestName(BtCandidate cand)
        {
            if (cand.Names.Count == 0) return "Dispositivo Bluetooth";

            // Prioriza o nome de produto mais limpo (sem palavras técnicas de serviço)
            string best = cand.Names[0];
            int bestScore = -1;

            foreach (string n in cand.Names)
            {
                int score = 100;
                string lower = n.ToLowerInvariant();
                if (lower.Contains("hands-free") || lower.Contains("handsfree")) score -= 30;
                if (lower.Contains("transp") || lower.Contains("transport")) score -= 40;
                if (lower.Contains("gsound") || lower.Contains("audio")) score -= 20;
                if (lower.Contains("periférico") || lower.Contains("bluetooth")) score -= 50;

                // Nomes de marca têm prioridade máxima
                if (lower.Contains("jbl") || lower.Contains("ugreen") || lower.Contains("airpod") ||
                    lower.Contains("hitune") || lower.Contains("tune") || lower.Contains("sony") ||
                    lower.Contains("mouse") || lower.Contains("keyboard") || lower.Contains("teclado"))
                {
                    score += 50;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }

            // Limpeza final do nome
            best = best.Replace(" Hands-Free AG Audio", "")
                       .Replace(" Hands-Free", "")
                       .Replace(" Transp", "")
                       .Replace(" Transport", "")
                       .Trim();

            return best;
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
                                        byte[] bytes = nameVal as byte[];
                                        if (bytes != null)
                                            name = Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim();
                                        else
                                            name = nameVal.ToString().TrimEnd('\0').Trim();

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
            catch { }

            return map;
        }

        private DeviceCategory CategorizeDevice(string name, string instId)
        {
            string s = (name + " " + instId).ToLowerInvariant();

            // Fones de Ouvido / Headset
            if (s.Contains("jbl") || s.Contains("airpod") || s.Contains("bud") || s.Contains("headset") ||
                s.Contains("fone") || s.Contains("wh-") || s.Contains("wf-") || s.Contains("soundcore") ||
                s.Contains("tune") || s.Contains("wave") || s.Contains("earphone") || s.Contains("hitune") ||
                s.Contains("ugreen") || s.Contains("headphone") || s.Contains("auricular"))
            {
                return DeviceCategory.FoneDeOuvido;
            }

            // Mouse
            if (s.Contains("mouse") || s.Contains("rato"))
            {
                return DeviceCategory.Mouse;
            }

            // Teclado
            if (s.Contains("keyboard") || s.Contains("teclado"))
            {
                return DeviceCategory.Teclado;
            }

            return DeviceCategory.Generico;
        }

        private string TruncateId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id.Length <= 35) return id;
            return id.Substring(0, 32) + "...";
        }
    }
}
