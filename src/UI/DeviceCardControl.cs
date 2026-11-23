using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using OmniBattery.Core;
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

            Size = new Size(396, 154);
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
            menu.Renderer = new DarkToolStripRenderer();

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

            addCat(I18n.T("CatMouse"), DeviceCategory.Mouse);
            addCat(I18n.T("CatKeyboard"), DeviceCategory.Teclado);
            addCat(I18n.T("CatHeadphones"), DeviceCategory.FoneDeOuvido);
            addCat(I18n.T("CatGamepad"), DeviceCategory.Controle);
            addCat(I18n.T("CatLaptop"), DeviceCategory.Laptop);
            addCat(I18n.T("CatGeneric"), DeviceCategory.Generico);

            menu.Items.Add(new ToolStripSeparator());

            // Custom image file
            menu.Items.Add(I18n.T("ChooseImageFile"), null, delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = _device.Name;
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
            menu.Items.Add(I18n.T("RestoreAutoIcon"), null, delegate
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

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            Color effectiveColor = _device != null ? _device.GetEffectiveColor() : Color.FromArgb(88, 101, 242);
            bool isConn = _device != null && _device.IsConnected;

            // 1. Halo / Brilho suave exterior ao passar o mouse
            if (_isHovered && isConn)
            {
                Rectangle glowRect = new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);
                using (GraphicsPath gp = RoundedRect(glowRect, 15))
                using (Pen glowPen = new Pen(Color.FromArgb(38, effectiveColor), 2f))
                    g.DrawPath(glowPen, gp);
            }

            // 2. Fundo e Vidro Translúcido Moderno (Deep Obsidian AeroGlass)
            using (GraphicsPath path = RoundedRect(rect, 14))
            {
                Color bgTop = !isConn
                    ? Color.FromArgb(20, 22, 32)
                    : (_isHovered ? Color.FromArgb(32, 36, 54) : Color.FromArgb(24, 27, 42));
                Color bgBot = !isConn
                    ? Color.FromArgb(14, 15, 22)
                    : (_isHovered ? Color.FromArgb(22, 25, 38) : Color.FromArgb(17, 19, 30));

                using (LinearGradientBrush b = new LinearGradientBrush(rect, bgTop, bgBot, 90f))
                    g.FillPath(b, path);

                // Destaque de cristal no topo interno
                Rectangle shineRect = new Rectangle(rect.X + 2, rect.Y + 1, rect.Width - 4, 38);
                using (GraphicsPath sp = RoundedRect(shineRect, 12))
                using (LinearGradientBrush s = new LinearGradientBrush(shineRect, Color.FromArgb(_isHovered ? 38 : 20, 255, 255, 255), Color.FromArgb(2, 255, 255, 255), 90f))
                {
                    g.FillPath(s, sp);
                }

                Color borderColor = !isConn
                    ? (_isHovered ? Color.FromArgb(65, 70, 95) : Color.FromArgb(38, 42, 60))
                    : (_isHovered ? Color.FromArgb(220, effectiveColor) : Color.FromArgb(48, 54, 82));

                using (Pen p = new Pen(borderColor, _isHovered && isConn ? 1.5f : 1.1f))
                    g.DrawPath(p, path);
            }

            if (_device == null) return;

            // ── 3. Ícone em Squircle Moderno (Superior Esquerdo) ────────────────
            int iconSz = 46;
            _iconCircleRect = new Rectangle(16, 16, iconSz, iconSz);

            Color circleColor = isConn ? effectiveColor : Color.FromArgb(100, 105, 130);
            using (GraphicsPath ip = RoundedRect(_iconCircleRect, 12))
            {
                using (SolidBrush ib = new SolidBrush(Color.FromArgb(isConn ? 38 : 20, circleColor)))
                    g.FillPath(ib, ip);
                using (Pen ipen = new Pen(Color.FromArgb(_isIconHovered ? 240 : (isConn ? 150 : 75), circleColor), 1.3f))
                    g.DrawPath(ipen, ip);
            }

            IconRenderer.DrawDeviceIcon(g, _iconCircleRect,
                _device.EffectiveCategory, circleColor, _device.CustomIconPath);

            // ── 4. Botões Superiores Direitos: [● Ícone Ativo] e [✕] ───────────
            int btnY = 16;
            int btnH = 24;
            int btnStatusW = 112;
            _btnStatusRect = new Rectangle(Width - btnStatusW - 36, btnY, btnStatusW, btnH);
            _btnHideRect   = new Rectangle(Width - 26, btnY, 20, btnH);

            bool trayOn = _device.IsTrayIconEnabled;
            string statusLabel = trayOn ? I18n.T("IconOn") : I18n.T("IconOff");
            Color statusColor = trayOn ? Color.FromArgb(0, 230, 118) : Color.FromArgb(140, 145, 170);
            DrawStatusButton(g, _btnStatusRect, statusLabel, _isStatusHovered, statusColor, trayOn);

            // Botão Fechar / Ocultar [✕]
            using (Font xf = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            {
                Color xc = _isHideHovered ? Color.FromArgb(255, 95, 95) : Color.FromArgb(115, 120, 148);
                g.DrawString("✕", xf, new SolidBrush(xc), _btnHideRect.X + 1, _btnHideRect.Y + 2);
            }

            // ── 5. Nome do Dispositivo ──────────────────────────────────────────
            int txtLeft = 72;
            Rectangle nameRect = new Rectangle(txtLeft, 14, Width - txtLeft - btnStatusW - 42, 24);
            using (Font nf = new Font("Segoe UI", 12f, FontStyle.Bold))
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

            // ── 6. Segunda Linha: Chip de Protocolo + Botões [Ícone] e [Cor] ───
            string proto = _device.Protocol;
            int curX = txtLeft;
            using (Font pf = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                SizeF ps = g.MeasureString(proto, pf);
                Rectangle badge = new Rectangle(curX, 42, (int)ps.Width + 12, 20);
                using (GraphicsPath bp = RoundedRect(badge, 6))
                {
                    using (SolidBrush bb = new SolidBrush(isConn ? Color.FromArgb(34, 38, 58) : Color.FromArgb(25, 27, 40)))
                        g.FillPath(bb, bp);
                    using (Pen bpen = new Pen(Color.FromArgb(55, 62, 92), 1f))
                        g.DrawPath(bpen, bp);
                    g.DrawString(proto, pf, new SolidBrush(isConn ? Color.FromArgb(185, 195, 225) : Color.FromArgb(120, 125, 150)), curX + 6, 44);
                }
                curX += (int)ps.Width + 16;
            }

            // Botões rápidos de Ícone e Cor com micro-cápsula moderna
            string iconBtnText = I18n.T("BtnIcon");
            string colorBtnText = I18n.T("BtnColor");
            _btnIconRect  = new Rectangle(curX, 42, 50, 20);
            _btnColorRect = new Rectangle(curX + 54, 42, 50, 20);
            DrawSmallButton(g, _btnIconRect,  iconBtnText,  _isIconHovered,  effectiveColor, false);
            DrawSmallButton(g, _btnColorRect, colorBtnText, _isColorHovered, effectiveColor, true);

            // ── 7. Porcentagem da Bateria & Barra de Energia / Aviso com Fio ────
            if (!_device.IsWired)
            {
                string pct = _device.GetFormattedPercentage();
                if (!string.IsNullOrEmpty(pct))
                {
                    using (Font pctF = new Font("Segoe UI", isConn ? 19f : 15f, FontStyle.Bold))
                    {
                        SizeF pctSz = g.MeasureString(pct, pctF);
                        float pctX = Width - pctSz.Width - 16;
                        float pctY = isConn ? 68f : 71f;
                        g.DrawString(pct, pctF, new SolidBrush(effectiveColor), pctX, pctY);
                    }
                }

                // Tubo / Barra de Bateria AeroGlass de Alta Resolução
                int barX = 16, barY = 88;
                int barW = Width - 124, barH = 11;
                Rectangle barBg = new Rectangle(barX, barY, barW, barH);

                using (GraphicsPath bgP = RoundedRect(barBg, 5))
                using (SolidBrush bgB = new SolidBrush(Color.FromArgb(14, 16, 25)))
                {
                    g.FillPath(bgB, bgP);
                    using (Pen tpen = new Pen(Color.FromArgb(38, 42, 64), 1f))
                        g.DrawPath(tpen, bgP);
                }

                if (isConn && _device.BatteryPercent > 0)
                {
                    int fill = Math.Max(barH, (int)(barW * (Math.Min(100, _device.BatteryPercent) / 100.0)));
                    Rectangle barFill = new Rectangle(barX, barY, fill, barH);
                    using (GraphicsPath fp = RoundedRect(barFill, 5))
                    {
                        Color c1 = effectiveColor;
                        Color c2 = Color.FromArgb(Math.Max(0, effectiveColor.R - 35), Math.Max(0, effectiveColor.G - 35), Math.Max(0, effectiveColor.B - 20));
                        using (LinearGradientBrush fb = new LinearGradientBrush(barFill, c1, c2, 0f))
                            g.FillPath(fb, fp);

                        // Linha de reflexo especular de cristal no topo da barra
                        Rectangle barShine = new Rectangle(barX, barY, fill, 4);
                        using (LinearGradientBrush sb = new LinearGradientBrush(barShine, Color.FromArgb(130, 255, 255, 255), Color.FromArgb(10, 255, 255, 255), 90f))
                        {
                            g.FillRectangle(sb, barShine);
                        }
                    }
                }
            }
            else
            {
                // Dispositivo com fio: Linha de aviso moderna em ciano elétrico
                int barX = 16, barY = 86;
                using (Font wf = new Font("Segoe UI", 8.8f, FontStyle.Bold))
                {
                    g.DrawString("⚡ " + I18n.T("WiredDeviceNotice"), wf, new SolidBrush(Color.FromArgb(100, 195, 255)), barX, barY);
                }
            }

            // ── 8. Informações do Rodapé do Card ──────────────────────────────
            string info;
            if (!isConn)
            {
                info = I18n.T("DisconnectedStatus");
            }
            else
            {
                string sub;
                if (_device.IsWired)
                    sub = I18n.T("WiredDeviceSubtitle");
                else if (string.IsNullOrEmpty(_device.Subtitle) || _device.Subtitle == "Sem fio" || _device.Subtitle == "Sem Fio")
                    sub = I18n.T("WirelessDeviceSubtitle");
                else
                    sub = _device.Subtitle;

                string updatedText = I18n.T("UpdatedAgo", _device.LastUpdated);
                info = _device.Voltage > 0
                    ? string.Format("{0}  •  {1}  •  {2}", I18n.T("VoltageLabel", _device.Voltage), sub, updatedText)
                    : string.Format("{0}  •  {1}", sub, updatedText);
            }

            using (Font inf = new Font("Segoe UI", 7.8f))
            {
                Color infoColor = isConn ? Color.FromArgb(135, 142, 172) : Color.FromArgb(100, 105, 130);
                g.DrawString(info, inf, new SolidBrush(infoColor), 16, 118);
            }
        }

        private void DrawSmallButton(Graphics g, Rectangle r, string label, bool hovered, Color accent, bool showDot)
        {
            using (GraphicsPath p = RoundedRect(r, 6))
            {
                Color bgTop = hovered ? Color.FromArgb(60, 68, 102) : Color.FromArgb(36, 42, 66);
                Color bgBottom = hovered ? Color.FromArgb(46, 52, 80) : Color.FromArgb(28, 32, 52);
                using (LinearGradientBrush b = new LinearGradientBrush(r, bgTop, bgBottom, 90f))
                    g.FillPath(b, p);

                using (Pen pen = new Pen(Color.FromArgb(hovered ? 130 : 55, 255, 255, 255), 1f))
                    g.DrawPath(pen, p);
            }

            if (showDot)
            {
                using (SolidBrush db = new SolidBrush(accent))
                    g.FillEllipse(db, r.X + 6, r.Y + 6, 8, 8);

                using (Font bf = new Font("Segoe UI", 7.2f, FontStyle.Bold))
                    g.DrawString(label, bf, Brushes.White, r.X + 17, r.Y + 3);
            }
            else
            {
                using (Font bf = new Font("Segoe UI", 7.2f, FontStyle.Bold))
                    g.DrawString(label, bf, Brushes.White, r.X + 8, r.Y + 3);
            }
        }

        private void DrawStatusButton(Graphics g, Rectangle r, string label, bool hovered, Color statusColor, bool isConnected)
        {
            using (GraphicsPath p = RoundedRect(r, 7))
            {
                Color bgTop = isConnected
                    ? (hovered ? Color.FromArgb(34, 75, 52) : Color.FromArgb(24, 55, 38))
                    : (hovered ? Color.FromArgb(55, 60, 80) : Color.FromArgb(38, 42, 60));
                Color bgBottom = isConnected
                    ? (hovered ? Color.FromArgb(22, 50, 34) : Color.FromArgb(16, 38, 26))
                    : (hovered ? Color.FromArgb(42, 46, 64) : Color.FromArgb(28, 31, 46));

                using (LinearGradientBrush b = new LinearGradientBrush(r, bgTop, bgBottom, 90f))
                    g.FillPath(b, p);

                Color borderC = isConnected
                    ? Color.FromArgb(hovered ? 240 : 160, 0, 230, 118)
                    : Color.FromArgb(hovered ? 150 : 80, 255, 255, 255);
                using (Pen pen = new Pen(borderC, 1f))
                    g.DrawPath(pen, p);
            }

            // LED de status
            if (isConnected)
            {
                using (SolidBrush dotB = new SolidBrush(Color.FromArgb(0, 230, 118)))
                    g.FillEllipse(dotB, r.X + 8, r.Y + 8, 8, 8);
            }

            using (Font f = new Font("Segoe UI", 7.6f, FontStyle.Bold))
            {
                int textX = isConnected ? r.X + 21 : r.X + 8;
                using (SolidBrush tb = new SolidBrush(statusColor))
                {
                    g.DrawString(label, f, tb, textX, r.Y + 4);
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
    }
}
