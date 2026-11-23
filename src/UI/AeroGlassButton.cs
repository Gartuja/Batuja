using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OmniBattery.UI
{
    /// <summary>
    /// Botão com estética AeroGlass: cantos arredondados matematicamente simétricos,
    /// reflexo suave e translúcido de vidro, borda sutil de cristal e iluminação
    /// elegante ao passar o mouse. Suporta opcionalmente ícone vetorial de engrenagem.
    /// </summary>
    public class AeroGlassButton : Button
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private int _cornerRadius = 6;
        private Color _baseColor;
        private Color _accentColor;
        private bool _useAccentBorder = false;
        private bool _showGearIcon = false;

        public int CornerRadius
        {
            get { return _cornerRadius; }
            set
            {
                if (_cornerRadius != value)
                {
                    _cornerRadius = value;
                    UpdateRegion();
                    Invalidate();
                }
            }
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

            _cornerRadius = 6;
            _baseColor = Color.FromArgb(42, 48, 70);
            _accentColor = Color.FromArgb(88, 101, 242);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (Width > 2 && Height > 2)
            {
                using (GraphicsPath p = CreateRoundedRectPath(new RectangleF(0, 0, Width, Height), _cornerRadius))
                {
                    this.Region = new Region(p);
                }
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

            float w = Width - 1.0f;
            float h = Height - 1.0f;
            if (w <= 2 || h <= 2) return;

            RectangleF rect = new RectangleF(0.5f, 0.5f, w, h);

            using (GraphicsPath path = CreateRoundedRectPath(rect, _cornerRadius))
            {
                // Cores do gradiente vítreo
                Color topColor;
                Color bottomColor;

                if (!Enabled)
                {
                    topColor = Color.FromArgb(40, 45, 60);
                    bottomColor = Color.FromArgb(28, 32, 45);
                }
                else if (_isPressed)
                {
                    topColor = BlendColors(_baseColor, Color.Black, 0.35f);
                    bottomColor = BlendColors(_baseColor, Color.Black, 0.15f);
                }
                else if (_isHovered)
                {
                    topColor = BlendColors(_baseColor, Color.White, 0.26f);
                    bottomColor = BlendColors(_baseColor, Color.White, 0.10f);
                }
                else
                {
                    topColor = BlendColors(_baseColor, Color.White, 0.14f);
                    bottomColor = BlendColors(_baseColor, Color.Black, 0.08f);
                }

                // 1. Fundo de vidro com gradiente vertical contínuo e suave
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect, topColor, bottomColor, 90f))
                {
                    g.FillPath(bgBrush, path);
                }

                // 2. Brilho especular sutil na borda superior interna do vidro (sem corte no meio)
                using (Pen topShinePen = new Pen(Color.FromArgb(_isHovered ? 75 : 45, 255, 255, 255), 1f))
                {
                    g.DrawLine(topShinePen, rect.X + _cornerRadius, rect.Y + 1f, rect.Right - _cornerRadius, rect.Y + 1f);
                }

                // 3. Borda de cristal / destaque de vidro
                Color borderColor;
                if (!Enabled)
                {
                    borderColor = Color.FromArgb(30, 255, 255, 255);
                }
                else if (_isHovered)
                {
                    borderColor = _useAccentBorder
                        ? Color.FromArgb(230, _accentColor)
                        : Color.FromArgb(135, 255, 255, 255);
                }
                else
                {
                    borderColor = _useAccentBorder
                        ? Color.FromArgb(160, _accentColor)
                        : Color.FromArgb(50, 255, 255, 255);
                }

                using (Pen borderPen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(borderPen, path);
                }

                // 4. Conteúdo: Texto e Ícone de Engrenagem (se ativado)
                Color textColor = !Enabled ? Color.FromArgb(120, 130, 155) : ForeColor;

                if (_showGearIcon)
                {
                    // Mede o texto para centralizar o conjunto (engrenagem + espaço + texto)
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
                    Rectangle textRect = new Rectangle(0, 0, Width, Height);
                    TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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

        private static Color BlendColors(Color c1, Color c2, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            int r = (int)(c1.R * (1 - amount) + c2.R * amount);
            int g = (int)(c1.G * (1 - amount) + c2.G * amount);
            int b = (int)(c1.B * (1 - amount) + c2.B * amount);
            return Color.FromArgb(c1.A, r, g, b);
        }
    }
}
