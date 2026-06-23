# macOS Version

This directory contains the macOS version of GatewaySwitch (元信旁路由).

## Key Differences from Windows Version

### Architecture
- **Windows**: Single 2050-line WinForms app with embedded PowerShell commands
- **macOS**: Swift AppKit menubar app (~400 lines) + Bash script (~250 lines)

### Network Operations
- **Windows**: PowerShell cmdlets (Get-NetRoute, New-NetRoute, Set-DnsClientServerAddress)
- **macOS**: `networksetup` command-line tool + `route` commands

### UI
- **Windows**: Full window with power button, cards, custom DPI scaling
- **macOS**: Menubar-only (no main window), native macOS notifications

### Permissions
- **Windows**: Manifest declares `requireAdministrator`, entire app runs elevated
- **macOS**: Runs as normal user, only prompts for password when needed (via AppleScript)

### Size
- **Windows**: ~150 KB executable (uses system .NET Framework)
- **macOS**: ~1-2 MB app bundle (includes Swift runtime)

## File Structure

```
macos/GatewaySwitch/
├── AppDelegate.swift           # Main app logic (~400 lines)
├── Package.swift               # Swift Package Manager config
├── Scripts/
│   └── gateway-manager.sh      # Network operations (~250 lines)
└── README.md
```

## Build Process

GitHub Actions workflow (`.github/workflows/build-macos.yml`) handles:
1. Building universal binary (x86_64 + arm64)
2. Creating .app bundle
3. Packaging as DMG
4. Uploading as artifact / release

## Testing Notes

Manual testing required:
1. Enable gateway → check with `networksetup -getinfo Wi-Fi`
2. Test latency
3. Restore → verify original gateway restored
4. Test SSID auto-switching

No unit tests exist (same as Windows version).
