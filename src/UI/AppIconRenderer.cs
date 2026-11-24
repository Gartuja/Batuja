using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace OmniBattery.UI
{
    /// <summary>
    /// Draws the MoniTuja application icon: a cute minimalist light-blue wolf.
    /// Also used by build.ps1 (via Add-Type) to generate app.ico for the executable.
    /// </summary>
    public static class AppIconRenderer
    {
        private static readonly Color FurTop      = Color.FromArgb(178, 226, 252); // soft ice blue
        private static readonly Color FurBottom   = Color.FromArgb(110, 190, 240); // deeper sky blue
        private static readonly Color MuzzleColor = Color.FromArgb(245, 250, 255); // pure soft white/cream
        private static readonly Color InnerEar    = Color.FromArgb(250, 195, 205); // soft cute pastel pink
        private static readonly Color Outline     = Color.FromArgb(88, 102, 118);  // sleek slate grey outline
        private static readonly Color DarkDetail  = Color.FromArgb(42, 52, 65);    // eyes & nose
        private static readonly Color BlushColor  = Color.FromArgb(80, 255, 140, 160); // cute cheek blush

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

        /// <summary>Draws the cute wolf inside the given square (designed on a 256 grid).</summary>
        public static void Draw(Graphics g, RectangleF area)
        {
            float k = Math.Min(area.Width, area.Height) / 256f;
            float ox = area.X + (area.Width - 256f * k) / 2f;
            float oy = area.Y + (area.Height - 256f * k) / 2f;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float stroke = Math.Max(1.2f, 9f * k);

            // ── 1. Ears (behind head) ──────────────────────────────────────────
            // Left Ear
            using (GraphicsPath leftEar = new GraphicsPath())
            {
                leftEar.AddPolygon(new[] {
                    new PointF(ox + 45 * k,  oy + 115 * k),
                    new PointF(ox + 48 * k,  oy + 28 * k),  // tip
                    new PointF(ox + 105 * k, oy + 80 * k)
                });
                using (LinearGradientBrush eb = new LinearGradientBrush(
                    new RectangleF(ox + 40 * k, oy + 20 * k, 70 * k, 100 * k),
                    FurTop, FurBottom, LinearGradientMode.Vertical))
                {
                    g.FillPath(eb, leftEar);
                }
                // Inner ear
                using (GraphicsPath leftInner = new GraphicsPath())
                {
                    leftInner.AddPolygon(new[] {
                        new PointF(ox + 56 * k, oy + 105 * k),
                        new PointF(ox + 58 * k, oy + 46 * k),
                        new PointF(ox + 94 * k, oy + 83 * k)
                    });
                    using (SolidBrush pkb = new SolidBrush(InnerEar))
                        g.FillPath(pkb, leftInner);
                }
                using (Pen p = new Pen(Outline, stroke))
                {
                    p.LineJoin = LineJoin.Round;
                    g.DrawPath(p, leftEar);
                }
            }

            // Right Ear
            using (GraphicsPath rightEar = new GraphicsPath())
            {
                rightEar.AddPolygon(new[] {
                    new PointF(ox + 211 * k, oy + 115 * k),
                    new PointF(ox + 208 * k, oy + 28 * k),  // tip
                    new PointF(ox + 151 * k, oy + 80 * k)
                });
                using (LinearGradientBrush eb = new LinearGradientBrush(
                    new RectangleF(ox + 145 * k, oy + 20 * k, 70 * k, 100 * k),
                    FurTop, FurBottom, LinearGradientMode.Vertical))
                {
                    g.FillPath(eb, rightEar);
                }
                // Inner ear
                using (GraphicsPath rightInner = new GraphicsPath())
                {
                    rightInner.AddPolygon(new[] {
                        new PointF(ox + 200 * k, oy + 105 * k),
                        new PointF(ox + 198 * k, oy + 46 * k),
                        new PointF(ox + 162 * k, oy + 83 * k)
                    });
                    using (SolidBrush pkb = new SolidBrush(InnerEar))
                        g.FillPath(pkb, rightInner);
                }
                using (Pen p = new Pen(Outline, stroke))
                {
                    p.LineJoin = LineJoin.Round;
                    g.DrawPath(p, rightEar);
                }
            }

            // ── 2. Head Shape (Rounded & Fluffy Cheeks) ─────────────────────────
            using (GraphicsPath head = new GraphicsPath())
            {
                head.AddArc(ox + 72 * k, oy + 64 * k, 112 * k, 70 * k, 180, 180);
                head.AddBezier(ox + 184 * k, oy + 99 * k,
                               ox + 220 * k, oy + 125 * k,
                               ox + 224 * k, oy + 165 * k,
                               ox + 214 * k, oy + 185 * k);
                head.AddLine(ox + 214 * k, oy + 185 * k, ox + 226 * k, oy + 192 * k);
                head.AddBezier(ox + 226 * k, oy + 192 * k,
                               ox + 216 * k, oy + 208 * k,
                               ox + 185 * k, oy + 236 * k,
                               ox + 128 * k, oy + 238 * k);
                head.AddBezier(ox + 128 * k, oy + 238 * k,
                               ox + 71 * k,  oy + 236 * k,
                               ox + 40 * k,  oy + 208 * k,
                               ox + 30 * k,  oy + 192 * k);
                head.AddLine(ox + 30 * k,  oy + 192 * k, ox + 42 * k,  oy + 185 * k);
                head.AddBezier(ox + 42 * k,  oy + 185 * k,
                               ox + 32 * k,  oy + 165 * k,
                               ox + 36 * k,  oy + 125 * k,
                               ox + 72 * k,  oy + 99 * k);
                head.CloseFigure();

                using (LinearGradientBrush hb = new LinearGradientBrush(
                    new RectangleF(ox + 30 * k, oy + 60 * k, 196 * k, 180 * k),
                    FurTop, FurBottom, LinearGradientMode.Vertical))
                {
                    g.FillPath(hb, head);
                }
                using (Pen p = new Pen(Outline, stroke))
                {
                    p.LineJoin = LineJoin.Round;
                    g.DrawPath(p, head);
                }
            }

            // ── 3. Cute Forehead Tuft Marking ───────────────────────────────────
            if (k * 256f >= 24f)
            {
                using (GraphicsPath tuft = new GraphicsPath())
                {
                    tuft.AddPolygon(new[] {
                        new PointF(ox + 128 * k, oy + 68 * k),
                        new PointF(ox + 138 * k, oy + 88 * k),
                        new PointF(ox + 128 * k, oy + 84 * k),
                        new PointF(ox + 118 * k, oy + 88 * k)
                    });
                    using (SolidBrush wb = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                        g.FillPath(wb, tuft);
                }
            }

            // ── 4. White Muzzle / Snout ─────────────────────────────────────────
            using (GraphicsPath muzzle = new GraphicsPath())
            {
                RectangleF mr = new RectangleF(ox + 82 * k, oy + 144 * k, 92 * k, 76 * k);
                muzzle.AddEllipse(mr);
                using (SolidBrush mb = new SolidBrush(MuzzleColor))
                    g.FillPath(mb, muzzle);
                using (Pen mp = new Pen(Color.FromArgb(170, Outline), Math.Max(1f, 4f * k)))
                    g.DrawPath(mp, muzzle);
            }

            // ── 5. Cute Soft Blush ──────────────────────────────────────────────
            if (k * 256f >= 20f)
            {
                using (SolidBrush bb = new SolidBrush(BlushColor))
                {
                    g.FillEllipse(bb, ox + 54 * k, oy + 162 * k, 24 * k, 15 * k);
                    g.FillEllipse(bb, ox + 178 * k, oy + 162 * k, 24 * k, 15 * k);
                }
            }

            // ── 6. Big Kawaii Eyes with Sparkle ─────────────────────────────────
            float eyeY = oy + 130 * k;
            float eyeW = 19 * k;
            float eyeH = 23 * k;

            // Left Eye
            RectangleF leftEyeRect = new RectangleF(ox + 76 * k, eyeY, eyeW, eyeH);
            using (SolidBrush db = new SolidBrush(DarkDetail))
                g.FillEllipse(db, leftEyeRect);
            // Right Eye
            RectangleF rightEyeRect = new RectangleF(ox + 161 * k, eyeY, eyeW, eyeH);
            using (SolidBrush db = new SolidBrush(DarkDetail))
                g.FillEllipse(db, rightEyeRect);

            if (k * 256f >= 18f)
            {
                using (SolidBrush wb = new SolidBrush(Color.White))
                {
                    g.FillEllipse(wb, ox + 79 * k, eyeY + 3 * k, 8 * k, 9 * k);
                    g.FillEllipse(wb, ox + 164 * k, eyeY + 3 * k, 8 * k, 9 * k);

                    if (k * 256f >= 32f)
                    {
                        g.FillEllipse(wb, ox + 87 * k, eyeY + 14 * k, 4 * k, 4 * k);
                        g.FillEllipse(wb, ox + 172 * k, eyeY + 14 * k, 4 * k, 4 * k);
                    }
                }
            }

            // ── 7. Cute Button Nose ─────────────────────────────────────────────
            float noseW = 20 * k;
            float noseH = 14 * k;
            RectangleF noseRect = new RectangleF(ox + 128 * k - noseW / 2f, oy + 154 * k, noseW, noseH);
            using (GraphicsPath np = new GraphicsPath())
            {
                np.AddArc(noseRect.X, noseRect.Y, noseW / 2f, noseH, 180, 90);
                np.AddArc(noseRect.Right - noseW / 2f, noseRect.Y, noseW / 2f, noseH, 270, 90);
                np.AddBezier(noseRect.Right, noseRect.Y + noseH * 0.6f,
                             noseRect.Right - 3 * k, noseRect.Bottom,
                             noseRect.X + 3 * k, noseRect.Bottom,
                             noseRect.X, noseRect.Y + noseH * 0.6f);
                np.CloseFigure();
                using (SolidBrush nb = new SolidBrush(DarkDetail))
                    g.FillPath(nb, np);

                if (k * 256f >= 24f)
                {
                    using (SolidBrush nhb = new SolidBrush(Color.FromArgb(180, Color.White)))
                        g.FillEllipse(nhb, noseRect.X + 4 * k, noseRect.Y + 2 * k, 6 * k, 3.5f * k);
                }
            }

            // ── 8. Cute Cat/Wolf Mouth (ω shape) ────────────────────────────────
            if (k * 256f >= 24f)
            {
                using (Pen mp = new Pen(DarkDetail, Math.Max(1.2f, 4.5f * k)))
                {
                    mp.StartCap = LineCap.Round;
                    mp.EndCap = LineCap.Round;
                    float my = oy + 168 * k;
                    g.DrawLine(mp, ox + 128 * k, my - 2 * k, ox + 128 * k, my + 6 * k);
                    g.DrawArc(mp, ox + 114 * k, my + 2 * k, 14 * k, 12 * k, 20, 150);
                    g.DrawArc(mp, ox + 128 * k, my + 2 * k, 14 * k, 12 * k, 10, 150);
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
