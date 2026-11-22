using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class DeviceCardControl : UserControl
    {
        private BatteryDevice _device;
        private bool _isHovered = false;

        private Rectangle _iconCircleRect;
        private Rectangle _btnStatusRect;
        private Rectangle _btnColorRect;
        private Rectangle _btnIconRect;
        private Rectangle _btnHideRect;

        private bool _isStatusHovered = false;
        private bool _isColorHovered = false;
        private bool _isIconHovered = false;
        private bool _isHideHovered = false;

        // customPath = null means category change; "" means reset; path = file chosen
        public event Action<BatteryDevice> RequestColorChange;
        public event Action<BatteryDevice, string> RequestIconChange;
        public event Action<BatteryDevice> RequestHideDevice;
        public event Action<BatteryDevice, bool> RequestTrayToggle;

        public BatteryDevice Device { get { return _device; } }

        public DeviceCardControl(BatteryDevice device)
        {
            _device = device;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(390, 145);
            Margin = new Padding(10);
            Cursor = Cursors.Default;

            // Forward mouse wheel to parent so the scroll panel catches it
            MouseWheel += (s, e) =>
            {
                Control p = Parent;
                while (p != null && !(p is ScrollableControl)) p = p.Parent;
                if (p is ScrollableControl)
                {
                    ScrollableControl sc = (ScrollableControl)p;
                    int delta = SystemInformation.MouseWheelScrollLines * 20;
                    int newVal = sc.VerticalScroll.Value - (e.Delta > 0 ? delta : -delta);
                    newVal = Math.Max(sc.VerticalScroll.Minimum,
                             Math.Min(sc.VerticalScroll.Maximum - sc.VerticalScroll.LargeChange + 1, newVal));
                    sc.VerticalScroll.Value = newVal;
                    sc.PerformLayout();
                }
            };
        }

        public void UpdateDevice(BatteryDevice device)
        {
            _device = device;
            Invalidate();
        }

        // ─── Mouse interaction ────────────────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool sh = _btnStatusRect.Contains(e.Location);
            bool ch = _btnColorRect.Contains(e.Location);
            bool ih = _btnIconRect.Contains(e.Location) || _iconCircleRect.Contains(e.Location);
            bool hh = _btnHideRect.Contains(e.Location);

            if (sh != _isStatusHovered || ch != _isColorHovered || ih != _isIconHovered || hh != _isHideHovered)
            {
                _isStatusHovered = sh; _isColorHovered = ch; _isIconHovered = ih; _isHideHovered = hh;
                Cursor = (sh || ch || ih || hh) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_btnStatusRect.Contains(e.Location))
                {
                    bool newEnabled = !_device.IsTrayIconEnabled;
                    _device.IsTrayIconEnabled = newEnabled;
                    Invalidate();
                    if (RequestTrayToggle != null) RequestTrayToggle(_device, newEnabled);
                }
                else if (_btnColorRect.Contains(e.Location))
                {
                    if (RequestColorChange != null) RequestColorChange(_device);
                }
                else if (_btnIconRect.Contains(e.Location) || _iconCircleRect.Contains(e.Location))
                {
                    ShowIconMenu();
                }
                else if (_btnHideRect.Contains(e.Location))
                {
                    // Desligar dispositivo: remove dos conectados e move para o histórico
                    if (RequestHideDevice != null) RequestHideDevice(_device);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = _isStatusHovered = _isColorHovered = _isIconHovered = _isHideHovered = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        // ─── Icon Menu ────────────────────────────────────────────────────────
        private void ShowIconMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

            ToolStripMenuItem header = new ToolStripMenuItem("Escolher Icone para " + _device.Name);
            header.Font = new Font(header.Font, FontStyle.Bold);
            header.Enabled = false;
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());

            // Category options with checkmark on current
            Action<string, DeviceCategory> addCat = (label, cat) =>
            {
                ToolStripMenuItem item = new ToolStripMenuItem(label);
                item.Checked = (_device.CustomCategory.HasValue && _device.CustomCategory.Value == cat);
                DeviceCategory c = cat;
                item.Click += delegate { SetCategory(c); };
                menu.Items.Add(item);
            };

            addCat("Mouse", DeviceCategory.Mouse);
            addCat("Teclado", DeviceCategory.Teclado);
            addCat("Fone de Ouvido / Headset", DeviceCategory.FoneDeOuvido);
            addCat("Controle / Gamepad", DeviceCategory.Controle);
            addCat("Laptop / Notebook", DeviceCategory.Laptop);
            addCat("Bateria / Generico", DeviceCategory.Generico);

            menu.Items.Add(new ToolStripSeparator());

            // Custom image file
            menu.Items.Add("Escolher Imagem (.png, .ico, .jpg, .gif)...", null, delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Icone personalizado para " + _device.Name;
                    ofd.Filter = "Imagens (*.png;*.ico;*.jpg;*.jpeg;*.gif)|*.png;*.ico;*.jpg;*.jpeg;*.gif";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _device.CustomIconPath = ofd.FileName;
                        _device.CustomCategory = null;
                        if (RequestIconChange != null)
                            RequestIconChange(_device, ofd.FileName);
                        Invalidate();
                    }
                }
            });

            // Reset to auto-detected icon
            menu.Items.Add("Restaurar Icone Automatico", null, delegate
            {
                _device.CustomIconPath = null;
                _device.CustomCategory = null;
                if (RequestIconChange != null)
                    RequestIconChange(null, null);  // null dev = reset
                Invalidate();
            });

            int menuX = _btnIconRect.Left;
            int menuY = _btnIconRect.Bottom + 4;
            menu.Show(this, new Point(menuX, menuY));
        }

        private void SetCategory(DeviceCategory cat)
        {
            _device.CustomCategory = cat;
            _device.CustomIconPath = null;
            if (RequestIconChange != null) RequestIconChange(_device, null);
            Invalidate();
        }

        // ─── Paint ────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(1, 1, Width - 2, Height - 2);
            Color effectiveColor = _device != null ? _device.GetEffectiveColor() : Color.FromArgb(88, 101, 242);
            bool isConn = _device != null && _device.IsConnected;
            Color bgColor = !isConn ? Color.FromArgb(20, 22, 33) : (_isHovered ? Color.FromArgb(37, 40, 58) : Color.FromArgb(28, 31, 46));
            Color borderColor = !isConn ? (_isHovered ? Color.FromArgb(60, 65, 90) : Color.FromArgb(35, 38, 54)) : (_isHovered ? effectiveColor : Color.FromArgb(48, 52, 76));

            // ── Card background with rounded corners ─────────────────────────
            using (GraphicsPath path = RoundedRect(rect, 10))
            {
                using (SolidBrush b = new SolidBrush(bgColor)) g.FillPath(b, path);
                using (Pen p = new Pen(borderColor, _isHovered ? 1.8f : 1.2f)) g.DrawPath(p, path);
            }

            if (_device == null) return;

            // ── Icon circle (top-left) ───────────────────────────────────────
            int iconSz = 44;
            _iconCircleRect = new Rectangle(14, 14, iconSz, iconSz);

            Color circleColor = isConn ? effectiveColor : Color.FromArgb(100, 105, 130);
            using (SolidBrush ib = new SolidBrush(Color.FromArgb(isConn ? 40 : 20, circleColor)))
                g.FillEllipse(ib, _iconCircleRect);
            using (Pen ip = new Pen(Color.FromArgb(_isIconHovered ? 220 : (isConn ? 140 : 80), circleColor), 1.5f))
                g.DrawEllipse(ip, _iconCircleRect);

            IconRenderer.DrawDeviceIcon(g, _iconCircleRect,
                _device.EffectiveCategory, circleColor, _device.CustomIconPath);

            // ── Top-right: [● Ícone Ligado] and [x] ───────────────────────────
            int btnY = 14;
            int btnH = 22;
            int btnStatusW = 108;
            _btnStatusRect = new Rectangle(Width - btnStatusW - 30, btnY, btnStatusW, btnH);
            _btnHideRect   = new Rectangle(Width - 24,  btnY, 18, btnH);

            bool trayOn = _device.IsTrayIconEnabled;
            string statusLabel = trayOn ? "● Ícone Ligado" : "○ Ícone Desligado";
            Color statusColor = trayOn ? Color.FromArgb(46, 204, 113) : Color.FromArgb(140, 145, 170);
            DrawStatusButton(g, _btnStatusRect, statusLabel, _isStatusHovered, statusColor, trayOn);

            // X button (Desligar / ocultar dispositivo da tela)
            using (Font xf = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                Color xc = _isHideHovered ? Color.FromArgb(255, 100, 100) : Color.FromArgb(100, 105, 135);
                g.DrawString("x", xf, new SolidBrush(xc), _btnHideRect.X + 2, _btnHideRect.Y + 1);
            }

            // ── Device name (full width available) ───────────────────────────
            int txtLeft = 66;
            Rectangle nameRect = new Rectangle(txtLeft, 13, Width - txtLeft - btnStatusW - 36, 22);
            using (Font nf = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                Color nameColor = isConn ? Color.White : Color.FromArgb(150, 155, 175);
                using (SolidBrush nb = new SolidBrush(nameColor))
                    g.DrawString(_device.Name, nf, nb, nameRect, sf);
            }

            // ── Second row: Protocol badge + [Icone] + [Cor] ─────────────────
            string proto = _device.Protocol;
            int curX = txtLeft;
            using (Font pf = new Font("Segoe UI", 7.5f))
            {
                SizeF ps = g.MeasureString(proto, pf);
                Rectangle badge = new Rectangle(curX, 39, (int)ps.Width + 10, 19);
                using (GraphicsPath bp = RoundedRect(badge, 4))
                using (SolidBrush bb = new SolidBrush(isConn ? Color.FromArgb(42, 45, 68) : Color.FromArgb(30, 32, 48)))
                    g.FillPath(bb, bp);
                g.DrawString(proto, pf, new SolidBrush(isConn ? Color.FromArgb(180, 185, 210) : Color.FromArgb(120, 125, 150)), curX + 5, 41);
                curX += (int)ps.Width + 14;
            }

            // [Icone] and [Cor] buttons on second row
            _btnIconRect  = new Rectangle(curX, 39, 44, 19);
            _btnColorRect = new Rectangle(curX + 48, 39, 42, 19);
            DrawSmallButton(g, _btnIconRect,  "Icone", _isIconHovered,  effectiveColor, false);
            DrawSmallButton(g, _btnColorRect, "Cor",   _isColorHovered, effectiveColor, true);

            // ── Percentage / Status ──────────────────────────────────────────
            string pct = _device.GetFormattedPercentage();
            using (Font pctF = new Font("Segoe UI", isConn ? 17f : 14f, FontStyle.Bold))
            {
                SizeF pctSz = g.MeasureString(pct, pctF);
                float pctX = Width - pctSz.Width - 14;
                float pctY = isConn ? 64f : 67f;
                g.DrawString(pct, pctF, new SolidBrush(effectiveColor), pctX, pctY);
            }

            // ── Battery bar ──────────────────────────────────────────────────
            int barX = 14, barY = 82;
            int barW = Width - 120, barH = 10;
            Rectangle barBg = new Rectangle(barX, barY, barW, barH);

            using (GraphicsPath bgP = RoundedRect(barBg, 5))
            using (SolidBrush bgB = new SolidBrush(isConn ? Color.FromArgb(45, 49, 72) : Color.FromArgb(30, 32, 48)))
                g.FillPath(bgB, bgP);

            if (isConn && _device.BatteryPercent > 0)
            {
                int fill = Math.Max(barH, (int)(barW * (Math.Min(100, _device.BatteryPercent) / 100.0)));
                Rectangle barFill = new Rectangle(barX, barY, fill, barH);
                using (GraphicsPath fp = RoundedRect(barFill, 5))
                using (SolidBrush fb = new SolidBrush(effectiveColor))
                    g.FillPath(fb, fp);
            }

            // ── Bottom info ──────────────────────────────────────────────────
            string info;
            if (!isConn)
            {
                info = "Dispositivo desligado  •  Oculto da barra de tarefas";
            }
            else
            {
                info = _device.Voltage > 0
                    ? string.Format("Tensao: {0:F2}V  •  {1}  •  {2:HH:mm:ss}", _device.Voltage, _device.Subtitle, _device.LastUpdated)
                    : string.Format("{0}  •  Atualizado as {1:HH:mm:ss}", _device.Subtitle, _device.LastUpdated);
            }

            using (Font inf = new Font("Segoe UI", 7.5f))
            {
                Color infoColor = isConn ? Color.FromArgb(130, 135, 165) : Color.FromArgb(100, 105, 130);
                g.DrawString(info, inf, new SolidBrush(infoColor), 14, 104);
            }
        }

        private void DrawSmallButton(Graphics g, Rectangle r, string label, bool hovered, Color accent, bool showDot)
        {
            using (GraphicsPath p = RoundedRect(r, 4))
            {
                Color bg = hovered ? Color.FromArgb(60, 65, 100) : Color.FromArgb(38, 42, 65);
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(80, 85, 120), 1f)) g.DrawPath(pen, p);
            }

            if (showDot)
            {
                using (SolidBrush db = new SolidBrush(accent))
                    g.FillEllipse(db, r.X + 5, r.Y + 5, 8, 8);

                using (Font bf = new Font("Segoe UI", 7.2f))
                    g.DrawString(label, bf, Brushes.White, r.X + 16, r.Y + 3);
            }
            else
            {
                using (Font bf = new Font("Segoe UI", 7.2f))
                    g.DrawString(label, bf, Brushes.White, r.X + 7, r.Y + 3);
            }
        }

        private void DrawStatusButton(Graphics g, Rectangle r, string label, bool hovered, Color statusColor, bool isConnected)
        {
            using (GraphicsPath p = RoundedRect(r, 4))
            {
                Color bg = isConnected
                    ? (hovered ? Color.FromArgb(30, 60, 45) : Color.FromArgb(22, 42, 35))
                    : (hovered ? Color.FromArgb(50, 52, 68) : Color.FromArgb(35, 38, 52));
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                Color borderC = isConnected ? Color.FromArgb(46, 204, 113) : Color.FromArgb(90, 95, 125);
                using (Pen pen = new Pen(hovered ? Color.White : borderC, 1f)) g.DrawPath(pen, p);
            }

            using (Font f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using (SolidBrush tb = new SolidBrush(statusColor))
                {
                    g.DrawString(label, f, tb, r, sf);
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Color.FromArgb(45, 49, 72); } }
            public override Color MenuBorder { get { return Color.FromArgb(50, 54, 80); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(88, 101, 242); } }
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(28, 31, 46); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(28, 31, 46); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(28, 31, 46); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(28, 31, 46); } }
            public override Color SeparatorDark { get { return Color.FromArgb(45, 49, 72); } }
        }
    }
}
