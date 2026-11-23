using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class OverlayForm : Form
    {
        private readonly BatteryManager _manager;
        private List<BatteryDevice> _devices = new List<BatteryDevice>();

        private bool _isDragging = false;
        private Point _dragStart;
        private Rectangle _closeBtnRect;
        private bool _isCloseHovered = false;
        private bool _isLocked = false;

        public event Action<bool> OverlayStateChanged;

        public OverlayForm(BatteryManager manager)
        {
            _manager = manager;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(16, 18, 27);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            try { Icon = AppIconRenderer.CreateIcon(); } catch { }

            // Restore opacity and position
            Opacity = Math.Max(0.3, Math.Min(1.0, _manager.Settings.OverlayOpacity / 100.0));

            int ox = _manager.Settings.OverlayX;
            int oy = _manager.Settings.OverlayY;
            if (ox >= 0 && oy >= 0)
            {
                Location = new Point(ox, oy);
            }
            else
            {
                // Default: top-right corner of primary screen
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa.Right - 420, wa.Top + 40);
            }

            BuildContextMenu();

            _manager.DevicesUpdated += OnDevicesUpdated;
            UpdateDevicesList(_manager.CurrentDevices);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (não rouba foco de jogos ou vídeos)
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (não polui o Alt+Tab)
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkToolStripRenderer();

            string txtOrient = I18n.CurrentLanguage == AppLanguage.EN ? "Toggle Orientation (Horizontal / Vertical)" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Alternar Orientación (Horizontal / Vertical)" : "Alternar Orientação (Horizontal / Vertical)");
            ToolStripMenuItem itemOrient = new ToolStripMenuItem(txtOrient);
            itemOrient.Click += delegate
            {
                _manager.Settings.OverlayVertical = !_manager.Settings.OverlayVertical;
                _manager.Settings.Save();
                UpdateLayout();
            };
            menu.Items.Add(itemOrient);

            ToolStripSeparator sep1 = new ToolStripSeparator();
            menu.Items.Add(sep1);

            string txtOpacity = I18n.CurrentLanguage == AppLanguage.EN ? "Opacity" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Opacidad" : "Opacidade");
            ToolStripMenuItem itemOpacity = new ToolStripMenuItem(txtOpacity);
            int[] opacities = new int[] { 100, 90, 75, 60, 45 };
            foreach (int op in opacities)
            {
                int val = op;
                ToolStripMenuItem it = new ToolStripMenuItem(val + "%");
                it.Click += delegate
                {
                    _manager.Settings.OverlayOpacity = val;
                    Opacity = val / 100.0;
                    _manager.Settings.Save();
                };
                itemOpacity.DropDownItems.Add(it);
            }
            menu.Items.Add(itemOpacity);

            string txtLock = I18n.CurrentLanguage == AppLanguage.EN ? "Lock Position" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Bloquear Posición" : "Bloquear Posição");
            ToolStripMenuItem itemLock = new ToolStripMenuItem(txtLock);
            itemLock.CheckOnClick = true;
            itemLock.Checked = _isLocked;
            itemLock.CheckedChanged += delegate
            {
                _isLocked = itemLock.Checked;
            };
            menu.Items.Add(itemLock);

            ToolStripSeparator sep2 = new ToolStripSeparator();
            menu.Items.Add(sep2);

            string txtClose = I18n.CurrentLanguage == AppLanguage.EN ? "Hide Overlay" :
                (I18n.CurrentLanguage == AppLanguage.ES ? "Ocultar Superposición" : "Ocultar Sobreposição");
            ToolStripMenuItem itemClose = new ToolStripMenuItem(txtClose);
            itemClose.Click += delegate { HideOverlay(); };
            menu.Items.Add(itemClose);

            ContextMenuStrip = menu;
        }

        private void OnDevicesUpdated(List<BatteryDevice> devices)
        {
            if (InvokeRequired)
            {
                try { Invoke(new Action<List<BatteryDevice>>(OnDevicesUpdated), devices); } catch { }
                return;
            }

            UpdateDevicesList(devices);
        }

        private void UpdateDevicesList(List<BatteryDevice> all)
        {
            List<BatteryDevice> active = new List<BatteryDevice>();
            if (all != null)
            {
                foreach (BatteryDevice dev in all)
                {
                    if (dev != null && dev.IsConnected && !dev.IsHidden)
                    {
                        active.Add(dev);
                    }
                }
            }

            _devices = active;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            bool isVert = _manager.Settings.OverlayVertical;
            int count = Math.Max(1, _devices.Count);

            if (!isVert)
            {
                // Horizontal layout
                int itemW = 164;
                int totalW = 44 + (count * itemW) + 20;
                int totalH = 64;
                Size = new Size(totalW, totalH);
            }
            else
            {
                // Vertical layout
                int itemH = 50;
                int totalW = 210;
                int totalH = 34 + (count * itemH) + 12;
                Size = new Size(totalW, totalH);
            }

            _closeBtnRect = new Rectangle(Width - 22, 6, 16, 16);
            Invalidate();
        }

        public void ToggleVisibility()
        {
            if (Visible)
            {
                HideOverlay();
            }
            else
            {
                ShowOverlay();
            }
        }

        public void ShowOverlay()
        {
            _manager.Settings.OverlayEnabled = true;
            _manager.Settings.Save();
            Show();
            UpdateLayout();
            if (OverlayStateChanged != null) OverlayStateChanged(true);
        }

        public void HideOverlay()
        {
            _manager.Settings.OverlayEnabled = false;
            _manager.Settings.Save();
            Hide();
            if (OverlayStateChanged != null) OverlayStateChanged(false);
        }

        // ── Mouse Drag & Click ───────────────────────────────────────────────
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_closeBtnRect.Contains(e.Location))
                {
                    HideOverlay();
                    return;
                }

                if (!_isLocked)
                {
                    _isDragging = true;
                    _dragStart = new Point(e.X, e.Y);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool ch = _closeBtnRect.Contains(e.Location);
            if (ch != _isCloseHovered)
            {
                _isCloseHovered = ch;
                Invalidate();
            }

            if (_isDragging && !_isLocked)
            {
                Point cur = PointToScreen(e.Location);
                Location = new Point(cur.X - _dragStart.X, cur.Y - _dragStart.Y);
            }
            else
            {
                Cursor = ch ? Cursors.Hand : (_isLocked ? Cursors.Default : Cursors.SizeAll);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                _manager.Settings.OverlayX = Location.X;
                _manager.Settings.OverlayY = Location.Y;
                _manager.Settings.Save();
            }
        }

        // ── Paint HUD ────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath bgp = RoundedRect(rect, 10))
            {
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(235, 16, 18, 27))) g.FillPath(bb, bgp);
                using (Pen bp = new Pen(Color.FromArgb(88, 101, 242), 1.4f)) g.DrawPath(bp, bgp);
            }

            // Close button [x]
            using (Font xf = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Color xc = _isCloseHovered ? Color.FromArgb(255, 100, 100) : Color.FromArgb(110, 115, 140);
                g.DrawString("x", xf, new SolidBrush(xc), _closeBtnRect.X, _closeBtnRect.Y);
            }

            bool isVert = _manager.Settings.OverlayVertical;

            if (_devices.Count == 0)
            {
                using (Font ef = new Font("Segoe UI", 8.5f))
                {
                    g.DrawString(I18n.T("OverlayNoDevices"), ef, new SolidBrush(Color.FromArgb(140, 145, 170)), 14, (Height - 16) / 2);
                }
                return;
            }

            if (!isVert)
            {
                // Horizontal layout
                // Drag handle on left
                using (Font hf = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                {
                    g.DrawString("BATUJA", hf, new SolidBrush(Color.FromArgb(120, 130, 160)), 10, 14);
                    g.DrawString("HUD", hf, new SolidBrush(Color.FromArgb(88, 101, 242)), 10, 26);
                    g.DrawString(":::", hf, new SolidBrush(Color.FromArgb(90, 95, 125)), 12, 42);
                }

                int curX = 48;
                int itemW = 164;
                for (int i = 0; i < _devices.Count; i++)
                {
                    BatteryDevice dev = _devices[i];
                    Color effCol = dev.GetEffectiveColor();

                    // Device Icon
                    Rectangle iconRect = new Rectangle(curX, 12, 40, 40);
                    using (SolidBrush ib = new SolidBrush(Color.FromArgb(35, effCol))) g.FillEllipse(ib, iconRect);
                    using (Pen ip = new Pen(Color.FromArgb(140, effCol), 1.2f)) g.DrawEllipse(ip, iconRect);
                    IconRenderer.DrawDeviceIcon(g, iconRect, dev.EffectiveCategory, effCol, dev.CustomIconPath);

                    // Segmented Battery (image 2 reference)
                    int batX = curX + 46;
                    int batY = 16;
                    DrawSegmentedBattery(g, new Rectangle(batX, batY, 48, 20), dev.BatteryPercent, dev.IsCharging, effCol, dev.IsWired);

                    // Percentage
                    using (Font pf = new Font("Segoe UI", 12f, FontStyle.Bold))
                    {
                        string pct = dev.IsWired ? "USB" : (dev.BatteryPercent >= 0 ? dev.BatteryPercent + "%" : "--");
                        g.DrawString(pct, pf, new SolidBrush(effCol), batX + 54, batY - 1);
                    }

                    // Short Name
                    using (Font nf = new Font("Segoe UI", 7.5f))
                    {
                        string sname = dev.Name;
                        if (sname.Length > 16) sname = sname.Substring(0, 14) + "...";
                        g.DrawString(sname, nf, new SolidBrush(Color.FromArgb(180, 185, 205)), batX, batY + 23);
                    }

                    // Divider line
                    if (i < _devices.Count - 1)
                    {
                        using (Pen dp = new Pen(Color.FromArgb(40, 45, 65), 1f))
                        {
                            g.DrawLine(dp, curX + itemW - 8, 14, curX + itemW - 8, Height - 14);
                        }
                    }

                    curX += itemW;
                }
            }
            else
            {
                // Vertical layout
                using (Font hf = new Font("Segoe UI", 7f, FontStyle.Bold))
                {
                    g.DrawString("BATUJA OVERLAY", hf, new SolidBrush(Color.FromArgb(88, 101, 242)), 14, 10);
                }

                int curY = 30;
                int itemH = 50;
                for (int i = 0; i < _devices.Count; i++)
                {
                    BatteryDevice dev = _devices[i];
                    Color effCol = dev.GetEffectiveColor();

                    // Device Icon
                    Rectangle iconRect = new Rectangle(14, curY + 5, 36, 36);
                    using (SolidBrush ib = new SolidBrush(Color.FromArgb(35, effCol))) g.FillEllipse(ib, iconRect);
                    using (Pen ip = new Pen(Color.FromArgb(140, effCol), 1.2f)) g.DrawEllipse(ip, iconRect);
                    IconRenderer.DrawDeviceIcon(g, iconRect, dev.EffectiveCategory, effCol, dev.CustomIconPath);

                    // Segmented Battery
                    int batX = 58;
                    int batY = curY + 7;
                    DrawSegmentedBattery(g, new Rectangle(batX, batY, 44, 18), dev.BatteryPercent, dev.IsCharging, effCol, dev.IsWired);

                    // Percentage
                    using (Font pf = new Font("Segoe UI", 11f, FontStyle.Bold))
                    {
                        string pct = dev.IsWired ? "USB" : (dev.BatteryPercent >= 0 ? dev.BatteryPercent + "%" : "--");
                        g.DrawString(pct, pf, new SolidBrush(effCol), batX + 49, batY - 1);
                    }

                    // Short Name
                    using (Font nf = new Font("Segoe UI", 7.2f))
                    {
                        string sname = dev.Name;
                        if (sname.Length > 20) sname = sname.Substring(0, 18) + "...";
                        g.DrawString(sname, nf, new SolidBrush(Color.FromArgb(170, 175, 195)), batX, batY + 20);
                    }

                    curY += itemH;
                }
            }
        }

        public static void DrawSegmentedBattery(Graphics g, Rectangle r, int percent, bool isCharging, Color fillCol, bool isWired = false)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int termW = 3;
            int termH = (int)(r.Height * 0.44f);
            int bodyW = r.Width - termW - 2;
            int bodyH = r.Height;

            Rectangle bodyRect = new Rectangle(r.X, r.Y, bodyW, bodyH);
            Rectangle termRect = new Rectangle(r.X + bodyW + 1, r.Y + (bodyH - termH) / 2, termW, termH);

            int radius = Math.Max(2, bodyH / 5);

            // Outer battery body border
            using (GraphicsPath bp = RoundedRect(bodyRect, radius))
            using (Pen pen = new Pen(Color.FromArgb(180, 190, 215), 1.8f))
            {
                g.DrawPath(pen, bp);
            }

            // Terminal tab
            using (GraphicsPath tp = RoundedRect(termRect, 1))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(180, 190, 215)))
            {
                g.FillPath(tb, tp);
            }

            // Segments
            int totalSegments = 6;
            int padX = 3;
            int padY = 3;
            int innerW = bodyW - (padX * 2);
            int innerH = bodyH - (padY * 2);
            int segSpacing = 2;
            int segW = (innerW - (totalSegments - 1) * segSpacing) / totalSegments;

            int activeSegments = 0;
            if (isWired)
            {
                activeSegments = totalSegments;
            }
            else if (percent > 0)
            {
                activeSegments = (int)Math.Round((Math.Min(100, percent) / 100.0) * totalSegments);
                if (activeSegments == 0 && percent > 0) activeSegments = 1;
            }

            for (int i = 0; i < totalSegments; i++)
            {
                int segX = r.X + padX + i * (segW + segSpacing);
                int segY = r.Y + padY;
                Rectangle segRect = new Rectangle(segX, segY, segW, innerH);

                if (i < activeSegments)
                {
                    using (GraphicsPath sp = RoundedRect(segRect, 1))
                    using (SolidBrush sb = new SolidBrush(fillCol))
                    {
                        g.FillPath(sb, sp);
                    }
                }
                else
                {
                    using (GraphicsPath sp = RoundedRect(segRect, 1))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(40, 48, 70)))
                    {
                        g.FillPath(sb, sp);
                    }
                }
            }

            if (isCharging)
            {
                using (Font pf = new Font("Segoe UI", 7f, FontStyle.Bold))
                {
                    g.DrawString("⚡", pf, Brushes.Cyan, r.Right + 3, r.Y + 1);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_manager != null)
                {
                    _manager.DevicesUpdated -= OnDevicesUpdated;
                }
            }
            base.Dispose(disposing);
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
