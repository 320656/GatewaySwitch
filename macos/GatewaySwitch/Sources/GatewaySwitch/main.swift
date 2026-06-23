import Cocoa
import Foundation
import UserNotifications

class AppDelegate: NSObject, NSApplicationDelegate {
    var statusItem: NSStatusItem!
    var menu: NSMenu!
    var isEnabled = false
    var timer: Timer?

    let scriptPath: String = {
        if let resourcePath = Bundle.main.resourcePath {
            let scriptInBundle = "\(resourcePath)/Scripts/gateway-manager.sh"
            if FileManager.default.fileExists(atPath: scriptInBundle) {
                return scriptInBundle
            }
        }
        // Fallback for development
        let currentDir = FileManager.default.currentDirectoryPath
        return "\(currentDir)/Scripts/gateway-manager.sh"
    }()

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Create status bar item
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        if let button = statusItem.button {
            button.image = NSImage(systemSymbolName: "network", accessibilityDescription: "Gateway Switch")
            button.image?.isTemplate = true
        }

        // Create menu
        menu = NSMenu()

        // Initialize script
        _ = runScript(args: ["init"])

        // Check initial state
        updateState()

        // Build menu
        rebuildMenu()

        // Start monitoring timer (every 3 seconds)
        timer = Timer.scheduledTimer(withTimeInterval: 3.0, repeats: true) { [weak self] _ in
            self?.checkAutoSwitch()
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate()
    }

