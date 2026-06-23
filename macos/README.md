# GatewaySwitch for macOS

元信旁路由的 macOS 版本 - 轻量级菜单栏应用

## 功能

- 菜单栏常驻，一键切换网关和 DNS
- ChatGPT 延迟测试
- SSID 自动切换支持
- 管理员权限处理（自动提示输入密码）

## 构建

### 前置要求
- macOS 11.0+
- Xcode 或 Swift 命令行工具

### 本地构建

```bash
cd macos/GatewaySwitch
chmod +x Scripts/gateway-manager.sh
swift build -c release
```

### GitHub Actions 自动构建

推送代码到 main 分支后，GitHub Actions 会自动构建并生成 DMG 文件。

在 Actions 页面下载 `GatewaySwitch-macOS` artifact。

### 发布版本

```bash
git tag v1.0.0-macos
git push origin v1.0.0-macos
```

会自动创建 GitHub Release 并附带 DMG 文件。

## 安装

1. 下载 `GatewaySwitch.dmg`
2. 打开 DMG，拖动 `GatewaySwitch.app` 到应用程序文件夹
3. 首次运行可能需要在「系统设置 > 隐私与安全性」中允许运行

## 使用

1. 运行应用，菜单栏会出现网络图标
2. 点击图标打开菜单
3. 点击「启用旁路由」（需要输入管理员密码）
4. 可以点击「测试延迟」检查 ChatGPT 连通性
5. 点击「设置...」配置旁路由 IP 和 SSID

## 架构

- **AppDelegate.swift**: 主应用逻辑（菜单栏、UI、状态管理）
- **Scripts/gateway-manager.sh**: 网络操作脚本（使用 networksetup 命令）
- **配置存储**: `~/Library/Application Support/GatewaySwitch/config.json`

## 文件大小

编译后的 .app 体积约 **1-2 MB**，保持轻量级。
