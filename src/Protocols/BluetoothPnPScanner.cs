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
    /// 
    /// Agrupa perfeitamente todos os sub-serviços do mesmo aparelho físico pelo endereço MAC real.
    /// Detecta com precisão se o dispositivo está realmente LIGADO/CONECTADO agora ou DESLIGADO.
    /// Dispositivos desligados são ignorados para não poluir a tela, a bandeja ou o overlay.
    /// Não utiliza valores de bateria falsos/estimados (como 85%).
    /// </summary>
    public class BluetoothPnPScanner : IProtocolScanner
    {
        public string Name
        {
            get { return "Bluetooth BAS / HFP (Windows PnP)"; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WAVEOUTCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
            public uint dwSupport;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WAVEINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint waveOutGetNumDevs();

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint waveOutGetDevCaps(IntPtr uDeviceID, ref WAVEOUTCAPS pwoc, uint cbwoc);

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint waveInGetNumDevs();

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint waveInGetDevCaps(IntPtr uDeviceID, ref WAVEINCAPS pwic, uint cbwic);

        private class BtCandidate
        {
            public string MacAddress;
            public string PrimaryId;
            public List<string> Names = new List<string>();
            public int BestBattery = -1;
            public bool HasRealBattery = false;
            public bool IsConnected = false;
            public DeviceCategory Category = DeviceCategory.Generico;
        }

        public List<BatteryDevice> Scan()
        {
            List<BatteryDevice> devices = new List<BatteryDevice>();
            Dictionary<string, string> pairedBtNames = GetPairedBluetoothNames();
            List<string> activeAudioDevices = GetActiveAudioDeviceNames();

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
                Win32Api.DEVPROPKEY keyIsConnected = new Win32Api.DEVPROPKEY { fmtid = new Guid("83da6326-97a6-4088-9453-a1923f573b29"), pid = 15 };
                Win32Api.DEVPROPKEY keyDevNodeStatus = new Win32Api.DEVPROPKEY { fmtid = new Guid("43424749-69da-4784-8243-d81149a33405"), pid = 38 };

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

                    // Extrai o endereço MAC físico Bluetooth real (48-bit)
                    string mac = ExtractBluetoothMac(instId);
                    if (string.IsNullOrEmpty(mac))
                        continue; // Sem MAC válido, descarta nó irrelevante

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

                    // Verifica se o devnode está fisicamente conectado
                    // DEVPKEY_Device_IsConnected ({83da6326-97a6-4088-9453-a1923f573b29}, 15)
                    uint propType;
                    byte[] connBuf = new byte[8];
                    uint reqSize;
                    bool nodeHasExplicitConnProp = false;
                    bool nodeIsConnected = false;

                    if (Win32Api.SetupDiGetDevicePropertyW(hDevInfo, ref devData, ref keyIsConnected, out propType, connBuf, (uint)connBuf.Length, out reqSize, 0) && reqSize > 0)
                    {
                        nodeHasExplicitConnProp = true;
                        if (connBuf[0] != 0)
                            nodeIsConnected = true;
                    }

                    // Lê bateria nativa se disponível (ATENÇÃO: Windows armazena este valor em cache de conexões anteriores!)
                    byte[] batBuf = new byte[64];
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

                    // Marca como conectado SOMENTE se o DEVPKEY_Device_IsConnected explícito for verdadeiro
                    if (nodeHasExplicitConnProp && nodeIsConnected)
                    {
                        cand.IsConnected = true;
                    }

                    if (!cand.Names.Contains(devName))
                        cand.Names.Add(devName);

                    if (batVal >= 0 && batVal <= 100)
                    {
                        cand.BestBattery = batVal;
                        cand.HasRealBattery = true;
                        // NUNCA fazer cand.IsConnected = true aqui, pois Windows mantém a bateria no cache do registro mesmo desligado!
                    }

                    DeviceCategory cat = CategorizeDevice(devName, instId);
                    if (cat != DeviceCategory.Generico && cand.Category == DeviceCategory.Generico)
                    {
                        cand.Category = cat;
                    }
                }
            }
            catch { }
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

                // Para fones de ouvido / headsets: se a propriedade PnP não marcou conectado,
                // confere se há endpoint de áudio ativo no subsistema de som do Windows
                if (category == DeviceCategory.FoneDeOuvido && !cand.IsConnected)
                {
                    string lowerBest = bestName.ToLowerInvariant();
                    foreach (string activeAudio in activeAudioDevices)
                    {
                        if (string.IsNullOrEmpty(activeAudio)) continue;
                        if (activeAudio.Contains(lowerBest) || lowerBest.Contains(activeAudio))
                        {
                            cand.IsConnected = true;
                            break;
                        }
                    }
                }

                // REGRA ESTRITA DO USUÁRIO: Somente dispositivos ligados e conectados no momento aparecem!
                // Se o dispositivo estiver desligado ou desconectado, descarta
                if (!cand.IsConnected)
                {
                    continue;
                }

                // Bateria: NUNCA usa 85% arbitrário. Se não reportada pelo driver, registra -1 (desconhecido)
                int finalBattery = cand.BestBattery >= 0 ? cand.BestBattery : -1;

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
                    Details = string.Format("Bluetooth MAC: {0} | {1}", FormatMac(cand.MacAddress), cand.HasRealBattery ? "Nativo Windows" : "Conectado")
                });
            }

            return devices;
        }

        private static List<string> GetActiveAudioDeviceNames()
        {
            List<string> names = new List<string>();
            try
            {
                uint numOut = waveOutGetNumDevs();
                for (int i = 0; i < numOut; i++)
                {
                    WAVEOUTCAPS caps = new WAVEOUTCAPS();
                    if (waveOutGetDevCaps((IntPtr)i, ref caps, (uint)Marshal.SizeOf(caps)) == 0)
                    {
                        if (!string.IsNullOrEmpty(caps.szPname))
                            names.Add(caps.szPname.ToLowerInvariant());
                    }
                }

                uint numIn = waveInGetNumDevs();
                for (int i = 0; i < numIn; i++)
                {
                    WAVEINCAPS capsIn = new WAVEINCAPS();
                    if (waveInGetDevCaps((IntPtr)i, ref capsIn, (uint)Marshal.SizeOf(capsIn)) == 0)
                    {
                        if (!string.IsNullOrEmpty(capsIn.szPname))
                            names.Add(capsIn.szPname.ToLowerInvariant());
                    }
                }
            }
            catch { }
            return names;
        }

        private string ExtractBluetoothMac(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return "";

            // 1. Padrão DEV_XXXXXXXXXXXX (BTHENUM\DEV_XXXXXXXXXXXX ou BTHLE\DEV_XXXXXXXXXXXX)
            Match m = Regex.Match(instId, @"DEV_([0-9a-fA-F]{12})", RegexOptions.IgnoreCase);
            if (m.Success && IsValidMac(m.Groups[1].Value))
                return m.Groups[1].Value.ToUpperInvariant();

            // 2. Padrão PnP com &0&XXXXXXXXXXXX_ ou _XXXXXXXXXXXX_
            m = Regex.Match(instId, @"&0&([0-9a-fA-F]{12})", RegexOptions.IgnoreCase);
            if (m.Success && IsValidMac(m.Groups[1].Value))
                return m.Groups[1].Value.ToUpperInvariant();

            m = Regex.Match(instId, @"[&_]([0-9a-fA-F]{12})[_&]", RegexOptions.IgnoreCase);
            if (m.Success && IsValidMac(m.Groups[1].Value))
                return m.Groups[1].Value.ToUpperInvariant();

            // 3. Procura da direita para a esquerda por qualquer sequência de 12 hex que seja um MAC válido
            MatchCollection mc = Regex.Matches(instId, @"([0-9a-fA-F]{12})");
            for (int i = mc.Count - 1; i >= 0; i--)
            {
                string val = mc[i].Groups[1].Value;
                if (IsValidMac(val))
                    return val.ToUpperInvariant();
            }

            return "";
        }

        private static bool IsValidMac(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length != 12) return false;
            string u = mac.ToUpperInvariant();
            // Rejeita o Base UUID do Bluetooth SIG (00805F9B34FB) que não é endereço de dispositivo
            if (u == "00805F9B34FB" || u == "000000000000" || u == "FFFFFFFFFFFF") return false;
            return true;
        }

        private static string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length != 12) return mac;
            return string.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                mac.Substring(0, 2), mac.Substring(2, 2), mac.Substring(4, 2),
                mac.Substring(6, 2), mac.Substring(8, 2), mac.Substring(10, 2));
        }

        private static bool IsIgnoredServiceOrDevice(string name, string instId)
        {
            string s = (name + " " + instId).ToLowerInvariant();

            if (s.Contains("serial") || s.Contains("porta serial") || s.Contains("com port") ||
                s.Contains("obex") || s.Contains("sim access") || s.Contains("sap") ||
                s.Contains("fastpair") || s.Contains("btfastpair") || s.Contains("btnotify") ||
                s.Contains("pan network") || s.Contains("network user") || s.Contains("dun") ||
                s.Contains("syncml") || s.Contains("fax") || s.Contains("modem") ||
                s.Contains("generic access") || s.Contains("generic attribute") ||
                s.Contains("device information") || s.Contains("dispositivo periférico") ||
                s.Contains("periférico bluetooth"))
            {
                return true;
            }

            return false;
        }

        private static bool IsBlacklistedProduct(string name)
        {
            string s = name.ToLowerInvariant();

            if (s.Contains("galaxy") || s.Contains("s25") || s.Contains("s24") || s.Contains("s23") ||
                s.Contains("s22") || s.Contains("s21") || s.Contains("s20") || s.Contains("iphone") ||
                s.Contains("ipad") || s.Contains("motorola") || s.Contains("moto g") || s.Contains("moto edge") ||
                s.Contains("xiaomi") || s.Contains("redmi") || s.Contains("poco") || s.Contains("huawei") ||
                s.Contains("desktop-") || s.Contains("laptop-") || s.Contains("pc-") || s.Contains("android"))
            {
                if (!s.Contains("buds") && !s.Contains("headset") && !s.Contains("fone"))
                    return true;
            }

            return false;
        }

        private string ResolveBestName(BtCandidate cand)
        {
            string best = cand.Names.Count > 0 ? cand.Names[0] : "Dispositivo Bluetooth";
            int bestScore = -100;

            foreach (string n in cand.Names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                int score = n.Length;

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
            byte[] buf = new byte[1024];
            uint reqSize;

            if (Win32Api.SetupDiGetDevicePropertyW(hDevInfo, ref devData, ref propKey, out propType, buf, (uint)buf.Length, out reqSize, 0) && reqSize > 0)
            {
                return Encoding.Unicode.GetString(buf, 0, (int)reqSize).TrimEnd('\0').Trim();
            }

            return string.Empty;
        }

        private Dictionary<string, string> GetPairedBluetoothNames()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices"))
                {
                    if (baseKey != null)
                    {
                        foreach (string subName in baseKey.GetSubKeyNames())
                        {
                            using (RegistryKey devKey = baseKey.OpenSubKey(subName))
                            {
                                if (devKey != null)
                                {
                                    object nameVal = devKey.GetValue("FriendlyName") ?? devKey.GetValue("Name");
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
    }
}
