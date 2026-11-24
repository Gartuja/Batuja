using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OmniBattery.UI
{
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(43, 82, 120); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(43, 82, 120); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(43, 82, 120); } }
        public override Color MenuBorder { get { return Color.FromArgb(24, 26, 30); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(54, 57, 63); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(46, 49, 54); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(46, 49, 54); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(46, 49, 54); } }
        public override Color SeparatorDark { get { return Color.FromArgb(24, 26, 30); } }
        public override Color SeparatorLight { get { return Color.FromArgb(80, 85, 96); } }
        public override Color CheckBackground { get { return Color.FromArgb(46, 49, 54); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(43, 82, 120); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(43, 82, 120); } }
    }

    public class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width, e.ToolStrip.Height);
            using (Pen pLight = new Pen(Color.FromArgb(120, 126, 138)))
            using (Pen pMidLight = new Pen(Color.FromArgb(90, 95, 105)))
            using (Pen pDark = new Pen(Color.FromArgb(28, 30, 34)))
            using (Pen pBlack = new Pen(Color.FromArgb(18, 20, 23)))
            {
                g.DrawLine(pLight, r.Left, r.Top, r.Right - 1, r.Top);
                g.DrawLine(pLight, r.Left, r.Top, r.Left, r.Bottom - 1);
                g.DrawLine(pMidLight, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                g.DrawLine(pMidLight, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                g.DrawLine(pBlack, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pBlack, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pDark, r.Right - 2, r.Top + 1, r.Right - 2, r.Bottom - 2);
                g.DrawLine(pDark, r.Left + 1, r.Bottom - 2, r.Right - 2, r.Bottom - 2);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item != null)
            {
                if (!e.Item.Enabled)
                {
                    e.TextColor = Color.FromArgb(120, 126, 138);
                }
                else
                {
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
            Rectangle r = e.ImageRectangle;

            Color checkColor = (e.Item != null && e.Item.Selected) ? Color.White : Color.FromArgb(70, 220, 120);
            using (Pen p = new Pen(checkColor, 1.8f))
            {
                Point[] pts = new Point[]
                {
                    new Point(r.Left + 3, r.Top + r.Height / 2),
                    new Point(r.Left + r.Width / 2 - 1, r.Bottom - 3),
                    new Point(r.Right - 2, r.Top + 4)
                };
                g.DrawLines(p, pts);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            int y = e.Item.Height / 2;
            using (Pen pDark = new Pen(Color.FromArgb(24, 26, 30), 1f))
            using (Pen pLight = new Pen(Color.FromArgb(80, 85, 96), 1f))
            {
                g.DrawLine(pDark, 4, y, e.Item.Width - 4, y);
                g.DrawLine(pLight, 4, y + 1, e.Item.Width - 4, y + 1);
            }
        }
    }
}
