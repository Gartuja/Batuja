using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace OmniBattery.UI
{
    /// <summary>
    /// Draws the Batuja application icon: a minimalist light-blue mouse with grey details.
    /// Also used by build.ps1 (via Add-Type) to generate app.ico for the executable.
    /// </summary>
    public static class AppIconRenderer
    {
        private static readonly Color BodyTop    = Color.FromArgb(178, 226, 252); // light blue
        private static readonly Color BodyBottom = Color.FromArgb(110, 190, 240); // light blue (deeper)
        private static readonly Color Outline    = Color.FromArgb(120, 132, 146); // grey
        private static readonly Color Detail     = Color.FromArgb(96, 108, 122);  // darker grey

        public static Bitmap Render(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                Draw(g, new RectangleF(0, 0, size, size));
            }
            return bmp;
        }

        /// <summary>Draws the mouse inside the given square (designed on a 256 grid).</summary>
        public static void Draw(Graphics g, RectangleF area)
        {
            float k = Math.Min(area.Width, area.Height) / 256f;
            float ox = area.X + (area.Width - 256f * k) / 2f;
            float oy = area.Y + (area.Height - 256f * k) / 2f;

            // Mouse body: 136 x 216, centred
            RectangleF body = new RectangleF(ox + 60 * k, oy + 20 * k, 136 * k, 216 * k);
            float radius = 66 * k;
            float stroke = Math.Max(1f, 11 * k);

            using (GraphicsPath path = RoundedRect(body, radius))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    new RectangleF(body.X, body.Y - 1, body.Width, body.Height + 2),
                    BodyTop, BodyBottom, LinearGradientMode.Vertical))
                {
                    g.FillPath(fill, path);
                }

                // Button area separators, clipped to body
                GraphicsState st = g.Save();
                g.SetClip(path);
                using (Pen sep = new Pen(Outline, Math.Max(1f, 8 * k)))
                {
                    float splitY = body.Y + 88 * k;
                    float midX = body.X + body.Width / 2f;
                    g.DrawLine(sep, midX, body.Y, midX, splitY);
                    g.DrawLine(sep, body.X, splitY, body.Right, splitY);
                }
                g.Restore(st);

                using (Pen pen = new Pen(Outline, stroke))
                {
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, path);
                }
            }

            // Scroll wheel (grey pill)
            if (k * 256f >= 20f)
            {
                RectangleF wheel = new RectangleF(ox + 116 * k, oy + 42 * k, 24 * k, 40 * k);
                using (GraphicsPath wp = RoundedRect(wheel, 12 * k))
                using (SolidBrush wb = new SolidBrush(Detail))
                {
                    g.FillPath(wb, wp);
                }
            }
        }

        /// <summary>Multi-resolution icon for window title bar / taskbar.</summary>
        public static Icon CreateIcon()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteIco(ms, new[] { 16, 20, 24, 32, 40, 48, 64 }, false);
                ms.Position = 0;
                return new Icon(ms);
            }
        }

        /// <summary>Writes app.ico (DIB frames + PNG 256px frame) to disk.</summary>
        public static void SaveIco(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                WriteIco(fs, new[] { 16, 20, 24, 32, 40, 48, 64, 256 }, true);
            }
        }

        private static void WriteIco(Stream output, int[] sizes, bool pngForLarge)
        {
            byte[][] frames = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap bmp = Render(sizes[i]))
                {
                    frames[i] = (pngForLarge && sizes[i] >= 256) ? EncodePng(bmp) : EncodeDib(bmp);
                }
            }

            BinaryWriter w = new BinaryWriter(output);
            w.Write((ushort)0);            // reserved
            w.Write((ushort)1);            // type: icon
            w.Write((ushort)sizes.Length); // count

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s)); // width
                w.Write((byte)(s >= 256 ? 0 : s)); // height
                w.Write((byte)0);                  // colour count
                w.Write((byte)0);                  // reserved
                w.Write((ushort)1);                // planes
                w.Write((ushort)32);               // bpp
                w.Write(frames[i].Length);
                w.Write(offset);
                offset += frames[i].Length;
            }
            for (int i = 0; i < frames.Length; i++)
                w.Write(frames[i]);
            w.Flush();
        }

        private static byte[] EncodePng(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private static byte[] EncodeDib(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            int maskStride = ((w + 31) / 32) * 4;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                // BITMAPINFOHEADER
                bw.Write(40);
                bw.Write(w);
                bw.Write(h * 2);       // XOR + AND masks
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(0);           // BI_RGB
                bw.Write(w * h * 4 + maskStride * h);
                bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[w * 4];
                    for (int y = h - 1; y >= 0; y--) // bottom-up
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), row, 0, row.Length);
                        bw.Write(row);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                // AND mask (all zero – alpha channel is used instead)
                bw.Write(new byte[maskStride * h]);
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
