using System;
using System.Collections.Generic;

namespace OmniBattery.Core
{
    public enum AppLanguage
    {
        PT, // Português (Padrão)
        EN, // English
        ES  // Español
    }

    public static class I18n
    {
        public static AppLanguage CurrentLanguage { get; set; }

        public static event Action LanguageChanged;

        private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Strings =
            new Dictionary<string, Dictionary<AppLanguage, string>>(StringComparer.OrdinalIgnoreCase);

        static I18n()
        {
            CurrentLanguage = AppLanguage.PT;
            RegisterStrings();
        }

        public static void SetLanguage(AppLanguage lang)
        {
            if (CurrentLanguage != lang)
            {
                CurrentLanguage = lang;
                if (LanguageChanged != null)
                    LanguageChanged();
            }
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";

            Dictionary<AppLanguage, string> dict;
            if (Strings.TryGetValue(key, out dict))
            {
                string val;
                if (dict.TryGetValue(CurrentLanguage, out val))
                    return val;
                if (dict.TryGetValue(AppLanguage.PT, out val))
                    return val;
            }

            return key;
        }

        public static string T(string key, params object[] args)
        {
            string format = T(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        private static void Reg(string key, string pt, string en, string es)
        {
            Dictionary<AppLanguage, string> dict = new Dictionary<AppLanguage, string>
            {
                { AppLanguage.PT, pt },
                { AppLanguage.EN, en },
                { AppLanguage.ES, es }
            };
            Strings[key] = dict;
        }

        private static void RegisterStrings()
        {
            // App Title & Header
            Reg("AppTitle", "Batuja - Monitor de Dispositivos sem Fio", "Batuja - Wireless Device Monitor", "Batuja - Monitor de Dispositivos Inalámbricos");
            Reg("DevicesCount", "{0} dispositivo(s)", "{0} device(s)", "{0} dispositivo(s)");
            Reg("Ready", "Pronto", "Ready", "Listo");
            Reg("ScanningStatus", "Buscando dispositivos sem fio...", "Searching for wireless devices...", "Buscando dispositivos inalámbricos...");
            Reg("LastUpdatedStatus", "Última atualização às {0:HH:mm:ss} (Próxima em {1}s)", "Last updated at {0:HH:mm:ss} (Next in {1}s)", "Última actualización a las {0:HH:mm:ss} (Próxima en {1}s)");
            Reg("NoDevicesTitle", "Nenhum dispositivo sem fio detectado", "No wireless devices detected", "Ningún dispositivo inalámbrico detectado");
            Reg("NoDevicesDesc1", "• Conecte seus fones, mouse ou teclado sem fio (2.4GHz ou Bluetooth).", "• Connect your wireless headphones, mouse or keyboard (2.4GHz or Bluetooth).", "• Conecte sus auriculares, ratón o teclado inalámbrico (2.4GHz o Bluetooth).");
            Reg("NoDevicesDesc2", "• Certifique-se de que estão ligados e o receptor USB ou Bluetooth pareado.", "• Make sure they are turned on and the USB receiver or Bluetooth is paired.", "• Asegúrese de que estén encendidos y el receptor USB o Bluetooth emparejado.");
            Reg("NoDevicesDesc3", "• Clique em 'Atualizar' para fazer uma nova varredura agora.", "• Click 'Refresh' to scan again now.", "• Haga clic en 'Actualizar' para volver a escanear ahora.");
            Reg("NoDevicesDesc4", "• Cada dispositivo ganhará seu ícone com % na bandeja do sistema!", "• Each device will get its own icon with % in the system tray!", "• ¡Cada dispositivo tendrá su propio ícono con % en la bandeja del sistema!");

            // Header Buttons
            Reg("BtnAdmin", "Admin", "Admin", "Admin");
            Reg("BtnBackground", "Fundo", "Background", "Fondo");
            Reg("BtnOverlay", "Sobrepor", "Overlay", "Superponer");
            Reg("BtnOverlayActive", "● Sobrepor", "● Overlay", "● Superponer");
            Reg("BtnHistory", "Histórico", "History", "Historial");
            Reg("BtnRefresh", "Atualizar", "Refresh", "Actualizar");
            Reg("BtnScanning", "Buscando dispositivos...", "Searching devices...", "Buscando dispositivos...");
            Reg("BtnSettings", "Configs", "Settings", "Ajustes");
            Reg("BtnLanguage", "Idioma", "Language", "Idioma");

            // Device Card
            Reg("IconOn", "● Ícone Ligado", "● Icon On", "● Ícono Activado");
            Reg("IconOff", "○ Ícone Desligado", "○ Icon Off", "○ Ícono Desactivado");
            Reg("BtnColor", "Cor", "Color", "Color");
            Reg("BtnIcon", "Ícone", "Icon", "Ícono");
            Reg("Charging", "Carregando", "Charging", "Cargando");
            Reg("UpdatedAgo", "Atualizado às {0:HH:mm:ss}", "Updated at {0:HH:mm:ss}", "Actualizado a las {0:HH:mm:ss}");
            Reg("UsbActive", "Receptor 2.4GHz USB Ativo", "Active 2.4GHz USB Receiver", "Receptor USB 2.4GHz Activo");
            Reg("BtActive", "Bluetooth Conectado", "Bluetooth Connected", "Bluetooth Conectado");
            Reg("WirelessSubtitle", "Sem Fio", "Wireless", "Inalámbrico");
            Reg("WirelessDeviceSubtitle", "Dispositivo Sem Fio", "Wireless Device", "Dispositivo Inalámbrico");
            Reg("WirelessBadge", "Sem Fio", "Wireless", "Inalámbrico");
            Reg("WiredDeviceSubtitle", "Dispositivo com Fio", "Wired Device", "Dispositivo con Cable");
            Reg("WiredDeviceNotice", "Dispositivo com Fio (Sem bateria)", "Wired Device (No battery)", "Dispositivo con Cable (Sin batería)");
            Reg("WiredBadge", "Com Fio", "Wired", "Con Cable");
            Reg("VoltageLabel", "Tensão: {0:F2}V", "Voltage: {0:F2}V", "Voltaje: {0:F2}V");
            Reg("OverlayNoDevices", "Batuja: Nenhum dispositivo sem fio ligado", "Batuja: No wireless devices turned on", "Batuja: Ningún dispositivo inalámbrico encendido");

            // Categories
            Reg("CatMouse", "Mouse", "Mouse", "Ratón / Mouse");
            Reg("CatKeyboard", "Teclado", "Keyboard", "Teclado");
            Reg("CatHeadphones", "Fone de Ouvido / Headset", "Headphones / Headset", "Auriculares / Headset");
            Reg("CatGamepad", "Controle / Gamepad", "Controller / Gamepad", "Mando / Gamepad");
            Reg("CatLaptop", "Laptop / Notebook", "Laptop / Notebook", "Portátil / Laptop");
            Reg("CatGeneric", "Bateria / Genérico", "Battery / Generic", "Batería / Genérico");

            // Menus
            Reg("ChooseImageFile", "Escolher Imagem (.png, .ico, .jpg, .gif)...", "Choose Custom Image (.png, .ico, .jpg, .gif)...", "Elegir Imagen Personalizada (.png, .ico, .jpg, .gif)...");
            Reg("RestoreAutoIcon", "Restaurar Ícone Automático", "Restore Automatic Icon", "Restaurar Ícono Automático");
            Reg("RestoreAutoColor", "Restaurar Cor Automática", "Restore Automatic Color", "Restaurar Color Automático");
            Reg("OpenDashboard", "Abrir Painel", "Open Dashboard", "Abrir Panel");
            Reg("ChangeBatteryColor", "Mudar Cor Desta Bateria...", "Change Battery Color...", "Cambiar Color de Batería...");
            Reg("ChangeDeviceIcon", "Mudar Ícone Deste Dispositivo", "Change Device Icon", "Cambiar Ícono de este Dispositivo");
            Reg("TrayIconStyle", "Estilo dos Ícones da Bandeja", "Tray Icon Style", "Estilo de Íconos de Barra");
            Reg("ScreenOverlay", "Sobreposição na Tela (HUD)", "Screen Overlay (HUD)", "Superposición en Pantalla (HUD)");
            Reg("RefreshNow", "Atualizar Agora", "Refresh Now", "Actualizar Ahora");
            Reg("ExitApp", "Sair do Batuja", "Exit Batuja", "Salir de Batuja");
            Reg("StyleModern", "Moderno", "Modern", "Moderno");
            Reg("StyleAeroGlass", "AeroGlass", "AeroGlass", "AeroGlass");
            Reg("StyleRetro", "Retro", "Retro", "Retro");
            Reg("StyleMinimal", "Minimalista", "Minimalist", "Minimalista");

            // Settings Dialog
            Reg("SettingsTitle", "Batuja - Configurações", "Batuja - Settings", "Batuja - Ajustes");
            Reg("RefreshIntervalLabel", "Intervalo de Atualização (segundos):", "Refresh Interval (seconds):", "Intervalo de Actualización (segundos):");
            Reg("LowBatteryThresholdLabel", "Alerta de Bateria Baixa (%):", "Low Battery Alert (%):", "Alerta de Batería Baja (%):");
            Reg("EnableNotifLabel", "Exibir Notificações do Windows", "Show Windows Notifications", "Mostrar Notificaciones de Windows");
            Reg("StartWithWinLabel", "Iniciar com o Windows", "Start with Windows", "Iniciar con Windows");
            Reg("ShowSysBatLabel", "Exibir Bateria do Notebook / Sistema", "Show Notebook / System Battery", "Mostrar Batería de Portátil / Sistema");
            Reg("TrayStyleLabel", "Estilo do Ícone da Bandeja:", "Tray Icon Style:", "Estilo del Ícono en Bandeja:");
            Reg("LanguageLabel", "Idioma / Language / Idioma:", "Language / Idioma:", "Idioma / Language:");
            Reg("BtnSave", "Salvar", "Save", "Guardar");
            Reg("BtnCancel", "Cancelar", "Cancel", "Cancelar");
            Reg("MinToTrayOnClose", "Minimizar para a bandeja ao fechar", "Minimize to tray on close", "Minimizar a la bandeja al cerrar");
            Reg("AppVersionLabel", "Versão do Batuja: {0}", "Batuja Version: {0}", "Versión de Batuja: {0}");
            Reg("CheckUpdatesBtn", "Buscar Atualizações no GitHub", "Check for Updates on GitHub", "Buscar Actualizaciones en GitHub");
            Reg("CheckingUpdates", "Buscando atualizações...", "Checking for updates...", "Buscando actualizaciones...");
            Reg("UpdateTitle", "Atualizações do Batuja", "Batuja Updates", "Actualizaciones de Batuja");
            Reg("UpdateAvailableMsg", "Uma nova versão ({0}) está disponível no GitHub!\nDeseja abrir a página de download agora?", "A new version ({0}) is available on GitHub!\nDo you want to open the download page now?", "¡Una nueva versión ({0}) está disponible en GitHub!\n¿Desea abrir la página de descarga ahora?");
            Reg("UpdateLatestMsg", "Você já está utilizando a versão mais recente ({0}).", "You are already using the latest version ({0}).", "Ya estás utilizando la versión más reciente ({0}).");
            Reg("UpdateErrorMsg", "Não foi possível verificar atualizações no momento.\nVerifique sua conexão à internet.", "Could not check for updates right now.\nPlease check your internet connection.", "No se pudo verificar actualizaciones en este momento.\nVerifique su conexión a internet.");

            // History Form
            Reg("HistoryTitle", "Batuja - Histórico de Dispositivos", "Batuja - Device History", "Batuja - Historial de Dispositivos");
            Reg("HistorySubtitle", "Todos os dispositivos sem fio já conectados durante o uso do Batuja", "All wireless devices connected during Batuja usage", "Todos los dispositivos inalámbricos conectados durante el uso de Batuja");
            Reg("ClearHistory", "Limpar Histórico", "Clear History", "Limpiar Historial");
            Reg("Close", "Fechar", "Close", "Cerrar");
            Reg("ConnectedStatus", "Conectado", "Connected", "Conectado");
            Reg("DisconnectedStatus", "Desconectado", "Disconnected", "Desconectado");
            Reg("LastSeen", "Visto: {0}", "Seen: {0}", "Visto: {0}");
            Reg("LastCharge", "Última carga: {0}%", "Last charge: {0}%", "Última carga: {0}%");
        }
    }
}
