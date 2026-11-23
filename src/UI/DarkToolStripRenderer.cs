using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OmniBattery.UI
{
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(55, 62, 95); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(55, 62, 95); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(55, 62, 95); } }
        public override Color MenuBorder { get { return Color.FromArgb(55, 60, 90); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(88, 101, 242); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 27, 40); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 27, 40); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 27, 40); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 27, 40); } }
        public override Color SeparatorDark { get { return Color.FromArgb(45, 50, 75); } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
        public override Color CheckBackground { get { return Color.FromArgb(40, 45, 70); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(55, 62, 95); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(55, 62, 95); } }
    }

    public class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item != null)
            {
                if (!e.Item.Enabled)
                {
                    // Headers or disabled options: soft slate color
                    e.TextColor = Color.FromArgb(140, 150, 175);
                }
                else
                {
                    // Active options: pure crisp white!
                    e.TextColor = Color.White;
                }
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = e.ImageRectangle;
            r.Inflate(1, 1);

            // Background badge for checkmark
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(88, 101, 242)))
            {
                g.FillEllipse(bg, r);
            }

            // Crisp white checkmark
            using (Pen p = new Pen(Color.White, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                Point[] pts = new Point[]
                {
                    new Point(r.Left + 3, r.Top + r.Height / 2),
                    new Point(r.Left + r.Width / 2 - 1, r.Bottom - 4),
                    new Point(r.Right - 3, r.Top + 4)
                };
                g.DrawLines(p, pts);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Color.FromArgb(45, 50, 75), 1f))
            {
                g.DrawLine(p, 28, y, e.Item.Width - 8, y);
            }
        }
    }
}
