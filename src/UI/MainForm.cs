using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly BatteryManager _manager;
        private TrayManager _trayManager;
        private OverlayForm _overlayForm;

        // Layout panels
        private Panel _titleBarPanel;
        private int _trafficHover = 0; // 0: none, 1: red, 2: yellow, 3: green
        private Panel _headerPanel;
        private Panel _footerPanel;
        private BgScrollPanel _scrollPanel;      // Outer panel: handles background + scrollbar
        private FlowLayoutPanel _cardsFlow;      // Inner FlowLayout: holds cards

        private Panel _emptyPanel;
        private Label _lblDeviceCount;
        private Label _lblStatus;
        private Label _lblEmptyMsg;
        private Label _lblEmptyDesc;
        private CheckBox _chkMinimize;

        private AeroGlassButton _btnRefresh;
        private AeroGlassButton _btnSettings;
        private AeroGlassButton _btnHistory;
        private AeroGlassButton _btnOverlay;
        private AeroGlassButton _btnBackground;
        private AeroGlassButton _btnAdmin;

        private Image _bgImage = null;
        private bool _isGifAnimated = false;
        private VideoBackgroundPlayer _videoPlayer = null;
        private int _bgOpacity = 75;
        private bool _isScanning = false;

        private readonly Dictionary<string, DeviceCardControl> _cardControls =
            new Dictionary<string, DeviceCardControl>();

        public MainForm(BatteryManager manager)
        {
            _manager = manager;
            _bgOpacity = _manager.Settings.BackgroundOpacity;
            I18n.CurrentLanguage = _manager.Settings.Language;

            InitializeFormStyle();
            BuildUI();
            LoadBackgroundImage();

            _overlayForm = new OverlayForm(_manager);
            _overlayForm.OverlayStateChanged += OnOverlayStateChanged;

            _manager.DevicesUpdated += OnDevicesUpdated;
            _manager.ScanStateChanged += OnScanStateChanged;

            _trayManager = new TrayManager(_manager, this);
            _trayManager.RequestColorChange += OnRequestColorChange;
            _trayManager.RequestToggleOverlay = delegate { _overlayForm.ToggleVisibility(); };

            I18n.LanguageChanged += ApplyLanguage;

            if (_manager.Settings.OverlayEnabled)
            {
                _overlayForm.ShowOverlay();
            }
        }

        private void OnOverlayStateChanged(bool isVisible)
        {
            if (_btnOverlay == null) return;
            _btnOverlay.Text = isVisible ? I18n.T("BtnOverlayActive") : I18n.T("BtnOverlay");
            _btnOverlay.ForeColor = Color.White;
            _btnOverlay.BaseGlassColor = Color.FromArgb(70, 74, 82);
            _btnOverlay.Invalidate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x00040000; // WS_THICKFRAME (habilita redimensionamento e Aero Snap nativo)
                cp.Style |= 0x00020000; // WS_MINIMIZEBOX
                cp.Style |= 0x00010000; // WS_MAXIMIZEBOX
                return cp;
            }
        }

        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                int x = unchecked((short)(m.LParam.ToInt32() & 0xFFFF));
                int y = unchecked((short)((m.LParam.ToInt32() >> 16) & 0xFFFF));
                Point pt = PointToClient(new Point(x, y));

                if (WindowState == FormWindowState.Normal)
                {
                    int border = 6;
                    int corner = 16;

                    if (pt.Y <= border)
                    {
                        if (pt.X <= corner) { m.Result = (IntPtr)HTTOPLEFT; return; }
                        if (pt.X >= ClientSize.Width - corner) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                        m.Result = (IntPtr)HTTOP; return;
                    }
                    if (pt.Y >= ClientSize.Height - border)
                    {
                        if (pt.X <= corner) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                        if (pt.X >= ClientSize.Width - corner) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                        m.Result = (IntPtr)HTBOTTOM; return;
                    }
                    if (pt.X <= border) { m.Result = (IntPtr)HTLEFT; return; }
                    if (pt.X >= ClientSize.Width - border) { m.Result = (IntPtr)HTRIGHT; return; }
                }

                // Área da barra de título para arrastar / duplo clique nativo / Aero Snap
                if (pt.Y >= 0 && pt.Y <= (_titleBarPanel != null ? _titleBarPanel.Height : 22))
                {
                    int rW = ClientSize.Width;
                    int bY = 4, btnH = 14;
                    bool overButtons = new Rectangle(rW - 62, bY, 60, btnH).Contains(pt);
                    if (!overButtons)
                    {
                        m.Result = (IntPtr)HTCAPTION;
                        return;
                    }
                }
                return;
            }

            base.WndProc(ref m);
        }

        // ─────────────────────────── Form style ───────────────────────────────
        private void InitializeFormStyle()
        {
            Text = I18n.T("AppTitle");
            Size = new Size(1000, 560);
            MinimumSize = new Size(860, 480);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(54, 57, 63);
            ForeColor = Color.White;
            Font = new Font("Tahoma", 8.5f, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            DoubleBuffered = true;
            try { Icon = AppIconRenderer.CreateIcon(); } catch { }
        }

        // ─────────────────────────── Build UI ─────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();

            // ── 0. Custom TitleBar com botões clássicos Win98 (_ □ X) ─────────
            _titleBarPanel = new Panel
            {
                BackColor = Color.FromArgb(0, 0, 128),
                Height = 22
            };
            _titleBarPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle r = _titleBarPanel.ClientRectangle;
                if (r.Width <= 0 || r.Height <= 0) return;

                // Win98 Blue Gradient: Navy (#000080) -> Azure (#1084d0)
                Rectangle gradRect = new Rectangle(2, 2, r.Width - 4, r.Height - 2);
                using (LinearGradientBrush lgb = new LinearGradientBrush(gradRect, Color.FromArgb(0, 0, 128), Color.FromArgb(16, 132, 208), 0f))
                    g.FillRectangle(lgb, gradRect);

                // Pequeno ícone do mouse na esquerda
                try
                {
                    Rectangle iconBox = new Rectangle(5, 4, 14, 15);
                    IconRenderer.DrawDeviceIcon(g, iconBox, DeviceCategory.Mouse, Color.White, null);
                }
                catch { }

                // Título da janela
                using (Font tf = new Font("Tahoma", 8.5f, FontStyle.Bold))
                {
                    g.DrawString("Batuja - Monitor de Dispositivos sem Fio", tf, Brushes.White, 23, 4);
                }

                // Botões clássicos de janela: _ □ X com relevo 3D
                int btnW = 16, btnH = 14;
                int bY = 4;
                int rW = r.Width;
                DrawTitleButton(g, new Rectangle(rW - 58, bY, btnW, btnH), "_", _trafficHover == 2);
                DrawTitleButton(g, new Rectangle(rW - 40, bY, btnW, btnH), "□", _trafficHover == 3);
                DrawTitleButton(g, new Rectangle(rW - 22, bY, btnW, btnH), "X", _trafficHover == 1);
            };

            _titleBarPanel.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    int rW = _titleBarPanel.Width;
                    int bY = 4, btnW = 16, btnH = 14;

                    if (new Rectangle(rW - 22, bY, btnW, btnH).Contains(e.Location))
                    {
                        Close();
                        return;
                    }
                    else if (new Rectangle(rW - 58, bY, btnW, btnH).Contains(e.Location))
                    {
                        WindowState = FormWindowState.Minimized;
                        return;
                    }
                    else if (new Rectangle(rW - 40, bY, btnW, btnH).Contains(e.Location))
                    {
                        WindowState = (WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);
                        return;
                    }

                    if (e.Clicks == 2)
                    {
                        WindowState = (WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);
                        return;
                    }

                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };

            _titleBarPanel.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                int rW = _titleBarPanel.Width;
                int bY = 4, btnW = 16, btnH = 14;
                int newH = 0;

                if (new Rectangle(rW - 22, bY, btnW, btnH).Contains(e.Location)) newH = 1;
                else if (new Rectangle(rW - 58, bY, btnW, btnH).Contains(e.Location)) newH = 2;
                else if (new Rectangle(rW - 40, bY, btnW, btnH).Contains(e.Location)) newH = 3;

                if (newH != _trafficHover)
                {
                    _trafficHover = newH;
                    _titleBarPanel.Invalidate();
                }
            };

            _titleBarPanel.MouseLeave += delegate
            {
                if (_trafficHover != 0)
                {
                    _trafficHover = 0;
                    _titleBarPanel.Invalidate();
                }
            };

            Controls.Add(_titleBarPanel);

            // ── 1. Header Panel com estética Clássica Retrô Dark ─────────────────
            _headerPanel = new Panel
            {
                BackColor = Color.FromArgb(54, 57, 63),
                Padding = new Padding(0)
            };
            _headerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle r = _headerPanel.ClientRectangle;
                if (r.Width <= 0 || r.Height <= 0) return;

                using (SolidBrush b = new SolidBrush(Color.FromArgb(54, 57, 63)))
                    g.FillRectangle(b, r);

                // Linha de divisão inferior em baixo-relevo (groove)
                int sepY = r.Height - 2;
                using (Pen pDark = new Pen(Color.FromArgb(28, 30, 34)))
                using (Pen pLight = new Pen(Color.FromArgb(85, 90, 100)))
                {
                    g.DrawLine(pDark, 2, sepY, r.Width - 3, sepY);
                    g.DrawLine(pLight, 2, sepY + 1, r.Width - 3, sepY + 1);
                }
            };

            // Permite arrastar janela e duplo clique para maximizar no cabeçalho
            _headerPanel.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 2)
                    {
                        WindowState = (WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);
                        return;
                    }
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };

            PictureBox pbLogo = new PictureBox
            {
                Size = new Size(22, 26),
                Location = new Point(10, 5),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            try { pbLogo.Image = AppIconRenderer.Render(48); } catch { }
            _headerPanel.Controls.Add(pbLogo);

            Label lblTitle = new Label
            {
                Text = "Batuja",
                Font = new Font("Tahoma", 13.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(36, 7)
            };
            _headerPanel.Controls.Add(lblTitle);

            _lblDeviceCount = new Label
            {
                Text = I18n.T("BtnScanning"),
                Font = new Font("Tahoma", 8.25f, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 225, 235),
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(160, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Location = new Point(114, 10)
            };
            _lblDeviceCount.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (SolidBrush dotB = new SolidBrush(Color.FromArgb(34, 197, 94)))
                    g.FillEllipse(dotB, 2, 5, 8, 8);
                using (Pen dotP = new Pen(Color.FromArgb(0, 100, 0), 1f))
                    g.DrawEllipse(dotP, 2, 5, 8, 8);
            };
            _headerPanel.Controls.Add(_lblDeviceCount);

            bool isAdmin = IsRunningAsAdmin();
            if (!isAdmin)
            {
                _btnAdmin = CreateStyledButton(I18n.T("BtnAdmin"), Color.FromArgb(70, 74, 82));
                _btnAdmin.Size = new Size(68, 22);
                _btnAdmin.ForeColor = Color.White;
                _btnAdmin.IconType = ButtonIconType.Shield;
                _btnAdmin.Click += OnRestartAsAdmin;
                _headerPanel.Controls.Add(_btnAdmin);
            }

            _btnBackground = CreateStyledButton(I18n.T("BtnBackground"), Color.FromArgb(70, 74, 82));
            _btnBackground.Size = new Size(68, 22);
            _btnBackground.ForeColor = Color.White;
            _btnBackground.IconType = ButtonIconType.Palette;
            _btnBackground.Click += OnBackgroundButtonClick;
            _headerPanel.Controls.Add(_btnBackground);

            bool overlayActive = _manager.Settings.OverlayEnabled;
            _btnOverlay = CreateStyledButton(overlayActive ? I18n.T("BtnOverlayActive") : I18n.T("BtnOverlay"),
                Color.FromArgb(70, 74, 82));
            _btnOverlay.Size = new Size(78, 22);
            _btnOverlay.ForeColor = Color.White;
            _btnOverlay.IconType = ButtonIconType.Overlay;
            _btnOverlay.Click += delegate { _overlayForm.ToggleVisibility(); };
            _headerPanel.Controls.Add(_btnOverlay);

            _btnHistory = CreateStyledButton(I18n.T("BtnHistory"), Color.FromArgb(70, 74, 82));
            _btnHistory.Size = new Size(78, 22);
            _btnHistory.ForeColor = Color.White;
            _btnHistory.IconType = ButtonIconType.History;
            _btnHistory.Click += OnHistoryClick;
            _headerPanel.Controls.Add(_btnHistory);

            _btnRefresh = CreateStyledButton(I18n.T("BtnRefresh"), Color.FromArgb(70, 74, 82));
            _btnRefresh.Size = new Size(148, 22);
            _btnRefresh.ForeColor = Color.White;
            _btnRefresh.IconType = ButtonIconType.Refresh;
            _btnRefresh.Click += delegate { _manager.TriggerScanAsync(); };
            _headerPanel.Controls.Add(_btnRefresh);

            _btnSettings = CreateStyledButton(I18n.T("BtnSettings"), Color.FromArgb(70, 74, 82));
            _btnSettings.Size = new Size(76, 22);
            _btnSettings.ForeColor = Color.White;
            _btnSettings.IconType = ButtonIconType.Gear;
            _btnSettings.Click += OnSettingsClick;
            _headerPanel.Controls.Add(_btnSettings);

            Controls.Add(_headerPanel);

            // ── Footer Panel com estética Clássica Retrô Dark ────────────────────
            _footerPanel = new Panel
            {
                BackColor = Color.FromArgb(54, 57, 63)
            };
            _footerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle r = _footerPanel.ClientRectangle;
                if (r.Width <= 0 || r.Height <= 0) return;

                int h = r.Height - 4;
                int p1W = 380;
                int p2W = Math.Max(50, r.Width - p1W - 270);
                int p3X = p1W + p2W + 8;
                int p3W = Math.Max(240, r.Width - p3X - 4);

                Draw3DSunken(g, new Rectangle(2, 2, p1W, h));
                Draw3DSunken(g, new Rectangle(p1W + 5, 2, p2W, h));
                Draw3DSunken(g, new Rectangle(p3X, 2, p3W, h));
            };

            _lblStatus = new Label
            {
                Text = I18n.T("Ready"),
                Font = new Font("Tahoma", 8f),
                ForeColor = Color.FromArgb(220, 225, 235),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(8, 5)
            };
            _footerPanel.Controls.Add(_lblStatus);

            _chkMinimize = new CheckBox
            {
                Text = I18n.T("MinToTrayOnClose"),
                Checked = _manager.Settings.MinimizeToTrayOnClose,
                Font = new Font("Tahoma", 8f),
                ForeColor = Color.FromArgb(220, 225, 235),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(16, 4)
            };
            _chkMinimize.CheckedChanged += delegate
            {
                _manager.Settings.MinimizeToTrayOnClose = _chkMinimize.Checked;
                _manager.Settings.Save();
            };
            _footerPanel.Controls.Add(_chkMinimize);

            Controls.Add(_footerPanel);

            // ── Scroll outer panel ───────────────────────────────────────────
            _scrollPanel = new BgScrollPanel();
            _scrollPanel.BackColor = Color.FromArgb(54, 57, 63);
            _scrollPanel.AutoScroll = true;
            _scrollPanel.MouseWheel += OnScrollPanelMouseWheel;
            Controls.Add(_scrollPanel);

            // ── Inner flow for cards ─────────────────────────────────────────
            _cardsFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 14, 12, 14)
            };
            _scrollPanel.Controls.Add(_cardsFlow);

            BuildEmptyStatePanel();
            _cardsFlow.Controls.Add(_emptyPanel);

            ResumeLayout(false);
            UpdateLayoutBounds();
        }

        private void OnScrollPanelMouseWheel(object sender, MouseEventArgs e)
        {
            int delta = SystemInformation.MouseWheelScrollLines * 20;
            int newVal = _scrollPanel.VerticalScroll.Value - (e.Delta > 0 ? delta : -delta);
            newVal = Math.Max(_scrollPanel.VerticalScroll.Minimum,
                     Math.Min(_scrollPanel.VerticalScroll.Maximum - _scrollPanel.VerticalScroll.LargeChange + 1, newVal));
            _scrollPanel.VerticalScroll.Value = newVal;
            _scrollPanel.PerformLayout();
        }

        private void UpdateLayoutBounds()
        {
            Relayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region = null;
            Relayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw3DRaised(e.Graphics, new Rectangle(0, 0, Width, Height), 2);
        }

        private void Relayout()
        {
            if (_headerPanel == null || _footerPanel == null || _scrollPanel == null)
                return;

            const int TITLE_H = 22;
            const int HEADER_H = 36;
            const int FOOTER_H = 26;

            int cw = ClientSize.Width;
            int ch = ClientSize.Height;
            if (cw <= 0 || ch <= 0) return;

            if (_titleBarPanel != null)
                _titleBarPanel.SetBounds(0, 0, cw, TITLE_H);

            _headerPanel.SetBounds(0, TITLE_H, cw, HEADER_H);

            // Reposition header buttons right-aligned inside header
            if (_btnRefresh != null)
                _btnRefresh.Width = _isScanning ? 160 : 130;

            int btnX = cw - 8;
            AeroGlassButton[] buttons = new[]
            {
                _btnSettings, _btnRefresh, _btnHistory, _btnOverlay, _btnBackground, _btnAdmin
            };

            foreach (AeroGlassButton btn in buttons)
            {
                if (btn == null) continue;
                btnX -= btn.Width + 4;
                btn.Location = new Point(btnX, 6);
            }

            // Reposition device counter badge safely so it never overlaps buttons
            if (_lblDeviceCount != null)
            {
                int leftPos = 114;
                _lblDeviceCount.Location = new Point(leftPos, 8);
                if (!string.IsNullOrEmpty(_lblDeviceCount.Text))
                {
                    using (Graphics g = CreateGraphics())
                    {
                        SizeF sz = g.MeasureString(_lblDeviceCount.Text, _lblDeviceCount.Font);
                        _lblDeviceCount.Width = Math.Max(110, (int)sz.Width + 24);
                    }
                    _lblDeviceCount.Visible = (btnX >= leftPos + _lblDeviceCount.Width + 4);
                }
                else
                {
                    _lblDeviceCount.Visible = false;
                }
            }

            // Footer: full bottom
            _footerPanel.SetBounds(0, ch - FOOTER_H, cw, FOOTER_H);

            // Footer checkbox right-aligned inside pane 3
            if (_chkMinimize != null)
                _chkMinimize.Location = new Point(cw - _chkMinimize.Width - 14, 5);

            // Scroll area: everything between header and footer
            int scrollH = Math.Max(60, ch - TITLE_H - HEADER_H - FOOTER_H);
            _scrollPanel.SetBounds(0, TITLE_H + HEADER_H, cw, scrollH);

            int usableW = _scrollPanel.ClientSize.Width - 28;
            if (usableW > 100)
            {
                _cardsFlow.MaximumSize = new Size(usableW, 0);
                _cardsFlow.Width = usableW;
            }

            CenterEmptyPanel();
        }

        private void CenterEmptyPanel()
        {
            if (_emptyPanel == null || _scrollPanel == null) return;
            int scrollW = _scrollPanel.ClientSize.Width;
            int scrollH = _scrollPanel.ClientSize.Height;
            int x = Math.Max(10, (scrollW - _emptyPanel.Width) / 2);
            int y = Math.Max(20, (scrollH - _emptyPanel.Height) / 2 - 20);
            _emptyPanel.Margin = new Padding(x, y, 0, 0);
        }

        // ─── Empty state panel ────────────────────────────────────────────────
        private void BuildEmptyStatePanel()
        {
            _emptyPanel = new Panel
            {
                Size = new Size(540, 220),
                BackColor = Color.FromArgb(62, 66, 73)
            };
            _emptyPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Draw3DRaised(g, new Rectangle(0, 0, _emptyPanel.Width - 1, _emptyPanel.Height - 1), 2);
            };

            PictureBox pbIcon = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(250, 20),
                BackColor = Color.Transparent
            };
            pbIcon.Paint += (s, e) =>
            {
                Rectangle r = new Rectangle(0, 0, 40, 40);
                Draw3DSunken(e.Graphics, r);
                IconRenderer.DrawDeviceIcon(e.Graphics, r,
                    DeviceCategory.Generico, Color.White, null);
            };
            _emptyPanel.Controls.Add(pbIcon);

            _lblEmptyMsg = new Label
            {
                Text = I18n.T("NoDevicesTitle"),
                Font = new Font("Tahoma", 11.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 70),
                Size = new Size(540, 26)
            };
            _emptyPanel.Controls.Add(_lblEmptyMsg);

            _lblEmptyDesc = new Label
            {
                Text = I18n.T("NoDevicesDesc1") + "\n" +
                       I18n.T("NoDevicesDesc2") + "\n" +
                       I18n.T("NoDevicesDesc3") + "\n" +
                       I18n.T("NoDevicesDesc4"),
                Font = new Font("Tahoma", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(210, 215, 225),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(0, 102),
                Size = new Size(540, 105)
            };
            _emptyPanel.Controls.Add(_lblEmptyDesc);
        }

        private static void Draw3DRaised(Graphics g, Rectangle r, int depth = 2)
        {
            Color cHi = Color.FromArgb(115, 122, 134);
            Color cLi = Color.FromArgb(90, 96, 108);
            Color cSh = Color.FromArgb(25, 27, 32);
            Color cDk = Color.FromArgb(14, 15, 18);

            using (Pen pHi = new Pen(cHi))
            using (Pen pLi = new Pen(cLi))
            using (Pen pSh = new Pen(cSh))
            using (Pen pDk = new Pen(cDk))
            {
                if (depth == 1)
                {
                    g.DrawLine(pHi, r.Left, r.Top, r.Right - 1, r.Top);
                    g.DrawLine(pHi, r.Left, r.Top, r.Left, r.Bottom - 1);
                    g.DrawLine(pDk, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pDk, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                }
                else
                {
                    g.DrawLine(pHi, r.Left, r.Top, r.Right - 1, r.Top);
                    g.DrawLine(pHi, r.Left, r.Top, r.Left, r.Bottom - 1);
                    g.DrawLine(pLi, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                    g.DrawLine(pLi, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                    g.DrawLine(pDk, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pDk, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pSh, r.Right - 2, r.Top + 1, r.Right - 2, r.Bottom - 2);
                    g.DrawLine(pSh, r.Left + 1, r.Bottom - 2, r.Right - 2, r.Bottom - 2);
                }
            }
        }

        private static void Draw3DSunken(Graphics g, Rectangle r)
        {
            Color cHi = Color.FromArgb(115, 122, 134);
            Color cLi = Color.FromArgb(90, 96, 108);
            Color cSh = Color.FromArgb(25, 27, 32);
            Color cDk = Color.FromArgb(14, 15, 18);

            using (Pen pHi = new Pen(cHi))
            using (Pen pLi = new Pen(cLi))
            using (Pen pSh = new Pen(cSh))
            using (Pen pDk = new Pen(cDk))
            {
                g.DrawLine(pSh, r.Left, r.Top, r.Right - 1, r.Top);
                g.DrawLine(pSh, r.Left, r.Top, r.Left, r.Bottom - 1);
                g.DrawLine(pDk, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                g.DrawLine(pDk, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                g.DrawLine(pHi, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pHi, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pLi, r.Right - 2, r.Top + 1, r.Right - 2, r.Bottom - 2);
                g.DrawLine(pLi, r.Left + 1, r.Bottom - 2, r.Right - 2, r.Bottom - 2);
            }
        }

        private static void DrawTitleButton(Graphics g, Rectangle r, string glyph, bool pressed = false)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 74, 82)))
                g.FillRectangle(b, r);

            if (pressed)
            {
                using (Pen pBlack = new Pen(Color.FromArgb(14, 15, 18)))
                using (Pen pDark = new Pen(Color.FromArgb(25, 27, 32)))
                using (Pen pWhite = new Pen(Color.FromArgb(115, 122, 134)))
                {
                    g.DrawLine(pBlack, r.Left, r.Top, r.Right - 1, r.Top);
                    g.DrawLine(pBlack, r.Left, r.Top, r.Left, r.Bottom - 1);
                    g.DrawLine(pDark, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                    g.DrawLine(pDark, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);

                    g.DrawLine(pWhite, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                    g.DrawLine(pWhite, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                }
            }
            else
            {
                Draw3DRaised(g, r, 2);
            }

            int off = pressed ? 1 : 0;
            using (Font f = new Font("Tahoma", 7.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Color.White))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(glyph, f, tb, new Rectangle(r.X + off, r.Y + off - 1, r.Width, r.Height), sf);
            }
        }

        private static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
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

        private void ApplyLanguage()
        {
            Text = I18n.T("AppTitle");

            if (_btnAdmin != null) _btnAdmin.Text = I18n.T("BtnAdmin");
            if (_btnBackground != null) _btnBackground.Text = I18n.T("BtnBackground");
            if (_btnOverlay != null) _btnOverlay.Text = (_overlayForm != null && _overlayForm.Visible) ? I18n.T("BtnOverlayActive") : I18n.T("BtnOverlay");
            if (_btnHistory != null) _btnHistory.Text = I18n.T("BtnHistory");
            if (_btnRefresh != null)
            {
                _btnRefresh.Text = _isScanning ? I18n.T("BtnScanning") : I18n.T("BtnRefresh");
                _btnRefresh.Width = _isScanning ? 165 : 92;
            }
            if (_btnSettings != null) _btnSettings.Text = I18n.T("BtnSettings");
            if (_chkMinimize != null) _chkMinimize.Text = I18n.T("MinToTrayOnClose");

            if (_lblEmptyMsg != null) _lblEmptyMsg.Text = I18n.T("NoDevicesTitle");
            if (_lblEmptyDesc != null)
            {
                _lblEmptyDesc.Text = I18n.T("NoDevicesDesc1") + "\n" +
                                     I18n.T("NoDevicesDesc2") + "\n" +
                                     I18n.T("NoDevicesDesc3") + "\n" +
                                     I18n.T("NoDevicesDesc4");
            }

            if (!_isScanning && _lblStatus != null)
            {
                _lblStatus.Text = I18n.T("LastUpdatedStatus", DateTime.Now, _manager.Settings.RefreshIntervalSeconds);
            }

            Relayout();

            foreach (DeviceCardControl card in _cardControls.Values)
            {
                card.Invalidate();
            }
        }

        // ─────────────────────────── Background ───────────────────────────────
        private void LoadBackgroundImage()
        {
            string path = _manager.Settings.BackgroundImagePath;

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.Dispose();
                _videoPlayer = null;
            }

            if (_bgImage != null && _isGifAnimated)
                ImageAnimator.StopAnimate(_bgImage, OnFrameChanged);

            _bgImage = null;
            _isGifAnimated = false;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".mp4")
                {
                    try
                    {
                        _videoPlayer = new VideoBackgroundPlayer();
                        _videoPlayer.FrameReady += OnVideoFrameReady;
                        _videoPlayer.LoadVideo(path);
                    }
                    catch
                    {
                        if (_videoPlayer != null)
                        {
                            _videoPlayer.Dispose();
                            _videoPlayer = null;
                        }
                    }
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    try
                    {
                        _bgImage = Image.FromFile(path);
                    }
                    catch { _bgImage = null; }
                }
            }

            _scrollPanel.BgImage = _bgImage;
            _scrollPanel.BgOpacity = _bgOpacity;
            _scrollPanel.Invalidate();
        }

        private void OnVideoFrameReady(Bitmap frame)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<Bitmap>(OnVideoFrameReady), frame); } catch { }
                return;
            }

            _scrollPanel.BgImage = frame;
            _scrollPanel.Invalidate();
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            if (InvokeRequired) { BeginInvoke(new EventHandler(OnFrameChanged), sender, e); return; }
            ImageAnimator.UpdateFrames();
            _scrollPanel.Invalidate();
        }

        private void OnBackgroundButtonClick(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkToolStripRenderer();

            ToolStripMenuItem itemChoose = new ToolStripMenuItem("Escolher Imagem ou Vídeo (PNG, JPG, MP4)...");
            itemChoose.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Escolher Fundo (PNG, JPG, MP4)";
                    ofd.Filter = "Arquivos Suportados (*.png;*.jpg;*.jpeg;*.mp4)|*.png;*.jpg;*.jpeg;*.mp4|Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Vídeos (*.mp4)|*.mp4";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _manager.Settings.BackgroundImagePath = ofd.FileName;
                        _manager.Settings.Save();
                        LoadBackgroundImage();
                    }
                }
            };
            menu.Items.Add(itemChoose);

            if (_bgImage != null || (_videoPlayer != null && _videoPlayer.IsPlaying))
            {
                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemRemove = new ToolStripMenuItem(I18n.T("RemoveBackground"));
                itemRemove.Click += delegate
                {
                    _manager.Settings.BackgroundImagePath = "";
                    _manager.Settings.Save();
                    LoadBackgroundImage();
                };
                menu.Items.Add(itemRemove);

                ToolStripMenuItem itemOpacity = new ToolStripMenuItem("Ajustar Escurecimento do Fundo");
                int[] ops = { 20, 40, 55, 70, 85, 95 };
                foreach (int op in ops)
                {
                    int curOp = op;
                    ToolStripMenuItem opItem = new ToolStripMenuItem(string.Format("{0}% de escurecimento", op));
                    opItem.Checked = (_bgOpacity == curOp);
                    opItem.Click += delegate
                    {
                        _bgOpacity = curOp;
                        _manager.Settings.BackgroundOpacity = curOp;
                        _manager.Settings.Save();
                        _scrollPanel.BgOpacity = curOp;
                        _scrollPanel.Invalidate();
                    };
                    itemOpacity.DropDownItems.Add(opItem);
                }
                menu.Items.Add(itemOpacity);
            }

            menu.Show(_btnBackground, new Point(0, _btnBackground.Height + 2));
        }

        private void OnHistoryClick(object sender, EventArgs e)
        {
            HistoryForm hf = new HistoryForm(_manager);
            hf.ShowDialog(this);
        }

        // ─────────────────────────── Scanning / Updates ───────────────────────
        private void OnScanStateChanged(bool isScanning)
        {
            if (InvokeRequired) { Invoke(new Action<bool>(OnScanStateChanged), isScanning); return; }
            _isScanning = isScanning;

            if (isScanning)
            {
                _btnRefresh.Enabled = false;
                _btnRefresh.Text = I18n.T("BtnScanning");
                _btnRefresh.Width = 195;
                _lblDeviceCount.Text = I18n.T("BtnScanning");
                _lblStatus.Text = I18n.T("ScanningStatus");
            }
            else
            {
                _btnRefresh.Enabled = true;
                _btnRefresh.Text = I18n.T("BtnRefresh");
                _btnRefresh.Width = 108;
                _lblStatus.Text = I18n.T("LastUpdatedStatus", DateTime.Now, _manager.Settings.RefreshIntervalSeconds);
            }
            Relayout();
        }

        private void OnDevicesUpdated(List<BatteryDevice> devices)
        {
            if (InvokeRequired) { Invoke(new Action<List<BatteryDevice>>(OnDevicesUpdated), devices); return; }

            int count = (devices != null) ? devices.Count : 0;
            if (count > 0)
                _lblDeviceCount.Text = I18n.T("DevicesCount", count);
            else if (!_isScanning)
                _lblDeviceCount.Text = "";

            Relayout();

            _cardsFlow.SuspendLayout();

            if (devices == null || devices.Count == 0)
            {
                foreach (var kvp in _cardControls)
                    _cardsFlow.Controls.Remove(kvp.Value);
                _cardControls.Clear();

                if (!_cardsFlow.Controls.Contains(_emptyPanel))
                    _cardsFlow.Controls.Add(_emptyPanel);

                _emptyPanel.Visible = true;
                _cardsFlow.ResumeLayout();
                CenterEmptyPanel();
                return;
            }

            _emptyPanel.Visible = false;
            _cardsFlow.Controls.Remove(_emptyPanel);

            HashSet<string> currentKeys = new HashSet<string>();

            foreach (BatteryDevice dev in devices)
            {
                string key = dev.DeviceKey;
                currentKeys.Add(key);

                if (_cardControls.ContainsKey(key))
                {
                    _cardControls[key].UpdateDevice(dev);
                }
                else
                {
                    DeviceCardControl card = new DeviceCardControl(dev);
                    card.RequestColorChange += OnRequestColorChange;
                    card.RequestIconChange  += OnRequestIconChange;
                    card.RequestHideDevice  += OnRequestHideDevice;
                    card.RequestTrayToggle  += OnRequestTrayToggle;

                    _cardControls[key] = card;
                    _cardsFlow.Controls.Add(card);
                }
            }

            List<string> toRemove = new List<string>();
            foreach (var kvp in _cardControls)
            {
                if (!currentKeys.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }

            foreach (string remKey in toRemove)
            {
                _cardsFlow.Controls.Remove(_cardControls[remKey]);
                _cardControls[remKey].Dispose();
                _cardControls.Remove(remKey);
            }

            _cardsFlow.ResumeLayout();
            Relayout();
        }

        private void OnRequestTrayToggle(BatteryDevice dev, bool enabled)
        {
            _manager.SetDeviceTrayIconEnabled(dev, enabled);
        }

        private void OnRequestHideDevice(BatteryDevice dev)
        {
            if (dev == null) return;
            _manager.SetDeviceHidden(dev, true);
            _manager.TriggerScanAsync();
        }

        private void OnRequestColorChange(BatteryDevice dev)
        {
            if (dev == null) return;

            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = dev.GetEffectiveColor();
                cd.FullOpen = true;

                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    _manager.SetCustomDeviceColor(dev, cd.Color);
                    if (_cardControls.ContainsKey(dev.DeviceKey))
                        _cardControls[dev.DeviceKey].Invalidate();
                }
            }
        }

        private void OnRequestIconChange(BatteryDevice dev, string customPath)
        {
            if (dev == null && customPath == null)
            {
                foreach (var card in _cardControls.Values)
                    _manager.ResetDeviceIcon(card.Device);
                _cardsFlow.Invalidate(true);
                return;
            }

            if (dev == null) return;

            if (customPath == null)
            {
                if (dev.CustomCategory.HasValue)
                {
                    string catStr = "cat:" + dev.CustomCategory.Value.ToString();
                    _manager.SetDeviceIcon(dev, catStr);
                }
            }
            else
            {
                _manager.SetDeviceIcon(dev, customPath);
            }

            if (_cardControls.ContainsKey(dev.DeviceKey))
                _cardControls[dev.DeviceKey].Invalidate();
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private void OnRestartAsAdmin(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                Application.Exit();
            }
            catch { }
        }

        // ─────────────────────────── Settings ─────────────────────────────────
        private void OnSettingsClick(object sender, EventArgs e)
        {
            using (Form sf = new Form())
            {
                sf.Text = I18n.T("SettingsTitle");
                sf.Size = new Size(430, 540);
                sf.StartPosition = FormStartPosition.CenterParent;
                sf.FormBorderStyle = FormBorderStyle.FixedDialog;
                sf.MaximizeBox = false; sf.MinimizeBox = false;
                sf.BackColor = Color.FromArgb(54, 57, 63);
                sf.ForeColor = Color.White;
                sf.Font = new Font("Tahoma", 8.5f);

                int y = 20;

                Label lbl1 = new Label { Text = I18n.T("RefreshIntervalLabel"), Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(lbl1); y += 24;

                NumericUpDown numInt = new NumericUpDown { Location = new Point(20, y), Width = 110, Minimum = 5, Maximum = 3600, Value = _manager.Settings.RefreshIntervalSeconds, BackColor = Color.FromArgb(40, 43, 48), ForeColor = Color.White };
                sf.Controls.Add(numInt); y += 34;

                Label lbl2 = new Label { Text = I18n.T("LowBatteryThresholdLabel"), Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(lbl2); y += 24;

                NumericUpDown numThr = new NumericUpDown { Location = new Point(20, y), Width = 110, Minimum = 5, Maximum = 50, Value = _manager.Settings.LowBatteryThreshold, BackColor = Color.FromArgb(40, 43, 48), ForeColor = Color.White };
                sf.Controls.Add(numThr); y += 34;

                CheckBox chkNotif = new CheckBox { Text = I18n.T("EnableNotifLabel"), Checked = _manager.Settings.EnableNotifications, Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(chkNotif); y += 28;

                CheckBox chkBoot = new CheckBox { Text = I18n.T("StartWithWinLabel"), Checked = _manager.Settings.StartWithWindows, Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(chkBoot); y += 28;

                CheckBox chkSys = new CheckBox { Text = I18n.T("ShowSysBatLabel"), Checked = _manager.Settings.ShowSystemBattery, Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(chkSys); y += 32;

                Label lblStyle = new Label { Text = I18n.T("TrayStyleLabel"), Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(lblStyle); y += 24;

                ComboBox cmbStyle = new ComboBox
                {
                    Location = new Point(20, y), Width = 260,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(40, 43, 48), ForeColor = Color.White
                };
                TrayIconStyle[] styles = (TrayIconStyle[])Enum.GetValues(typeof(TrayIconStyle));
                foreach (TrayIconStyle st in styles)
                    cmbStyle.Items.Add(TrayManager.GetStyleLabel(st));
                cmbStyle.SelectedIndex = Array.IndexOf(styles, _manager.Settings.TrayStyle);
                sf.Controls.Add(cmbStyle); y += 36;

                // Language selector
                Label lblLang = new Label { Text = I18n.T("LanguageLabel"), Location = new Point(20, y), AutoSize = true, ForeColor = Color.White };
                sf.Controls.Add(lblLang); y += 24;

                ComboBox cmbLang = new ComboBox
                {
                    Location = new Point(20, y), Width = 260,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(40, 43, 48), ForeColor = Color.White
                };
                cmbLang.Items.Add("Português (PT)");
                cmbLang.Items.Add("English (EN)");
                cmbLang.Items.Add("Español (ES)");
                cmbLang.SelectedIndex = (int)_manager.Settings.Language;
                sf.Controls.Add(cmbLang); y += 38;

                // Version display
                Label lblVersion = new Label
                {
                    Text = I18n.T("AppVersionLabel", AppSettings.AppVersion),
                    Font = new Font("Tahoma", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(70, 190, 255),
                    Location = new Point(20, y),
                    AutoSize = true
                };
                sf.Controls.Add(lblVersion); y += 24;

                // Check updates button
                AeroGlassButton btnCheckUpdate = CreateStyledButton(I18n.T("CheckUpdatesBtn"), Color.FromArgb(70, 74, 82));
                btnCheckUpdate.Location = new Point(20, y);
                btnCheckUpdate.Size = new Size(260, 26);
                sf.Controls.Add(btnCheckUpdate); y += 32;

                Label lblUpdateStatus = new Label
                {
                    Text = "",
                    Font = new Font("Tahoma", 8f),
                    ForeColor = Color.FromArgb(220, 225, 235),
                    Location = new Point(20, y),
                    Size = new Size(380, 20)
                };
                sf.Controls.Add(lblUpdateStatus); y += 26;

                btnCheckUpdate.Click += delegate
                {
                    btnCheckUpdate.Enabled = false;
                    lblUpdateStatus.Text = I18n.T("CheckingUpdates");
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072 // Tls12
                                | (System.Net.SecurityProtocolType)768 // Tls11
                                | (System.Net.SecurityProtocolType)192; // Tls
                            string tag = "";

                            using (System.Net.WebClient wc = new System.Net.WebClient())
                            {
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Batuja/" + AppSettings.AppVersion);
                                wc.Headers.Add("Accept", "application/vnd.github.v3+json");

                                // 1. Tenta obter a release mais recente no GitHub
                                try
                                {
                                    string json = wc.DownloadString("https://api.github.com/repos/Gartuja/Batuja/releases/latest");
                                    int tagIdx = json.IndexOf("\"tag_name\":");
                                    if (tagIdx >= 0)
                                    {
                                        int startQuote = json.IndexOf('"', tagIdx + 11);
                                        if (startQuote >= 0)
                                        {
                                            int endQuote = json.IndexOf('"', startQuote + 1);
                                            if (endQuote > startQuote)
                                                tag = json.Substring(startQuote + 1, endQuote - startQuote - 1);
                                        }
                                    }
                                }
                                catch (System.Net.WebException) { }

                                // 2. Caso não haja releases formais, verifica tags do repositório
                                if (string.IsNullOrEmpty(tag))
                                {
                                    try
                                    {
                                        wc.Headers.Clear();
                                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Batuja/" + AppSettings.AppVersion);
                                        wc.Headers.Add("Accept", "application/vnd.github.v3+json");
                                        string tagsJson = wc.DownloadString("https://api.github.com/repos/Gartuja/Batuja/tags");
                                        int nameIdx = tagsJson.IndexOf("\"name\":");
                                        if (nameIdx >= 0)
                                        {
                                            int startQuote = tagsJson.IndexOf('"', nameIdx + 7);
                                            if (startQuote >= 0)
                                            {
                                                int endQuote = tagsJson.IndexOf('"', startQuote + 1);
                                                if (endQuote > startQuote)
                                                    tag = tagsJson.Substring(startQuote + 1, endQuote - startQuote - 1);
                                            }
                                        }
                                    }
                                    catch (System.Net.WebException) { }
                                }

                                sf.BeginInvoke(new Action(delegate
                                {
                                    btnCheckUpdate.Enabled = true;
                                    if (!string.IsNullOrEmpty(tag) &&
                                        !tag.Equals(AppSettings.AppVersion, StringComparison.OrdinalIgnoreCase) &&
                                        !tag.Equals(AppSettings.AppVersion.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase))
                                    {
                                        lblUpdateStatus.Text = string.Format("Nova versão disponível: {0}", tag);
                                        if (MessageBox.Show(sf, I18n.T("UpdateAvailableMsg", tag), I18n.T("UpdateTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                                        {
                                            try { Process.Start(AppSettings.GitHubRepoUrl + "/releases"); } catch { }
                                        }
                                    }
                                    else
                                    {
                                        string currentVer = !string.IsNullOrEmpty(tag) ? tag : AppSettings.AppVersion;
                                        lblUpdateStatus.Text = string.Format("Batuja {0} é a versão mais recente.", currentVer);
                                        MessageBox.Show(sf, string.Format("Você já está na versão mais recente ({0}).\nNenhuma atualização necessária.", currentVer), I18n.T("UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    }
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            sf.BeginInvoke(new Action(delegate
                            {
                                btnCheckUpdate.Enabled = true;
                                lblUpdateStatus.Text = "Erro ao verificar atualizações.";
                                MessageBox.Show(sf, "Não foi possível verificar atualizações no momento.\nVerifique sua conexão à internet.\n(" + ex.Message + ")", I18n.T("UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }));
                        }
                    });
                };

                AeroGlassButton btnSave = CreateStyledButton(I18n.T("BtnSave"), Color.FromArgb(70, 74, 82));
                btnSave.Location = new Point(190, y); btnSave.Size = new Size(95, 26);
                btnSave.Click += delegate
                {
                    _manager.Settings.RefreshIntervalSeconds = (int)numInt.Value;
                    _manager.Settings.LowBatteryThreshold = (int)numThr.Value;
                    _manager.Settings.EnableNotifications = chkNotif.Checked;
                    _manager.Settings.StartWithWindows = chkBoot.Checked;
                    _manager.Settings.ShowSystemBattery = chkSys.Checked;

                    if (cmbLang.SelectedIndex >= 0)
                    {
                        AppLanguage newLang = (AppLanguage)cmbLang.SelectedIndex;
                        _manager.Settings.Language = newLang;
                        I18n.SetLanguage(newLang);
                    }

                    _manager.Settings.Save();
                    AutostartManager.SetAutostart(chkBoot.Checked);
                    _manager.UpdateRefreshInterval((int)numInt.Value);
                    if (cmbStyle.SelectedIndex >= 0 && styles[cmbStyle.SelectedIndex] != _manager.Settings.TrayStyle)
                        _trayManager.ApplyTrayStyle(styles[cmbStyle.SelectedIndex]);
                    sf.Close();
                };
                sf.Controls.Add(btnSave);

                AeroGlassButton btnCancel = CreateStyledButton(I18n.T("BtnCancel"), Color.FromArgb(70, 74, 82));
                btnCancel.Location = new Point(295, y); btnCancel.Size = new Size(95, 26);
                btnCancel.Click += delegate { sf.Close(); };
                sf.Controls.Add(btnCancel);

                sf.ShowDialog(this);
            }
        }

        // ─────────────────────────── Helpers ──────────────────────────────────
        private AeroGlassButton CreateStyledButton(string text, Color bg)
        {
            Color baseCol = bg.IsEmpty ? Color.FromArgb(70, 74, 82) : bg;
            AeroGlassButton btn = new AeroGlassButton
            {
                Text = text,
                BaseGlassColor = baseCol,
                ForeColor = Color.White,
                Font = new Font("Tahoma", 8.25f, FontStyle.Regular),
                Classic3D = true,
                CornerRadius = 0
            };
            return btn;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && _manager.Settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);

            if (e.CloseReason != CloseReason.ApplicationExitCall &&
                e.CloseReason != CloseReason.WindowsShutDown &&
                e.CloseReason != CloseReason.TaskManagerClosing)
            {
                if (_overlayForm != null && !_overlayForm.IsDisposed)
                {
                    _overlayForm.Close();
                }
            }

            if (_trayManager != null)
            {
                _trayManager.Dispose();
                _trayManager = null;
            }

            if (_videoPlayer != null)
            {
                _videoPlayer.Dispose();
                _videoPlayer = null;
            }

            if (_bgImage != null && _isGifAnimated)
            {
                ImageAnimator.StopAnimate(_bgImage, OnFrameChanged);
            }
        }

        // ─────────────────── BgScrollPanel ────────────────────────────────────
        private class BgScrollPanel : Panel
        {
            public Image BgImage { get; set; }
            public int BgOpacity { get; set; }

            public BgScrollPanel()
            {
                BgOpacity = 75;
                SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle clip = new Rectangle(0, 0, Width, Height);
                if (clip.Width <= 0 || clip.Height <= 0) return;

                if (BgImage != null)
                {
                    g.Clear(Color.FromArgb(54, 57, 63));
                    try
                    {
                        g.DrawImage(BgImage, clip);
                        int alpha = Math.Min(255, Math.Max(0, (int)(BgOpacity * 2.55)));
                        using (SolidBrush tint = new SolidBrush(Color.FromArgb(alpha, 54, 57, 63)))
                            g.FillRectangle(tint, clip);
                    }
                    catch { }
                }
                else
                {
                    using (SolidBrush gb = new SolidBrush(Color.FromArgb(54, 57, 63)))
                    {
                        g.FillRectangle(gb, clip);
                    }
                }
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
            }
        }
    }
}
