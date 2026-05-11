# 元信旁路由 (GatewaySwitch)

一键切换 Wi-Fi 默认网关到旁路由（192.168.3.187），并测试 ChatGPT 连通性。

## 功能

- 一键将 Wi-Fi 网关和 DNS 切换到旁路由
- 一键恢复原始网络配置
- ChatGPT 延迟测试（TCP 连接 chatgpt.com:443）
- 切换网关后自动测试连通性
- 自动检测网关状态变化
- 系统托盘最小化，后台常驻

## 环境要求

- Windows 10 / 11
- .NET Framework 4.8（Windows 10+ 自带）
- 管理员权限（修改网关需要）

## 本地构建

### 方式一：dotnet CLI（推荐）

需要安装 [.NET SDK 8.0+](https://dotnet.microsoft.com/download/dotnet/8.0)：

```bash
winget install Microsoft.DotNet.SDK.8
```

构建：

```bash
dotnet build GatewaySwitch.csproj -c Release
```

产物位于 `bin\Release\net48\GatewaySwitch.exe`。

### 方式二：csc.exe（无需额外安装）

Windows 自带的 .NET Framework 编译器：

```cmd
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:dist\GatewaySwitch.exe /win32icon:assets\app.ico /win32manifest:GatewaySwitch.manifest /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll GatewaySwitch.cs
```

产物位于 `dist\GatewaySwitch.exe`。

## CI/CD

项目使用 GitHub Actions 自动构建。

- 推送到 main 分支：自动编译，产物可在 Actions Artifacts 下载
- 打 tag（如 `v1.1.0`）：自动编译并创建 GitHub Release

发版流程：

```bash
git tag v1.1.0
git push origin v1.1.0
```

## 使用说明

1. 以管理员身份运行 `GatewaySwitch.exe`
2. 点击电源按钮切换到旁路由
3. 切换成功后自动测试 ChatGPT 连通性
4. 也可手动点击"测试延迟"按钮检测
5. 再次点击电源按钮恢复原始网关
6. 点击关闭按钮会最小化到托盘，右键托盘图标可退出