    func rebuildMenu() {
        menu.removeAllItems()

        // Title
        let titleItem = NSMenuItem(title: "元信旁路由", action: nil, keyEquivalent: "")
        titleItem.isEnabled = false
        menu.addItem(titleItem)

        menu.addItem(NSMenuItem.separator())

        // Toggle gateway
        let toggleTitle = isEnabled ? "禁用旁路由" : "启用旁路由"
        let toggleItem = NSMenuItem(title: toggleTitle, action: #selector(toggleGateway), keyEquivalent: "t")
        toggleItem.target = self
        menu.addItem(toggleItem)

        // Test latency
        let testItem = NSMenuItem(title: "测试延迟", action: #selector(testLatency), keyEquivalent: "l")
        testItem.target = self
        menu.addItem(testItem)

        menu.addItem(NSMenuItem.separator())

        // Settings
        let settingsItem = NSMenuItem(title: "设置...", action: #selector(openSettings), keyEquivalent: ",")
        settingsItem.target = self
        menu.addItem(settingsItem)

        menu.addItem(NSMenuItem.separator())

        // Quit
        let quitItem = NSMenuItem(title: "退出", action: #selector(quitApp), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    @objc func toggleGateway() {
        let alert = NSAlert()
        alert.messageText = "需要管理员权限"
        alert.informativeText = "修改网络配置需要输入密码"
        alert.alertStyle = .informational
        alert.addButton(withTitle: "确定")
        alert.addButton(withTitle: "取消")

        if alert.runModal() == .alertFirstButtonReturn {
            if isEnabled {
                restoreGateway()
            } else {
                enableGateway()
            }
        }
    }

    func enableGateway() {
        let result = runScriptWithSudo(args: ["enable"])

        if result.success {
            showNotification(title: "旁路由已启用", body: "网关和 DNS 已切换")
            updateState()
            rebuildMenu()

            // Auto test latency
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { [weak self] in
                self?.testLatency()
            }
        } else {
            showAlert(title: "启用失败", message: result.output)
        }
    }

    func restoreGateway() {
        let result = runScriptWithSudo(args: ["restore"])

        if result.success {
            showNotification(title: "已恢复原始配置", body: "网关和 DNS 已还原")
            updateState()
            rebuildMenu()
        } else {
            showAlert(title: "恢复失败", message: result.output)
        }
    }

    @objc func testLatency() {
        showNotification(title: "测试中...", body: "正在连接 chatgpt.com")

        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self = self else { return }
            let result = self.runScript(args: ["test-latency"])

            DispatchQueue.main.async {
                if let ms = Int(result.output.trimmingCharacters(in: .whitespacesAndNewlines)), ms >= 0 {
                    let seconds = Double(ms) / 1000.0
                    let status: String
                    if seconds < 1.0 {
                        status = "连接稳定"
                    } else if seconds <= 3.0 {
                        status = "连接较慢"
                    } else {
                        status = "超时"
                    }
                    self.showNotification(title: "延迟测试结果", body: String(format: "%@: %.2fs", status, seconds))
                } else {
                    self.showNotification(title: "测试失败", body: "无法连接到 ChatGPT")
                }
            }
        }
    }

    @objc func openSettings() {
        let settingsWindow = SettingsWindowController()
        settingsWindow.showWindow(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    @objc func quitApp() {
        NSApplication.shared.terminate(self)
    }

    func updateState() {
        let result = runScript(args: ["is-active"])
        isEnabled = result.output.trimmingCharacters(in: .whitespacesAndNewlines) == "true"

        // Update icon
        if let button = statusItem.button {
            let iconName = isEnabled ? "network.badge.shield.half.filled" : "network"
            button.image = NSImage(systemSymbolName: iconName, accessibilityDescription: "Gateway Switch")
            button.image?.isTemplate = true
        }
    }

    func checkAutoSwitch() {
        updateState()

        let autoEnable = runScript(args: ["get-config", "auto_enable"]).output.trimmingCharacters(in: .whitespacesAndNewlines)
        let configSsid = runScript(args: ["get-config", "ssid"]).output.trimmingCharacters(in: .whitespacesAndNewlines)
        let currentSsid = runScript(args: ["get-ssid"]).output.trimmingCharacters(in: .whitespacesAndNewlines)

        guard !configSsid.isEmpty && autoEnable == "true" else { return }

        if currentSsid == configSsid && !isEnabled {
            enableGateway()
        } else if currentSsid != configSsid && isEnabled {
            restoreGateway()
        }
    }

    func runScript(args: [String]) -> (success: Bool, output: String) {
        guard !scriptPath.isEmpty, FileManager.default.fileExists(atPath: scriptPath) else {
            return (false, "Script not found")
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/bash")
        process.arguments = [scriptPath] + args

        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe

        do {
            try process.run()
            process.waitUntilExit()

            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            let output = String(data: data, encoding: .utf8) ?? ""

            return (process.terminationStatus == 0, output)
        } catch {
            return (false, error.localizedDescription)
        }
    }

    func runScriptWithSudo(args: [String]) -> (success: Bool, output: String) {
        guard !scriptPath.isEmpty else {
            return (false, "Script not found")
        }

        let script = """
        do shell script "/bin/bash '\(scriptPath)' \(args.joined(separator: " "))" with administrator privileges
        """

        var error: NSDictionary?
        if let scriptObject = NSAppleScript(source: script) {
            let output = scriptObject.executeAndReturnError(&error)
            if error != nil {
                return (false, error?["NSAppleScriptErrorMessage"] as? String ?? "Unknown error")
            }
            return (true, output.stringValue ?? "")
        }

        return (false, "Failed to create AppleScript")
    }

    func showNotification(title: String, body: String) {
        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        content.sound = UNNotificationSound.default

        let request = UNNotificationRequest(identifier: UUID().uuidString, content: content, trigger: nil)
        UNUserNotificationCenter.current().add(request)
    }

    func showAlert(title: String, message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.alertStyle = .warning
        alert.addButton(withTitle: "确定")
        alert.runModal()
    }
}

// Settings Window Controller
class SettingsWindowController: NSWindowController {
    convenience init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 400, height: 280),
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: false
        )
        window.title = "设置"
        window.center()

        self.init(window: window)

        let contentView = SettingsView()
        window.contentView = contentView
    }
}

// Settings View
class SettingsView: NSView {
    let ipv4Field = NSTextField(frame: NSRect(x: 20, y: 200, width: 360, height: 24))
    let ipv6Field = NSTextField(frame: NSRect(x: 20, y: 140, width: 360, height: 24))
    let ssidField = NSTextField(frame: NSRect(x: 20, y: 80, width: 260, height: 24))
    let autoEnableCheckbox = NSButton(checkboxWithTitle: "连上该 Wi-Fi 时自动启用旁路由", target: nil, action: nil)
    let getCurrentSsidButton = NSButton(title: "获取当前", target: nil, action: #selector(getCurrentSsid))
    let saveButton = NSButton(title: "保存", target: nil, action: #selector(saveSettings))

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        setupUI()
        loadSettings()
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        setupUI()
        loadSettings()
    }

    func setupUI() {
        // IPv4 label
        let ipv4Label = NSTextField(labelWithString: "旁路由 IPv4 地址:")
        ipv4Label.frame = NSRect(x: 20, y: 225, width: 200, height: 20)
        addSubview(ipv4Label)

        // IPv4 field
        ipv4Field.placeholderString = "192.168.3.187"
        addSubview(ipv4Field)

        // IPv6 label
        let ipv6Label = NSTextField(labelWithString: "旁路由 IPv6 地址 (可选):")
        ipv6Label.frame = NSRect(x: 20, y: 165, width: 200, height: 20)
        addSubview(ipv6Label)

        // IPv6 field
        ipv6Field.placeholderString = "fe80::20c:29ff:fe2b:cd7d"
        addSubview(ipv6Field)

        // SSID label
        let ssidLabel = NSTextField(labelWithString: "绑定 Wi-Fi SSID (留空表示不限制):")
        ssidLabel.frame = NSRect(x: 20, y: 105, width: 250, height: 20)
        addSubview(ssidLabel)

        // SSID field
        addSubview(ssidField)

        // Get current SSID button
        getCurrentSsidButton.frame = NSRect(x: 290, y: 78, width: 90, height: 28)
        getCurrentSsidButton.target = self
        getCurrentSsidButton.bezelStyle = .rounded
        addSubview(getCurrentSsidButton)

        // Auto enable checkbox
        autoEnableCheckbox.frame = NSRect(x: 18, y: 45, width: 300, height: 24)
        autoEnableCheckbox.state = .on
        addSubview(autoEnableCheckbox)

        // Save button
        saveButton.frame = NSRect(x: 290, y: 10, width: 90, height: 32)
        saveButton.target = self
        saveButton.bezelStyle = .rounded
        saveButton.keyEquivalent = "\r"
        addSubview(saveButton)
    }

    func loadSettings() {
        let appDelegate = NSApplication.shared.delegate as! AppDelegate

        let ipv4 = appDelegate.runScript(args: ["get-config", "gateway_ipv4"]).output.trimmingCharacters(in: .whitespacesAndNewlines)
        let ipv6 = appDelegate.runScript(args: ["get-config", "gateway_ipv6"]).output.trimmingCharacters(in: .whitespacesAndNewlines)
        let ssid = appDelegate.runScript(args: ["get-config", "ssid"]).output.trimmingCharacters(in: .whitespacesAndNewlines)
        let autoEnable = appDelegate.runScript(args: ["get-config", "auto_enable"]).output.trimmingCharacters(in: .whitespacesAndNewlines)

        ipv4Field.stringValue = ipv4
        ipv6Field.stringValue = ipv6
        ssidField.stringValue = ssid
        autoEnableCheckbox.state = (autoEnable == "true") ? .on : .off
    }

    @objc func getCurrentSsid() {
        let appDelegate = NSApplication.shared.delegate as! AppDelegate
        let ssid = appDelegate.runScript(args: ["get-ssid"]).output.trimmingCharacters(in: .whitespacesAndNewlines)

        if ssid.isEmpty {
            let alert = NSAlert()
            alert.messageText = "未能检测到 Wi-Fi"
            alert.informativeText = "请确认已开启 Wi-Fi 并连入网络"
            alert.alertStyle = .informational
            alert.addButton(withTitle: "确定")
            alert.runModal()
        } else {
            ssidField.stringValue = ssid
        }
    }

    @objc func saveSettings() {
        let ipv4 = ipv4Field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        let ipv6 = ipv6Field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        let ssid = ssidField.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        let autoEnable = autoEnableCheckbox.state == .on ? "true" : "false"

        // Validate IPv4
        if ipv4.isEmpty || !isValidIPv4(ipv4) {
            showAlert(title: "错误", message: "请输入有效的 IPv4 地址")
            return
        }

        let appDelegate = NSApplication.shared.delegate as! AppDelegate
        _ = appDelegate.runScript(args: ["set-config", "gateway_ipv4", ipv4])
        _ = appDelegate.runScript(args: ["set-config", "gateway_ipv6", ipv6])
        _ = appDelegate.runScript(args: ["set-config", "ssid", ssid])
        _ = appDelegate.runScript(args: ["set-config", "auto_enable", autoEnable])

        window?.close()

        appDelegate.showNotification(title: "设置已保存", body: "配置已更新")
    }

    func isValidIPv4(_ ip: String) -> Bool {
        let parts = ip.split(separator: ".")
        guard parts.count == 4 else { return false }

        for part in parts {
            guard let num = Int(part), num >= 0 && num <= 255 else {
                return false
            }
        }
        return true
    }

    func showAlert(title: String, message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.alertStyle = .warning
        alert.addButton(withTitle: "确定")
        alert.runModal()
    }
}

// Application entry point
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
