#!/bin/bash
# Gateway Manager for macOS
# Handles network gateway and DNS switching

set -e

CONFIG_DIR="$HOME/Library/Application Support/GatewaySwitch"
CONFIG_FILE="$CONFIG_DIR/config.json"
BACKUP_FILE="$CONFIG_DIR/backup.json"

# Ensure config directory exists
mkdir -p "$CONFIG_DIR"

# Initialize default config if not exists
init_config() {
    if [ ! -f "$CONFIG_FILE" ]; then
        cat > "$CONFIG_FILE" <<EOF
{
  "gateway_ipv4": "192.168.3.187",
  "gateway_ipv6": "",
  "ssid": "",
  "auto_enable": true
}
EOF
    fi
}

# Get config value
get_config() {
    local key="$1"
    init_config
    CONFIG_FILE="$CONFIG_FILE" CONFIG_KEY="$key" python3 <<'PY'
import json
import os

with open(os.environ["CONFIG_FILE"], "r", encoding="utf-8") as f:
    config = json.load(f)

value = config.get(os.environ["CONFIG_KEY"], "")
if isinstance(value, bool):
    print(str(value).lower())
else:
    print(value)
PY
}

# Set config value
set_config() {
    local key="$1"
    local value="${2-}"
    init_config
    CONFIG_FILE="$CONFIG_FILE" CONFIG_KEY="$key" CONFIG_VALUE="$value" python3 <<'PY'
import json
import os

config_file = os.environ["CONFIG_FILE"]
key = os.environ["CONFIG_KEY"]
value = os.environ["CONFIG_VALUE"]

if key == "auto_enable":
    value = value.lower() == "true"

with open(config_file, "r", encoding="utf-8") as f:
    config = json.load(f)

config[key] = value

with open(config_file, "w", encoding="utf-8") as f:
    json.dump(config, f, indent=2)
PY
}

# Get active Wi-Fi service name
get_wifi_service() {
    # Simply return "Wi-Fi" if it exists in network services
    networksetup -listnetworkserviceorder | grep -o "Wi-Fi" | head -1
}

# Get active Wi-Fi interface device
get_wifi_interface() {
    # Get the device name (e.g., "en0")
    networksetup -listallhardwareports | awk '/Wi-Fi|AirPort/{getline; print $2}'
}

# Get current SSID
get_current_ssid() {
    local interface=$(get_wifi_interface)
    if [ -z "$interface" ]; then
        echo ""
        return
    fi

    # Try multiple methods to get SSID
    # Method 1: airport command (older macOS)
    local ssid=$(/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport -I 2>/dev/null | awk '/ SSID/ {print $2}')

    # Method 2: networksetup command
    if [ -z "$ssid" ]; then
        ssid=$(networksetup -getairportnetwork "$interface" 2>/dev/null | sed 's/Current Wi-Fi Network: //')
    fi

    # Method 3: system_profiler (slowest but most reliable)
    if [ -z "$ssid" ] || [ "$ssid" = "You are not associated with an AirPort network." ]; then
        ssid=$(system_profiler SPAirPortDataType 2>/dev/null | awk -F': ' '/Current Network/ {getline; print $2}' | xargs)
    fi

    # Clean up error messages
    if [[ "$ssid" == *"not associated"* ]] || [[ "$ssid" == *"off"* ]]; then
        echo ""
    else
        echo "$ssid"
    fi
}

# Get current gateway
get_current_gateway() {
    local service="$1"
    networksetup -getinfo "$service" | awk '/^Router:/ {print $2}'
}

# Get current DNS servers
get_current_dns() {
    local service="$1"
    networksetup -getdnsservers "$service" | grep -v "There aren't any DNS Servers" | tr '\n' ',' | sed 's/,$//'
}

# Check if using DHCP for DNS
is_dns_dhcp() {
    local service="$1"
    local dns=$(networksetup -getdnsservers "$service")
    [[ "$dns" == *"aren't any DNS Servers"* ]] && echo "true" || echo "false"
}

# Check if gateway is active
is_gateway_active() {
    local target_gateway="$1"
    local service=$(get_wifi_service)

    if [ -z "$service" ]; then
        echo "false"
        return
    fi

    local current_gateway=$(get_current_gateway "$service")
    local current_dns=$(get_current_dns "$service")

    if [ "$current_gateway" = "$target_gateway" ] && [[ "$current_dns" == *"$target_gateway"* ]]; then
        echo "true"
    else
        echo "false"
    fi
}

