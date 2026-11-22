using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public static class IconRenderer
    {
        public static void DrawDeviceIcon(Graphics g, Rectangle rect, DeviceCategory category, Color color, string customIconPath)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // If user supplied a valid custom image file (.png, .jpg, .ico, .gif)
            if (!string.IsNullOrEmpty(customIconPath) && File.Exists(customIconPath))
            {
                try
                {
                    using (Image img = Image.FromFile(customIconPath))
                    {
                        Rectangle imgRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                        g.DrawImage(img, imgRect);
                        return;
                    }
                }
                catch
                {
                    // Fallback to vector drawing
                }
            }

            int cx = rect.X + rect.Width / 2;
            int cy = rect.Y + rect.Height / 2;

            switch (category)
            {
                case DeviceCategory.Mouse:
                    DrawMouse(g, rect, color);
                    break;
                case DeviceCategory.Teclado:
                    DrawKeyboard(g, rect, color);
                    break;
                case DeviceCategory.FoneDeOuvido:
                    DrawHeadset(g, rect, color);
                    break;
                case DeviceCategory.Controle:
                    DrawGamepad(g, rect, color);
                    break;
                case DeviceCategory.Laptop:
                    DrawLaptop(g, rect, color);
                    break;
                default:
                    DrawBattery(g, rect, color);
                    break;
            }
        }

        private static void DrawMouse(Graphics g, Rectangle r, Color c)
        {
            // Center area: 20 width x 28 height
            int w = 20;
            int h = 28;
            int x = r.X + (r.Width - w) / 2;
            int y = r.Y + (r.Height - h) / 2;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(x, y, 10, 10, 180, 90);
                path.AddArc(x + w - 10, y, 10, 10, 270, 90);
                path.AddArc(x + w - 12, y + h - 12, 12, 12, 0, 90);
                path.AddArc(x, y + h - 12, 12, 12, 90, 90);
                path.CloseFigure();

                using (Pen pen = new Pen(Color.White, 1.8f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Button split line
            using (Pen splitPen = new Pen(Color.FromArgb(180, Color.White), 1.2f))
            {
                g.DrawLine(splitPen, x + w / 2, y, x + w / 2, y + 10);
            }

            // Scroll wheel
            int ww = 3;
            int wh = 6;
            Rectangle wheelRect = new Rectangle(x + (w - ww) / 2, y + 4, ww, wh);
            using (SolidBrush wb = new SolidBrush(c))
            {
                g.FillRectangle(wb, wheelRect);
            }
        }

        private static void DrawKeyboard(Graphics g, Rectangle r, Color c)
        {
            int w = 32;
            int h = 20;
            int x = r.X + (r.Width - w) / 2;
            int y = r.Y + (r.Height - h) / 2;

            // Outer chassis
            using (GraphicsPath path = CreateRoundedRect(new Rectangle(x, y, w, h), 3))
            {
                using (Pen pen = new Pen(Color.White, 1.6f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Rows of keys
            using (SolidBrush keyBrush = new SolidBrush(Color.FromArgb(200, Color.White)))
            {
                // Row 1
                for (int i = 0; i < 5; i++)
                {
                    g.FillRectangle(keyBrush, x + 3 + i * 5, y + 3, 3, 3);
                }
                // Row 2
                for (int i = 0; i < 5; i++)
                {
                    g.FillRectangle(keyBrush, x + 3 + i * 5, y + 8, 3, 3);
                }
            }

            // Spacebar in accent color
            using (SolidBrush spaceBrush = new SolidBrush(c))
            {
                g.FillRectangle(spaceBrush, x + 7, y + 13, 16, 3);
            }
        }

        private static void DrawHeadset(Graphics g, Rectangle r, Color c)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;

            // Arch headband
            Rectangle arcRect = new Rectangle(cx - 12, cy - 13, 24, 22);
            using (Pen arcPen = new Pen(Color.White, 2.2f))
            {
                g.DrawArc(arcPen, arcRect, 180, 180);
            }

            // Left Earcup
            Rectangle leftCup = new Rectangle(cx - 14, cy - 3, 6, 14);
            using (GraphicsPath lp = CreateRoundedRect(leftCup, 3))
            {
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillPath(b, lp);
                }
                using (Pen p = new Pen(Color.White, 1.2f))
                {
                    g.DrawPath(p, lp);
                }
            }

            // Right Earcup
            Rectangle rightCup = new Rectangle(cx + 8, cy - 3, 6, 14);
            using (GraphicsPath rp = CreateRoundedRect(rightCup, 3))
            {
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillPath(b, rp);
                }
                using (Pen p = new Pen(Color.White, 1.2f))
                {
                    g.DrawPath(p, rp);
                }
            }

            // Microphone Boom
            using (Pen micPen = new Pen(Color.FromArgb(200, Color.White), 1.2f))
            {
                g.DrawArc(micPen, cx - 12, cy + 3, 14, 12, 90, 90);
                using (SolidBrush mb = new SolidBrush(c))
                {
                    g.FillEllipse(mb, cx - 2, cy + 10, 3, 3);
                }
            }
        }

        private static void DrawGamepad(Graphics g, Rectangle r, Color c)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;

            // Gamepad body
            int w = 30;
            int h = 18;
            int x = cx - w / 2;
            int y = cy - h / 2 - 1;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(x, y, 8, 8, 180, 90);
                path.AddArc(x + w - 8, y, 8, 8, 270, 90);
                // Grips angled downward
                path.AddLine(x + w, y + 6, x + w - 2, y + h + 2);
                path.AddArc(x + w - 8, y + h - 2, 7, 7, 0, 90);
                path.AddLine(x + w - 5, y + h + 4, cx + 4, y + 10);
                path.AddLine(cx - 4, y + 10, x + 5, y + h + 4);
                path.AddArc(x + 1, y + h - 2, 7, 7, 90, 90);
                path.AddLine(x, y + h + 2, x, y + 6);
                path.CloseFigure();

                using (Pen pen = new Pen(Color.White, 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Left D-Pad cross
            using (SolidBrush db = new SolidBrush(Color.White))
            {
                g.FillRectangle(db, x + 5, y + 5, 2, 6);
                g.FillRectangle(db, x + 3, y + 7, 6, 2);
            }

            // Right Action buttons (4 dots)
            using (SolidBrush bb = new SolidBrush(c))
            {
                g.FillEllipse(bb, x + w - 7, y + 4, 2, 2);
                g.FillEllipse(bb, x + w - 9, y + 6, 2, 2);
                g.FillEllipse(bb, x + w - 5, y + 6, 2, 2);
                g.FillEllipse(bb, x + w - 7, y + 8, 2, 2);
            }
        }

        private static void DrawLaptop(Graphics g, Rectangle r, Color c)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;

            // Screen
            Rectangle screenRect = new Rectangle(cx - 12, cy - 10, 24, 15);
            using (Pen p = new Pen(Color.White, 1.5f))
            {
                g.DrawRectangle(p, screenRect);
            }

            // Screen inside display in accent color
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, c)))
            {
                g.FillRectangle(b, screenRect.X + 2, screenRect.Y + 2, screenRect.Width - 4, screenRect.Height - 4);
            }

            // Base
            using (Pen p = new Pen(Color.White, 1.8f))
            {
                g.DrawLine(p, cx - 16, cy + 7, cx + 16, cy + 7);
            }
        }

        private static void DrawBattery(Graphics g, Rectangle r, Color c)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;

            // Battery body
            Rectangle bRect = new Rectangle(cx - 8, cy - 11, 16, 22);
            using (GraphicsPath path = CreateRoundedRect(bRect, 3))
            {
                using (Pen p = new Pen(Color.White, 1.6f))
                {
                    g.DrawPath(p, path);
                }
            }

            // Terminal cap
            Rectangle cap = new Rectangle(cx - 3, cy - 14, 6, 3);
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, cap);
            }

            // Lightning Bolt ⚡
            using (GraphicsPath bolt = new GraphicsPath())
            {
                bolt.AddLine(cx + 1, cy - 8, cx - 4, cy + 1);
                bolt.AddLine(cx - 4, cy + 1, cx - 1, cy + 1);
                bolt.AddLine(cx - 1, cy + 1, cx - 2, cy + 8);
                bolt.AddLine(cx - 2, cy + 8, cx + 4, cy - 1);
                bolt.AddLine(cx + 4, cy - 1, cx + 1, cy - 1);
                bolt.CloseFigure();

                using (SolidBrush bb = new SolidBrush(c))
                {
                    g.FillPath(bb, bolt);
                }
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
