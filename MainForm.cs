using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CTL472_OsuDriver
{
    public class MainForm : Form
    {
        private DriverConfig _config;
        private DriverCore _driver;

        // Custom Visual Controls
        private Panel _pnlHeader;
        private Label _lblTitle;
        private Label _lblSubTitle;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _lblStatusText;
        private ToolStripStatusLabel _lblHzText;
        private ToolStripStatusLabel _lblPosText;

        private TabletCanvas _tabletCanvas;
        private AimTestCanvas _aimCanvas;

        // Input Numeric Controls
        private NumericUpDown _numWidth;
        private NumericUpDown _numHeight;
        private NumericUpDown _numOffsetX;
        private NumericUpDown _numOffsetY;
        private NumericUpDown _numRotationAngle;

        // Checkboxes & Buttons
        private CheckBox _chkRotate180;
        private CheckBox _chkLeftHanded;
        private CheckBox _chkLockAspect;
        private CheckBox _chkAbsoluteMode;
        private CheckBox _chk1000Hz;
        private CheckBox _chkForce200Hz;
        private CheckBox _chkEnableDriver;
        private CheckBox _chkMinimizeTray;

        private Button _btnPreset169;
        private Button _btnPreset43;
        private Button _btnPresetMax;
        private Button _btnCenter;
        private Button _btnSaveConfig;

        private NotifyIcon _notifyIcon;
        private System.Windows.Forms.Timer _uiTimer;



        public MainForm()
        {
            _config = DriverConfig.Load();
            _driver = new DriverCore(_config);

            InitializeComponent();
            ApplyTheme();

            _driver.TabletStateUpdated += Driver_TabletStateUpdated;
            _driver.Start();
            _driver.RegisterRawInput(this.Handle);

            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 33; // ~30 FPS UI refresh
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void InitializeComponent()
        {
            this.Text = "Wacom CTL-472 Ultra-Low Latency osu! Driver";
            this.Size = new Size(1100, 720);
            this.MinimumSize = new Size(1000, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;
            this.DoubleBuffered = true;

            // System Tray Setup
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Text = "CTL-472 osu! Driver";
            _notifyIcon.Visible = false;
            _notifyIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                _notifyIcon.Visible = false;
            };

            // Header Panel
            _pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(20, 22, 30),
                Padding = new Padding(20, 10, 20, 10)
            };

            _lblTitle = new Label
            {
                Text = "⚡ WACOM CTL-472 OSU! DRIVER",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 255),
                AutoSize = true,
                Location = new Point(15, 10)
            };

            _lblSubTitle = new Label
            {
                Text = "Ultra-Low Latency • Custom Active Area • 180° Flip • Left-Handed Mode",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 170, 190),
                AutoSize = true,
                Location = new Point(18, 40)
            };

            _pnlHeader.Controls.Add(_lblTitle);
            _pnlHeader.Controls.Add(_lblSubTitle);

            // Tab Control for Main Settings vs Aim Test
            TabControl mainTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Point(15, 8)
            };

            TabPage tabDriver = new TabPage("🎨 Tablet Active Area & Options");
            TabPage tabAimTest = new TabPage("🎯 Aim & Latency Test Arena");

            tabDriver.BackColor = Color.FromArgb(28, 30, 42);
            tabAimTest.BackColor = Color.FromArgb(28, 30, 42);

            // Main Settings Layout: Left = Interactive Tablet Canvas, Right = Controls Panel
            TableLayoutPanel layoutMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(15)
            };
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            // Tablet Canvas
            _tabletCanvas = new TabletCanvas(_config)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5)
            };
            _tabletCanvas.AreaChanged += TabletCanvas_AreaChanged;

            // Right Panel (Controls & Parameters)
            Panel pnlControls = CreateControlsPanel();

            layoutMain.Controls.Add(_tabletCanvas, 0, 0);
            layoutMain.Controls.Add(pnlControls, 1, 0);

            tabDriver.Controls.Add(layoutMain);

            // Aim Test Tab
            _aimCanvas = new AimTestCanvas()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(15)
            };
            tabAimTest.Controls.Add(_aimCanvas);

            mainTabs.TabPages.Add(tabDriver);
            mainTabs.TabPages.Add(tabAimTest);

            // Status Bar
            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(18, 20, 26),
                ForeColor = Color.White
            };

            _lblStatusText = new ToolStripStatusLabel("Status: Initializing...") { ForeColor = Color.LightGreen };
            _lblHzText = new ToolStripStatusLabel("Polling Rate: 0 Hz (0.0 ms)") { ForeColor = Color.Cyan, Margin = new Padding(20, 0, 0, 0) };
            _lblPosText = new ToolStripStatusLabel("Tablet Pos: 0.0, 0.0 mm") { ForeColor = Color.Yellow, Margin = new Padding(20, 0, 0, 0) };

            _statusStrip.Items.Add(_lblStatusText);
            _statusStrip.Items.Add(_lblHzText);
            _statusStrip.Items.Add(_lblPosText);

            this.Controls.Add(mainTabs);
            this.Controls.Add(_pnlHeader);
            this.Controls.Add(_statusStrip);
        }

        private Panel CreateControlsPanel()
        {
            Panel pnl = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(22, 24, 34),
                Padding = new Padding(15)
            };

            int top = 10;

            // Group 1: Area Dimensions (mm)
            Label lblAreaTitle = new Label
            {
                Text = "📐 Active Area Dimensions (mm)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 255),
                Location = new Point(10, top),
                AutoSize = true
            };
            pnl.Controls.Add(lblAreaTitle);
            top += 30;

            TableLayoutPanel gridArea = new TableLayoutPanel
            {
                Location = new Point(10, top),
                Size = new Size(360, 110),
                ColumnCount = 2,
                RowCount = 4
            };
            gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            gridArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

            _numWidth = CreateNumericInput(5.0, 152.0, (decimal)_config.AreaWidth);
            _numHeight = CreateNumericInput(5.0, 95.0, (decimal)_config.AreaHeight);
            _numOffsetX = CreateNumericInput(0.0, 152.0, (decimal)_config.OffsetX);
            _numOffsetY = CreateNumericInput(0.0, 95.0, (decimal)_config.OffsetY);

            _numWidth.ValueChanged += (s, e) => OnNumericInputChanged();
            _numHeight.ValueChanged += (s, e) => OnNumericInputChanged();
            _numOffsetX.ValueChanged += (s, e) => OnNumericInputChanged();
            _numOffsetY.ValueChanged += (s, e) => OnNumericInputChanged();

            gridArea.Controls.Add(CreateLabel("Width (mm):"), 0, 0);
            gridArea.Controls.Add(_numWidth, 1, 0);
            gridArea.Controls.Add(CreateLabel("Height (mm):"), 0, 1);
            gridArea.Controls.Add(_numHeight, 1, 1);
            gridArea.Controls.Add(CreateLabel("Offset X (mm):"), 0, 2);
            gridArea.Controls.Add(_numOffsetX, 1, 2);
            gridArea.Controls.Add(CreateLabel("Offset Y (mm):"), 0, 3);
            gridArea.Controls.Add(_numOffsetY, 1, 3);

            pnl.Controls.Add(gridArea);
            top += 125;

            // Presets & Alignment Buttons
            FlowLayoutPanel flowPresets = new FlowLayoutPanel
            {
                Location = new Point(10, top),
                Size = new Size(360, 40),
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnPreset169 = CreateButton("16:9 Preset", (s, e) => SetPreset(16.0 / 9.0));
            _btnPreset43 = CreateButton("4:3 Preset", (s, e) => SetPreset(4.0 / 3.0));
            _btnPresetMax = CreateButton("Full Area", (s, e) => SetPresetFullArea());
            _btnCenter = CreateButton("Center Area", (s, e) => CenterArea());

            flowPresets.Controls.Add(_btnPreset169);
            flowPresets.Controls.Add(_btnPreset43);
            flowPresets.Controls.Add(_btnPresetMax);
            flowPresets.Controls.Add(_btnCenter);

            pnl.Controls.Add(flowPresets);
            top += 45;

            // Group 2: Orientation & Handedness (กลับหัว / มือซ้าย)
            Label lblOrientTitle = new Label
            {
                Text = "🔄 Orientation & Handedness",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 255),
                Location = new Point(10, top),
                AutoSize = true
            };
            pnl.Controls.Add(lblOrientTitle);
            top += 30;

            _chkRotate180 = new CheckBox
            {
                Text = "🔄 Rotate 180° (กลับหัวแท็บเล็ต)",
                Checked = _config.Rotate180,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.Gold,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkRotate180.CheckedChanged += (s, e) =>
            {
                _config.Rotate180 = _chkRotate180.Checked;
                _driver.UpdateConfig(_config);
                _tabletCanvas.Invalidate();
            };
            pnl.Controls.Add(_chkRotate180);
            top += 30;

            _chkLeftHanded = new CheckBox
            {
                Text = "🤚 Left-Handed Mode (กลับด้านมือซ้าย)",
                Checked = _config.LeftHanded,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.MediumSpringGreen,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkLeftHanded.CheckedChanged += (s, e) =>
            {
                _config.LeftHanded = _chkLeftHanded.Checked;
                _driver.UpdateConfig(_config);
                _tabletCanvas.Invalidate();
            };
            pnl.Controls.Add(_chkLeftHanded);
            top += 32;

            // Custom Rotation Angle Input Grid
            TableLayoutPanel gridOrient = new TableLayoutPanel
            {
                Location = new Point(10, top),
                Size = new Size(360, 32),
                ColumnCount = 2,
                RowCount = 1
            };
            gridOrient.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            gridOrient.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            _numRotationAngle = CreateNumericInput(0.0, 360.0, (decimal)_config.RotationAngle);
            _numRotationAngle.Increment = 1.0m;
            _numRotationAngle.ValueChanged += (s, e) =>
            {
                _config.RotationAngle = (double)_numRotationAngle.Value;
                _driver.UpdateConfig(_config);
                _tabletCanvas.Invalidate();
            };

            gridOrient.Controls.Add(CreateLabel("📐 Rotation Angle (°):"), 0, 0);
            gridOrient.Controls.Add(_numRotationAngle, 1, 0);
            pnl.Controls.Add(gridOrient);
            top += 38;

            // Group 3: Driver Options
            Label lblOptTitle = new Label
            {
                Text = "⚙️ Driver Settings",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 255),
                Location = new Point(10, top),
                AutoSize = true
            };
            pnl.Controls.Add(lblOptTitle);
            top += 30;

            _chkLockAspect = new CheckBox
            {
                Text = "🔒 Lock Aspect Ratio (16:9)",
                Checked = _config.LockAspectRatio,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.White,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkLockAspect.CheckedChanged += (s, e) =>
            {
                _config.LockAspectRatio = _chkLockAspect.Checked;
                _driver.UpdateConfig(_config);
            };
            pnl.Controls.Add(_chkLockAspect);
            top += 28;

            _chkAbsoluteMode = new CheckBox
            {
                Text = "🎯 Absolute Mode (Direct 1:1 Hardware Mapping)",
                Checked = _config.AbsoluteMode,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkAbsoluteMode.CheckedChanged += (s, e) =>
            {
                _config.AbsoluteMode = _chkAbsoluteMode.Checked;
                _driver.UpdateConfig(_config);
            };
            pnl.Controls.Add(_chkAbsoluteMode);
            top += 28;

            _chk1000Hz = new CheckBox
            {
                Text = "⚡ Enable 1000 Hz Ultra-Smooth Engine (1ms Sub-frame)",
                Checked = _config.Enable1000Hz,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chk1000Hz.CheckedChanged += (s, e) =>
            {
                _config.Enable1000Hz = _chk1000Hz.Checked;
                _driver.UpdateConfig(_config);
            };
            pnl.Controls.Add(_chk1000Hz);
            top += 28;

            _chkForce200Hz = new CheckBox
            {
                Text = "🔥 Force Constant 200 Hz Raw Mode (5ms Locked Rate)",
                Checked = _config.Force200Hz,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.Orange,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkForce200Hz.CheckedChanged += (s, e) =>
            {
                _config.Force200Hz = _chkForce200Hz.Checked;
                if (_chkForce200Hz.Checked)
                {
                    _chk1000Hz.Checked = false;
                    _config.Enable1000Hz = false;
                }
                _driver.UpdateConfig(_config);
            };
            pnl.Controls.Add(_chkForce200Hz);
            top += 28;

            _chkEnableDriver = new CheckBox
            {
                Text = "⚡ Enable Ultra-Low Latency Driver",
                Checked = _config.EnableDriver,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.LightGreen,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkEnableDriver.CheckedChanged += (s, e) =>
            {
                _config.EnableDriver = _chkEnableDriver.Checked;
                _driver.UpdateConfig(_config);
            };
            pnl.Controls.Add(_chkEnableDriver);
            top += 28;

            _chkMinimizeTray = new CheckBox
            {
                Text = "📥 Minimize to System Tray",
                Checked = _config.MinimizeToTray,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.White,
                Location = new Point(15, top),
                AutoSize = true
            };
            _chkMinimizeTray.CheckedChanged += (s, e) =>
            {
                _config.MinimizeToTray = _chkMinimizeTray.Checked;
            };
            pnl.Controls.Add(_chkMinimizeTray);
            top += 40;

            // Save Button
            _btnSaveConfig = new Button
            {
                Text = "💾 Save Configuration",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 180, 236),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(340, 42),
                Location = new Point(15, top),
                Cursor = Cursors.Hand
            };
            _btnSaveConfig.FlatAppearance.BorderSize = 0;
            _btnSaveConfig.Click += (s, e) =>
            {
                _config.Save();
                MessageBox.Show("Driver settings saved successfully!", "CTL-472 osu! Driver", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            pnl.Controls.Add(_btnSaveConfig);

            return pnl;
        }

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Left,
                AutoSize = true
            };
        }

        private NumericUpDown CreateNumericInput(double min, double max, decimal initial)
        {
            return new NumericUpDown
            {
                Minimum = (decimal)min,
                Maximum = (decimal)max,
                Value = initial,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.FromArgb(35, 38, 52),
                ForeColor = Color.Cyan,
                Width = 100
            };
        }

        private Button CreateButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(45, 48, 66),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(82, 30),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 85, 110);
            btn.Click += onClick;
            return btn;
        }

        private void OnNumericInputChanged()
        {
            _config.AreaWidth = (double)_numWidth.Value;
            _config.AreaHeight = (double)_numHeight.Value;
            _config.OffsetX = (double)_numOffsetX.Value;
            _config.OffsetY = (double)_numOffsetY.Value;

            _driver.UpdateConfig(_config);
            _tabletCanvas.Invalidate();
        }

        private void TabletCanvas_AreaChanged(object sender, EventArgs e)
        {
            // Sync values from visual canvas to numeric boxes
            _numWidth.Value = (decimal)Math.Min(152.0, Math.Max(5.0, _config.AreaWidth));
            _numHeight.Value = (decimal)Math.Min(95.0, Math.Max(5.0, _config.AreaHeight));
            _numOffsetX.Value = (decimal)Math.Min(152.0, Math.Max(0.0, _config.OffsetX));
            _numOffsetY.Value = (decimal)Math.Min(95.0, Math.Max(0.0, _config.OffsetY));
            _numRotationAngle.Value = (decimal)Math.Min(360.0, Math.Max(0.0, _config.RotationAngle));

            _driver.UpdateConfig(_config);
        }

        private void SetPreset(double ratio)
        {
            double targetH = _config.AreaWidth / ratio;
            if (targetH > 95.0)
            {
                targetH = 95.0;
                _config.AreaWidth = targetH * ratio;
            }
            _config.AreaHeight = targetH;
            _config.AspectRatioValue = ratio;

            CenterArea();
        }

        private void SetPresetFullArea()
        {
            _config.AreaWidth = 152.0;
            _config.AreaHeight = 95.0;
            _config.OffsetX = 0.0;
            _config.OffsetY = 0.0;
            TabletCanvas_AreaChanged(null, null);
            _tabletCanvas.Invalidate();
        }

        private void CenterArea()
        {
            _config.OffsetX = Math.Max(0.0, (152.0 - _config.AreaWidth) / 2.0);
            _config.OffsetY = Math.Max(0.0, (95.0 - _config.AreaHeight) / 2.0);
            TabletCanvas_AreaChanged(null, null);
            _tabletCanvas.Invalidate();
        }

        private void Driver_TabletStateUpdated(object sender, TabletStateEventArgs e)
        {
            _tabletCanvas.UpdatePenPosition(e.TabletXmm, e.TabletYmm);
            _aimCanvas.UpdatePenPosition(e.ScreenX, e.ScreenY, e.TipDown || e.Button1);

            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.BeginInvoke(new Action(() =>
                {
                    _lblHzText.Text = string.Format(CultureInfo.InvariantCulture, "Polling Rate: {0:F0} Hz ({1:F1} ms)", e.Hz, (e.Hz > 0 ? 1000.0 / e.Hz : 0));
                    _lblPosText.Text = string.Format(CultureInfo.InvariantCulture, "Tablet Pos: {0:F1}, {1:F1} mm", e.TabletXmm, e.TabletYmm);
                }));
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_driver.IsTabletConnected)
            {
                _lblStatusText.Text = "Status: Wacom CTL-472 Connected (Ultra-Low Latency Active)";
                _lblStatusText.ForeColor = Color.LightGreen;
            }
            else
            {
                _lblStatusText.Text = "Status: Waiting for Wacom CTL-472 tablet connection...";
                _lblStatusText.ForeColor = Color.Orange;
            }

            _tabletCanvas.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Minimized && _config.MinimizeToTray)
            {
                this.Hide();
                _notifyIcon.Visible = true;
                _notifyIcon.ShowBalloonTip(1000, "CTL-472 Driver", "Driver running in background with zero latency.", ToolTipIcon.Info);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _driver.Dispose();
            _notifyIcon.Dispose();
            base.OnFormClosing(e);
        }

        private void ApplyTheme()
        {
            this.BackColor = Color.FromArgb(20, 22, 30);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x00FF) // WM_INPUT
            {
                _driver.ProcessRawInput(m.LParam);
            }
            base.WndProc(ref m);
        }
    }

    #region Custom Visual Tablet Canvas (Interactive Drag & Resize)
    public class TabletCanvas : Control
    {
        private DriverConfig _config;
        public event EventHandler AreaChanged;

        private RectangleF _activeRectPx;
        private bool _isDragging = false;
        private bool _isRotating = false;
        private Point _dragStart;
        private PointF _origOffset;

        private int _resizeHandle = -1; // 0: TL, 1: TR, 2: BL, 3: BR
        private PointF _origAreaSize;
        private PointF _rotateHandlePx;

        private PointF _penPosMm = new PointF(-1, -1);

        public TabletCanvas(DriverConfig config)
        {
            _config = config;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(16, 18, 24);
            this.Cursor = Cursors.Cross;
        }

        public void UpdatePenPosition(double xMm, double yMm)
        {
            _penPosMm = new PointF((float)xMm, (float)yMm);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw Tablet Outer Surface (152 x 95 mm aspect ratio)
            float margin = 30f;
            float availW = this.Width - margin * 2;
            float availH = this.Height - margin * 2;

            float aspectTablet = (float)(152.0 / 95.0);
            float tabW = availW;
            float tabH = tabW / aspectTablet;

            if (tabH > availH)
            {
                tabH = availH;
                tabW = tabH * aspectTablet;
            }

            float tabX = margin + (availW - tabW) / 2f;
            float tabY = margin + (availH - tabH) / 2f;

            RectangleF tabRect = new RectangleF(tabX, tabY, tabW, tabH);

            // Draw Tablet Body
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(25, 27, 36)))
            using (Pen borderPen = new Pen(Color.FromArgb(60, 65, 85), 2f))
            {
                g.FillRectangle(bgBrush, tabRect);
                g.DrawRectangle(borderPen, tabX, tabY, tabW, tabH);
            }

            // Draw Wacom Dot Grid Pattern
            using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(45, 50, 68)))
            {
                for (float dx = tabX + 15; dx < tabX + tabW - 10; dx += 20)
                {
                    for (float dy = tabY + 15; dy < tabY + tabH - 10; dy += 20)
                    {
                        g.FillEllipse(dotBrush, dx, dy, 2, 2);
                    }
                }
            }

            // Draw Physical Text Info
            using (Font f = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(100, 110, 135)))
            {
                g.DrawString("WACOM ONE CTL-472 (152.0 x 95.0 mm)", f, sb, tabX + 10, tabY + 10);
            }

            // Calculate Active Area Pixel Rectangle
            float scaleX = tabW / 152.0f;
            float scaleY = tabH / 95.0f;

            float areaX = tabX + (float)_config.OffsetX * scaleX;
            float areaY = tabY + (float)_config.OffsetY * scaleY;
            float areaW = (float)_config.AreaWidth * scaleX;
            float areaH = (float)_config.AreaHeight * scaleY;

            _activeRectPx = new RectangleF(areaX, areaY, areaW, areaH);

            // Calculate Active Area Center & Rotation
            PointF areaCenter = new PointF(areaX + areaW / 2f, areaY + areaH / 2f);
            float rotAngle = (float)_config.RotationAngle;

            GraphicsState state = g.Save();
            g.TranslateTransform(areaCenter.X, areaCenter.Y);
            g.RotateTransform(rotAngle);

            RectangleF localRect = new RectangleF(-areaW / 2f, -areaH / 2f, areaW, areaH);

            // Draw Active Area Fill & Border
            using (SolidBrush areaBrush = new SolidBrush(Color.FromArgb(40, 0, 210, 255)))
            using (Pen areaPen = new Pen(Color.FromArgb(0, 210, 255), 2.5f))
            {
                g.FillRectangle(areaBrush, localRect);
                g.DrawRectangle(areaPen, localRect.X, localRect.Y, localRect.Width, localRect.Height);
            }

            // Draw Top Rotation Handle & Line
            PointF topCenterLocal = new PointF(0, -areaH / 2f);
            PointF rotateHandleLocal = new PointF(0, -areaH / 2f - 24f);

            using (Pen linePen = new Pen(Color.FromArgb(0, 210, 255), 2f) { DashStyle = DashStyle.Dot })
            using (SolidBrush rotateBrush = new SolidBrush(Color.Gold))
            using (Pen rotateBorder = new Pen(Color.White, 2f))
            {
                g.DrawLine(linePen, topCenterLocal, rotateHandleLocal);
                g.FillEllipse(rotateBrush, rotateHandleLocal.X - 8, rotateHandleLocal.Y - 8, 16, 16);
                g.DrawEllipse(rotateBorder, rotateHandleLocal.X - 8, rotateHandleLocal.Y - 8, 16, 16);
            }

            // Draw Corner Handles for Resizing
            float handleSize = 8f;
            PointF[] localHandles = new PointF[]
            {
                new PointF(localRect.Left, localRect.Top),
                new PointF(localRect.Right, localRect.Top),
                new PointF(localRect.Left, localRect.Bottom),
                new PointF(localRect.Right, localRect.Bottom)
            };

            using (SolidBrush handleBrush = new SolidBrush(Color.Cyan))
            {
                foreach (PointF h in localHandles)
                {
                    g.FillRectangle(handleBrush, h.X - handleSize / 2, h.Y - handleSize / 2, handleSize, handleSize);
                }
            }

            // Draw Active Area Dimensions & Angle Label inside rectangle
            using (Font f = new Font("Segoe UI", 10F, FontStyle.Bold))
            using (SolidBrush sb = new SolidBrush(Color.White))
            {
                string infoStr = string.Format(CultureInfo.InvariantCulture, "{0:F1} x {1:F1} mm ({2:F0}°)", _config.AreaWidth, _config.AreaHeight, _config.RotationAngle);
                SizeF sz = g.MeasureString(infoStr, f);
                g.DrawString(infoStr, f, sb, -sz.Width / 2f, -sz.Height / 2f);
            }

            g.Restore(state);

            // Compute Rotation Handle Location in Canvas Coordinates for Hit-Testing
            double rad = rotAngle * (Math.PI / 180.0);
            float handleRelY = -areaH / 2f - 24f;
            float rotHx = (float)(0 * Math.Cos(rad) - handleRelY * Math.Sin(rad));
            float rotHy = (float)(0 * Math.Sin(rad) + handleRelY * Math.Cos(rad));
            _rotateHandlePx = new PointF(areaCenter.X + rotHx, areaCenter.Y + rotHy);

            // Draw Pen Tracking Indicator
            if (_penPosMm.X >= 0 && _penPosMm.Y >= 0)
            {
                float pxX = tabX + _penPosMm.X * scaleX;
                float pxY = tabY + _penPosMm.Y * scaleY;

                using (Pen penRing = new Pen(Color.Magenta, 2f))
                using (SolidBrush penDot = new SolidBrush(Color.Yellow))
                {
                    g.DrawEllipse(penRing, pxX - 8, pxY - 8, 16, 16);
                    g.FillEllipse(penDot, pxX - 3, pxY - 3, 6, 6);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Check Rotation Handle click first
            if (Math.Abs(e.X - _rotateHandlePx.X) <= 14f && Math.Abs(e.Y - _rotateHandlePx.Y) <= 14f)
            {
                _isRotating = true;
                return;
            }

            // Check corner resize handles click
            float handleRadius = 10f;
            PointF[] handles = new PointF[]
            {
                new PointF(_activeRectPx.Left, _activeRectPx.Top),
                new PointF(_activeRectPx.Right, _activeRectPx.Top),
                new PointF(_activeRectPx.Left, _activeRectPx.Bottom),
                new PointF(_activeRectPx.Right, _activeRectPx.Bottom)
            };

            for (int i = 0; i < handles.Length; i++)
            {
                if (Math.Abs(e.X - handles[i].X) <= handleRadius && Math.Abs(e.Y - handles[i].Y) <= handleRadius)
                {
                    _resizeHandle = i;
                    _dragStart = e.Location;
                    _origOffset = new PointF((float)_config.OffsetX, (float)_config.OffsetY);
                    _origAreaSize = new PointF((float)_config.AreaWidth, (float)_config.AreaHeight);
                    return;
                }
            }

            // Check dragging active area position
            if (_activeRectPx.Contains(e.Location))
            {
                _isDragging = true;
                _dragStart = e.Location;
                _origOffset = new PointF((float)_config.OffsetX, (float)_config.OffsetY);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            float scaleX = _activeRectPx.Width / (float)_config.AreaWidth;
            float scaleY = _activeRectPx.Height / (float)_config.AreaHeight;

            if (_isRotating)
            {
                PointF center = new PointF(_activeRectPx.Left + _activeRectPx.Width / 2f, _activeRectPx.Top + _activeRectPx.Height / 2f);
                double rad = Math.Atan2(e.Y - center.Y, e.X - center.X);
                double deg = rad * (180.0 / Math.PI) + 90.0;
                if (deg < 0) deg += 360.0;
                _config.RotationAngle = Math.Round(deg % 360.0, 0);

                if (AreaChanged != null) AreaChanged(this, EventArgs.Empty);
                this.Invalidate();
            }
            else if (_isDragging)
            {
                float dxMm = (e.X - _dragStart.X) / scaleX;
                float dyMm = (e.Y - _dragStart.Y) / scaleY;

                _config.OffsetX = Math.Max(0.0, Math.Min(152.0 - _config.AreaWidth, _origOffset.X + dxMm));
                _config.OffsetY = Math.Max(0.0, Math.Min(95.0 - _config.AreaHeight, _origOffset.Y + dyMm));

                if (AreaChanged != null) AreaChanged(this, EventArgs.Empty);
                this.Invalidate();
            }
            else if (_resizeHandle >= 0)
            {
                float dxMm = (e.X - _dragStart.X) / scaleX;
                float dyMm = (e.Y - _dragStart.Y) / scaleY;

                if (_resizeHandle == 3) // Bottom Right
                {
                    _config.AreaWidth = Math.Max(5.0, Math.Min(152.0 - _config.OffsetX, _origAreaSize.X + dxMm));
                    _config.AreaHeight = Math.Max(5.0, Math.Min(95.0 - _config.OffsetY, _origAreaSize.Y + dyMm));
                }

                if (_config.LockAspectRatio)
                {
                    _config.AreaHeight = _config.AreaWidth / _config.AspectRatioValue;
                }

                if (AreaChanged != null) AreaChanged(this, EventArgs.Empty);
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
            _isRotating = false;
            _resizeHandle = -1;
        }
    }
    #endregion

    #region Aim Testing Arena Canvas
    public class AimTestCanvas : Control
    {
        private Point _penPos;
        private Point _targetPos;
        private Random _rnd = new Random();
        private int _score = 0;

        public AimTestCanvas()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(18, 20, 28);
            SpawnNewTarget();
        }

        private void SpawnNewTarget()
        {
            int margin = 50;
            _targetPos = new Point(
                _rnd.Next(margin, Math.Max(margin + 1, this.Width - margin)),
                _rnd.Next(margin, Math.Max(margin + 1, this.Height - margin))
            );
        }

        public void UpdatePenPosition(int screenX, int screenY, bool clicked)
        {
            Point local = this.PointToClient(new Point(screenX, screenY));
            _penPos = local;

            if (clicked)
            {
                double dist = Math.Sqrt(Math.Pow(local.X - _targetPos.X, 2) + Math.Pow(local.Y - _targetPos.Y, 2));
                if (dist <= 30)
                {
                    _score++;
                    SpawnNewTarget();
                }
            }
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw Target Circle (osu! style)
            using (SolidBrush targetBg = new SolidBrush(Color.FromArgb(255, 0, 128, 255)))
            using (Pen targetPen = new Pen(Color.White, 3f))
            {
                g.FillEllipse(targetBg, _targetPos.X - 25, _targetPos.Y - 25, 50, 50);
                g.DrawEllipse(targetPen, _targetPos.X - 25, _targetPos.Y - 25, 50, 50);
            }

            // Draw Pen Cursor
            using (SolidBrush penBrush = new SolidBrush(Color.Yellow))
            using (Pen penRing = new Pen(Color.Lime, 2f))
            {
                g.FillEllipse(penBrush, _penPos.X - 4, _penPos.Y - 4, 8, 8);
                g.DrawEllipse(penRing, _penPos.X - 12, _penPos.Y - 12, 24, 24);
            }

            // Draw Score and Instructions
            using (Font f = new Font("Segoe UI", 12F, FontStyle.Bold))
            using (SolidBrush sb = new SolidBrush(Color.White))
            {
                g.DrawString("🎯 Aim & Response Test - Score: " + _score, f, sb, 20, 20);
                g.DrawString("Move pen over the circle and tap to test aiming precision & instant response!", new Font("Segoe UI", 9.5F), Brushes.LightGray, 20, 48);
            }
        }
    }
    #endregion
}
