using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class MainForm : Form
    {
        private readonly BatteryManager _manager;
        private TrayManager _trayManager;
        private OverlayForm _overlayForm;

        // Layout panels
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
            if (isVisible)
            {
                _btnOverlay.Text = I18n.T("BtnOverlayActive");
                _btnOverlay.BaseGlassColor = Color.FromArgb(30, 75, 50);
                _btnOverlay.AccentColor = Color.FromArgb(46, 220, 113);
                _btnOverlay.UseAccentBorder = true;
                _btnOverlay.ForeColor = Color.White;
            }
            else
            {
                _btnOverlay.Text = I18n.T("BtnOverlay");
                _btnOverlay.BaseGlassColor = Color.FromArgb(36, 42, 64);
                _btnOverlay.AccentColor = Color.FromArgb(88, 101, 242);
                _btnOverlay.UseAccentBorder = false;
                _btnOverlay.ForeColor = Color.White;
            }
        }

        // ─────────────────────────── Form style ───────────────────────────────
        private void InitializeFormStyle()
        {
            Text = I18n.T("AppTitle");
            Size = new Size(940, 640);
            MinimumSize = new Size(840, 500);
            BackColor = Color.FromArgb(18, 20, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            DoubleBuffered = true;
            try { Icon = AppIconRenderer.CreateIcon(); } catch { }
        }

        // ─────────────────────────── Build UI ─────────────────────────────────
        private void BuildUI()
        {
            SuspendLayout();

            // ── Header Panel com estética AeroGlass ──────────────────────────
            _headerPanel = new Panel
            {
                BackColor = Color.FromArgb(26, 29, 44),
                Padding = new Padding(0)
            };
            _headerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle r = _headerPanel.ClientRectangle;
                if (r.Width <= 0 || r.Height <= 0) return;

                // Fundo gradiente AeroGlass
                using (LinearGradientBrush lgb = new LinearGradientBrush(r, Color.FromArgb(34, 38, 58), Color.FromArgb(22, 25, 38), 90f))
                    g.FillRectangle(lgb, r);

                // Brilho de vidro no topo
                using (Pen tp = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                    g.DrawLine(tp, 0, 0, r.Width, 0);

                // Linha de divisão inferior sutil
                using (Pen bp = new Pen(Color.FromArgb(48, 54, 80), 1f))
                    g.DrawLine(bp, 0, r.Height - 1, r.Width, r.Height - 1);
            };

            PictureBox pbLogo = new PictureBox
            {
                Size = new Size(28, 28),
                Location = new Point(14, 18),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            try { pbLogo.Image = AppIconRenderer.Render(56); } catch { }
            _headerPanel.Controls.Add(pbLogo);

            Label lblTitle = new Label
            {
                Text = "Batuja",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(46, 17)
            };
            _headerPanel.Controls.Add(lblTitle);

            _lblDeviceCount = new Label
            {
                Text = I18n.T("BtnScanning"),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 140, 255),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(lblTitle.Left + lblTitle.PreferredWidth + 10, 24)
            };
            _headerPanel.Controls.Add(_lblDeviceCount);

            bool isAdmin = IsRunningAsAdmin();
            if (!isAdmin)
            {
                _btnAdmin = CreateStyledButton(I18n.T("BtnAdmin"), Color.FromArgb(75, 30, 42));
                _btnAdmin.Size = new Size(80, 34);
                _btnAdmin.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _btnAdmin.ForeColor = Color.FromArgb(255, 130, 130);
                _btnAdmin.AccentColor = Color.FromArgb(255, 90, 90);
                _btnAdmin.UseAccentBorder = true;
                _btnAdmin.Click += OnRestartAsAdmin;
                _headerPanel.Controls.Add(_btnAdmin);
            }

            _btnBackground = CreateStyledButton(I18n.T("BtnBackground"), Color.FromArgb(36, 42, 64));
            _btnBackground.Size = new Size(86, 34);
            _btnBackground.Click += OnBackgroundButtonClick;
            _headerPanel.Controls.Add(_btnBackground);

            bool overlayActive = _manager.Settings.OverlayEnabled;
            _btnOverlay = CreateStyledButton(overlayActive ? I18n.T("BtnOverlayActive") : I18n.T("BtnOverlay"),
                overlayActive ? Color.FromArgb(30, 75, 50) : Color.FromArgb(36, 42, 64));
            _btnOverlay.Size = new Size(96, 34);
            _btnOverlay.AccentColor = overlayActive ? Color.FromArgb(46, 220, 113) : Color.FromArgb(88, 101, 242);
            _btnOverlay.UseAccentBorder = overlayActive;
            _btnOverlay.Click += delegate { _overlayForm.ToggleVisibility(); };
            _headerPanel.Controls.Add(_btnOverlay);

            _btnHistory = CreateStyledButton(I18n.T("BtnHistory"), Color.FromArgb(36, 42, 64));
            _btnHistory.Size = new Size(92, 34);
            _btnHistory.Click += OnHistoryClick;
            _headerPanel.Controls.Add(_btnHistory);

            _btnRefresh = CreateStyledButton(I18n.T("BtnRefresh"), Color.FromArgb(88, 101, 242));
            _btnRefresh.Size = new Size(96, 34);
            _btnRefresh.AccentColor = Color.FromArgb(120, 135, 255);
            _btnRefresh.UseAccentBorder = true;
            _btnRefresh.Click += delegate { _manager.TriggerScanAsync(); };
            _headerPanel.Controls.Add(_btnRefresh);

            _btnSettings = CreateStyledButton(I18n.T("BtnSettings"), Color.FromArgb(36, 42, 64));
            _btnSettings.Size = new Size(104, 34);
            _btnSettings.ShowGearIcon = true;
            _btnSettings.Click += OnSettingsClick;
            _headerPanel.Controls.Add(_btnSettings);

            Controls.Add(_headerPanel);

            // ── Footer Panel com estética AeroGlass ──────────────────────────
            _footerPanel = new Panel
            {
                BackColor = Color.FromArgb(24, 27, 40)
            };
            _footerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle r = _footerPanel.ClientRectangle;
                if (r.Width <= 0 || r.Height <= 0) return;

                using (Pen tp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    g.DrawLine(tp, 0, 0, r.Width, 0);
            };

            _lblStatus = new Label
            {
                Text = I18n.T("Ready"),
                ForeColor = Color.FromArgb(135, 140, 165),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(16, 10)
            };
            _footerPanel.Controls.Add(_lblStatus);

            _chkMinimize = new CheckBox
            {
                Text = I18n.T("MinToTrayOnClose"),
                Checked = _manager.Settings.MinimizeToTrayOnClose,
                ForeColor = Color.FromArgb(160, 165, 190),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(16, 10)
            };
            _chkMinimize.CheckedChanged += delegate
            {
                _manager.Settings.MinimizeToTrayOnClose = _chkMinimize.Checked;
                _manager.Settings.Save();
            };
            _footerPanel.Controls.Add(_chkMinimize);

            Controls.Add(_footerPanel);

            // ── Scroll outer panel (background + scrollbar) ──────────────────
            _scrollPanel = new BgScrollPanel();
            _scrollPanel.BackColor = Color.FromArgb(18, 20, 30);
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
                Padding = new Padding(14, 14, 14, 14)
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
            Relayout();
        }

        private void Relayout()
        {
            if (_headerPanel == null || _footerPanel == null || _scrollPanel == null)
                return;

            const int HEADER_H = 64;
            const int FOOTER_H = 38;

            int cw = ClientSize.Width;
            int ch = ClientSize.Height;
            if (cw <= 0 || ch <= 0) return;

            // Header: full top
            _headerPanel.SetBounds(0, 0, cw, HEADER_H);

            // Reposition header buttons right-aligned
            if (_btnRefresh != null)
                _btnRefresh.Width = _isScanning ? 165 : 96;

            int btnX = cw - 16;
            AeroGlassButton[] buttons = new[]
            {
                _btnSettings, _btnRefresh, _btnHistory, _btnOverlay, _btnBackground, _btnAdmin
            };

            foreach (AeroGlassButton btn in buttons)
            {
                if (btn == null) continue;
                btnX -= btn.Width + 8;
                btn.Location = new Point(btnX, 15);
            }

            // Footer: full bottom
            _footerPanel.SetBounds(0, ch - FOOTER_H, cw, FOOTER_H);

            // Footer checkbox right-aligned
            if (_chkMinimize != null)
                _chkMinimize.Location = new Point(cw - _chkMinimize.Width - 16, 10);

            // Scroll area: everything between header and footer
            int scrollH = Math.Max(60, ch - HEADER_H - FOOTER_H);
            _scrollPanel.SetBounds(0, HEADER_H, cw, scrollH);

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
                Size = new Size(540, 240),
                BackColor = Color.Transparent
            };
            _emptyPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF r = new RectangleF(1f, 1f, _emptyPanel.Width - 2f, _emptyPanel.Height - 2f);
                using (GraphicsPath p = CreateRoundedRect(r, 12f))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 27, 40)))
                        g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(45, 50, 75), 1.2f))
                        g.DrawPath(pen, p);
                }
            };

            PictureBox pbIcon = new PictureBox
            {
                Size = new Size(52, 52),
                Location = new Point(244, 18),
                BackColor = Color.Transparent
            };
            pbIcon.Paint += (s, e) =>
            {
                IconRenderer.DrawDeviceIcon(e.Graphics, new Rectangle(0, 0, 52, 52),
                    DeviceCategory.Generico, Color.FromArgb(88, 101, 242), null);
            };
            _emptyPanel.Controls.Add(pbIcon);

            _lblEmptyMsg = new Label
            {
                Text = I18n.T("NoDevicesTitle"),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 76),
                Size = new Size(540, 32)
            };
            _emptyPanel.Controls.Add(_lblEmptyMsg);

            _lblEmptyDesc = new Label
            {
                Text = I18n.T("NoDevicesDesc1") + "\n" +
                       I18n.T("NoDevicesDesc2") + "\n" +
                       I18n.T("NoDevicesDesc3") + "\n" +
                       I18n.T("NoDevicesDesc4"),
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(145, 150, 180),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(0, 114),
                Size = new Size(540, 110)
            };
            _emptyPanel.Controls.Add(_lblEmptyDesc);
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

            if (_bgImage != null && _isGifAnimated)
                ImageAnimator.StopAnimate(_bgImage, OnFrameChanged);

            _bgImage = null;
            _isGifAnimated = false;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    _bgImage = Image.FromFile(path);
                    _isGifAnimated = ImageAnimator.CanAnimate(_bgImage);
                    if (_isGifAnimated)
                        ImageAnimator.Animate(_bgImage, OnFrameChanged);
                }
                catch { _bgImage = null; }
            }

            _scrollPanel.BgImage = _bgImage;
            _scrollPanel.BgOpacity = _bgOpacity;
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

            ToolStripMenuItem itemChoose = new ToolStripMenuItem("Escolher Imagem (PNG, JPG, GIF, BMP)...");
            itemChoose.Click += delegate
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Escolher Imagem de Fundo";
                    ofd.Filter = "Imagens (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _manager.Settings.BackgroundImagePath = ofd.FileName;
                        _manager.Settings.Save();
                        LoadBackgroundImage();
                    }
                }
            };
            menu.Items.Add(itemChoose);

            if (_bgImage != null)
            {
                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem itemRemove = new ToolStripMenuItem("Remover Imagem (Fundo Padrão Escuro)");
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
                _btnRefresh.Width = 165;
                _lblDeviceCount.Text = I18n.T("BtnScanning");
                _lblStatus.Text = I18n.T("ScanningStatus");
            }
            else
            {
                _btnRefresh.Enabled = true;
                _btnRefresh.Text = I18n.T("BtnRefresh");
                _btnRefresh.Width = 92;
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
                sf.BackColor = Color.FromArgb(24, 27, 40);
                sf.ForeColor = Color.White;
                sf.Font = new Font("Segoe UI", 9f);

                int y = 20;

                Label lbl1 = new Label { Text = I18n.T("RefreshIntervalLabel"), Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lbl1); y += 24;

                NumericUpDown numInt = new NumericUpDown { Location = new Point(20, y), Width = 110, Minimum = 5, Maximum = 3600, Value = _manager.Settings.RefreshIntervalSeconds, BackColor = Color.FromArgb(36, 40, 60), ForeColor = Color.White };
                sf.Controls.Add(numInt); y += 34;

                Label lbl2 = new Label { Text = I18n.T("LowBatteryThresholdLabel"), Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lbl2); y += 24;

                NumericUpDown numThr = new NumericUpDown { Location = new Point(20, y), Width = 110, Minimum = 5, Maximum = 50, Value = _manager.Settings.LowBatteryThreshold, BackColor = Color.FromArgb(36, 40, 60), ForeColor = Color.White };
                sf.Controls.Add(numThr); y += 34;

                CheckBox chkNotif = new CheckBox { Text = I18n.T("EnableNotifLabel"), Checked = _manager.Settings.EnableNotifications, Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(chkNotif); y += 28;

                CheckBox chkBoot = new CheckBox { Text = I18n.T("StartWithWinLabel"), Checked = _manager.Settings.StartWithWindows, Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(chkBoot); y += 28;

                CheckBox chkSys = new CheckBox { Text = I18n.T("ShowSysBatLabel"), Checked = _manager.Settings.ShowSystemBattery, Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(chkSys); y += 32;

                Label lblStyle = new Label { Text = I18n.T("TrayStyleLabel"), Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lblStyle); y += 24;

                ComboBox cmbStyle = new ComboBox
                {
                    Location = new Point(20, y), Width = 260,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(36, 40, 60), ForeColor = Color.White
                };
                TrayIconStyle[] styles = (TrayIconStyle[])Enum.GetValues(typeof(TrayIconStyle));
                foreach (TrayIconStyle st in styles)
                    cmbStyle.Items.Add(TrayManager.GetStyleLabel(st));
                cmbStyle.SelectedIndex = Array.IndexOf(styles, _manager.Settings.TrayStyle);
                sf.Controls.Add(cmbStyle); y += 36;

                // Language selector
                Label lblLang = new Label { Text = I18n.T("LanguageLabel"), Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lblLang); y += 24;

                ComboBox cmbLang = new ComboBox
                {
                    Location = new Point(20, y), Width = 260,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(36, 40, 60), ForeColor = Color.White
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
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 205, 255),
                    Location = new Point(20, y),
                    AutoSize = true
                };
                sf.Controls.Add(lblVersion); y += 24;

                // Check updates button
                AeroGlassButton btnCheckUpdate = CreateStyledButton(I18n.T("CheckUpdatesBtn"), Color.FromArgb(38, 48, 75));
                btnCheckUpdate.Location = new Point(20, y);
                btnCheckUpdate.Size = new Size(260, 32);
                sf.Controls.Add(btnCheckUpdate); y += 36;

                Label lblUpdateStatus = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = Color.FromArgb(160, 165, 195),
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
                            System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; // Tls12
                            using (System.Net.WebClient wc = new System.Net.WebClient())
                            {
                                wc.Headers.Add("User-Agent", "Batuja-App");
                                string json = wc.DownloadString("https://api.github.com/repos/Gartuja/Batuja/releases/latest");
                                string tag = "";
                                int tagIdx = json.IndexOf("\"tag_name\":");
                                if (tagIdx >= 0)
                                {
                                    int startQuote = json.IndexOf('"', tagIdx + 11);
                                    if (startQuote >= 0)
                                    {
                                        int endQuote = json.IndexOf('"', startQuote + 1);
                                        if (endQuote > startQuote)
                                        {
                                            tag = json.Substring(startQuote + 1, endQuote - startQuote - 1);
                                        }
                                    }
                                }

                                sf.BeginInvoke(new Action(delegate
                                {
                                    btnCheckUpdate.Enabled = true;
                                    if (!string.IsNullOrEmpty(tag) && !tag.Equals(AppSettings.AppVersion, StringComparison.OrdinalIgnoreCase))
                                    {
                                        lblUpdateStatus.Text = string.Format("Nova versão disponível: {0}", tag);
                                        if (MessageBox.Show(sf, I18n.T("UpdateAvailableMsg", tag), I18n.T("UpdateTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                                        {
                                            try { Process.Start(AppSettings.GitHubRepoUrl + "/releases"); } catch { }
                                        }
                                    }
                                    else
                                    {
                                        lblUpdateStatus.Text = I18n.T("UpdateLatestMsg", AppSettings.AppVersion);
                                        MessageBox.Show(sf, I18n.T("UpdateLatestMsg", AppSettings.AppVersion), I18n.T("UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    }
                                }));
                            }
                        }
                        catch
                        {
                            sf.BeginInvoke(new Action(delegate
                            {
                                btnCheckUpdate.Enabled = true;
                                lblUpdateStatus.Text = I18n.T("UpdateErrorMsg");
                                MessageBox.Show(sf, I18n.T("UpdateErrorMsg"), I18n.T("UpdateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }));
                        }
                    });
                };

                if (_manager.Settings.HiddenDevices.Count > 0)
                {
                    AeroGlassButton btnRestore = CreateStyledButton(string.Format("Restaurar Dispositivos ({0})", _manager.Settings.HiddenDevices.Count), Color.FromArgb(50, 55, 80));
                    btnRestore.Location = new Point(20, y); btnRestore.Size = new Size(260, 30);
                    btnRestore.Click += delegate { _manager.Settings.HiddenDevices.Clear(); _manager.Settings.Save(); _manager.TriggerScanAsync(); btnRestore.Enabled = false; };
                    sf.Controls.Add(btnRestore); y += 38;
                }

                AeroGlassButton btnSave = CreateStyledButton(I18n.T("BtnSave"), Color.FromArgb(88, 101, 242));
                btnSave.Location = new Point(190, y); btnSave.Size = new Size(95, 32);
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

                AeroGlassButton btnCancel = CreateStyledButton(I18n.T("BtnCancel"), Color.FromArgb(42, 48, 70));
                btnCancel.Location = new Point(295, y); btnCancel.Size = new Size(95, 32);
                btnCancel.Click += delegate { sf.Close(); };
                sf.Controls.Add(btnCancel);

                sf.ShowDialog(this);
            }
        }

        // ─────────────────────────── Helpers ──────────────────────────────────
        private AeroGlassButton CreateStyledButton(string text, Color bg)
        {
            AeroGlassButton btn = new AeroGlassButton
            {
                Text = text,
                BaseGlassColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                CornerRadius = 8
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
                g.Clear(Color.FromArgb(18, 20, 30));

                if (BgImage != null)
                {
                    try
                    {
                        g.DrawImage(BgImage, clip);
                        int alpha = Math.Min(255, Math.Max(0, (int)(BgOpacity * 2.55)));
                        using (SolidBrush tint = new SolidBrush(Color.FromArgb(alpha, 14, 16, 26)))
                            g.FillRectangle(tint, clip);
                    }
                    catch { }
                }
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
            }
        }
    }
}
