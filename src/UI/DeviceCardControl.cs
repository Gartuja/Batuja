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
            g.SmoothingMode = SmoothingMode.None; // Pixel-sharp retro aesthetic
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color cGrey = Color.FromArgb(192, 192, 192);
            Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color effectiveColor = _device != null ? _device.GetEffectiveColor() : Color.FromArgb(0, 240, 255);
            bool isConn = _device != null && _device.IsConnected;

            // 1. Fundo cinza clássico e Borda 3D em relevo (Raised Bevel)
            using (SolidBrush b = new SolidBrush(cGrey))
                g.FillRectangle(b, cardRect);
            Draw3DRaised(g, cardRect, 2);

            if (_device == null) return;

            // ── 2. Topo Direito: Botão Fechar [x] e Status [Ícone Ligado] ─────
            _btnHideRect = new Rectangle(cardRect.Right - 16, cardRect.Top + 6, 12, 12);
            DrawClassicButton(g, _btnHideRect, "x", _isHideHovered, false);

            int btnStatusW = 78;
            int btnStatusH = 16;
            _btnStatusRect = new Rectangle(_btnHideRect.Left - btnStatusW - 4, cardRect.Top + 6, btnStatusW, btnStatusH);

            bool trayOn = _device.IsTrayIconEnabled;
            string statusLabel = trayOn ? I18n.T("IconOn") : I18n.T("IconOff");
            string cleanLabel = statusLabel.TrimStart('●', '○', ' ').Trim();

            // Pílula / Caixa verde clássica com contorno preto fino
            Color stBg = trayOn ? Color.FromArgb(34, 197, 94) : Color.FromArgb(180, 180, 180);
            using (SolidBrush sb = new SolidBrush(stBg))
                g.FillRectangle(sb, _btnStatusRect);
            using (Pen sp = new Pen(Color.Black, 1f))
                g.DrawRectangle(sp, _btnStatusRect);

            using (Font stFont = new Font("Tahoma", 7.5f, FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(Color.Black))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(cleanLabel, stFont, tb, _btnStatusRect, sf);
            }

            // ── 3. Ícone em Caixa Rebaixada (Sunken 3D Box) ────────────────────
            int iconSz = 40;
            _iconCircleRect = new Rectangle(cardRect.X + 8, cardRect.Y + 8, iconSz, iconSz);
            using (SolidBrush ib = new SolidBrush(cGrey))
                g.FillRectangle(ib, _iconCircleRect);
            Draw3DSunken(g, _iconCircleRect);

            IconRenderer.DrawDeviceIcon(g, _iconCircleRect,
                _device.EffectiveCategory, Color.Black, _device.CustomIconPath);

            // ── 4. Nome do Dispositivo ──────────────────────────────────────────
            int txtLeft = cardRect.X + 54;
            Rectangle nameRect = new Rectangle(txtLeft, cardRect.Y + 7, _btnStatusRect.Left - txtLeft - 4, 18);
            using (Font nf = new Font("Tahoma", 10f, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                Color nameColor = isConn ? Color.Black : Color.FromArgb(100, 100, 100);
                using (SolidBrush nb = new SolidBrush(nameColor))
                    g.DrawString(_device.Name, nf, nb, nameRect, sf);
            }

            // ── 5. Segunda Linha: Protocolo e Botões [Ícone] e [Cor] ───────────
            int row2Y = cardRect.Y + 26;
            string proto = _device.Protocol;
            using (Font pf = new Font("Tahoma", 7.5f, FontStyle.Regular))
            {
                Rectangle protoRect = new Rectangle(txtLeft, row2Y + 2, 115, 16);
                StringFormat psf = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(proto, pf, Brushes.Black, protoRect, psf);
            }

            // Botões clássicos 3D de Ícone e Cor
            int bLeft = cardRect.Left + 175;
            string iconBtnText = I18n.T("BtnIcon");
            string colorBtnText = I18n.T("BtnColor");

            _btnIconRect  = new Rectangle(bLeft, row2Y, 44, 18);
            _btnColorRect = new Rectangle(_btnIconRect.Right + 5, row2Y, 46, 18);

            DrawClassicButton(g, _btnIconRect,  iconBtnText,  _isIconHovered,  false);
            DrawClassicButton(g, _btnColorRect, " " + colorBtnText, _isColorHovered, false);
            // Bolinha colorida dentro do botão de cor
            using (SolidBrush cb = new SolidBrush(effectiveColor))
            {
                g.FillEllipse(cb, _btnColorRect.X + 6, _btnColorRect.Y + 5, 7, 7);
                g.DrawEllipse(Pens.Black, _btnColorRect.X + 6, _btnColorRect.Y + 5, 7, 7);
            }

            // ── 6. Barra de Bateria Estilo Medidor de LED / VU-Meter Retro ─────
            int barX = cardRect.X + 8;
            int barY = cardRect.Y + 56;
            int barW = cardRect.Width - 92;
            int barH = 25;
            Rectangle gaugeRect = new Rectangle(barX, barY, barW, barH);

            // Canaleta preta rebaixada clássica
            using (SolidBrush b = new SolidBrush(Color.Black))
                g.FillRectangle(b, gaugeRect);
            Draw3DSunken(g, gaugeRect);

            if (!_device.IsWired)
            {
                if (isConn && _device.BatteryPercent > 0)
                {
                    // Blocos discretos de LED com separação de 2px
                    int totalBlocks = 16;
                    int activeBlocks = (int)Math.Round(totalBlocks * (Math.Min(100, _device.BatteryPercent) / 100.0));
                    int pad = 2;
                    int innerW = barW - (pad * 2);
                    int innerH = barH - (pad * 2);
                    float blockW = (innerW - ((totalBlocks - 1) * 2f)) / totalBlocks;

                    for (int bIdx = 0; bIdx < totalBlocks; bIdx++)
                    {
                        float bx = barX + pad + bIdx * (blockW + 2f);
                        float by = barY + pad;
                        RectangleF bRect = new RectangleF(bx, by, blockW, innerH);

                        if (bIdx < activeBlocks)
                        {
                            using (SolidBrush sb = new SolidBrush(effectiveColor))
                                g.FillRectangle(sb, bRect);
                        }
                        else
                        {
                            using (SolidBrush sb = new SolidBrush(Color.FromArgb(20, 20, 20)))
                                g.FillRectangle(sb, bRect);
                        }
                    }
                }

                // Porcentagem da bateria em 19pt Bold preto
                string pct = _device.GetFormattedPercentage();
                if (!string.IsNullOrEmpty(pct))
                {
                    using (Font pctFont = new Font("Tahoma", 19f, FontStyle.Bold))
                    {
                        g.DrawString(pct, pctFont, Brushes.Black, cardRect.Right - 78, cardRect.Y + 52);
                    }
                }
            }
            else
            {
                // Dispositivo com fio: bloco azul/ciano
                using (Font wf = new Font("Tahoma", 8.8f, FontStyle.Bold))
                {
                    g.DrawString("⚡ " + I18n.T("WiredDeviceNotice"), wf, Brushes.Cyan, barX + 6, barY + 5);
                }
                using (Font pctFont = new Font("Tahoma", 17f, FontStyle.Bold))
                {
                    g.DrawString("USB", pctFont, Brushes.Black, cardRect.Right - 76, cardRect.Y + 53);
                }
            }

            // ── 7. Informações do Rodapé do Card ──────────────────────────────
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

            using (Font inf = new Font("Tahoma", 7.5f, FontStyle.Regular))
            {
                Color infoColor = isConn ? Color.Black : Color.FromArgb(100, 100, 100);
                using (SolidBrush ib = new SolidBrush(infoColor))
                    g.DrawString(info, inf, ib, cardRect.X + 8, cardRect.Bottom - 18);
            }
        }

        private static void Draw3DRaised(Graphics g, Rectangle r, int depth = 2)
        {
            using (Pen pWhite = new Pen(Color.FromArgb(255, 255, 255)))
            using (Pen pBlack = new Pen(Color.FromArgb(0, 0, 0)))
            using (Pen pDark = new Pen(Color.FromArgb(128, 128, 128)))
            using (Pen pLight = new Pen(Color.FromArgb(223, 223, 223)))
            {
                if (depth == 1)
                {
                    g.DrawLine(pWhite, r.Left, r.Top, r.Right - 1, r.Top);
                    g.DrawLine(pWhite, r.Left, r.Top, r.Left, r.Bottom - 1);
                    g.DrawLine(pBlack, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pBlack, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                }
                else
                {
                    g.DrawLine(pWhite, r.Left, r.Top, r.Right - 1, r.Top);
                    g.DrawLine(pWhite, r.Left, r.Top, r.Left, r.Bottom - 1);
                    g.DrawLine(pLight, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                    g.DrawLine(pLight, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                    g.DrawLine(pBlack, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pBlack, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pDark, r.Right - 2, r.Top + 1, r.Right - 2, r.Bottom - 2);
                    g.DrawLine(pDark, r.Left + 1, r.Bottom - 2, r.Right - 2, r.Bottom - 2);
                }
            }
        }

        private static void Draw3DSunken(Graphics g, Rectangle r)
        {
            using (Pen pWhite = new Pen(Color.FromArgb(255, 255, 255)))
            using (Pen pBlack = new Pen(Color.FromArgb(0, 0, 0)))
            using (Pen pDark = new Pen(Color.FromArgb(128, 128, 128)))
            using (Pen pLight = new Pen(Color.FromArgb(223, 223, 223)))
            {
                g.DrawLine(pDark, r.Left, r.Top, r.Right - 1, r.Top);
                g.DrawLine(pDark, r.Left, r.Top, r.Left, r.Bottom - 1);
                g.DrawLine(pBlack, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                g.DrawLine(pBlack, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                g.DrawLine(pWhite, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pWhite, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pLight, r.Right - 2, r.Top + 1, r.Right - 2, r.Bottom - 2);
                g.DrawLine(pLight, r.Left + 1, r.Bottom - 2, r.Right - 2, r.Bottom - 2);
            }
        }

        private static void DrawClassicButton(Graphics g, Rectangle r, string text, bool pressed = false, bool focused = false)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(192, 192, 192)))
                g.FillRectangle(b, r);

            if (pressed)
            {
                using (Pen pBlack = new Pen(Color.FromArgb(0, 0, 0)))
                using (Pen pDark = new Pen(Color.FromArgb(128, 128, 128)))
                using (Pen pWhite = new Pen(Color.FromArgb(255, 255, 255)))
                {
                    g.DrawLine(pBlack, r.Left, r.Top, r.Right - 1, r.Top);
                    g.DrawLine(pBlack, r.Left, r.Top, r.Left, r.Bottom - 1);
                    g.DrawLine(pDark, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                    g.DrawLine(pDark, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                    g.DrawLine(pWhite, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pWhite, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                }
            }
            else
            {
                Draw3DRaised(g, r, 2);
            }

            int off = pressed ? 1 : 0;
            using (Font f = new Font("Tahoma", 7.5f, FontStyle.Regular))
            using (SolidBrush tb = new SolidBrush(Color.Black))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, f, tb, new Rectangle(r.X + off, r.Y + off, r.Width, r.Height), sf);
            }
        }
    }
}
