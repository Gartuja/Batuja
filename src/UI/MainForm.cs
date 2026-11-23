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
        private Button _btnRefresh;
        private Button _btnSettings;
        private Button _btnHistory;
        private Button _btnOverlay;
        private Button _btnBackground;
        private Button _btnAdmin;

        private Image _bgImage = null;
        private bool _isGifAnimated = false;
        private int _bgOpacity = 75;

        private readonly Dictionary<string, DeviceCardControl> _cardControls =
            new Dictionary<string, DeviceCardControl>();

        public MainForm(BatteryManager manager)
        {
            _manager = manager;
            _bgOpacity = _manager.Settings.BackgroundOpacity;

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
                _btnOverlay.Text = "● Sobrepor";
                _btnOverlay.BackColor = Color.FromArgb(46, 204, 113);
                _btnOverlay.ForeColor = Color.Black;
            }
            else
            {
                _btnOverlay.Text = "Sobrepor";
                _btnOverlay.BackColor = Color.FromArgb(45, 49, 72);
                _btnOverlay.ForeColor = Color.White;
            }
        }

        // ─────────────────────────── Form style ───────────────────────────────
        private void InitializeFormStyle()
        {
            Text = "Batuja - Monitor de Dispositivos sem Fio";
            Size = new Size(880, 640);
            MinimumSize = new Size(650, 480);
            BackColor = Color.FromArgb(20, 22, 32);
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

            // ── Header ──────────────────────────────────────────────────────
            _headerPanel = new Panel
            {
                BackColor = Color.FromArgb(28, 31, 46),
                Padding = new Padding(0)
            };

            PictureBox pbLogo = new PictureBox
            {
                Size = new Size(28, 28),
                Location = new Point(14, 16),
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
                AutoSize = true,
                Location = new Point(46, 16)
            };
            _headerPanel.Controls.Add(lblTitle);

            _lblDeviceCount = new Label
            {
                Text = "Varrendo...",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(88, 101, 242),
                AutoSize = true,
                Location = new Point(lblTitle.Left + lblTitle.PreferredWidth + 10, 23)
            };
            _headerPanel.Controls.Add(_lblDeviceCount);

            bool isAdmin = IsRunningAsAdmin();
            if (!isAdmin)
            {
                _btnAdmin = CreateStyledButton("Admin", Color.FromArgb(60, 40, 45));
                _btnAdmin.Size = new Size(68, 32);
                _btnAdmin.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                _btnAdmin.ForeColor = Color.FromArgb(255, 120, 120);
                _btnAdmin.Click += OnRestartAsAdmin;
                _headerPanel.Controls.Add(_btnAdmin);
            }

            _btnBackground = CreateStyledButton("Fundo", Color.FromArgb(45, 49, 72));
            _btnBackground.Size = new Size(72, 32);
            _btnBackground.Click += OnBackgroundButtonClick;
            _headerPanel.Controls.Add(_btnBackground);

            _btnOverlay = CreateStyledButton(_manager.Settings.OverlayEnabled ? "● Sobrepor" : "Sobrepor",
                _manager.Settings.OverlayEnabled ? Color.FromArgb(46, 204, 113) : Color.FromArgb(45, 49, 72));
            if (_manager.Settings.OverlayEnabled) _btnOverlay.ForeColor = Color.Black;
            _btnOverlay.Size = new Size(88, 32);
            _btnOverlay.Click += delegate { _overlayForm.ToggleVisibility(); };
            _headerPanel.Controls.Add(_btnOverlay);

            _btnHistory = CreateStyledButton("Histórico", Color.FromArgb(45, 49, 72));
            _btnHistory.Size = new Size(88, 32);
            _btnHistory.Click += OnHistoryClick;
            _headerPanel.Controls.Add(_btnHistory);

            _btnRefresh = CreateStyledButton("Atualizar", Color.FromArgb(88, 101, 242));
            _btnRefresh.Size = new Size(90, 32);
            _btnRefresh.Click += delegate { _manager.TriggerScanAsync(); };
            _headerPanel.Controls.Add(_btnRefresh);

            _btnSettings = CreateStyledButton("Configs", Color.FromArgb(45, 49, 72));
            _btnSettings.Size = new Size(80, 32);
            _btnSettings.Click += OnSettingsClick;
            _headerPanel.Controls.Add(_btnSettings);

            Controls.Add(_headerPanel);

            // ── Footer ──────────────────────────────────────────────────────
            _footerPanel = new Panel
            {
                BackColor = Color.FromArgb(28, 31, 46)
            };

            _lblStatus = new Label
            {
                Text = "Pronto",
                ForeColor = Color.FromArgb(130, 135, 160),
                AutoSize = true,
                Location = new Point(16, 10)
            };
            _footerPanel.Controls.Add(_lblStatus);

            CheckBox chkMinimize = new CheckBox
            {
                Text = "Minimizar para a bandeja ao fechar",
                Checked = _manager.Settings.MinimizeToTrayOnClose,
                ForeColor = Color.FromArgb(160, 165, 190),
                AutoSize = true,
                Location = new Point(16, 10)
            };
            chkMinimize.CheckedChanged += delegate
            {
                _manager.Settings.MinimizeToTrayOnClose = chkMinimize.Checked;
                _manager.Settings.Save();
            };
            _footerPanel.Controls.Add(chkMinimize);

            Controls.Add(_footerPanel);

            // ── Scroll outer panel (background + scrollbar) ──────────────────
            _scrollPanel = new BgScrollPanel();
            _scrollPanel.BackColor = Color.FromArgb(20, 22, 32);
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
            // Forward wheel event to the scroll panel (needed when inner controls eat it)
            int delta = SystemInformation.MouseWheelScrollLines * 20;
            int newVal = _scrollPanel.VerticalScroll.Value - (e.Delta > 0 ? delta : -delta);
            newVal = Math.Max(_scrollPanel.VerticalScroll.Minimum,
                     Math.Min(_scrollPanel.VerticalScroll.Maximum - _scrollPanel.VerticalScroll.LargeChange + 1, newVal));
            _scrollPanel.VerticalScroll.Value = newVal;
            _scrollPanel.PerformLayout();
        }

        // ─────────────────────────── Layout ───────────────────────────────────
        private void UpdateLayoutBounds()
        {
            if (_headerPanel == null || _footerPanel == null || _scrollPanel == null)
                return;

            // Skip when minimized (ClientSize can be 0)
            if (WindowState == FormWindowState.Minimized)
                return;

            const int HEADER_H = 64;
            const int FOOTER_H = 36;
            int cw = ClientSize.Width;
            int ch = ClientSize.Height;

            if (cw <= 0 || ch <= 0) return;

            // Header: full top
            _headerPanel.SetBounds(0, 0, cw, HEADER_H);

            // Reposition header buttons right-aligned
            int btnX = cw - 14;
            foreach (Button btn in new[] { _btnSettings, _btnRefresh, _btnHistory, _btnOverlay, _btnBackground, _btnAdmin })
            {
                if (btn == null) continue;
                btnX -= btn.Width + 6;
                btn.Location = new Point(btnX, 16);
            }

            // Footer: full bottom
            _footerPanel.SetBounds(0, ch - FOOTER_H, cw, FOOTER_H);

            // Footer checkbox right-aligned
            Control chk = _footerPanel.Controls.Count > 1 ? _footerPanel.Controls[1] : null;
            if (chk != null)
                chk.Location = new Point(cw - chk.Width - 16, 10);

            // Scroll area: everything between header and footer
            int scrollH = Math.Max(60, ch - HEADER_H - FOOTER_H);
            _scrollPanel.SetBounds(0, HEADER_H, cw, scrollH);

            // Cards flow width matches scroll panel (minus scrollbar)
            _cardsFlow.MaximumSize = new Size(Math.Max(100, cw - 20), 0);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutBounds();
        }

        // ─────────────────────────── Empty State ──────────────────────────────
        private void BuildEmptyStatePanel()
        {
            _emptyPanel = new Panel
            {
                Size = new Size(540, 240),
                BackColor = Color.Transparent,
                Margin = new Padding(30)
            };

            // Draw a battery icon via GDI instead of emoji to avoid glyph boxes
            PictureBox pbIcon = new PictureBox
            {
                Size = new Size(60, 60),
                BackColor = Color.Transparent,
                Location = new Point(240, 10)
            };
            pbIcon.Paint += delegate(object s, PaintEventArgs pe)
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                IconRenderer.DrawDeviceIcon(pe.Graphics,
                    new Rectangle(0, 0, 60, 60),
                    DeviceCategory.Generico,
                    Color.FromArgb(88, 101, 242), null);
            };
            _emptyPanel.Controls.Add(pbIcon);

            Label lblEmptyMsg = new Label
            {
                Text = "Nenhum dispositivo sem fio detectado",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 76),
                Size = new Size(540, 32)
            };
            _emptyPanel.Controls.Add(lblEmptyMsg);

            Label lblEmptyDesc = new Label
            {
                Text = "• Conecte seus receptores USB 2.4GHz (MCHOSE V9 Turbo, Attack Shark RS6, VK 87, Logitech...)\n" +
                       "• Certifique-se de que os dispositivos estão ligados e o receptor USB encaixado.\n" +
                       "• Clique em 'Atualizar' para fazer uma nova varredura agora.\n" +
                       "• Cada dispositivo ganhará seu ícone com % na bandeja do sistema!",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(140, 145, 175),
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(0, 114),
                Size = new Size(540, 100)
            };
            _emptyPanel.Controls.Add(lblEmptyDesc);
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

                ToolStripMenuItem itemRemove = new ToolStripMenuItem("Remover Imagem (Fundo Padrao Escuro)");
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
                    opItem.Checked = (_bgOpacity == op);
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

        // ─────────────────────────── Events ───────────────────────────────────
        private void OnScanStateChanged(bool isScanning)
        {
            if (InvokeRequired) { Invoke(new Action<bool>(OnScanStateChanged), isScanning); return; }

            if (isScanning)
            {
                _btnRefresh.Enabled = false;
                _btnRefresh.Text = "Varrendo...";
                _lblStatus.Text = "Buscando perifericos sem fio...";
            }
            else
            {
                _btnRefresh.Enabled = true;
                _btnRefresh.Text = "Atualizar";
                _lblStatus.Text = string.Format("Ultima atualizacao as {0:HH:mm:ss} (Proxima em {1}s)",
                    DateTime.Now, _manager.Settings.RefreshIntervalSeconds);
            }
        }

        private void OnDevicesUpdated(List<BatteryDevice> devices)
        {
            if (InvokeRequired) { Invoke(new Action<List<BatteryDevice>>(OnDevicesUpdated), devices); return; }

            int count = (devices != null) ? devices.Count : 0;
            _lblDeviceCount.Text = string.Format("{0} dispositivo(s)", count);

            _cardsFlow.SuspendLayout();

            if (devices == null || devices.Count == 0)
            {
                _cardsFlow.Controls.Clear();
                _cardControls.Clear();
                _cardsFlow.Controls.Add(_emptyPanel);
                _cardsFlow.ResumeLayout(true);
                return;
            }

            if (_cardsFlow.Controls.Contains(_emptyPanel))
                _cardsFlow.Controls.Remove(_emptyPanel);

            HashSet<string> currentKeys = new HashSet<string>();
            foreach (BatteryDevice dev in devices)
            {
                if (!dev.IsConnected)
                    continue;

                string key = !string.IsNullOrEmpty(dev.Id) ? dev.Id : dev.Name;
                currentKeys.Add(key);

                if (_cardControls.ContainsKey(key))
                {
                    _cardControls[key].UpdateDevice(dev);
                }
                else
                {
                    DeviceCardControl card = new DeviceCardControl(dev);
                    card.RequestColorChange += OnRequestColorChange;
                    card.RequestIconChange += OnCardRequestIconChange;
                    card.RequestHideDevice += OnCardRequestHideDevice;
                    card.RequestTrayToggle += OnCardRequestTrayToggle;
                    _cardControls[key] = card;
                    _cardsFlow.Controls.Add(card);
                }
            }

            List<string> toRemove = new List<string>();
            foreach (string k in _cardControls.Keys)
                if (!currentKeys.Contains(k)) toRemove.Add(k);

            foreach (string rk in toRemove)
            {
                _cardsFlow.Controls.Remove(_cardControls[rk]);
                _cardControls[rk].Dispose();
                _cardControls.Remove(rk);
            }

            _cardsFlow.ResumeLayout(true);
        }

        private void OnRequestColorChange(BatteryDevice dev)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = dev.GetEffectiveColor();
                cd.FullOpen = true;
                if (cd.ShowDialog(this) == DialogResult.OK)
                    _manager.SetCustomDeviceColor(dev, cd.Color);
            }
        }

        private void OnCardRequestIconChange(BatteryDevice dev, string customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
                _manager.SetDeviceIcon(dev, customPath);
            else if (dev != null && dev.CustomCategory.HasValue)
                _manager.SetDeviceIcon(dev, dev.CustomCategory.Value.ToString());
            else if (dev == null)
                _manager.ResetDeviceIcon(dev);
        }

        private void OnCardRequestHideDevice(BatteryDevice dev)
        {
            DialogResult dr = MessageBox.Show(
                string.Format("Deseja desligar '{0}'?\n\nEle sairá da tela de dispositivos conectados e poderá ser reativado a qualquer momento no Histórico.", dev.Name),
                "Desligar Dispositivo", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
                _manager.DisconnectDevice(dev);
        }

        private void OnCardRequestTrayToggle(BatteryDevice dev, bool enabled)
        {
            _manager.SetDeviceTrayIconEnabled(dev, enabled);
        }

        private void OnHistoryClick(object sender, EventArgs e)
        {
            using (HistoryForm hf = new HistoryForm(_manager))
            {
                hf.ShowDialog(this);
            }
        }

        // ─────────────────────────── Admin ────────────────────────────────────
        private bool IsRunningAsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
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
                sf.Text = "Configurações - Batuja";
                sf.Size = new Size(400, 530);
                sf.StartPosition = FormStartPosition.CenterParent;
                sf.FormBorderStyle = FormBorderStyle.FixedDialog;
                sf.MaximizeBox = false; sf.MinimizeBox = false;
                sf.BackColor = Color.FromArgb(28, 31, 46);
                sf.ForeColor = Color.White;
                sf.Font = new Font("Segoe UI", 9f);

                int y = 20;

                Label lbl1 = new Label { Text = "Intervalo de Varredura (segundos):", Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lbl1); y += 24;

                NumericUpDown numInt = new NumericUpDown { Location = new Point(20, y), Width = 100, Minimum = 5, Maximum = 3600, Value = _manager.Settings.RefreshIntervalSeconds, BackColor = Color.FromArgb(40, 44, 65), ForeColor = Color.White };
                sf.Controls.Add(numInt); y += 34;

                Label lbl2 = new Label { Text = "Alerta de Bateria Fraca (%):", Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lbl2); y += 24;

                NumericUpDown numThr = new NumericUpDown { Location = new Point(20, y), Width = 100, Minimum = 5, Maximum = 50, Value = _manager.Settings.LowBatteryThreshold, BackColor = Color.FromArgb(40, 44, 65), ForeColor = Color.White };
                sf.Controls.Add(numThr); y += 34;

                CheckBox chkNotif = new CheckBox { Text = "Exibir notificacoes na bandeja", Checked = _manager.Settings.EnableNotifications, Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(chkNotif); y += 28;

                CheckBox chkBoot = new CheckBox { Text = "Iniciar com o Windows", Checked = _manager.Settings.StartWithWindows, Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(chkBoot); y += 28;

                CheckBox chkSys = new CheckBox { Text = "Exibir bateria do Notebook/Sistema", Checked = _manager.Settings.ShowSystemBattery, Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(chkSys); y += 32;

                Label lblStyle = new Label { Text = "Estilo dos ícones da bandeja:", Location = new Point(20, y), AutoSize = true };
                sf.Controls.Add(lblStyle); y += 24;

                ComboBox cmbStyle = new ComboBox
                {
                    Location = new Point(20, y), Width = 240,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(40, 44, 65), ForeColor = Color.White
                };
                TrayIconStyle[] styles = (TrayIconStyle[])Enum.GetValues(typeof(TrayIconStyle));
                foreach (TrayIconStyle st in styles)
                    cmbStyle.Items.Add(TrayManager.GetStyleLabel(st));
                cmbStyle.SelectedIndex = Array.IndexOf(styles, _manager.Settings.TrayStyle);
                sf.Controls.Add(cmbStyle); y += 38;

                if (_manager.Settings.HiddenDevices.Count > 0)
                {
                    Button btnRestore = CreateStyledButton(string.Format("Restaurar Dispositivos Ocultos ({0})", _manager.Settings.HiddenDevices.Count), Color.FromArgb(50, 55, 80));
                    btnRestore.Location = new Point(20, y); btnRestore.Size = new Size(260, 28);
                    btnRestore.Click += delegate { _manager.Settings.HiddenDevices.Clear(); _manager.Settings.Save(); _manager.TriggerScanAsync(); btnRestore.Enabled = false; btnRestore.Text = "Restaurados!"; };
                    sf.Controls.Add(btnRestore); y += 36;
                }

                if (_manager.Settings.DisconnectedDevices.Count > 0)
                {
                    Button btnResetOff = CreateStyledButton(string.Format("Reativar Dispositivos Desligados ({0})", _manager.Settings.DisconnectedDevices.Count), Color.FromArgb(50, 55, 80));
                    btnResetOff.Location = new Point(20, y); btnResetOff.Size = new Size(260, 28);
                    btnResetOff.Click += delegate { _manager.Settings.DisconnectedDevices.Clear(); _manager.Settings.Save(); _manager.TriggerScanAsync(); btnResetOff.Enabled = false; btnResetOff.Text = "Reativados!"; };
                    sf.Controls.Add(btnResetOff); y += 36;
                }

                Button btnSave = CreateStyledButton("Salvar", Color.FromArgb(88, 101, 242));
                btnSave.Location = new Point(200, y); btnSave.Size = new Size(80, 30);
                btnSave.Click += delegate
                {
                    _manager.Settings.RefreshIntervalSeconds = (int)numInt.Value;
                    _manager.Settings.LowBatteryThreshold = (int)numThr.Value;
                    _manager.Settings.EnableNotifications = chkNotif.Checked;
                    _manager.Settings.StartWithWindows = chkBoot.Checked;
                    _manager.Settings.ShowSystemBattery = chkSys.Checked;
                    _manager.Settings.Save();
                    AutostartManager.SetAutostart(chkBoot.Checked);
                    _manager.UpdateRefreshInterval((int)numInt.Value);
                    if (cmbStyle.SelectedIndex >= 0 && styles[cmbStyle.SelectedIndex] != _manager.Settings.TrayStyle)
                        _trayManager.ApplyTrayStyle(styles[cmbStyle.SelectedIndex]);
                    sf.Close();
                };
                sf.Controls.Add(btnSave);

                Button btnCancel = CreateStyledButton("Cancelar", Color.FromArgb(45, 49, 72)); btnCancel.Location = new Point(292, y); btnCancel.Size = new Size(80, 30);
                btnCancel.Click += delegate { sf.Close(); };
                sf.Controls.Add(btnCancel);

                sf.ShowDialog(this);
            }
        }

        // ─────────────────────────── Helpers ──────────────────────────────────
        private Button CreateStyledButton(string text, Color bg)
        {
            Button btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
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

            // Durante Application.Exit(), o Windows Forms já enumera e fecha todos os formulários abertos.
            // Chamar Dispose() em outro Form aqui modificaria Application.OpenForms durante essa enumeração.
            if (e.CloseReason != CloseReason.ApplicationExitCall &&
                e.CloseReason != CloseReason.WindowsShutDown &&
                e.CloseReason != CloseReason.TaskManagerClosing)
            {
                if (_overlayForm != null && !_overlayForm.IsDisposed)
                {
                    _overlayForm.Dispose();
                    _overlayForm = null;
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
        // A Panel that owns the scrollbar AND draws the background behind cards.
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
                g.Clear(Color.FromArgb(20, 22, 32));

                if (BgImage != null)
                {
                    try
                    {
                        g.DrawImage(BgImage, clip);
                        int alpha = Math.Min(255, Math.Max(0, (int)(BgOpacity * 2.55)));
                        using (SolidBrush tint = new SolidBrush(Color.FromArgb(alpha, 16, 18, 28)))
                            g.FillRectangle(tint, clip);
                    }
                    catch { }
                }
            }

            // Make sure mouse wheel on child controls bubbles up to this panel
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
            }
        }
    }
}
