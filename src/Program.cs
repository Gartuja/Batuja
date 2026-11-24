using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;
using OmniBattery.UI;

namespace OmniBattery
{
    static class Program
    {
        private const string MutexName = "OmniBatteryMonitor_SingleInstance_Mutex_987213";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [STAThread]
        static void Main(string[] args)
        {
            bool scanOnly = false;
            bool startInTray = false;

            foreach (string arg in args)
            {
                if (arg.Equals("--scan", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-scan", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--test", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-test", StringComparison.OrdinalIgnoreCase))
                {
                    scanOnly = true;
                    break;
                }
                else if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("-tray", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("/tray", StringComparison.OrdinalIgnoreCase))
                {
                    startInTray = true;
                }
            }

            if (scanOnly)
            {
                RunScanMode();
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("O MoniTuja já está em execução na bandeja do sistema!",
                        "MoniTuja - Monitor de Dispositivos sem Fio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppSettings settings = AppSettings.Load();

                // Executable was renamed to MoniTuja.exe: refresh the autostart path if enabled
                if (settings.StartWithWindows)
                    AutostartManager.SetAutostart(true);

                BatteryManager manager = new BatteryManager(settings);
                manager.Start();

                MainForm mainForm = new MainForm(manager);

                if (startInTray)
                {
                    mainForm.WindowState = FormWindowState.Minimized;
                    mainForm.ShowInTaskbar = false;
                    Application.Run();
                }
                else
                {
                    Application.Run(mainForm);
                }
            }
        }

        private static void RunScanMode()
        {
            AttachConsole(unchecked((uint)-1));

            Console.WriteLine();
            Console.WriteLine("==========================================================================");
            Console.WriteLine("  MoniTuja - Diagnostico e Varredura de Dispositivos sem Fio            ");
            Console.WriteLine("==========================================================================");
            Console.WriteLine("Iniciando varredura nos protocolos: Compx 2.4G, Logitech, XInput, Bluetooth...");
            Console.WriteLine();

            AppSettings settings = AppSettings.Load();
            settings.ShowSystemBattery = true;
            BatteryManager manager = new BatteryManager(settings);

            var scanners = new List<Protocols.IProtocolScanner>
            {
                new Protocols.CompxScanner(),
                new Protocols.LogitechScanner(),
                new Protocols.XInputScanner(),
                new Protocols.BluetoothPnPScanner(),
                new Protocols.GamepadHidScanner(),
                new Protocols.GenericHidScanner(true)
            };

            int totalFound = 0;
            foreach (var sc in scanners)
            {
                try
                {
                    var devs = sc.Scan();
                    if (devs != null && devs.Count > 0)
                    {
                        foreach (var d in devs)
                        {
                            totalFound++;
                            string batStr = d.BatteryPercent >= 0 ? d.BatteryPercent + "%" : "Desconhecido";
                            string status = d.IsCharging ? "[CARREGANDO]" : (d.IsConnected ? "[CONECTADO]" : "[OFFLINE]");

                            Console.WriteLine(string.Format("o [{0}] {1}", d.Category, d.Name));
                            Console.WriteLine(string.Format("  Bateria:   {0} {1}", batStr, status));
                            Console.WriteLine(string.Format("  Protocolo: {0}", d.Protocol));
                            if (d.Voltage > 0)
                                Console.WriteLine(string.Format("  Tensao:    {0:F2}V", d.Voltage));
                            if (!string.IsNullOrEmpty(d.Details))
                                Console.WriteLine(string.Format("  Detalhes:  {0}", d.Details));
                            Console.WriteLine();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Erro no scanner {0}: {1}", sc.Name, ex.Message));
                }
            }

            if (totalFound == 0)
            {
                Console.WriteLine("Nenhum dispositivo sem fio com telemetria ativa detectado no ambiente atual.");
                Console.WriteLine("Dica: Certifique-se de que o dongle USB (Attack Shark / MCHOSE / Logitech) esta plugado");
                Console.WriteLine("ou o dispositivo Bluetooth (JBL / Xbox / Anbernic) esta ligado.");
            }
            else
            {
                Console.WriteLine(string.Format("Total de dispositivos detectados: {0}", totalFound));
            }

            Console.WriteLine("==========================================================================");
            Console.WriteLine();
        }
    }
}
