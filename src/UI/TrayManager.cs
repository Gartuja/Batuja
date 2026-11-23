using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class TrayManager : IDisposable
    {
        private readonly BatteryManager _manager;
        private readonly Form _mainForm;
        private readonly Dictionary<string, NotifyIcon> _deviceTrayIcons = new Dictionary<string, NotifyIcon>();
        private NotifyIcon _defaultIcon;

        public event Action<BatteryDevice> RequestColorChange;
        public Action RequestToggleOverlay { get; set; }

        public TrayManager(BatteryManager manager, Form mainForm)
        {
            _manager = manager;
            _mainForm = mainForm;

            _defaultIcon = new NotifyIcon
            {
                Text = "Batuja: Buscando dispositivos...",
                Visible = true,
                Icon = CreateNumericIcon(null, Color.FromArgb(88, 101, 242))
            };
            _defaultIcon.ContextMenuStrip = BuildDefaultContextMenu();
            _defaultIcon.DoubleClick += delegate { ToggleMainWindow(); };

            _manager.DevicesUpdated += OnDevicesUpdated;
            _manager.LowBatteryAlert += OnLowBatteryAlert;
        }

        private ContextMenuStrip BuildDefaultContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkToolStripRenderer();

            ToolStripMenuItem itemOpen = new ToolStripMenuItem(I18n.T("OpenDashboard"));
            itemOpen.Font = new Font(itemOpen.Font, FontStyle.Bold);
            itemOpen.Click += delegate { ShowMainWindow(); };
            menu.Items.Add(itemOpen);

            ToolStripMenuItem itemOverlay = new ToolStripMenuItem(I18n.T("ScreenOverlay"));
            itemOverlay.Click += delegate { if (RequestToggleOverlay != null) RequestToggleOverlay(); };
            menu.Items.Add(itemOverlay);

            ToolStripMenuItem itemRefresh = new ToolStripMenuItem(I18n.T("RefreshNow"));
            itemRefresh.Click += delegate { _manager.TriggerScanAsync(); };
            menu.Items.Add(itemRefresh);

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(BuildStyleMenu());
            menu.Items.Add(BuildLanguageMenu());

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemExit = new ToolStripMenuItem(I18n.T("ExitApp"));
            itemExit.Click += delegate
            {
                Dispose();
                Application.Exit();
            };
            menu.Items.Add(itemExit);

            return menu;
        }

        private void OnDevicesUpdated(List<BatteryDevice> devices)
        {
            if (_mainForm != null && _mainForm.InvokeRequired)
            {
                _mainForm.Invoke(new Action<List<BatteryDevice>>(OnDevicesUpdated), devices);
                return;
            }

            if (devices == null || devices.Count == 0)
            {
                ClearAllDeviceIcons();
                _defaultIcon.Visible = true;
                _defaultIcon.Text = "Batuja: Nenhum dispositivo ativo";
                return;
            }

            _defaultIcon.Visible = false;

            HashSet<string> currentKeys = new HashSet<string>();

            foreach (BatteryDevice dev in devices)
            {
                // Só exibe na barra de tarefas / bandeja os dispositivos que estão LIGADOS e com ÍCONE LIGADO
                if (!dev.IsConnected || !dev.IsTrayIconEnabled)
                    continue;

                string key = !string.IsNullOrEmpty(dev.Id) ? dev.Id : dev.Name;
                currentKeys.Add(key);

                NotifyIcon icon;
                if (!_deviceTrayIcons.TryGetValue(key, out icon))
                {
                    icon = new NotifyIcon();
                    icon.Visible = true;
                    icon.DoubleClick += delegate { ToggleMainWindow(); };
                    _deviceTrayIcons[key] = icon;
                }

                UpdateDeviceIcon(icon, dev);
            }

            // Se nenhum dispositivo estiver ligado no momento, ativa o ícone padrão indicando estado
            if (currentKeys.Count == 0)
            {
                _defaultIcon.Visible = true;
                _defaultIcon.Text = "Batuja: Todos os dispositivos estão desligados";
            }

            // Remove icons for disconnected devices
            List<string> toRemove = new List<string>();
            foreach (string k in _deviceTrayIcons.Keys)
            {
                if (!currentKeys.Contains(k))
                    toRemove.Add(k);
            }

            foreach (string rk in toRemove)
            {
                _deviceTrayIcons[rk].Visible = false;
                _deviceTrayIcons[rk].Dispose();
                _deviceTrayIcons.Remove(rk);
            }
        }

        private void UpdateDeviceIcon(NotifyIcon icon, BatteryDevice dev)
        {
            Color effectiveColor = dev.GetEffectiveColor();
            SetIcon(icon, CreateNumericIcon(dev, effectiveColor));

            string statusText = dev.IsWired ? "[USB]" : (dev.IsCharging ? "[Carregando ⚡]" : (dev.IsConnected ? "[Conectado]" : "[Offline]"));
            string tooltip = string.Format("{0}: {1} {2}",
                dev.Name, dev.GetFormattedPercentage(), statusText);

            if (tooltip.Length >= 64)
                tooltip = tooltip.Substring(0, 63);

            icon.Text = tooltip;
            icon.ContextMenuStrip = BuildDeviceContextMenu(dev);
        }

        private ContextMenuStrip BuildDeviceContextMenu(BatteryDevice dev)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkToolStripRenderer();

            ToolStripMenuItem headerItem = new ToolStripMenuItem(dev.Name);
            headerItem.Font = new Font(headerItem.Font, FontStyle.Bold);
            headerItem.Enabled = false;
            menu.Items.Add(headerItem);

            ToolStripMenuItem itemOpen = new ToolStripMenuItem(I18n.T("OpenDashboard"));
            itemOpen.Click += delegate { ShowMainWindow(); };
            menu.Items.Add(itemOpen);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemColor = new ToolStripMenuItem(I18n.T("ChangeBatteryColor"));
            itemColor.Click += delegate
            {
                if (RequestColorChange != null)
                {
                    RequestColorChange(dev);
                }
                else
                {
                    PromptColorChange(dev);
                }
            };
            menu.Items.Add(itemColor);

            if (dev.CustomColor.HasValue)
            {
                ToolStripMenuItem itemResetColor = new ToolStripMenuItem(I18n.T("RestoreAutoColor"));
                itemResetColor.Click += delegate
                {
                    _manager.ResetCustomDeviceColor(dev);
                };
                menu.Items.Add(itemResetColor);
            }

            // Submenu to change icon from tray
            ToolStripMenuItem itemIcon = new ToolStripMenuItem(I18n.T("ChangeDeviceIcon"));
            itemIcon.DropDownItems.Add(I18n.T("CatMouse"), null, delegate { _manager.SetDeviceIcon(dev, "Mouse"); });
            itemIcon.DropDownItems.Add(I18n.T("CatKeyboard"), null, delegate { _manager.SetDeviceIcon(dev, "Teclado"); });
            itemIcon.DropDownItems.Add(I18n.T("CatHeadphones"), null, delegate { _manager.SetDeviceIcon(dev, "Fone"); });
            itemIcon.DropDownItems.Add(I18n.T("CatGamepad"), null, delegate { _manager.SetDeviceIcon(dev, "Controle"); });
            itemIcon.DropDownItems.Add(I18n.T("CatLaptop"), null, delegate { _manager.SetDeviceIcon(dev, "Laptop"); });
            itemIcon.DropDownItems.Add(I18n.T("CatGeneric"), null, delegate { _manager.SetDeviceIcon(dev, "Bateria"); });
            itemIcon.DropDownItems.Add(new ToolStripSeparator());
            itemIcon.DropDownItems.Add(I18n.T("RestoreAutoIcon"), null, delegate { _manager.ResetDeviceIcon(dev); });
            menu.Items.Add(itemIcon);

            menu.Items.Add(new ToolStripSeparator());

            // Tray icon style submenu
            menu.Items.Add(BuildStyleMenu());
            menu.Items.Add(BuildLanguageMenu());

            ToolStripMenuItem itemOverlay = new ToolStripMenuItem(I18n.T("ScreenOverlay"));
            itemOverlay.Click += delegate { if (RequestToggleOverlay != null) RequestToggleOverlay(); };
            menu.Items.Add(itemOverlay);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemRefresh = new ToolStripMenuItem(I18n.T("RefreshNow"));
            itemRefresh.Click += delegate { _manager.TriggerScanAsync(); };
            menu.Items.Add(itemRefresh);

            ToolStripMenuItem itemExit = new ToolStripMenuItem(I18n.T("ExitApp"));
            itemExit.Click += delegate
            {
                Dispose();
                Application.Exit();
            };
            menu.Items.Add(itemExit);

            return menu;
        }

        public static string GetStyleLabel(TrayIconStyle style)
        {
            switch (style)
            {
                case TrayIconStyle.AeroGlass:   return I18n.T("StyleAeroGlass");
                case TrayIconStyle.Retro:       return I18n.T("StyleRetro");
                case TrayIconStyle.Minimalista: return I18n.T("StyleMinimal");
                default:                        return I18n.T("StyleModern");
            }
        }

        private ToolStripMenuItem BuildStyleMenu()
        {
            ToolStripMenuItem itemStyle = new ToolStripMenuItem(I18n.T("TrayIconStyle"));
            foreach (TrayIconStyle styleOption in Enum.GetValues(typeof(TrayIconStyle)))
            {
                TrayIconStyle captured = styleOption;
                ToolStripMenuItem si = new ToolStripMenuItem(GetStyleLabel(captured));
                si.Checked = (_manager.Settings.TrayStyle == captured);
                si.Click += delegate { ApplyTrayStyle(captured); };
                itemStyle.DropDownItems.Add(si);
            }
            return itemStyle;
        }

        private ToolStripMenuItem BuildLanguageMenu()
        {
            ToolStripMenuItem itemLang = new ToolStripMenuItem(I18n.T("BtnLanguage"));

            Action<string, AppLanguage> addLang = (name, lang) =>
            {
                ToolStripMenuItem si = new ToolStripMenuItem(name);
                si.Checked = (I18n.CurrentLanguage == lang);
                si.Click += delegate
                {
                    _manager.Settings.Language = lang;
                    _manager.Settings.Save();
                    I18n.SetLanguage(lang);
                };
                itemLang.DropDownItems.Add(si);
            };

            addLang("Português (PT)", AppLanguage.PT);
            addLang("English (EN)", AppLanguage.EN);
            addLang("Español (ES)", AppLanguage.ES);

            return itemLang;
        }

        /// <summary>Saves the style and immediately re-renders every tray icon.</summary>
        public void ApplyTrayStyle(TrayIconStyle style)
        {
            _manager.Settings.TrayStyle = style;
            _manager.Settings.Save();

            SetIcon(_defaultIcon, CreateNumericIcon(null, Color.FromArgb(88, 101, 242)));
            _defaultIcon.ContextMenuStrip = BuildDefaultContextMenu();

            List<BatteryDevice> devices = _manager.CurrentDevices;
            if (devices != null && devices.Count > 0)
                OnDevicesUpdated(devices);
        }

        private static void SetIcon(NotifyIcon target, Icon newIcon)
        {
            Icon old = target.Icon;
            target.Icon = newIcon;
            if (old != null)
            {
                DestroyIcon(old.Handle);
                old.Dispose();
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void PromptColorChange(BatteryDevice dev)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = dev.GetEffectiveColor();
                cd.FullOpen = true;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    _manager.SetCustomDeviceColor(dev, cd.Color);
                }
            }
        }

        private Icon CreateNumericIcon(BatteryDevice dev, Color accentColor)
        {
            TrayIconStyle style = _manager.Settings.TrayStyle;

            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    string text = "ON";
                    if (dev != null)
                    {
                        if (dev.IsWired)
                            text = "USB";
                        else if (dev.BatteryPercent >= 0)
                            text = dev.BatteryPercent.ToString();
                    }

                    Rectangle bgRect = new Rectangle(1, 3, 30, 26);

                    switch (style)
                    {
                        // ── Aero Glass ───────────────────────────────────────────
                        case TrayIconStyle.AeroGlass:
                        {
                            using (GraphicsPath path = CreateRoundedRect(bgRect, 6))
                            {
                                // Frosted glass base
                                using (SolidBrush glassBg = new SolidBrush(Color.FromArgb(140, 30, 36, 56)))
                                    g.FillPath(glassBg, path);
                                // Top gloss sheen
                                Rectangle gloss = new Rectangle(bgRect.X + 2, bgRect.Y + 1, bgRect.Width - 4, bgRect.Height / 2);
                                using (LinearGradientBrush shine = new LinearGradientBrush(gloss,
                                    Color.FromArgb(80, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                                    LinearGradientMode.Vertical))
                                {
                                    using (GraphicsPath glossPath = CreateRoundedRect(gloss, 4))
                                        g.FillPath(shine, glossPath);
                                }
                                // Soft accent rim
                                using (Pen borderPen = new Pen(Color.FromArgb(160, accentColor), 1.2f))
                                    g.DrawPath(borderPen, path);
                            }
                            // Text with soft shadow
                            float fs = text.Length >= 3 ? 9.5f : 11.5f;
                            using (Font font = new Font("Segoe UI", fs, FontStyle.Bold))
                            {
                                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                Rectangle tr = new Rectangle(0, 3, 32, 26);
                                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                                    g.DrawString(text, font, shadow, new Rectangle(1, 4, 32, 26), sf);
                                using (SolidBrush textBrush = new SolidBrush(Color.White))
                                    g.DrawString(text, font, textBrush, tr, sf);
                            }
                            break;
                        }

                        // ── Retro ─────────────────────────────────────────────────
                        case TrayIconStyle.Retro:
                        {
                            // Flat chunky pixel-style rectangle
                            Rectangle outer = bgRect;
                            // Dark border
                            using (SolidBrush darkBorder = new SolidBrush(Color.FromArgb(255, 20, 24, 36)))
                                g.FillRectangle(darkBorder, outer);
                            // Classic gradient fill
                            Rectangle inner = new Rectangle(outer.X + 2, outer.Y + 2, outer.Width - 4, outer.Height - 4);
                            using (LinearGradientBrush retroGrad = new LinearGradientBrush(inner,
                                Color.FromArgb(255, 50, 60, 100), Color.FromArgb(255, 20, 28, 55),
                                LinearGradientMode.Vertical))
                                g.FillRectangle(retroGrad, inner);
                            // Accent top line (scanline look)
                            using (Pen scanline = new Pen(Color.FromArgb(180, accentColor), 1f))
                                g.DrawLine(scanline, inner.X, inner.Y, inner.Right, inner.Y);
                            // Bold pixel-style border
                            using (Pen border = new Pen(accentColor, 2f))
                                g.DrawRectangle(border, outer);

                            float fs2 = text.Length >= 3 ? 9f : 11.5f;
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                            using (Font font2 = new Font("Consolas", fs2, FontStyle.Bold))
                            {
                                StringFormat sf2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                Rectangle tr2 = new Rectangle(0, 3, 32, 26);
                                using (SolidBrush textBrush2 = new SolidBrush(accentColor))
                                    g.DrawString(text, font2, textBrush2, tr2, sf2);
                            }
                            break;
                        }

                        // ── Minimalista ───────────────────────────────────────────
                        case TrayIconStyle.Minimalista:
                        {
                            // No background box — just bare number with tiny underline
                            float fs3 = text.Length >= 3 ? 10f : 13f;
                            using (Font font3 = new Font("Segoe UI", fs3, FontStyle.Bold))
                            {
                                StringFormat sf3 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                Rectangle tr3 = new Rectangle(0, 0, 32, 28);
                                using (SolidBrush textBrush3 = new SolidBrush(accentColor))
                                    g.DrawString(text, font3, textBrush3, tr3, sf3);
                            }
                            // Thin underline accent
                            using (Pen underline = new Pen(Color.FromArgb(180, accentColor), 1.5f))
                                g.DrawLine(underline, 6, 28, 26, 28);
                            break;
                        }

                        // ── Moderno (default) ─────────────────────────────────────
                        default:
                        {
                            using (GraphicsPath path = CreateRoundedRect(bgRect, 4))
                            {
                                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(220, 24, 28, 42)))
                                    g.FillPath(bgBrush, path);
                                using (Pen borderPen = new Pen(accentColor, 1.8f))
                                    g.DrawPath(borderPen, path);
                            }
                            float fs4 = text.Length >= 3 ? 9.5f : 11.5f;
                            using (Font font4 = new Font("Segoe UI", fs4, FontStyle.Bold))
                            {
                                StringFormat sf4 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                Rectangle tr4 = new Rectangle(0, 3, 32, 26);
                                using (SolidBrush textBrush4 = new SolidBrush(Color.White))
                                    g.DrawString(text, font4, textBrush4, tr4, sf4);
                            }
                            break;
                        }
                    }

                    // Charging bolt overlay (all styles)
                    if (dev != null && dev.IsCharging)
                    {
                        using (Font boltFont = new Font("Segoe UI Emoji", 7f, FontStyle.Bold))
                        using (SolidBrush boltBrush = new SolidBrush(Color.FromArgb(0, 210, 211)))
                            g.DrawString("⚡", boltFont, boltBrush, 18, 0);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private void OnLowBatteryAlert(BatteryDevice device, string message)
        {
            try
            {
                string key = !string.IsNullOrEmpty(device.Id) ? device.Id : device.Name;
                NotifyIcon icon;
                if (_deviceTrayIcons.TryGetValue(key, out icon))
                {
                    icon.ShowBalloonTip(5000, "Bateria Fraca: " + device.Name, message, ToolTipIcon.Warning);
                }
                else if (_defaultIcon.Visible)
                {
                    _defaultIcon.ShowBalloonTip(5000, "Bateria Fraca: " + device.Name, message, ToolTipIcon.Warning);
                }
            }
            catch { }
        }

        public void ShowMainWindow()
        {
            if (_mainForm != null)
            {
                _mainForm.Show();
                _mainForm.WindowState = FormWindowState.Normal;
                _mainForm.BringToFront();
                _mainForm.Activate();
            }
        }

        public void ToggleMainWindow()
        {
            if (_mainForm.Visible && _mainForm.WindowState != FormWindowState.Minimized)
            {
                _mainForm.Hide();
            }
            else
            {
                ShowMainWindow();
            }
        }

        private void ClearAllDeviceIcons()
        {
            foreach (var icon in _deviceTrayIcons.Values)
            {
                icon.Visible = false;
                icon.Dispose();
            }
            _deviceTrayIcons.Clear();
        }

        public void Dispose()
        {
            ClearAllDeviceIcons();
            if (_defaultIcon != null)
            {
                _defaultIcon.Visible = false;
                _defaultIcon.Dispose();
                _defaultIcon = null;
            }
        }
    }
}
