using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OmniBattery.UI
{
    /// <summary>
    /// Botão com estética AeroGlass moderna e fluida:
    /// Cantos suavemente arredondados (radius 8px), gradiente translúcido contínuo,
    /// linha de destaque de cristal no topo e suporte nativo ao ícone vetorial de engrenagem.
    /// Renderização 100% anti-aliased sem serrilhamento ou recortes por máscara de 1-bit.
    /// </summary>
    public class AeroGlassButton : Button
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private int _cornerRadius = 8;
        private Color _baseColor;
        private Color _accentColor;
        private bool _useAccentBorder = false;
        private bool _showGearIcon = false;

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

        public bool ShowGearIcon
        {
            get { return _showGearIcon; }
            set { _showGearIcon = value; Invalidate(); }
        }

        public AeroGlassButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            _cornerRadius = 8;
            _baseColor = Color.FromArgb(36, 42, 64);
            _accentColor = Color.FromArgb(88, 101, 242);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Não desenha fundo quadrado nativo de botão para evitar artefatos nos cantos
            if (Parent != null)
            {
                // Deixa o painel pai pintar o fundo sob o botão
                InvokePaintBackground(Parent, pevent);
            }
            else
            {
                base.OnPaintBackground(pevent);
            }
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

            float w = Width - 1.2f;
            float h = Height - 1.2f;
            if (w <= 4 || h <= 4) return;

            RectangleF rect = new RectangleF(0.6f, 0.6f, w, h);

            using (GraphicsPath path = CreateRoundedRectPath(rect, _cornerRadius))
            {
                // Paleta de gradiente vítreo suave
                Color c = _baseColor;
                Color topColor;
                Color bottomColor;

                if (!Enabled)
                {
                    topColor = Color.FromArgb(38, 42, 56);
                    bottomColor = Color.FromArgb(24, 28, 38);
                }
                else if (_isPressed)
                {
                    topColor = Color.FromArgb(Math.Max(0, c.R - 15), Math.Max(0, c.G - 15), Math.Max(0, c.B - 10));
                    bottomColor = Color.FromArgb(Math.Max(0, c.R - 25), Math.Max(0, c.G - 25), Math.Max(0, c.B - 20));
                }
                else if (_isHovered)
                {
                    topColor = Color.FromArgb(Math.Min(255, c.R + 40), Math.Min(255, c.G + 40), Math.Min(255, c.B + 50));
                    bottomColor = Color.FromArgb(Math.Min(255, c.R + 15), Math.Min(255, c.G + 15), Math.Min(255, c.B + 20));
                }
                else
                {
                    topColor = Color.FromArgb(Math.Min(255, c.R + 25), Math.Min(255, c.G + 25), Math.Min(255, c.B + 30));
                    bottomColor = Color.FromArgb(Math.Max(0, c.R - 10), Math.Max(0, c.G - 10), Math.Max(0, c.B - 10));
                }

                // 1. Fundo de vidro translúcido contínuo
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect, topColor, bottomColor, 90f))
                {
                    g.FillPath(bgBrush, path);
                }

                // 2. Linha de brilho especular suave na borda superior interna
                int shineAlpha = _isHovered ? 70 : 45;
                using (Pen topShinePen = new Pen(Color.FromArgb(shineAlpha, 255, 255, 255), 1f))
                {
                    g.DrawLine(topShinePen, rect.X + _cornerRadius, rect.Y + 1.2f, rect.Right - _cornerRadius, rect.Y + 1.2f);
                }

                // 3. Borda de cristal
                Color borderColor;
                if (!Enabled)
                {
                    borderColor = Color.FromArgb(25, 255, 255, 255);
                }
                else if (_isHovered)
                {
                    borderColor = _useAccentBorder
                        ? Color.FromArgb(240, _accentColor)
                        : Color.FromArgb(120, 255, 255, 255);
                }
                else
                {
                    borderColor = _useAccentBorder
                        ? Color.FromArgb(160, _accentColor)
                        : Color.FromArgb(45, 255, 255, 255);
                }

                using (Pen borderPen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(borderPen, path);
                }

                // 4. Texto e Ícone de Engrenagem
                Color textColor = !Enabled ? Color.FromArgb(120, 130, 155) : ForeColor;

                if (_showGearIcon)
                {
                    SizeF textSize = g.MeasureString(Text, Font);
                    float gearOuterR = 5.2f;
                    float gearInnerR = 3.6f;
                    float gearHoleR = 1.8f;
                    float gearDiameter = gearOuterR * 2f;
                    float gap = 6f;
                    float totalWidth = gearDiameter + gap + textSize.Width;

                    float startX = (Width - totalWidth) / 2f;
                    float gearCx = startX + gearOuterR;
                    float gearCy = Height / 2f;

                    DrawGearIcon(g, gearCx, gearCy, gearOuterR, gearInnerR, gearHoleR, 6, textColor);

                    float textX = startX + gearDiameter + gap;
                    float textY = (Height - textSize.Height) / 2f;
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(Text, Font, textBrush, textX, textY);
                    }
                }
                else
                {
                    StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        g.DrawString(Text, Font, textBrush, rect, sf);
                    }
                }
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
