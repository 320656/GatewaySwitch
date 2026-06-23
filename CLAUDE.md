# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**元信旁路由 (GatewaySwitch)** is a Windows Forms application that allows users to switch their Wi-Fi gateway and DNS to a bypass router (旁路由) with one click, and test ChatGPT connectivity.

**macOS version** is a lightweight menubar app with the same functionality.

Key functionality:
- One-click gateway/DNS switching to a configured bypass router IP
- Automatic restoration of original network configuration
- ChatGPT latency testing (TCP connection to chatgpt.com:443)
- System tray/menubar integration with background monitoring
- SSID-based auto-enable support

## Build Commands

### Windows Version

**Primary build method** (always use this after code changes):
```bash
powershell -ExecutionPolicy Bypass -File .\scripts\local-build.ps1
```

This creates timestamped Release builds under `bin\Release\GatewaySwitch-yyyy-MM-dd-HHmmss\`.

Alternative build methods:
```bash
# Using dotnet CLI
dotnet build GatewaySwitch.csproj -c Release

# Output: bin\Release\net48\GatewaySwitch.exe
```

### macOS Version

**Build on macOS** (requires Xcode or Swift CLI):
```bash
cd macos/GatewaySwitch
chmod +x Scripts/gateway-manager.sh
swift build -c release
```

**Or use GitHub Actions** - pushes to `main` branch with changes in `macos/**` will trigger automatic builds. Download the DMG from Actions artifacts.

## Architecture

### Windows Version (Single-File Structure)

The entire application is in **GatewaySwitch.cs** (~2050 lines). This is intentional to keep deployment simple (single .exe output).

**Main components** (in order of appearance):

1. **Program** (lines 17-38)
   - Entry point with single-instance mutex (`YuanxinGateway.SingleInstance`)
   - Prevents multiple app instances from running

2. **MainForm** (lines 40-958)
   - Main UI with custom DPI-aware scaling
   - Power button for gateway toggle
   - Network diagnostics card with latency testing
   - Implements custom DPI scaling logic: combines system DPI ratio with screen resolution proportion (baseline: 1920×1080)
   - Uses GraphicsPath regions for rounded corners on controls
   - Settings button (gear icon) opens SettingsForm

3. **SettingsForm** (lines 974-1217)
   - Configuration dialog for:
     - IPv4 gateway IP (default: 192.168.3.187)
     - IPv6 gateway IP (optional, supports fe80:: link-local)
     - SSID binding (empty = no restriction)
     - Auto-enable checkbox

4. **GatewayManager** (lines 1219-1978)
   - Core network manipulation logic
   - Registry storage: `HKCU\Software\YuanxinGatewaySwitch`
   - **Critical**: Uses PowerShell cmdlets (Get-NetRoute, New-NetRoute, Set-DnsClientServerAddress) via Process.Start
   - Saves original configuration before switching, restores on disable
   - Supports both IPv4 and IPv6 gateway manipulation

5. **ChatGptLatencyTester** (lines 1988-2049)
   - Simple TCP connection test to chatgpt.com:443
   - 6-second timeout
   - Returns elapsed milliseconds

### macOS Version (Modular Structure)

Located in `macos/GatewaySwitch/`:

1. **AppDelegate.swift** (~400 lines)
   - NSStatusItem menubar integration
   - Settings window with native macOS controls
   - Calls shell script for network operations
   - Uses AppleScript for sudo prompts

2. **Scripts/gateway-manager.sh** (~250 lines)
   - Network operations using `networksetup` CLI
   - JSON config storage in `~/Library/Application Support/GatewaySwitch/`
   - Functions: enable, restore, test-latency, get-ssid, is-active

3. **Package.swift**
   - Swift Package Manager configuration
   - Bundles script as resource

**Key difference**: macOS version separates UI (Swift) from network logic (Bash), making it more modular than Windows version.

## Network Operations Architecture

### Windows Enable Flow
1. Detect current active network adapter (prefers Wi-Fi over Ethernet)
2. Save original gateway/DNS to registry (`Original` subkey)
3. Execute PowerShell to:
   - Remove existing default routes (0.0.0.0/0 and ::/0)
   - Add new default routes pointing to bypass router
   - Set static DNS to bypass router IP
4. Verify routes were applied correctly

### macOS Enable Flow
1. Get active Wi-Fi interface via `networksetup -listallhardwareports`
2. Save backup to JSON file
3. Use `networksetup -setmanual` to set gateway
4. Use `networksetup -setdnsservers` to set DNS
5. Flush DNS cache

**Auto-switching** (RefreshTimer_Tick / timer, every 3 seconds):
- If SSID configured and auto-enable checked:
  - When connected to configured SSID → auto-enable gateway
  - When disconnected from configured SSID → auto-restore
- Detects manual changes to gateway state and syncs UI

## DPI Scaling Implementation (Windows Only)

Custom proportional scaling system (not standard WinForms AutoScale):

```
dpiScale = max(1.0, dpiRatio × screenRatio)

where:
  dpiRatio = currentDPI / 96.0
  screenRatio = min(logicalWidth/1920, logicalHeight/1080)
```

Design baseline: 1920×1080 @ 96 DPI. On higher resolution displays (e.g., 4K), the window grows proportionally to occupy the same screen percentage.

All controls use `ScalePoint()`, `ScaleSize()`, `UiFont()` helpers that multiply base values by `dpiScale`.

## PowerShell/Shell Command Generation

### Windows Security
- All user input is escaped via `EscapePowerShell()` (replaces `'` with `''`)
- Commands are passed to `powershell.exe -ExecutionPolicy Bypass -Command`
- IPv6 link-local addresses (fe80::) automatically get `%InterfaceIndex` scope added when needed
- Dual-store approach: applies changes to both ActiveStore and PersistentStore for NetRoute operations

### macOS Security
- Shell script uses proper quoting for all variables
- Sudo operations use AppleScript's `with administrator privileges`
- Script path validation before execution

## Key Dependencies

### Windows
- .NET Framework 4.8 (target framework)
- Windows Forms, System.Drawing
- PowerShell cmdlets: Get-NetRoute, New-NetRoute, Set-DnsClientServerAddress
- Windows Registry API

### macOS
- macOS 11.0+ (Big Sur)
- Swift 5.9+
- AppKit framework
- `networksetup` CLI tool (system-provided)
- Python 3 (for JSON parsing in shell script, system-provided)

## Testing Considerations

No automated tests exist for either version. Manual testing workflow:

### Windows
1. Run as admin
2. Enable gateway → verify routes with `Get-NetRoute -InterfaceIndex X -DestinationPrefix 0.0.0.0/0`
3. Test latency → should connect to chatgpt.com:443
4. Disable → verify original gateway restored
5. Test SSID auto-switching (if configured)

### macOS
1. Run app (no admin needed initially)
2. Enable gateway → check with `networksetup -getinfo Wi-Fi`
3. Test latency
4. Restore → verify original gateway restored
5. Test SSID auto-switching

## Common Pitfalls

### Windows
1. **PowerShell execution failures**: If cmdlets fail, error messages are in Chinese and come from PowerShell stderr
2. **IPv6 scope handling**: Link-local IPv6 addresses (fe80::) require zone ID (`%InterfaceIndex`). Code auto-adds this when creating routes.
3. **Virtual adapter filtering**: GetCurrentNetworkConfig() filters out Hyper-V, VMware, VirtualBox, VPN adapters by checking interface description
4. **DPI changes**: OnDpiChanged must call ReapplyControlRegions() to recreate GraphicsPath regions
5. **Resource disposal**: GraphicsPath objects must be disposed. Current code uses `using` statements but some paths create regions that need re-creation on resize.

### macOS
1. **Script bundling**: The shell script must be marked executable and bundled as a resource in Package.swift
2. **Sudo prompts**: AppleScript's `with administrator privileges` only works when app is launched normally (not via debugger/CLI in some cases)
3. **Interface detection**: `networksetup -listallhardwareports` output format can vary; script uses awk to parse
4. **SSID detection**: Requires access to private Apple80211 framework via CLI tool

## Release Process

### Windows
GitHub Actions auto-builds on push. For releases:
```bash
git tag v1.x.x
git push origin v1.x.x
```

This triggers GitHub Release creation with compiled executable.

### macOS
Same process, but use tag like `v1.x.x-macos` to distinguish:
```bash
git tag v1.0.0-macos
git push origin v1.0.0-macos
```

Workflow creates DMG and attaches to release.

## Localization

All UI text is in Chinese (Simplified) for both versions. No localization framework exists.

## File Size Comparison

- **Windows**: ~150 KB (uses system .NET Framework)
- **macOS**: ~1-2 MB app bundle (includes Swift runtime, still lightweight)

Both versions prioritize minimal file size and no external dependencies beyond system frameworks.
