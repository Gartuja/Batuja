using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OmniBattery.UI
{
    public enum ButtonIconType
    {
        None,
        Gear,
        Refresh,
        History,
        Overlay,
        Palette,
        Shield
    }

    /// <summary>
    /// Botão com estética AeroGlass moderna, fluida e tátil:
    /// Cantos arredondados (radius 9px), gradiente vítreo translúcido,
    /// linha de destaque de cristal no topo, halo sutil ao passar o mouse
    /// e suporte nativo a ícones vetoriais anti-aliased (engrenagem, atualizar, histórico, sobrepor, etc.).
    /// </summary>
    public class AeroGlassButton : Button
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private int _cornerRadius = 0;
        private Color _baseColor;
        private Color _accentColor;
        private bool _useAccentBorder = false;
        private ButtonIconType _iconType = ButtonIconType.None;
        private bool _classic3D = true;

        public bool Classic3D
        {
            get { return _classic3D; }
            set { _classic3D = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get { return _cornerRadius; }
            set { _cornerRadius = value; Invalidate(); }
        }

        public Color BaseGlassColor
        {
            get { return _baseColor; }
            set { _baseColor = value; Invalidate(); }
        }

        public Color AccentColor
        {
            get { return _accentColor; }
            set { _accentColor = value; Invalidate(); }
        }

        public bool UseAccentBorder
        {
            get { return _useAccentBorder; }
            set { _useAccentBorder = value; Invalidate(); }
        }

        public ButtonIconType IconType
        {
            get { return _iconType; }
            set { _iconType = value; Invalidate(); }
        }

        public bool ShowGearIcon
        {
            get { return _iconType == ButtonIconType.Gear; }
            set { if (value) _iconType = ButtonIconType.Gear; else if (_iconType == ButtonIconType.Gear) _iconType = ButtonIconType.None; Invalidate(); }
        }

        public AeroGlassButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            _cornerRadius = 9;
            _baseColor = Color.FromArgb(34, 40, 62);
            _accentColor = Color.FromArgb(88, 101, 242);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Evita pintar fundo padrão quadrado ou chamar InvokePaintBackground com coordenadas incorretas
        }

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
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_classic3D)
            {
                Rectangle r = new Rectangle(0, 0, Width, Height);
                Color faceColor = Color.FromArgb(192, 192, 192);
                using (SolidBrush b = new SolidBrush(faceColor))
                    g.FillRectangle(b, r);

                using (Pen pWhite = new Pen(Color.FromArgb(255, 255, 255)))
                using (Pen pBlack = new Pen(Color.FromArgb(0, 0, 0)))
                using (Pen pDark = new Pen(Color.FromArgb(128, 128, 128)))
                using (Pen pLight = new Pen(Color.FromArgb(223, 223, 223)))
                {
                    if (_isPressed)
                    {
                        g.DrawLine(pBlack, r.Left, r.Top, r.Right - 1, r.Top);
                        g.DrawLine(pBlack, r.Left, r.Top, r.Left, r.Bottom - 1);
                        g.DrawLine(pDark, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                        g.DrawLine(pDark, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                        g.DrawLine(pWhite, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                        g.DrawLine(pWhite, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
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

                Color textColor = !Enabled ? Color.FromArgb(128, 128, 128) : Color.Black;
                float offsetY = _isPressed ? 1f : 0f;
                float offsetX = _isPressed ? 1f : 0f;

                if (_iconType != ButtonIconType.None)
                {
                    SizeF textSize = g.MeasureString(Text, Font);
                    float iconDiameter = 12f;
                    float gap = 5f;
                    float totalContentWidth = iconDiameter + gap + textSize.Width;

                    float startX = Math.Max(5f, (Width - totalContentWidth) / 2f) + offsetX;
                    float iconCx = startX + (iconDiameter / 2f);
                    float iconCy = (Height / 2f) + offsetY;

                    DrawVectorIcon(g, _iconType, iconCx, iconCy, textColor);

                    float textX = startX + iconDiameter + gap;
                    float textY = ((Height - textSize.Height) / 2f) + offsetY;
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(Text, Font, textBrush, textX, textY);
                    }
                }
                else
                {
                    RectangleF textRect = new RectangleF(offsetX, offsetY, Width, Height);
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(Text, Font, textBrush, textRect, sf);
                    }
                }
                return;
            }

            float w = Width - 1.2f;
            float h = Height - 1.2f;
            if (w <= 4 || h <= 4) return;

            RectangleF rect = new RectangleF(0.6f, 0.6f, w, h);

            using (GraphicsPath path = CreateRoundedRectPath(rect, _cornerRadius))
            {
                // Paleta de gradiente vítreo moderna
                Color c = _baseColor;
                Color topColor;
                Color bottomColor;

                if (!Enabled)
                {
                    topColor = Color.FromArgb(36, 40, 54);
                    bottomColor = Color.FromArgb(22, 25, 36);
                }
                else if (_isPressed)
                {
                    topColor = Color.FromArgb(Math.Max(0, c.R - 15), Math.Max(0, c.G - 15), Math.Max(0, c.B - 10));
                    bottomColor = Color.FromArgb(Math.Max(0, c.R - 25), Math.Max(0, c.G - 25), Math.Max(0, c.B - 20));
                }
                else if (_isHovered)
                {
                    topColor = Color.FromArgb(Math.Min(255, c.R + 45), Math.Min(255, c.G + 45), Math.Min(255, c.B + 55));
                    bottomColor = Color.FromArgb(Math.Min(255, c.R + 18), Math.Min(255, c.G + 18), Math.Min(255, c.B + 24));
                }
                else
                {
                    topColor = Color.FromArgb(Math.Min(255, c.R + 25), Math.Min(255, c.G + 25), Math.Min(255, c.B + 32));
                    bottomColor = Color.FromArgb(Math.Max(0, c.R - 10), Math.Max(0, c.G - 10), Math.Max(0, c.B - 10));
                }

                // 1. Fundo de vidro translúcido contínuo
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect, topColor, bottomColor, 90f))
                {
                    g.FillPath(bgBrush, path);
                }

                // 2. Borda elegante escura translúcida (sem bordas brancas indesejadas)
                Color borderColor;
                if (!Enabled)
                {
                    borderColor = Color.FromArgb(38, 44, 62);
                }
                else if (_isHovered)
                {
                    borderColor = _useAccentBorder
                        ? Color.FromArgb(220, _accentColor)
                        : Color.FromArgb(90, 110, 175);
                }
                else
                {
                    borderColor = _useAccentBorder
                        ? Color.FromArgb(160, _accentColor)
                        : Color.FromArgb(50, 60, 88);
                }

                using (Pen borderPen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(borderPen, path);
                }

                // 3. Texto e Ícone Vetorial
                Color textColor = !Enabled ? Color.FromArgb(120, 130, 155) : ForeColor;
                float offsetY = _isPressed ? 1f : 0f;

                if (_iconType != ButtonIconType.None)
                {
                    SizeF textSize = g.MeasureString(Text, Font);
                    float iconDiameter = 12f;
                    float gap = 6f;
                    float totalContentWidth = iconDiameter + gap + textSize.Width;

                    float startX = Math.Max(8f, (Width - totalContentWidth) / 2f);
                    float iconCx = startX + (iconDiameter / 2f);
                    float iconCy = (Height / 2f) + offsetY;

                    DrawVectorIcon(g, _iconType, iconCx, iconCy, textColor);

                    float textX = startX + iconDiameter + gap;
                    float textY = ((Height - textSize.Height) / 2f) + offsetY;
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(Text, Font, textBrush, textX, textY);
                    }
                }
                else
                {
                    RectangleF textRect = new RectangleF(rect.X, rect.Y + offsetY, rect.Width, rect.Height);
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(Text, Font, textBrush, textRect, sf);
                    }
                }
            }
        }

        private static void DrawVectorIcon(Graphics g, ButtonIconType type, float cx, float cy, Color color)
        {
            switch (type)
            {
                case ButtonIconType.Gear:
                    DrawGearIcon(g, cx, cy, 5.5f, 3.8f, 1.9f, 6, color);
                    break;

                case ButtonIconType.Refresh:
                    using (Pen pen = new Pen(color, 1.7f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawArc(pen, cx - 5.5f, cy - 5.5f, 11f, 11f, 35f, 260f);
                    }
                    using (SolidBrush b = new SolidBrush(color))
                    {
                        PointF[] arrow = new PointF[] {
                            new PointF(cx + 6f, cy - 4.5f),
                            new PointF(cx + 1.5f, cy - 7f),
                            new PointF(cx + 2.5f, cy - 1.5f)
                        };
                        g.FillPolygon(b, arrow);
                    }
                    break;

                case ButtonIconType.History:
                    using (Pen pen = new Pen(color, 1.5f))
                    {
                        g.DrawEllipse(pen, cx - 6f, cy - 6f, 12f, 12f);
                        g.DrawLine(pen, cx, cy, cx, cy - 3.5f);
                        g.DrawLine(pen, cx, cy, cx + 2.5f, cy);
                    }
                    break;

                case ButtonIconType.Overlay:
                    using (Pen pen = new Pen(color, 1.4f))
                    {
                        g.DrawRectangle(pen, cx - 6.5f, cy - 5f, 13f, 10f);
                        g.DrawLine(pen, cx - 6.5f, cy + 2f, cx + 6.5f, cy + 2f);
                    }
                    using (SolidBrush b = new SolidBrush(color))
                        g.FillEllipse(b, cx + 2.2f, cy - 3.5f, 2.2f, 2.2f);
                    break;

                case ButtonIconType.Palette:
                    using (Pen pen = new Pen(color, 1.4f))
                    {
                        g.DrawRectangle(pen, cx - 6f, cy - 5f, 12f, 10f);
                    }
                    using (SolidBrush b = new SolidBrush(color))
                    {
                        g.FillEllipse(b, cx - 3.5f, cy - 3f, 2.5f, 2.5f);
                        PointF[] pts = new PointF[] {
                            new PointF(cx - 6f, cy + 5f),
                            new PointF(cx - 1.5f, cy + 0.5f),
                            new PointF(cx + 1.5f, cy + 2.5f),
                            new PointF(cx + 4.5f, cy - 0.5f),
                            new PointF(cx + 6f, cy + 0.5f),
                            new PointF(cx + 6f, cy + 5f)
                        };
                        g.FillPolygon(b, pts);
                    }
                    break;

                case ButtonIconType.Shield:
                    using (GraphicsPath sp = new GraphicsPath())
                    {
                        sp.AddLine(cx - 5.5f, cy - 5.5f, cx + 5.5f, cy - 5.5f);
                        sp.AddLine(cx + 5.5f, cy - 5.5f, cx + 5.5f, cy);
                        sp.AddBezier(cx + 5.5f, cy, cx + 4.5f, cy + 4.8f, cx, cy + 6.8f, cx, cy + 6.8f);
                        sp.AddBezier(cx, cy + 6.8f, cx - 4.5f, cy + 4.8f, cx - 5.5f, cy, cx - 5.5f, cy);
                        sp.CloseFigure();
                        using (Pen pen = new Pen(color, 1.4f))
                            g.DrawPath(pen, sp);
                    }
                    break;
            }
        }

        private static void DrawGearIcon(Graphics g, float cx, float cy, float outerR, float innerR, float holeR, int teeth, Color color)
        {
            float step = (float)(2 * Math.PI / teeth);
            float halfStep = step / 2f;
            float toothWidth = step * 0.28f;

            using (GraphicsPath p = new GraphicsPath(FillMode.Alternate))
            {
                PointF[] pts = new PointF[teeth * 4];
                int idx = 0;
                for (int i = 0; i < teeth; i++)
                {
                    float a = i * step;
                    pts[idx++] = new PointF(cx + (float)Math.Cos(a - toothWidth) * outerR, cy + (float)Math.Sin(a - toothWidth) * outerR);
                    pts[idx++] = new PointF(cx + (float)Math.Cos(a + toothWidth) * outerR, cy + (float)Math.Sin(a + toothWidth) * outerR);
                    pts[idx++] = new PointF(cx + (float)Math.Cos(a + halfStep - toothWidth) * innerR, cy + (float)Math.Sin(a + halfStep - toothWidth) * innerR);
                    pts[idx++] = new PointF(cx + (float)Math.Cos(a + halfStep + toothWidth) * innerR, cy + (float)Math.Sin(a + halfStep + toothWidth) * innerR);
                }
                p.AddPolygon(pts);
                p.AddEllipse(cx - holeR, cy - holeR, holeR * 2f, holeR * 2f);

                using (SolidBrush b = new SolidBrush(color))
                {
                    g.FillPath(b, p);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectPath(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
