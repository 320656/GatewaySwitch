using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace YuanxinGateway
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = "YuanxinGateway.SingleInstance";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex instanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MainForm.SignalExistingInstance();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    internal sealed class MainForm : Form
    {


        private readonly Color backgroundColor = Color.FromArgb(16, 19, 26);
        private readonly Color cardColor = Color.FromArgb(29, 32, 39);
        private readonly Color cardBorderColor = Color.FromArgb(66, 71, 84);
        private readonly Color primaryColor = Color.FromArgb(173, 198, 255);
        private readonly Color primaryDimColor = Color.FromArgb(77, 142, 255);
        private readonly Color textColor = Color.FromArgb(225, 226, 236);
        private readonly Color mutedTextColor = Color.FromArgb(194, 198, 214);
        private readonly Color dangerColor = Color.FromArgb(255, 180, 171);
        private readonly Color successColor = Color.FromArgb(72, 224, 146);
        private readonly Color warningColor = Color.FromArgb(255, 183, 134);

        private readonly Label titleLabel;
        private readonly Label helperLabel;
        private readonly Button powerButton;
        private readonly Label routeStateLabel;
        private readonly Panel cardPanel;
        private readonly Label cardTitleLabel;
        private readonly Label connectionBadgeLabel;
        private readonly Label gatewayCaptionLabel;
        private readonly Label gatewayValueLabel;
        private readonly Label detailLabel;
        private readonly Panel progressTrack;
        private readonly Panel progressFill;
        private readonly Button testButton;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip trayMenu;

        private const int BaseClientWidth = 440;
        private const int BaseClientHeight = 620;
        private const int BaseProgressWidth = 328;
        private const int ReferenceScreenWidth = 1920;
        private const int ReferenceScreenHeight = 1080;

        private bool enabledByApp;
        private bool allowExit;
        private bool isProcessing;
        private System.Windows.Forms.Timer loadingTimer;
        private string loadingBaseText;
        private int loadingDotCount;
        private bool userManuallyDisabled;
        private string lastSsid;
        private float dpiScale = 1F;
        private int progressDesignWidth;
        private readonly Button settingsButton;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETICON = 0x0080;
        private static readonly IntPtr IconSmall = new IntPtr(0);
        private static readonly IntPtr IconBig = new IntPtr(1);
        private static readonly int ShowExistingInstanceMessage = RegisterWindowMessage("YuanxinGateway.ShowExistingInstance");
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        public MainForm()
        {
            Text = "元信旁路由";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(BaseClientWidth, BaseClientHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = backgroundColor;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            TrySetAppIcon();
            SuspendLayout();

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("显示主窗口", null, ShowMainWindowMenuItem_Click);
            trayMenu.Items.Add("退出", null, ExitMenuItem_Click);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "元信旁路由";
            trayIcon.Icon = Icon;
            trayIcon.Visible = true;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.Text = "元信旁路由";
            titleLabel.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
            titleLabel.ForeColor = primaryColor;
            titleLabel.Location = new Point(28, 24);
            titleLabel.Size = new Size(220, 36);
            titleLabel.AutoEllipsis = true;

            settingsButton = new Button();
            settingsButton.FlatStyle = FlatStyle.Flat;
            settingsButton.FlatAppearance.BorderSize = 0;
            settingsButton.Font = new Font("Segoe UI Symbol", 16F);
            settingsButton.Text = "⚙";
            settingsButton.ForeColor = mutedTextColor;
            settingsButton.Location = new Point(370, 24);
            settingsButton.Size = new Size(40, 36);
            settingsButton.Cursor = Cursors.Hand;
            settingsButton.Click += SettingsButton_Click;

            helperLabel = new Label();
            helperLabel.AutoSize = false;
            helperLabel.Text = "Wi-Fi 网关助手";
            helperLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            helperLabel.ForeColor = mutedTextColor;
            helperLabel.Location = new Point(30, 58);
            helperLabel.Size = new Size(180, 22);
            helperLabel.AutoEllipsis = true;

            powerButton = new Button();
            powerButton.FlatStyle = FlatStyle.Flat;
            powerButton.FlatAppearance.BorderSize = 0;
            powerButton.Font = new Font("Segoe UI Symbol", 54F, FontStyle.Regular);
            powerButton.Location = new Point(130, 122);
            powerButton.Size = new Size(180, 180);
            powerButton.Text = "⏻";
            powerButton.UseVisualStyleBackColor = false;
            powerButton.Cursor = Cursors.Hand;
            powerButton.Click += PowerButton_Click;
            powerButton.SizeChanged += (sender, args) => ApplyCircleRegion(powerButton);
            ApplyCircleRegion(powerButton);

            routeStateLabel = new Label();
            routeStateLabel.AutoSize = false;
            routeStateLabel.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            routeStateLabel.ForeColor = mutedTextColor;
            routeStateLabel.TextAlign = ContentAlignment.MiddleCenter;
            routeStateLabel.Location = new Point(40, 318);
            routeStateLabel.Size = new Size(360, 30);
            routeStateLabel.AutoEllipsis = true;

            cardPanel = new Panel();
            cardPanel.BackColor = cardColor;
            cardPanel.Location = new Point(30, 382);
            cardPanel.Size = new Size(380, 190);
            cardPanel.Paint += CardPanel_Paint;
            cardPanel.SizeChanged += (sender, args) => ApplyRoundedRegion(cardPanel, ScaleInt(22));
            ApplyRoundedRegion(cardPanel, ScaleInt(22));
            cardPanel.SuspendLayout();

            cardTitleLabel = new Label();
            cardTitleLabel.AutoSize = false;
            cardTitleLabel.Text = "网络诊断";
            cardTitleLabel.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
            cardTitleLabel.ForeColor = textColor;
            cardTitleLabel.Location = new Point(22, 20);
            cardTitleLabel.Size = new Size(150, 30);
            cardTitleLabel.AutoEllipsis = true;

            connectionBadgeLabel = new Label();
            connectionBadgeLabel.AutoSize = false;
            connectionBadgeLabel.Text = "未测试";
            connectionBadgeLabel.TextAlign = ContentAlignment.MiddleCenter;
            connectionBadgeLabel.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold);
            connectionBadgeLabel.ForeColor = mutedTextColor;
            connectionBadgeLabel.BackColor = Color.FromArgb(50, 53, 60);
            connectionBadgeLabel.Location = new Point(264, 22);
            connectionBadgeLabel.Size = new Size(88, 24);
            connectionBadgeLabel.SizeChanged += (sender, args) => ApplyRoundedRegion(connectionBadgeLabel, ScaleInt(12));
            ApplyRoundedRegion(connectionBadgeLabel, ScaleInt(12));

            gatewayCaptionLabel = new Label();
            gatewayCaptionLabel.AutoSize = false;
            gatewayCaptionLabel.Text = "GPT 延迟:";
            gatewayCaptionLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            gatewayCaptionLabel.ForeColor = Color.FromArgb(140, 144, 159);
            gatewayCaptionLabel.Location = new Point(24, 66);
            gatewayCaptionLabel.Size = new Size(120, 24);
            gatewayCaptionLabel.AutoEllipsis = true;

            gatewayValueLabel = new Label();
            gatewayValueLabel.AutoSize = false;
            gatewayValueLabel.Text = "--s";
            gatewayValueLabel.Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold);
            gatewayValueLabel.ForeColor = primaryColor;
            gatewayValueLabel.TextAlign = ContentAlignment.MiddleRight;
            gatewayValueLabel.Location = new Point(190, 58);
            gatewayValueLabel.Size = new Size(160, 42);
            gatewayValueLabel.AutoEllipsis = true;

            progressTrack = new Panel();
            progressTrack.BackColor = Color.FromArgb(38, 42, 50);
            progressTrack.Location = new Point(24, 104);
            progressTrack.Size = new Size(328, 7);
            progressTrack.SizeChanged += (sender, args) => ApplyRoundedRegion(progressTrack, ScaleInt(4));
            ApplyRoundedRegion(progressTrack, ScaleInt(4));

            progressFill = new Panel();
            progressFill.BackColor = primaryColor;
            progressFill.Location = new Point(0, 0);
            progressFill.Size = new Size(0, 7);
            progressFill.SizeChanged += (sender, args) => ApplyRoundedRegion(progressFill, ScaleInt(4));
            ApplyRoundedRegion(progressFill, ScaleInt(4));
            progressTrack.Controls.Add(progressFill);

            detailLabel = new Label();
            detailLabel.AutoSize = false;
            detailLabel.Text = "点击测试延迟检查 ChatGPT 访问。";
            detailLabel.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            detailLabel.ForeColor = mutedTextColor;
            detailLabel.Location = new Point(24, 118);
            detailLabel.Size = new Size(328, 22);
            detailLabel.AutoEllipsis = true;

            testButton = new Button();
            testButton.Text = "测试延迟";
            testButton.Location = new Point(24, 146);
            testButton.Size = new Size(328, 36);
            StylePrimaryButton(testButton);
            testButton.Click += TestButton_Click;

            Controls.Add(titleLabel);
            Controls.Add(settingsButton);
            Controls.Add(helperLabel);
            Controls.Add(powerButton);
            Controls.Add(routeStateLabel);
            Controls.Add(cardPanel);
            cardPanel.Controls.Add(cardTitleLabel);
            cardPanel.Controls.Add(connectionBadgeLabel);
            cardPanel.Controls.Add(gatewayCaptionLabel);
            cardPanel.Controls.Add(gatewayValueLabel);
            cardPanel.Controls.Add(progressTrack);
            cardPanel.Controls.Add(detailLabel);
            cardPanel.Controls.Add(testButton);
            testButton.SizeChanged += (sender, args) => ApplyRoundedRegion(testButton, ScaleInt(12));
            cardPanel.ResumeLayout(false);
            ResumeLayout(false);
            ApplyDpiAwareLayout();
            ReapplyControlRegions();

            loadingTimer = new System.Windows.Forms.Timer();
            loadingTimer.Interval = 500;
            loadingTimer.Tick += LoadingTimer_Tick;

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 3000;
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            string configIp = GatewayManager.GetConfigGatewayIp();
            enabledByApp = GatewayManager.IsTargetActive(configIp);
            UpdateRouteVisualState();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDpiAwareLayout();
            TryUseDarkTitleBar();
            TrySetAppIcon();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyDpiAwareLayout();
            ReapplyControlRegions();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ReapplyControlRegions();
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void ApplyDpiAwareLayout()
        {
            dpiScale = GetCurrentDpiScale();
            Size clientSize = ScaleSize(BaseClientWidth, BaseClientHeight);

            SuspendLayout();
            if (cardPanel != null)
            {
                cardPanel.SuspendLayout();
            }

            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            ClientSize = clientSize;
            Size fixedWindowSize = SizeFromClientSize(clientSize);
            MinimumSize = fixedWindowSize;
            MaximumSize = fixedWindowSize;
            Font = UiFont(12F, FontStyle.Regular);

            titleLabel.Font = UiFont(24F, FontStyle.Bold);
            titleLabel.Location = ScalePoint(28, 22);
            titleLabel.Size = ScaleSize(270, 40);

            settingsButton.Font = UiFont(22F, FontStyle.Regular, "Segoe UI Symbol");
            settingsButton.Location = ScalePoint(370, 24);
            settingsButton.Size = ScaleSize(40, 36);

            helperLabel.Font = UiFont(12F, FontStyle.Bold);
            helperLabel.Location = ScalePoint(30, 60);
            helperLabel.Size = ScaleSize(210, 22);

            powerButton.Font = UiFont(72F, FontStyle.Regular, "Segoe UI Symbol");
            powerButton.Location = ScalePoint(130, 122);
            powerButton.Size = ScaleSize(180, 180);

            routeStateLabel.Font = UiFont(15F, FontStyle.Bold);
            routeStateLabel.Location = ScalePoint(40, 318);
            routeStateLabel.Size = ScaleSize(360, 32);

            cardPanel.Location = ScalePoint(30, 382);
            cardPanel.Size = ScaleSize(380, 190);

            cardTitleLabel.Font = UiFont(20F, FontStyle.Bold);
            cardTitleLabel.Location = ScalePoint(22, 18);
            cardTitleLabel.Size = ScaleSize(170, 34);

            connectionBadgeLabel.Font = UiFont(11F, FontStyle.Bold);
            connectionBadgeLabel.Location = ScalePoint(264, 21);
            connectionBadgeLabel.Size = ScaleSize(88, 26);

            gatewayCaptionLabel.Font = UiFont(14F, FontStyle.Bold);
            gatewayCaptionLabel.Location = ScalePoint(24, 66);
            gatewayCaptionLabel.Size = ScaleSize(130, 26);

            gatewayValueLabel.Font = UiFont(28F, FontStyle.Bold);
            gatewayValueLabel.Location = ScalePoint(176, 54);
            gatewayValueLabel.Size = ScaleSize(176, 48);

            progressTrack.Location = ScalePoint(24, 106);
            progressTrack.Size = ScaleSize(BaseProgressWidth, 7);

            detailLabel.Font = UiFont(11F, FontStyle.Regular);
            detailLabel.Location = ScalePoint(24, 121);
            detailLabel.Size = ScaleSize(BaseProgressWidth, 22);

            testButton.Font = UiFont(12F, FontStyle.Bold);
            testButton.Location = ScalePoint(24, 148);
            testButton.Size = ScaleSize(BaseProgressWidth, 36);

            ApplyProgressWidth();

            if (cardPanel != null)
            {
                cardPanel.ResumeLayout(false);
                cardPanel.Invalidate();
            }
            ResumeLayout(false);
            ReapplyControlRegions();
            Invalidate();
        }

        private float GetCurrentDpiScale()
        {
            try
            {
                float dpiRatio;
                using (Graphics graphics = IsHandleCreated ? CreateGraphics() : Graphics.FromHwnd(IntPtr.Zero))
                {
                    dpiRatio = graphics.DpiX / 96F;
                }

                // Use the screen work area to compute a proportion-based scale.
                // The design baseline is 1080p (1920x1080 logical pixels at 96dpi).
                // On screens with higher logical resolution (e.g. 4K at 100%/125% DPI),
                // the window must grow proportionally so it occupies the same
                // percentage of the screen regardless of resolution.
                Rectangle workArea;
                try
                {
                    Screen currentScreen = IsHandleCreated
                        ? Screen.FromHandle(Handle)
                        : Screen.PrimaryScreen;
                    workArea = currentScreen.WorkingArea;
                }
                catch
                {
                    workArea = Screen.PrimaryScreen.WorkingArea;
                }

                // WorkingArea is in physical pixels when PerMonitorV2 is declared.
                // Divide by dpiRatio to get logical pixel dimensions, then compute
                // how the logical screen compares to the 1080p reference.
                float logicalWidth = workArea.Width / dpiRatio;
                float logicalHeight = workArea.Height / dpiRatio;

                float scaleByWidth = logicalWidth / ReferenceScreenWidth;
                float scaleByHeight = logicalHeight / ReferenceScreenHeight;
                float screenRatio = Math.Min(scaleByWidth, scaleByHeight);

                // Final scale = dpiRatio (to get physical pixels) * screenRatio
                // (to maintain screen proportion). Clamp to at least 1.
                return Math.Max(1F, dpiRatio * screenRatio);
            }
            catch
            {
                return 1F;
            }
        }

        private int ScaleInt(int value)
        {
            return (int)Math.Round(value * dpiScale);
        }

        private Point ScalePoint(int x, int y)
        {
            return new Point(ScaleInt(x), ScaleInt(y));
        }

        private Size ScaleSize(int width, int height)
        {
            return new Size(Math.Max(1, ScaleInt(width)), Math.Max(1, ScaleInt(height)));
        }

        private Font UiFont(float pixelSize, FontStyle style, string family = "Microsoft YaHei UI")
        {
            return new Font(family, Math.Max(1F, pixelSize * dpiScale), style, GraphicsUnit.Pixel);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            refreshTimer.Stop();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (SolidBrush blueGlow = new SolidBrush(Color.FromArgb(18, primaryColor)))
            using (SolidBrush warmGlow = new SolidBrush(Color.FromArgb(14, 255, 183, 134)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(blueGlow, ScaleInt(285), -ScaleInt(75), ScaleInt(230), ScaleInt(230));
                e.Graphics.FillEllipse(warmGlow, -ScaleInt(95), ScaleInt(455), ScaleInt(250), ScaleInt(250));
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == ShowExistingInstanceMessage)
            {
                RestoreFromTray();
                return;
            }

            base.WndProc(ref m);
        }

        private void LoadingTimer_Tick(object sender, EventArgs e)
        {
            loadingDotCount = (loadingDotCount + 1) % 4;
            routeStateLabel.Text = loadingBaseText + new string('.', loadingDotCount);
        }

        private void StartLoading(string text)
        {
            isProcessing = true;
            powerButton.Enabled = false;
            testButton.Enabled = false;
            loadingBaseText = text;
            loadingDotCount = 0;
            routeStateLabel.Text = text;
            routeStateLabel.ForeColor = primaryColor;
            loadingTimer.Start();
        }

        private void StopLoading()
        {
            isProcessing = false;
            powerButton.Enabled = true;
            testButton.Enabled = true;
            loadingTimer.Stop();
        }

        private async void PowerButton_Click(object sender, EventArgs e)
        {
            if (isProcessing) return;

            if (enabledByApp)
            {
                userManuallyDisabled = true;
                await RestoreWifiAsync();
            }
            else
            {
                userManuallyDisabled = false;
                await EnableGatewayAsync();
            }
        }

        private async System.Threading.Tasks.Task EnableGatewayAsync()
        {
            try
            {
                StartLoading("正在配置旁路由");
                string configIp = GatewayManager.GetConfigGatewayIp();
                await System.Threading.Tasks.Task.Run(() => GatewayManager.Enable(configIp));
                enabledByApp = true;
                StopLoading();
                UpdateRouteVisualState();
                RunLatencyTest();
            }
            catch (Exception ex)
            {
                StopLoading();
                MessageBox.Show(this, "配置旁路由失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                string configIp = GatewayManager.GetConfigGatewayIp();
                enabledByApp = GatewayManager.IsTargetActive(configIp);
                UpdateRouteVisualState();
            }
        }

        private async System.Threading.Tasks.Task RestoreWifiAsync()
        {
            try
            {
                StartLoading("正在恢复原始网络");
                string configIp = GatewayManager.GetConfigGatewayIp();
                await System.Threading.Tasks.Task.Run(() => GatewayManager.Restore(configIp));
                enabledByApp = false;
                StopLoading();
                UpdateRouteVisualState();
            }
            catch (Exception ex)
            {
                StopLoading();
                MessageBox.Show(this, "恢复 Wi-Fi 配置失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                string configIp = GatewayManager.GetConfigGatewayIp();
                enabledByApp = GatewayManager.IsTargetActive(configIp);
                UpdateRouteVisualState();
            }
        }

        private void TestButton_Click(object sender, EventArgs e)
        {
            RunLatencyTest();
        }

        private void RunLatencyTest()
        {
            testButton.Enabled = false;
            testButton.Text = "测试中...";
            connectionBadgeLabel.Text = "检测中";
            connectionBadgeLabel.ForeColor = primaryColor;
            gatewayValueLabel.Text = "--s";
            detailLabel.Text = "正在连接 chatgpt.com...";
            SetProgress(20, primaryDimColor);

            ThreadPool.QueueUserWorkItem(delegate
            {
                LatencyResult result = ChatGptLatencyTester.Test();
                BeginInvoke(new Action(delegate
                {
                    testButton.Enabled = true;
                    testButton.Text = "测试延迟";
                    ApplyLatencyResult(result);
                }));
            });
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            using (SettingsForm sf = new SettingsForm())
            {
                if (sf.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateRouteVisualState();
                }
            }
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (isProcessing) return;

            string configIp = GatewayManager.GetConfigGatewayIp();
            string configSsid = GatewayManager.GetConfigSsid();
            bool autoEnable = GatewayManager.GetConfigAutoEnable();

            bool active = GatewayManager.IsTargetActive(configIp);
            if (active != enabledByApp)
            {
                enabledByApp = active;
                UpdateRouteVisualState();
            }

            // SSID Auto-switch logic
            string currentSsid = GatewayManager.GetActiveWifiSsid();
            if (currentSsid != lastSsid)
            {
                userManuallyDisabled = false;
                lastSsid = currentSsid;
            }

            if (!string.IsNullOrEmpty(configSsid))
            {
                if (enabledByApp)
                {
                    if (currentSsid != configSsid)
                    {
                        await RestoreWifiAsync();
                        trayIcon.ShowBalloonTip(3000, "旁路由助手", "当前连接的 Wi-Fi (" + (string.IsNullOrEmpty(currentSsid) ? "有线/无无线连接" : currentSsid) + ") 未配置旁路由，已自动恢复默认网络。", ToolTipIcon.Info);
                    }
                }
                else
                {
                    if (currentSsid == configSsid && autoEnable && !userManuallyDisabled)
                    {
                        await EnableGatewayAsync();
                        trayIcon.ShowBalloonTip(3000, "旁路由助手", "已连接到指定 Wi-Fi，已自动启用旁路由。", ToolTipIcon.Info);
                    }
                }
            }
        }

        private void UpdateRouteVisualState()
        {
            WifiConfig current = GatewayManager.GetCurrentNetworkConfig();
            string typeStr = current == null ? "网络" : (current.IsWifi ? "Wi-Fi" : "以太网");
            string alias = current == null ? "未检测到活动连接" : current.InterfaceAlias;

            if (enabledByApp)
            {
                powerButton.BackColor = primaryColor;
                powerButton.ForeColor = Color.FromArgb(0, 46, 106);
                routeStateLabel.Text = alias + " 已切到旁路由";
                routeStateLabel.ForeColor = primaryColor;
            }
            else
            {
                powerButton.BackColor = Color.FromArgb(50, 53, 60);
                powerButton.ForeColor = Color.FromArgb(194, 198, 214);
                routeStateLabel.Text = alias + " 未启用旁路由";
                routeStateLabel.ForeColor = mutedTextColor;
            }
        }

        private void ApplyLatencyResult(LatencyResult result)
        {
            if (result.Success)
            {
                double seconds = result.ElapsedMilliseconds / 1000.0;
                if (seconds < 1.0)
                {
                    gatewayValueLabel.Text = seconds.ToString("0.00") + "s";
                    connectionBadgeLabel.Text = "连接稳定";
                    connectionBadgeLabel.ForeColor = successColor;
                    detailLabel.Text = "ChatGPT 可达。";
                    SetProgress(300, successColor);
                }
                else if (seconds <= 3.0)
                {
                    gatewayValueLabel.Text = seconds.ToString("0.00") + "s";
                    connectionBadgeLabel.Text = "连接较慢";
                    connectionBadgeLabel.ForeColor = warningColor;
                    detailLabel.Text = "ChatGPT 可达，但响应偏慢。";
                    SetProgress(180, warningColor);
                }
                else
                {
                    gatewayValueLabel.Text = "--s";
                    connectionBadgeLabel.Text = "已超时";
                    connectionBadgeLabel.ForeColor = dangerColor;
                    detailLabel.Text = "超时中止测试。";
                    SetProgress(42, dangerColor);
                }
            }
            else
            {
                gatewayValueLabel.Text = "--s";
                connectionBadgeLabel.Text = result.TimedOut ? "已超时" : "连接失败";
                connectionBadgeLabel.ForeColor = dangerColor;
                detailLabel.Text = result.TimedOut ? "超时中止测试。" : result.ErrorMessage;
                SetProgress(42, dangerColor);
            }
        }

        private void SetProgress(int width, Color color)
        {
            if (width < 0)
            {
                width = 0;
            }
            if (width > BaseProgressWidth)
            {
                width = BaseProgressWidth;
            }

            progressDesignWidth = width;
            progressFill.BackColor = color;
            ApplyProgressWidth();
        }

        private void ApplyProgressWidth()
        {
            if (progressTrack == null || progressFill == null)
            {
                return;
            }

            int width = (int)Math.Round(progressTrack.Width * (progressDesignWidth / (double)BaseProgressWidth));
            if (width < 0)
            {
                width = 0;
            }
            if (width > progressTrack.Width)
            {
                width = progressTrack.Width;
            }

            progressFill.Location = Point.Empty;
            progressFill.Size = new Size(width, progressTrack.Height);
            ApplyRoundedRegion(progressFill, ScaleInt(4));
        }

        private void ReapplyControlRegions()
        {
            if (powerButton != null)
            {
                ApplyCircleRegion(powerButton);
            }
            if (cardPanel != null)
            {
                ApplyRoundedRegion(cardPanel, ScaleInt(22));
            }
            if (connectionBadgeLabel != null)
            {
                ApplyRoundedRegion(connectionBadgeLabel, ScaleInt(12));
            }
            if (progressTrack != null)
            {
                ApplyRoundedRegion(progressTrack, ScaleInt(4));
            }
            if (progressFill != null)
            {
                ApplyRoundedRegion(progressFill, ScaleInt(4));
            }
            if (testButton != null)
            {
                ApplyRoundedRegion(testButton, ScaleInt(12));
            }
        }

        private void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = primaryColor;
            button.ForeColor = Color.FromArgb(0, 46, 106);
            button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            ApplyRoundedRegion(button, ScaleInt(12));
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen border = new Pen(cardBorderColor))
            using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, cardPanel.Width - 1, cardPanel.Height - 1), ScaleInt(22)))
            {
                e.Graphics.DrawPath(border, path);
            }
        }

        private void TrySetAppIcon()
        {
            try
            {
                Icon embeddedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (embeddedIcon != null)
                {
                    Icon = embeddedIcon;
                    if (trayIcon != null)
                    {
                        trayIcon.Icon = embeddedIcon;
                    }
                    if (IsHandleCreated)
                    {
                        SendMessage(Handle, WM_SETICON, IconSmall, embeddedIcon.Handle);
                        SendMessage(Handle, WM_SETICON, IconBig, embeddedIcon.Handle);
                    }
                }
            }
            catch
            {
            }
        }

        private void HideToTray(bool showTip)
        {
            Hide();
        }

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            BringToFront();
            Activate();
        }

        public static void SignalExistingInstance()
        {
            PostMessage(HwndBroadcast, ShowExistingInstanceMessage, IntPtr.Zero, IntPtr.Zero);
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void ShowMainWindowMenuItem_Click(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            allowExit = true;
            Close();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void TryUseDarkTitleBar()
        {
            try
            {
                int useDark = 1;
                DwmSetWindowAttribute(Handle, 20, ref useDark, Marshal.SizeOf(typeof(int)));
                DwmSetWindowAttribute(Handle, 19, ref useDark, Marshal.SizeOf(typeof(int)));
            }
            catch
            {
            }
        }

        private static void ApplyCircleRegion(Control control)
        {
            if (control.Width <= 0 || control.Height <= 0)
            {
                control.Region = null;
                return;
            }

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, control.Width, control.Height);
                control.Region = new Region(path);
            }
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0)
            {
                control.Region = null;
                return;
            }

            int safeRadius = Math.Min(radius, Math.Min(control.Width, control.Height) / 2);
            if (safeRadius <= 0)
            {
                control.Region = null;
                return;
            }

            using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, control.Width, control.Height), safeRadius))
            {
                control.Region = new Region(path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int safeRadius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            if (safeRadius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            diameter = safeRadius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class WifiConfig
    {
        public string InterfaceAlias;
        public string InterfaceId;
        public int InterfaceIndex;
        public string IPv4Address;
        public string Gateway;
        public string GatewayIpv6;
        public List<string> Ipv6Gateways = new List<string>();
        public List<string> DnsServers = new List<string>();
        public bool IsDnsDhcp;
        public bool IsWifi;
    }

    internal sealed class SettingsForm : Form
    {
        private const int BaseWindowWidth = 380;
        private const int BaseWindowHeight = 370;
        private const int ReferenceScreenWidth = 1920;
        private const int ReferenceScreenHeight = 1080;

        private readonly Color backgroundColor = Color.FromArgb(16, 19, 26);
        private readonly Color cardColor = Color.FromArgb(29, 32, 39);
        private readonly Color cardBorderColor = Color.FromArgb(66, 71, 84);
        private readonly Color primaryColor = Color.FromArgb(173, 198, 255);
        private readonly Color textColor = Color.FromArgb(225, 226, 236);
        private readonly Color mutedTextColor = Color.FromArgb(194, 198, 214);

        private readonly Label lblIp;
        private readonly TextBox txtIp;
        private readonly Label lblIpv6;
        private readonly TextBox txtIpv6;
        private readonly Label lblSsid;
        private readonly TextBox txtSsid;
        private readonly Button btnGetSsid;
        private readonly CheckBox chkAutoEnable;
        private readonly Button btnSave;
        private readonly Button btnCancel;
        private float dpiScale = 1F;

        public SettingsForm()
        {
            Text = "设置";
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(BaseWindowWidth, BaseWindowHeight);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = backgroundColor;
            ForeColor = textColor;
            Font = new Font("Microsoft YaHei UI", 9F);
            SuspendLayout();

            lblIp = new Label { Text = "旁路由 IP 地址:", Location = new Point(25, 20), Size = new Size(200, 20), ForeColor = mutedTextColor };
            txtIp = new TextBox { Text = GatewayManager.GetConfigGatewayIp(), Location = new Point(25, 42), Size = new Size(310, 25), BackColor = cardColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };

            lblIpv6 = new Label { Text = "旁路由 IPv6 地址 (可选):", Location = new Point(25, 80), Size = new Size(200, 20), ForeColor = mutedTextColor };
            txtIpv6 = new TextBox { Text = GatewayManager.GetConfigGatewayIpv6(), Location = new Point(25, 102), Size = new Size(310, 25), BackColor = cardColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };

            lblSsid = new Label { Text = "绑定 Wi-Fi SSID (留空表示不限制):", Location = new Point(25, 142), Size = new Size(250, 20), ForeColor = mutedTextColor };
            txtSsid = new TextBox { Text = GatewayManager.GetConfigSsid(), Location = new Point(25, 164), Size = new Size(210, 25), BackColor = cardColor, ForeColor = textColor, BorderStyle = BorderStyle.FixedSingle };
            
            btnGetSsid = new Button { Text = "获取当前", Location = new Point(245, 163), Size = new Size(90, 26), FlatStyle = FlatStyle.Flat, BackColor = cardColor, ForeColor = primaryColor };
            btnGetSsid.FlatAppearance.BorderColor = cardBorderColor;
            btnGetSsid.Click += BtnGetSsid_Click;

            chkAutoEnable = new CheckBox { Text = "连上该 Wi-Fi 时自动启用旁路由", Checked = GatewayManager.GetConfigAutoEnable(), Location = new Point(25, 205), Size = new Size(310, 25), ForeColor = textColor };

            btnSave = new Button { Text = "保存", Location = new Point(145, 265), Size = new Size(90, 35), FlatStyle = FlatStyle.Flat, BackColor = primaryColor, ForeColor = Color.FromArgb(0, 46, 106) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "取消", Location = new Point(245, 265), Size = new Size(90, 35), FlatStyle = FlatStyle.Flat, BackColor = cardColor, ForeColor = mutedTextColor };
            btnCancel.FlatAppearance.BorderColor = cardBorderColor;
            btnCancel.Click += (s, e) => Close();

            Controls.Add(lblIp);
            Controls.Add(txtIp);
            Controls.Add(lblIpv6);
            Controls.Add(txtIpv6);
            Controls.Add(lblSsid);
            Controls.Add(txtSsid);
            Controls.Add(btnGetSsid);
            Controls.Add(chkAutoEnable);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            ResumeLayout(false);
            ApplyDpiAwareLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDpiAwareLayout();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyDpiAwareLayout();
        }

        private void ApplyDpiAwareLayout()
        {
            dpiScale = GetCurrentDpiScale();
            Size scaledWindowSize = ScaleSize(BaseWindowWidth, BaseWindowHeight);

            SuspendLayout();
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            Size = scaledWindowSize;
            MinimumSize = scaledWindowSize;
            MaximumSize = scaledWindowSize;
            Font = UiFont(12F, FontStyle.Regular);

            lblIp.Font = UiFont(12F, FontStyle.Regular);
            lblIp.Location = ScalePoint(25, 20);
            lblIp.Size = ScaleSize(220, 22);

            txtIp.Font = UiFont(12F, FontStyle.Regular);
            txtIp.Location = ScalePoint(25, 42);
            txtIp.Size = ScaleSize(310, 26);

            lblIpv6.Font = UiFont(12F, FontStyle.Regular);
            lblIpv6.Location = ScalePoint(25, 80);
            lblIpv6.Size = ScaleSize(220, 22);

            txtIpv6.Font = UiFont(12F, FontStyle.Regular);
            txtIpv6.Location = ScalePoint(25, 102);
            txtIpv6.Size = ScaleSize(310, 26);

            lblSsid.Font = UiFont(12F, FontStyle.Regular);
            lblSsid.Location = ScalePoint(25, 142);
            lblSsid.Size = ScaleSize(280, 22);

            txtSsid.Font = UiFont(12F, FontStyle.Regular);
            txtSsid.Location = ScalePoint(25, 164);
            txtSsid.Size = ScaleSize(210, 26);

            btnGetSsid.Font = UiFont(12F, FontStyle.Regular);
            btnGetSsid.Location = ScalePoint(245, 163);
            btnGetSsid.Size = ScaleSize(90, 28);

            chkAutoEnable.Font = UiFont(12F, FontStyle.Regular);
            chkAutoEnable.Location = ScalePoint(25, 205);
            chkAutoEnable.Size = ScaleSize(310, 28);

            btnSave.Font = UiFont(12F, FontStyle.Bold);
            btnSave.Location = ScalePoint(145, 265);
            btnSave.Size = ScaleSize(90, 36);

            btnCancel.Font = UiFont(12F, FontStyle.Regular);
            btnCancel.Location = ScalePoint(245, 265);
            btnCancel.Size = ScaleSize(90, 36);

            ResumeLayout(false);
        }

        private float GetCurrentDpiScale()
        {
            try
            {
                float dpiRatio;
                using (Graphics graphics = IsHandleCreated ? CreateGraphics() : Graphics.FromHwnd(IntPtr.Zero))
                {
                    dpiRatio = graphics.DpiX / 96F;
                }

                Rectangle workArea;
                try
                {
                    Screen currentScreen = IsHandleCreated
                        ? Screen.FromHandle(Handle)
                        : (Owner != null ? Screen.FromHandle(Owner.Handle) : Screen.PrimaryScreen);
                    workArea = currentScreen.WorkingArea;
                }
                catch
                {
                    workArea = Screen.PrimaryScreen.WorkingArea;
                }

                float logicalWidth = workArea.Width / dpiRatio;
                float logicalHeight = workArea.Height / dpiRatio;

                float scaleByWidth = logicalWidth / ReferenceScreenWidth;
                float scaleByHeight = logicalHeight / ReferenceScreenHeight;
                float screenRatio = Math.Min(scaleByWidth, scaleByHeight);

                return Math.Max(1F, dpiRatio * screenRatio);
            }
            catch
            {
                return 1F;
            }
        }

        private int ScaleInt(int value)
        {
            return (int)Math.Round(value * dpiScale);
        }

        private Point ScalePoint(int x, int y)
        {
            return new Point(ScaleInt(x), ScaleInt(y));
        }

        private Size ScaleSize(int width, int height)
        {
            return new Size(Math.Max(1, ScaleInt(width)), Math.Max(1, ScaleInt(height)));
        }

        private Font UiFont(float pixelSize, FontStyle style, string family = "Microsoft YaHei UI")
        {
            return new Font(family, Math.Max(1F, pixelSize * dpiScale), style, GraphicsUnit.Pixel);
        }

        private void BtnGetSsid_Click(object sender, EventArgs e)
        {
            string ssid = GatewayManager.GetActiveWifiSsid();
            if (string.IsNullOrEmpty(ssid))
            {
                MessageBox.Show(this, "未能检测到已连接的 Wi-Fi。请确认已开启 Wi-Fi 并连入网络。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                txtSsid.Text = ssid;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string ip = txtIp.Text.Trim();
            if (string.IsNullOrEmpty(ip) || !System.Net.IPAddress.TryParse(ip, out var ipv4Addr) || ipv4Addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                MessageBox.Show(this, "请输入有效的旁路由 IPv4 地址！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string ipv6 = txtIpv6.Text.Trim();
            if (!string.IsNullOrEmpty(ipv6))
            {
                string normalizedIpv6;
                string ipv6Error;
                if (!GatewayManager.TryNormalizeGatewayIpv6Input(ipv6, out normalizedIpv6, out ipv6Error))
                {
                    MessageBox.Show(this, ipv6Error, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                ipv6 = normalizedIpv6;
            }

            GatewayManager.SaveSettings(ip, ipv6, txtSsid.Text.Trim(), chkAutoEnable.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal static class GatewayManager
    {
        private const string RegistryPath = "Software\\YuanxinGatewaySwitch";
        private const string DefaultGatewayIp = "192.168.3.187";
        private const string DefaultGatewayIpv6 = "fe80::20c:29ff:fe2b:cd7d";

        public static bool TryNormalizeGatewayIpv6Input(string value, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            string candidate = (value ?? string.Empty).Trim();
            if (candidate.Length == 0)
            {
                return true;
            }

            if (candidate.StartsWith("[") && candidate.Contains("]"))
            {
                int endBracket = candidate.IndexOf(']');
                candidate = candidate.Substring(1, endBracket - 1).Trim();
            }

            int slashIndex = candidate.IndexOf('/');
            if (slashIndex >= 0)
            {
                string prefixText = candidate.Substring(slashIndex + 1).Trim();
                candidate = candidate.Substring(0, slashIndex).Trim();
                int prefixLength;
                if (prefixText.Length > 0 && (!int.TryParse(prefixText, out prefixLength) || prefixLength < 0 || prefixLength > 128))
                {
                    error = "IPv6 前缀长度需在 0 到 128 之间。";
                    return false;
                }
            }

            int zoneIndex = candidate.IndexOf('%');
            if (zoneIndex >= 0)
            {
                candidate = candidate.Substring(0, zoneIndex).Trim();
            }

            IPAddress ipv6Address;
            if (!IPAddress.TryParse(candidate, out ipv6Address) || ipv6Address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                error = "请输入有效的旁路由 IPv6 地址。\n可填: fe80::20c:29ff:fe2b:cd7d\n也兼容: fe80::20c:29ff:fe2b:cd7d/64";
                return false;
            }

            normalized = ipv6Address.ToString();
            return true;
        }

        public static string GetActiveWifiSsid()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    string[] lines = output.Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("BSSID"))
                        {
                            int idx = trimmed.IndexOf(':');
                            if (idx >= 0)
                            {
                                return trimmed.Substring(idx + 1).Trim();
                            }
                        }
                    }
                }
            }
            catch {}
            return string.Empty;
        }

        public static string GetConfigGatewayIp()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key != null)
                {
                    return Convert.ToString(key.GetValue("ConfigGatewayIp", DefaultGatewayIp));
                }
            }
            return DefaultGatewayIp;
        }

        public static string GetConfigGatewayIpv6()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key != null)
                {
                    return Convert.ToString(key.GetValue("ConfigGatewayIpv6", DefaultGatewayIpv6));
                }
            }
            return DefaultGatewayIpv6;
        }

        public static string GetConfigSsid()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key != null)
                {
                    return Convert.ToString(key.GetValue("ConfigSsid", ""));
                }
            }
            return "";
        }

        public static bool GetConfigAutoEnable()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key != null)
                {
                    return Convert.ToInt32(key.GetValue("ConfigAutoEnable", 1)) == 1;
                }
            }
            return true;
        }

        public static void SaveSettings(string ip, string ipv6, string ssid, bool autoEnable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("ConfigGatewayIp", ip ?? string.Empty, RegistryValueKind.String);
                key.SetValue("ConfigGatewayIpv6", ipv6 ?? string.Empty, RegistryValueKind.String);
                key.SetValue("ConfigSsid", ssid ?? string.Empty, RegistryValueKind.String);
                key.SetValue("ConfigAutoEnable", autoEnable ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        public static WifiConfig GetCurrentNetworkConfig()
        {
            List<WifiConfig> candidates = new List<WifiConfig>();

            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (item.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 && 
                    item.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                {
                    continue;
                }

                // Filter virtual adapters
                string desc = item.Description.ToLower();
                if (desc.Contains("virtual") || desc.Contains("pseudo") || desc.Contains("loopback") || 
                    desc.Contains("hyper-v") || desc.Contains("wsl") || desc.Contains("vmware") || 
                    desc.Contains("virtualbox") || desc.Contains("vpn"))
                {
                    continue;
                }

                IPInterfaceProperties props = item.GetIPProperties();
                IPv4InterfaceProperties ipv4Props = props.GetIPv4Properties();
                if (ipv4Props == null)
                {
                    continue;
                }

                string address = null;
                foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        address = ip.Address.ToString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(address))
                {
                    continue;
                }

                WifiConfig config = new WifiConfig();
                config.InterfaceAlias = item.Name;
                config.InterfaceId = item.Id;
                config.InterfaceIndex = ipv4Props.Index;
                config.IPv4Address = address;
                config.IsWifi = item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + item.Id))
                {
                    if (key != null)
                    {
                        string nameServer = key.GetValue("NameServer") as string;
                        config.IsDnsDhcp = string.IsNullOrEmpty(nameServer);
                    }
                }

                foreach (GatewayIPAddressInformation gateway in props.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        config.Gateway = gateway.Address.ToString();
                    }
                    else if (gateway.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        string gatewayIpv6 = NormalizeAddressText(gateway.Address.ToString());
                        if (!string.IsNullOrEmpty(gatewayIpv6))
                        {
                            config.Ipv6Gateways.Add(gatewayIpv6);
                            if (string.IsNullOrEmpty(config.GatewayIpv6))
                            {
                                config.GatewayIpv6 = gatewayIpv6;
                            }
                        }
                    }
                }

                foreach (IPAddress dns in props.DnsAddresses)
                {
                    if (dns.AddressFamily == AddressFamily.InterNetwork ||
                        dns.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        config.DnsServers.Add(dns.ToString());
                    }
                }

                candidates.Add(config);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            string activeSsid = GetActiveWifiSsid();
            if (!string.IsNullOrEmpty(activeSsid))
            {
                foreach (WifiConfig candidate in candidates)
                {
                    if (candidate.IsWifi)
                    {
                        return candidate;
                    }
                }
            }

            foreach (WifiConfig candidate in candidates)
            {
                if (candidate.IsWifi && (!string.IsNullOrEmpty(candidate.Gateway) || candidate.Ipv6Gateways.Count > 0))
                {
                    return candidate;
                }
            }

            foreach (WifiConfig candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate.Gateway) || candidate.Ipv6Gateways.Count > 0)
                {
                    return candidate;
                }
            }

            return candidates[0];
        }

        public static bool IsTargetActive(string targetGateway)
        {
            WifiConfig current = GetCurrentNetworkConfig();
            if (current == null)
            {
                return false;
            }

            bool gatewayMatch = AddressesEqual(current.Gateway, targetGateway) ||
                HasDefaultRoute(current.InterfaceIndex, "0.0.0.0/0", targetGateway);
            bool dnsMatch = ContainsAddress(current.DnsServers, targetGateway);

            string targetGatewayIpv6 = GetNormalizedConfigGatewayIpv6();
            if (!string.IsNullOrEmpty(targetGatewayIpv6))
            {
                gatewayMatch = gatewayMatch && (ContainsAddress(current.Ipv6Gateways, targetGatewayIpv6) ||
                    HasDefaultRoute(current.InterfaceIndex, "::/0", targetGatewayIpv6));
                dnsMatch = dnsMatch && ContainsAddress(current.DnsServers, targetGatewayIpv6);
            }

            return gatewayMatch && dnsMatch;
        }

        public static void Enable(string targetGateway)
        {
            WifiConfig current = GetCurrentNetworkConfig();
            if (current == null)
            {
                throw new InvalidOperationException("没有检测到已连接的网络适配器。");
            }

            if (!current.IsWifi && !string.IsNullOrEmpty(GetConfigSsid()))
            {
                throw new InvalidOperationException("当前没有检测到已连接的 Wi-Fi，已阻止修改非 WLAN 网卡。");
            }

            SaveOriginal(current);
            string targetGatewayIpv6 = GetNormalizedConfigGatewayIpv6(true);

            string command = "$idx=" + current.InterfaceIndex + ";"
                + "$gw='" + EscapePowerShell(targetGateway) + "';"
                + "$ErrorActionPreference='Stop';"
                + PowerShellNormalizeFunction()
                + PowerShellDnsFunction()
                + "Set-StaticDns $idx @() @();"
                + "foreach($store in @('ActiveStore','PersistentStore')){"
                + "Get-NetRoute -PolicyStore $store -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue;"
                + "Get-NetRoute -PolicyStore $store -InterfaceIndex $idx -DestinationPrefix '::/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue;"
                + "}"
                + "New-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -NextHop $gw -RouteMetric 1 -PolicyStore ActiveStore -ErrorAction Stop;"
                + "New-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -NextHop $gw -RouteMetric 1 -PolicyStore PersistentStore -ErrorAction SilentlyContinue;";

            if (!string.IsNullOrEmpty(targetGatewayIpv6))
            {
                string gw6 = EscapePowerShell(targetGatewayIpv6);
                command += "$gw6='" + gw6 + "';"
                    + "$gw6Scoped=Add-ScopeIfNeeded $gw6 $idx;"
                    + "$gw6Norm=Normalize-Address $gw6;"
                    + "try{New-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -NextHop $gw6 -RouteMetric 1 -PolicyStore ActiveStore -ErrorAction Stop;}catch{New-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -NextHop $gw6Scoped -RouteMetric 1 -PolicyStore ActiveStore -ErrorAction Stop;}"
                    + "try{New-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -NextHop $gw6 -RouteMetric 1 -PolicyStore PersistentStore -ErrorAction SilentlyContinue;}catch{New-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -NextHop $gw6Scoped -RouteMetric 1 -PolicyStore PersistentStore -ErrorAction SilentlyContinue;}"
                    + "Set-StaticDns $idx @($gw) @($gw6);";
            }
            else
            {
                command += "Set-StaticDns $idx @($gw) @();";
            }

            command += "Clear-DnsClientCache -ErrorAction SilentlyContinue;"
                + "Register-DnsClient -ErrorAction SilentlyContinue;";

            RunPowerShell(command);
            VerifyApplied(current.InterfaceIndex, targetGateway, targetGatewayIpv6);
        }

        public static void Restore(string targetGateway)
        {
            OriginalConfig original = LoadOriginal();
            WifiConfig current = GetCurrentNetworkConfig();
            int index = ResolveCurrentInterfaceIndex(original, current);
            if (index <= 0)
            {
                throw new InvalidOperationException("没有可恢复的网卡接口信息。");
            }

            string targetGatewayIpv6 = GetNormalizedConfigGatewayIpv6();

            string command = "$idx=" + index + ";"
                + "$target='" + EscapePowerShell(targetGateway) + "';"
                + PowerShellNormalizeFunction()
                + PowerShellDnsFunction()
                + "foreach($store in @('ActiveStore','PersistentStore')){Get-NetRoute -PolicyStore $store -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Where-Object { $_.NextHop -eq $target } | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue;}";

            if (!string.IsNullOrEmpty(targetGatewayIpv6))
            {
                command += "$target6='" + EscapePowerShell(targetGatewayIpv6) + "';"
                    + "$target6Norm=Normalize-Address $target6;"
                    + "foreach($store in @('ActiveStore','PersistentStore')){Get-NetRoute -PolicyStore $store -InterfaceIndex $idx -DestinationPrefix '::/0' -ErrorAction SilentlyContinue | Where-Object { (Normalize-Address $_.NextHop) -eq $target6Norm } | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue;}";
            }

            if (!string.IsNullOrEmpty(original.Gateway))
            {
                command += "$oldgw='" + EscapePowerShell(original.Gateway) + "';"
                    + "New-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -NextHop $oldgw -RouteMetric 10 -PolicyStore ActiveStore -ErrorAction SilentlyContinue;";
            }

            if (original.Ipv6Gateways.Count > 0)
            {
                command += "$oldgw6=@(" + QuotePowerShellArray(original.Ipv6Gateways) + ");"
                    + "foreach($old in $oldgw6){if([string]::IsNullOrWhiteSpace($old)){continue;}$oldScoped=Add-ScopeIfNeeded $old $idx;try{New-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -NextHop $old -RouteMetric 10 -PolicyStore ActiveStore -ErrorAction SilentlyContinue;}catch{New-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -NextHop $oldScoped -RouteMetric 10 -PolicyStore ActiveStore -ErrorAction SilentlyContinue;}}";
            }

            if (original.IsDnsDhcp || original.DnsServers.Count == 0)
            {
                command += "try{Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses -ErrorAction Stop;}catch{Set-NetshDnsFamily 'ipv4' $idx @();Set-NetshDnsFamily 'ipv6' $idx @();}";
            }
            else
            {
                command += "$dns=@(" + QuotePowerShellArray(original.DnsServers) + ");"
                    + "$dns4=@($dns | Where-Object { $_ -notlike '*:*' });"
                    + "$dns6=@($dns | Where-Object { $_ -like '*:*' });"
                    + "Set-StaticDns $idx $dns4 $dns6;";
            }

            command += "Clear-DnsClientCache -ErrorAction SilentlyContinue;"
                + "Register-DnsClient -ErrorAction SilentlyContinue;";

            RunPowerShell(command);
            ClearOriginal();
        }

        private static void SaveOriginal(WifiConfig config)
        {
            if (OriginalExists())
            {
                return;
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath + "\\Original"))
            {
                key.SetValue("InterfaceAlias", config.InterfaceAlias ?? string.Empty, RegistryValueKind.String);
                key.SetValue("InterfaceId", config.InterfaceId ?? string.Empty, RegistryValueKind.String);
                key.SetValue("InterfaceIndex", config.InterfaceIndex, RegistryValueKind.DWord);
                key.SetValue("Gateway", config.Gateway ?? string.Empty, RegistryValueKind.String);
                key.SetValue("GatewayIpv6", string.Join(";", config.Ipv6Gateways.ToArray()), RegistryValueKind.String);
                key.SetValue("DnsServers", string.Join(";", config.DnsServers.ToArray()), RegistryValueKind.String);
                key.SetValue("IsDnsDhcp", config.IsDnsDhcp ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private static bool OriginalExists()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath + "\\Original", false))
            {
                if (key == null)
                {
                    return false;
                }

                return Convert.ToInt32(key.GetValue("InterfaceIndex", 0)) > 0;
            }
        }

        private static OriginalConfig LoadOriginal()
        {
            OriginalConfig config = new OriginalConfig();
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath + "\\Original", false))
            {
                if (key == null)
                {
                    return config;
                }

                config.InterfaceIndex = Convert.ToInt32(key.GetValue("InterfaceIndex", 0));
                config.InterfaceAlias = Convert.ToString(key.GetValue("InterfaceAlias", string.Empty));
                config.InterfaceId = Convert.ToString(key.GetValue("InterfaceId", string.Empty));
                config.Gateway = Convert.ToString(key.GetValue("Gateway", string.Empty));
                string gatewayIpv6 = Convert.ToString(key.GetValue("GatewayIpv6", string.Empty));
                if (!string.IsNullOrEmpty(gatewayIpv6))
                {
                    config.Ipv6Gateways.AddRange(gatewayIpv6.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                string dns = Convert.ToString(key.GetValue("DnsServers", string.Empty));
                if (!string.IsNullOrEmpty(dns))
                {
                    config.DnsServers.AddRange(dns.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                config.IsDnsDhcp = Convert.ToInt32(key.GetValue("IsDnsDhcp", 0)) == 1;
            }
            return config;
        }

        private static int ResolveCurrentInterfaceIndex(OriginalConfig original, WifiConfig current)
        {
            if (!string.IsNullOrEmpty(original.InterfaceId) || !string.IsNullOrEmpty(original.InterfaceAlias))
            {
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    try
                    {
                        IPInterfaceProperties props = item.GetIPProperties();
                        IPv4InterfaceProperties ipv4Props = props.GetIPv4Properties();
                        if (ipv4Props == null)
                        {
                            continue;
                        }

                        bool idMatch = !string.IsNullOrEmpty(original.InterfaceId) &&
                            string.Equals(item.Id, original.InterfaceId, StringComparison.OrdinalIgnoreCase);
                        bool aliasMatch = !string.IsNullOrEmpty(original.InterfaceAlias) &&
                            string.Equals(item.Name, original.InterfaceAlias, StringComparison.OrdinalIgnoreCase);
                        if (idMatch || aliasMatch)
                        {
                            return ipv4Props.Index;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (current != null && current.InterfaceIndex > 0)
            {
                if (original.InterfaceIndex <= 0 ||
                    string.IsNullOrEmpty(original.InterfaceId) ||
                    string.Equals(current.InterfaceId, original.InterfaceId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current.InterfaceAlias, original.InterfaceAlias, StringComparison.OrdinalIgnoreCase))
                {
                    return current.InterfaceIndex;
                }
            }

            if (original.InterfaceIndex > 0 && InterfaceIndexExists(original.InterfaceIndex))
            {
                return original.InterfaceIndex;
            }

            return 0;
        }

        private static bool InterfaceIndexExists(int interfaceIndex)
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    IPv4InterfaceProperties ipv4Props = item.GetIPProperties().GetIPv4Properties();
                    if (ipv4Props != null && ipv4Props.Index == interfaceIndex)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static void ClearOriginal()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteSubKeyTree("Original", false);
                    }
                }
            }
            catch {}
        }

        private static string GetNormalizedConfigGatewayIpv6()
        {
            return GetNormalizedConfigGatewayIpv6(false);
        }

        private static string GetNormalizedConfigGatewayIpv6(bool throwOnInvalid)
        {
            string raw = GetConfigGatewayIpv6();
            string normalized;
            string error;
            if (TryNormalizeGatewayIpv6Input(raw, out normalized, out error))
            {
                return normalized;
            }

            if (throwOnInvalid)
            {
                throw new InvalidOperationException(error);
            }

            return string.Empty;
        }

        private static bool ContainsAddress(IEnumerable<string> addresses, string target)
        {
            string normalizedTarget = NormalizeAddressText(target);
            if (string.IsNullOrEmpty(normalizedTarget))
            {
                return false;
            }

            foreach (string address in addresses)
            {
                if (NormalizeAddressText(address) == normalizedTarget)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AddressesEqual(string left, string right)
        {
            string normalizedLeft = NormalizeAddressText(left);
            string normalizedRight = NormalizeAddressText(right);
            return normalizedLeft.Length > 0 && normalizedLeft == normalizedRight;
        }

        private static string NormalizeAddressText(string value)
        {
            string candidate = (value ?? string.Empty).Trim();
            if (candidate.Length == 0)
            {
                return string.Empty;
            }

            if (candidate.StartsWith("[") && candidate.Contains("]"))
            {
                int endBracket = candidate.IndexOf(']');
                candidate = candidate.Substring(1, endBracket - 1).Trim();
            }

            int slashIndex = candidate.IndexOf('/');
            if (slashIndex >= 0)
            {
                candidate = candidate.Substring(0, slashIndex).Trim();
            }

            int zoneIndex = candidate.IndexOf('%');
            if (zoneIndex >= 0)
            {
                candidate = candidate.Substring(0, zoneIndex).Trim();
            }

            IPAddress address;
            if (IPAddress.TryParse(candidate, out address))
            {
                return address.ToString().ToLowerInvariant();
            }

            return candidate.ToLowerInvariant();
        }

        private static string PowerShellNormalizeFunction()
        {
            return "function Normalize-Address($value){if($null -eq $value){return ''}$text=$value.ToString().Trim();$slash=$text.IndexOf('/');if($slash -ge 0){$text=$text.Substring(0,$slash)}$zone=$text.IndexOf('%');if($zone -ge 0){$text=$text.Substring(0,$zone)}return $text.ToLowerInvariant();};"
                + "function Add-ScopeIfNeeded($value,$idx){$text=$value.ToString();if($text -like 'fe80:*' -and $text -notlike '*%*'){return ($text + '%' + $idx)}return $text;};";
        }

        private static string PowerShellDnsFunction()
        {
            return "function Set-NetshDnsFamily($family,$idx,[string[]]$addresses){"
                + "if($null -eq $addresses -or $addresses.Count -eq 0){& netsh interface $family set dnsservers \"name=$idx\" source=static address=none validate=no | Out-Null;return;}"
                + "$first=$addresses[0];"
                + "& netsh interface $family set dnsservers \"name=$idx\" source=static \"address=$first\" register=primary validate=no | Out-Null;"
                + "if($LASTEXITCODE -ne 0){throw ($family + ' DNS 写入失败: ' + $first);}"
                + "for($i=1;$i -lt $addresses.Count;$i++){"
                + "$address=$addresses[$i];$order=$i + 1;"
                + "& netsh interface $family add dnsservers \"name=$idx\" \"address=$address\" \"index=$order\" validate=no | Out-Null;"
                + "if($LASTEXITCODE -ne 0){throw ($family + ' DNS 追加失败: ' + $address);}"
                + "}"
                + "};"
                + "function Set-StaticDns($idx,[string[]]$v4,[string[]]$v6){Set-NetshDnsFamily 'ipv4' $idx $v4;Set-NetshDnsFamily 'ipv6' $idx $v6;};";
        }

        private static void VerifyApplied(int interfaceIndex, string targetGateway, string targetGatewayIpv6)
        {
            string command = "$idx=" + interfaceIndex + ";"
                + "$gw='" + EscapePowerShell(targetGateway) + "';"
                + PowerShellNormalizeFunction()
                + "$dns=@((Get-DnsClientServerAddress -InterfaceIndex $idx -ErrorAction Stop).ServerAddresses | ForEach-Object { Normalize-Address $_ });"
                + "$v4Route=Get-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Where-Object { $_.NextHop -eq $gw };"
                + "if(-not $v4Route){throw 'IPv4 默认网关未切换到目标地址。'}"
                + "if(-not ($dns -contains (Normalize-Address $gw))){throw 'DNS 未包含目标 IPv4 地址。'}";

            if (!string.IsNullOrEmpty(targetGatewayIpv6))
            {
                command += "$gw6='" + EscapePowerShell(targetGatewayIpv6) + "';"
                    + "$gw6Norm=Normalize-Address $gw6;"
                    + "$v6Route=Get-NetRoute -InterfaceIndex $idx -DestinationPrefix '::/0' -ErrorAction SilentlyContinue | Where-Object { (Normalize-Address $_.NextHop) -eq $gw6Norm };"
                    + "if(-not $v6Route){throw 'IPv6 默认网关未切换到目标地址。'}"
                    + "if(-not ($dns -contains $gw6Norm)){throw 'DNS 未包含目标 IPv6 地址。'}";
            }

            RunPowerShell(command);
        }

        private static bool HasDefaultRoute(int interfaceIndex, string destinationPrefix, string nextHop)
        {
            if (interfaceIndex <= 0 || string.IsNullOrWhiteSpace(destinationPrefix) || string.IsNullOrWhiteSpace(nextHop))
            {
                return false;
            }

            try
            {
                string command = "$idx=" + interfaceIndex + ";"
                    + "$prefix='" + EscapePowerShell(destinationPrefix) + "';"
                    + "$target='" + EscapePowerShell(nextHop) + "';"
                    + PowerShellNormalizeFunction()
                    + "$targetNorm=Normalize-Address $target;"
                    + "$route=Get-NetRoute -InterfaceIndex $idx -DestinationPrefix $prefix -ErrorAction SilentlyContinue | Where-Object { (Normalize-Address $_.NextHop) -eq $targetNorm } | Select-Object -First 1;"
                    + "if($route){Write-Output 'true'}else{Write-Output 'false'}";
                return string.Equals(RunPowerShell(command).Trim(), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string RunPowerShell(string command)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " + QuoteArgument(command);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
                }
                return output;
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string EscapePowerShell(string value)
        {
            return value.Replace("'", "''");
        }

        private static string QuotePowerShellArray(List<string> values)
        {
            List<string> quoted = new List<string>();
            foreach (string value in values)
            {
                quoted.Add("'" + EscapePowerShell(value) + "'");
            }
            return string.Join(",", quoted.ToArray());
        }

        private sealed class OriginalConfig
        {
            public string InterfaceAlias;
            public string InterfaceId;
            public int InterfaceIndex;
            public string Gateway;
            public List<string> Ipv6Gateways = new List<string>();
            public List<string> DnsServers = new List<string>();
            public bool IsDnsDhcp;
        }
    }

    internal sealed class LatencyResult
    {
        public bool Success;
        public long ElapsedMilliseconds;
        public bool TimedOut;
        public string ErrorMessage;
    }

    internal static class ChatGptLatencyTester
    {
        private const string TargetHost = "chatgpt.com";
        private const int TargetPort = 443;
        private const int TimeoutMs = 6000;

        public static LatencyResult Test()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                using (System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(TargetHost, TargetPort, null, null);
                    bool completed = ar.AsyncWaitHandle.WaitOne(TimeoutMs);
                    stopwatch.Stop();

                    if (!completed || !client.Connected)
                    {
                        try { client.Close(); } catch { }
                        return new LatencyResult
                        {
                            Success = false,
                            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                            TimedOut = true,
                            ErrorMessage = "连接超时。"
                        };
                    }

                    client.EndConnect(ar);
                    return new LatencyResult
                    {
                        Success = true,
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        TimedOut = false,
                        ErrorMessage = string.Empty
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new LatencyResult
                {
                    Success = false,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    TimedOut = stopwatch.ElapsedMilliseconds >= TimeoutMs,
                    ErrorMessage = ShortenError(ex.Message)
                };
            }
        }

        private static string ShortenError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "连接失败。";
            }

            return message.Length > 34 ? message.Substring(0, 34) + "..." : message;
        }
    }
}
