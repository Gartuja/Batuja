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
            BackColor = Color.FromArgb(192, 192, 192);
            ForeColor = Color.Black;
            Font = new Font("Tahoma", 8.5f);
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
            int headerH = 64;
            int footerH = 44;

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
                    _btnClose.Location = new Point(_footerPanel.Width - _btnClose.Width - 16, 8);
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
                BackColor = Color.FromArgb(192, 192, 192),
                Padding = new Padding(18, 10, 18, 10)
            };
            _headerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int sepY = _headerPanel.Height - 2;
                using (Pen pDark = new Pen(Color.FromArgb(128, 128, 128)))
                using (Pen pWhite = new Pen(Color.FromArgb(255, 255, 255)))
                {
                    g.DrawLine(pDark, 0, sepY, _headerPanel.Width, sepY);
                    g.DrawLine(pWhite, 0, sepY + 1, _headerPanel.Width, sepY + 1);
                }
            };

            Label lblTitle = new Label
            {
                Text = I18n.T("HistoryTitle"),
                Font = new Font("Tahoma", 11.5f, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(14, 10)
            };
            _headerPanel.Controls.Add(lblTitle);

            Label lblSubtitle = new Label
            {
                Text = I18n.T("HistorySubtitle"),
                Font = new Font("Tahoma", 8f),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(15, 34)
            };
            _headerPanel.Controls.Add(lblSubtitle);

            _btnClear = new AeroGlassButton
            {
                Text = I18n.T("ClearHistory"),
                Font = new Font("Tahoma", 8.25f),
                ForeColor = Color.Black,
                BaseGlassColor = Color.FromArgb(192, 192, 192),
                Classic3D = true,
                CornerRadius = 0,
                Size = new Size(116, 24),
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
                BackColor = Color.FromArgb(192, 192, 192),
                Padding = new Padding(16, 6, 16, 6)
            };
            _footerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                using (Pen pDark = new Pen(Color.FromArgb(128, 128, 128)))
                using (Pen pWhite = new Pen(Color.FromArgb(255, 255, 255)))
                {
                    g.DrawLine(pDark, 0, 0, _footerPanel.Width, 0);
                    g.DrawLine(pWhite, 0, 1, _footerPanel.Width, 1);
                }
            };

            _lblSummary = new Label
            {
                Text = "",
                Font = new Font("Tahoma", 8f),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(16, 12)
            };
            _footerPanel.Controls.Add(_lblSummary);

            _btnClose = new AeroGlassButton
            {
                Text = I18n.T("Close"),
                Font = new Font("Tahoma", 8.25f),
                ForeColor = Color.Black,
                BaseGlassColor = Color.FromArgb(192, 192, 192),
                Classic3D = true,
                CornerRadius = 0,
                Size = new Size(80, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(534, 8),
                Cursor = Cursors.Hand
            };
            _btnClose.Click += delegate { Close(); };
            _footerPanel.Controls.Add(_btnClose);

            // Items panel (scrollable)
            _itemsPanel = new Panel
            {
                AutoScroll = true,
                BackColor = Color.FromArgb(192, 192, 192),
                Padding = new Padding(14)
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
                    Font = new Font("Tahoma", 9f),
                    ForeColor = Color.FromArgb(80, 80, 80),
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
                BackColor = Color.FromArgb(192, 192, 192),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Rectangle r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                Draw3DRaised(g, r, 2);
            };

            // Device Icon (left) in Sunken 3D Box
            PictureBox pbIcon = new PictureBox
            {
                Size = new Size(42, 42),
                Location = new Point(14, 16),
                BackColor = Color.FromArgb(192, 192, 192)
            };
            pbIcon.Paint += delegate(object s, PaintEventArgs pe)
            {
                Graphics g = pe.Graphics;
                Rectangle r = new Rectangle(0, 0, 41, 41);
                Draw3DSunken(g, r);
                IconRenderer.DrawDeviceIcon(g, r, item.Category, Color.Black, null);
            };
            p.Controls.Add(pbIcon);

            // Name
            Label lblName = new Label
            {
                Text = item.Name,
                Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(66, 12)
            };
            p.Controls.Add(lblName);

            // Protocol & Battery
            string sub = item.LastBatteryPercent >= 0
                ? string.Format("{0}  •  {1}", item.Protocol, I18n.T("LastCharge", item.LastBatteryPercent))
                : item.Protocol;
            Label lblSub = new Label
            {
                Text = sub,
                Font = new Font("Tahoma", 8f),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(67, 34)
            };
            p.Controls.Add(lblSub);

            // Last Seen timestamp
            Label lblTime = new Label
            {
                Text = I18n.T("LastSeen", string.Format("{0:dd/MM HH:mm:ss}", item.LastSeen)),
                Font = new Font("Tahoma", 7.5f),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Location = new Point(67, 52)
            };
            p.Controls.Add(lblTime);

            // Status Badge on Right
            Label lblStatus = new Label
            {
                Text = isLive ? ("● " + I18n.T("ConnectedStatus")) : ("○ " + I18n.T("DisconnectedStatus")),
                Font = new Font("Tahoma", 8.5f, isLive ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isLive ? Color.FromArgb(0, 128, 0) : Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(p.Width - 130, 26)
            };
            p.Controls.Add(lblStatus);

            return p;
        }

        private static void Draw3DRaised(Graphics g, Rectangle r, int depth = 2)
        {
            Color cWhite = Color.FromArgb(255, 255, 255);
            Color cDark = Color.FromArgb(128, 128, 128);
            Color cBlack = Color.FromArgb(0, 0, 0);

            for (int i = 0; i < depth; i++)
            {
                Color topCol = (i == 0) ? cWhite : Color.FromArgb(223, 223, 223);
                Color botCol = (i == 0) ? cBlack : cDark;

                using (Pen pTop = new Pen(topCol))
                {
                    g.DrawLine(pTop, r.Left + i, r.Top + i, r.Right - i - 1, r.Top + i);
                    g.DrawLine(pTop, r.Left + i, r.Top + i, r.Left + i, r.Bottom - i - 1);
                }
                using (Pen pBot = new Pen(botCol))
                {
                    g.DrawLine(pBot, r.Left + i, r.Bottom - i - 1, r.Right - i - 1, r.Bottom - i - 1);
                    g.DrawLine(pBot, r.Right - i - 1, r.Top + i, r.Right - i - 1, r.Bottom - i - 1);
                }
            }
        }

        private static void Draw3DSunken(Graphics g, Rectangle r)
        {
            Color cWhite = Color.FromArgb(255, 255, 255);
            Color cDark = Color.FromArgb(128, 128, 128);
            Color cBlack = Color.FromArgb(0, 0, 0);

            using (Pen pDark = new Pen(cDark))
            {
                g.DrawLine(pDark, r.Left, r.Top, r.Right - 1, r.Top);
                g.DrawLine(pDark, r.Left, r.Top, r.Left, r.Bottom - 1);
            }
            using (Pen pBlack = new Pen(cBlack))
            {
                g.DrawLine(pBlack, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                g.DrawLine(pBlack, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);
            }
            using (Pen pWhite = new Pen(cWhite))
            {
                g.DrawLine(pWhite, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pWhite, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
            }
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
