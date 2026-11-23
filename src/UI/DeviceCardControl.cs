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

            Size = new Size(300, 146);
            Margin = new Padding(6);
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

            Rectangle cardRect = new Rectangle(5, 5, Width - 10, Height - 10);
            Color effectiveColor = _device != null ? _device.GetEffectiveColor() : Color.FromArgb(88, 101, 242);
            bool isConn = _device != null && _device.IsConnected;

            // 1. Halo / Brilho Neon Atmosférico Radiante (mesmo estilo da imagem de referência)
            if (isConn)
            {
                int red = effectiveColor.R;
                int green = effectiveColor.G;
                int blue = effectiveColor.B;
                int baseAlpha = _isHovered ? 12 : 7;

                for (int i = 5; i >= 1; i--)
                {
                    Rectangle glowR = new Rectangle(cardRect.X - i, cardRect.Y - i, cardRect.Width + (i * 2), cardRect.Height + (i * 2));
                    using (GraphicsPath gp = RoundedRect(glowR, 16 + i))
                    using (Pen gpPen = new Pen(Color.FromArgb(baseAlpha * (6 - i), red, green, blue), 2f))
                    {
                        g.DrawPath(gpPen, gp);
                    }
                }
            }

            // 2. Fundo Translúcido Vítreo e Borda Neon com Brilho
            using (GraphicsPath path = RoundedRect(cardRect, 16))
            {
                Color bgTop = !isConn ? Color.FromArgb(20, 22, 32) : Color.FromArgb(220, 22, 26, 40);
                Color bgBot = !isConn ? Color.FromArgb(14, 15, 22) : Color.FromArgb(240, 14, 16, 26);

                using (LinearGradientBrush b = new LinearGradientBrush(cardRect, bgTop, bgBot, 90f))
                    g.FillPath(b, path);

                Color borderColor = !isConn
                    ? (_isHovered ? Color.FromArgb(70, 75, 100) : Color.FromArgb(42, 46, 68))
                    : Color.FromArgb(_isHovered ? 240 : 170, effectiveColor);

                using (Pen p = new Pen(borderColor, 1.2f))
                    g.DrawPath(p, path);
            }

            if (_device == null) return;

            // ── 3. Ícone em Squircle Neon (Superior Esquerdo) ─────────────────
            int iconSz = 42;
            _iconCircleRect = new Rectangle(cardRect.X + 14, cardRect.Y + 14, iconSz, iconSz);

            Color circleColor = isConn ? effectiveColor : Color.FromArgb(100, 105, 130);
            using (GraphicsPath ip = RoundedRect(_iconCircleRect, 10))
            {
                using (SolidBrush ib = new SolidBrush(Color.FromArgb(isConn ? 28 : 16, circleColor)))
                    g.FillPath(ib, ip);
                using (Pen ipen = new Pen(Color.FromArgb(_isIconHovered ? 240 : (isConn ? 160 : 60), circleColor), 1.2f))
                    g.DrawPath(ipen, ip);
            }

            IconRenderer.DrawDeviceIcon(g, _iconCircleRect,
                _device.EffectiveCategory, circleColor, _device.CustomIconPath);

            // ── 4. Botões Superiores Direitos: [● Ícone Ligado] e [✕] ───────────
            int btnY = cardRect.Y + 13;
            int btnH = 20;
            int btnStatusW = 88;
            _btnStatusRect = new Rectangle(cardRect.Right - btnStatusW - 22, btnY, btnStatusW, btnH);
            _btnHideRect   = new Rectangle(cardRect.Right - 18, btnY, 16, btnH);

            bool trayOn = _device.IsTrayIconEnabled;
            string statusLabel = trayOn ? I18n.T("IconOn") : I18n.T("IconOff");
            Color statusColor = trayOn ? Color.FromArgb(0, 230, 118) : Color.FromArgb(140, 145, 170);
            DrawStatusButton(g, _btnStatusRect, statusLabel, _isStatusHovered, statusColor, trayOn);

            // Botão Fechar / Ocultar [✕]
            using (Font xf = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                Color xc = _isHideHovered ? Color.FromArgb(255, 95, 95) : Color.FromArgb(110, 115, 140);
                g.DrawString("✕", xf, new SolidBrush(xc), _btnHideRect.X + 1, _btnHideRect.Y + 1);
            }

            // ── 5. Nome do Dispositivo ──────────────────────────────────────────
            int txtLeft = cardRect.X + 64;
            Rectangle nameRect = new Rectangle(txtLeft, cardRect.Y + 12, cardRect.Width - 64 - btnStatusW - 28, 22);
            using (Font nf = new Font("Segoe UI", 11.2f, FontStyle.Bold))
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
            int row2Y = cardRect.Y + 38;
            int curX = txtLeft;
            string proto = _device.Protocol;
            using (Font pf = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            {
                SizeF ps = g.MeasureString(proto, pf);
                int badgeW = Math.Min(115, (int)ps.Width + 10);
                Rectangle badge = new Rectangle(curX, row2Y, badgeW, 19);
                using (GraphicsPath bp = RoundedRect(badge, 5))
                {
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(28, 32, 48)))
                        g.FillPath(bb, bp);
                    using (Pen bpen = new Pen(Color.FromArgb(50, 58, 86), 1f))
                        g.DrawPath(bpen, bp);

                    StringFormat bsf = new StringFormat
                    {
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    Rectangle textR = new Rectangle(badge.X + 5, badge.Y + 3, badge.Width - 8, badge.Height - 4);
                    g.DrawString(proto, pf, new SolidBrush(Color.FromArgb(170, 180, 210)), textR, bsf);
                }
                curX += badgeW + 6;
            }

            // Botões rápidos de Ícone e Cor com micro-cápsula moderna
            string iconBtnText = I18n.T("BtnIcon");
            string colorBtnText = I18n.T("BtnColor");
            using (Font bf = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            {
                _btnIconRect  = new Rectangle(curX, row2Y, 44, 19);
                _btnColorRect = new Rectangle(_btnIconRect.Right + 6, row2Y, 46, 19);
            }
            DrawSmallButton(g, _btnIconRect,  iconBtnText,  _isIconHovered,  effectiveColor, false);
            DrawSmallButton(g, _btnColorRect, colorBtnText, _isColorHovered, effectiveColor, true);

            // ── 7. Tubo de Laser / Barra Neon Brilhante & Porcentagem ──────────
            if (!_device.IsWired)
            {
                int barX = cardRect.X + 14;
                int barY = cardRect.Y + 84;
                int barW = cardRect.Width - 14 - 84;
                int barH = 11;

                // Canaleta escura de fundo
                Rectangle barTrack = new Rectangle(barX, barY, barW, barH);
                using (GraphicsPath btp = RoundedRect(barTrack, 5))
                {
                    using (SolidBrush btb = new SolidBrush(Color.FromArgb(14, 16, 26))) g.FillPath(btb, btp);
                    using (Pen btpen = new Pen(Color.FromArgb(34, 40, 60), 1f)) g.DrawPath(btpen, btp);
                }

                if (isConn && _device.BatteryPercent > 0)
                {
                    int fillW = Math.Max(barH, (int)(barW * (Math.Min(100, _device.BatteryPercent) / 100.0)));
                    Rectangle barFill = new Rectangle(barX, barY, fillW, barH);

                    // Halo neon difuso brilhante ao redor do tubo preenchido
                    int red = effectiveColor.R;
                    int green = effectiveColor.G;
                    int blue = effectiveColor.B;
                    for (int i = 4; i >= 1; i--)
                    {
                        Rectangle tg = new Rectangle(barX - i, barY - i, fillW + (i * 2), barH + (i * 2));
                        using (GraphicsPath tgp = RoundedRect(tg, (barH + (i * 2)) / 2))
                        using (SolidBrush tgb = new SolidBrush(Color.FromArgb(22 * (5 - i), red, green, blue)))
                        {
                            g.FillPath(tgb, tgp);
                        }
                    }

                    // Núcleo do tubo neon radiante
                    using (GraphicsPath fp = RoundedRect(barFill, 5))
                    {
                        using (SolidBrush fb = new SolidBrush(effectiveColor))
                            g.FillPath(fb, fp);

                        // Linha de reflexo especular de laser no topo do tubo
                        Rectangle coreRect = new Rectangle(barX + 2, barY + 2, fillW - 4, 3);
                        if (coreRect.Width > 4)
                        {
                            using (GraphicsPath coreP = RoundedRect(coreRect, 1))
                            using (SolidBrush coreB = new SolidBrush(Color.FromArgb(225, 255, 255, 255)))
                            {
                                g.FillPath(coreB, coreP);
                            }
                        }
                    }
                }

                // Porcentagem da bateria em 20pt Bold na cor do dispositivo
                string pct = _device.GetFormattedPercentage();
                if (!string.IsNullOrEmpty(pct))
                {
                    using (Font pctFont = new Font("Segoe UI", 20f, FontStyle.Bold))
                    {
                        g.DrawString(pct, pctFont, new SolidBrush(isConn ? effectiveColor : Color.FromArgb(120, 125, 145)), cardRect.Right - 76, cardRect.Y + 68);
                    }
                }
            }
            else
            {
                // Dispositivo com fio: Linha de aviso moderna em ciano elétrico
                int barX = cardRect.X + 14, barY = cardRect.Y + 84;
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

            using (Font inf = new Font("Segoe UI", 7.6f))
            {
                Color infoColor = isConn ? Color.FromArgb(125, 132, 155) : Color.FromArgb(90, 95, 115);
                g.DrawString(info, inf, new SolidBrush(infoColor), cardRect.X + 14, cardRect.Y + 112);
            }
        }

        private void DrawSmallButton(Graphics g, Rectangle r, string label, bool hovered, Color accent, bool showDot)
        {
            using (GraphicsPath p = RoundedRect(r, 6))
            {
                Color bgTop = hovered ? Color.FromArgb(50, 58, 88) : Color.FromArgb(28, 34, 52);
                Color bgBottom = hovered ? Color.FromArgb(38, 44, 68) : Color.FromArgb(20, 24, 38);
                using (LinearGradientBrush b = new LinearGradientBrush(r, bgTop, bgBottom, 90f))
                    g.FillPath(b, p);

                using (Pen pen = new Pen(Color.FromArgb(hovered ? 120 : 45, 255, 255, 255), 1f))
                    g.DrawPath(pen, p);
            }

            using (Font bf = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            {
                if (showDot)
                {
                    using (SolidBrush db = new SolidBrush(accent))
                        g.FillEllipse(db, r.X + 6, r.Y + 6, 7, 7);

                    g.DrawString(label, bf, Brushes.White, r.X + 17, r.Y + 3);
                }
                else
                {
                    SizeF s = g.MeasureString(label, bf);
                    float tx = r.X + (r.Width - s.Width) / 2f;
                    g.DrawString(label, bf, Brushes.White, tx, r.Y + 3);
                }
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

            // Remove quaisquer marcadores existentes na string para evitar bola verde duplicada
            string cleanLabel = label.TrimStart('●', '○', ' ').Trim();

            // Apenas UMA bola LED limpa indicadora
            if (isConnected)
            {
                using (SolidBrush dotB = new SolidBrush(Color.FromArgb(0, 230, 118)))
                    g.FillEllipse(dotB, r.X + 8, r.Y + 8, 7, 7);
            }
            else
            {
                using (SolidBrush dotB = new SolidBrush(Color.FromArgb(140, 145, 170)))
                    g.FillEllipse(dotB, r.X + 8, r.Y + 8, 7, 7);
            }

            using (Font f = new Font("Segoe UI", 7.6f, FontStyle.Bold))
            {
                int textX = r.X + 20;
                using (SolidBrush tb = new SolidBrush(statusColor))
                {
                    g.DrawString(cleanLabel, f, tb, textX, r.Y + 4);
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
