// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "GatewaySwitch",
    platforms: [
        .macOS(.v11)
    ],
    products: [
        .executable(
            name: "GatewaySwitch",
            targets: ["GatewaySwitch"]
        )
    ],
    targets: [
        .executableTarget(
            name: "GatewaySwitch",
            path: ".",
            exclude: ["Scripts"],
            resources: [
                .copy("Scripts/gateway-manager.sh")
            ]
        )
    ]
)
