# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**元信旁路由 (GatewaySwitch)** is a Windows Forms application that allows users to switch their Wi-Fi gateway and DNS to a bypass router (旁路由) with one click, and test ChatGPT connectivity.

Key functionality:
- One-click gateway/DNS switching to a configured bypass router IP
- Automatic restoration of original network configuration
- ChatGPT latency testing (TCP connection to chatgpt.com:443)
- System tray integration with background monitoring
- SSID-based auto-enable support

## Build Commands

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

## Architecture

### Single-File Structure

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

### Network Operations Architecture

**Enable flow**:
1. Detect current active network adapter (prefers Wi-Fi over Ethernet)
2. Save original gateway/DNS to registry (`Original` subkey)
3. Execute PowerShell to:
   - Remove existing default routes (0.0.0.0/0 and ::/0)
   - Add new default routes pointing to bypass router
   - Set static DNS to bypass router IP
4. Verify routes were applied correctly

**Restore flow**:
1. Load original config from registry
2. Remove routes pointing to bypass router
3. Restore original gateway routes
4. Restore DNS (either DHCP or static)
5. Clear registry backup

**Auto-switching** (RefreshTimer_Tick, every 3 seconds):
- If SSID configured and auto-enable checked:
  - When connected to configured SSID → auto-enable gateway
  - When disconnected from configured SSID → auto-restore
- Detects manual changes to gateway state and syncs UI

### DPI Scaling Implementation

Custom proportional scaling system (not standard WinForms AutoScale):

```
dpiScale = max(1.0, dpiRatio × screenRatio)

where:
  dpiRatio = currentDPI / 96.0
  screenRatio = min(logicalWidth/1920, logicalHeight/1080)
```

Design baseline: 1920×1080 @ 96 DPI. On higher resolution displays (e.g., 4K), the window grows proportionally to occupy the same screen percentage.

All controls use `ScalePoint()`, `ScaleSize()`, `UiFont()` helpers that multiply base values by `dpiScale`.

### PowerShell Command Generation

Security notes:
- All user input is escaped via `EscapePowerShell()` (replaces `'` with `''`)
- Commands are passed to `powershell.exe -ExecutionPolicy Bypass -Command`
- IPv6 link-local addresses (fe80::) automatically get `%InterfaceIndex` scope added when needed
- Dual-store approach: applies changes to both ActiveStore and PersistentStore for NetRoute operations

### Manifest Requirements

**GatewaySwitch.manifest** declares:
- `requireAdministrator` (network changes require admin)
- PerMonitorV2 DPI awareness
- Windows 7+ compatibility GUIDs

## Key Dependencies

- .NET Framework 4.8 (target framework)
- Windows Forms, System.Drawing
- PowerShell cmdlets: Get-NetRoute, New-NetRoute, Set-DnsClientServerAddress
- Windows Registry API

## Testing Considerations

No automated tests exist. Manual testing workflow:
1. Run as admin
2. Enable gateway → verify routes with `Get-NetRoute -InterfaceIndex X -DestinationPrefix 0.0.0.0/0`
3. Test latency → should connect to chatgpt.com:443
4. Disable → verify original gateway restored
5. Test SSID auto-switching (if configured)

## Localization

All UI text is in Chinese (Simplified). No localization framework exists.

## Common Pitfalls

1. **PowerShell execution failures**: If cmdlets fail, error messages are in Chinese and come from PowerShell stderr
2. **IPv6 scope handling**: Link-local IPv6 addresses (fe80::) require zone ID (`%InterfaceIndex`). Code auto-adds this when creating routes.
3. **Virtual adapter filtering**: GetCurrentNetworkConfig() filters out Hyper-V, VMware, VirtualBox, VPN adapters by checking interface description
4. **DPI changes**: OnDpiChanged must call ReapplyControlRegions() to recreate GraphicsPath regions
5. **Resource disposal**: GraphicsPath objects must be disposed. Current code uses `using` statements but some paths create regions that need re-creation on resize.

## Release Process

GitHub Actions auto-builds on push. For releases:
```bash
git tag v1.x.x
git push origin v1.x.x
```

This triggers GitHub Release creation with compiled executable.
