using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class OverlayForm : Form
    {
        private readonly BatteryManager _manager;
        private List<BatteryDevice> _allConnectedDevices = new List<BatteryDevice>();
        private List<BatteryDevice> _devices = new List<BatteryDevice>();

        private bool _isDragging = false;
        private Point _dragStart;
        private Rectangle _closeBtnRect;
        private bool _isCloseHovered = false;
        private bool _isHovered = false;
        private bool _isLocked = false;

        public event Action<bool> OverlayStateChanged;

        public OverlayForm(BatteryManager manager)
        {
            _manager = manager;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(14, 16, 22);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            try { Icon = AppIconRenderer.CreateIcon(); } catch { }

            Opacity = Math.Max(0.3, Math.Min(1.0, _manager.Settings.OverlayOpacity / 100.0));

            int ox = _manager.Settings.OverlayX;
            int oy = _manager.Settings.OverlayY;
            if (ox >= 0 && oy >= 0)
            {
                Location = new Point(ox, oy);
            }
            else
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Right - 120, wa.Top + 24);
            }

            _manager.DevicesUpdated += OnDevicesUpdated;
            UpdateDevicesList(_manager.CurrentDevices);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (não rouba foco de jogos)
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (não polui Alt+Tab)
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkToolStripRenderer();

            // 1. Escolher Dispositivo para o Overlay
            string txtDevSel = I18n.CurrentLanguage == AppLanguage.EN ? "Overlay Device" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Dispositivo en Overlay" : "Dispositivo no Overlay");
            ToolStripMenuItem itemDevSel = new ToolStripMenuItem(txtDevSel);

            string txtAllDevs = I18n.CurrentLanguage == AppLanguage.EN ? "All Devices" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Todos los dispositivos" : "Todos os dispositivos");
            ToolStripMenuItem itAll = new ToolStripMenuItem(txtAllDevs);
            itAll.Checked = string.IsNullOrEmpty(_manager.Settings.OverlaySelectedDeviceId);
            itAll.Click += delegate
            {
                _manager.Settings.OverlaySelectedDeviceId = "";
                _manager.Settings.Save();
                UpdateDevicesList(_manager.CurrentDevices);
            };
            itemDevSel.DropDownItems.Add(itAll);
            itemDevSel.DropDownItems.Add(new ToolStripSeparator());

            if (_allConnectedDevices != null && _allConnectedDevices.Count > 0)
            {
                foreach (BatteryDevice d in _allConnectedDevices)
                {
                    BatteryDevice curD = d;
                    ToolStripMenuItem itDev = new ToolStripMenuItem(curD.Name);
                    itDev.Checked = string.Equals(_manager.Settings.OverlaySelectedDeviceId, curD.DeviceKey, StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(_manager.Settings.OverlaySelectedDeviceId, curD.Name, StringComparison.OrdinalIgnoreCase);
                    itDev.Click += delegate
                    {
                        _manager.Settings.OverlaySelectedDeviceId = curD.DeviceKey;
                        _manager.Settings.Save();
                        UpdateDevicesList(_manager.CurrentDevices);
                    };
                    itemDevSel.DropDownItems.Add(itDev);
                }
            }
            menu.Items.Add(itemDevSel);
            menu.Items.Add(new ToolStripSeparator());

            // 2. Alternar Orientação
            string txtOrient = I18n.CurrentLanguage == AppLanguage.EN ? "Toggle Orientation (Horizontal / Vertical)" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Alternar Orientación (Horizontal / Vertical)" : "Alternar Orientação (Horizontal / Vertical)");
            ToolStripMenuItem itemOrient = new ToolStripMenuItem(txtOrient);
            itemOrient.Click += delegate
            {
                _manager.Settings.OverlayVertical = !_manager.Settings.OverlayVertical;
                _manager.Settings.Save();
                UpdateLayout();
            };
            menu.Items.Add(itemOrient);

            // 3. Opacidade
            string txtOpacity = I18n.CurrentLanguage == AppLanguage.EN ? "Opacity" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Opacidad" : "Opacidade");
            ToolStripMenuItem itemOpacity = new ToolStripMenuItem(txtOpacity);
            int[] opacities = new int[] { 100, 90, 75, 60, 45 };
            foreach (int op in opacities)
            {
                int val = op;
                ToolStripMenuItem it = new ToolStripMenuItem(val + "%");
                it.Checked = (_manager.Settings.OverlayOpacity == val);
                it.Click += delegate
                {
                    _manager.Settings.OverlayOpacity = val;
                    Opacity = val / 100.0;
                    _manager.Settings.Save();
                    BuildContextMenu();
                };
                itemOpacity.DropDownItems.Add(it);
            }
            menu.Items.Add(itemOpacity);

            // 4. Bloquear Posição
            string txtLock = I18n.CurrentLanguage == AppLanguage.EN ? "Lock Position" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Bloquear Posición" : "Bloquear Posição");
            ToolStripMenuItem itemLock = new ToolStripMenuItem(txtLock);
            itemLock.CheckOnClick = true;
            itemLock.Checked = _isLocked;
            itemLock.CheckedChanged += delegate
            {
                _isLocked = itemLock.Checked;
            };
            menu.Items.Add(itemLock);

            menu.Items.Add(new ToolStripSeparator());

            // 5. Ocultar
            string txtClose = I18n.CurrentLanguage == AppLanguage.EN ? "Hide Overlay" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Ocultar Overlay" : "Ocultar Overlay");
            ToolStripMenuItem itemClose = new ToolStripMenuItem(txtClose);
            itemClose.Click += delegate { HideOverlay(); };
            menu.Items.Add(itemClose);

            ContextMenuStrip = menu;
        }

        private void OnDevicesUpdated(List<BatteryDevice> devices)
        {
            if (InvokeRequired)
            {
                try { Invoke(new Action<List<BatteryDevice>>(OnDevicesUpdated), devices); } catch { }
                return;
            }

            UpdateDevicesList(devices);
        }

        public void UpdateDevicesList(List<BatteryDevice> all)
        {
            List<BatteryDevice> active = new List<BatteryDevice>();
            if (all != null)
            {
                foreach (BatteryDevice dev in all)
                {
                    if (dev != null && dev.IsConnected && !dev.IsHidden)
                    {
                        active.Add(dev);
                    }
                }
            }

            _allConnectedDevices = active;

            // Filtro por dispositivo específico escolhido pelo usuário
            string targetId = _manager.Settings.OverlaySelectedDeviceId;
            if (!string.IsNullOrEmpty(targetId))
            {
                BatteryDevice found = active.Find(d => string.Equals(d.DeviceKey, targetId, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(d.Name, targetId, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    _devices = new List<BatteryDevice> { found };
                }
                else
                {
                    // Dispositivo selecionado não está online no momento
                    _devices = active.Count > 0 ? new List<BatteryDevice> { active[0] } : new List<BatteryDevice>();
                }
            }
            else
            {
                _devices = active;
            }

            BuildContextMenu();
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            bool isVert = _manager.Settings.OverlayVertical;
            int count = Math.Max(1, _devices.Count);

            if (!isVert)
            {
                // Layout Horizontal Ultracompacto Gamer
                // Para 1 dispositivo: largura ~68px, altura 28px
                // Para múltiplos: 56px por dispositivo + margens
                int itemW = 56;
                int totalW = (_devices.Count <= 1) ? 68 : (count * itemW + 12);
                int totalH = 28;
                Size = new Size(totalW, totalH);
            }
            else
            {
                // Layout Vertical Compacto
                int itemH = 28;
                int totalW = 68;
                int totalH = Math.Max(28, count * itemH + 4);
                Size = new Size(totalW, totalH);
            }

            _closeBtnRect = new Rectangle(Width - 14, 2, 12, 12);
            Invalidate();
        }

        public void ToggleVisibility()
        {
            if (Visible)
            {
                HideOverlay();
            }
            else
            {
                ShowOverlay();
            }
        }

        public void ShowOverlay()
        {
            _manager.Settings.OverlayEnabled = true;
            _manager.Settings.Save();
            Show();
            UpdateDevicesList(_manager.CurrentDevices);
            if (OverlayStateChanged != null) OverlayStateChanged(true);
        }

        public void HideOverlay()
        {
            _manager.Settings.OverlayEnabled = false;
            _manager.Settings.Save();
            Hide();
            if (OverlayStateChanged != null) OverlayStateChanged(false);
        }

        // ── Mouse Drag & Hover ──────────────────────────────────────────────
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isCloseHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_isHovered && _closeBtnRect.Contains(e.Location))
                {
                    HideOverlay();
                    return;
                }

                if (!_isLocked)
                {
                    _isDragging = true;
                    _dragStart = new Point(e.X, e.Y);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool ch = _isHovered && _closeBtnRect.Contains(e.Location);
            if (ch != _isCloseHovered)
            {
                _isCloseHovered = ch;
                Invalidate();
            }

            if (_isDragging && !_isLocked)
            {
                Point cur = PointToScreen(e.Location);
                Location = new Point(cur.X - _dragStart.X, cur.Y - _dragStart.Y);
            }
            else
            {
                Cursor = ch ? Cursors.Hand : (_isLocked ? Cursors.Default : Cursors.SizeAll);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                _manager.Settings.OverlayX = Location.X;
                _manager.Settings.OverlayY = Location.Y;
                _manager.Settings.Save();
            }
        }

        // ── Paint HUD Minimalista Gamer ─────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Min(12, Height / 2);

            // 1. Fundo Pílula Translúcida Dark
            using (GraphicsPath bgp = RoundedRect(rect, radius))
            {
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(195, 12, 14, 20)))
                    g.FillPath(bb, bgp);
                using (Pen bp = new Pen(Color.FromArgb(55, 255, 255, 255), 1f))
                    g.DrawPath(bp, bgp);
            }

            // 2. Dispositivos (Ícone + % Bateria)
            if (_devices.Count == 0)
            {
                // Estado sem dispositivos ativos
                Rectangle iconR = new Rectangle(8, 6, 16, 16);
                IconRenderer.DrawDeviceIcon(g, iconR, DeviceCategory.Generico, Color.FromArgb(140, 145, 165), null);

                using (Font pf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(140, 145, 165)))
                {
                    g.DrawString("--", pf, pb, 28, 5);
                }
            }
            else
            {
                bool isVert = _manager.Settings.OverlayVertical;

                if (!isVert)
                {
                    // Horizontal
                    int curX = (_devices.Count == 1) ? 7 : 6;
                    int itemW = (_devices.Count == 1) ? 54 : 54;

                    for (int i = 0; i < _devices.Count; i++)
                    {
                        BatteryDevice dev = _devices[i];
                        Color effCol = dev.GetEffectiveColor();

                        // Ícone do dispositivo
                        Rectangle iconRect = new Rectangle(curX, (Height - 16) / 2, 16, 16);
                        IconRenderer.DrawDeviceIcon(g, iconRect, dev.EffectiveCategory, effCol, dev.CustomIconPath);

                        // Texto de Bateria / USB
                        string txt;
                        Color txtColor;
                        if (dev.IsWired)
                        {
                            txt = "USB";
                            txtColor = Color.FromArgb(100, 200, 255);
                        }
                        else
                        {
                            txt = dev.BatteryPercent >= 0 ? dev.BatteryPercent + "%" : "--";
                            if (dev.BatteryPercent >= 0 && dev.BatteryPercent <= 20)
                                txtColor = Color.FromArgb(255, 80, 80);
                            else
                                txtColor = effCol;
                        }

                        using (Font pf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                        using (SolidBrush pb = new SolidBrush(txtColor))
                        {
                            g.DrawString(txt, pf, pb, curX + 18, (Height - 17) / 2);
                        }

                        // Divisor sutil entre múltiplos dispositivos
                        if (i < _devices.Count - 1)
                        {
                            using (Pen dp = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                            {
                                int sepX = curX + itemW;
                                g.DrawLine(dp, sepX, 5, sepX, Height - 6);
                            }
                        }

                        curX += itemW + 4;
                    }
                }
                else
                {
                    // Vertical
                    int curY = 4;
                    int itemH = 28;

                    for (int i = 0; i < _devices.Count; i++)
                    {
                        BatteryDevice dev = _devices[i];
                        Color effCol = dev.GetEffectiveColor();

                        Rectangle iconRect = new Rectangle(8, curY + 6, 16, 16);
                        IconRenderer.DrawDeviceIcon(g, iconRect, dev.EffectiveCategory, effCol, dev.CustomIconPath);

                        string txt;
                        Color txtColor;
                        if (dev.IsWired)
                        {
                            txt = "USB";
                            txtColor = Color.FromArgb(100, 200, 255);
                        }
                        else
                        {
                            txt = dev.BatteryPercent >= 0 ? dev.BatteryPercent + "%" : "--";
                            if (dev.BatteryPercent >= 0 && dev.BatteryPercent <= 20)
                                txtColor = Color.FromArgb(255, 80, 80);
                            else
                                txtColor = effCol;
                        }

                        using (Font pf = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                        using (SolidBrush pb = new SolidBrush(txtColor))
                        {
                            g.DrawString(txt, pf, pb, 28, curY + 5);
                        }

                        curY += itemH;
                    }
                }
            }

            // 3. Botão Fechar [x] sutil somente ao passar o mouse
            if (_isHovered)
            {
                using (Font xf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    Color xc = _isCloseHovered ? Color.FromArgb(255, 100, 100) : Color.FromArgb(130, 135, 160);
                    using (SolidBrush xb = new SolidBrush(xc))
                    {
                        g.DrawString("×", xf, xb, _closeBtnRect.X, _closeBtnRect.Y - 1);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_manager != null)
                {
                    _manager.DevicesUpdated -= OnDevicesUpdated;
                }
            }
            base.Dispose(disposing);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
