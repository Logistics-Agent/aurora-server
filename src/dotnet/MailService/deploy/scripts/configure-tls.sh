#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Automated TLS Certificate Provisioning via Cloudflare DNS-01
# Gate 6E: TLS-READY (Phase P)
# ==============================================================================
set -euo pipefail

MAIL_DOMAIN="${1:-aurora.vn}"
MAIL_HOST="${2:-mail.${MAIL_DOMAIN}}"
CF_CREDENTIALS_FILE="/opt/aurora/mail/config/cloudflare.ini"
CERTS_DIR="/opt/aurora/mail/certs"

mkdir -p "${CERTS_DIR}" "$(dirname "${CF_CREDENTIALS_FILE}")"
chmod 700 "${CERTS_DIR}"

echo "======================================================================"
echo ">> Provisioning TLS Certificate for: ${MAIL_HOST}"
echo "   Method: Cloudflare DNS-01 Challenge (No Port 80/HTTP-01 Required)"
echo "   Target Path: ${CERTS_DIR}"
echo "======================================================================"

# 1. Verify Cloudflare API Token Credentials File
if [[ ! -f "${CF_CREDENTIALS_FILE}" ]]; then
    echo "[-] Error: Cloudflare credentials file '${CF_CREDENTIALS_FILE}' not found!" >&2
    echo "    Please create ${CF_CREDENTIALS_FILE} with:" >&2
    echo "    dns_cloudflare_api_token = <YOUR_SCOPED_ZONE_DNS_EDIT_TOKEN>" >&2
    echo "    Then chmod 600 ${CF_CREDENTIALS_FILE}" >&2
    exit 1
fi

chmod 600 "${CF_CREDENTIALS_FILE}"

# 2. Check & Install Certbot and certbot-dns-cloudflare plugin
if ! command -v certbot >/dev/null 2>&1; then
    echo "[+] Installing certbot and certbot-dns-cloudflare plugin via apt..."
    apt-get update && apt-get install -y certbot python3-certbot-dns-cloudflare
fi

# 3. Request / Renew Certificate using DNS-01 (Wildcard & Mail Host)
echo "[+] Requesting Let's Encrypt TLS Certificate via Cloudflare DNS-01..."
certbot certonly \
    --dns-cloudflare \
    --dns-cloudflare-credentials "${CF_CREDENTIALS_FILE}" \
    --dns-cloudflare-propagation-seconds 20 \
    --non-interactive \
    --agree-tos \
    --email "admin@${MAIL_DOMAIN}" \
    -d "${MAIL_HOST}" \
    --keep-until-expiring

# 4. Copy to Stalwart Volume Path
if [[ -f "/etc/letsencrypt/live/${MAIL_HOST}/fullchain.pem" ]]; then
    cp -L "/etc/letsencrypt/live/${MAIL_HOST}/fullchain.pem" "${CERTS_DIR}/fullchain.pem"
    cp -L "/etc/letsencrypt/live/${MAIL_HOST}/privkey.pem" "${CERTS_DIR}/privkey.pem"

    chmod 644 "${CERTS_DIR}/fullchain.pem"
    chmod 600 "${CERTS_DIR}/privkey.pem"

    echo "[✓] TLS Certificate successfully copied to ${CERTS_DIR}"
fi

# 5. Register Automated Renewal Deploy Hook
RENEWAL_SCRIPT="/opt/aurora/mail/scripts/renew-tls-hook.sh"
cat << 'EOF' > "${RENEWAL_SCRIPT}"
#!/usr/bin/env bash
set -euo pipefail

MAIL_HOST="mail.aurora.vn"
CERTS_DIR="/opt/aurora/mail/certs"

if [ -f "/etc/letsencrypt/live/${MAIL_HOST}/fullchain.pem" ]; then
    cp -L "/etc/letsencrypt/live/${MAIL_HOST}/fullchain.pem" "${CERTS_DIR}/fullchain.pem"
    cp -L "/etc/letsencrypt/live/${MAIL_HOST}/privkey.pem" "${CERTS_DIR}/privkey.pem"
    chmod 644 "${CERTS_DIR}/fullchain.pem"
    chmod 600 "${CERTS_DIR}/privkey.pem"

    # Reload Stalwart container certificates seamlessly without closing active connections
    if docker ps --format '{{.Names}}' | grep -q "aurora-stalwart"; then
        docker kill -s SIGHUP aurora-stalwart || true
        echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Stalwart TLS certificates reloaded."
    fi
fi
EOF

chmod 755 "${RENEWAL_SCRIPT}"

# Daily cron at 03:30 UTC
CRON_JOB="30 3 * * * certbot renew --dns-cloudflare --dns-cloudflare-credentials ${CF_CREDENTIALS_FILE} --quiet --deploy-hook ${RENEWAL_SCRIPT}"
if ! crontab -l 2>/dev/null | grep -q "renew-tls-hook.sh"; then
    (crontab -l 2>/dev/null; echo "${CRON_JOB}") | crontab -
    echo "[+] Daily TLS renewal cron job registered."
fi

echo "======================================================================"
echo "[✓] Gate 6E: Cloudflare DNS-01 TLS Automation Ready."
echo "======================================================================"