# Enable gateway
enable_gateway() {
    local target_gateway=$(get_config "gateway_ipv4")
    local target_gateway_ipv6=$(get_config "gateway_ipv6")
    local service=$(get_wifi_service)
    local device=$(get_wifi_interface)

    if [ -z "$service" ]; then
        echo "ERROR: No Wi-Fi service found"
        exit 1
    fi

    # Capture the current automatic addresses before switching anything to manual.
    local current_ip=$(networksetup -getinfo "$service" | awk '/^IP address:/ {print $3}')
    local subnet_mask=$(networksetup -getinfo "$service" | awk '/^Subnet mask:/ {print $3}')
    local current_ipv6=""

    if [ -n "$target_gateway_ipv6" ]; then
        current_ipv6=$(ifconfig "$device" | awk '/inet6 / && $2 !~ /^fe80:/ && $2 !~ /^fc/ && $2 !~ /^fd/ {print $2; exit}')
    fi

    # Fallback to ifconfig if networksetup fails
    if [ -z "$current_ip" ] || [ "$current_ip" = "none" ]; then
        current_ip=$(ifconfig "$device" | awk '/inet / {print $2}')
        subnet_mask=$(ifconfig "$device" | awk '/inet / {print $4}' | sed 's/0x//')
        if [ -n "$subnet_mask" ]; then
            subnet_mask=$(python3 -c "import socket,struct;print(socket.inet_ntoa(struct.pack('>I', int('$subnet_mask', 16))))")
        fi
    fi

    if [ -z "$current_ip" ] || [ "$current_ip" = "none" ]; then
        echo "ERROR: No IP address assigned"
        exit 1
    fi

    # Set manual IPv4 with new gateway
    echo "Setting IPv4: $current_ip/$subnet_mask, gateway: $target_gateway"
    networksetup -setmanual "$service" "$current_ip" "$subnet_mask" "$target_gateway"

    # Set IPv6 if configured
    if [ -n "$target_gateway_ipv6" ]; then
        if [ -n "$current_ipv6" ]; then
            echo "Setting IPv6: $current_ipv6/64, gateway: $target_gateway_ipv6"
            networksetup -setv6manual "$service" "$current_ipv6" 64 "$target_gateway_ipv6"
        else
            echo "WARNING: No IPv6 address found, skipping IPv6 configuration"
        fi
    fi

    # Set DNS servers
    echo "Setting DNS servers..."
    if [ -n "$target_gateway_ipv6" ]; then
        networksetup -setdnsservers "$service" "$target_gateway" "$target_gateway_ipv6"
    else
        networksetup -setdnsservers "$service" "$target_gateway"
    fi

    # Flush DNS cache
    sudo dscacheutil -flushcache
    sudo killall -HUP mDNSResponder 2>/dev/null || true

    echo "Gateway enabled successfully"
}

# Restore to DHCP/Automatic configuration
restore_gateway() {
    local service=$(get_wifi_service)

    if [ -z "$service" ]; then
        echo "ERROR: No Wi-Fi service found"
        exit 1
    fi

    echo "Restoring to automatic configuration..."

    # Restore IPv4 to DHCP
    echo "Setting IPv4 to DHCP..."
    networksetup -setdhcp "$service"

    # Restore IPv6 to Automatic
    echo "Setting IPv6 to Automatic..."
    networksetup -setv6automatic "$service"

    # Clear DNS (will follow DHCP)
    echo "Clearing DNS servers..."
    networksetup -setdnsservers "$service" "Empty"

    # Flush DNS cache
    sudo dscacheutil -flushcache
    sudo killall -HUP mDNSResponder 2>/dev/null || true

    echo "Gateway restored successfully"
}

# Test latency to chatgpt.com
test_latency() {
    local start=$(python3 -c 'import time; print(int(time.time() * 1000))')

    if timeout 6 bash -c "cat < /dev/null > /dev/tcp/chatgpt.com/443" 2>/dev/null; then
        local end=$(python3 -c 'import time; print(int(time.time() * 1000))')
        local elapsed=$((end - start))
        echo "$elapsed"
    else
        echo "-1"
    fi
}

# Main command dispatcher
case "$1" in
    init)
        init_config
        ;;
    get-config)
        get_config "$2"
        ;;
    set-config)
        set_config "$2" "$3"
        ;;
    get-ssid)
        get_current_ssid
        ;;
    is-active)
        is_gateway_active "$(get_config gateway_ipv4)"
        ;;
    enable)
        enable_gateway
        ;;
    restore)
        restore_gateway
        ;;
    test-latency)
        test_latency
        ;;
    *)
        echo "Usage: $0 {init|get-config|set-config|get-ssid|is-active|enable|restore|test-latency}"
        exit 1
        ;;
esac
