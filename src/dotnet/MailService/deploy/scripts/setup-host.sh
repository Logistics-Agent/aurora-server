#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Mini PC Host Setup & Security Hardening
# Target OS: Ubuntu 24.04 LTS (linux-x64)
# Gate 6A: HOST-READY
# ==============================================================================
set -euo pipefail

SSH_ALLOWED_CIDR="${1:-}" # Optional: Restrict SSH to specific subnet, e.g. 192.168.1.0/24 or 100.64.0.0/10 (Tailscale)

echo "======================================================================"
echo ">> Setting up Aurora Mail Platform Host (Ubuntu 24.04 LTS Production Mode)"
echo "======================================================================"

# 1. Require root execution
if [[ $EUID -ne 0 ]]; then
   echo "[-] Error: This script must be run as root (sudo)." >&2
   exit 1
fi

# 2. Configure Timezone to UTC and enable NTP
echo "[+] Configuring Timezone and NTP synchronization..."
timedatectl set-timezone UTC
timedatectl set-ntp true

# 3. Kernel and System Tuning for High-Concurrency Mail Processing
echo "[+] Applying sysctl network, socket, and memory limits..."
cat << 'EOF' > /etc/sysctl.d/99-aurora-mail.conf
# File descriptor limits
fs.file-max = 2097152

# Virtual memory max map count for high-concurrency runtimes
vm.max_map_count = 262144

# Socket backlog and connection queue limits
net.core.somaxconn = 65535
net.core.netdev_max_backlog = 16384
net.ipv4.tcp_max_syn_backlog = 8192

# TCP buffer tuning
net.core.rmem_max = 16777216
net.core.wmem_max = 16777216
net.ipv4.tcp_rmem = 4096 87380 16777216
net.ipv4.tcp_wmem = 4096 65536 16777216

# Ephemeral port range
net.ipv4.ip_local_port_range = 1024 65535

# Fast socket reuse
net.ipv4.tcp_tw_reuse = 1
net.ipv4.tcp_fin_timeout = 15
EOF

sysctl --system > /dev/null

# 4. Configure Security Limits (/etc/security/limits.d/)
echo "[+] Configuring user file limits..."
cat << 'EOF' > /etc/security/limits.d/99-aurora-mail.conf
*       soft    nofile      65536
*       hard    nofile      1048576
*       soft    nproc       32768
*       hard    nproc       65536
root    soft    nofile      65536
root    hard    nofile      1048576
EOF

# 5. Create Production Filesystem Layout
echo "[+] Creating production directory hierarchy..."
INSTALL_DIR="/opt/aurora/mail"
mkdir -p "${INSTALL_DIR}"/{config,certs,data,backups,scripts,logs,bin}
mkdir -p "${INSTALL_DIR}/data"/{stalwart,rabbitmq,redis,clamav}

# Set tight permissions (only root / docker services have access)
chmod 700 "${INSTALL_DIR}"
chmod 700 "${INSTALL_DIR}"/{config,certs,backups}
chmod 755 "${INSTALL_DIR}"/scripts

# 6. Configure UFW Firewall (Strict Inbound Filter)
if command -v ufw >/dev/null 2>&1; then
    echo "[+] Configuring UFW Firewall..."
    ufw default deny incoming
    ufw default allow outgoing

    # SSH Management rule
    if [[ -n "${SSH_ALLOWED_CIDR}" ]]; then
        echo "[+] Restricting SSH (port 22) to CIDR: ${SSH_ALLOWED_CIDR}"
        ufw allow from "${SSH_ALLOWED_CIDR}" to any port 22 proto tcp comment "SSH Management restricted"
    else
        echo "[!] Allowing SSH (port 22) from all interfaces. Recommend specifying subnet CIDR."
        ufw allow 22/tcp comment "SSH Management"
    fi

    # Allow Public Internet Mail Traffic
    ufw allow 25/tcp comment "SMTP Inbound (Internet Mail)"
    ufw allow 587/tcp comment "SMTP Submission (STARTTLS)"
    ufw allow 993/tcp comment "IMAPS (Mail Access)"
    ufw allow 4190/tcp comment "ManageSieve (Optional Filter Management)"

    # Strictly Deny / Block all internal infrastructure ports from WAN
    ufw deny 5003/tcp comment "Block Mail gRPC from WAN"
    ufw deny 9090/tcp comment "Block Mail Metrics from WAN"
    ufw deny 5672/tcp comment "Block RabbitMQ from WAN"
    ufw deny 15672/tcp comment "Block RabbitMQ Admin from WAN"
    ufw deny 6379/tcp comment "Block Redis from WAN"
    ufw deny 3310/tcp comment "Block ClamAV from WAN"
    ufw deny 783/tcp comment "Block SpamAssassin from WAN"
    ufw deny 8080/tcp comment "Block Stalwart Admin API from WAN"

    echo "y" | ufw enable || true
    echo "[+] UFW status:"
    ufw status verbose
fi

# 7. Configure Docker Auto-Start on System Boot
if command -v systemctl >/dev/null 2>&1; then
    echo "[+] Enabling Docker service auto-start..."
    systemctl enable docker || true
    systemctl enable containerd || true
fi

echo "======================================================================"
echo "[✓] Gate 6A: HOST-READY completed successfully."
echo "    Production directory initialized at: ${INSTALL_DIR}"
echo "======================================================================"
