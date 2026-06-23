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
    python3 -c "import json; print(json.load(open('$CONFIG_FILE')).get('$key', ''))"
}

# Set config value
set_config() {
    local key="$1"
    local value="$2"
    init_config
    python3 -c "
import json
with open('$CONFIG_FILE', 'r') as f:
    config = json.load(f)
config['$key'] = '$value'
with open('$CONFIG_FILE', 'w') as f:
    json.dump(config, f, indent=2)
"
}

# Get active Wi-Fi interface
get_wifi_interface() {
    networksetup -listallhardwareports | awk '/Wi-Fi|AirPort/{getline; print $2}'
}

# Get current SSID
get_current_ssid() {
    local interface=$(get_wifi_interface)
    if [ -z "$interface" ]; then
        echo ""
        return
    fi
    /System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport -I | awk '/ SSID/ {print $2}'
}

# Get current gateway
get_current_gateway() {
    local interface="$1"
    networksetup -getinfo "$interface" | awk '/^Router:/ {print $2}'
}

# Get current DNS servers
get_current_dns() {
    local interface="$1"
    networksetup -getdnsservers "$interface" | grep -v "There aren't any DNS Servers" | tr '\n' ',' | sed 's/,$//'
}

# Check if using DHCP for DNS
is_dns_dhcp() {
    local interface="$1"
    local dns=$(networksetup -getdnsservers "$interface")
    [[ "$dns" == *"aren't any DNS Servers"* ]] && echo "true" || echo "false"
}

# Save current network configuration
save_backup() {
    local interface="$1"
    local gateway=$(get_current_gateway "$interface")
    local dns=$(get_current_dns "$interface")
    local is_dhcp=$(is_dns_dhcp "$interface")

    cat > "$BACKUP_FILE" <<EOF
{
  "interface": "$interface",
  "gateway": "$gateway",
  "dns": "$dns",
  "is_dhcp": $is_dhcp
}
EOF
}

# Check if gateway is active
is_gateway_active() {
    local target_gateway="$1"
    local interface=$(get_wifi_interface)

    if [ -z "$interface" ]; then
        echo "false"
        return
    fi

    local current_gateway=$(get_current_gateway "$interface")
    local current_dns=$(get_current_dns "$interface")

    if [ "$current_gateway" = "$target_gateway" ] && [[ "$current_dns" == *"$target_gateway"* ]]; then
        echo "true"
    else
        echo "false"
    fi
}

# Enable gateway
enable_gateway() {
    local target_gateway=$(get_config "gateway_ipv4")
    local interface=$(get_wifi_interface)

    if [ -z "$interface" ]; then
        echo "ERROR: No Wi-Fi interface found"
        exit 1
    fi

    # Save current configuration
    save_backup "$interface"

    # Get current IP address and subnet
    local current_ip=$(networksetup -getinfo "$interface" | awk '/^IP address:/ {print $3}')
    local subnet_mask=$(networksetup -getinfo "$interface" | awk '/^Subnet mask:/ {print $3}')

    if [ -z "$current_ip" ] || [ "$current_ip" = "none" ]; then
        echo "ERROR: No IP address assigned"
        exit 1
    fi

    # Set manual IP with new gateway
    networksetup -setmanual "$interface" "$current_ip" "$subnet_mask" "$target_gateway"

    # Set DNS to gateway
    networksetup -setdnsservers "$interface" "$target_gateway"

    # Flush DNS cache
    sudo dscacheutil -flushcache
    sudo killall -HUP mDNSResponder 2>/dev/null || true

    echo "Gateway enabled successfully"
}

# Restore original configuration
restore_gateway() {
    if [ ! -f "$BACKUP_FILE" ]; then
        echo "ERROR: No backup found"
        exit 1
    fi

    local interface=$(python3 -c "import json; print(json.load(open('$BACKUP_FILE'))['interface'])")
    local gateway=$(python3 -c "import json; print(json.load(open('$BACKUP_FILE'))['gateway'])")
    local dns=$(python3 -c "import json; print(json.load(open('$BACKUP_FILE'))['dns'])")
    local is_dhcp=$(python3 -c "import json; print(json.load(open('$BACKUP_FILE'))['is_dhcp'])")

    # Get current IP and subnet
    local current_ip=$(networksetup -getinfo "$interface" | awk '/^IP address:/ {print $3}')
    local subnet_mask=$(networksetup -getinfo "$interface" | awk '/^Subnet mask:/ {print $3}')

    if [ -n "$gateway" ] && [ "$gateway" != "none" ]; then
        networksetup -setmanual "$interface" "$current_ip" "$subnet_mask" "$gateway"
    fi

    # Restore DNS
    if [ "$is_dhcp" = "true" ]; then
        networksetup -setdnsservers "$interface" "Empty"
    else
        IFS=',' read -ra DNS_ARRAY <<< "$dns"
        if [ ${#DNS_ARRAY[@]} -gt 0 ]; then
            networksetup -setdnsservers "$interface" "${DNS_ARRAY[@]}"
        fi
    fi

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
