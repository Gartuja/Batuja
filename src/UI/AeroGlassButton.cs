using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OmniBattery.UI
{
    /// <summary>
    /// Botão com estética AeroGlass: cantos arredondados, reflexo de vidro translúcido,
    /// borda sutil de cristal e iluminação suave ao passar o mouse.
    /// </summary>
    public class AeroGlassButton : Button
    {
        private bool _isHovered = false;
        private bool _isPressed = false;
        private int _cornerRadius = 8;
        private Color _baseColor;
        private Color _accentColor;
        private bool _useAccentBorder = false;

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

        public AeroGlassButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            _cornerRadius = 8;
            _baseColor = Color.FromArgb(42, 48, 70);
            _accentColor = Color.FromArgb(88, 101, 242);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
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
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 2 || rect.Height <= 2) return;

            using (GraphicsPath path = CreateRoundedRectPath(rect, _cornerRadius))
            {
                // Determina cor de base e gradiente AeroGlass
                Color topColor;
                Color bottomColor;

                if (!Enabled)
                {
                    topColor = Color.FromArgb(40, 45, 60);
                    bottomColor = Color.FromArgb(28, 32, 45);
                }
                else if (_isPressed)
                {
                    topColor = BlendColors(_baseColor, Color.Black, 0.25f);
                    bottomColor = _baseColor;
                }
                else if (_isHovered)
                {
                    topColor = BlendColors(_baseColor, Color.White, 0.22f);
                    bottomColor = BlendColors(_baseColor, Color.White, 0.08f);
                }
                else
                {
                    topColor = BlendColors(_baseColor, Color.White, 0.12f);
                    bottomColor = _baseColor;
                }

                // 1. Fundo do botão com gradiente suave
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect, topColor, bottomColor, 90f))
                {
                    g.FillPath(bgBrush, path);
                }

                // 2. Reflexo AeroGlass no topo (metade superior)
                Rectangle shineRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, Math.Max(2, rect.Height / 2));
                using (GraphicsPath shinePath = CreateTopShinePath(shineRect, _cornerRadius))
                {
                    int alpha = _isHovered ? 75 : 45;
                    using (LinearGradientBrush shineBrush = new LinearGradientBrush(
                        shineRect,
                        Color.FromArgb(alpha, 255, 255, 255),
                        Color.FromArgb(5, 255, 255, 255),
                        90f))
                    {
                        g.FillPath(shineBrush, shinePath);
                    }
                }

                // 3. Borda de vidro translúcida
                Color borderColor;
                if (!Enabled)
                {
                    borderColor = Color.FromArgb(35, 255, 255, 255);
                }
                else if (_isHovered)
                {
                    borderColor = _useAccentBorder ?
                        Color.FromArgb(220, _accentColor) :
                        Color.FromArgb(140, 255, 255, 255);
                }
                else
                {
                    borderColor = _useAccentBorder ?
                        Color.FromArgb(140, _accentColor) :
                        Color.FromArgb(55, 255, 255, 255);
                }

                float penWidth = (_isHovered || _useAccentBorder) ? 1.4f : 1.0f;
                using (Pen borderPen = new Pen(borderColor, penWidth))
                {
                    g.DrawPath(borderPen, path);
                }

                // 4. Texto centralizado
                Color textColor = !Enabled ? Color.FromArgb(120, 130, 155) : ForeColor;
                TextRenderer.DrawText(g, Text, Font, rect, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath CreateTopShinePath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
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
