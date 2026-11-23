using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OmniBattery.Core;
using OmniBattery.Models;

namespace OmniBattery.UI
{
    public class HistoryForm : Form
    {
        private readonly BatteryManager _manager;
        private Panel _headerPanel;
        private Panel _footerPanel;
        private Panel _itemsPanel;
        private Label _lblSummary;
        private AeroGlassButton _btnClear;
        private AeroGlassButton _btnClose;

        public HistoryForm(BatteryManager manager)
        {
            _manager = manager;

            Text = I18n.T("HistoryTitle");
            Size = new Size(640, 520);
            MinimumSize = new Size(540, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(20, 22, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;

            try { Icon = AppIconRenderer.CreateIcon(); } catch { }

            BuildUI();
            LayoutPanels();
            RefreshList();
        }

        private void LayoutPanels()
        {
            int cw = ClientSize.Width;
            int ch = ClientSize.Height;
            int headerH = 68;
            int footerH = 50;

            if (_headerPanel != null)
            {
                _headerPanel.SetBounds(0, 0, cw, headerH);
                if (_btnClear != null)
                    _btnClear.Location = new Point(_headerPanel.Width - _btnClear.Width - 16, 18);
            }

            if (_footerPanel != null)
            {
                _footerPanel.SetBounds(0, ch - footerH, cw, footerH);
                if (_btnClose != null)
                    _btnClose.Location = new Point(_footerPanel.Width - _btnClose.Width - 16, 10);
            }

            if (_itemsPanel != null)
            {
                _itemsPanel.SetBounds(0, headerH, cw, Math.Max(10, ch - headerH - footerH));
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutPanels();
        }

        private void BuildUI()
        {
            // Header
            _headerPanel = new Panel
            {
                BackColor = Color.FromArgb(28, 31, 46),
                Padding = new Padding(18, 12, 18, 12)
            };

            Label lblTitle = new Label
            {
                Text = I18n.T("HistoryTitle"),
                Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(16, 12)
            };
            _headerPanel.Controls.Add(lblTitle);

            Label lblSubtitle = new Label
            {
                Text = I18n.T("HistorySubtitle"),
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(140, 145, 175),
                AutoSize = true,
                Location = new Point(17, 38)
            };
            _headerPanel.Controls.Add(lblSubtitle);

            _btnClear = new AeroGlassButton
            {
                Text = I18n.T("ClearHistory"),
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 120, 120),
                BaseGlassColor = Color.FromArgb(60, 35, 45),
                AccentColor = Color.FromArgb(255, 90, 90),
                UseAccentBorder = true,
                Size = new Size(124, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(492, 18),
                Cursor = Cursors.Hand
            };
            _btnClear.Click += delegate
            {
                if (MessageBox.Show(this, "Deseja realmente limpar todo o histórico de dispositivos?", I18n.T("ClearHistory"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _manager.History.Clear();
                    RefreshList();
                }
            };
            _headerPanel.Controls.Add(_btnClear);

            // Footer
            _footerPanel = new Panel
            {
                BackColor = Color.FromArgb(28, 31, 46),
                Padding = new Padding(16, 8, 16, 8)
            };

            _lblSummary = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 145, 175),
                AutoSize = true,
                Location = new Point(16, 15)
            };
            _footerPanel.Controls.Add(_lblSummary);

            _btnClose = new AeroGlassButton
            {
                Text = I18n.T("Close"),
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = Color.White,
                BaseGlassColor = Color.FromArgb(42, 48, 70),
                Size = new Size(88, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(526, 10),
                Cursor = Cursors.Hand
            };
            _btnClose.Click += delegate { Close(); };
            _footerPanel.Controls.Add(_btnClose);

            // Items panel (scrollable)
            _itemsPanel = new Panel
            {
                AutoScroll = true,
                BackColor = Color.FromArgb(20, 22, 32),
                Padding = new Padding(16)
            };

            Controls.Add(_headerPanel);
            Controls.Add(_footerPanel);
            Controls.Add(_itemsPanel);
        }

        private void RefreshList()
        {
            _itemsPanel.SuspendLayout();
            _itemsPanel.Controls.Clear();

            List<DeviceHistoryEntry> list = _manager.History.GetAll();

            // Check live connection against CurrentDevices
            HashSet<string> liveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BatteryDevice cd in _manager.CurrentDevices)
            {
                liveKeys.Add(cd.DeviceKey);
                if (!string.IsNullOrEmpty(cd.Name)) liveKeys.Add(cd.Name);
            }

            int countConnected = 0;
            int y = 14;
            int cardW = _itemsPanel.ClientSize.Width - 36;
            if (cardW < 400) cardW = 560;

            if (list.Count == 0)
            {
                Label empty = new Label
                {
                    Text = "Nenhum histórico registrado ainda.\nConecte um dispositivo sem fio para registrar.",
                    Font = new Font("Segoe UI", 10.5f),
                    ForeColor = Color.FromArgb(110, 115, 145),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false,
                    Size = new Size(cardW, 120),
                    Location = new Point(18, 40)
                };
                _itemsPanel.Controls.Add(empty);
            }
            else
            {
                foreach (DeviceHistoryEntry item in list)
                {
                    bool isLive = liveKeys.Contains(item.Key) || liveKeys.Contains(item.Name);
                    if (isLive) countConnected++;

                    Panel card = CreateHistoryCard(item, isLive, cardW);
                    card.Location = new Point(14, y);
                    _itemsPanel.Controls.Add(card);
                    y += card.Height + 10;
                }
            }

            _lblSummary.Text = string.Format("Total: {0} dispositivo(s) no histórico ({1} ativo(s) na tela)", list.Count, countConnected);
            _itemsPanel.ResumeLayout(true);
        }

        private Panel CreateHistoryCard(DeviceHistoryEntry item, bool isLive, int width)
        {
            Panel p = new Panel
            {
                Size = new Size(width, 74),
                BackColor = isLive ? Color.FromArgb(28, 31, 46) : Color.FromArgb(22, 24, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using (GraphicsPath gp = RoundedRect(r, 8))
                {
                    Color border = isLive ? Color.FromArgb(46, 204, 113) : Color.FromArgb(45, 48, 68);
                    using (Pen pen = new Pen(border, isLive ? 1.4f : 1f))
                        g.DrawPath(pen, gp);
                }
            };

            // Device Icon (left)
            PictureBox pbIcon = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(14, 17),
                BackColor = Color.Transparent
            };
            Color iconColor = isLive ? Color.FromArgb(46, 204, 113) : Color.FromArgb(140, 145, 175);
            pbIcon.Paint += delegate(object s, PaintEventArgs pe)
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(isLive ? 35 : 20, iconColor)))
                    pe.Graphics.FillEllipse(b, 0, 0, 39, 39);
                using (Pen pen = new Pen(iconColor, 1.2f))
                    pe.Graphics.DrawEllipse(pen, 0, 0, 39, 39);
                IconRenderer.DrawDeviceIcon(pe.Graphics, new Rectangle(0, 0, 40, 40), item.Category, iconColor, null);
            };
            p.Controls.Add(pbIcon);

            // Name
            Label lblName = new Label
            {
                Text = item.Name,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = isLive ? Color.White : Color.FromArgb(180, 185, 205),
                AutoSize = true,
                Location = new Point(64, 14)
            };
            p.Controls.Add(lblName);

            // Protocol & Battery
            string sub = item.LastBatteryPercent >= 0
                ? string.Format("{0}  •  {1}", item.Protocol, I18n.T("LastCharge", item.LastBatteryPercent))
                : item.Protocol;
            Label lblSub = new Label
            {
                Text = sub,
                Font = new Font("Segoe UI", 7.8f),
                ForeColor = Color.FromArgb(130, 135, 160),
                AutoSize = true,
                Location = new Point(65, 38)
            };
            p.Controls.Add(lblSub);

            // Last Seen timestamp
            Label lblTime = new Label
            {
                Text = I18n.T("LastSeen", string.Format("{0:dd/MM HH:mm:ss}", item.LastSeen)),
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(100, 105, 130),
                AutoSize = true,
                Location = new Point(65, 54)
            };
            p.Controls.Add(lblTime);

            // Status Badge on Right (sem botão de desligar manual)
            if (isLive)
            {
                Label lblStatus = new Label
                {
                    Text = "● " + I18n.T("ConnectedStatus"),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(46, 204, 113),
                    AutoSize = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Location = new Point(p.Width - 120, 27)
                };
                p.Controls.Add(lblStatus);
            }
            else
            {
                Label lblStatus = new Label
                {
                    Text = "○ " + I18n.T("DisconnectedStatus"),
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = Color.FromArgb(130, 135, 160),
                    AutoSize = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Location = new Point(p.Width - 120, 27)
                };
                p.Controls.Add(lblStatus);
            }

            return p;
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
