using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace YuanxinGateway
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private const string TargetGateway = "192.168.3.187";

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

        private bool enabledByApp;
        private bool allowExit;
        private bool isProcessing;
        private System.Windows.Forms.Timer loadingTimer;
        private string loadingBaseText;
        private int loadingDotCount;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETICON = 0x0080;
        private static readonly IntPtr IconSmall = new IntPtr(0);
        private static readonly IntPtr IconBig = new IntPtr(1);

        public MainForm()
        {
            Text = "元信旁路由";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 620);
            MinimumSize = new Size(440, 620);
            MaximumSize = new Size(440, 620);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = backgroundColor;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            TrySetAppIcon();

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

            helperLabel = new Label();
            helperLabel.AutoSize = false;
            helperLabel.Text = "Wi-Fi 网关助手";
            helperLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            helperLabel.ForeColor = mutedTextColor;
            helperLabel.Location = new Point(30, 58);
            helperLabel.Size = new Size(180, 22);

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
            ApplyCircleRegion(powerButton);

            routeStateLabel = new Label();
            routeStateLabel.AutoSize = false;
            routeStateLabel.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            routeStateLabel.ForeColor = mutedTextColor;
            routeStateLabel.TextAlign = ContentAlignment.MiddleCenter;
            routeStateLabel.Location = new Point(40, 318);
            routeStateLabel.Size = new Size(360, 30);

            cardPanel = new Panel();
            cardPanel.BackColor = cardColor;
            cardPanel.Location = new Point(30, 382);
            cardPanel.Size = new Size(380, 190);
            cardPanel.Paint += CardPanel_Paint;
            ApplyRoundedRegion(cardPanel, 22);

            cardTitleLabel = new Label();
            cardTitleLabel.AutoSize = false;
            cardTitleLabel.Text = "网络诊断";
            cardTitleLabel.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
            cardTitleLabel.ForeColor = textColor;
            cardTitleLabel.Location = new Point(22, 20);
            cardTitleLabel.Size = new Size(150, 30);

            connectionBadgeLabel = new Label();
            connectionBadgeLabel.AutoSize = false;
            connectionBadgeLabel.Text = "未测试";
            connectionBadgeLabel.TextAlign = ContentAlignment.MiddleCenter;
            connectionBadgeLabel.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold);
            connectionBadgeLabel.ForeColor = mutedTextColor;
            connectionBadgeLabel.BackColor = Color.FromArgb(50, 53, 60);
            connectionBadgeLabel.Location = new Point(264, 22);
            connectionBadgeLabel.Size = new Size(88, 24);
            ApplyRoundedRegion(connectionBadgeLabel, 12);

            gatewayCaptionLabel = new Label();
            gatewayCaptionLabel.AutoSize = false;
            gatewayCaptionLabel.Text = "GPT 延迟:";
            gatewayCaptionLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            gatewayCaptionLabel.ForeColor = Color.FromArgb(140, 144, 159);
            gatewayCaptionLabel.Location = new Point(24, 66);
            gatewayCaptionLabel.Size = new Size(120, 24);

            gatewayValueLabel = new Label();
            gatewayValueLabel.AutoSize = false;
            gatewayValueLabel.Text = "--s";
            gatewayValueLabel.Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold);
            gatewayValueLabel.ForeColor = primaryColor;
            gatewayValueLabel.TextAlign = ContentAlignment.MiddleRight;
            gatewayValueLabel.Location = new Point(190, 58);
            gatewayValueLabel.Size = new Size(160, 42);

            progressTrack = new Panel();
            progressTrack.BackColor = Color.FromArgb(38, 42, 50);
            progressTrack.Location = new Point(24, 104);
            progressTrack.Size = new Size(328, 7);
            ApplyRoundedRegion(progressTrack, 4);

            progressFill = new Panel();
            progressFill.BackColor = primaryColor;
            progressFill.Location = new Point(0, 0);
            progressFill.Size = new Size(0, 7);
            ApplyRoundedRegion(progressFill, 4);
            progressTrack.Controls.Add(progressFill);

            detailLabel = new Label();
            detailLabel.AutoSize = false;
            detailLabel.Text = "点击测试延迟检查 ChatGPT 访问。";
            detailLabel.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            detailLabel.ForeColor = mutedTextColor;
            detailLabel.Location = new Point(24, 118);
            detailLabel.Size = new Size(328, 22);

            testButton = new Button();
            testButton.Text = "测试延迟";
            testButton.Location = new Point(24, 146);
            testButton.Size = new Size(328, 36);
            StylePrimaryButton(testButton);
            testButton.Click += TestButton_Click;

            Controls.Add(titleLabel);
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

            loadingTimer = new System.Windows.Forms.Timer();
            loadingTimer.Interval = 500;
            loadingTimer.Tick += LoadingTimer_Tick;

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 3000;
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            enabledByApp = GatewayManager.IsTargetActive(TargetGateway);
            UpdateRouteVisualState();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryUseDarkTitleBar();
            TrySetAppIcon();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
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
                e.Graphics.FillEllipse(blueGlow, 285, -75, 230, 230);
                e.Graphics.FillEllipse(warmGlow, -95, 455, 250, 250);
            }
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
                await RestoreWifiAsync();
            }
            else
            {
                await EnableGatewayAsync();
            }
        }

        private async System.Threading.Tasks.Task EnableGatewayAsync()
        {
            try
            {
                StartLoading("正在配置旁路由");
                await System.Threading.Tasks.Task.Run(() => GatewayManager.Enable(TargetGateway));
                enabledByApp = true;
                StopLoading();
                UpdateRouteVisualState();
                RunLatencyTest();
            }
            catch (Exception ex)
            {
                StopLoading();
                MessageBox.Show(this, "配置旁路由失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enabledByApp = GatewayManager.IsTargetActive(TargetGateway);
                UpdateRouteVisualState();
            }
        }

        private async System.Threading.Tasks.Task RestoreWifiAsync()
        {
            try
            {
                StartLoading("正在恢复原始网络");
                await System.Threading.Tasks.Task.Run(() => GatewayManager.Restore(TargetGateway));
                enabledByApp = false;
                StopLoading();
                UpdateRouteVisualState();
            }
            catch (Exception ex)
            {
                StopLoading();
                MessageBox.Show(this, "恢复 Wi-Fi 配置失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                enabledByApp = GatewayManager.IsTargetActive(TargetGateway);
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

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            bool active = GatewayManager.IsTargetActive(TargetGateway);
            if (active != enabledByApp)
            {
                enabledByApp = active;
                UpdateRouteVisualState();
            }
        }

        private void UpdateRouteVisualState()
        {
            WifiConfig current = GatewayManager.GetCurrentWifiConfig();
            string alias = current == null ? "未检测到 Wi-Fi" : current.InterfaceAlias;

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
            if (width > progressTrack.Width)
            {
                width = progressTrack.Width;
            }

            progressFill.Width = width;
            progressFill.BackColor = color;
            ApplyRoundedRegion(progressFill, 4);
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
            ApplyRoundedRegion(button, 12);
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen border = new Pen(cardBorderColor))
            using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, cardPanel.Width - 1, cardPanel.Height - 1), 22))
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

            Activate();
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
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, control.Width, control.Height);
                control.Region = new Region(path);
            }
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            using (GraphicsPath path = RoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius))
            {
                control.Region = new Region(path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
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
        public int InterfaceIndex;
        public string IPv4Address;
        public string Gateway;
        public List<string> DnsServers = new List<string>();
        public bool IsDnsDhcp;
    }

    internal static class GatewayManager
    {
        private const string RegistryPath = "Software\\YuanxinGatewaySwitch";

        public static WifiConfig GetCurrentWifiConfig()
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 || item.OperationalStatus != OperationalStatus.Up)
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
                config.InterfaceIndex = ipv4Props.Index;
                config.IPv4Address = address;

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
                    if (gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        config.Gateway = gateway.Address.ToString();
                        break;
                    }
                }

                foreach (IPAddress dns in props.DnsAddresses)
                {
                    if (dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        config.DnsServers.Add(dns.ToString());
                    }
                }

                return config;
            }

            return null;
        }

        public static bool IsTargetActive(string targetGateway)
        {
            WifiConfig current = GetCurrentWifiConfig();
            if (current == null)
            {
                return false;
            }

            bool gatewayMatch = string.Equals(current.Gateway, targetGateway, StringComparison.OrdinalIgnoreCase);
            bool dnsMatch = current.DnsServers.Count == 1 && string.Equals(current.DnsServers[0], targetGateway, StringComparison.OrdinalIgnoreCase);
            return gatewayMatch && dnsMatch;
        }

        public static void Enable(string targetGateway)
        {
            WifiConfig current = GetCurrentWifiConfig();
            if (current == null)
            {
                throw new InvalidOperationException("没有检测到已连接的 Wi-Fi。");
            }

            SaveOriginal(current);
            string command = "$idx=" + current.InterfaceIndex + ";"
                + "$gw='" + EscapePowerShell(targetGateway) + "';"
                + "foreach($store in @('ActiveStore','PersistentStore')){Get-NetRoute -PolicyStore $store -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue;}"
                + "New-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -NextHop $gw -RouteMetric 1 -PolicyStore ActiveStore -ErrorAction Stop;"
                + "New-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -NextHop $gw -RouteMetric 1 -PolicyStore PersistentStore -ErrorAction SilentlyContinue;"
                + "Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses $gw -ErrorAction Stop;";
            RunPowerShell(command);
        }

        public static void Restore(string targetGateway)
        {
            OriginalConfig original = LoadOriginal();
            WifiConfig current = GetCurrentWifiConfig();
            int index = original.InterfaceIndex > 0 ? original.InterfaceIndex : (current == null ? 0 : current.InterfaceIndex);
            if (index <= 0)
            {
                throw new InvalidOperationException("没有可恢复的 Wi-Fi 接口信息。");
            }

            string command = "$idx=" + index + ";"
                + "$target='" + EscapePowerShell(targetGateway) + "';"
                + "foreach($store in @('ActiveStore','PersistentStore')){Get-NetRoute -PolicyStore $store -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Where-Object { $_.NextHop -eq $target } | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue;}";

            if (!string.IsNullOrEmpty(original.Gateway))
            {
                command += "$oldgw='" + EscapePowerShell(original.Gateway) + "';"
                    + "New-NetRoute -InterfaceIndex $idx -DestinationPrefix '0.0.0.0/0' -NextHop $oldgw -RouteMetric 10 -PolicyStore ActiveStore -ErrorAction SilentlyContinue;";
            }

            if (original.IsDnsDhcp || original.DnsServers.Count == 0)
            {
                command += "Set-DnsClientServerAddress -InterfaceIndex $idx -ResetServerAddresses -ErrorAction Stop;";
            }
            else
            {
                command += "$dns=@(" + QuotePowerShellArray(original.DnsServers) + ");"
                    + "Set-DnsClientServerAddress -InterfaceIndex $idx -ServerAddresses $dns -ErrorAction Stop;";
            }

            command += "Clear-DnsClientCache -ErrorAction SilentlyContinue;"
                + "Register-DnsClient -ErrorAction SilentlyContinue;";

            RunPowerShell(command);
            ClearOriginal();
        }

        private static void SaveOriginal(WifiConfig config)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("InterfaceAlias", config.InterfaceAlias ?? string.Empty, RegistryValueKind.String);
                key.SetValue("InterfaceIndex", config.InterfaceIndex, RegistryValueKind.DWord);
                key.SetValue("Gateway", config.Gateway ?? string.Empty, RegistryValueKind.String);
                key.SetValue("DnsServers", string.Join(";", config.DnsServers.ToArray()), RegistryValueKind.String);
                key.SetValue("IsDnsDhcp", config.IsDnsDhcp ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private static OriginalConfig LoadOriginal()
        {
            OriginalConfig config = new OriginalConfig();
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key == null)
                {
                    return config;
                }

                config.InterfaceIndex = Convert.ToInt32(key.GetValue("InterfaceIndex", 0));
                config.Gateway = Convert.ToString(key.GetValue("Gateway", string.Empty));
                string dns = Convert.ToString(key.GetValue("DnsServers", string.Empty));
                if (!string.IsNullOrEmpty(dns))
                {
                    config.DnsServers.AddRange(dns.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                config.IsDnsDhcp = Convert.ToInt32(key.GetValue("IsDnsDhcp", 0)) == 1;
            }
            return config;
        }

        private static void ClearOriginal()
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
        }

        private static void RunPowerShell(string command)
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
            public int InterfaceIndex;
            public string Gateway;
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
